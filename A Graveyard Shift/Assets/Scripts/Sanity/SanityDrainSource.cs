using UnityEngine;

/// <summary>
/// Type of sanity drain trigger.
/// </summary>
public enum SanityDrainTrigger
{
    FlashlightOnObject,     // Drains when player shines flashlight on this object
    Proximity,              // Drains when player is near this object
    Always,                 // Always drains (while active)
    Manual                  // Only drains when triggered via script
}

/// <summary>
/// Component that drains player sanity based on various triggers.
/// Attach to tombstones, NPCs, or other objects that should affect sanity.
/// </summary>
public class SanityDrainSource : MonoBehaviour
{
    #region Inspector Settings
    [Header("Drain Settings")]
    [Tooltip("How sanity drain is triggered")]
    [SerializeField] private SanityDrainTrigger drainTrigger = SanityDrainTrigger.FlashlightOnObject;

    [Tooltip("Sanity points drained per second")]
    [SerializeField] private float drainPerSecond = 1f;

    [Tooltip("If true, this drain source is currently active")]
    [SerializeField] private bool isActive = true;

    [Header("Phase Restrictions")]
    [Tooltip("If true, only drains during specified phases. If false, always active.")]
    [SerializeField] private bool usePhaseRestriction = true;

    [Tooltip("Phases during which this drain source is active (uses SanityManager's tombstone phases if empty)")]
    [SerializeField] private int[] activePhases = new int[0];

    [Header("Flashlight Detection (for FlashlightOnObject trigger)")]
    [Tooltip("Angle threshold for flashlight detection")]
    [Range(5f, 60f)]
    [SerializeField] private float flashlightDetectionAngle = 30f;

    [Tooltip("Maximum distance for flashlight to affect this object")]
    [Range(1f, 50f)]
    [SerializeField] private float flashlightMaxDistance = 15f;

    [Tooltip("If true, flashlight must be on to trigger drain")]
    [SerializeField] private bool requireFlashlightOn = true;

    [Tooltip("Ignore obstacles between flashlight and object")]
    [SerializeField] private bool flashlightIgnoreObstacles = true;

    [Tooltip("Height offset for detection point (from object origin)")]
    [SerializeField] private float detectionHeightOffset = 0.5f;

    [Header("Proximity Detection (for Proximity trigger)")]
    [Tooltip("Distance at which proximity drain activates")]
    [Range(1f, 20f)]
    [SerializeField] private float proximityDistance = 5f;

    [Tooltip("If true, requires line of sight to player for proximity drain")]
    [SerializeField] private bool proximityRequiresLineOfSight = false;

    [Header("Layer Mask")]
    [Tooltip("Layers that block line of sight")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;
    #endregion

    #region Private Fields
    private Transform playerTransform;
    private Camera playerCamera;
    private SimpleFlashlight playerFlashlight;
    private bool isDraining = false;
    private bool isRegistered = false;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        FindPlayerReferences();
    }

    private void Update()
    {
        if (!isActive || SanityManager.Instance == null) return;

        // Check phase restriction
        if (usePhaseRestriction && !IsActiveInCurrentPhase())
        {
            if (isDraining)
            {
                StopDraining();
            }
            return;
        }

        bool shouldDrain = false;

        switch (drainTrigger)
        {
            case SanityDrainTrigger.FlashlightOnObject:
                shouldDrain = IsFlashlightOnObject();
                break;

            case SanityDrainTrigger.Proximity:
                shouldDrain = IsPlayerInProximity();
                break;

            case SanityDrainTrigger.Always:
                shouldDrain = true;
                break;

            case SanityDrainTrigger.Manual:
                // Only drain when explicitly triggered
                shouldDrain = isDraining;
                break;
        }

        if (shouldDrain && !isDraining)
        {
            StartDraining();
        }
        else if (!shouldDrain && isDraining)
        {
            StopDraining();
        }

        if (isDraining)
        {
            SanityManager.Instance.DrainSanityPerSecond(drainPerSecond);
        }
    }

    private void OnDisable()
    {
        if (isDraining)
        {
            StopDraining();
        }
    }

    private void OnDestroy()
    {
        if (isDraining)
        {
            StopDraining();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        Vector3 detectionPoint = transform.position + Vector3.up * detectionHeightOffset;

        // Draw detection point
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(detectionPoint, 0.2f);

        // Draw flashlight range
        if (drainTrigger == SanityDrainTrigger.FlashlightOnObject)
        {
            Gizmos.color = isDraining ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(detectionPoint, flashlightMaxDistance);
        }

        // Draw proximity range
        if (drainTrigger == SanityDrainTrigger.Proximity)
        {
            Gizmos.color = isDraining ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, proximityDistance);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Manually start draining (for Manual trigger type).
    /// </summary>
    public void StartManualDrain()
    {
        if (drainTrigger == SanityDrainTrigger.Manual && !isDraining)
        {
            StartDraining();
        }
    }

    /// <summary>
    /// Manually stop draining (for Manual trigger type).
    /// </summary>
    public void StopManualDrain()
    {
        if (drainTrigger == SanityDrainTrigger.Manual && isDraining)
        {
            StopDraining();
        }
    }

    /// <summary>
    /// Set whether this drain source is active.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        if (!active && isDraining)
        {
            StopDraining();
        }
    }

    /// <summary>
    /// Check if currently draining sanity.
    /// </summary>
    public bool IsDraining => isDraining;
    #endregion

    #region Private Methods
    private void FindPlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerFlashlight = player.GetComponentInChildren<SimpleFlashlight>();
            if (playerFlashlight == null)
            {
                playerFlashlight = FindFirstObjectByType<SimpleFlashlight>();
            }
        }

        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }
    }

    private bool IsActiveInCurrentPhase()
    {
        if (SanityManager.Instance == null) return false;

        // If no phases specified, use SanityManager's tombstone phases
        int[] phasesToCheck = activePhases.Length > 0 ? activePhases : null;

        if (phasesToCheck == null || phasesToCheck.Length == 0)
        {
            // Fall back to SanityManager's tombstone drain check
            return SanityManager.Instance.IsTombstoneDrainActiveInCurrentPhase();
        }

        int currentPhase = SanityManager.Instance.CurrentPhase;
        foreach (int phase in phasesToCheck)
        {
            if (phase == currentPhase) return true;
        }

        return false;
    }

    private bool IsFlashlightOnObject()
    {
        if (playerCamera == null) return false;

        // Check if flashlight is on
        if (requireFlashlightOn && playerFlashlight != null && !playerFlashlight.IsFlashlightOn())
        {
            return false;
        }

        Vector3 flashlightPos = playerCamera.transform.position;
        Vector3 flashlightDir = playerCamera.transform.forward;
        Vector3 detectionPoint = transform.position + Vector3.up * detectionHeightOffset;
        Vector3 toObject = detectionPoint - flashlightPos;

        float distance = toObject.magnitude;

        // Check distance
        if (distance > flashlightMaxDistance)
        {
            return false;
        }

        // Check angle
        float angle = Vector3.Angle(flashlightDir, toObject.normalized);
        if (angle > flashlightDetectionAngle)
        {
            return false;
        }

        // Check line of sight (unless ignoring obstacles)
        if (!flashlightIgnoreObstacles)
        {
            if (Physics.Raycast(flashlightPos, toObject.normalized, out RaycastHit hit, distance, blockingLayers))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsPlayerInProximity()
    {
        if (playerTransform == null) return false;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance > proximityDistance)
        {
            return false;
        }

        // Check line of sight if required
        if (proximityRequiresLineOfSight)
        {
            Vector3 dirToPlayer = (playerTransform.position + Vector3.up * 1.5f) - (transform.position + Vector3.up * detectionHeightOffset);
            if (Physics.Raycast(transform.position + Vector3.up * detectionHeightOffset, dirToPlayer.normalized, out RaycastHit hit, dirToPlayer.magnitude, blockingLayers))
            {
                if (hit.transform != playerTransform && !hit.transform.IsChildOf(playerTransform))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void StartDraining()
    {
        isDraining = true;

        if (!isRegistered && SanityManager.Instance != null)
        {
            SanityManager.Instance.RegisterDrainSource();
            isRegistered = true;
        }

        Debug.Log($"SanityDrainSource '{gameObject.name}': Started draining sanity");
    }

    private void StopDraining()
    {
        isDraining = false;

        if (isRegistered && SanityManager.Instance != null)
        {
            SanityManager.Instance.UnregisterDrainSource();
            isRegistered = false;
        }

        Debug.Log($"SanityDrainSource '{gameObject.name}': Stopped draining sanity");
    }
    #endregion
}
