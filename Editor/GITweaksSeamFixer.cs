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
                GITweaksLightingDataAssetEditor.RefreshLDA();
                GITweaksSeamFixer.FixSeams(sf, false);
            }
            if (GUILayout.Button("Reset preview"))
            {
                GITweaksLightingDataAssetEditor.RefreshLDA();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply fix"))
            {
                GITweaksSeamFixer.FixSeams(sf, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    public static class GITweaksSeamFixer
    {
        public struct SamplePoint
        {
            public Vector3 vertex;
            //public Vector3 normal; // TODO
            public Vector2 uv;
        }

        public struct PixelInfo
        {
            public Vector2Int position;
            public Color color;
            public int lightmapIndex;
        }

        private static List<SamplePoint> GenerateSamplePoints(MeshRenderer selfMr, float densityMultiplier)
        {
            var mesh = selfMr.GetComponent<MeshFilter>().sharedMesh;

            // Get attributes
            var verts = mesh.vertices;
            var l2w = selfMr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = l2w.MultiplyPoint3x4(verts[i]);
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

            float samplesPerMeter = selfMr.scaleInLightmap * Lightmapping.lightingSettings.lightmapResolution * densityMultiplier;

            // Generate samples along them
            List<SamplePoint> selfSamplePoints = new List<SamplePoint>();
            for (int i = 0; i < edges.Length; i++)
            {
                Vector3 vertA = verts[edges[i].indexA];
                Vector3 vertB = verts[edges[i].indexB];
                Vector2 uvA = uvs[edges[i].indexA];
                Vector2 uvB = uvs[edges[i].indexB];

                float length = Vector3.Distance(vertA, vertB);
                int numSamples = Mathf.CeilToInt(length * samplesPerMeter);
                for (int j = 0; j < numSamples; j++)
                {
                    float t = (float)j / (float)(numSamples - 1);
                    selfSamplePoints.Add(new SamplePoint
                    {
                        vertex = Vector3.Lerp(vertA, vertB, t),
                        uv = Vector2.Lerp(uvA, uvB, t)
                    });
                }
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
                    sf.MaxSearchDistance,
                    sf.SampleDensityMultiplier,
                    sf.MaxSolverIterationCount,
                    sf.SolverTolerance,
                    sf.SolverStrength);
            }
        }

        public static List<(SamplePoint self, SamplePoint other)> GenerateSamplePairs(
            MeshRenderer selfMr,
            MeshRenderer otherMr,
            float maxSearchDistance,
            float sampleDensityMultiplier)
        {
            var result = new List<(SamplePoint, SamplePoint)>();
            if (selfMr.lightmapIndex >= 65534 || otherMr.lightmapIndex >= 65534)
                return result;

            var selfSamples = GenerateSamplePoints(selfMr, sampleDensityMultiplier);
            var otherSamples = GenerateSamplePoints(otherMr, sampleDensityMultiplier);

            // Find sample pairs
            foreach (var selfSample in selfSamples)
            {
                foreach (var otherSample in otherSamples)
                {
                    if (Vector3.Distance(selfSample.vertex, otherSample.vertex) <= maxSearchDistance)
                    {
                        result.Add((selfSample, otherSample));
                    }
                }
            }

            return result;
        }

        public static void FixSeams(
            MeshRenderer selfMr,
            MeshRenderer otherMr,
            bool saveToDisk,
            float maxSearchDistance,
            float sampleDensityMultiplier,
            int solveIterations,
            float solveTolerance,
            float edgeConstraintWeight)
        {
            if (selfMr.lightmapIndex >= 65534 || otherMr.lightmapIndex >= 65534)
                return;

            // Generate samples
            var samplePairs = GenerateSamplePairs(selfMr, otherMr, maxSearchDistance, sampleDensityMultiplier);
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
            Dictionary<Vector2Int, int> pixelToPixelInfoMap = new Dictionary<Vector2Int, int>(); // TODO: One per lightmap
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
                    if (!pixelToPixelInfoMap.ContainsKey(selfPos) && selfPos.x < selfLightmap.width && selfPos.y < selfLightmap.height)
                    {
                        int selfIndex = selfPos.y * selfLightmap.width + selfPos.x;
                        pixelInfo.Add(new PixelInfo { color = selfColors[selfIndex], lightmapIndex = selfMr.lightmapIndex, position = selfPos });
                        pixelToPixelInfoMap.Add(selfPos, pixelInfo.Count - 1);
                    }

                    Vector2Int otherPos = new Vector2Int((int)otherLightmapUV.x, (int)otherLightmapUV.y) + offset;
                    if (!pixelToPixelInfoMap.ContainsKey(otherPos) && otherPos.x < otherLightmap.width && otherPos.y < otherLightmap.height)
                    {
                        int otherIndex = otherPos.y * otherLightmap.width + otherPos.x;
                        pixelInfo.Add(new PixelInfo { color = otherColors[otherIndex], lightmapIndex = otherMr.lightmapIndex, position = otherPos });
                        pixelToPixelInfoMap.Add(otherPos, pixelInfo.Count - 1);
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
                pixelToPixelInfoMap,
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
                // Make sure our lightmaps are up to date
                GITweaksLightingDataAssetEditor.RefreshLDA();

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
                    initialLightmaps[i].lightmapColor = AssetDatabase.LoadAssetAtPath<Texture2D>(lmPath);
                }
            }
            else
            {
                initialLightmaps[selfMr.lightmapIndex].lightmapColor = selfLightmap;
                initialLightmaps[otherMr.lightmapIndex].lightmapColor = otherLightmap;
            }

            LightmapSettings.lightmaps = initialLightmaps;
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
            Dictionary<Vector2Int, int> pixelToPixelInfoMap,
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
                    pixelToPixelInfoMap,
                    UVToLightmap(samplePair.self.uv, selfSt, selfWidth, selfHeight),
                    selfWidth, selfHeight,
                    edgeConstraintWeight,
                    selfIxs, selfWeights);
                BilinearSample(
                    pixelToPixelInfoMap,
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