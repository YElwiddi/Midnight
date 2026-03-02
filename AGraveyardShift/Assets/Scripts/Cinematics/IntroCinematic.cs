using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Plays an intro cinematic when the scene loads.
/// Camera starts looking at the ceiling, then the player "wakes up" and gets out of bed.
/// </summary>
public class IntroCinematic : MonoBehaviour
{
    [Header("Cinematic Settings")]
    [Tooltip("Delay before the cinematic starts (after scene load)")]
    [SerializeField] private float startDelay = 0.5f;

    [Tooltip("How long the camera stays looking at the ceiling")]
    [SerializeField] private float ceilingLookDuration = 2f;

    [Tooltip("How long it takes for the camera to look down (wake up)")]
    [SerializeField] private float lookDownDuration = 1.5f;

    [Header("Camera Settings")]
    [Tooltip("Starting camera angle (negative = looking up, -89 = straight up at ceiling)")]
    [SerializeField] private float startingLookAngle = -70f;

    [Tooltip("Ending camera angle after waking up (0 = straight ahead)")]
    [SerializeField] private float endingLookAngle = 0f;

    [Header("Movement Settings")]
    [Tooltip("Should the player walk off the bed after waking?")]
    [SerializeField] private bool walkOffBed = true;

    [Tooltip("Transform marking where to walk to (leave empty to walk forward)")]
    [SerializeField] private Transform walkTarget;

    [Tooltip("Distance to walk forward if no target set")]
    [SerializeField] private float walkDistance = 2f;

    [Tooltip("Walking speed")]
    [SerializeField] private float walkSpeed = 2f;

    [Tooltip("Delay after looking down before starting to walk")]
    [SerializeField] private float delayBeforeWalk = 0.5f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip wakeUpSound;
    [SerializeField] private AudioSource audioSource;

    [Header("References (Auto-found if empty)")]
    [SerializeField] private Movement playerMovement;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CharacterController characterController;

    [Header("Dialogue After Cinematic")]
    [Tooltip("Show dialogue after the cinematic completes")]
    [SerializeField] private bool showDialogueOnComplete = false;

    [TextArea(3, 10)]
    [SerializeField] private string dialogueText = "";

    [SerializeField] private string speakerName = "";

    [Tooltip("How long the dialogue stays on screen after typing completes")]
    [SerializeField] private float dialogueDuration = 3f;

    [Tooltip("Characters per second (0 = instant)")]
    [SerializeField] private float dialogueTypewriterSpeed = 30f;

    [Header("Events")]
    [Tooltip("Fired when the cinematic completes (after dialogue if enabled)")]
    [SerializeField] private UnityEvent onCinematicComplete;

    private bool cinematicComplete = false;

    private void Awake()
    {
        // Auto-find references immediately
        FindReferences();

        // Disable input immediately to prevent any player control before cinematic
        if (playerMovement != null)
        {
            playerMovement.DisableAllInput();
        }

        // Set camera to ceiling position immediately
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(startingLookAngle, 0, 0);
        }
    }

    private void Start()
    {
        // Start the cinematic
        StartCoroutine(PlayIntroCinematic());
    }

    private void FindReferences()
    {
        if (playerMovement == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerMovement = player.GetComponent<Movement>();
                characterController = player.GetComponent<CharacterController>();
            }
        }

        if (playerCamera == null && playerMovement != null)
        {
            playerCamera = playerMovement.playerCamera;
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (characterController == null && playerMovement != null)
        {
            characterController = playerMovement.GetComponent<CharacterController>();
        }
    }

    private IEnumerator PlayIntroCinematic()
    {
        // Hide crosshair during cinematic
        CrosshairManager crosshair = FindFirstObjectByType<CrosshairManager>();
        if (crosshair != null)
        {
            crosshair.Hide();
        }

        Debug.Log("IntroCinematic: Looking at ceiling");

        // Initial delay (camera already set to ceiling in Awake)
        yield return new WaitForSeconds(startDelay);

        // Wait while looking at ceiling
        yield return new WaitForSeconds(ceilingLookDuration);

        // Play wake up sound if configured
        if (wakeUpSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(wakeUpSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(wakeUpSound, playerCamera.transform.position);
            }
        }

        // Animate camera looking down (waking up)
        Debug.Log("IntroCinematic: Waking up (looking down)");
        yield return StartCoroutine(AnimateCameraLookDown());

        // Delay before walking
        if (walkOffBed && delayBeforeWalk > 0)
        {
            yield return new WaitForSeconds(delayBeforeWalk);
        }

        // Walk off the bed
        if (walkOffBed)
        {
            Debug.Log("IntroCinematic: Walking off bed");
            yield return StartCoroutine(WalkOffBed());
        }

        // Sync camera rotation before re-enabling input (prevents snap-back)
        if (playerMovement != null)
        {
            playerMovement.SyncRotationFromCamera();
            playerMovement.EnableAllInput();
        }

        // Show crosshair
        if (crosshair != null)
        {
            crosshair.Show();
        }

        cinematicComplete = true;
        Debug.Log("IntroCinematic: Complete - player control enabled");

        // Show dialogue if configured
        if (showDialogueOnComplete && !string.IsNullOrEmpty(dialogueText))
        {
            SimpleDialogueTrigger.ShowDialogue(dialogueText, speakerName, dialogueDuration, dialogueTypewriterSpeed);
        }

        // Fire completion event
        onCinematicComplete?.Invoke();
    }

    private IEnumerator AnimateCameraLookDown()
    {
        if (playerCamera == null) yield break;

        float elapsed = 0f;
        Quaternion startRotation = Quaternion.Euler(startingLookAngle, 0, 0);
        Quaternion endRotation = Quaternion.Euler(endingLookAngle, 0, 0);

        while (elapsed < lookDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lookDownDuration;

            // Use smooth easing
            float smoothT = SmoothStep(t);

            playerCamera.transform.localRotation = Quaternion.Slerp(startRotation, endRotation, smoothT);
            yield return null;
        }

        playerCamera.transform.localRotation = endRotation;
    }

    private IEnumerator WalkOffBed()
    {
        if (characterController == null) yield break;

        Vector3 targetPosition;

        if (walkTarget != null)
        {
            targetPosition = walkTarget.position;
        }
        else
        {
            // Walk forward from current position
            targetPosition = characterController.transform.position +
                             characterController.transform.forward * walkDistance;
        }

        // Calculate direction (ignore Y)
        Vector3 direction = targetPosition - characterController.transform.position;
        direction.y = 0;
        float distance = direction.magnitude;

        if (distance < 0.1f) yield break;

        direction.Normalize();

        // Play footstep sounds using Movement's system
        float footstepInterval = 0.5f;
        float lastFootstepTime = Time.time;

        while (true)
        {
            Vector3 toTarget = targetPosition - characterController.transform.position;
            toTarget.y = 0;

            if (toTarget.magnitude < 0.3f)
            {
                break;
            }

            // Move toward target
            Vector3 moveDirection = direction * walkSpeed;

            // Apply gravity
            if (!characterController.isGrounded)
            {
                moveDirection.y = -9.81f;
            }
            else
            {
                moveDirection.y = -0.1f;
            }

            characterController.Move(moveDirection * Time.deltaTime);

            // Play footstep sounds
            if (characterController.isGrounded && Time.time - lastFootstepTime >= footstepInterval)
            {
                PlayFootstepSound();
                lastFootstepTime = Time.time;
            }

            yield return null;
        }
    }

    private void PlayFootstepSound()
    {
        if (playerMovement != null && playerMovement.footstepAudioSource != null)
        {
            AudioClip[] clips = playerMovement.walkingFootstepSounds;
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    playerMovement.footstepAudioSource.PlayOneShot(clip, playerMovement.footstepVolume);
                }
            }
        }
    }

    /// <summary>
    /// Smooth step function for easing (slow at start and end).
    /// </summary>
    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Skip the cinematic (for testing or player choice).
    /// </summary>
    public void SkipCinematic()
    {
        if (cinematicComplete) return;

        StopAllCoroutines();

        // Reset camera to normal
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(endingLookAngle, 0, 0);
        }

        // Sync and enable player input
        if (playerMovement != null)
        {
            playerMovement.SyncRotationFromCamera();
            playerMovement.EnableAllInput();
        }

        // Show crosshair
        CrosshairManager crosshair = FindFirstObjectByType<CrosshairManager>();
        if (crosshair != null)
        {
            crosshair.Show();
        }

        cinematicComplete = true;
        Debug.Log("IntroCinematic: Skipped");
    }
}
