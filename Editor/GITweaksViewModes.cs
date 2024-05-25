using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using System.Linq;
using UnityEditor.Overlays;

[InitializeOnLoad]
public static class GITweaksViewModes
{
    [Overlay(typeof(SceneView), "Baked Transmission Legend", false)]
    public class MyToolButtonOverlay : Overlay, ITransientOverlay
    {
        private static VisualElement CreateColorSwatch(string label, Color color)
        {
            var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginLeft = 2 } };
            row.AddToClassList("unity-base-field");

            var swatchContainer = new VisualElement();
            swatchContainer.AddToClassList("unity-base-field__label");
            swatchContainer.AddToClassList("unity-pbr-validation-color-swatch");

            var colorContent = new VisualElement() { name = "color-content" };
                colorContent.style.backgroundColor = new StyleColor(color);
           
            swatchContainer.Add(colorContent);
            row.Add(swatchContainer);

            var colorLabel = new Label(label) { name = "color-label" };
            colorLabel.AddToClassList("unity-base-field__label");
            row.Add(colorLabel);
            return row;
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement() { name = "Root" };
            root.Add(CreateColorSwatch("Full RGB transmission", Color.red));
            root.Add(CreateColorSwatch("Alpha/Fade transmission", Color.green));
            root.Add(CreateColorSwatch("Cutout transmission", Color.blue));
            root.Add(CreateColorSwatch("Opaque (Missing _MainTex)", Color.magenta));
            root.Add(CreateColorSwatch("Opaque", Color.white));
            return root;

        }

        public bool visible => SceneView.lastActiveSceneView.cameraMode.name == BakedTransmissionModes;
    }

    const string BakedTransmissionModes = "Baked Transmission Modes";
    const string BakedTransmissionTextures = "Baked Transmission Data";

    static GITweaksViewModes()
    {
        Init();
    }

    public static void Init()
    {
        if (!GITweaksSettingsWindow.IsEnabled(GITweak.BakedTransmissionViewModes))
            return;

        var modes = (List<SceneView.CameraMode>)typeof(SceneView)
            .GetProperty("userDefinedModes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .GetValue(null, new object[0]);

        if (!modes.Any(x => x.name == BakedTransmissionModes))
        {
            SceneView.AddCameraMode(BakedTransmissionModes, "GI Tweaks");
        }
        if (!modes.Any(x => x.name == BakedTransmissionTextures))
        {
            SceneView.AddCameraMode(BakedTransmissionTextures, "GI Tweaks");
        }

        SceneView.beforeSceneGui -= RenderCustom;
        SceneView.beforeSceneGui += RenderCustom;
    }

    public static void Deinit()
    {
        foreach (SceneView sv in SceneView.sceneViews)
        {
            sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
        }

        SceneView.ClearUserDefinedCameraModes();
    }

    enum TransparencyMode
    {
        RGB,
        Alpha,
        Cutout,
        MissingMainTex,
        Opaque,
    }

    private static TransparencyMode GetTransparencyMode(Material m, out Texture tex, out float alpha)
    {
        alpha = m.color.a;

        if (m.HasProperty("_TransparencyLM"))
        {
            alpha = 1;
            tex = m.GetTexture("_TransparencyLM");
            return TransparencyMode.RGB;
        }

        // Transparent type
        bool cutout = m.renderQueue >= (int)RenderQueue.AlphaTest && m.renderQueue < (int)RenderQueue.Transparent;
        bool transparent = m.renderQueue >= (int)RenderQueue.Transparent && m.renderQueue < (int)RenderQueue.Overlay;

        if (!transparent)
        {
            string renderType = m.GetTag("RenderType", false);
            if (m.name.Contains("Transparent") || (m.name.Contains("Tree") && (m.name.Contains("Leaves") || m.name.Contains("Leaf"))))
            {
                transparent = true;
            }
            else if (renderType == "GrassBillboard" || renderType == "Transparent" || renderType == "Grass" || renderType == "TreeLeaf")
            {
                transparent = true;
            }
        }

        if (!cutout)
        {
            string renderType = m.GetTag("RenderType", false);
            if (renderType == "TransparentCutout" || renderType == "TreeTransparentCutout")
            {
                cutout = true;
            }
            else if (m.IsKeywordEnabled("GEOM_TYPE_FROND") || m.IsKeywordEnabled("GEOM_TYPE_LEAF"))
            {
                cutout = true;
            }
        }

        if (cutout || transparent)
        {
            tex = m.mainTexture;

            bool hasMainTexture = m.mainTexture != null;
            if (!hasMainTexture)
            {
                if (m.HasProperty("_MainTex"))
                {
                    hasMainTexture = true;
                }
                else
                {
                    var shader = m.shader;
                    int propertyCount = shader.GetPropertyCount();
                    for (int i = 0; i < propertyCount; i++)
                    {
                        if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                            continue;

                        if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainTexture) != 0)
                        {
                            hasMainTexture = true;
                            break;
                        }
                    }
                }
            }

            if (!hasMainTexture)
                return TransparencyMode.MissingMainTex;

            if (cutout && (m.HasProperty("_Cutoff") || m.HasProperty("_AlphaTestRef")))
                return TransparencyMode.Cutout;

            if (transparent)
                return TransparencyMode.Alpha;
        }

        tex = Texture2D.blackTexture;
        return TransparencyMode.Opaque;
    }

    private static void RenderCustom(SceneView sceneView)
    {
        if (sceneView.cameraMode.name != BakedTransmissionModes && sceneView.cameraMode.name != BakedTransmissionTextures)
            return;

        bool showTextures = sceneView.cameraMode.name == BakedTransmissionTextures;

        if (Event.current.type != EventType.Repaint)
            return;

        sceneView.SetSceneViewShaderReplace(null, "");

        var mat = new Material(Shader.Find("Hidden/pema99/Overlay"));

        var planes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
        var mrs = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var mr in mrs)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, mr.bounds))
                continue;

            var mode = GetTransparencyMode(mr.sharedMaterial, out var tex, out var alpha);
            switch (mode)
            {
                case TransparencyMode.RGB: mat.color = Color.red; break;
                case TransparencyMode.Alpha: mat.color = Color.green; break;
                case TransparencyMode.Cutout: mat.color = Color.blue; break;
                case TransparencyMode.MissingMainTex: mat.color = Color.magenta; break;
                case TransparencyMode.Opaque: mat.color = Color.white; break;
            }
            mat.mainTexture = tex;
            mat.SetFloat("_Alpha", alpha);
            mat.SetInt("_Mode", showTextures ? (mode == TransparencyMode.RGB ? 2 : 1) : 0);
            mat.SetPass(0);

            Graphics.DrawMeshNow(mr.GetComponent<MeshFilter>().sharedMesh, mr.localToWorldMatrix);
        }
    }
}