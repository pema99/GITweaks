using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class GITweaksHarmony
{
    public static Harmony Instance = new Harmony("pema.dev.gitweaks");

    [HarmonyPatch(typeof(MaterialEditor), nameof(MaterialEditor.PropertiesDefaultGUI))]
    private class ShowGIFlagsMaterialEditorPatch
    {
        static void Postfix(MaterialEditor __instance)
        {
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
            __instance.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            __instance.prioritizeView = false;
        }
    }

    static void NewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
    {
        LightingSettings settings = new LightingSettings() { name = "Lighting Settings (Embedded)" };
        Lightmapping.SetLightingSettingsForScene(scene, settings);
    }

    static GITweaksHarmony()
    {
        Instance.PatchAll();

        EditorSceneManager.newSceneCreated -= NewSceneCreated;
        EditorSceneManager.newSceneCreated += NewSceneCreated;
    }
}
