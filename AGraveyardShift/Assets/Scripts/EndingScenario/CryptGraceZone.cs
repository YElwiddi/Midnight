using UnityEngine;

/// <summary>
/// The crypt's arrival nook. EndingScenarioManager holds the crypt killer back while the
/// player is in here and only spawns it once they step out (on entering the crypt, and again
/// after every checkpoint respawn).
/// A plain box (this object's position/rotation + Size), not a trigger collider, so it can
/// never block the interaction raycast or the killer's line of sight.
/// </summary>
public class CryptGraceZone : MonoBehaviour
{
    [Tooltip("Size of the nook box in metres, centred on this object and turned with it (scale is ignored)")]
    public Vector3 size = new Vector3(9f, 5f, 9f);

    /// <summary>True if the world point is inside the nook box.</summary>
    public bool Contains(Vector3 worldPoint)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
        Vector3 half = size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.08f);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
