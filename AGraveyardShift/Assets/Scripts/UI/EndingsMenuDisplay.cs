using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills the main-menu Endings panel: five stone tablets, I..V (EndingInfo.InOrder). A found
/// ending is carved with its kind ("Bad Ending"), its name and the date it was first reached;
/// the others stay blank, worn stone with only the numeral. Hovering a found tablet shows its
/// line under the row. Refreshes every time the panel is enabled.
/// </summary>
public class EndingsMenuDisplay : MonoBehaviour
{
    [Header("Tablets, in order I..V")]
    public Image[] tablets = new Image[5];
    public TextMeshProUGUI[] numerals = new TextMeshProUGUI[5];
    public TextMeshProUGUI[] kinds = new TextMeshProUGUI[5];
    public TextMeshProUGUI[] titles = new TextMeshProUGUI[5];
    public TextMeshProUGUI[] dates = new TextMeshProUGUI[5];
    public Sprite foundSprite;
    public Sprite wornSprite;

    [Header("Carving")]
    [Tooltip("The font the carved material belongs to; other fonts (translations) keep their own material.")]
    public TMP_FontAsset carvedFont;
    public Material carvedMaterial;
    public Color carvedColor = new Color(0.14f, 0.13f, 0.12f, 1f);
    public Color wornNumeralColor = new Color(0.10f, 0.10f, 0.09f, 0.55f);

    [Header("Text")]
    [Tooltip("The hovered ending's line, under the tablets.")]
    public TextMeshProUGUI description;
    [Tooltip("'X / 5 discovered'.")]
    public TextMeshProUGUI subtitle;
    [Tooltip("Crypt Fiends button label: '0 / 3 ?????' until the first fiend is found, then the count.")]
    public TextMeshProUGUI cryptFiendsTeaser;
    [Tooltip("Field Notes button label ('X / 6 FIELD NOTES').")]
    public TextMeshProUGUI fieldNotesTeaser;

    private void OnEnable()
    {
        Loc.LocaleChanged += Refresh;
        Refresh();
        ShowDescription(-1);
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= Refresh;
    }

    public void Refresh()
    {
        EndingsSave.Reload();

        int count = 0;
        for (int i = 0; i < EndingInfo.InOrder.Length; i++)
        {
            Ending e = EndingInfo.InOrder[i];
            bool found = EndingsSave.IsUnlocked(e);
            if (found) count++;

            EndingInfo.Data info = EndingInfo.Get(e);
            string kind, name;
            Split(info.title, out kind, out name);

            if (i < tablets.Length && tablets[i] != null)
                tablets[i].sprite = found ? foundSprite : wornSprite;

            Carve(At(numerals, i), info.roman, found ? carvedColor : wornNumeralColor);
            Carve(At(kinds, i), found ? kind : "", carvedColor);
            Carve(At(titles, i), found ? name : "", carvedColor);

            string date = "";
            DateTime when;
            if (found && EndingsSave.TryGetUnlockDate(e, out when))
                date = Loc.Format("ui.endings.found", "FOUND {0}", FormatDate(when));
            Carve(At(dates, i), date, carvedColor);
        }

        if (subtitle != null)
        {
            Loc.EnsureFontComponent(subtitle);
            subtitle.text = Loc.Format("ui.endings.discovered", "{0} / {1}  DISCOVERED", count, EndingInfo.InOrder.Length);
        }

        if (cryptFiendsTeaser != null)
        {
            Loc.EnsureFontComponent(cryptFiendsTeaser);
            CryptFiendsSave.Reload();
            int fiends = CryptFiendsSave.UnlockedCount();
            cryptFiendsTeaser.text = fiends == 0
                ? Loc.Format("ui.endings.fiendsTeaserLocked", "{0} / {1}  ?????", 0, CryptFiendInfo.InOrder.Length)
                : Loc.Format("ui.endings.fiendsTeaser", "{0} / {1}  CRYPT FIENDS FOUND", fiends, CryptFiendInfo.InOrder.Length);
        }

        if (fieldNotesTeaser != null)
        {
            Loc.EnsureFontComponent(fieldNotesTeaser);
            FieldNotesSave.Reload();
            FieldNoteArchive archive = FieldNoteArchive.Load();
            int total = archive != null && archive.entries != null ? archive.entries.Length : 0;
            fieldNotesTeaser.text = Loc.Format("ui.endings.fieldNotesTeaser", "{0} / {1}  FIELD NOTES", FieldNotesSave.UnlockedCount(archive), total);
        }
    }

    /// <summary>Shows the line for tablet <paramref name="index"/> (found endings only); -1 clears it.</summary>
    public void ShowDescription(int index)
    {
        if (description == null) return;
        Loc.EnsureFontComponent(description);
        if (index < 0 || index >= EndingInfo.InOrder.Length || !EndingsSave.IsUnlocked(EndingInfo.InOrder[index]))
        {
            description.text = "";
            return;
        }
        description.text = EndingInfo.Get(EndingInfo.InOrder[index]).description;
    }

    private static TextMeshProUGUI At(TextMeshProUGUI[] arr, int i)
    {
        return arr != null && i < arr.Length ? arr[i] : null;
    }

    private void Carve(TextMeshProUGUI t, string text, Color color)
    {
        if (t == null) return;
        Loc.EnsureFontComponent(t);
        t.text = text;
        t.color = color;
        if (carvedMaterial != null && carvedFont != null && t.font == carvedFont && t.fontSharedMaterial != carvedMaterial)
            t.fontSharedMaterial = carvedMaterial;
    }

    // "Bad Ending - An Ambush" -> "Bad Ending" / "An Ambush". Every translation keeps the " - ".
    private static void Split(string title, out string kind, out string name)
    {
        int dash = string.IsNullOrEmpty(title) ? -1 : title.IndexOf(" - ", StringComparison.Ordinal);
        if (dash < 0) { kind = ""; name = title ?? ""; return; }
        kind = title.Substring(0, dash).Trim();
        name = title.Substring(dash + 3).Trim();
    }

    private static string FormatDate(DateTime d)
    {
        try
        {
            return d.ToString("d", new CultureInfo(Loc.Locale));
        }
        catch (Exception)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
