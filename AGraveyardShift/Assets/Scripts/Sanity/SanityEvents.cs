using System;

/// <summary>
/// Event definitions for the sanity system.
/// Follows the same pattern as GameFlowEvents and SideGameEvents.
/// </summary>
public class SanityEvents
{
    /// <summary>Fired when sanity value changes. Parameters: (currentSanity, maxSanity)</summary>
    public event Action<int, int> onSanityChanged;
    public void SanityChanged(int current, int max)
    {
        onSanityChanged?.Invoke(current, max);
    }

    /// <summary>Fired when sanity reaches zero.</summary>
    public event Action onSanityDepleted;
    public void SanityDepleted()
    {
        onSanityDepleted?.Invoke();
    }

    /// <summary>Fired when a sanity threshold is crossed going down. Parameter: threshold ID</summary>
    public event Action<string> onThresholdCrossedDown;
    public void ThresholdCrossedDown(string thresholdId)
    {
        onThresholdCrossedDown?.Invoke(thresholdId);
    }

    /// <summary>Fired when a sanity threshold is crossed going up (recovery). Parameter: threshold ID</summary>
    public event Action<string> onThresholdCrossedUp;
    public void ThresholdCrossedUp(string thresholdId)
    {
        onThresholdCrossedUp?.Invoke(thresholdId);
    }

    /// <summary>Fired when sanity is fully restored.</summary>
    public event Action onSanityRestored;
    public void SanityRestored()
    {
        onSanityRestored?.Invoke();
    }

    /// <summary>Fired when crucifix falls from door.</summary>
    public event Action onCrucifixFell;
    public void CrucifixFell()
    {
        onCrucifixFell?.Invoke();
    }

    /// <summary>Fired when crucifix is re-affixed.</summary>
    public event Action onCrucifixAffixed;
    public void CrucifixAffixed()
    {
        onCrucifixAffixed?.Invoke();
    }

    /// <summary>Fired when a lamp state changes. Parameters: (lampName, isLit)</summary>
    public event Action<string, bool> onLampStateChanged;
    public void LampStateChanged(string lampName, bool isLit)
    {
        onLampStateChanged?.Invoke(lampName, isLit);
    }
}
