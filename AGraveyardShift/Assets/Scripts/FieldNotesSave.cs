using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists which creature field notes the player has read in the cabin to a JSON file in
/// Application.persistentDataPath. A read note unlocks its full text in the main-menu archive.
/// Pure static API — no scene object needed. Mirrors CryptFiendsSave.
/// </summary>
public static class FieldNotesSave
{
    [Serializable]
    private class SaveData
    {
        public List<string> found = new List<string>();
    }

    private const string FileName = "fieldnotes.json";
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
            Debug.LogWarning("FieldNotesSave: load failed (" + e.Message + "); starting fresh.");
            data = new SaveData();
        }
        if (data.found == null) data.found = new List<string>();
    }

    private static void WriteToDisk()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("FieldNotesSave: save failed (" + e.Message + ").");
        }
    }

    public static bool IsUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureLoaded();
        return data.found.Contains(id);
    }

    public static void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        EnsureLoaded();
        if (data.found.Contains(id)) return;

        data.found.Add(id);
        WriteToDisk();
        Debug.Log("FieldNotesSave: unlocked " + id + " (" + FilePath + ")");
    }

    /// <summary>How many of the archive's notes have been found.</summary>
    public static int UnlockedCount(FieldNoteArchive archive)
    {
        if (archive == null || archive.entries == null) return 0;
        EnsureLoaded();
        int count = 0;
        foreach (var entry in archive.entries)
        {
            if (entry != null && data.found.Contains(entry.id)) count++;
        }
        return count;
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
