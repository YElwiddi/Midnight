using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists which endings the player has unlocked to a JSON file in
/// Application.persistentDataPath. Pure static API — no scene object needed.
/// </summary>
public static class EndingsSave
{
    [Serializable]
    private class SaveData
    {
        public bool wrathfulSpirit;
        public bool ambush;
        public bool wrongfulConviction;
        public bool happilyEverAfter;
    }

    private const string FileName = "endings.json";
    private static SaveData data;

    private static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, FileName); }
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;
        try
        {
            if (File.Exists(FilePath))
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            else
                data = new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning("EndingsSave: load failed (" + e.Message + "); starting fresh.");
            data = new SaveData();
        }
    }

    private static void WriteToDisk()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("EndingsSave: save failed (" + e.Message + ").");
        }
    }

    public static bool IsUnlocked(Ending e)
    {
        EnsureLoaded();
        switch (e)
        {
            case Ending.WrathfulSpirit: return data.wrathfulSpirit;
            case Ending.Ambush: return data.ambush;
            case Ending.WrongfulConviction: return data.wrongfulConviction;
            case Ending.HappilyEverAfter: return data.happilyEverAfter;
            default: return false;
        }
    }

    public static void Unlock(Ending e)
    {
        EnsureLoaded();
        bool changed = false;
        switch (e)
        {
            case Ending.WrathfulSpirit: changed = !data.wrathfulSpirit; data.wrathfulSpirit = true; break;
            case Ending.Ambush: changed = !data.ambush; data.ambush = true; break;
            case Ending.WrongfulConviction: changed = !data.wrongfulConviction; data.wrongfulConviction = true; break;
            case Ending.HappilyEverAfter: changed = !data.happilyEverAfter; data.happilyEverAfter = true; break;
        }
        if (changed)
        {
            WriteToDisk();
            Debug.Log("EndingsSave: unlocked " + e + " (" + FilePath + ")");
        }
    }

    public static int UnlockedCount()
    {
        EnsureLoaded();
        int c = 0;
        if (data.wrathfulSpirit) c++;
        if (data.ambush) c++;
        if (data.wrongfulConviction) c++;
        if (data.happilyEverAfter) c++;
        return c;
    }

    /// <summary>Clears all unlocks (testing / "new game" reset).</summary>
    public static void ResetAll()
    {
        data = new SaveData();
        WriteToDisk();
    }

    /// <summary>Drops the in-memory cache so the next read re-loads from disk.</summary>
    public static void Reload()
    {
        data = null;
        EnsureLoaded();
    }
}
