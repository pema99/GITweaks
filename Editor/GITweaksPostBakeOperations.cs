using Accord.Math.Optimization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[InitializeOnLoad]
public static class GITweaksPostBakeOperations
{
    static GITweaksPostBakeOperations()
    {
        Lightmapping.bakeCompleted -= BakeFinished;
        Lightmapping.bakeCompleted += BakeFinished;
    }

    private static void BakeFinished()
    {
        // Shared LOD handling
        var sharedLODs = Object.FindObjectsByType<GITweaksSharedLOD>(FindObjectsSortMode.None);
        foreach (var sharedLOD in sharedLODs)
        {
            var lods = sharedLOD.GetComponent<LODGroup>().GetLODs();
            if (lods.Length == 0) continue;
            var lod0 = lods[0].renderers.FirstOrDefault(x => x is MeshRenderer) as MeshRenderer;
            if (lod0 == null) continue;

            var mrs = sharedLOD.RenderersToLightmap;
            GITweaksLightingDataAssetEditor.CopyAtlasSettingsToRenderers(Lightmapping.lightingDataAsset, lod0, mrs);
            GITweaksLightingDataAssetEditor.RefreshLDA();
        }

        // Fix seams
        var seamFixes = Object.FindObjectsByType<GITweaksSeamFix>(FindObjectsSortMode.None);
        foreach (var seamFix in seamFixes)
        {
            var selfMr = seamFix.GetComponent<MeshRenderer>();
            var otherMrs = seamFix.RenderersToFixSeamsWith;

            foreach (var otherMr in otherMrs)
            {
                FixSeams(selfMr, otherMr);
            }
        }
    }

    struct SamplePoint
    {
        public Vector3 vertex;
        //public Vector3 normal;
        public Vector2 uv;
    }

    struct PixelInfo
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

                //GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //go.transform.position = Vector3.Lerp(vertA, vertB, (float)j / (float)(numSamples - 1));
                //go.transform.localScale = Vector3.one * 0.05f;
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

        lightmapUV.x = Mathf.Clamp(lightmapUV.x, 0, lightmapWidth);
        lightmapUV.y = Mathf.Clamp(lightmapUV.y, 0, lightmapHeight);
        return lightmapUV;
    }

    private static void FixSeams(MeshRenderer selfMr, MeshRenderer otherMr)
    {
        if (selfMr.lightmapIndex >= 65534 || otherMr.lightmapIndex >= 65534)
            return;

        // TODO
        var selfSamples = GenerateSamplePoints(selfMr, 1.1f);
        var otherSamples = GenerateSamplePoints(otherMr, 1.1f);

        // TODO
        float searchDist = 0.01f;

        // Find sample pairs
        List<(SamplePoint self, SamplePoint other)> samplePairs = new List<(SamplePoint, SamplePoint)>();
        foreach (var selfSample in selfSamples)
        {
            foreach (var otherSample in otherSamples)
            {
                if (Vector3.Distance(selfSample.vertex, otherSample.vertex) <= searchDist)
                {
                    samplePairs.Add((selfSample, otherSample));
                }
            }
        }

        // TODO: Kernel around each sample

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

        List<PixelInfo> pixelInfo = new List<PixelInfo>();
        Dictionary<Vector2Int, int> pixelToPixelInfoMap = new Dictionary<Vector2Int, int>();
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
                // TODO: Clamp

                Vector2Int selfPos = Vector2Int.FloorToInt(selfLightmapUV) + offset;
                if (!pixelToPixelInfoMap.ContainsKey(selfPos))
                {
                    int selfIndex = selfPos.y * selfLightmap.width + selfPos.x;
                    pixelInfo.Add(new PixelInfo { color = selfColors[selfIndex], lightmapIndex = selfMr.lightmapIndex, position = selfPos });
                    pixelToPixelInfoMap.Add(selfPos, pixelInfo.Count - 1);
                }

                Vector2Int otherPos = Vector2Int.FloorToInt(otherLightmapUV) + offset;
                if (!pixelToPixelInfoMap.ContainsKey(otherPos))
                {
                    int otherIndex = otherPos.y * otherLightmap.width + otherPos.x;
                    pixelInfo.Add(new PixelInfo { color = otherColors[otherIndex], lightmapIndex = otherMr.lightmapIndex, position = otherPos });
                    pixelToPixelInfoMap.Add(otherPos, pixelInfo.Count - 1);
                }
            }
        }

        int totalPixels = pixelInfo.Count;

        SparseMat AtA = new SparseMat(totalPixels, totalPixels);
        VectorX AtbR = new VectorX(totalPixels), AtbG = new VectorX(totalPixels), AtbB = new VectorX(totalPixels);
        VectorX guessR = new VectorX(totalPixels), guessG = new VectorX(totalPixels), guessB = new VectorX(totalPixels);
        SetupLeastSquares(
            selfMr.lightmapScaleOffset,
            otherMr.lightmapScaleOffset,
            selfLightmap.width,
            selfLightmap.height, 
            otherLightmap.width,
            otherLightmap.height,
            samplePairs, 
            pixelToPixelInfoMap, 
            pixelInfo, 
            AtA, 
            AtbR, 
            AtbG, 
            AtbB, 
            guessR, 
            guessG, 
            guessB);

        VectorX solutionR, solutionG, solutionB;
        solutionR = ConjugateGradientOptimize(AtA, guessR, AtbR, 10000, 0.001f);
        solutionG = ConjugateGradientOptimize(AtA, guessR, AtbR, 10000, 0.001f);
        solutionB = ConjugateGradientOptimize(AtA, guessR, AtbR, 10000, 0.001f);

        for (int i = 0; i < totalPixels; i++)
        {
            PixelInfo px = pixelInfo[i];
            Color col = new Color(solutionR[i], solutionG[i], solutionB[i]);
            if (px.lightmapIndex == selfMr.lightmapIndex)
                selfColors[px.position.y * selfLightmap.width + px.position.x] = col;
            else
                otherColors[px.position.y * otherLightmap.width + px.position.x] = col;
        }
        selfLightmap.SetPixels(selfColors);
        selfLightmap.Apply();

        otherLightmap.SetPixels(otherColors);
        otherLightmap.Apply();

        var lms = LightmapSettings.lightmaps;
        lms[selfMr.lightmapIndex].lightmapColor = selfLightmap;
        lms[otherMr.lightmapIndex].lightmapColor = otherLightmap;
        LightmapSettings.lightmaps = lms;
    }

    private static void BilinearSample(
        Dictionary<Vector2Int, int> pixelToPixelInfoMap,
        Vector2 sample,
        int width,
        int height,
        out int[] outIxs,
        out float[] outWeights)
    {
        outIxs = new int[4];
        outWeights = new float[4];

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
            outWeights[i] *= 5.0f; // EDGE_CONSTRAINT_WEIGHT TODO
        }
    }

    private static void SetupLeastSquares(
        Vector4 selfSt,
        Vector4 otherSt,
        int selfWidth, int selfHeight,
        int otherWidth, int otherHeight,
        List<(SamplePoint self, SamplePoint other)> samplePairs,
        Dictionary<Vector2Int, int> pixelToPixelInfoMap,
        List<PixelInfo> pixelInfo,
        SparseMat AtA,
        VectorX AtbR,
        VectorX AtbG,
        VectorX AtbB,
        VectorX guessR,
        VectorX guessG,
        VectorX guessB)
    {
        foreach (var samplePair in samplePairs)
        {
            BilinearSample(pixelToPixelInfoMap, UVToLightmap(samplePair.self.uv, selfSt, selfWidth, selfHeight), selfWidth, selfHeight, out var selfIxs, out var selfWeights);
            BilinearSample(pixelToPixelInfoMap, UVToLightmap(samplePair.other.uv, otherSt, otherWidth, otherHeight), otherWidth, otherHeight, out var otherIxs, out var otherWeights);

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

            AtbR[i] += pixel.color.r;
            AtbG[i] += pixel.color.g;
            AtbB[i] += pixel.color.b;

            guessR[i] = pixel.color.r;
            guessG[i] = pixel.color.g;
            guessB[i] = pixel.color.b;
        }
    }

    private static Texture2D GetRWLightmap(MeshRenderer mr)
    {
        Texture2D lightmap = LightmapSettings.lightmaps[mr.lightmapIndex].lightmapColor;
        RenderTexture temp = RenderTexture.GetTemporary(lightmap.width, lightmap.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
        Graphics.Blit(lightmap, temp);

        Texture2D lightmapCopy = new Texture2D(lightmap.width, lightmap.height, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None);
        lightmapCopy.wrapMode = TextureWrapMode.Clamp;
        var prevRT = RenderTexture.active;
        RenderTexture.active = temp;
        lightmapCopy.ReadPixels(new Rect(0, 0, lightmap.width, lightmap.height), 0, 0);
        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(temp);

        return lightmapCopy;
    }

    /////////

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
            VectorX.MulAdd(ref x, p, alpha, x); // x = x + alpha * p
            VectorX.MulAdd(ref r, Ap, -alpha, r); // r = r - alpha * Ap
            float rsqNew = VectorX.Dot(r, r);
            if (Mathf.Abs(rsqNew - rsq) < tolerance * n)
                break;
            float beta = rsqNew / rsq;
            VectorX.MulAdd(ref p, p, beta, r); // p = r + beta * p
            rsq = rsqNew;
        }

        return x;
    }

    class SparseMat
    {
        private Row[] rows;
        private int numRows, numCols;

        public SparseMat(int numRows, int numCols)
        {
            this.rows = new Row[numRows];
            this.numRows = numRows;
            this.numCols = numCols;
            for (int i = 0; i < numRows; i++)
            {
                rows[i] = new Row();
            }
        }

        public float this[int row, int column]
        {
            get
            {
                if (row >= numRows || row < 0) throw new System.ArgumentOutOfRangeException(nameof(row));
                if (column >= numCols || column < 0) throw new System.ArgumentOutOfRangeException(nameof(column));
                return rows[row][column];
            }
            set
            {
                if (row >= numRows || row < 0) throw new System.ArgumentOutOfRangeException(nameof(row));
                if (column >= numCols || column < 0) throw new System.ArgumentOutOfRangeException(nameof(column));
                rows[row][column] = value;
            }
        }

        public static void Mul(VectorX outVector, SparseMat A, VectorX x)
        {
            if (outVector.Size != A.numRows) throw new System.ArgumentException("Output vector size must match the number of rows in the matrix.");
            if (x.Size != A.numCols) throw new System.ArgumentException("Input vector size must match the number of columns in the matrix.");

            System.Threading.Tasks.Parallel.For(0, A.numRows, r =>
            {
                outVector[r] = Dot(x, A.rows[r]);
            });
        }

        private static float Dot(VectorX x, Row row)
        {
            float sum = 0.0f;
            for (int i = 0; i < row.n; i++)
            {
                sum += x[row.indices[i]] * row.coeffs[i];
            }
            return sum;
        }

        public class Row
        {
            public int n = 0;
            public int capacity = 0;
            public float[] coeffs;
            public int[] indices;

            public float this[int column]
            {
                get
                {
                    // Find the element
                    int index = findClosestIndex(column);
                    if (n == 0 || indices[index] != column) // Add new element
                    {
                        if (n == capacity)
                        {
                            grow();
                        }

                        // Put the new element in the right place, and shift existing elements down by one.
                        float prevCoeff = 0;
                        int prevIndex = column;
                        ++n;
                        for (int i = index; i < n; ++i)
                        {
                            float tmpCoeff = coeffs[i];
                            int tmpIndex = indices[i];
                            coeffs[i] = prevCoeff;
                            indices[i] = prevIndex;
                            prevCoeff = tmpCoeff;
                            prevIndex = tmpIndex;
                        }
                    }
                    return coeffs[index];
                }
                set
                {
                    // Find the element
                    int index = findClosestIndex(column);
                    if (n == 0 || indices[index] != column) // Add new element
                    {
                        if (n == capacity)
                        {
                            grow();
                        }

                        // Put the new element in the right place, and shift existing elements down by one.
                        float prevCoeff = 0;
                        int prevIndex = column;
                        ++n;
                        for (int i = index; i < n; ++i)
                        {
                            float tmpCoeff = coeffs[i];
                            int tmpIndex = indices[i];
                            coeffs[i] = prevCoeff;
                            indices[i] = prevIndex;
                            prevCoeff = tmpCoeff;
                            prevIndex = tmpIndex;
                        }
                    }
                    coeffs[index] = value;
                }
            }

            private void grow()
            {
                capacity = capacity == 0 ? 16 : capacity + capacity / 2;
                var newCoeffs = new float[capacity];
                var newIndices = new int[capacity];

                // Copy existing data over
                if (coeffs != null)
                {
                    System.Array.Copy(coeffs, newCoeffs, n);
                }
                if (indices != null)
                {
                    System.Array.Copy(indices, newIndices, n);
                }

                coeffs = newCoeffs;
                indices = newIndices;
            }

            private int findClosestIndex(int columnIndex)
            {
                for (int i = 0; i < n; ++i)
                {
                    if (indices[i] >= columnIndex)
                        return i;
                }
                return n;
            }
        }
    }

    public struct VectorX
    {
        private float[] data;

        public VectorX(int size)
        {
            data = new float[size];
        }

        public int Size => data.Length;

        public float this[int index]
        {
            get => data[index];
            set => data[index] = value;
        }

        public void CopyFrom(VectorX other)
        {
            if (this.Size != other.Size)
                throw new System.ArgumentException("Vector sizes must match.");
            System.Array.Copy(other.data, this.data, this.Size);
        }

        public static void Sub(ref VectorX result, VectorX a, VectorX b)
        {
            if (a.Size != b.Size || result.Size != a.Size)
                throw new System.ArgumentException("Vector sizes must match.");
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

        public static void MulAdd(ref VectorX outVec, VectorX v, float a, VectorX b)
        {
            for (int i = 0; i < v.Size; ++i)
            {
                outVec[i] = v[i] * a + b[i];
            }
        }
    }
    /////////
}
