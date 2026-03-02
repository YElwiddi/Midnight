using System;

/// <summary>
/// Event definitions for the game flow system.
/// Follows the same pattern as DialogueEvents.
/// </summary>
public class GameFlowEvents
{
    /// <summary>Fired when a game event starts. Parameter is the event name.</summary>
    public event Action<string> onEventStarted;
    public void EventStarted(string eventName)
    {
        onEventStarted?.Invoke(eventName);
    }

    /// <summary>Fired when a game event completes. Parameter is the event name.</summary>
    public event Action<string> onEventCompleted;
    public void EventCompleted(string eventName)
    {
        onEventCompleted?.Invoke(eventName);
    }

    /// <summary>Fired when all events in the queue have completed.</summary>
    public event Action onAllEventsCompleted;
    public void AllEventsCompleted()
    {
        onAllEventsCompleted?.Invoke();
    }
}
