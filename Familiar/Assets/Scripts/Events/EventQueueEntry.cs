using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines how an event is selected from the queue entry.
/// </summary>
public enum EventSelectionMode
{
    Single,         // Use a single specified event
    RandomFromGroup // Randomly select one event from a group
}

/// <summary>
/// Defines the type of condition to check before playing an event.
/// </summary>
public enum EventConditionType
{
    None,           // No condition, always play
    IntStat,        // Check an integer stat from GameManager
    BoolFlag        // Check a bool flag from GameManager
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

    [Header("Condition (Optional)")]
    [Tooltip("Type of condition to check before playing this event")]
    public EventConditionType conditionType = EventConditionType.None;

    [Tooltip("Name of the stat to check (e.g., 'friendly', 'brave') - used when Condition Type is IntStat")]
    public string statName = "";

    [Tooltip("How to compare the stat value - used when Condition Type is IntStat")]
    public StatComparison statComparison = StatComparison.GreaterOrEqual;

    [Tooltip("Value to compare against - used when Condition Type is IntStat")]
    public int statValue = 0;

    [Tooltip("Name of the bool flag to check (e.g., 'metHuang', 'helpedHuang') - used when Condition Type is BoolFlag")]
    public string boolName = "";

    [Tooltip("Required value of the bool flag - used when Condition Type is BoolFlag")]
    public bool boolValue = true;

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

            default:
                return "(Unknown)";
        }
    }

    /// <summary>
    /// Checks if the condition for this event entry is met.
    /// Returns true if there's no condition or if the condition passes.
    /// </summary>
    public bool CheckCondition()
    {
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

            case EventConditionType.BoolFlag:
                return CheckBoolCondition(gameManager);

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

    private bool CheckBoolCondition(GameManager gameManager)
    {
        if (string.IsNullOrEmpty(boolName))
        {
            Debug.LogWarning("EventQueueEntry: Bool name is empty for BoolFlag condition");
            return true;
        }

        bool currentValue = gameManager.GetBoolValue(boolName);
        bool result = currentValue == boolValue;

        Debug.Log($"EventQueueEntry: Condition check - {boolName} ({currentValue}) == {boolValue} = {result}");
        return result;
    }
}
