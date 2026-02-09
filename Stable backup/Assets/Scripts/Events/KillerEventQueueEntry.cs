using UnityEngine;

/// <summary>
/// Queue entry for conditional killer events.
/// The event checks its condition and either plays (spawning a killer NPC) or is skipped.
/// </summary>
[System.Serializable]
public class KillerEventQueueEntry
{
    [Tooltip("The conditional killer event to play")]
    public ConditionalKillerEvent killerEvent;

    [Header("Spawn Conditions (Optional)")]
    [Tooltip("What spawn conditions must be met before the killer NPC appears")]
    public SpawnConditionRequirement spawnConditionRequirement = SpawnConditionRequirement.None;

    [Tooltip("Name of the trigger zone the player must enter")]
    public string requiredZoneName = "";

    [Tooltip("Name of the object the player must be facing/looking at")]
    public string requiredFacingObjectName = "";

    [Header("Timing")]
    [Tooltip("Time to wait (in seconds) after this event completes before starting the next event")]
    [Range(0f, 120f)]
    public float waitTimeBeforeNextEvent = 0f;

    /// <summary>
    /// Gets a display name for this entry (for debugging).
    /// </summary>
    public string GetDisplayName()
    {
        return killerEvent != null ? killerEvent.eventName : "(No Killer Event)";
    }

    /// <summary>
    /// Checks if the condition for this killer event is met.
    /// Delegates to the ConditionalKillerEvent's condition check.
    /// </summary>
    public bool CheckCondition()
    {
        if (killerEvent == null)
        {
            Debug.LogWarning("KillerEventQueueEntry: No killer event assigned!");
            return false;
        }

        return killerEvent.CheckCondition();
    }

    /// <summary>
    /// Returns true if this event entry has any spawn conditions that need to be checked.
    /// </summary>
    public bool HasSpawnConditions()
    {
        return spawnConditionRequirement != SpawnConditionRequirement.None;
    }

    /// <summary>
    /// Checks if the spawn conditions for this event entry are met.
    /// </summary>
    public bool CheckSpawnConditions(Transform playerTransform, Camera playerCamera)
    {
        if (spawnConditionRequirement == SpawnConditionRequirement.None)
        {
            return true;
        }

        bool inZone = CheckZoneCondition(playerTransform);
        bool facingObject = CheckFacingCondition(playerCamera);

        bool result = spawnConditionRequirement switch
        {
            SpawnConditionRequirement.ZoneOnly => inZone,
            SpawnConditionRequirement.FacingOnly => facingObject,
            SpawnConditionRequirement.Both => inZone && facingObject,
            SpawnConditionRequirement.Either => inZone || facingObject,
            _ => true
        };

        Debug.Log($"KillerEventQueueEntry: Spawn condition check - Zone({requiredZoneName})={inZone}, Facing({requiredFacingObjectName})={facingObject}, Result={result}");
        return result;
    }

    private bool CheckZoneCondition(Transform playerTransform)
    {
        if (string.IsNullOrEmpty(requiredZoneName))
        {
            return true;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("KillerEventQueueEntry: Player transform is null for zone check");
            return false;
        }

        GameObject zoneObject = GameObject.Find(requiredZoneName);
        if (zoneObject == null)
        {
            Debug.LogWarning($"KillerEventQueueEntry: Zone '{requiredZoneName}' not found");
            return false;
        }

        Collider zoneCollider = zoneObject.GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Debug.LogWarning($"KillerEventQueueEntry: Zone '{requiredZoneName}' has no Collider component");
            return false;
        }

        return zoneCollider.bounds.Contains(playerTransform.position);
    }

    private bool CheckFacingCondition(Camera playerCamera)
    {
        if (string.IsNullOrEmpty(requiredFacingObjectName))
        {
            return true;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning("KillerEventQueueEntry: Player camera is null for facing check");
            return false;
        }

        // Check FacingTarget components first
        FacingTarget[] facingTargets = Object.FindObjectsByType<FacingTarget>(FindObjectsSortMode.None);
        foreach (var target in facingTargets)
        {
            if (target.targetId == requiredFacingObjectName && target.IsCameraFacing(playerCamera))
            {
                return true;
            }
        }

        // Fallback to raycast
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Transform current = hit.transform;
            while (current != null)
            {
                if (current.name == requiredFacingObjectName)
                {
                    return true;
                }
                current = current.parent;
            }
        }

        return false;
    }
}
