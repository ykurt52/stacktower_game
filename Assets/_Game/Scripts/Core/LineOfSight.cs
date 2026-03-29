using UnityEngine;

/// <summary>
/// Static line-of-sight utility using Physics.Linecast.
/// Checks if there's a clear path between two positions (no Obstacle layer blocking).
/// When no obstacles exist in the scene, always returns true (zero overhead).
/// </summary>
public static class LineOfSight
{
    private static readonly int OBSTACLE_LAYER = LayerMask.NameToLayer("Obstacle");
    private static LayerMask _obstacleMask;
    private static bool _initialized;

    private static void Init()
    {
        if (_initialized) return;
        int layer = LayerMask.NameToLayer("Obstacle");
        _obstacleMask = (layer >= 0) ? (1 << layer) : 0;
        _initialized = true;
    }

    /// <summary>
    /// Returns true if there's a clear line of sight between two positions.
    /// If Obstacle layer doesn't exist or mask is 0, always returns true.
    /// </summary>
    public static bool Check(Vector3 from, Vector3 to)
    {
        Init();

        // No obstacle layer configured — always visible (no overhead)
        if (_obstacleMask == 0) return true;

        // Raise to chest height to avoid ground hits
        Vector3 a = from + Vector3.up * 0.5f;
        Vector3 b = to + Vector3.up * 0.5f;

        return !Physics.Linecast(a, b, _obstacleMask);
    }
}
