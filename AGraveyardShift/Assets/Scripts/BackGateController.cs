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
///    free of charge and with no confirm ("loops as is") -- unless it has since
///    jammed (see the creak threshold below), in which case it reports "stuck".
///
/// Protection thresholds (watched live):
///  - &lt;= <see cref="keyVanishThreshold"/> (50): if the key was never taken it
///    vanishes from the table. If the player already has it, nothing changes.
///  - &lt;= <see cref="gateCreakThreshold"/> (25): the gate jams part-open (one leaf
///    creaks ajar, the other shuts) and from then on every interaction reports
///    "The gate is stuck." -- this wins even over a gate the player opened earlier.
///    The jam is DEFERRED until the player is at least <see cref="creakMinPlayerDistance"/>
///    away AND on the inside (graveyard) side, so it never jams point-blank or traps
///    the player outside the gate.
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
    [Tooltip("Locked prompt shown once the key has gone missing (protection at/under the vanish threshold).")]
    [SerializeField] private string gateKeyLostText = "Its locked. I can't find my key...";
    [TextArea(2, 5)]
    [Tooltip("Shown when the player interacts with the gate after it has creaked part-way open and jammed.")]
    [SerializeField] private string gateStuckText = "The gate is stuck.";
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
    [Tooltip("At or under this protection, one leaf creaks part-way open on its own and jams.")]
    [SerializeField] private int gateCreakThreshold = 25;
    [Tooltip("How far (degrees) the leaf creaks open at low protection. Kept small so the gap is too narrow to fit through (full open is the leaf's own openAngle, ~70).")]
    [SerializeField] private float creakOpenAngle = 12f;

    [Header("Creak Gating")]
    [Tooltip("The gate only jams once the player is at least this far (horizontal units) from the gate, so it never jams in the player's face -- e.g. right as they open it.")]
    [SerializeField] private float creakMinPlayerDistance = 15f;
    [Tooltip("World-space direction from the gate toward the INSIDE (graveyard) side. The jam only triggers while the player is on this side, NEVER while they are outside the gate. In this scene the graveyard is +Z.")]
    [SerializeField] private Vector3 insideDirectionWorld = new Vector3(0f, 0f, 1f);

    // Runtime
    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private GraveyardProtectionManager protection;
    private bool isShowingChoice;
    private bool gateCreaked;       // sticky: a creak has happened -> the gate is jammed part-open
    private bool creakArmed = true; // re-arms when protection climbs back above the creak threshold
    private bool creakPending;      // protection low & armed -> waiting for a safe spot to jam (player far + inside)
    private bool keyLost;           // sticky: the key vanished at low protection -> locked prompt changes
    private Transform playerTransform;
    private Action<int> activeChoiceHandler;

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        if (playerMovement != null) playerTransform = playerMovement.transform;
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

    private void Update()
    {
        TryPendingCreak();
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

        // The gate jammed part-open at low protection: it won't budge. Checked FIRST so it wins
        // even over a gate the player opened earlier -- every interaction reports "stuck" until
        // protection recovers above the creak threshold.
        if (gateCreaked)
        {
            float speed = useTypewriter ? typewriterSpeed : 0f;
            SimpleDialogueTrigger.ShowDialogue(gateStuckText, speakerName, lockedDuration, speed);
            return;
        }

        // Already opened deliberately: toggle BOTH leaves freely, no cost/confirm. "Loops as is."
        if (IsFlag("backgateopened"))
        {
            if (AreGatesOpen()) CloseBoth();
            else OpenBoth();
            return;
        }

        // Locked unless the player took the key. Once the key has gone missing the line changes.
        if (!IsFlag("keypickedup"))
        {
            float speed = useTypewriter ? typewriterSpeed : 0f;
            SimpleDialogueTrigger.ShowDialogue(keyLost ? gateKeyLostText : gateLockedText, speakerName, lockedDuration, speed);
            return;
        }

        // First deliberate open: confirm, then swing both leaves outward and pay the one-time penalty.
        StartCoroutine(ShowConfirm(gateOpenPrompt, yes =>
        {
            if (!yes) return;
            // Record the open before the side-effects below. If the drain pushes protection past
            // the creak threshold the gate jams itself later (once the player has stepped far
            // enough back inside) -- intended; the stuck check above takes priority over "opened".
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
        // The key is lost for good once protection drops low enough -- even if the player is
        // already holding it. The gate's locked prompt changes after this (see keyLost).
        if (current <= keyVanishThreshold && !keyLost)
        {
            keyLost = true;
            if (key != null && key.IsVisible) key.Hide();        // the table key vanishes
            if (IsFlag("keypickedup")) SetFlag("keypickedup", false); // ...and a held key is lost too
        }

        // Re-arm the creak (and un-stick the gate) once protection climbs back above the
        // threshold, so a later dip can jam it again.
        if (current > gateCreakThreshold)
        {
            creakArmed = true;
            creakPending = false;
            if (gateCreaked)
            {
                gateCreaked = false;
                if (creakingGate != null) creakingGate.SetOpen(false); // close the previously-jammed leaf
            }
            return;
        }

        // Protection is low: ARM the jam, but don't snap it shut here. We wait until the player is
        // safely far from the gate and on the inside (see TryPendingCreak) so it never jams in
        // their face or locks them outside.
        if (creakArmed)
        {
            creakArmed = false;
            creakPending = true;
        }
    }

    /// <summary>
    /// While a jam is pending (protection low, armed), fire it the moment the player is BOTH far
    /// enough from the gate AND on the inside (graveyard) side -- so the gate never jams
    /// point-blank or while the player is outside it.
    /// </summary>
    private void TryPendingCreak()
    {
        if (!creakPending || gateCreaked) return;
        if (!PlayerSafeForCreak()) return;
        DoCreak();
    }

    /// <summary>
    /// True when the player is at least <see cref="creakMinPlayerDistance"/> from the gate
    /// (horizontally) AND clearly on the inside side. Fails safe (false) when the player can't be
    /// found, guaranteeing the gate never jams while the player is outside it.
    /// </summary>
    private bool PlayerSafeForCreak()
    {
        if (playerTransform == null) return false;

        Vector3 gateCenter = GateCenter();
        Vector3 toPlayer = playerTransform.position - gateCenter;
        toPlayer.y = 0f;

        // Far enough away?
        if (toPlayer.magnitude < creakMinPlayerDistance) return false;

        // On the inside side? Dot > 0 means the player is past the gate plane toward the graveyard.
        // NEVER jam while they're on the outside (dot <= 0).
        Vector3 inside = insideDirectionWorld;
        inside.y = 0f;
        if (inside.sqrMagnitude < 0.0001f) return false; // misconfigured -> fail safe
        return Vector3.Dot(toPlayer, inside.normalized) > 0f;
    }

    /// <summary>
    /// Snaps the gate into its jammed part-open pose: the creaking leaf swings ajar and the other
    /// leaf shuts, so the whole thing reads as stuck regardless of whether it was open before.
    /// </summary>
    private void DoCreak()
    {
        creakPending = false;
        gateCreaked = true; // sticky: the gate is jammed part-open
        SwingingGate other = creakingGate == gate1 ? gate2 : gate1;
        if (other != null) other.SetOpen(false);
        if (creakingGate != null) creakingGate.SwingToAngle(creakOpenAngle);
    }

    /// <summary>
    /// World-space midpoint between the two leaves (the gate opening). Falls back to a single leaf,
    /// then this transform, if a reference is missing.
    /// </summary>
    private Vector3 GateCenter()
    {
        if (gate1 != null && gate2 != null)
            return (gate1.transform.position + gate2.transform.position) * 0.5f;
        if (gate1 != null) return gate1.transform.position;
        if (gate2 != null) return gate2.transform.position;
        return transform.position;
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
