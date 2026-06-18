using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the cabin key + back-gate (BackGate1 / BackGate2) interactions and the
/// protection-driven scares. Uses the same Yes/No confirm UI as
/// DialogueTeleportInteractable (DialogueUI choices), so it matches the
/// "church-door" style dialogue the rest of the game uses.
///
/// Behaviour:
///  - Key: Yes/No confirm to pick up. Yes -> GameManager flag "keypickedup" = true
///    and the key vanishes. No -> nothing.
///  - Gate without the key: shows "It's locked." (auto-hiding line).
///  - Gate with the key, first time: Yes/No confirm to open. Yes -> both leaves
///    swing ajar, GraveyardProtection drops once by <see cref="openProtectionPenalty"/>,
///    and "backgateopened" = true. No -> nothing.
///  - Gate after it has been opened once: interacting just toggles open/closed,
///    free of charge and with no confirm ("loops as is").
///
/// Protection thresholds (watched live):
///  - &lt;= <see cref="keyVanishThreshold"/> (50): if the key was never taken it
///    vanishes from the table. If the player already has it, nothing changes.
///  - &lt;= <see cref="gateCreakThreshold"/> (25): one leaf creaks half-open on its
///    own; the other stays shut. Skipped if the player already opened the gate.
/// </summary>
public class BackGateController : MonoBehaviour
{
    [Header("Gate Leaves")]
    [SerializeField] private SwingingGate gate1;
    [SerializeField] private SwingingGate gate2;
    [Tooltip("Which leaf creaks half-open on its own at the low-protection threshold. Defaults to gate1.")]
    [SerializeField] private SwingingGate creakingGate;

    [Header("Key")]
    [SerializeField] private BackGateKeyInteractable key;

    [Header("Dialogue Text")]
    [TextArea(2, 5)]
    [SerializeField] private string keyPrompt = "This is the key to the back gate. I think I should leave it here. Should I pick it up anyway?";
    [TextArea(2, 5)]
    [SerializeField] private string gateLockedText = "It's locked.";
    [TextArea(2, 5)]
    [SerializeField] private string gateOpenPrompt = "I should definitely keep this locked. Should I open it anyway?";
    [SerializeField] private string yesText = "Yes";
    [SerializeField] private string noText = "No";
    [Tooltip("Speaker name for the confirm lines. Leave empty for an internal-thought (no name).")]
    [SerializeField] private string speakerName = "";
    [Tooltip("How long the auto-hiding \"It's locked.\" line stays up after typing.")]
    [SerializeField] private float lockedDuration = 2.5f;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Tuning")]
    [Tooltip("GraveyardProtection lost (once) the first time the gate is opened.")]
    [SerializeField] private int openProtectionPenalty = 40;
    [Tooltip("At or under this protection, an un-taken key vanishes from the table.")]
    [SerializeField] private int keyVanishThreshold = 50;
    [Tooltip("At or under this protection, one leaf creaks half-open on its own.")]
    [SerializeField] private int gateCreakThreshold = 25;

    // Runtime
    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private GraveyardProtectionManager protection;
    private bool isShowingChoice;
    private bool gateCreaked;       // sticky: a creak has happened -> gate is player-toggleable
    private bool creakArmed = true; // re-arms when protection climbs back above the creak threshold
    private Action<int> activeChoiceHandler;

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();
        if (creakingGate == null) creakingGate = gate1;

        protection = GraveyardProtectionManager.Instance;
        if (protection != null)
        {
            protection.OnProtectionChanged += HandleProtectionChanged;
            // Evaluate the current value immediately in case we start below a threshold.
            HandleProtectionChanged(protection.CurrentProtection, protection.MaxProtection);
        }
    }

    private void OnDestroy()
    {
        if (protection != null) protection.OnProtectionChanged -= HandleProtectionChanged;
        if (activeChoiceHandler != null && dialogueUI != null)
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
    }

    #region Key
    public void OnKeyInteracted()
    {
        if (isShowingChoice || IsBusy()) return;
        if (key == null || !key.IsVisible) return;   // already gone
        if (IsFlag("keypickedup")) return;

        StartCoroutine(ShowConfirm(keyPrompt, yes =>
        {
            if (!yes) return;
            SetFlag("keypickedup", true);
            key.Hide();
        }));
    }
    #endregion

    #region Gate
    public void OnGateInteracted()
    {
        if (isShowingChoice || IsBusy()) return;

        // Already opened deliberately: toggle BOTH leaves freely, no cost/confirm. "Loops as is."
        if (IsFlag("backgateopened"))
        {
            if (AreGatesOpen()) CloseBoth();
            else OpenBoth();
            return;
        }

        // The gate creaked open on its own from low protection: let the player close/open that one leaf freely.
        if (gateCreaked)
        {
            if (creakingGate != null) creakingGate.SetOpen(!creakingGate.IsOpen);
            return;
        }

        // Locked unless the player took the key.
        if (!IsFlag("keypickedup"))
        {
            float speed = useTypewriter ? typewriterSpeed : 0f;
            SimpleDialogueTrigger.ShowDialogue(gateLockedText, speakerName, lockedDuration, speed);
            return;
        }

        // First deliberate open: confirm, then swing both leaves outward and pay the one-time penalty.
        StartCoroutine(ShowConfirm(gateOpenPrompt, yes =>
        {
            if (!yes) return;
            // Mark opened BEFORE draining so the low-protection creak logic won't fight us.
            SetFlag("backgateopened", true);
            OpenBoth();
            if (protection != null && openProtectionPenalty > 0)
                protection.DrainProtection(openProtectionPenalty);
        }));
    }
    #endregion

    #region Protection thresholds
    private void HandleProtectionChanged(int current, int max)
    {
        // The un-taken key is lost once protection gets low enough.
        if (current <= keyVanishThreshold && key != null && key.IsVisible && !IsFlag("keypickedup"))
        {
            key.Hide();
        }

        // Re-arm the creak once protection climbs back above the threshold, so a later dip
        // creaks the gate open again (also lets the Current Protection lever re-test it).
        if (current > gateCreakThreshold)
        {
            creakArmed = true;
            return;
        }

        // Protection is low: creak one leaf open (wide enough to pass), once per dip. This now
        // happens even if the player opened the gate before and closed it again — a closed gate
        // won't stay shut down here. Firing once per dip still lets the player re-close it after.
        if (creakArmed)
        {
            creakArmed = false;
            gateCreaked = true; // sticky: gate stays player-toggleable afterward
            if (creakingGate != null) creakingGate.SetOpen(true);
        }
    }
    #endregion

    #region Gate helpers
    private void OpenBoth()
    {
        if (gate1 != null) gate1.SetOpen(true);
        if (gate2 != null) gate2.SetOpen(true);
    }

    private void CloseBoth()
    {
        if (gate1 != null) gate1.SetOpen(false);
        if (gate2 != null) gate2.SetOpen(false);
    }

    private bool AreGatesOpen()
    {
        return (gate1 != null && gate1.IsOpen) || (gate2 != null && gate2.IsOpen);
    }
    #endregion

    #region Helpers
    private bool IsFlag(string flag) => GameManager.Instance != null && GameManager.Instance.GetBoolFlag(flag);
    private void SetFlag(string flag, bool value) { if (GameManager.Instance != null) GameManager.Instance.SetBoolFlag(flag, value); }

    private bool IsBusy()
    {
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return true;
        DialogueManager dm = DialogueManager.GetInstance();
        return dm != null && dm.IsDialoguePlaying();
    }

    /// <summary>
    /// Shows a single line then Yes/No choices, mirroring DialogueTeleportInteractable.
    /// Invokes <paramref name="onResult"/> with true if Yes was chosen, false otherwise.
    /// </summary>
    private IEnumerator ShowConfirm(string message, Action<bool> onResult)
    {
        if (dialogueUI == null)
        {
            onResult?.Invoke(false);
            yield break;
        }

        isShowingChoice = true;

        // Take over input / free the cursor so the player can click a choice.
        if (playerMovement != null) playerMovement.DisableAllInput();
        if (crosshairManager != null) crosshairManager.Hide();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialogueUI.Show();
        if (useTypewriter) dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);
        dialogueUI.SetTypewriterSoundMuted(true);   // these internal-thought confirms are silent (no typewriter beep)
        dialogueUI.SetDialogueText(message, string.IsNullOrEmpty(speakerName) ? null : speakerName);

        while (dialogueUI.IsTypewriting) yield return null;
        yield return null;

        dialogueUI.DisplayChoices(new List<string> { yesText, noText });

        bool choiceMade = false;
        int selected = -1;
        activeChoiceHandler = index =>
        {
            selected = index;
            choiceMade = true;
            if (dialogueUI != null && activeChoiceHandler != null)
                dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        };
        dialogueUI.OnChoiceSelected += activeChoiceHandler;

        while (!choiceMade && dialogueUI != null && dialogueUI.IsVisible)
            yield return null;

        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }
        if (dialogueUI != null)
        {
            dialogueUI.ClearTypewriterSpeedOverride();
            dialogueUI.SetTypewriterSoundMuted(false);
        }

        // Only tidy up / restore control if the dialogue wasn't taken over by something else.
        bool stillOurs = dialogueUI != null && dialogueUI.IsVisible;
        if (stillOurs)
        {
            dialogueUI.ClearChoices();
            dialogueUI.Hide();
            if (playerMovement != null) playerMovement.EnableAllInput();
            if (crosshairManager != null) crosshairManager.Show();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        isShowingChoice = false;
        onResult?.Invoke(choiceMade && selected == 0);
    }
    #endregion
}
