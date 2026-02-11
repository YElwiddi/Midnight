using System;

/// <summary>
/// Event definitions for the side game event system.
/// Follows the same pattern as GameFlowEvents.
/// </summary>
public class SideGameEvents
{
    /// <summary>Fired when a side game event starts. Parameter is the event name.</summary>
    public event Action<string> onSideEventStarted;
    public void SideEventStarted(string eventName)
    {
        onSideEventStarted?.Invoke(eventName);
    }

    /// <summary>Fired when a side game event completes. Parameter is the event name.</summary>
    public event Action<string> onSideEventCompleted;
    public void SideEventCompleted(string eventName)
    {
        onSideEventCompleted?.Invoke(eventName);
    }

    /// <summary>Fired when a watcher NPC starts staring at the player.</summary>
    public event Action<string> onWatcherStartedStaring;
    public void WatcherStartedStaring(string watcherName)
    {
        onWatcherStartedStaring?.Invoke(watcherName);
    }

    /// <summary>Fired when a watcher NPC stops staring (retreated or despawned).</summary>
    public event Action<string> onWatcherStoppedStaring;
    public void WatcherStoppedStaring(string watcherName)
    {
        onWatcherStoppedStaring?.Invoke(watcherName);
    }

    /// <summary>Fired when a watcher retreats due to flashlight.</summary>
    public event Action<string> onWatcherRetreated;
    public void WatcherRetreated(string watcherName)
    {
        onWatcherRetreated?.Invoke(watcherName);
    }

    /// <summary>Fired when graveyard protection is depleted and jumpscare triggers.</summary>
    public event Action onProtectionDepletedJumpscare;
    public void ProtectionDepletedJumpscare()
    {
        onProtectionDepletedJumpscare?.Invoke();
    }

    /// <summary>Fired when the game phase changes.</summary>
    public event Action<int> onPhaseChanged;
    public void PhaseChanged(int newPhase)
    {
        onPhaseChanged?.Invoke(newPhase);
    }
}
