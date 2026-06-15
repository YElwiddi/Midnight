using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists which crypt fiends the player has encountered-and-survived to a JSON file
/// in Application.persistentDataPath. Pure static API — no scene object needed.
/// Mirrors EndingsSave. A fiend is unlocked when the player completes the game (the
/// good ending) after that fiend stalked them in the crypt.
/// </summary>
public static class CryptFiendsSave
{
    [Serializable]
    private class SaveData
    {
        public bool friend;
        public bool wraith;
        public bool beast;
    }

    private const string FileName = "cryptfiends.json";
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
            Debug.LogWarning("CryptFiendsSave: load failed (" + e.Message + "); starting fresh.");
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
            Debug.LogWarning("CryptFiendsSave: save failed (" + e.Message + ").");
        }
    }

    public static bool IsUnlocked(CryptFiend f)
    {
        EnsureLoaded();
        switch (f)
        {
            case CryptFiend.Friend: return data.friend;
            case CryptFiend.Wraith: return data.wraith;
            case CryptFiend.Beast:  return data.beast;
            default: return false;
        }
    }

    public static void Unlock(CryptFiend f)
    {
        EnsureLoaded();
        bool changed = false;
        switch (f)
        {
            case CryptFiend.Friend: changed = !data.friend; data.friend = true; break;
            case CryptFiend.Wraith: changed = !data.wraith; data.wraith = true; break;
            case CryptFiend.Beast:  changed = !data.beast;  data.beast  = true; break;
        }
        if (changed)
        {
            WriteToDisk();
            Debug.Log("CryptFiendsSave: unlocked " + f + " (" + FilePath + ")");
        }
    }

    public static int UnlockedCount()
    {
        EnsureLoaded();
        int c = 0;
        if (data.friend) c++;
        if (data.wraith) c++;
        if (data.beast) c++;
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
