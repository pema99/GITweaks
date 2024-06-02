#if UNITY_EDITOR
using UnityEngine;

public class GITweaksSeamFixVolume : MonoBehaviour
{
    public bool RunAfterBaking = true;
    public MeshRenderer[] RenderersToExclude;

    [Range(0, 180)] public float MaxSurfaceAngle = 15;
    [Min(0.001f)] public float SeamFixStrength = 5.0f;

    [Min(1)] public int MaxSolverIterationCount = 100;
    [Min(1e-13f)] public float SolverTolerance = 0.001f;

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