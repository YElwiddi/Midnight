using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rose inside the cabin chest. It cannot be targeted or taken until the chest is
/// unlocked: while the GameManager flag "chestunlocked" is false the rose's own colliders
/// are disabled (so the interaction ray can't reach it through the gap under the chest) and
/// Interact() refuses outright. Once the chest is unlocked the rose becomes targetable;
/// interacting shows a Yes/No confirm and choosing Yes sets "rosepickedup" and removes the
/// rose. Same DialogueUI confirm flow as SecretKeyInteractable / ChestInteractable.
/// </summary>
public class RoseInteractable : MonoBehaviour, IInteractable
{
    [Header("Pickup")]
    [Tooltip("Root object to deactivate when the rose is taken. Defaults to this object.")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private string interactionPrompt = "Take";
    [Tooltip("GameManager bool flag set to true when the rose is taken.")]
    [SerializeField] private string flagName = "rosepickedup";

    [Header("Lock (chest)")]
    [Tooltip("GameManager bool flag that must be true (chest unlocked) before the rose can be targeted or taken. Leave empty to disable gating.")]
    [SerializeField] private string chestUnlockedFlag = "chestunlocked";

    [Header("Confirm Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string prompt = "A single rose, perfectly preserved. Should I take it?";
    [SerializeField] private string yesText = "Yes";
    [SerializeField] private string noText = "No";
    [Tooltip("Speaker name. Leave empty for an internal-thought line (no name).")]
    [SerializeField] private string speakerName = "";

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 40f;

    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private bool isShowingChoice;
    private Action<int> activeChoiceHandler;

    private Collider[] roseColliders;
    private bool unlocked;

    /// <summary>True while the rose is still present in the world.</summary>
    public bool IsVisible => visualRoot != null && visualRoot.activeSelf;

    /// <summary>True once the chest is unlocked (or gating is disabled), so the rose may be taken.</summary>
    private bool ChestUnlocked =>
        string.IsNullOrEmpty(chestUnlockedFlag) ||
        (GameManager.Instance != null && GameManager.Instance.GetBoolFlag(chestUnlockedFlag));

    private void Awake()
    {
        if (visualRoot == null) visualRoot = gameObject;
        // Gather the rose's own colliders so we can keep the interaction ray from reaching
        // it while the chest is still locked (defends against aiming through the gap under
        // the chest). Includes inactive children in case the rose starts hidden.
        roseColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();

        // Start non-targetable unless the chest is already unlocked this session.
        SetTargetable(ChestUnlocked);
    }

    private void Update()
    {
        // Once the chest is unlocked, make the rose targetable and stop polling.
        if (!unlocked && ChestUnlocked) SetTargetable(true);
    }

    // Enables/disables the rose's colliders so the interaction ray only hits it once the
    // chest is unlocked. Interact() still hard-gates on ChestUnlocked as a second guard.
    private void SetTargetable(bool targetable)
    {
        unlocked = targetable;
        if (roseColliders == null) return;
        foreach (var c in roseColliders)
            if (c != null) c.enabled = targetable;
    }

    private void OnDestroy()
    {
        if (activeChoiceHandler != null && dialogueUI != null)
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
    }

    // IInteractable
    public void Interact()
    {
        if (isShowingChoice || IsBusy()) return;
        if (!IsVisible) return;
        if (!ChestUnlocked) return; // chest still locked -- rose cannot be taken
        if (GameManager.Instance != null && GameManager.Instance.GetBoolFlag(flagName)) return; // already taken

        StartCoroutine(ShowConfirm(prompt, yes =>
        {
            if (!yes) return;
            if (GameManager.Instance != null) GameManager.Instance.SetBoolFlag(flagName, true);
            Hide();
        }));
    }

    public string GetInteractionPrompt() => interactionPrompt;

    /// <summary>Removes the rose from the world by deactivating its visual root.</summary>
    public void Hide()
    {
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    private bool IsBusy()
    {
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return true;
        DialogueManager dm = DialogueManager.GetInstance();
        return dm != null && dm.IsDialoguePlaying();
    }

    /// <summary>
    /// Shows a single line then Yes/No choices (mirrors SecretKeyInteractable / ChestInteractable).
    /// Invokes <paramref name="onResult"/> with true if Yes was chosen.
    /// </summary>
    private IEnumerator ShowConfirm(string message, Action<bool> onResult)
    {
        if (dialogueUI == null)
        {
            onResult?.Invoke(false);
            yield break;
        }

        isShowingChoice = true;

        if (playerMovement != null) playerMovement.DisableAllInput();
        if (crosshairManager != null) crosshairManager.Hide();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialogueUI.Show();
        if (useTypewriter) dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);
        dialogueUI.SetTypewriterSoundMuted(true);
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
}
