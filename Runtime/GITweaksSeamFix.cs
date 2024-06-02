#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GITweaksSeamFix : MonoBehaviour
{
    public bool RunOnBake = true;
    public MeshRenderer[] RenderersToFixSeamsWith;

    [Range(0, 180)] public float MaxSearchAngle = 15; 

    [Min(1)] public int MaxSolverIterationCount = 100;
    [Min(0)] public float SolverTolerance = 0.001f;
    [Min(0)] public float SolverStrength = 5.0f;
}

#else
public class GITweaksSeamFix : MonoBehaviour {}
#endif