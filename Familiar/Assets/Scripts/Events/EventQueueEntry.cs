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

    /// <summary>
    /// Gets the event to execute based on the selection mode.
    /// Returns null if no valid event is available.
    /// </summary>
    public GameEvent GetEvent()
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
                if (validEvents.Count == 0)
                {
                    Debug.LogWarning("EventQueueEntry: Event group has no valid events!");
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
}
