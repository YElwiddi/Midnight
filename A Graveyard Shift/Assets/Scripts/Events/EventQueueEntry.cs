using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines how an event is selected from the queue entry.
/// </summary>
public enum EventSelectionMode
{
    Single,         // Use a single specified event
    RandomFromGroup, // Randomly select one event from a group
    KillerEvent     // Use a conditional killer event (checks condition and spawns killer if met)
}

/// <summary>
/// Defines the type of condition to check before playing an event.
/// </summary>
public enum EventConditionType
{
    None,           // No condition, always play
    IntStat,        // Check an integer stat against a fixed value
    StatVsStat      // Compare one stat against another stat
}

/// <summary>
/// Defines comparison operators for integer stat conditions.
/// </summary>
public enum StatComparison
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual
}

/// <summary>
/// Defines what spawn conditions must be met for the NPC to appear.
/// </summary>
public enum SpawnConditionRequirement
{
    None,           // No spawn conditions, spawn immediately
    ZoneOnly,       // Only check if player is in zone
    FacingOnly,     // Only check if player is facing object
    Both,           // Both zone and facing conditions must be true
    Either          // Either zone or facing condition must be true
}

/// <summary>
/// Wrapper class that represents an entry in the event queue.
/// Can be either a single event or a random selection from a group of events.
/// </summary>
[System.Serializable]
public class EventQueueEntry
{
    [Tooltip("How to select the event for this queue entry")]
    public EventSelectionMode selectionMode = EventSelectionMode.Single;

    [Tooltip("The single event to use (when Selection Mode is Single)")]
    public GameEvent singleEvent;

    [Tooltip("Group of events to randomly select from (when Selection Mode is RandomFromGroup)")]
    public List<GameEvent> eventGroup = new List<GameEvent>();

    [Tooltip("If enabled, events that were already selected in previous queue entries will be excluded from random selection")]
    public bool excludePreviouslySelectedEvents = false;

    [Header("Killer Event (when Selection Mode is KillerEvent)")]
    [Tooltip("The conditional killer event to use. Has its own built-in condition check.")]
    public ConditionalKillerEvent killerEvent;

    [Header("Condition (Optional)")]
    [Tooltip("Type of condition to check before playing this event")]
    public EventConditionType conditionType = EventConditionType.None;

    [Tooltip("Name of the stat to check (e.g., 'player_scared', 'player_mean', 'SpiritAngered') - used for IntStat and StatVsStat conditions")]
    public string statName = "";

    [Tooltip("How to compare the stat value - used for IntStat and StatVsStat conditions")]
    public StatComparison statComparison = StatComparison.GreaterOrEqual;

    [Tooltip("Value to compare against - used when Condition Type is IntStat")]
    public int statValue = 0;

    [Tooltip("Name of the second stat to compare against - used when Condition Type is StatVsStat")]
    public string compareToStatName = "";

    [Header("Game Phase")]
    [Tooltip("Set the game phase when this event starts. -1 = don't change phase. Used by SideGameEventManager to filter which side events can spawn.")]
    [Range(-1, 10)]
    public int setPhaseOnStart = -1;

    [Header("Timing")]
    [Tooltip("Time to wait (in seconds) after this event completes before starting the next event")]
    [Range(0f, 120f)]
    public float waitTimeBeforeNextEvent = 0f;

    [Header("Spawn Conditions (Optional)")]
    [Tooltip("What spawn conditions must be met before the NPC appears")]
    public SpawnConditionRequirement spawnConditionRequirement = SpawnConditionRequirement.None;

    [Tooltip("Name of the trigger zone the player must enter (used when SpawnConditionRequirement includes zone check)")]
    public string requiredZoneName = "";

    [Tooltip("Name of the object the player must be facing/looking at (used when SpawnConditionRequirement includes facing check)")]
    public string requiredFacingObjectName = "";

    /// <summary>
    /// Gets the event to execute based on the selection mode.
    /// Returns null if no valid event is available.
    /// </summary>
    public GameEvent GetEvent()
    {
        return GetEvent(null);
    }

    /// <summary>
    /// Gets the event to execute based on the selection mode, optionally excluding previously selected events.
    /// </summary>
    /// <param name="eventsToExclude">Set of events to exclude from random selection (only used if excludePreviouslySelectedEvents is true)</param>
    public GameEvent GetEvent(HashSet<GameEvent> eventsToExclude)
    {
        switch (selectionMode)
        {
            case EventSelectionMode.Single:
                return singleEvent;

            case EventSelectionMode.RandomFromGroup:
                if (eventGroup == null || eventGroup.Count == 0)
                {
                    Debug.LogWarning("EventQueueEntry: Event group is empty!");
                    return null;
                }
                // Filter out null entries
                var validEvents = eventGroup.FindAll(e => e != null);

                // If exclusion is enabled and we have events to exclude, filter them out
                if (excludePreviouslySelectedEvents && eventsToExclude != null && eventsToExclude.Count > 0)
                {
                    validEvents = validEvents.FindAll(e => !eventsToExclude.Contains(e));
                }

                if (validEvents.Count == 0)
                {
                    Debug.LogWarning("EventQueueEntry: Event group has no valid events after exclusion!");
                    return null;
                }
                int randomIndex = Random.Range(0, validEvents.Count);
                return validEvents[randomIndex];

            default:
                return singleEvent;
        }
    }

    /// <summary>
    /// Gets a display name for this entry (for debugging).
    /// </summary>
    public string GetDisplayName()
    {
        switch (selectionMode)
        {
            case EventSelectionMode.Single:
                return singleEvent != null ? singleEvent.eventName : "(No Event)";

            case EventSelectionMode.RandomFromGroup:
                if (eventGroup == null || eventGroup.Count == 0)
                    return "(Empty Group)";
                return $"Random ({eventGroup.Count} events)";

            case EventSelectionMode.KillerEvent:
                return killerEvent != null ? $"[KILLER] {killerEvent.eventName}" : "(No Killer Event)";

            default:
                return "(Unknown)";
        }
    }

    /// <summary>
    /// Returns true if this entry is a killer event.
    /// </summary>
    public bool IsKillerEvent()
    {
        return selectionMode == EventSelectionMode.KillerEvent;
    }

    /// <summary>
    /// Gets the killer event (only valid when selectionMode is KillerEvent).
    /// </summary>
    public ConditionalKillerEvent GetKillerEvent()
    {
        return selectionMode == EventSelectionMode.KillerEvent ? killerEvent : null;
    }

    /// <summary>
    /// Checks if the condition for this event entry is met.
    /// Returns true if there's no condition or if the condition passes.
    /// For killer events, uses the killer event's built-in condition.
    /// </summary>
    public bool CheckCondition()
    {
        // For killer events, use the killer event's built-in condition check
        if (selectionMode == EventSelectionMode.KillerEvent)
        {
            if (killerEvent == null)
            {
                Debug.LogWarning("EventQueueEntry: Killer event is null!");
                return false;
            }
            return killerEvent.CheckCondition();
        }

        // For regular events, use the EventQueueEntry's condition settings
        if (conditionType == EventConditionType.None)
        {
            return true;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogWarning("EventQueueEntry: GameManager not found, skipping condition check");
            return true;
        }

        switch (conditionType)
        {
            case EventConditionType.IntStat:
                return CheckIntStatCondition(gameManager);

            case EventConditionType.StatVsStat:
                return CheckStatVsStatCondition(gameManager);

            default:
                return true;
        }
    }

    private bool CheckIntStatCondition(GameManager gameManager)
    {
        if (string.IsNullOrEmpty(statName))
        {
            Debug.LogWarning("EventQueueEntry: Stat name is empty for IntStat condition");
            return true;
        }

        int currentValue = gameManager.GetStatValue(statName);

        bool result = statComparison switch
        {
            StatComparison.Equals => currentValue == statValue,
            StatComparison.NotEquals => currentValue != statValue,
            StatComparison.GreaterThan => currentValue > statValue,
            StatComparison.LessThan => currentValue < statValue,
            StatComparison.GreaterOrEqual => currentValue >= statValue,
            StatComparison.LessOrEqual => currentValue <= statValue,
            _ => true
        };

        Debug.Log($"EventQueueEntry: Condition check - {statName} ({currentValue}) {statComparison} {statValue} = {result}");
        return result;
    }

    private bool CheckStatVsStatCondition(GameManager gameManager)
    {
        if (string.IsNullOrEmpty(statName))
        {
            Debug.LogWarning("EventQueueEntry: First stat name is empty for StatVsStat condition");
            return true;
        }

        if (string.IsNullOrEmpty(compareToStatName))
        {
            Debug.LogWarning("EventQueueEntry: Second stat name (compareToStatName) is empty for StatVsStat condition");
            return true;
        }

        int firstStatValue = gameManager.GetStatValue(statName);
        int secondStatValue = gameManager.GetStatValue(compareToStatName);

        bool result = statComparison switch
        {
            StatComparison.Equals => firstStatValue == secondStatValue,
            StatComparison.NotEquals => firstStatValue != secondStatValue,
            StatComparison.GreaterThan => firstStatValue > secondStatValue,
            StatComparison.LessThan => firstStatValue < secondStatValue,
            StatComparison.GreaterOrEqual => firstStatValue >= secondStatValue,
            StatComparison.LessOrEqual => firstStatValue <= secondStatValue,
            _ => true
        };

        Debug.Log($"EventQueueEntry: StatVsStat check - {statName} ({firstStatValue}) {statComparison} {compareToStatName} ({secondStatValue}) = {result}");
        return result;
    }

    /// <summary>
    /// Checks if the spawn conditions for this event entry are met.
    /// Returns true if there are no spawn conditions or if the required conditions pass.
    /// </summary>
    /// <param name="playerTransform">The player's transform for position/facing checks</param>
    /// <param name="playerCamera">The player's camera for facing raycast checks</param>
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

        Debug.Log($"EventQueueEntry: Spawn condition check - Zone({requiredZoneName})={inZone}, Facing({requiredFacingObjectName})={facingObject}, Requirement={spawnConditionRequirement}, Result={result}");
        return result;
    }

    /// <summary>
    /// Checks if the player is currently inside the required zone.
    /// </summary>
    private bool CheckZoneCondition(Transform playerTransform)
    {
        if (string.IsNullOrEmpty(requiredZoneName))
        {
            // No zone specified, condition passes
            return true;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("EventQueueEntry: Player transform is null for zone check");
            return false;
        }

        // Find the zone by name
        GameObject zoneObject = GameObject.Find(requiredZoneName);
        if (zoneObject == null)
        {
            Debug.LogWarning($"EventQueueEntry: Zone '{requiredZoneName}' not found");
            return false;
        }

        Collider zoneCollider = zoneObject.GetComponent<Collider>();
        if (zoneCollider == null)
        {
            Debug.LogWarning($"EventQueueEntry: Zone '{requiredZoneName}' has no Collider component");
            return false;
        }

        // Check if the player's position is within the collider bounds
        return zoneCollider.bounds.Contains(playerTransform.position);
    }

    /// <summary>
    /// Checks if the player is currently facing/looking at the required object.
    /// First checks for FacingTarget components (no collider needed), then falls back to raycast.
    /// </summary>
    private bool CheckFacingCondition(Camera playerCamera)
    {
        if (string.IsNullOrEmpty(requiredFacingObjectName))
        {
            // No facing object specified, condition passes
            return true;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning("EventQueueEntry: Player camera is null for facing check");
            return false;
        }

        // First, check for FacingTarget components (no collider required)
        FacingTarget[] facingTargets = Object.FindObjectsByType<FacingTarget>(FindObjectsSortMode.None);
        foreach (var target in facingTargets)
        {
            if (target.targetId == requiredFacingObjectName && target.IsCameraFacing(playerCamera))
            {
                return true;
            }
        }

        // Fallback: Raycast from camera to see what player is looking at (requires collider)
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Check if the hit object or any of its parents match the required name
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

    /// <summary>
    /// Returns true if this event entry has any spawn conditions that need to be checked.
    /// </summary>
    public bool HasSpawnConditions()
    {
        return spawnConditionRequirement != SpawnConditionRequirement.None;
    }
}
