using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lockable chest (in the cabin) holding the rose. It stays locked until the player has
/// taken the secret-room key (GameManager flag "secretkey"). Interacting:
///  - without the key -> "It's locked." (auto-hiding line)
///  - with the key    -> Yes/No confirm "...Should I unlock it?"; choosing Yes opens the
///    lid, sets GameManager "chestunlocked" = true (which the upcoming rose feature reads),
///    and reveals the rose inside.
/// Uses the same DialogueUI confirm flow as SecretKeyInteractable / BackGateController.
/// </summary>
public class ChestInteractable : MonoBehaviour, IInteractable
{
    [Header("Lock")]
    [Tooltip("GameManager bool flag that must be true to unlock (the church / secret-room key).")]
    [SerializeField] private string requiredKeyFlag = "secretkey";
    [Tooltip("GameManager bool flag set true once the chest is unlocked (drives the rose feature).")]
    [SerializeField] private string unlockedFlag = "chestunlocked";
    [SerializeField] private string interactionPrompt = "Examine";

    [Header("Dialogue")]
    [TextArea] [SerializeField] private string lockedText = "It's locked.";
    [TextArea] [SerializeField] private string unlockPrompt = "I think this key from the church opens this chest. Should I unlock it?";
    [SerializeField] private string yesText = "Yes";
    [SerializeField] private string noText = "No";
    [Tooltip("Speaker name. Leave empty for an internal-thought line (no name).")]
    [SerializeField] private string speakerName = "";
    [Tooltip("How long the auto-hiding \"It's locked.\" line stays up.")]
    [SerializeField] private float lockedDuration = 2.5f;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Opening")]
    [Tooltip("The chest top (lid). Simply deactivated on unlock to reveal the contents.")]
    [SerializeField] private Transform lid;
    [Tooltip("Layer the chest moves to once unlocked, so its collider stops blocking interaction with the contents (0 = Default).")]
    [SerializeField] private int unlockedLayer = 0;
    [Tooltip("The rose inside (kept for the next feature). Not required.")]
    [SerializeField] private GameObject rose;
    [SerializeField] private AudioClip openSound;
    [Range(0f, 1f)] [SerializeField] private float openVolume = 0.8f;

    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private bool isShowingChoice;
    private Action<int> activeChoiceHandler;
    private bool isOpen;

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();

        // If the chest was already unlocked this session, start open.
        if (GameManager.Instance != null && GameManager.Instance.GetBoolFlag(unlockedFlag))
        {
            isOpen = true;
            ApplyOpenedState();
        }
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
        if (isOpen) return; // already unlocked

        bool hasKey = GameManager.Instance != null && GameManager.Instance.GetBoolFlag(requiredKeyFlag);
        if (!hasKey)
        {
            float speed = useTypewriter ? typewriterSpeed : 0f;
            SimpleDialogueTrigger.ShowDialogue(lockedText, speakerName, lockedDuration, speed);
            return;
        }

        StartCoroutine(ShowConfirm(unlockPrompt, yes =>
        {
            if (yes) Unlock();
        }));
    }

    public string GetInteractionPrompt() => interactionPrompt;

    private void Unlock()
    {
        if (isOpen) return;
        isOpen = true;
        if (GameManager.Instance != null) GameManager.Instance.SetBoolFlag(unlockedFlag, true);
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position, openVolume);
        ApplyOpenedState();
    }

    // Removes the chest top and takes the chest off the interaction layer, so the player
    // can interact with the contents (the rose) instead of the chest blocking the ray.
    private void ApplyOpenedState()
    {
        if (lid != null) lid.gameObject.SetActive(false);
        gameObject.layer = unlockedLayer;
    }

    private bool IsBusy()
    {
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return true;
        DialogueManager dm = DialogueManager.GetInstance();
        return dm != null && dm.IsDialoguePlaying();
    }

    /// <summary>
    /// Shows a single line then Yes/No choices (mirrors SecretKeyInteractable / BackGateController).
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
