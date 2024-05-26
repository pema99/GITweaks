using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GITweaksLightingDataAssetEditor
{
    private static System.Reflection.PropertyInfo inspectorModeObject =
        typeof(SerializedObject).GetProperty("inspectorMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    struct SerializedObjectID : System.IEquatable<SerializedObjectID>
    {
        public long MainLFID; // If prefab, LFID in MeshRenderer in prefab stage, else LFID of object
        public long PrefabLFID; // If prefab, LFID of "Prefab instance" object, points to prefab

        public SerializedObjectID(long main, long prefab)
        {
            MainLFID = main;
            PrefabLFID = prefab;
        }

        public bool Equals(SerializedObjectID other) => other.MainLFID == MainLFID && other.PrefabLFID == PrefabLFID;
        public override bool Equals(object obj) => obj is SerializedObjectID id && Equals(id);
        public static bool operator ==(SerializedObjectID a, SerializedObjectID b) => a.Equals(b);
        public static bool operator !=(SerializedObjectID a, SerializedObjectID b) => !(a == b);
        public override int GetHashCode() => System.HashCode.Combine(MainLFID, PrefabLFID);
    }

    private static SerializedObjectID ObjectToSOI(Object obj)
    {
        using var mainSO = new SerializedObject(obj);
        inspectorModeObject.SetValue(mainSO, InspectorMode.DebugInternal);
        long lfid = mainSO.FindProperty("m_LocalIdentfierInFile").longValue;

        var prefabInstance = mainSO.FindProperty("m_PrefabInstance");
        if (prefabInstance.objectReferenceValue != null)
        {
            using var prefabInstanceSO = new SerializedObject(prefabInstance.objectReferenceValue);
            inspectorModeObject.SetValue(prefabInstanceSO, InspectorMode.DebugInternal);

            using var correspondingSO = new SerializedObject(mainSO.FindProperty("m_CorrespondingSourceObject").objectReferenceValue);
            inspectorModeObject.SetValue(correspondingSO, InspectorMode.DebugInternal);

            long sourceLFID = correspondingSO.FindProperty("m_LocalIdentfierInFile").longValue;
            long prefabLFID = prefabInstanceSO.FindProperty("m_LocalIdentfierInFile").longValue;

            return new SerializedObjectID(sourceLFID, prefabLFID);
        }
        else
        {
            return new SerializedObjectID(lfid, 0);
        }
    }

    public static void CopyAtlasSettingsToRenderers(LightingDataAsset lda, MeshRenderer from, MeshRenderer[] to)
    {
        if (from.lightmapIndex >= 65534 && from.realtimeLightmapIndex >= 65534)
            return;

        using SerializedObject o = new SerializedObject(lda);
        inspectorModeObject.SetValue(o, InspectorMode.DebugInternal);

        // Get LOD0 SOI
        var mainSOI = ObjectToSOI(from);

        // Find LOD0
        int lm0Index = -1;
        using var lmIds = o.FindProperty("m_LightmappedRendererDataIDs");
        for (int i = 0; i < lmIds.arraySize; i++)
        {
            using var elem = lmIds.GetArrayElementAtIndex(i);
            elem.Next(true);
            long main = elem.longValue;
            elem.Next(false);
            long prefab = elem.longValue;
            
            if (mainSOI == new SerializedObjectID(main, prefab))
            {
                lm0Index = i;
                break;
            }
        }

        // Append LODs
        var lmVals = o.FindProperty("m_LightmappedRendererData");
        Debug.Assert(lmIds.arraySize == lmVals.arraySize);
        int baseOffset = lmIds.arraySize;
        lmIds.arraySize += to.Length;
        lmVals.arraySize += to.Length;
        for (int i = 0; i < to.Length; i++)
        {
            MeshRenderer newMr = to[i];
            int lmIndex = baseOffset + i;

            // Set SOI
            var newSOI = ObjectToSOI(newMr);
            using var soiData = lmIds.GetArrayElementAtIndex(lmIndex);
            soiData.Next(true);
            soiData.longValue = newSOI.MainLFID;
            soiData.Next(false);
            soiData.longValue = newSOI.PrefabLFID;

            // Set atlas data
            using var fromAtlasData = lmVals.GetArrayElementAtIndex(lm0Index);
            using var fromAtlasDataEnd = fromAtlasData.Copy();
            fromAtlasDataEnd.Next(false);
            using var toAtlasData = lmVals.GetArrayElementAtIndex(lmIndex);
            toAtlasData.Next(true);
            fromAtlasData.Next(true);
            while (!SerializedProperty.EqualContents(fromAtlasData, fromAtlasDataEnd))
            {
                toAtlasData.boxedValue = fromAtlasData.boxedValue;
                toAtlasData.Next(false);
                fromAtlasData.Next(false);
            }
        }

        o.ApplyModifiedProperties();
    }

    public static void MakeRendererProbeLit(LightingDataAsset lda, MeshRenderer mr)
    {
        using SerializedObject o = new SerializedObject(lda);
        inspectorModeObject.SetValue(o, InspectorMode.DebugInternal);

        var mainSOI = ObjectToSOI(mr);

        // Find mr
        int mrIndex = -1;
        using var lmIds = o.FindProperty("m_LightmappedRendererDataIDs");
        for (int i = 0; i < lmIds.arraySize; i++)
        {
            using var elem = lmIds.GetArrayElementAtIndex(i);
            elem.Next(true);
            long main = elem.longValue;
            elem.Next(false);
            long prefab = elem.longValue;

            if (mainSOI == new SerializedObjectID(main, prefab))
            {
                mrIndex = i;
                break;
            }
        }

        var lmVals = o.FindProperty("m_LightmappedRendererData");
        var atlasData = lmVals.GetArrayElementAtIndex(mrIndex);
        atlasData.FindPropertyRelative("lightmapIndex").intValue = 65534;

        o.ApplyModifiedProperties();
    }

    public static void RefreshLDA()
    {
        Lightmapping.lightingDataAsset = Lightmapping.lightingDataAsset;
    }

    /*static void TODO(LightingDataAsset lda)
    {
        using SerializedObject o = new SerializedObject(lda);
        inspectorModeObject.SetValue(o, InspectorMode.DebugInternal);

        // SOI -> Index in m_LightmappedRendererData
        Dictionary<SerializedObjectID, int> seralizedIdToIndex = new Dictionary<SerializedObjectID, int>();
        var lmIds = o.FindProperty("m_LightmappedRendererDataIDs");
        for (int i = 0; i < lmIds.arraySize; i++)
        {
            var elem = lmIds.GetArrayElementAtIndex(i);
            elem.Next(true);
            long main = elem.longValue;
            elem.Next(false);
            long prefab = elem.longValue;
            seralizedIdToIndex.Add(new SerializedObjectID(main, prefab), i);
        }

        // MeshRenderer -> m_LightmappedRendererData
        MeshRenderer[] mrToAtlasData = new MeshRenderer[lmIds.arraySize];
        var allMr = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var mr in allMr)
        {
            using var mainSO = new SerializedObject(mr);
            inspectorModeObject.SetValue(mainSO, InspectorMode.DebugInternal);
            long lfid = mainSO.FindProperty("m_LocalIdentfierInFile").longValue;

            SerializedObjectID id;

            var prefabInstance = mainSO.FindProperty("m_PrefabInstance");
            if (prefabInstance.objectReferenceValue != null)
            {
                using var prefabInstanceSO = new SerializedObject(prefabInstance.objectReferenceValue);
                inspectorModeObject.SetValue(prefabInstanceSO, InspectorMode.DebugInternal);

                using var correspondingSO = new SerializedObject(mainSO.FindProperty("m_CorrespondingSourceObject").objectReferenceValue);
                inspectorModeObject.SetValue(correspondingSO, InspectorMode.DebugInternal);

                long sourceLFID = correspondingSO.FindProperty("m_LocalIdentfierInFile").longValue;
                long prefabLFID = prefabInstanceSO.FindProperty("m_LocalIdentfierInFile").longValue;

                id = new SerializedObjectID(sourceLFID, prefabLFID);
            }
            else
            {
                id = new SerializedObjectID(lfid, 0);
            }

            if (seralizedIdToIndex.TryGetValue(id, out int idx))
            {
                mrToAtlasData[idx] = mr;
            }
        }

        var lmVals = o.FindProperty("m_LightmappedRendererData");
        for (int i = 0; i < mrToAtlasData.Length; i++)
        {
            var mr = mrToAtlasData[i];

            if (to.Contains(mr))
            {
                var atlasData = lmVals.GetArrayElementAtIndex(i).;
                var lightmapST = atlasData.FindPropertyRelative("lightmapST");
                lightmapST.vector4Value = from.lightmapScaleOffset;
            }
        }
        o.ApplyModifiedProperties();
    }*/

}


