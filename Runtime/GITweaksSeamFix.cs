#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GITweaksSeamFix : MonoBehaviour
{
    public MeshRenderer[] RenderersToFixSeamsWith;
    // TODO: Bounds

    public void Reset()
    {
    }
}

#else
public class GITweaksSeamFix : MonoBehaviour {}
#endif