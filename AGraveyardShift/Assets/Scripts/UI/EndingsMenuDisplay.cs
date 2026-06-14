using TMPro;
using UnityEngine;

/// <summary>
/// Fills the main-menu Endings panel from saved unlocks. Locked endings show "??????".
/// Slot order is I..IV, matching EndingInfo.InOrder.
/// Refreshes every time the panel is enabled.
/// </summary>
public class EndingsMenuDisplay : MonoBehaviour
{
    [Tooltip("The four slot value labels, in order I, II, III, IV.")]
    public TextMeshProUGUI[] slotValues = new TextMeshProUGUI[4];

    [Tooltip("Optional per-slot type labels (Bad/Good). Same order as slotValues.")]
    public TextMeshProUGUI[] slotTypes = new TextMeshProUGUI[4];

    [Tooltip("Optional 'X / 4 discovered' subtitle.")]
    public TextMeshProUGUI subtitle;

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
        Refresh();
    }

    public void Refresh()
    {
        EndingsSave.Reload();

        int count = 0;
        for (int i = 0; i < EndingInfo.InOrder.Length && i < 4; i++)
        {
            Ending e = EndingInfo.InOrder[i];
            bool unlocked = EndingsSave.IsUnlocked(e);
            if (unlocked) count++;

            EndingInfo.Data info = EndingInfo.Get(e);

            if (slotValues != null && i < slotValues.Length && slotValues[i] != null)
            {
                slotValues[i].text = unlocked ? info.title : lockedText;
                slotValues[i].color = unlocked ? (info.isGood ? goodColor : badColor) : lockedColor;
            }

            if (slotTypes != null && i < slotTypes.Length && slotTypes[i] != null)
            {
                slotTypes[i].text = unlocked ? info.typeLabel : "";
            }
        }

        if (subtitle != null)
            subtitle.text = count + " / 4  DISCOVERED";
    }
}
