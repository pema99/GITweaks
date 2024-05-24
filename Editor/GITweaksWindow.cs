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

public class GITweaksWindow : EditorWindow
{
    /* TODO:
    - Toggles for each tweak
    - Transparency view mode
    - Lighting settings template override
    - Easily switch between lighting settings
    - Seam stitching across meshes
    - Shadow only debug view
    - Reassign Shadowmask indices without rebake (component)
    - Change lightmapped renderer to probe lit without needing rebake
    - Click to highlight object in lightmap preview window
    - Probe placement projection guide
    - Auto assign sun source env
    - Create default skybox button
    X Show lightmap flags in inspector for material
    X View all renderers by receive GI mode
    X Auto GPU lightmapper selection + no prioritize view
    X Auto embedded lighting settings
    X Move LODs to overlap on lightmap
    X Better LDA inspector
    Not possible:
        - Preview atlassing (might not be possible 2022)
        - Ability to move or rebake probes post bake (not possible 2022)
    */

    [MenuItem("Tools/GI Tweaks/Dashboard")]
    public static void ShowExample()
    {
        GITweaksWindow wnd = GetWindow<GITweaksWindow>();
        wnd.titleContent = new GUIContent("GI Tweaks Dashboard");
    }

    bool headerMassRenderSelection = true;
    bool filterActiveObjects = false;
    bool filterGIContributors = true;
    bool filterReceiveGI = false;
    ReceiveGI receiveGIFilter = ReceiveGI.LightProbes;
    bool filterLightProbesUsage = false;
    LightProbeUsage lightProbeUsageFilter = LightProbeUsage.BlendProbes;
    bool filterReflectionProbesUsage = false;
    ReflectionProbeUsage reflectionProbeUsageFilter = ReflectionProbeUsage.BlendProbes;
    bool filterOnlyCurrentScene = false;
    bool filterOnlyCurrentSelection = false;
    // TODO: Presets

    public void OnGUI()
    {
        headerMassRenderSelection = EditorGUILayout.BeginFoldoutHeaderGroup(headerMassRenderSelection, "Mass renderer selection");
        if (headerMassRenderSelection)
        {
            filterActiveObjects = GUILayout.Toggle(filterActiveObjects, "Select only active objects");
            filterGIContributors = GUILayout.Toggle(filterGIContributors, "Select only GI contributors");
            filterReceiveGI = GUILayout.Toggle(filterReceiveGI, "Filter by receive GI mode");
            using (new EditorGUI.DisabledScope(!filterReceiveGI))
            {
                EditorGUI.indentLevel++;
                receiveGIFilter = (ReceiveGI)EditorGUILayout.EnumPopup("Receive GI mode", receiveGIFilter);
                EditorGUI.indentLevel--;
            }
            filterLightProbesUsage = GUILayout.Toggle(filterLightProbesUsage, "Filter by light probe usage");
            using (new EditorGUI.DisabledScope(!filterLightProbesUsage))
            {
                EditorGUI.indentLevel++;
                lightProbeUsageFilter = (LightProbeUsage)EditorGUILayout.EnumPopup("Light Probe Usage", lightProbeUsageFilter);
                EditorGUI.indentLevel--;
            }
            filterReflectionProbesUsage = GUILayout.Toggle(filterReflectionProbesUsage, "Filter by reflection probe usage");
            using (new EditorGUI.DisabledScope(!filterReflectionProbesUsage))
            {
                EditorGUI.indentLevel++;
                reflectionProbeUsageFilter = (ReflectionProbeUsage)EditorGUILayout.EnumPopup("Reflection Probe Usage", reflectionProbeUsageFilter);
                EditorGUI.indentLevel--;
            }
            filterOnlyCurrentScene = GUILayout.Toggle(filterOnlyCurrentScene, "Limit to active scene");
            filterOnlyCurrentSelection = GUILayout.Toggle(filterOnlyCurrentSelection, "Limit to current selection");

            if (GUILayout.Button("Select renderers"))
            {
                var source = filterOnlyCurrentSelection
                    ? Selection.gameObjects.Select(x => x.GetComponent<MeshRenderer>()).Where(x => x != null)
                    : FindObjectsByType<MeshRenderer>(filterActiveObjects ? FindObjectsInactive.Exclude : FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (filterOnlyCurrentScene)
                    source = source.Where(x => x.gameObject.scene == SceneManager.GetActiveScene());

                if (filterActiveObjects)
                    source = source.Where(x => x.enabled && x.gameObject.activeInHierarchy);

                if (filterGIContributors)
                    source = source.Where(x => GameObjectUtility.AreStaticEditorFlagsSet(x.gameObject, StaticEditorFlags.ContributeGI));

                if (filterReceiveGI)
                    source = source.Where(x => x.receiveGI == receiveGIFilter ||
                        (receiveGIFilter == ReceiveGI.LightProbes && !GameObjectUtility.AreStaticEditorFlagsSet(x.gameObject, StaticEditorFlags.ContributeGI)));

                if (filterLightProbesUsage)
                    source = source.Where(x => x.lightProbeUsage == lightProbeUsageFilter);

                if (filterReflectionProbesUsage)
                    source = source.Where(x => x.reflectionProbeUsage == reflectionProbeUsageFilter);

                Selection.objects = source.Select(x => x.gameObject).ToArray();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (GUILayout.Button("Open Lighting Window"))
        {
            System.Type.GetType("UnityEditor.LightingWindow, UnityEditor")
                .GetMethod("CreateLightingWindow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[0]);
        }
        if (GUILayout.Button("Bake lighting"))
        {
            Lightmapping.BakeAsync();
        }
        if (GUILayout.Button("Bake Reflection Probes only"))
        {
            typeof(Lightmapping)
                .GetMethod("BakeAllReflectionProbesSnapshots", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[0]);
        }
    }
}
