using UnityEngine;

/// <summary>
/// Marker component that identifies an object as a valid facing target for spawn conditions.
/// Add this to any object you want players to be able to "look at" without requiring a Collider.
/// </summary>
public class FacingTarget : MonoBehaviour
{
    [Tooltip("Unique identifier for this facing target. This is what you enter in 'Required Facing Object Name' in the Event Queue Entry.")]
    public string targetId = "";

    [Tooltip("Maximum angle (in degrees) from the center of the player's view to count as 'facing' this target. Lower = more precise.")]
    [Range(1f, 45f)]
    public float maxViewAngle = 15f;

    [Tooltip("Maximum distance from the player to count as 'facing' this target. 0 = unlimited.")]
    public float maxDistance = 0f;

    [Tooltip("If true, requires an unobstructed line of sight to the target (raycasts to check for obstacles).")]
    public bool requireLineOfSight = false;

    [Tooltip("Layers that block line of sight (only used if Require Line Of Sight is true).")]
    public LayerMask lineOfSightBlockers = ~0; // Default to everything

    private void OnValidate()
    {
        // Auto-populate targetId with GameObject name if empty
        if (string.IsNullOrEmpty(targetId))
        {
            targetId = gameObject.name;
        }
    }

    private void Awake()
    {
        // Ensure targetId is set
        if (string.IsNullOrEmpty(targetId))
        {
            targetId = gameObject.name;
        }
    }

    /// <summary>
    /// Checks if the given camera is facing this target.
    /// </summary>
    public bool IsCameraFacing(Camera camera)
    {
        if (camera == null) return false;

        Vector3 directionToTarget = transform.position - camera.transform.position;
        float distance = directionToTarget.magnitude;

        // Check max distance (if set)
        if (maxDistance > 0f && distance > maxDistance)
        {
            return false;
        }

        // Check angle
        float angle = Vector3.Angle(camera.transform.forward, directionToTarget);
        if (angle > maxViewAngle)
        {
            return false;
        }

        // Check line of sight (if required)
        if (requireLineOfSight)
        {
            if (Physics.Raycast(camera.transform.position, directionToTarget.normalized, out RaycastHit hit, distance, lineOfSightBlockers))
            {
                // Something is blocking the view - check if it's this object or a child
                if (!hit.transform.IsChildOf(transform) && hit.transform != transform)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the detection area
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (maxDistance > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}
