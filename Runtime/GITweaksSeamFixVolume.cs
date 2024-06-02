#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GITweaksSeamFixVolume : MonoBehaviour
{
    public bool RunOnBake = true;
    public MeshRenderer[] RenderersToExclude;

    [Range(0, 180)] public float MaxSearchAngle = 15;

    [Min(1)] public int MaxSolverIterationCount = 100;
    [Min(0)] public float SolverTolerance = 0.001f;
    [Min(0)] public float SolverStrength = 5.0f;

    public void OnDrawGizmosSelected()
    {
        var oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = oldMatrix;
    }
}
#else
public class GITweaksSeamFixVolume : MonoBehaviour {}
#endif