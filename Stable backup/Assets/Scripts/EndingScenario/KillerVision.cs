using UnityEngine;

/// <summary>
/// Handles vision detection for the crypt killer using raycast-based vision cone.
/// </summary>
public class KillerVision : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionDistance = 15f;
    [SerializeField] private float visionAngle = 60f;
    [SerializeField] private int rayCount = 7;
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField] private float visionHeight = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private Color debugRayColor = Color.yellow;
    [SerializeField] private Color debugRayHitColor = Color.green;

    private Transform playerTransform;
    private Camera playerCamera;

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        playerCamera = Camera.main;
    }

    /// <summary>
    /// Configure vision settings from a CryptKillerConfig.
    /// </summary>
    public void Configure(CryptKillerConfig config)
    {
        visionDistance = config.visionDistance;
        visionAngle = config.visionAngle;
        rayCount = config.visionRayCount;
        blockingLayers = config.visionBlockingLayers;
        visionHeight = config.visionHeight;
    }

    /// <summary>
    /// Checks if the player is visible to the killer.
    /// Uses multiple raycasts in a cone pattern for reliability.
    /// </summary>
    public bool CanSeePlayer()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return false;
        }

        Vector3 eyePosition = transform.position + Vector3.up * visionHeight;
        Vector3 directionToPlayer = (playerTransform.position + Vector3.up * 1f) - eyePosition;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Check distance first
        if (distanceToPlayer > visionDistance)
        {
            return false;
        }

        // Check if player is within vision angle
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 toPlayerHorizontal = directionToPlayer;
        toPlayerHorizontal.y = 0;
        toPlayerHorizontal.Normalize();

        float angle = Vector3.Angle(forward, toPlayerHorizontal);
        if (angle > visionAngle)
        {
            return false;
        }

        // Perform raycast to check for obstructions
        // Cast multiple rays to different parts of the player for reliability
        Vector3[] targetPoints = new Vector3[]
        {
            playerTransform.position + Vector3.up * 0.5f,  // Lower body
            playerTransform.position + Vector3.up * 1.0f,  // Torso
            playerTransform.position + Vector3.up * 1.6f   // Head
        };

        foreach (Vector3 targetPoint in targetPoints)
        {
            Vector3 rayDirection = (targetPoint - eyePosition).normalized;
            float rayDistance = Vector3.Distance(eyePosition, targetPoint);

            if (showDebugRays)
            {
                Debug.DrawRay(eyePosition, rayDirection * rayDistance, debugRayColor);
            }

            // Cast ray - if it doesn't hit anything blocking, or hits the player, we can see them
            if (Physics.Raycast(eyePosition, rayDirection, out RaycastHit hit, rayDistance, blockingLayers))
            {
                // Check if we hit the player
                if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                {
                    if (showDebugRays)
                    {
                        Debug.DrawRay(eyePosition, rayDirection * rayDistance, debugRayHitColor);
                        Debug.Log($"KillerVision: Player VISIBLE - raycast hit player directly on layer {LayerMask.LayerToName(hit.transform.gameObject.layer)}");
                    }
                    return true;
                }
                // Otherwise, something is blocking the view for this ray
            }
            else
            {
                // Nothing blocking - we have clear line of sight
                // But we need to verify the player is actually there
                // Do a longer raycast that includes the player layer
                int allLayers = blockingLayers | (1 << playerTransform.gameObject.layer);
                if (Physics.Raycast(eyePosition, rayDirection, out hit, visionDistance, allLayers))
                {
                    if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                    {
                        if (showDebugRays)
                        {
                            Debug.DrawRay(eyePosition, rayDirection * rayDistance, debugRayHitColor);
                        }
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the last known position of the player.
    /// </summary>
    public Vector3 GetPlayerPosition()
    {
        if (playerTransform == null)
        {
            FindPlayer();
        }
        return playerTransform != null ? playerTransform.position : transform.position;
    }

    /// <summary>
    /// Gets the distance to the player.
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return float.MaxValue;
        }
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw vision cone
        Vector3 eyePosition = transform.position + Vector3.up * visionHeight;

        Gizmos.color = Color.yellow;

        // Draw vision distance sphere
        Gizmos.DrawWireSphere(eyePosition, visionDistance);

        // Draw vision cone edges
        Vector3 leftEdge = Quaternion.Euler(0, -visionAngle, 0) * transform.forward * visionDistance;
        Vector3 rightEdge = Quaternion.Euler(0, visionAngle, 0) * transform.forward * visionDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(eyePosition, eyePosition + leftEdge);
        Gizmos.DrawLine(eyePosition, eyePosition + rightEdge);
        Gizmos.DrawLine(eyePosition, eyePosition + transform.forward * visionDistance);
    }
}
