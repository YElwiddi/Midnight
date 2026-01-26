using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class LockedTeleportInteractable : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [Tooltip("The boolean flag name in GameManager to check (e.g., 'cryptunlocked')")]
    [SerializeField] private string requiredBoolFlag = "cryptunlocked";

    [Header("Locked Dialogue")]
    [TextArea(3, 10)]
    [SerializeField] private string lockedDialogueText = "The door is locked...";
    [SerializeField] private string lockedSpeakerName = "";
    [SerializeField] private float lockedDialogueDuration = 3f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriterEffect = true;
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("Teleport Settings")]
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool setPlayerRotation = false;
    [SerializeField] private Vector3 targetRotation;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Ambient Sound")]
    [Tooltip("Set to true if the destination is indoors (cabin, house, etc.)")]
    [SerializeField] private bool destinationIsIndoor = false;
    [Tooltip("Optional: Change the ambient sound clip when entering this area")]
    [SerializeField] private AudioClip destinationAmbientClip;
    [Tooltip("Optional: Override the ambient volume for this destination (-1 = use default)")]
    [Range(-1f, 1f)]
    [SerializeField] private float destinationAmbientVolume = -1f;

    [Header("Events")]
    [Tooltip("Called when the teleport completes successfully")]
    public UnityEvent OnTeleportComplete;

    private static Image fadeOverlay;
    private static Canvas fadeCanvas;
    private static bool isTransitioning = false;
    private DialogueManager dialogueManager;

    private void Start()
    {
        dialogueManager = FindFirstObjectByType<DialogueManager>();
    }

    public void Interact()
    {
        if (isTransitioning) return;

        // Don't interact if any dialogue is playing
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return;
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying()) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || teleportDestination == null) return;

        bool isUnlocked = GameManager.Instance != null && GameManager.Instance.GetBoolFlag(requiredBoolFlag);

        if (isUnlocked)
        {
            StartCoroutine(TeleportSequence(player));
        }
        else
        {
            float speed = useTypewriterEffect ? typewriterSpeed : 0f;
            SimpleDialogueTrigger.ShowDialogue(lockedDialogueText, lockedSpeakerName, lockedDialogueDuration, speed);
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

        // Update ambient sound for indoor/outdoor transition
        if (destinationIsIndoor)
        {
            AmbientSoundManager.Instance?.EnterIndoor();
        }
        else
        {
            AmbientSoundManager.Instance?.ExitIndoor();
        }

        // Change ambient clip if specified
        if (destinationAmbientClip != null)
        {
            Debug.Log($"LockedTeleportInteractable: Attempting to change ambient clip to {destinationAmbientClip.name}, volume: {destinationAmbientVolume}");
            if (AmbientSoundManager.Instance != null)
            {
                AmbientSoundManager.Instance.SetAmbientClipWithMemory(destinationAmbientClip, destinationAmbientVolume);
            }
            else
            {
                Debug.LogWarning("LockedTeleportInteractable: AmbientSoundManager.Instance is null!");
            }
        }
        else
        {
            Debug.Log("LockedTeleportInteractable: No destination ambient clip assigned");
        }

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(Fade(1f, 0f, halfDuration));

        if (movement != null)
        {
            movement.EnableAllInput();
        }

        isTransitioning = false;

        // Fire teleport complete event
        OnTeleportComplete?.Invoke();
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

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
}
