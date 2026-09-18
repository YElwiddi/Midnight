using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills the main-menu Crypt Fiends panel from saved unlocks. Locked fiends show "???"
/// and are not clickable; unlocked fiends show their name and open a detail view with
/// the fiend's lore when clicked. Slot order is Friend, Wraith, Beast (CryptFiendInfo.InOrder).
/// Refreshes every time the panel is enabled. Mirrors EndingsMenuDisplay.
/// </summary>
public class CryptFiendsMenuDisplay : MonoBehaviour
{
    [Header("Slots (order: Friend, Wraith, Beast)")]
    [Tooltip("The three slot name labels, in order.")]
    public TextMeshProUGUI[] slotNames = new TextMeshProUGUI[3];

    [Tooltip("The three slot buttons, in order. Disabled (non-interactable) when locked.")]
    public Button[] slotButtons = new Button[3];

    [Tooltip("'X / 3 FOUND' subtitle.")]
    public TextMeshProUGUI subtitle;

    [Tooltip("Modal title label. Shows '?????' until the first fiend is found, then 'CRYPT FIENDS'.")]
    public TextMeshProUGUI header;

    [Header("Detail View")]
    [Tooltip("Container shown when an unlocked fiend is clicked. Hidden by default.")]
    public GameObject detailPanel;
    [Tooltip("Big fiend name shown in the detail view.")]
    public TextMeshProUGUI detailName;
    [Tooltip("Body text (flavor + ability + spawn condition).")]
    public TextMeshProUGUI detailBody;
    [Tooltip("Button that returns from the detail view to the list.")]
    public Button detailBackButton;

    [Header("Style")]
    [Tooltip("Text shown for fiends the player has not encountered yet.")]
    public string lockedText = "???";
    [Tooltip("Tint for unlocked (revealed) fiend names.")]
    public Color unlockedColor = new Color(0.88f, 0.86f, 0.82f, 1f);
    [Tooltip("Tint for locked fiend names.")]
    public Color lockedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);

    private bool wiredBack;

    private void Awake()
    {
        WireBackButton();
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

    private void WireBackButton()
    {
        if (wiredBack || detailBackButton == null) return;
        detailBackButton.onClick.AddListener(HideDetail);
        wiredBack = true;
    }

    public void Refresh()
    {
        WireBackButton();
        CryptFiendsSave.Reload();
        HideDetail();

        int count = 0;
        for (int i = 0; i < CryptFiendInfo.InOrder.Length && i < 3; i++)
        {
            CryptFiend fiend = CryptFiendInfo.InOrder[i];
            bool unlocked = CryptFiendsSave.IsUnlocked(fiend);
            if (unlocked) count++;

            CryptFiendInfo.Data info = CryptFiendInfo.Get(fiend);

            if (slotNames != null && i < slotNames.Length && slotNames[i] != null)
            {
                Loc.EnsureFontComponent(slotNames[i]);
                slotNames[i].text = unlocked ? info.title : Loc.Tr(lockedText);
                slotNames[i].color = unlocked ? unlockedColor : lockedColor;
            }

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
            {
                Button btn = slotButtons[i];
                btn.onClick.RemoveAllListeners();
                btn.interactable = unlocked;
                if (unlocked)
                {
                    CryptFiend captured = fiend; // avoid closure over the loop variable
                    btn.onClick.AddListener(() => ShowDetail(captured));
                }
            }
        }

        if (header != null)
        {
            Loc.EnsureFontComponent(header);
            header.text = count == 0 ? Loc.Get("ui.fiends.headerLocked", "?????") : Loc.Get("ui.fiends.header", "CRYPT FIENDS");
        }

        if (subtitle != null)
        {
            Loc.EnsureFontComponent(subtitle);
            subtitle.text = Loc.Format("ui.fiends.found", "{0} / {1}  FOUND", count, CryptFiendInfo.InOrder.Length);
        }

        Loc.EnsureFontComponent(detailName);
        Loc.EnsureFontComponent(detailBody);
    }

    public void ShowDetail(CryptFiend fiend)
    {
        CryptFiendInfo.Data info = CryptFiendInfo.Get(fiend);
        if (detailName != null) detailName.text = info.title;
        if (detailBody != null) detailBody.text = CryptFiendInfo.Body(fiend);
        if (detailPanel != null) detailPanel.SetActive(true);
    }

    public void HideDetail()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
    }
}
