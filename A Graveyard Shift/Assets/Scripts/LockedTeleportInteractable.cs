using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using Unity.AI.Navigation;

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

    [Header("Sanity")]
    [Tooltip("If true, destination is a safe zone where sanity cannot drain")]
    [SerializeField] private bool destinationIsSafeZone = false;

    [Header("Lighting")]
    [Tooltip("Lighting preset to apply at destination (None = don't change)")]
    [SerializeField] private LightingPreset destinationLightingPreset = LightingPreset.None;

    [Header("Events")]
    [Tooltip("Called when the teleport completes successfully")]
    public UnityEvent OnTeleportComplete;

    [Header("NavMesh")]
    [Tooltip("HACK: Two NavMesh surfaces (GraveyardMap and FBX) are colliding. Disabling the FBX NavMesh agent fixes crypt killer movement but breaks jumpscare angles. Keeping both on breaks killer pathing. This disables the GraveyardMap surface on teleport as a workaround.")]
    [SerializeField] private bool disableGraveyardNavMesh = false;

    [Header("Post-Teleport Game Event")]
    [Tooltip("Optional: A GameEvent to trigger after teleportation completes (e.g., escort NPC for ending sequence)")]
    [SerializeField] private GameEvent eventToTriggerAfterTeleport;

    private static Image fadeOverlay;
    private static Canvas fadeCanvas;
    private static bool isTransitioning = false;
    private DialogueManager dialogueManager;

    private void Start()
    {
        dialogueManager = FindFirstObjectByType<DialogueManager>();

        // Validate the required flag exists in GameManager at startup
        if (GameManager.Instance != null && !string.IsNullOrEmpty(requiredBoolFlag)
            && !GameManager.Instance.HasBoolFlag(requiredBoolFlag))
        {
            Debug.LogError($"LockedTeleportInteractable ({gameObject.name}): requiredBoolFlag '{requiredBoolFlag}' does not exist in GameManager! Door will remain locked and show dialogue as fallback.");
        }
    }

    public void Interact()
    {
        if (isTransitioning) return;

        // Don't interact if any dialogue is playing
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return;
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying()) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || teleportDestination == null) return;

        bool isUnlocked = false;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"LockedTeleportInteractable ({gameObject.name}): GameManager.Instance is null — treating door as locked.");
        }
        else if (!GameManager.Instance.HasBoolFlag(requiredBoolFlag))
        {
            Debug.LogWarning($"LockedTeleportInteractable ({gameObject.name}): Flag '{requiredBoolFlag}' not found in GameManager — treating door as locked.");
        }
        else
        {
            isUnlocked = GameManager.Instance.GetBoolFlag(requiredBoolFlag);
        }

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

        // Apply lighting preset if specified
        if (destinationLightingPreset != LightingPreset.None && LightingController.Instance != null)
        {
            Debug.Log($"LockedTeleportInteractable: Applying lighting preset '{destinationLightingPreset}'");
            LightingController.Instance.ApplyPreset(destinationLightingPreset);
        }

        // Disable graveyard NavMeshSurface if configured (prevents graveyard NPCs from pathing into crypt)
        if (disableGraveyardNavMesh)
        {
            GameObject graveyardMap = GameObject.Find("GraveyardMap");
            if (graveyardMap != null)
            {
                NavMeshSurface surface = graveyardMap.GetComponent<NavMeshSurface>();
                if (surface != null)
                {
                    surface.enabled = false;
                    Debug.Log("LockedTeleportInteractable: Disabled GraveyardMap NavMeshSurface");
                }
            }
        }

        // Refresh zone tracking after teleport (OnTriggerEnter/Exit don't fire on teleport)
        PlayerZoneTracker.RefreshZonesAfterTeleport();

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(Fade(1f, 0f, halfDuration));

        // If we're triggering a GameEvent, don't re-enable player input
        // The GameEvent's disablePlayerControlOnStart will handle controls
        if (eventToTriggerAfterTeleport == null)
        {
            if (movement != null)
            {
                movement.EnableAllInput();
            }
        }

        isTransitioning = false;

        // Fire teleport complete event
        OnTeleportComplete?.Invoke();

        // Trigger the post-teleport GameEvent if configured
        if (eventToTriggerAfterTeleport != null)
        {
            GameFlowManager flowManager = FindFirstObjectByType<GameFlowManager>();
            if (flowManager != null)
            {
                Debug.Log($"LockedTeleportInteractable: Triggering post-teleport event '{eventToTriggerAfterTeleport.eventName}'");
                flowManager.TriggerEvent(eventToTriggerAfterTeleport);
            }
            else
            {
                Debug.LogWarning("LockedTeleportInteractable: No GameFlowManager found to trigger post-teleport event!");
                // Re-enable input since we couldn't trigger the event
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

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
}
