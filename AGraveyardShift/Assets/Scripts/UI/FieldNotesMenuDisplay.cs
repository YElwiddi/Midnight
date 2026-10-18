using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills the main-menu Field Notes panel: the full creature field notes the player has read in
/// the cabin (FieldNotesSave). Unread notes show "???" and are not clickable; read notes open a
/// paper detail view with the complete text, one page at a time.
/// Slot order follows FieldNoteArchive.entries. Refreshes every time the panel is enabled.
/// Mirrors CryptFiendsMenuDisplay.
/// </summary>
public class FieldNotesMenuDisplay : MonoBehaviour
{
    [Header("Slots (archive order)")]
    public TextMeshProUGUI[] slotNames;
    public Button[] slotButtons;

    [Tooltip("'X / 6 RECOVERED' subtitle.")]
    public TextMeshProUGUI subtitle;

    [Tooltip("Modal title label.")]
    public TextMeshProUGUI header;

    [Header("Detail View")]
    [Tooltip("Container shown when a read note is clicked. Hidden by default.")]
    public GameObject detailPanel;
    public TextMeshProUGUI detailBody;
    public TextMeshProUGUI detailPage;
    public Button detailPrevButton;
    public Button detailNextButton;
    public Button detailBackButton;

    [Header("Style")]
    [Tooltip("Text shown for notes the player has not found yet.")]
    public string lockedText = "???";
    public Color unlockedColor = new Color(0.88f, 0.86f, 0.82f, 1f);
    public Color lockedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);

    private string[] currentPages;
    private int currentPage;
    private bool wired;

    private void Awake()
    {
        WireDetailButtons();
    }

    private void OnEnable()
    {
        Loc.LocaleChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= Refresh;
    }

    private void WireDetailButtons()
    {
        if (wired) return;
        if (detailBackButton != null) detailBackButton.onClick.AddListener(HideDetail);
        if (detailPrevButton != null) detailPrevButton.onClick.AddListener(PreviousPage);
        if (detailNextButton != null) detailNextButton.onClick.AddListener(NextPage);
        wired = true;
    }

    public void Refresh()
    {
        WireDetailButtons();
        FieldNotesSave.Reload();
        HideDetail();

        FieldNoteArchive archive = FieldNoteArchive.Load();
        int total = archive != null && archive.entries != null ? archive.entries.Length : 0;
        int count = 0;

        for (int i = 0; slotNames != null && i < slotNames.Length; i++)
        {
            FieldNoteArchive.Entry entry = i < total ? archive.entries[i] : null;
            bool unlocked = entry != null && FieldNotesSave.IsUnlocked(entry.id);
            if (unlocked) count++;

            if (slotNames[i] != null)
            {
                Loc.EnsureFontComponent(slotNames[i]);
                slotNames[i].text = unlocked ? FieldNoteArchive.Title(entry) : Loc.Tr(lockedText);
                slotNames[i].color = unlocked ? unlockedColor : lockedColor;
            }

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
            {
                Button btn = slotButtons[i];
                btn.onClick.RemoveAllListeners();
                btn.interactable = unlocked;
                if (unlocked)
                {
                    FieldNoteArchive.Entry captured = entry; // avoid closure over the loop variable
                    btn.onClick.AddListener(() => ShowDetail(captured));
                }
            }
        }

        if (header != null)
        {
            Loc.EnsureFontComponent(header);
            header.text = Loc.Get("ui.fieldnotes.header", "FIELD NOTES");
        }

        if (subtitle != null)
        {
            Loc.EnsureFontComponent(subtitle);
            subtitle.text = Loc.Format("ui.fieldnotes.found", "{0} / {1}  RECOVERED", count, total);
        }

        Loc.EnsureFontComponent(detailBody);
        Loc.EnsureFontComponent(detailPage);
    }

    public void ShowDetail(FieldNoteArchive.Entry entry)
    {
        if (entry == null || entry.pages == null || entry.pages.Length == 0) return;

        currentPages = Loc.TrAll(entry.pages);
        currentPage = 0;
        if (detailPanel != null) detailPanel.SetActive(true);
        ShowPage();
    }

    public void HideDetail()
    {
        currentPages = null;
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    private void PreviousPage()
    {
        if (currentPages == null || currentPage <= 0) return;
        currentPage--;
        ShowPage();
    }

    private void NextPage()
    {
        if (currentPages == null || currentPage >= currentPages.Length - 1) return;
        currentPage++;
        ShowPage();
    }

    private void ShowPage()
    {
        if (currentPages == null) return;

        if (detailBody != null) detailBody.text = currentPages[currentPage];

        bool paged = currentPages.Length > 1;
        if (detailPage != null)
        {
            detailPage.gameObject.SetActive(paged);
            detailPage.text = Loc.Format("ui.readable.page", "Page {0}/{1}", currentPage + 1, currentPages.Length);
        }
        if (detailPrevButton != null) detailPrevButton.gameObject.SetActive(paged && currentPage > 0);
        if (detailNextButton != null) detailNextButton.gameObject.SetActive(paged && currentPage < currentPages.Length - 1);
    }
}
