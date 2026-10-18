using UnityEngine;

/// <summary>
/// The full creature field notes, readable from the main menu (Endings > Field Notes) once the
/// player has found the short version in the cabin. The in-game notes only carry a one-line
/// survival rule; the complete original text lives here.
/// Loaded from Resources/FieldNoteArchive. Pages are English source text translated through
/// Loc.Tr, so they share translations with the original in-game pages.
/// </summary>
[CreateAssetMenu(fileName = "FieldNoteArchive", menuName = "Game/Field Note Archive")]
public class FieldNoteArchive : ScriptableObject
{
    public const string ResourcesPath = "FieldNoteArchive";

    [System.Serializable]
    public class Entry
    {
        [Tooltip("Stable id, matched by ReadableTrigger.fieldNoteId and saved in fieldnotes.json")]
        public string id;

        [Tooltip("Display name in the archive (English; localized under 'fieldnote.<id>.title')")]
        public string title;

        [Tooltip("The full note, one element per page")]
        [TextArea(5, 15)]
        public string[] pages;
    }

    [Tooltip("Notes in menu order")]
    public Entry[] entries;

    private static FieldNoteArchive cached;

    public static FieldNoteArchive Load()
    {
        if (cached == null) cached = Resources.Load<FieldNoteArchive>(ResourcesPath);
        return cached;
    }

    /// <summary>Localized display name for an entry.</summary>
    public static string Title(Entry entry)
    {
        if (entry == null) return string.Empty;
        return Loc.Get("fieldnote." + entry.id + ".title", entry.title);
    }
}
