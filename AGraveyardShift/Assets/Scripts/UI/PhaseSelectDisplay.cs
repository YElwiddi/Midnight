using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Locks the phase select slots for watches the player hasn't reached yet (PhaseSelect.IsAvailable):
/// the slot can't be clicked and its line says how to unlock it.
/// </summary>
public class PhaseSelectDisplay : MonoBehaviour
{
    [Tooltip("Slot buttons, first to third watch.")]
    public Button[] slotButtons = new Button[3];
    [Tooltip("Slot descriptions, first to third watch.")]
    public TMP_Text[] slotDescs = new TMP_Text[3];

    private static readonly string[] DescKeys = { "ui.phase.first.desc", "ui.phase.second.desc", "ui.phase.third.desc" };
    private static readonly string[] DescEnglish = { "The whole night.", "Skip the first three visitors.", "Skip ahead to the last visitors of the night." };

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
        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool open = PhaseSelect.IsAvailable(i + 1);
            if (slotButtons[i] != null) slotButtons[i].interactable = open;
            if (i < slotDescs.Length && slotDescs[i] != null)
            {
                Loc.EnsureFontComponent(slotDescs[i]);
                slotDescs[i].text = open ? Loc.Get(DescKeys[i], DescEnglish[i]) : Loc.Get("ui.phase.locked", "Reach this watch to unlock it.");
            }
        }
    }
}
