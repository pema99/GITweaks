using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Atlassing = System.Collections.Generic.Dictionary<UnityEngine.Component, (int lightmapIndex, UnityEngine.Vector4 lightmapST)>;

namespace GITweaks
{
    [InitializeOnLoad]
    public static class GITweaksPostBakeOperations
    {
        static GITweaksPostBakeOperations()
        {
            Lightmapping.bakeCompleted -= BakeFinished;
            Lightmapping.bakeCompleted += BakeFinished;

            #if BAKERY_INCLUDED
            var bakeryType = System.Type.GetType("ftRenderLightmap, BakeryEditorAssembly");
            if (bakeryType == null)
                return;
            var evt = bakeryType.GetEvent("OnFinishedFullRender", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (evt == null)
                return;
            evt.RemoveEventHandler(null, (System.EventHandler)BakeryBakeFinished);
            evt.AddEventHandler(null, (System.EventHandler)BakeryBakeFinished);
            #endif
        }

        #if BAKERY_INCLUDED
        private static void BakeryBakeFinished(object sender, System.EventArgs e) => BakeFinished();
        #endif

        private static void BakeFinished()
        {
            if (!GITweaksUtils.IsCurrentSceneBakedWithBakery())
            {
                if (GITweaksSettingsWindow.IsEnabled(GITweak.OptimizeLightmapSizes))
                    RepackAtlasses();

                if (GITweaksSettingsWindow.IsEnabled(GITweak.SharedLODGroupComponents))
                    RearrangeLODs();

                GITweaksUtils.RefreshLDA();
            }

            if (GITweaksSettingsWindow.IsEnabled(GITweak.SeamFixes))
            {
                var seamFixes = Object.FindObjectsByType<GITweaksSeamFix>(FindObjectsSortMode.None);
                var seamFixVolumes = Object.FindObjectsByType<GITweaksSeamFixVolume>(FindObjectsSortMode.None);
                foreach (var seamFix in seamFixes)
                {
                    if (seamFix.RunAfterBaking)
                        GITweaksSeamFixer.FixSeams(seamFix, true);
                }
                foreach (var seamFixVolume in seamFixVolumes)
                {
                    if (seamFixVolume.RunAfterBaking)
                        GITweaksSeamFixer.FixSeams(seamFixVolume, true);
                }
            }
        }

        private static void RearrangeLODs()
        {
            var sharedLODs = Object.FindObjectsByType<GITweaksSharedLOD>(FindObjectsSortMode.None);
            foreach (var sharedLOD in sharedLODs)
            {
                var lods = sharedLOD.GetComponent<LODGroup>().GetLODs();
                if (lods.Length == 0) continue;
                var lod0 = lods[0].renderers.FirstOrDefault(x => x is MeshRenderer) as MeshRenderer;
                if (lod0 == null) continue;

                var mrs = sharedLOD.RenderersToLightmap;
                var lda = GITweaksLightingDataAssetEditor.GetLDAForScene(lod0.gameObject.scene.path);
                GITweaksLightingDataAssetEditor.CopyAtlasSettingsToRenderers(lda, lod0, mrs);
            }
            if (GITweaksSettingsWindow.IsEnabled(GITweak.Logging) && sharedLODs.Length > 0)
                Debug.Log($"[GITweaks] Finished re-arranging {sharedLODs.Length} LOD groups for sharing.");
        }

        class AtlassingCache
        {
            public Dictionary<Component, int> AtlasIndices;
            public List<Vector2Int> AtlasSizes;
            public List<HashSet<Component>> RenderersPerAtlas;
            public Dictionary<Component, Rect> PixelRectsFractional;
            public Dictionary<Component, RectInt> PixelRects;
            public Dictionary<Component, Vector2Int> RendererScale;
            public Dictionary<Component, Rect> UVBounds;

            private AtlassingCache() { }

            public AtlassingCache(Atlassing atlassing, List<Vector2Int> atlasSizes)
            {
                AtlasIndices = atlassing.ToDictionary(x => x.Key, x => x.Value.lightmapIndex);
                AtlasSizes = atlasSizes;
                PixelRectsFractional = new Dictionary<Component, Rect>();
                PixelRects = new Dictionary<Component, RectInt>();
                RendererScale = new Dictionary<Component, Vector2Int>();
                UVBounds = new Dictionary<Component, Rect>();

                RenderersPerAtlas = new List<HashSet<Component>>();
                for (int i = 0; i < atlasSizes.Count; i++)
                {
                    RenderersPerAtlas.Add(new HashSet<Component>());
                }

                foreach ((Component c, (int idx, Vector4 st)) in atlassing)
                {
                    var stRect = new Rect(st.z, st.w, st.x, st.y);
                    var pixelRect = stRect;

                    var uvBounds = GITweaksUtils.ComputeUVBounds(c);
                    pixelRect = GITweaksUtils.STRectToPixelRect(uvBounds, stRect);

                    var fractionalPixelRect = new Rect(atlasSizes[idx] * pixelRect.position, atlasSizes[idx] * pixelRect.size);
                    PixelRectsFractional[c] = fractionalPixelRect;
                    PixelRects[c] = fractionalPixelRect.ToRectInt();
                    RendererScale[c] = Vector2Int.one;
                    UVBounds[c] = uvBounds;
                    RenderersPerAtlas[idx].Add(c);
                }
            }

            public AtlassingCache Copy()
            {
                var copy = new AtlassingCache();
                copy.AtlasIndices = new Dictionary<Component, int>(AtlasIndices);
                copy.AtlasSizes = new List<Vector2Int>(AtlasSizes);
                copy.RenderersPerAtlas = RenderersPerAtlas.Select(x => new HashSet<Component>(x)).ToList();
                copy.PixelRectsFractional = new Dictionary<Component, Rect>(PixelRectsFractional);
                copy.PixelRects = new Dictionary<Component, RectInt>(PixelRects);
                copy.RendererScale = new Dictionary<Component, Vector2Int>(RendererScale);
                copy.UVBounds = new Dictionary<Component, Rect>(UVBounds);
                return copy;
            }
        }

        private static float GetCoveragePercentageInRange(AtlassingCache atlassing, int startIndex, int amount)
        {
            int lightmapArea = 0;
            float pixelRectsArea = 0;

            for (int i = 0; i < amount; i++)
            {
                int lightmapIndex = startIndex + i;

                Vector2Int lightmapSize = atlassing.AtlasSizes[lightmapIndex];
                lightmapArea += lightmapSize.x * lightmapSize.y;

                pixelRectsArea += atlassing.AtlasIndices
                    .Where(x => x.Value == lightmapIndex)
                    .Select(x => atlassing.PixelRects[x.Key].size)
                    .Select(x => x.x * x.y)
                    .Sum();
            }

            return pixelRectsArea / lightmapArea;
        }

        private static float GetCoveragePercentage(AtlassingCache atlassing, int lightmapIndex)
        {
            return GetCoveragePercentageInRange(atlassing, lightmapIndex, 1);
        }

        private static float GetCoveragePercentageInRange(AtlassingCache atlassing)
        {
            return GetCoveragePercentageInRange(atlassing, 0, atlassing.AtlasSizes.Count);
        }

        private static float GetSplitCoveragePercentage(Vector2Int splitAtlasSize, int splitAtlasCount, IEnumerable<(Component key, RectInt rect)> instances)
        {
            int lightmapArea = splitAtlasSize.x * splitAtlasSize.y * splitAtlasCount;
            float pixelRectsArea = 0;

            foreach (var instance in instances)
            {
                pixelRectsArea += instance.rect.width * instance.rect.height;
            }

            return pixelRectsArea / lightmapArea;
        }

        private static bool Pack<K>(
            int width,
            int height,
            int padding,
            IEnumerable<(K key, RectInt rect)> rects,
            out HashSet<(K key, RectInt rect)> packedRects,
            out HashSet<(K key, RectInt rect)> remainder)
        {
            GITweaksTexturePacker packer = new GITweaksTexturePacker(width, height, padding, 0);
            packedRects = new HashSet<(K key, RectInt size)>();
            remainder = rects.ToHashSet();
            var sorted = rects.OrderByDescending(x => x.rect.width * x.rect.height);
            foreach (var instance in sorted)
            {
                if (!packer.Pack(instance.rect.width, instance.rect.height, out var frame))
                    return false;

                packedRects.Add((instance.key, frame));
                remainder.Remove(instance);
            }
            return true;
        }

        // To include bilinear neighborhood
        private static Rect DilateRect(Rect rect)
        {
            rect.position -= Vector2.one*2;
            rect.size += Vector2.one * 2*2;
            return rect;
        }

        private static Vector2Int DilatePosition(Vector2Int pos)
        {
            pos -= Vector2Int.one*2;
            return pos;
        }

        private static void RepackAtlasses()
        {
            // Find textures used before
            var usedBefore = LightmapSettings.lightmaps.SelectMany(x => new [] { x.lightmapColor, x.lightmapDir, x.shadowMask }).ToHashSet();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                RepackAtlassesForScene(scene.path);
            }

            // Now diff lightmaps, delete unused
            GITweaksUtils.RefreshLDA();
            var usedAfter = LightmapSettings.lightmaps.SelectMany(x => new[] { x.lightmapColor, x.lightmapDir, x.shadowMask }).ToHashSet();
            usedBefore.ExceptWith(usedAfter);
            foreach (var asset in usedBefore)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void RepackAtlassesForScene(string scenePath)
        {
            var lda = GITweaksLightingDataAssetEditor.GetLDAForScene(scenePath);
            if (lda == null)
                return;

            var initialLightmaps = GITweaksLightingDataAssetEditor.GetLightmaps(lda);
            if (initialLightmaps.Length == 0)
                return;

            // Find lightmap index offset in case of multiscene
            var firstLightmap = initialLightmaps[0].lightmapColor;
            var globalLightmaps = LightmapSettings.lightmaps;
            int lightmapIndexBase = 0;
            for (int i = 0; i < globalLightmaps.Length; i++)
            {
                if (globalLightmaps[i].lightmapColor == firstLightmap)
                {
                    lightmapIndexBase = i;
                    break;
                }
            }

            bool hasDirectionality = initialLightmaps[0].lightmapDir != null;
            bool hasShadowmask = initialLightmaps[0].shadowMask != null;

            // Settings
            float minCoveragePercent = GITweaksSettingsWindow.LightmapOptimizationTargetCoverage;
            int minLightmapSize = GITweaksSettingsWindow.LightmapOptimizationMinLightmapSize;
            int padding = Mathf.Max(3, Lightmapping.lightingSettings.lightmapPadding);

            var initialAtlassing = GITweaksLightingDataAssetEditor.GetAtlassing(lda);
            if (initialAtlassing == null || initialAtlassing.Count == 0)
                return;

            List<Vector2Int> atlasSizes = new List<Vector2Int>();
            for (int i = 0; i < initialLightmaps.Length; i++)
                atlasSizes.Add(new Vector2Int(initialLightmaps[i].lightmapColor.width, initialLightmaps[i].lightmapColor.height));
            var initialAtlassingCache = new AtlassingCache(initialAtlassing, atlasSizes);
            var atlassingCache = initialAtlassingCache.Copy();

            bool didRepack = false;

            for (int i = 0; i < atlassingCache.AtlasSizes.Count; i++)
            {
                // TODO: Halving

                float coverage = GetCoveragePercentage(atlassingCache, i);
                if (coverage < minCoveragePercent)
                {
                    // Get quadrant size, check it is big enough
                    var splitLightmapSize = atlassingCache.AtlasSizes[i] / 2;
                    if (splitLightmapSize.x < minLightmapSize || splitLightmapSize.y < minLightmapSize)
                        continue;

                    // Get the renderers to repack
                    var renderers = atlassingCache.RenderersPerAtlas[i];
                    var rectsToPack = renderers.Select(x => (x, atlassingCache.PixelRects[x]));

                    // Try to repack 1 smaller quadrant. If we can't fit anything, go to next atlas.
                    Pack(splitLightmapSize.x, splitLightmapSize.y, padding, rectsToPack, out var packedRectsFirst, out var remainder);
                    if (packedRectsFirst.Count == 0)
                        continue;

                    // Repack into smaller quadrants until we either packed every rect, or until we fail to pack anymore
                    var packedRects = new List<HashSet<(Component key, RectInt rect)>>() { packedRectsFirst };
                    while (remainder.Count > 0)
                    {
                        Pack(splitLightmapSize.x, splitLightmapSize.y, padding, remainder, out var packedRectsCont, out remainder);
                        if (packedRectsCont.Count == 0)
                            break;
                        packedRects.Add(packedRectsCont);
                    }

                    // If there is still some remainder, this packing isn't valid, so go to next atlas.
                    if (remainder.Count > 0)
                        continue;

                    // If the coverage is now worse, there is no saving - go to next atlas.
                    if (GetSplitCoveragePercentage(splitLightmapSize, packedRects.Count, rectsToPack) <= coverage)
                        continue;

                    atlassingCache.AtlasSizes.RemoveAt(i);
                    atlassingCache.AtlasSizes.InsertRange(i, packedRects.Select(_ => splitLightmapSize));

                    HashSet<Component>[] newRenderersPerAtlas = packedRects.Select(rects => rects.Select(x => x.key).ToHashSet()).ToArray();
                    atlassingCache.RenderersPerAtlas.RemoveAt(i);
                    atlassingCache.RenderersPerAtlas.InsertRange(i, newRenderersPerAtlas.Take(packedRects.Count));

                    for (int group = 0; group < packedRects.Count; group++)
                    {
                        foreach (var renderer in packedRects[group])
                        {
                            atlassingCache.AtlasIndices[renderer.key] = i + group;
                            atlassingCache.PixelRects[renderer.key] = renderer.rect;
                            atlassingCache.PixelRectsFractional[renderer.key] = renderer.rect.ToRect();
                            atlassingCache.RendererScale[renderer.key] *= 2;
                        }
                    }

                    didRepack = true;

                    // We just replaced an atlas, now we want to re-visit the results of that
                    i--;
                }
            }

            // If we achieved did nothing, just bail
            if (!didRepack)
                return;

            // Create new lightmap textures to render into
            var newLightmapRTs = new (RenderTexture light, RenderTexture dir, RenderTexture shadow)[atlassingCache.AtlasSizes.Count];
            for (int i = 0; i < newLightmapRTs.Length; i++)
            {
                var size = atlassingCache.AtlasSizes[i];
                var light = new RenderTexture(new RenderTextureDescriptor(size.x, size.y) { graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, });
                var dir = hasDirectionality ? new RenderTexture(new RenderTextureDescriptor(size.x, size.y) { graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, enableRandomWrite = true, }) : null;
                var shadow = hasShadowmask ? new RenderTexture(new RenderTextureDescriptor(size.x, size.y) { graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, enableRandomWrite = true, }) : null;
                newLightmapRTs[i] = (light, dir, shadow);
            }

            // Copy renderers over, update their atlassing data
            foreach ((var renderer, int lightmapIndex) in atlassingCache.AtlasIndices)
            {
                var oldAtlasData = initialAtlassing[renderer];
                var newAtlasData = oldAtlasData;

                // Create new atlas data
                var lmSize = atlassingCache.AtlasSizes[lightmapIndex];
                var pixelRectScaled = atlassingCache.PixelRects[renderer];

                Vector2 scale = atlassingCache.RendererScale[renderer];
                newAtlasData.lightmapST = new Vector4(
                    newAtlasData.lightmapST.x * scale.x,
                    newAtlasData.lightmapST.y * scale.y,
                    (float)pixelRectScaled.position.x / lmSize.x,
                    (float)pixelRectScaled.position.y / lmSize.y);
                newAtlasData.lightmapIndex = lightmapIndex;
                GITweaksUtils.OffsetLightmapSTByPixelRectOffset(atlassingCache.UVBounds[renderer], ref newAtlasData.lightmapST);

                // Copy renderer
                int oldLightmapIndex = oldAtlasData.lightmapIndex;
                var oldRect = DilateRect(initialAtlassingCache.PixelRectsFractional[renderer]);
                var newPosition = DilatePosition(atlassingCache.PixelRects[renderer].position);

                Texture2D oldLightmap = initialLightmaps[oldLightmapIndex].lightmapColor;
                RenderTexture newLightmap = newLightmapRTs[lightmapIndex].light;
                GITweaksUtils.CopyFractional(oldLightmap, oldRect, newLightmap, newPosition, PlayerSettings.colorSpace == ColorSpace.Gamma);

                if (hasDirectionality)
                {
                    Texture2D oldDir = initialLightmaps[oldLightmapIndex].lightmapDir;
                    RenderTexture newDir = newLightmapRTs[lightmapIndex].dir;
                    GITweaksUtils.CopyFractional(oldDir, oldRect, newDir, newPosition, false);
                }

                if (hasShadowmask)
                {
                    Texture2D oldShadowmask = initialLightmaps[oldLightmapIndex].shadowMask;
                    RenderTexture newShadowmask = newLightmapRTs[lightmapIndex].shadow;
                    GITweaksUtils.CopyFractional(oldShadowmask, oldRect, newShadowmask, newPosition, false);
                }

                initialAtlassing[renderer] = newAtlasData;
            }

            // Convert to texture2D and import the new lightmaps
            var newLightmaps = new LightmapData[newLightmapRTs.Length];
            for (int i = 0; i < newLightmapRTs.Length; i++)
            {
                newLightmaps[i] = new LightmapData();

                var newPair = newLightmapRTs[i];

                var newColor = GITweaksUtils.RenderTextureToTexture2D(newPair.light);
                newPair.light.Release();
                Object.DestroyImmediate(newPair.light);
                string lmPath = AssetDatabase.GetAssetPath(initialLightmaps[0].lightmapColor).Replace("Lightmap-", $"Lightmap-{i}-");
                File.WriteAllBytes(lmPath, newColor.EncodeToEXR());
                Object.DestroyImmediate(newColor);
                AssetDatabase.ImportAsset(lmPath, ImportAssetOptions.ForceSynchronousImport);
                GITweaksUtils.CopyImporterSettingsAndReimport(initialLightmaps[0].lightmapColor, lmPath);
                newLightmaps[i].lightmapColor = AssetDatabase.LoadAssetAtPath<Texture2D>(lmPath);

                if (hasDirectionality)
                {
                    var newDir = GITweaksUtils.RenderTextureToTexture2D(newPair.dir);
                    newPair.dir.Release();
                    Object.DestroyImmediate(newPair.dir);
                    string dirPath = AssetDatabase.GetAssetPath(initialLightmaps[0].lightmapDir).Replace("Lightmap-", $"Lightmap-{i}-");
                    File.WriteAllBytes(dirPath, newDir.EncodeToPNG());
                    Object.DestroyImmediate(newDir);
                    AssetDatabase.ImportAsset(dirPath, ImportAssetOptions.ForceSynchronousImport);
                    GITweaksUtils.CopyImporterSettingsAndReimport(initialLightmaps[0].lightmapDir, dirPath);
                    newLightmaps[i].lightmapDir = AssetDatabase.LoadAssetAtPath<Texture2D>(dirPath);
                }

                if (hasShadowmask)
                {
                    var newShadow = GITweaksUtils.RenderTextureToTexture2D(newPair.shadow);
                    newPair.shadow.Release();
                    Object.DestroyImmediate(newPair.shadow);
                    string smPath = AssetDatabase.GetAssetPath(initialLightmaps[0].shadowMask).Replace("Lightmap-", $"Lightmap-{lightmapIndexBase}-{i}-");
                    File.WriteAllBytes(smPath, newShadow.EncodeToPNG());
                    Object.DestroyImmediate(newShadow);
                    AssetDatabase.ImportAsset(smPath, ImportAssetOptions.ForceSynchronousImport);
                    GITweaksUtils.CopyImporterSettingsAndReimport(initialLightmaps[0].shadowMask, smPath);
                    newLightmaps[i].shadowMask = AssetDatabase.LoadAssetAtPath<Texture2D>(smPath);
                }
            }

            GITweaksLightingDataAssetEditor.UpdateAtlassing(lda, initialAtlassing);
            GITweaksLightingDataAssetEditor.UpdateLightmaps(lda, newLightmaps);

            if (GITweaksSettingsWindow.IsEnabled(GITweak.Logging))
                Debug.Log($"[GITweaks] Finished re-packing atlasses for scene \"{scenePath}\". " +
                    $"New atlas count: {newLightmaps.Length}. " +
                    $"Old atlas count: {initialLightmaps.Length}. " +
                    $"New coverage: {GetCoveragePercentageInRange(atlassingCache) * 100}%. " +
                    $"Old coverage: {GetCoveragePercentageInRange(initialAtlassingCache) * 100}%. ");
        }
    }
}