using TMPro;
using UnityEngine;

/// <summary>
/// Fills the main-menu Endings panel from saved unlocks. Locked endings show "??????".
/// Slot order is I..V, matching EndingInfo.InOrder.
/// Refreshes every time the panel is enabled.
/// </summary>
public class EndingsMenuDisplay : MonoBehaviour
{
    [Tooltip("The slot value labels, in order I, II, III, IV, V, VI.")]
    public TextMeshProUGUI[] slotValues = new TextMeshProUGUI[6];

    [Tooltip("Optional per-slot type labels (Bad/Good/Secret). Same order as slotValues.")]
    public TextMeshProUGUI[] slotTypes = new TextMeshProUGUI[6];

    [Tooltip("Optional 'X / 5 discovered' subtitle.")]
    public TextMeshProUGUI subtitle;

    [Tooltip("Optional Crypt Fiends teaser button label inside the modal. Shows '0 / 3 ?????' until the first fiend is found, then the count.")]
    public TextMeshProUGUI cryptFiendsTeaser;

    [Tooltip("Text shown for endings the player has not unlocked yet.")]
    public string lockedText = "??????";

    [Tooltip("Tint for unlocked good endings.")]
    public Color goodColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [Tooltip("Tint for unlocked bad endings.")]
    public Color badColor = new Color(0.85f, 0.78f, 0.78f, 1f);
    [Tooltip("Tint for locked endings.")]
    public Color lockedColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);

    private void OnEnable()
    {
        Loc.LocaleChanged += Refresh;
        Refresh();
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
            bool unlocked = EndingsSave.IsUnlocked(e);
            if (unlocked) count++;

            EndingInfo.Data info = EndingInfo.Get(e);

            if (slotValues != null && i < slotValues.Length && slotValues[i] != null)
            {
                Loc.EnsureFontComponent(slotValues[i]);
                slotValues[i].text = unlocked ? info.title : Loc.Tr(lockedText);
                slotValues[i].color = unlocked ? (info.isGood ? goodColor : badColor) : lockedColor;
            }

            if (slotTypes != null && i < slotTypes.Length && slotTypes[i] != null)
            {
                Loc.EnsureFontComponent(slotTypes[i]);
                slotTypes[i].text = unlocked ? info.typeLabel : "";
            }
        }

        if (subtitle != null)
        {
            Loc.EnsureFontComponent(subtitle);
            subtitle.text = Loc.Format("ui.endings.discovered", "{0} / {1}  DISCOVERED", count, EndingInfo.InOrder.Length);
        }

        // Crypt Fiends teaser button label (the gallery opens from inside this modal).
        if (cryptFiendsTeaser != null)
        {
            Loc.EnsureFontComponent(cryptFiendsTeaser);
            CryptFiendsSave.Reload();
            int fiends = CryptFiendsSave.UnlockedCount();
            cryptFiendsTeaser.text = fiends == 0
                ? Loc.Format("ui.endings.fiendsTeaserLocked", "{0} / {1}  ?????", 0, CryptFiendInfo.InOrder.Length)
                : Loc.Format("ui.endings.fiendsTeaser", "{0} / {1}  CRYPT FIENDS FOUND", fiends, CryptFiendInfo.InOrder.Length);
        }
    }
}
