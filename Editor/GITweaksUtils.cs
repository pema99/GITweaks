using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GITweaks
{
    public static class GITweaksUtils
    {
        public static Rect GetSTRect(MeshRenderer mr)
        {
            return new Rect(mr.lightmapScaleOffset.z, mr.lightmapScaleOffset.w, mr.lightmapScaleOffset.x, mr.lightmapScaleOffset.y);
        }

        public static Rect GetSTRect(Terrain tr)
        {
            return new Rect(tr.lightmapScaleOffset.z, tr.lightmapScaleOffset.w, tr.lightmapScaleOffset.x, tr.lightmapScaleOffset.y);
        }

        public static Rect GetSTRect(Component c)
        {
            if (c is MeshRenderer mr) return GetSTRect(mr);
            else return GetSTRect((Terrain)c);
        }

        public static Rect ComputeUVBounds(MeshRenderer mr)
        {
            var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
            var verts = mesh.uv2;
            if (verts == null || verts.Length == 0)
                verts = mesh.uv;
            Vector2 minVert = Vector3.positiveInfinity, maxVert = Vector3.negativeInfinity;
            foreach (Vector3 vert in verts)
            {
                if (vert.x < minVert.x)
                    minVert.x = vert.x;
                if (vert.y < minVert.y)
                    minVert.y = vert.y;
                if (vert.x > maxVert.x)
                    maxVert.x = vert.x;
                if (vert.y > maxVert.y)
                    maxVert.y = vert.y;
            }
            Rect uvBounds = new Rect(minVert, maxVert - minVert);
            return uvBounds;
        }

        public static Rect ComputeUVBounds(Component c)
        {
            if (c is MeshRenderer mr)
                return ComputeUVBounds(mr);
            else
                return new Rect(0, 0, 1, 1);
        }

        public static Rect STRectToPixelRect(MeshRenderer mr, Rect stRect)
        {
            Rect uvBounds = ComputeUVBounds(mr);

            // Scale ST rect to pixel rect
            stRect.x += uvBounds.x * stRect.width;
            stRect.y += uvBounds.y * stRect.height;
            stRect.width *= uvBounds.width;
            stRect.height *= uvBounds.height;

            return stRect;
        }

        public static Rect STRectToPixelRect(Rect uvBounds, Rect stRect)
        {
            // Scale ST rect to pixel rect
            stRect.x += uvBounds.x * stRect.width;
            stRect.y += uvBounds.y * stRect.height;
            stRect.width *= uvBounds.width;
            stRect.height *= uvBounds.height;

            return stRect;
        }

        public static void OffsetLightmapSTByPixelRectOffset(Rect uvBounds, ref Vector4 lightmapST)
        {
            lightmapST.z -= uvBounds.x * lightmapST.x;
            lightmapST.w -= uvBounds.y * lightmapST.y;
        }

        public static void OffsetLightmapSTByPixelRectOffset(MeshRenderer mr, ref Vector4 lightmapST)
        {
            // Scale to uv bounds
            if (mr != null)
            {
                var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                var verts = mesh.uv2;
                if (verts == null || verts.Length == 0)
                    verts = mesh.uv;
                Vector2 minVert = Vector3.positiveInfinity;
                foreach (Vector3 vert in verts)
                {
                    if (vert.x < minVert.x)
                        minVert.x = vert.x;
                    if (vert.y < minVert.y)
                        minVert.y = vert.y;
                }
                // Scale ST rect to pixel rect
                lightmapST.z -= minVert.x * lightmapST.x;
                lightmapST.w -= minVert.y * lightmapST.y;
            }
        }

        public static void GetSTAndPixelRect(Component c, out Rect stRect, out Rect pixelRect)
        {
            if (c is MeshRenderer mr)
            {
                stRect = GetSTRect(mr);
                pixelRect = STRectToPixelRect(mr, stRect);
            }
            else
            {
                stRect = GetSTRect((Terrain)c);
                pixelRect = stRect;
            }
        }

        public static int Top(this RectInt r) => r.y;
        public static int Bottom(this RectInt r) => r.y + r.height - 1;
        public static int Left(this RectInt r) => r.x;
        public static int Right(this RectInt r) => r.x + r.width - 1;
        public static bool Contains(this RectInt self, RectInt other)
        {
            return self.Left() <= other.Left()
                && self.Right() >= other.Right()
                && self.Top() <= other.Top()
                && self.Bottom() >= other.Bottom();
        }
        public static Rect ToRect(this RectInt self)
        {
            return new Rect(self.position, self.size);
        }
        public static RectInt ToRectInt(this Rect self)
        {
            return new RectInt(Vector2Int.CeilToInt(self.position), Vector2Int.CeilToInt(self.size));
        }

        private static int DivUp(int x, int y) => (x + y - 1) / y;

        private static ComputeShader copyFractionalShader = null;
        private static int copyFractionalShaderKernel = -1;
        public static void CopyFractional(Texture2D from, Rect fromRect, RenderTexture to, Vector2Int toPosition, bool gammaToLinear)
        {
            if (copyFractionalShader == null)
                copyFractionalShader = Resources.Load<ComputeShader>("CopyFractional");
            if (copyFractionalShaderKernel < 0)
                copyFractionalShaderKernel = copyFractionalShader.FindKernel("CopyFractional");

            copyFractionalShader.SetTexture(copyFractionalShaderKernel, "_Input", from);
            copyFractionalShader.SetTexture(copyFractionalShaderKernel, "_Output", to);
            copyFractionalShader.SetVector("_SrcRect", new Vector4(fromRect.width, fromRect.height, fromRect.x, fromRect.y));
            copyFractionalShader.SetInt("_DstX", toPosition.x);
            copyFractionalShader.SetInt("_DstY", toPosition.y);
            copyFractionalShader.SetInt("_GammaToLinear", gammaToLinear ? 1 : 0);

            copyFractionalShader.GetKernelThreadGroupSizes(copyFractionalShaderKernel, out uint kx, out uint ky, out _);
            copyFractionalShader.Dispatch(
                copyFractionalShaderKernel,
                DivUp(Mathf.CeilToInt(fromRect.width), (int)kx),
                DivUp(Mathf.CeilToInt(fromRect.height), (int)ky),
                1);
        }

        public static Texture2D GetRWTextureCopy(Texture2D texture, GraphicsFormat format)
        {
            RenderTexture temp = RenderTexture.GetTemporary(texture.width, texture.height, 0, format);
            Graphics.Blit(texture, temp);

            Texture2D textureCopy = new Texture2D(texture.width, texture.height, format, TextureCreationFlags.None) { name =  $"Copy of {texture.name}"};
            textureCopy.wrapMode = TextureWrapMode.Clamp;
            var prevRT = RenderTexture.active;
            RenderTexture.active = temp;
            textureCopy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(temp);

            return textureCopy;
        }

        public static void RenderTextureToTexture2D(RenderTexture src, Texture2D dst)
        {
            var prevRT = RenderTexture.active;
            RenderTexture.active = src;
            dst.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prevRT;
        }

        public static Texture2D RenderTextureToTexture2D(RenderTexture src)
        {
            Texture2D result = new Texture2D(src.width, src.height, src.graphicsFormat, TextureCreationFlags.None) { name = $"Conversion of {src.name}" };
            var prevRT = RenderTexture.active;
            RenderTexture.active = src;
            result.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prevRT;
            return result;
        }

        public static void Texture2DToRenderTexture(Texture2D src, RenderTexture dst)
        {
            Graphics.Blit(src, dst);
        }

        public static void CopyImporterSettingsAndReimport(Texture2D template, string dstPath)
        {
            var srcImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(template));
            var dstImporter = AssetImporter.GetAtPath(dstPath);

            var srcImporterObj = new SerializedObject(srcImporter);
            var dstImporterObj = new SerializedObject(dstImporter);

            var srcIter = srcImporterObj.GetIterator();

            while (srcIter.Next(true))
            {
                dstImporterObj.CopyFromSerializedProperty(srcIter);
            }

            dstImporterObj.ApplyModifiedProperties();
            dstImporter.SaveAndReimport();
        }

        public static bool IsLightmapped(MeshRenderer mr)
        {
            return mr.lightmapIndex >= 0 && mr.lightmapIndex < 65534;
        }

        public static bool IsRealtimeLightmapped(MeshRenderer mr)
        {
            return mr.realtimeLightmapIndex >= 0 && mr.realtimeLightmapIndex < 65534;
        }
    }

    public class PrefInt
    {
        int Value;
        string Name;
        bool Loaded;

        public PrefInt(string name, int value)
        {
            Name = $"GITweaks.{name}";
            Loaded = false;
            Value = value;
        }

        private void Load()
        {
            if (Loaded)
                return;

            Loaded = true;
            Value = EditorPrefs.GetInt(Name, Value);
        }

        public int value
        {
            get { Load(); return Value; }
            set
            {
                Load();
                if (Value == value)
                    return;
                Value = value;
                EditorPrefs.SetInt(Name, value);
            }
        }

        public static implicit operator int(PrefInt s)
        {
            return s.value;
        }
    }

    public class PrefFloat
    {
        float Value;
        string Name;
        bool Loaded;

        public PrefFloat(string name, float value)
        {
            Name = $"GITweaks.{name}";
            Loaded = false;
            Value = value;
        }

        private void Load()
        {
            if (Loaded)
                return;

            Loaded = true;
            Value = EditorPrefs.GetFloat(Name, Value);
        }

        public float value
        {
            get { Load(); return Value; }
            set
            {
                Load();
                if (Value == value)
                    return;
                Value = value;
                EditorPrefs.SetFloat(Name, value);
            }
        }

        public static implicit operator float(PrefFloat s)
        {
            return s.value;
        }
    }

    public class PrefBool
    {
        bool Value;
        string Name;
        bool Loaded;

        public PrefBool(string name, bool value)
        {
            Name = $"GITweaks.{name}";
            Loaded = false;
            Value = value;
        }

        private void Load()
        {
            if (Loaded)
                return;

            Loaded = true;
            Value = EditorPrefs.GetBool(Name, Value);
        }

        public bool value
        {
            get { Load(); return Value; }
            set
            {
                Load();
                if (Value == value)
                    return;
                Value = value;
                EditorPrefs.SetBool(Name, value);
            }
        }

        public static implicit operator bool(PrefBool s)
        {
            return s.value;
        }
    }

    public class PrefEnum<TEnum>
        where TEnum : System.Enum
    {
        TEnum Value;
        string Name;
        bool Loaded;

        public PrefEnum(string name, TEnum value)
        {
            Name = $"GITweaks.{name}";
            Loaded = false;
            Value = value;
        }

        private void Load()
        {
            if (Loaded)
                return;

            Loaded = true;
            Value = (TEnum)(object)EditorPrefs.GetInt(Name, (int)(object)Value);
        }

        public TEnum value
        {
            get { Load(); return Value; }
            set
            {
                Load();
                if (EqualityComparer<TEnum>.Default.Equals(Value, value))
                    return;
                Value = value;
                EditorPrefs.SetInt(Name, (int)(object)value);
            }
        }

        public static implicit operator TEnum(PrefEnum<TEnum> s)
        {
            return s.value;
        }
    }
}