#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(LODGroup))]
public class GITweaksSharedLOD : MonoBehaviour
{
    public MeshRenderer[] RenderersToLightmap;

    public void Reset()
    {
        var lods = GetComponent<LODGroup>().GetLODs();
        if (lods.Length > 1)
        {
            RenderersToLightmap = lods
                .Skip(1)
                .SelectMany(x => x.renderers)
                .Select(x => x as MeshRenderer)
                .Where(x => x != null)
                .ToArray();
        }
    }
}

[CustomEditor(typeof(GITweaksSharedLOD))]
public class GITweaksSharedLODEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target is not GITweaksSharedLOD lod)
            return;

        serializedObject.Update();

        if (GUILayout.Button("Reset selection to all LODs"))
        {
            lod.Reset();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(GITweaksSharedLOD.RenderersToLightmap)));

        var lods = lod.GetComponent<LODGroup>().GetLODs();
        var lod0renderers = lods.Length > 0 ? lods[0].renderers : System.Array.Empty<Renderer>();
        var contributorAlready = lod.RenderersToLightmap
            .Where(x => GameObjectUtility.AreStaticEditorFlagsSet(x.gameObject, StaticEditorFlags.ContributeGI))
            .Where(x => !lod0renderers.Contains(x));
        if (contributorAlready.Any())
        {
            string renderers = string.Join("\n", contributorAlready.Select(x => $"- {x.name}"));
            EditorGUILayout.HelpBox(
                $"Some LOD's are marked as GI contributors. For this script to function properly, only LOD0 should be a contributor. The problematic MeshRenderers are:\n{renderers}",
                MessageType.Warning);
            if (GUILayout.Button("Fix issue"))
            {
                foreach (var renderer in contributorAlready)
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    flags &= ~StaticEditorFlags.ContributeGI;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                }
            }
        }

        var firstMR = lod0renderers.FirstOrDefault(x => x is MeshRenderer);
        if (firstMR != null && lod0renderers.Length > 1)
        {
            EditorGUILayout.HelpBox(
                $"LOD0 contains multiple renderers. Only the first MeshRenderer, {firstMR}, will have its lightmap data copied to other LODs.",
                MessageType.Warning);
        }

#if BAKERY_INCLUDED
        EditorGUILayout.HelpBox("This component is incompatible with Bakery. It will have no effect when baking using Bakery.", MessageType.Warning);
#endif

        serializedObject.ApplyModifiedProperties();
    }
}

#else
public class GITweaksSharedLOD : MonoBehaviour {}
#endif