using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A key resting on a table in the church's Secret Room. Interacting shows a Yes/No
/// confirm ("You found a key. Do you want to pick it up?"), mirroring the church-door /
/// back-gate dialogue style (DialogueUI choices, no Ink). Choosing Yes sets the
/// GameManager bool flag <see cref="flagName"/> ("secretkey") and removes the key.
/// </summary>
public class SecretKeyInteractable : MonoBehaviour, IInteractable
{
    [Header("Pickup")]
    [Tooltip("Root object to deactivate when the key is taken. Defaults to this object.")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private string interactionPrompt = "Pick up";
    [Tooltip("GameManager bool flag set to true when the key is taken.")]
    [SerializeField] private string flagName = "secretkey";

    [Header("Confirm Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string keyPrompt = "You found a key. Do you want to pick it up?";
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

    /// <summary>True while the key is still present in the world.</summary>
    public bool IsVisible => visualRoot != null && visualRoot.activeSelf;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = gameObject;
    }

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();
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
        if (GameManager.Instance != null && GameManager.Instance.GetBoolFlag(flagName)) return; // already taken

        StartCoroutine(ShowConfirm(keyPrompt, yes =>
        {
            if (!yes) return;
            if (GameManager.Instance != null) GameManager.Instance.SetBoolFlag(flagName, true);
            Hide();
        }));
    }

    public string GetInteractionPrompt() => interactionPrompt;

    /// <summary>Removes the key from the world by deactivating its visual root.</summary>
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
    /// Shows a single line then Yes/No choices, mirroring DialogueTeleportInteractable /
    /// BackGateController. Invokes <paramref name="onResult"/> with true if Yes was chosen.
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
        dialogueUI.SetTypewriterSoundMuted(true);   // internal-thought confirm is silent (no typewriter beep)
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
}
