using System;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GITweaks
{
    [InitializeOnLoad]
    internal static class GITweaksHarmony
    {
        public static Harmony Instance = new Harmony("pema.dev.gitweaks");

        [HarmonyPatch(typeof(MaterialEditor), nameof(MaterialEditor.PropertiesDefaultGUI))]
        private class ShowGIFlagsMaterialEditorPatch
        {
            static void Postfix(MaterialEditor __instance)
            {
                if (!GITweaksSettingsWindow.IsEnabled(GITweak.LightmapFlagsDropdown))
                    return;

                try
                {
                    Rect r = (Rect)AccessTools.Method(typeof(MaterialEditor), "GetControlRectForSingleLine").Invoke(__instance, new object[0]);

                    AccessTools.Method(typeof(MaterialEditor), "BeginProperty", new System.Type[] { typeof(Rect), System.Type.GetType("UnityEngine.MaterialSerializedProperty, UnityEngine"), typeof(Object[]) })
                        .Invoke(null, new object[] { r, 1 << 1, __instance.targets });

                    EditorGUI.BeginChangeCheck();
                    MaterialGlobalIlluminationFlags flags = (MaterialGlobalIlluminationFlags)EditorGUI.EnumPopup(
                        r,
                        new GUIContent("Lightmap Flags"),
                        (__instance.targets[0] as Material).globalIlluminationFlags,
                        x => Mathf.IsPowerOfTwo((int)((object)x)));
                    if (EditorGUI.EndChangeCheck())
                    {
                        foreach (Material material in __instance.targets)
                            material.globalIlluminationFlags = flags;
                    }

                    AccessTools.Method(typeof(MaterialEditor), "EndProperty").Invoke(null, new object[0]);
                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        [HarmonyPatch]
        private class BetterDefaultLightingSettingsPatch
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Constructor(typeof(LightingSettings));

            [HarmonyPostfix]
            static void Postfix(LightingSettings __instance)
            {
                try
                {
                    if (!GITweaksSettingsWindow.IsEnabled(GITweak.BetterLightingSettingsDefaults))
                        return;

                    __instance.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                    __instance.prioritizeView = false;
                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        [HarmonyPatch]
        private class CloneSkyboxMaterialPatch
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(System.Type.GetType("UnityEditor.LightingEditor, UnityEditor"), "DrawGUI");

            [HarmonyPostfix]
            static void Prefix()
            {
                if (!GITweaksSettingsWindow.IsEnabled(GITweak.NewSkyboxButton))
                    return;

                try
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("New skybox material"))
                    {
                        Material mat = new Material(Shader.Find("Skybox/Procedural"));
                        mat.name = "New Skybox Material";
                        RenderSettings.skybox = mat;
                        ProjectWindowUtil.CreateAsset(mat, (mat.name + ".mat"));
                    }

                    using (new EditorGUI.DisabledScope(RenderSettings.skybox == null))
                        if (GUILayout.Button("Clone skybox material"))
                        {
                            Material mat = new Material(RenderSettings.skybox);
                            mat.name = string.IsNullOrEmpty(RenderSettings.skybox.name) ? "New Lighting Settings" : RenderSettings.skybox.name;
                            RenderSettings.skybox = mat;
                            ProjectWindowUtil.CreateAsset(mat, (mat.name + ".mat"));
                        }

                    EditorGUILayout.EndHorizontal();
                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        [HarmonyPatch]
        private class LightmapPreviewDropdownWindow
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(System.Type.GetType("UnityEditor.LightmapPreviewWindow, UnityEditor"), "DrawPreviewSettings");

            [HarmonyPostfix]
            static void Prefix(object __instance)
            {
                if (!GITweaksSettingsWindow.IsEnabled(GITweak.LightmapPreviewDropdown))
                    return;

                try
                {
                    System.Type thisType = __instance.GetType();
                    int id = (int)AccessTools.Field(thisType, "m_InstanceID").GetValue(__instance);
                    if (id != -1)
                        return;

                    var lightmapIndexField = AccessTools.Field(thisType, "m_LightmapIndex");

                    int lmCount = LightmapSettings.lightmaps.Length;
                    if (lmCount == 0)
                        return;

                    var lms = Enumerable.Range(0, lmCount);
                    EditorGUI.BeginChangeCheck();
                    int newLmIndex = EditorGUILayout.IntPopup((int)lightmapIndexField.GetValue(__instance), lms.Select(x => $"Lightmap {x}").ToArray(), lms.ToArray());
                    if (EditorGUI.EndChangeCheck())
                    {
                        lightmapIndexField.SetValue(__instance, newLmIndex);
                    }

                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        [HarmonyPatch]
        private class ClickableUVChartPatch
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(System.Type.GetType("UnityEditor.LightmapPreviewWindow, UnityEditor"), "DrawPreview");

            private static Rect ResizeRectToFit(Rect rect, Rect to)
            {
                float widthScale = to.width / rect.width;
                float heightScale = to.height / rect.height;
                float scale = Mathf.Min(widthScale, heightScale);

                float width = (int)Mathf.Round((rect.width * scale));
                float height = (int)Mathf.Round((rect.height * scale));

                return new Rect(rect.x, rect.y, width, height);
            }

            private static Rect ScaleRectByZoomableArea(Rect rect, Rect zoomableRect, Rect zoomableShown)
            {
                float x = -(zoomableShown.x / zoomableShown.width) * (rect.x + zoomableRect.width);
                float y = ((zoomableShown.y - (1f - zoomableShown.height)) / zoomableShown.height) * zoomableRect.height;

                float width = rect.width / zoomableShown.width;
                float height = rect.height / zoomableShown.height;

                return new Rect(rect.x + x, rect.y + y, width, height);
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, Rect r)
            {
                if (!GITweaksSettingsWindow.IsEnabled(GITweak.ClickableLightmapCharts))
                    return;

                try
                {
                    System.Type thisType = __instance.GetType();
                    object visTex = AccessTools.Field(thisType, "m_CachedTexture").GetValue(__instance);
                    System.Type visTexType = visTex.GetType();
                    Texture2D texture = (Texture2D)AccessTools.Field(visTexType, "texture").GetValue(visTex);

                    object zoom = AccessTools.Field(thisType, "m_ZoomablePreview").GetValue(__instance);
                    System.Type zoomType = zoom.GetType();
                    Rect drawableArea = (Rect)AccessTools.Property(zoomType, "drawRect").GetValue(zoom);
                    Rect shownArea = (Rect)AccessTools.Property(zoomType, "shownArea").GetValue(zoom);
                    Rect zoomRect = (Rect)AccessTools.Property(zoomType, "rect").GetValue(zoom);

                    Rect textureRect = new Rect(r.x, r.y, texture.width, texture.height);
                    textureRect = ResizeRectToFit(textureRect, drawableArea);
                    textureRect = ScaleRectByZoomableArea(textureRect, zoomRect, shownArea);
                    textureRect.x += 5;
                    textureRect.width -= 5 * 2;
                    textureRect.height -= 2;

                    Event e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 0 && textureRect.Contains(e.mousePosition))
                    {
                        Vector2 uv = (e.mousePosition - textureRect.position) / textureRect.size;
                        uv.y = 1.0f - uv.y;

                        // Bakery might create overlapping UVSTs, so we need to find the smallest overlapping one.
                        GameObject[] cachedTextureObjects = (GameObject[])AccessTools.Field(thisType, "m_CachedTextureObjects").GetValue(__instance);
                        var candidates = new List<(GameObject go, float size)>();
                        foreach (GameObject cachedTextureObject in cachedTextureObjects)
                        {
                            MeshRenderer mr = cachedTextureObject.GetComponent<MeshRenderer>();
                            Vector4 lightmapScaleOffset;
                            if (mr != null)
                            {
                                lightmapScaleOffset = mr.lightmapScaleOffset;
                            }
                            else
                            {
                                Terrain tr = cachedTextureObject.GetComponent<Terrain>();
                                if (tr == null)
                                    continue;
                                lightmapScaleOffset = tr.lightmapScaleOffset;
                            }

                            // Get raw ST rect (with padding)
                            Rect stRect = new Rect(lightmapScaleOffset.z, lightmapScaleOffset.w, lightmapScaleOffset.x, lightmapScaleOffset.y);

                            // If we are in the raw rect, we _might_ be in the pixel rect
                            if (stRect.Contains(uv))
                            {
                                // Scale to uv bounds
                                if (mr != null)
                                {
                                    stRect = GITweaksUtils.STRectToPixelRect(mr, stRect);
                                }

                                if (stRect.Contains(uv))
                                {
                                    candidates.Add((cachedTextureObject, stRect.width * stRect.height));
                                }
                            }
                        }
                        
                        // Find the best candidate
                        GameObject bestChart = null;
                        if (candidates.Count == 1)
                        {
                            bestChart = candidates[0].go;
                        }
                        else if (candidates.Count > 1)
                        {
                            int candidateIndex = -1;

                            try
                            {
                                // Render a mask of UV charts
                                Material mat = Resources.Load<Material>(SystemInfo.supportsConservativeRaster ? "RenderUVMaskConservative" : "RenderUVMask");
                                RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
                                var prevRT = RenderTexture.active;
                                RenderTexture.active = rt;
                                GL.Clear(true, true, Color.black);
                                for (int i = 0; i < candidates.Count; i++)
                                {
                                    var go = candidates[i].go;
                                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                                    MeshFilter mf = go.GetComponent<MeshFilter>();
                                    var mesh = new Mesh();
                                    if (mr == null || mf == null)
                                        continue;

                                    if (!GITweaksUtils.GetMeshAndUVChannel(mr, out var uvs, out var uvIndex))
                                        continue;
                                    mesh.vertices = mf.sharedMesh.vertices;
                                    mesh.uv = uvs;
                                    mesh.triangles = mf.sharedMesh.triangles;
                                    
                                    mat.SetInteger("_CandidateIndex", i + 1); // 1 to avoid writing 0
                                    mat.SetVector("_CandidateST", mr.lightmapScaleOffset);
                                    mat.SetPass(0);
                                    Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
                                    
                                    Object.DestroyImmediate(mesh);
                                }
                                Texture2D readback = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
                                readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                                readback.Apply();
                                RenderTexture.active = prevRT;
                                RenderTexture.ReleaseTemporary(rt);
                                candidateIndex = Mathf.RoundToInt(readback.GetPixel((int)(uv.x * readback.width), (int)(uv.y * readback.height)).r - 1);
                                Object.DestroyImmediate(readback);
                            }
                            catch
                            {
                                // Ignore, fallback to cheap method
                            }
                            
                            if (candidateIndex >= 0)
                            {
                                bestChart = candidates[candidateIndex].go;
                            }
                            else
                            {
                                // Fall back to picking the smallest bounding box
                                bestChart = candidates.OrderBy(x => x.size).FirstOrDefault().go;
                            }
                        }
                        
                        if (bestChart != null)
                        {
                            Selection.activeGameObject = bestChart;
                            EditorGUIUtility.PingObject(bestChart);
                        }
                    }
                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        [HarmonyPatch]
        private class ConvertToProbeLitPatch
        {
            static System.Type MainType = System.Type.GetType("UnityEditor.RendererLightingSettings, UnityEditor");

            [HarmonyTargetMethod]
            static MethodBase TargetMethod() => AccessTools.Method(MainType, "RenderSettings");

            static MethodInfo InjectedMethodInfo = SymbolExtensions.GetMethodInfo((object self) => InjectedMethod(self));

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool first = true;
                foreach (var instruction in instructions)
                {
                    yield return instruction;

                    if (instruction.operand is MethodInfo info && info.Name == "IntPopup")
                    {
                        if (first)
                        {
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Call, InjectedMethodInfo);

                            first = false;
                        }
                    }
                }
            }

            static void InjectedMethod(object self)
            {
                if (GITweaksUtils.IsCurrentSceneBakedWithBakery())
                    return;

                if (!GITweaksSettingsWindow.IsEnabled(GITweak.LightmappedToProbeLit))
                    return;

                try
                {
                    SerializedObject obj = (SerializedObject)AccessTools.Field(MainType, "m_SerializedObject").GetValue(self);
                    if (obj.targetObject is MeshRenderer mr)
                    {
                        if (mr.receiveGI == ReceiveGI.LightProbes && mr.lightmapIndex < 65534)
                        {
                            var rect = EditorGUILayout.GetControlRect();
                            rect.x += 14;
                            rect.width -= 14;
                            if (GUI.Button(rect, "Convert lightmapped to probe-lit"))
                            {
                                var lda = GITweaksLightingDataAssetEditor.GetLDAForScene(mr.gameObject.scene.path);
                                GITweaksLightingDataAssetEditor.MakeRendererProbeLit(lda, mr);
                                GITweaksUtils.RefreshLDA();
                            }
                        }
                    }
                }
                catch
                {
                    // Fail silently, don't want to bother the user
                }
            }
        }

        // TODO: Move this
        static void NewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            // Tweak: Embedded lighting settings
            if (GITweaksSettingsWindow.IsEnabled(GITweak.AutomaticEmbeddedLightingSettings))
            {
                LightingSettings settings = new LightingSettings() { name = "Lighting Settings (Embedded)" };
                Lightmapping.SetLightingSettingsForScene(scene, settings);
            }
        }

        static GITweaksHarmony()
        {
            EditorSceneManager.newSceneCreated -= NewSceneCreated;
            EditorSceneManager.newSceneCreated += NewSceneCreated;

            EditorApplication.update -= WaitThenPatch;
            EditorApplication.update += WaitThenPatch;
        }

        // Wait for a few frames before patching, to let static initializers run.
        private static int WaitFrames = 0;
        private static void WaitThenPatch()
        {
            WaitFrames++;
            if (WaitFrames > 2)
            {
                EditorApplication.update -= WaitThenPatch;
                Instance.PatchAll();
            }
        }
    }
}