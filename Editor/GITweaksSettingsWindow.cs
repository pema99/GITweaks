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

public enum GITweak
{
    ClickableLightmapCharts,
    BakedTransmissionViewModes,
    BetterLDAInspector,
    LightmapFlagsDropdown,
    AutomaticEmbeddedLightingSettings,
    BetterLightingSettingsDefaults,
    NewSkyboxButton,
}

public class GITweaksSettingsWindow : EditorWindow
{
    /* TODO:
    X Toggles for each tweak
    - Lighting settings template override
    - Easily switch between lighting settings
    - Seam stitching across meshes
    - Shadow only debug view
    - Change lightmapped renderer to probe lit without needing rebake
    - Probe placement projection guide
    - Atlassing post bake optim
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
        wnd.minSize = new Vector2(350, 150);
        wnd.titleContent = new GUIContent("GI Tweaks Dashboard");
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

    bool tweakToggles = true;

    private void ShowTweakToggle(GITweak tweak, string label)
    {
        string key = $"GITweaks.{Enum.GetName(typeof(GITweak), tweak)}";
        bool val = EditorPrefs.GetBool(key, true);
        EditorGUI.BeginChangeCheck();
        val = GUILayout.Toggle(val, label);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(key, val);
        }
    }

    public void OnGUI()
    {
        tweakToggles = EditorGUILayout.BeginFoldoutHeaderGroup(tweakToggles, "Tweak toggles");
        ShowTweakToggle(GITweak.ClickableLightmapCharts, "Clickable charts in Lightmap Preview Window");
        ShowTweakToggle(GITweak.BetterLDAInspector, "Better Lighting Data asset inspector");
        ShowTweakToggle(GITweak.LightmapFlagsDropdown, "Show \"Lightmap Flags\" dropdown in material inspector");
        ShowTweakToggle(GITweak.AutomaticEmbeddedLightingSettings, "Use embedded Lighting Settings asset for new scenes");
        ShowTweakToggle(GITweak.BetterLightingSettingsDefaults, "Default to GPU lightmapper and no view prioritization");
        ShowTweakToggle(GITweak.NewSkyboxButton, "Show New and Clone buttons for skybox materials");

        EditorGUI.BeginChangeCheck();
        ShowTweakToggle(GITweak.BakedTransmissionViewModes, "Scene view modes for Baked Transmission");
        if (EditorGUI.EndChangeCheck())
        {
            if (IsEnabled(GITweak.BakedTransmissionViewModes))
                GITweaksViewModes.Init();
            else
                GITweaksViewModes.Deinit();
        }
    }

    public static bool IsEnabled(GITweak tweak)
    {
        string key = $"GITweaks.{Enum.GetName(typeof(GITweak), tweak)}";
        return EditorPrefs.GetBool(key, true);
    }
}
