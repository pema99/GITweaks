using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GITweaks
{
    [CustomEditor(typeof(LightingDataAsset))]
    public class GITweaksLDAInspector : Editor
    {
        System.Reflection.PropertyInfo inspectorModeSelf = typeof(Editor).GetProperty("inspectorMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.PropertyInfo inspectorModeObject = typeof(SerializedObject).GetProperty("inspectorMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public override void OnInspectorGUI()
        {
            if (GITweaksSettingsWindow.IsEnabled(GITweak.BetterLDAInspector))
            {
                inspectorModeSelf.SetValue(this, InspectorMode.DebugInternal);
                inspectorModeObject.SetValue(serializedObject, InspectorMode.DebugInternal);
            }
            else
            {
                inspectorModeSelf.SetValue(this, InspectorMode.Normal);
                inspectorModeObject.SetValue(serializedObject, InspectorMode.Normal);
            }
            base.OnInspectorGUI();
        }
    }
}