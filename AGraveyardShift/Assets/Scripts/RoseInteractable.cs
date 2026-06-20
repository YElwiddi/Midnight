using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rose inside the cabin chest. Once the chest is unlocked it moves to layer 0 and
/// stops blocking the interaction ray, so the rose (on the Interactable layer) becomes
/// targetable. Interacting shows a Yes/No confirm; choosing Yes sets the GameManager flag
/// "rosepickedup" and removes the rose. Same DialogueUI confirm flow as
/// SecretKeyInteractable / ChestInteractable.
/// </summary>
public class RoseInteractable : MonoBehaviour, IInteractable
{
    [Header("Pickup")]
    [Tooltip("Root object to deactivate when the rose is taken. Defaults to this object.")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private string interactionPrompt = "Take";
    [Tooltip("GameManager bool flag set to true when the rose is taken.")]
    [SerializeField] private string flagName = "rosepickedup";

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

    /// <summary>True while the rose is still present in the world.</summary>
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
