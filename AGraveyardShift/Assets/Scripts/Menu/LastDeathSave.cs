using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Remembers the watch the last keeper died in, and who took them, so the main menu can lead with
/// "Return to the Second Watch" after a death (GameMainMenu). Cleared as soon as a new run starts.
/// Persists to lastdeath.json in Application.persistentDataPath, like EndingsSave.
/// </summary>
public static class LastDeathSave
{
    [Serializable]
    private class SaveData
    {
        public int returnWatch;
        public string returnCause;
    }

    private const string FileName = "lastdeath.json";
    private static SaveData data;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private static void EnsureLoaded()
    {
        if (data != null) return;
        try
        {
            data = File.Exists(FilePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("LastDeathSave: load failed (" + e.Message + "); starting fresh.");
        }
        if (data == null) data = new SaveData();
    }

    private static void WriteToDisk()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("LastDeathSave: save failed (" + e.Message + ").");
        }
    }

    /// <summary>The watch to offer a return to (0 = no offer).</summary>
    public static int ReturnWatch { get { EnsureLoaded(); return data.returnWatch; } }

    /// <summary>DeathCauses id of the last death.</summary>
    public static string ReturnCause { get { EnsureLoaded(); return data.returnCause; } }

    public static void RecordDeath(string cause, int watch)
    {
        EnsureLoaded();
        data.returnWatch = Mathf.Clamp(watch, 1, 3);
        data.returnCause = string.IsNullOrEmpty(cause) ? DeathCauses.Night : cause;
        WriteToDisk();
        Debug.Log("LastDeathSave: keeper lost to " + data.returnCause + " in watch " + data.returnWatch);
    }

    /// <summary>A new run has started: stop offering to return to the last watch.</summary>
    public static void ClearReturnOffer()
    {
        EnsureLoaded();
        if (data.returnWatch == 0) return;
        data.returnWatch = 0;
        data.returnCause = null;
        WriteToDisk();
    }

    public static void Reload()
    {
        data = null;
        EnsureLoaded();
    }
}

/// <summary>Who took a keeper, and the localized lines the menu shows for it.</summary>
public static class DeathCauses
{
    public const string Spirit = "spirit";
    public const string GraveRobber = "graverobber";
    public const string Bride = "bride";
    public const string Father = "father";
    public const string Night = "night";

    /// <summary>The death endings each name their killer.</summary>
    public static string FromEnding(Ending ending)
    {
        switch (ending)
        {
            case Ending.WrathfulSpirit: return Spirit;
            case Ending.Ambush: return GraveRobber;
            case Ending.HappilyEverAfter: return Bride;
            case Ending.Father: return Father;
            default: return Night;
        }
    }

    /// <summary>The watch (phase 1..3) the night is currently in.</summary>
    public static int CurrentWatch()
    {
        int phase = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentPhase : 1;
        return Mathf.Clamp(phase, 1, 3);
    }

    /// <summary>"Taken by the Grave Robber"</summary>
    public static string Label(string cause)
    {
        string english;
        switch (cause)
        {
            case Spirit: english = "Taken by the Wrathful Spirit"; break;
            case GraveRobber: english = "Taken by the Grave Robber"; break;
            case Bride: english = "Taken by the Bride"; break;
            case Father: english = "Taken by the Father"; break;
            default: cause = Night; english = "Taken by the night"; break;
        }
        return Loc.Get("death.cause." + cause, english);
    }

    /// <summary>"RETURN TO THE SECOND WATCH"</summary>
    public static string ReturnLabel(int watch)
    {
        return watch >= 3
            ? Loc.Get("ui.menu.return.3", "RETURN TO THE THIRD WATCH")
            : Loc.Get("ui.menu.return.2", "RETURN TO THE SECOND WATCH");
    }
}
