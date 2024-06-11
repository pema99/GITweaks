// Uses ideas from https://www.sebastiansylvan.com/post/LeastSquaresTextureSeams/
// and https://gist.github.com/ssylvan/18fb6875824c14aa2b8c by Sebastian Sylvan, provided under MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace GITweaks
{
    [CustomEditor(typeof(GITweaksSeamFix))]
    public class GITweaksSeamFixEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sf = target as GITweaksSeamFix;
            if (target == null)
                return;

            base.OnInspectorGUI(); // TODO: Better inspector

            serializedObject.Update();

            //var sp = serializedObject.FindProperty(nameof(GITweaksSeamFix.RenderersToFixSeamsWith));
            //EditorGUILayout.PropertyField(sp);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview fix"))
            {
                GITweaksUtils.RefreshLDA();
                GITweaksSeamFixer.FixSeams(sf, false);
            }
            if (GUILayout.Button("Reset preview"))
            {
                GITweaksUtils.RefreshLDA();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply fix"))
            {
                GITweaksUtils.RefreshLDA();
                DelayApplyTarget = sf;
                EditorApplication.update += DelayApply;
            }

            if (EditorGUILayout.LinkButton("Open documentation"))
                Application.OpenURL("https://github.com/pema99/GITweaks/tree/master?tab=readme-ov-file#fix-lightmap-seams-between-objects");

            serializedObject.ApplyModifiedProperties();
        }

        private static GITweaksSeamFix DelayApplyTarget;
        private static void DelayApply()
        {
            if (DelayApplyTarget != null)
                GITweaksSeamFixer.FixSeams(DelayApplyTarget, true);
            EditorApplication.update -= DelayApply;
        }
    }

    [CustomEditor(typeof(GITweaksSeamFixVolume))]
    public class GITweaksSeamFixVolumeEditor : Editor
    {
        [MenuItem("GameObject/Light/GI Tweaks Seam Fix Volume")]
        public static void AddVolume()
        {
            GameObject go = new GameObject("Seam Fix Volume");
            go.AddComponent<GITweaksSeamFixVolume>();
            Selection.activeGameObject = go;
        }

        public override void OnInspectorGUI()
        {
            var sfv = target as GITweaksSeamFixVolume;
            if (target == null)
                return;

            base.OnInspectorGUI(); // TODO: Better inspector

            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview fix"))
            {
                GITweaksUtils.RefreshLDA();
                GITweaksSeamFixer.FixSeams(sfv, false);
            }
            if (GUILayout.Button("Reset preview"))
            {
                GITweaksUtils.RefreshLDA();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply fix"))
            {
                GITweaksUtils.RefreshLDA();
                DelayApplyTarget = sfv;
                EditorApplication.update += DelayApply;
            }

            if (EditorGUILayout.LinkButton("Open documentation"))
                Application.OpenURL("https://github.com/pema99/GITweaks/tree/master?tab=readme-ov-file#fix-lightmap-seams-between-objects");

            serializedObject.ApplyModifiedProperties();
        }

        private static GITweaksSeamFixVolume DelayApplyTarget;
        private static void DelayApply()
        {
            if (DelayApplyTarget != null)
                GITweaksSeamFixer.FixSeams(DelayApplyTarget, true);
            EditorApplication.update -= DelayApply;
        }
    }

    public static class GITweaksSeamFixer
    {
        public struct SamplePoint
        {
            public Vector3 vertex;
            public Vector3 normal;
            public Vector2 uv;
        }

        public struct PixelInfo
        {
            public Vector2Int position;
            public Color color;
            public int lightmapIndex;
        }

        private static float GetSamplesPerMeter(MeshRenderer mr)
        {
            float resolution = Lightmapping.lightingSettingsDefaults.lightmapResolution;
            if (Lightmapping.TryGetLightingSettings(out var settings))
                resolution = settings.lightmapResolution;
            return mr.scaleInLightmap * resolution;
        }

        private static List<SamplePoint> GenerateSamplePoints(MeshRenderer selfMr, Bounds? bounds)
        {
            var mesh = selfMr.GetComponent<MeshFilter>().sharedMesh;

            // Get attributes
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var l2w = selfMr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = l2w.MultiplyPoint3x4(verts[i]);
                normals[i] = l2w.MultiplyVector(normals[i]);
            }
            var uvs = mesh.uv2;
            if (uvs == null || uvs.Length == 0) uvs = mesh.uv;
            var indices = mesh.triangles;

            // Find edges
            Dictionary<(int indexA, int indexB), int> edgeRefs = new Dictionary<(int indexA, int indexB), int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                for (int j = 0; j < 3; j++)
                {
                    (int a, int b) edge = (indices[i + j], indices[i + ((j + 1) % 3)]);
                    if (edge.a > edge.b) edge = (edge.b, edge.a);
                    if (!edgeRefs.ContainsKey(edge))
                        edgeRefs.Add(edge, 0);
                    edgeRefs[edge]++;
                }
            }
            var edges = edgeRefs
                .Where(x => x.Value == 1)
                .Select(x => x.Key)
                .ToArray();

            float samplesPerMeter = GetSamplesPerMeter(selfMr) * 1.5f; // Add a bit of bias, just above sqrt(2)

            // Generate samples along them
            List<SamplePoint> selfSamplePoints = new List<SamplePoint>();
            for (int i = 0; i < edges.Length; i++)
            {
                Vector3 vertA = verts[edges[i].indexA];
                Vector3 vertB = verts[edges[i].indexB];
                Vector3 normalA = normals[edges[i].indexA].normalized;
                Vector3 normalB = normals[edges[i].indexB].normalized;
                Vector2 uvA = uvs[edges[i].indexA];
                Vector2 uvB = uvs[edges[i].indexB];

                float length = Vector3.Distance(vertA, vertB);
                int numSamples = Mathf.Max(3, Mathf.CeilToInt(length * samplesPerMeter));
                for (int j = 0; j < numSamples; j++)
                {
                    float t = (float)j / (float)(numSamples - 1);
                    selfSamplePoints.Add(new SamplePoint
                    {
                        vertex = Vector3.Lerp(vertA, vertB, t),
                        normal = Vector3.Lerp(normalA, normalB, t).normalized, // TODO: Slerp ?
                        uv = Vector2.Lerp(uvA, uvB, t)
                    });
                }
            }

            if (bounds != null)
            {
                selfSamplePoints.RemoveAll(x => !bounds.Value.Contains(x.vertex));
            }

            return selfSamplePoints;
        }

        private static Vector2 UVToLightmap(Vector2 uv, Vector4 st, int lightmapWidth, int lightmapHeight)
        {
            Vector2 instanceUV = uv;
            instanceUV *= new Vector2(st.x, st.y);
            instanceUV += new Vector2(st.z, st.w);

            Vector2 lightmapUV = new Vector2(instanceUV.x * lightmapWidth, instanceUV.y * lightmapHeight);
            lightmapUV -= Vector2.one * 0.5f;

            return lightmapUV;
        }

        public static void FixSeams(GITweaksSeamFix sf, bool saveToDisk)
        {
            foreach (var other in sf.RenderersToFixSeamsWith)
            {
                FixSeams(
                    sf.GetComponent<MeshRenderer>(),
                    other,
                    saveToDisk,
                    null,
                    sf.MaxSurfaceAngle,
                    sf.MaxSolverIterationCount,
                    sf.SolverTolerance,
                    sf.SeamFixStrength);
            }

            if (GITweaksSettingsWindow.IsEnabled(GITweak.Logging) && saveToDisk)
                Debug.Log($"[GITweaks] Finished applying seam fixes for GameObject \"{sf.gameObject.name}\"");
        }

        public static void FixSeams(GITweaksSeamFixVolume sfv, bool saveToDisk)
        {
            var bounds = new Bounds(sfv.transform.position, sfv.transform.lossyScale);

            var mrs = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var filtered = new List<MeshRenderer>();
            foreach (var mr in mrs)
            {
                if (bounds.Intersects(mr.bounds) && !sfv.RenderersToExclude.Contains(mr))
                {
                    filtered.Add(mr);
                }
            }

            for (int i = 0; i < filtered.Count; i++)
            {
                MeshRenderer self = filtered[i];
                for (int j = i + 1; j < filtered.Count; j++)
                {
                    MeshRenderer other = filtered[j];
                    FixSeams(
                        self,
                        other,
                        saveToDisk,
                        bounds,
                        sfv.MaxSurfaceAngle,
                        sfv.MaxSolverIterationCount,
                        sfv.SolverTolerance,
                        sfv.SeamFixStrength);
                }
            }

            if (GITweaksSettingsWindow.IsEnabled(GITweak.Logging) && saveToDisk)
                Debug.Log($"[GITweaks] Finished applying seam fixes for volume. {filtered.Count} MeshRenderers were taken into account.");
        }

        public static List<(SamplePoint self, SamplePoint other)> GenerateSamplePairs(
            MeshRenderer selfMr,
            MeshRenderer otherMr,
            float maxSearchAngle,
            Bounds? bounds)
        {
            var result = new List<(SamplePoint, SamplePoint)>();
            if (!GITweaksUtils.IsLightmapped(selfMr) || !GITweaksUtils.IsLightmapped(otherMr))
                return result;

            var selfSamples = GenerateSamplePoints(selfMr, bounds);
            var otherSamples = GenerateSamplePoints(otherMr, bounds);

            float selfSamplesPerMeter = GetSamplesPerMeter(selfMr);
            float otherSamplesPerMeter = GetSamplesPerMeter(otherMr);
            float maxSamplesPerMeter = Mathf.Min(selfSamplesPerMeter, otherSamplesPerMeter);
            float sameDist = (1.0f / maxSamplesPerMeter) * 0.5f;

            var otherHashGrid = new Dictionary<Vector3Int, List<SamplePoint>>();
            foreach (var otherSample in otherSamples)
            {
                Vector3Int quantized = Vector3Int.FloorToInt(otherSample.vertex / sameDist);
                if (!otherHashGrid.TryGetValue(quantized, out var cell))
                    otherHashGrid[quantized] = cell = new List<SamplePoint>();
                cell.Add(otherSample);
            }

            // Find sample pairs
            foreach (var selfSample in selfSamples)
            {
                Vector3Int quantized = Vector3Int.FloorToInt(selfSample.vertex / sameDist);

                for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                {
                    Vector3Int cellPos = quantized + new Vector3Int(x - 1, y - 1, z - 1);
                    if (otherHashGrid.TryGetValue(cellPos, out var closeSamples))
                    {
                        foreach (var otherSample in closeSamples)
                        {
                            if (Vector3.Distance(selfSample.vertex, otherSample.vertex) <= sameDist &&
                                Vector3.Angle(selfSample.normal, otherSample.normal) < maxSearchAngle)
                            {
                                result.Add((selfSample, otherSample));
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static void FixSeams(
            MeshRenderer selfMr,
            MeshRenderer otherMr,
            bool saveToDisk,
            Bounds? bounds,
            float maxSearchAngle,
            int solveIterations,
            float solveTolerance,
            float edgeConstraintWeight)
        {
            if (!GITweaksUtils.IsLightmapped(selfMr) || !GITweaksUtils.IsLightmapped(otherMr) || LightmapSettings.lightmaps.Length == 0)
                return;

            // Generate samples
            var samplePairs = GenerateSamplePairs(selfMr, otherMr, maxSearchAngle, bounds);
            // TODO: Kernel around each sample

            // Get writable lightmaps
            Texture2D selfLightmap;
            Texture2D otherLightmap;
            Color[] selfColors;
            Color[] otherColors;
            if (selfMr.lightmapIndex == otherMr.lightmapIndex)
            {
                selfLightmap = otherLightmap = GetRWLightmap(selfMr);
                selfColors = otherColors = selfLightmap.GetPixels();
            }
            else
            {
                selfLightmap = GetRWLightmap(selfMr);
                otherLightmap = GetRWLightmap(otherMr);
                selfColors = selfLightmap.GetPixels();
                otherColors = otherLightmap.GetPixels();
            }

            // Get bilinear neighborhood of each sample
            List<PixelInfo> pixelInfo = new List<PixelInfo>();
            Dictionary<Vector2Int, int> selfPixelToPixelInfoMap = new Dictionary<Vector2Int, int>();
            Dictionary<Vector2Int, int> otherPixelToPixelInfoMap = new Dictionary<Vector2Int, int>();
            foreach (var samplePoint in samplePairs)
            {
                Vector2 selfUV = samplePoint.self.uv;
                Vector2 selfLightmapUV = UVToLightmap(selfUV, selfMr.lightmapScaleOffset, selfLightmap.width, selfLightmap.height);

                Vector2 otherUV = samplePoint.other.uv;
                Vector2 otherLightmapUV = UVToLightmap(otherUV, otherMr.lightmapScaleOffset, otherLightmap.width, otherLightmap.height);

                for (int i = 0; i < 4; i++)
                {
                    int xOffset = i & 0b01;
                    int yOffset = (i & 0b10) >> 1;

                    Vector2Int offset = new Vector2Int(xOffset, yOffset);
                    Vector2Int selfPos = new Vector2Int((int)selfLightmapUV.x, (int)selfLightmapUV.y) + offset;
                    if (!selfPixelToPixelInfoMap.ContainsKey(selfPos) && selfPos.x < selfLightmap.width && selfPos.y < selfLightmap.height)
                    {
                        int selfIndex = selfPos.y * selfLightmap.width + selfPos.x;
                        //selfColors[selfIndex] = Color.red;
                        pixelInfo.Add(new PixelInfo { color = selfColors[selfIndex], lightmapIndex = selfMr.lightmapIndex, position = selfPos });
                        selfPixelToPixelInfoMap.Add(selfPos, pixelInfo.Count - 1);
                    }

                    Vector2Int otherPos = new Vector2Int((int)otherLightmapUV.x, (int)otherLightmapUV.y) + offset;
                    if (!otherPixelToPixelInfoMap.ContainsKey(otherPos) && otherPos.x < otherLightmap.width && otherPos.y < otherLightmap.height)
                    {
                        int otherIndex = otherPos.y * otherLightmap.width + otherPos.x;
                        //otherColors[otherIndex] = Color.magenta;
                        pixelInfo.Add(new PixelInfo { color = otherColors[otherIndex], lightmapIndex = otherMr.lightmapIndex, position = otherPos });
                        otherPixelToPixelInfoMap.Add(otherPos, pixelInfo.Count - 1);
                    }
                }
            }

            // Setup solver
            int totalPixels = pixelInfo.Count;
            SparseMat AtA = new SparseMat(totalPixels, totalPixels);
            VectorX[] Atbs = { new VectorX(totalPixels), new VectorX(totalPixels), new VectorX(totalPixels) };
            VectorX[] guesses = { new VectorX(totalPixels), new VectorX(totalPixels), new VectorX(totalPixels) };
            SetupLeastSquares(
                selfMr.lightmapScaleOffset,
                otherMr.lightmapScaleOffset,
                selfLightmap.width,
                selfLightmap.height,
                otherLightmap.width,
                otherLightmap.height,
                edgeConstraintWeight,
                samplePairs,
                selfPixelToPixelInfoMap,
                otherPixelToPixelInfoMap,
                pixelInfo,
                AtA,
                Atbs,
                guesses);

            // Solve
            var solutions = new VectorX[3];
            for (int i = 0; i < 3; i++)
            {
                solutions[i] = ConjugateGradientOptimize(AtA, guesses[i], Atbs[i], solveIterations, solveTolerance);
            }

            // Read back pixels
            for (int i = 0; i < totalPixels; i++)
            {
                PixelInfo px = pixelInfo[i];
                Color col = new Color(solutions[0][i], solutions[1][i], solutions[2][i]);
                if (px.lightmapIndex == selfMr.lightmapIndex)
                    selfColors[px.position.y * selfLightmap.width + px.position.x] = col;
                else
                    otherColors[px.position.y * otherLightmap.width + px.position.x] = col;
            }

            // Apply to lightmaps
            selfLightmap.SetPixels(selfColors);
            selfLightmap.Apply();
            otherLightmap.SetPixels(otherColors);
            otherLightmap.Apply();

            var initialLightmaps = LightmapSettings.lightmaps;
            if (saveToDisk)
            {
                // Save and apply importer settings
                MeshRenderer[] mrs = { selfMr, otherMr };
                Texture2D[] newLMs = { selfLightmap, otherLightmap };
                int numLMs = selfMr.lightmapIndex == otherMr.lightmapIndex ? 1 : 2;
                for (int i = 0; i < numLMs; i++)
                {
                    int lmIndex = mrs[i].lightmapIndex;
                    string lmPath = AssetDatabase.GetAssetPath(initialLightmaps[lmIndex].lightmapColor);
                    File.WriteAllBytes(lmPath, newLMs[i].EncodeToEXR());
                    Object.DestroyImmediate(newLMs[i]);
                    AssetDatabase.ImportAsset(lmPath, ImportAssetOptions.ForceSynchronousImport);
                    GITweaksUtils.CopyImporterSettingsAndReimport(initialLightmaps[lmIndex].lightmapColor, lmPath);
                }
            }
            else
            {
                initialLightmaps[selfMr.lightmapIndex].lightmapColor = selfLightmap;
                initialLightmaps[otherMr.lightmapIndex].lightmapColor = otherLightmap;
                LightmapSettings.lightmaps = initialLightmaps;
            }
        }
        private static void BilinearSample(
            Dictionary<Vector2Int, int> pixelToPixelInfoMap,
            Vector2 sample,
            int width,
            int height,
            float weight,
            int[] outIxs,
            float[] outWeights)
        {
            int truncu = (int)sample.x;
            int truncv = (int)sample.y;

            int[] xs = { truncu, truncu + 1, truncu + 1, truncu };
            int[] ys = { truncv, truncv, truncv + 1, truncv + 1 };
            for (int i = 0; i < 4; ++i)
            {
                int x = Mathf.Clamp(xs[i], 0, width);
                int y = Mathf.Clamp(ys[i], 0, height);
                outIxs[i] = pixelToPixelInfoMap[new Vector2Int(x, y)];
            }

            float fracX = sample.x - truncu;
            float fracY = sample.y - truncv;
            outWeights[0] = (1.0f - fracX) * (1.0f - fracY);
            outWeights[1] = fracX * (1.0f - fracY);
            outWeights[2] = fracX * fracY;
            outWeights[3] = (1.0f - fracX) * fracY;
            for (int i = 0; i < 4; ++i)
            {
                outWeights[i] *= weight;
            }
        }

        private static void SetupLeastSquares(
            Vector4 selfSt,
            Vector4 otherSt,
            int selfWidth, int selfHeight,
            int otherWidth, int otherHeight,
            float edgeConstraintWeight,
            List<(SamplePoint self, SamplePoint other)> samplePairs,
            Dictionary<Vector2Int, int> selfPixelToPixelInfoMap,
            Dictionary<Vector2Int, int> otherPixelToPixelInfoMap,
            List<PixelInfo> pixelInfo,
            SparseMat AtA,
            VectorX[] Atbs,
            VectorX[] guesses)
        {
            int[] selfIxs = new int[4];
            int[] otherIxs = new int[4];
            float[] selfWeights = new float[4];
            float[] otherWeights = new float[4];
            foreach (var samplePair in samplePairs)
            {
                BilinearSample(
                    selfPixelToPixelInfoMap,
                    UVToLightmap(samplePair.self.uv, selfSt, selfWidth, selfHeight),
                    selfWidth, selfHeight,
                    edgeConstraintWeight,
                    selfIxs, selfWeights);
                BilinearSample(
                    otherPixelToPixelInfoMap,
                    UVToLightmap(samplePair.other.uv, otherSt, otherWidth, otherHeight),
                    otherWidth, otherHeight, edgeConstraintWeight,
                    otherIxs,
                    otherWeights);

                for (int i = 0; i < 4; ++i)
                {
                    for (int j = 0; j < 4; ++j)
                    {
                        // + a*a^t
                        AtA[selfIxs[i], selfIxs[j]] += selfWeights[i] * selfWeights[j];
                        // + b*b^t
                        AtA[otherIxs[i], otherIxs[j]] += otherWeights[i] * otherWeights[j];
                        // - a*b^t
                        AtA[selfIxs[i], otherIxs[j]] -= selfWeights[i] * otherWeights[j];
                        // - b*a^t
                        AtA[otherIxs[i], selfIxs[j]] -= otherWeights[i] * selfWeights[j];
                    }
                }
            }

            for (int i = 0; i < pixelInfo.Count; i++)
            {
                var pixel = pixelInfo[i];

                AtA[i, i] += 1.0f; // equality cost

                Atbs[0][i] += pixel.color.r;
                Atbs[1][i] += pixel.color.g;
                Atbs[2][i] += pixel.color.b;

                guesses[0][i] = pixel.color.r;
                guesses[1][i] = pixel.color.g;
                guesses[2][i] = pixel.color.b;
            }
        }

        private static Texture2D GetRWLightmap(MeshRenderer mr)
        {
            Texture2D lightmap = LightmapSettings.lightmaps[mr.lightmapIndex].lightmapColor;
            return GITweaksUtils.GetRWTextureCopy(lightmap, GraphicsFormat.R16G16B16A16_SFloat);
        }

        private static VectorX ConjugateGradientOptimize(
            SparseMat A,
            VectorX guess,
            VectorX b,
            int numIterations,
            float tolerance)
        {
            int n = guess.Size;
            VectorX p = new VectorX(n), r = new VectorX(n), Ap = new VectorX(n), tmp = new VectorX(n);
            VectorX x = new VectorX(n);
            x.CopyFrom(guess);

            // r = b - A * x;
            SparseMat.Mul(tmp, A, x);
            VectorX.Sub(ref r, b, tmp);

            p.CopyFrom(r);
            float rsq = VectorX.Dot(r, r);
            for (int i = 0; i < numIterations; ++i)
            {
                SparseMat.Mul(Ap, A, p);
                float alpha = rsq / VectorX.Dot(p, Ap);
                VectorX.MulAdd(x, p, alpha, x); // x = x + alpha * p
                VectorX.MulAdd(r, Ap, -alpha, r); // r = r - alpha * Ap
                float rsqNew = VectorX.Dot(r, r);
                if (Mathf.Abs(rsqNew - rsq) < tolerance * n)
                    break;
                float beta = rsqNew / rsq;
                VectorX.MulAdd(p, p, beta, r); // p = r + beta * p
                rsq = rsqNew;
            }

            return x;
        }

        class SparseMat
        {
            public Row[] Rows;
            public int NumRows, NumCols;

            public SparseMat(int numRows, int numCols)
            {
                this.Rows = new Row[numRows];
                this.NumRows = numRows;
                this.NumCols = numCols;
                for (int i = 0; i < numRows; i++)
                {
                    Rows[i] = new Row();
                }
            }

            public float this[int row, int column]
            {
                get
                {
                    return Rows[row][column];
                }
                set
                {
                    Rows[row][column] = value;
                }
            }

            public static void Mul(VectorX outVector, SparseMat A, VectorX x)
            {
                System.Threading.Tasks.Parallel.For(0, A.NumRows, r =>
                {
                    outVector[r] = Dot(x, A.Rows[r]);
                });
            }

            private static float Dot(VectorX x, Row row)
            {
                float sum = 0.0f;
                for (int i = 0; i < row.Size; i++)
                {
                    sum += x[row.Indices[i]] * row.Coefficients[i];
                }
                return sum;
            }

            public class Row
            {
                public int Size = 0;
                public int Capacity = 0;
                public float[] Coefficients;
                public int[] Indices;

                public float this[int column]
                {
                    get
                    {
                        int index = GetColumnIndexAndGrowIfNeeded(column);
                        return Coefficients[index];
                    }
                    set
                    {
                        int index = GetColumnIndexAndGrowIfNeeded(column);
                        Coefficients[index] = value;
                    }
                }

                private void Grow()
                {
                    Capacity = Capacity == 0 ? 16 : Capacity + Capacity / 2;
                    var newCoeffs = new float[Capacity];
                    var newIndices = new int[Capacity];

                    // Copy existing data over
                    if (Coefficients != null)
                    {
                        System.Array.Copy(Coefficients, newCoeffs, Size);
                    }
                    if (Indices != null)
                    {
                        System.Array.Copy(Indices, newIndices, Size);
                    }

                    Coefficients = newCoeffs;
                    Indices = newIndices;
                }

                private int FindClosestIndex(int columnIndex)
                {
                    for (int i = 0; i < Size; ++i)
                    {
                        if (Indices[i] >= columnIndex)
                            return i;
                    }
                    return Size;
                }

                private int GetColumnIndexAndGrowIfNeeded(int column)
                {
                    // Find the element
                    int index = FindClosestIndex(column);
                    if (Size == 0 || index >= Indices.Length || Indices[index] != column) // Add new element
                    {
                        if (Size == Capacity)
                        {
                            Grow();
                        }

                        // Put the new element in the right place, and shift existing elements down by one.
                        float prevCoeff = 0;
                        int prevIndex = column;
                        ++Size;
                        for (int i = index; i < Size; ++i)
                        {
                            float tmpCoeff = Coefficients[i];
                            int tmpIndex = Indices[i];
                            Coefficients[i] = prevCoeff;
                            Indices[i] = prevIndex;
                            prevCoeff = tmpCoeff;
                            prevIndex = tmpIndex;
                        }
                    }
                    return index;
                }
            }
        }

        public class VectorX
        {
            private float[] Data;

            public VectorX(int size)
            {
                Data = new float[size];
            }

            public int Size => Data.Length;

            public float this[int index]
            {
                get => Data[index];
                set => Data[index] = value;
            }

            public void CopyFrom(VectorX other)
            {
                System.Array.Copy(other.Data, this.Data, this.Size);
            }

            public static void Sub(ref VectorX result, VectorX a, VectorX b)
            {
                for (int i = 0; i < a.Size; i++)
                {
                    result[i] = a[i] - b[i];
                }
            }

            public static float Dot(VectorX a, VectorX b)
            {
                float sum = 0;
                for (int i = 0; i < a.Size; i++)
                {
                    sum += a[i] * b[i];
                }
                return sum;
            }

            public static void MulAdd(VectorX outVec, VectorX v, float a, VectorX b)
            {
                for (int i = 0; i < v.Size; ++i)
                {
                    outVec[i] = v[i] * a + b[i];
                }
            }
        }
    }
}