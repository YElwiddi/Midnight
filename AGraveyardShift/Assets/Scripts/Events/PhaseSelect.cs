using UnityEngine;

/// <summary>
/// Which phase ("watch") of the night the next run starts at. Picked from the main menu's
/// phase select (or its "Return to the ... Watch" button after a death) and read by
/// GameFlowManager when the first event starts, and by CabinNoteSchedule for which notes are
/// already on the desk. A later start skips the earlier visitors and their consequences:
/// stats start fresh.
/// Each watch unlocks once the player has reached it (GameFlowManager reports every phase it
/// enters); beating the game (ending III) unlocks all of them, as it did before.
/// </summary>
public static class PhaseSelect
{
    public const int FirstPhase = 1;
    public const int LastSelectablePhase = 3;

    private const string MaxReachedKey = "MaxWatchReached";

    private static int startPhase = FirstPhase;

    /// <summary>Phase the current (or next) run starts at. 1 = the whole night.</summary>
    public static int StartPhase
    {
        get { return startPhase; }
        set { startPhase = Clamp(value); }
    }

    /// <summary>The furthest watch the player has ever reached.</summary>
    public static int MaxReached => Clamp(PlayerPrefs.GetInt(MaxReachedKey, FirstPhase));

    /// <summary>Phase select is offered once any later watch can be picked.</summary>
    public static bool IsUnlocked => IsAvailable(FirstPhase + 1);

    /// <summary>Whether the night can start at this watch.</summary>
    public static bool IsAvailable(int phase)
    {
        return phase <= MaxReached || EndingsSave.IsUnlocked(Ending.WrongfulConviction);
    }

    /// <summary>Called whenever a run enters a phase.</summary>
    public static void MarkReached(int phase)
    {
        phase = Clamp(phase);
        if (phase <= MaxReached) return;
        PlayerPrefs.SetInt(MaxReachedKey, phase);
        PlayerPrefs.Save();
    }

    /// <summary>Back to a full-night start (called when the main menu loads).</summary>
    public static void Reset()
    {
        startPhase = FirstPhase;
    }

    private static int Clamp(int value)
    {
        return value < FirstPhase ? FirstPhase : (value > LastSelectablePhase ? LastSelectablePhase : value);
    }
}
