using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Teleport interactable that shows a Yes/No confirmation dialogue before teleporting.
/// Can optionally be locked behind a GameManager bool flag.
/// Combines dialogue choice logic from DirtPileInteractable with teleport logic from LockedTeleportInteractable.
/// </summary>
public class DialogueTeleportInteractable : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [Tooltip("If true, requires a GameManager bool flag to be true before allowing interaction")]
    [SerializeField] private bool requiresUnlock = true;
    [Tooltip("The boolean flag name in GameManager to check (e.g., 'cryptunlocked')")]
    [SerializeField] private string requiredBoolFlag = "cryptunlocked";

    [Header("Locked Dialogue")]
    [TextArea(3, 10)]
    [SerializeField] private string lockedDialogueText = "The door is locked...";
    [SerializeField] private string lockedSpeakerName = "";
    [SerializeField] private float lockedDialogueDuration = 3f;

    [Header("Confirmation Dialogue")]
    [TextArea(3, 10)]
    [SerializeField] private string confirmationMessage = "Are you sure you want to enter?";
    [SerializeField] private string yesOptionText = "Yes";
    [SerializeField] private string noOptionText = "No";

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriterEffect = true;
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 30f;
    [Tooltip("Mute the typewriter sound for this dialogue")]
    [SerializeField] private bool muteTypewriterSound = false;

    [Header("Teleport Settings")]
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool setPlayerRotation = false;
    [SerializeField] private Vector3 targetRotation;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Ambient Sound")]
    [Tooltip("Set to true if the destination is indoors")]
    [SerializeField] private bool destinationIsIndoor = false;
    [Tooltip("Optional: Change the ambient sound clip when entering this area")]
    [SerializeField] private AudioClip destinationAmbientClip;
    [Tooltip("Optional: Override the ambient volume for this destination (-1 = use default)")]
    [Range(-1f, 1f)]
    [SerializeField] private float destinationAmbientVolume = -1f;

    [Header("Sanity")]
    [Tooltip("If true, destination is a safe zone where sanity cannot drain")]
    [SerializeField] private bool destinationIsSafeZone = false;

    [Header("Lighting")]
    [Tooltip("Lighting preset to apply at destination (None = don't change)")]
    [SerializeField] private LightingPreset destinationLightingPreset = LightingPreset.None;

    [Header("Events")]
    [Tooltip("Called when the teleport completes successfully")]
    public UnityEvent OnTeleportComplete;

    [Header("Post-Teleport Game Event")]
    [Tooltip("Optional: A GameEvent to trigger after teleportation completes")]
    [SerializeField] private GameEvent eventToTriggerAfterTeleport;

    // Shared fade overlay (same pattern as LockedTeleportInteractable)
    private static Image fadeOverlay;
    private static Canvas fadeCanvas;
    private static bool isTransitioning = false;

    // Dialogue choice state
    private bool isShowingChoice = false;
    private DialogueUI dialogueUI;
    private DialogueManager dialogueManager;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private Coroutine activeChoiceCoroutine;
    private System.Action<int> activeChoiceHandler;

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        dialogueManager = FindFirstObjectByType<DialogueManager>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }

    public void Interact()
    {
        if (isTransitioning || isShowingChoice) return;

        // Don't interact if any dialogue is playing
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return;
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying()) return;

        // Check lock condition
        if (requiresUnlock)
        {
            bool isUnlocked = GameManager.Instance != null && GameManager.Instance.GetBoolFlag(requiredBoolFlag);
            if (!isUnlocked)
            {
                float speed = useTypewriterEffect ? typewriterSpeed : 0f;
                SimpleDialogueTrigger.ShowDialogue(lockedDialogueText, lockedSpeakerName, lockedDialogueDuration, speed);
                return;
            }
        }

        // Show Yes/No confirmation
        activeChoiceCoroutine = StartCoroutine(ShowConfirmation());
    }

    private void OnDisable()
    {
        if (activeChoiceCoroutine != null)
        {
            StopCoroutine(activeChoiceCoroutine);
            activeChoiceCoroutine = null;
        }

        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }

        isShowingChoice = false;
    }

    private IEnumerator ShowConfirmation()
    {
        if (dialogueUI == null)
        {
            Debug.LogWarning("DialogueTeleportInteractable: No DialogueUI found!");
            yield break;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || teleportDestination == null) yield break;

        isShowingChoice = true;

        // Disable player input
        if (playerMovement != null)
        {
            playerMovement.DisableAllInput();
        }
        if (crosshairManager != null)
        {
            crosshairManager.Hide();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show dialogue with choices
        dialogueUI.Show();
        if (useTypewriterEffect)
        {
            dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);
        }
        if (muteTypewriterSound)
        {
            dialogueUI.SetTypewriterSoundOverride(null, 0f);
        }
        dialogueUI.SetDialogueText(confirmationMessage, null);

        // Wait for typewriter to finish before showing choices
        while (dialogueUI.IsTypewriting)
        {
            yield return null;
        }

        yield return null;

        List<string> choices = new List<string> { yesOptionText, noOptionText };
        dialogueUI.DisplayChoices(choices);

        // Wait for choice
        bool choiceMade = false;
        int selectedChoice = -1;

        activeChoiceHandler = (index) =>
        {
            selectedChoice = index;
            choiceMade = true;
            if (dialogueUI != null && activeChoiceHandler != null)
            {
                dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            }
            activeChoiceHandler = null;
        };

        dialogueUI.OnChoiceSelected += activeChoiceHandler;

        while (!choiceMade && dialogueUI != null && dialogueUI.IsVisible)
        {
            yield return null;
        }

        // Clean up handler if still subscribed
        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }

        // Clear typewriter overrides
        if (dialogueUI != null)
        {
            dialogueUI.ClearTypewriterSpeedOverride();
            dialogueUI.ClearTypewriterSoundOverride();
        }

        // Only proceed with cleanup if dialogue wasn't closed externally
        if (dialogueUI != null && dialogueUI.IsVisible)
        {
            dialogueUI.ClearChoices();
            dialogueUI.Hide();

            if (playerMovement != null)
            {
                playerMovement.EnableAllInput();
            }
            if (crosshairManager != null)
            {
                crosshairManager.Show();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        isShowingChoice = false;
        activeChoiceCoroutine = null;

        // If player chose Yes, teleport
        if (choiceMade && selectedChoice == 0)
        {
            StartCoroutine(TeleportSequence(player));
        }
    }

    private IEnumerator TeleportSequence(GameObject player)
    {
        isTransitioning = true;

        Movement movement = player.GetComponent<Movement>();
        CharacterController cc = player.GetComponent<CharacterController>();

        if (movement != null)
        {
            movement.DisableAllInput();
        }

        EnsureFadeOverlay();

        float halfDuration = transitionDuration / 2f;
        yield return StartCoroutine(Fade(0f, 1f, halfDuration));

        if (cc != null) cc.enabled = false;
        player.transform.position = teleportDestination.position;
        if (setPlayerRotation)
        {
            player.transform.rotation = Quaternion.Euler(targetRotation);
        }
        if (cc != null) cc.enabled = true;

        // Update ambient sound
        if (destinationIsIndoor)
        {
            AmbientSoundManager.Instance?.EnterIndoor();
        }
        else
        {
            AmbientSoundManager.Instance?.ExitIndoor();
        }

        // Update sanity safe zone
        if (destinationIsSafeZone)
        {
            SanityManager.Instance?.EnterSafeZone();
        }
        else
        {
            SanityManager.Instance?.ExitSafeZone();
        }

        // Change ambient clip if specified
        if (destinationAmbientClip != null)
        {
            if (AmbientSoundManager.Instance != null)
            {
                AmbientSoundManager.Instance.SetAmbientClipWithMemory(destinationAmbientClip, destinationAmbientVolume);
            }
        }

        // Apply lighting preset if specified
        if (destinationLightingPreset != LightingPreset.None && LightingController.Instance != null)
        {
            LightingController.Instance.ApplyPreset(destinationLightingPreset);
        }

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(Fade(1f, 0f, halfDuration));

        if (eventToTriggerAfterTeleport == null)
        {
            if (movement != null)
            {
                movement.EnableAllInput();
            }
        }

        isTransitioning = false;

        OnTeleportComplete?.Invoke();

        // Trigger post-teleport GameEvent if configured
        if (eventToTriggerAfterTeleport != null)
        {
            GameFlowManager flowManager = FindFirstObjectByType<GameFlowManager>();
            if (flowManager != null)
            {
                flowManager.TriggerEvent(eventToTriggerAfterTeleport);
            }
            else
            {
                Debug.LogWarning("DialogueTeleportInteractable: No GameFlowManager found to trigger post-teleport event!");
                if (movement != null)
                {
                    movement.EnableAllInput();
                }
            }
        }
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvas == null)
        {
            GameObject canvasObj = new GameObject("TeleportFadeCanvas");
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();

            GameObject imageObj = new GameObject("FadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform, false);
            fadeOverlay = imageObj.AddComponent<Image>();
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

            RectTransform rt = fadeOverlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            DontDestroyOnLoad(canvasObj);
        }
        else
        {
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        }
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color color = fadeOverlay.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            fadeOverlay.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeOverlay.color = color;
    }
}
