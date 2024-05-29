using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GITweaks
{
    public enum GITweak
    {
        Logging,
        ClickableLightmapCharts,
        BakedTransmissionViewModes,
        BetterLDAInspector,
        LightmapFlagsDropdown,
        AutomaticEmbeddedLightingSettings,
        BetterLightingSettingsDefaults,
        NewSkyboxButton,
        LightmapPreviewDropdown,
        LightmappedToProbeLit,
        SharedLODGroupComponents,
        OptimizeLightmapSizes,
    }

    public class GITweaksSettingsWindow : EditorWindow
    {
        /* TODO:
        X Toggles for each tweak
        - Lighting settings template override
        - Easily switch between lighting settings
        - Probe placement projection guide
        - Seam stitching across meshes
        - Shadow only debug view
        - Post-denoising (no need to bake)
        X Dropdown for lightmap index in preview window
        X Atlassing post bake optim
        X Change lightmapped renderer to probe lit without needing rebake
        X Create default skybox button
        X Transparency view mode
        X Click to highlight object in lightmap preview window
        X Show lightmap flags in inspector for material
        X View all renderers by receive GI mode
        X Auto GPU lightmapper selection + no prioritize view
        X Auto embedded lighting settings
        X Move LODs to overlap on lightmap
        X Better LDA inspector
        */

        [MenuItem("Tools/GI Tweaks/Settings")]
        public static void ShowExample()
        {
            GITweaksSettingsWindow wnd = GetWindow<GITweaksSettingsWindow>();
            wnd.minSize = new Vector2(350, 330);
            wnd.titleContent = new GUIContent("GI Tweaks Settings");
        }

        [MenuItem("Tools/GI Tweaks/Bake Lighting")]
        public static void BakeLighting()
        {
            Lightmapping.BakeAsync();
        }

        [MenuItem("Tools/GI Tweaks/Bake Reflection Probes")]
        public static void BakeReflectionProbes()
        {
            typeof(Lightmapping)
                .GetMethod("BakeAllReflectionProbesSnapshots", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[0]);
        }

        [MenuItem("Tools/GI Tweaks/Open Lightmap Preview")]
        public static void OpenLightmapPreview()
        {
            var type = Type.GetType("UnityEditor.LightmapPreviewWindow, UnityEditor");
            var window = EditorWindow.CreateInstance(type) as EditorWindow;
            type.GetField("m_LightmapIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(window, 0);
            window.minSize = new Vector2(360, 390);
            window.Show();
        }

        private static readonly Dictionary<GITweak, bool> defaultValues = new Dictionary<GITweak, bool>()
        {
            { GITweak.Logging, true },
            { GITweak.ClickableLightmapCharts, true },
            { GITweak.BakedTransmissionViewModes, true },
            { GITweak.BetterLDAInspector, true },
            { GITweak.LightmapFlagsDropdown, true },
            { GITweak.AutomaticEmbeddedLightingSettings, true },
            { GITweak.BetterLightingSettingsDefaults, true },
            { GITweak.NewSkyboxButton, true },
            { GITweak.LightmapPreviewDropdown, true },
            { GITweak.LightmappedToProbeLit, true },
            { GITweak.OptimizeLightmapSizes, false },
            { GITweak.SharedLODGroupComponents, true },
        };

        public static PrefFloat LightmapOptimizationTargetCoverage = new PrefFloat("LightmapOptimization.TargetCoverage", 0.85f);
        public static PrefInt LightmapOptimizationMinLightmapSize = new PrefInt("LightmapOptimization.MinLightmapSize", 32);

        bool tweakTogglesHeader = true;
        bool lightmapOptimizationHeader = true;

        private void ShowTweakToggle(GITweak tweak, string label)
        {
            string key = $"GITweaks.{Enum.GetName(typeof(GITweak), tweak)}";
            bool val = EditorPrefs.GetBool(key, defaultValues[tweak]);
            EditorGUI.BeginChangeCheck();
            val = GUILayout.Toggle(val, label);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(key, val);
            }

            if (val && IsIncompatible(tweak))
            {
                EditorGUILayout.HelpBox("This setting is incompatible with Bakery! Do not use it when baking with Bakery.", MessageType.Warning);
            }
        }

        public void OnGUI()
        {
            if (EditorGUILayout.LinkButton("Open feature overview"))
                Application.OpenURL("https://github.com/pema99/GITweaks/blob/master/README.md#current-features");

            tweakTogglesHeader = EditorGUILayout.BeginFoldoutHeaderGroup(tweakTogglesHeader, "Tweak toggles");
            if (tweakTogglesHeader)
            {
                ShowTweakToggle(GITweak.Logging, "Enable console logging");
                ShowTweakToggle(GITweak.ClickableLightmapCharts, "Clickable charts in Lightmap Preview Window");
                ShowTweakToggle(GITweak.BetterLDAInspector, "Better Lighting Data asset inspector");
                ShowTweakToggle(GITweak.LightmapFlagsDropdown, "Show \"Lightmap Flags\" dropdown in material inspector");
                ShowTweakToggle(GITweak.AutomaticEmbeddedLightingSettings, "Use embedded Lighting Settings asset for new scenes");
                ShowTweakToggle(GITweak.BetterLightingSettingsDefaults, "Default to GPU lightmapper and no view prioritization");
                ShowTweakToggle(GITweak.NewSkyboxButton, "Show New and Clone buttons for skybox materials");
                ShowTweakToggle(GITweak.LightmapPreviewDropdown, "Show lightmap index dropdown in preview window");
                ShowTweakToggle(GITweak.LightmappedToProbeLit, "Allow converting lightmapped renderers to probe-lit");

                EditorGUI.BeginChangeCheck();
                ShowTweakToggle(GITweak.BakedTransmissionViewModes, "Scene view modes for Baked Transmission");
                if (EditorGUI.EndChangeCheck())
                {
                    if (IsEnabled(GITweak.BakedTransmissionViewModes))
                        GITweaksViewModes.Init();
                    else
                        GITweaksViewModes.Deinit();
                }

                ShowTweakToggle(GITweak.SharedLODGroupComponents, "Allow shared LOD group components");
                ShowTweakToggle(GITweak.OptimizeLightmapSizes, "Optimize lightmap sizes after baking");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            using (new EditorGUI.DisabledScope(!IsEnabled(GITweak.OptimizeLightmapSizes)))
            {
                lightmapOptimizationHeader = EditorGUILayout.BeginFoldoutHeaderGroup(lightmapOptimizationHeader, "Lightmap size optimization");
                if (lightmapOptimizationHeader)
                {
                    LightmapOptimizationTargetCoverage.value = EditorGUILayout.Slider("Target coverage %", LightmapOptimizationTargetCoverage * 100.0f, 0, 100) / 100.0f;
                    var sizes = Enumerable.Range(4, 8).Select(x => 2 << x);

                    LightmapOptimizationMinLightmapSize.value = EditorGUILayout.IntPopup(
                        "Minimum Lightmap Size",
                        LightmapOptimizationMinLightmapSize,
                        sizes.Select(x => x.ToString()).ToArray(),
                        sizes.ToArray());
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        public static bool IsEnabled(GITweak tweak)
        {
            string key = $"GITweaks.{Enum.GetName(typeof(GITweak), tweak)}";
            return EditorPrefs.GetBool(key, defaultValues[tweak]);
        }

        public static bool ProjectUsesBakery()
        {
            #if BAKERY_INCLUDED
            return true;
            #else
            return false;
            #endif
        }

        public static bool IsIncompatible(GITweak tweak)
        {
            if (!ProjectUsesBakery())
                return false;

            // Bakery has no traditional LDA to edit
            if (tweak == GITweak.LightmappedToProbeLit) return true;

            if (tweak == GITweak.OptimizeLightmapSizes) return true;

            return false;
        }
    }
}