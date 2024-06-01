#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GITweaksSeamFix : MonoBehaviour
{
    public MeshRenderer[] RenderersToFixSeamsWith;

    [Min(0)] public float MaxSearchDistance = 0.01f;
    [Min(0)] public float SampleDensityMultiplier = 1.1f;
    // TODO: Take normals into account

    [Min(1)] public int MaxSolverIterationCount = 10000;
    [Min(0)] public float SolverTolerance = 0.001f;
    [Min(0)] public float SolverStrength = 5.0f;
    // TODO: Bounds

    public void Reset()
    {
    }
}

#else
public class GITweaksSeamFix : MonoBehaviour {}
#endif