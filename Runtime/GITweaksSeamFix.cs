#if UNITY_EDITOR
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GITweaksSeamFix : MonoBehaviour
{
    public bool RunAfterBaking = true;
    public MeshRenderer[] RenderersToFixSeamsWith;

    [Range(0, 180)] public float MaxSurfaceAngle = 15; 
    [Min(0.001f)] public float SeamFixStrength = 5.0f;

    [Min(1)] public int MaxSolverIterationCount = 100;
    [Min(1e-13f)] public float SolverTolerance = 0.001f;
}

#else
public class GITweaksSeamFix : MonoBehaviour {}
#endif