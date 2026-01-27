using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the player character during a cinematic sequence.
/// Takes over player movement and walks them along waypoints while the player watches.
/// </summary>
public class CinematicPlayerController : MonoBehaviour
{
    /// <summary>
    /// Fired when the cinematic walk sequence is complete (before fade).
    /// </summary>
    public event Action OnCinematicWalkComplete;

    /// <summary>
    /// Fired when the entire cinematic sequence is complete (after fade and ending screen).
    /// </summary>
    public event Action OnCinematicComplete;

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Camera playerCamera;

    [Header("Movement Settings")]
    [Tooltip("Animation parameter name for walking")]
    [SerializeField] private string walkingAnimationBool = "IsWalking";

    [Tooltip("Rotation speed when turning toward waypoints")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    // Runtime state
    private CinematicEndingData currentCinematic;
    private int currentWaypointIndex = 0;
    private bool isWalking = false;
    private float currentMoveSpeed = 2f;
    private Vector3 currentTargetPosition;
    private Movement playerMovement;
    private AudioSource musicAudioSource;

    // Footstep tracking
    private float lastFootstepTime;
    private float footstepInterval = 0.5f;

    private void Awake()
    {
        // Auto-find references if not set
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        playerMovement = GetComponent<Movement>();
    }

    /// <summary>
    /// Starts the cinematic sequence with the given configuration.
    /// </summary>
    public void StartCinematic(CinematicEndingData cinematicData)
    {
        if (cinematicData == null)
        {
            Debug.LogError("CinematicPlayerController: No cinematic data provided!");
            return;
        }

        if (cinematicData.playerWaypoints == null || cinematicData.playerWaypoints.Length == 0)
        {
            Debug.LogError("CinematicPlayerController: No waypoints configured in cinematic data!");
            return;
        }

        currentCinematic = cinematicData;
        currentWaypointIndex = 0;

        Debug.Log($"CinematicPlayerController: Starting cinematic '{cinematicData.cinematicName}'");

        StartCoroutine(CinematicSequence());
    }

    private IEnumerator CinematicSequence()
    {
        // Disable player input
        DisablePlayerInput();

        // Fade out current audio and start cinematic music if configured
        if (currentCinematic.fadeOutCurrentAudio)
        {
            yield return StartCoroutine(FadeOutCurrentAudio());
        }

        if (currentCinematic.cinematicMusic != null)
        {
            PlayCinematicMusic();
        }

        // Walk through all waypoints
        while (currentWaypointIndex < currentCinematic.playerWaypoints.Length)
        {
            CinematicWaypointData waypoint = currentCinematic.playerWaypoints[currentWaypointIndex];

            // Find waypoint target
            GameObject waypointObj = GameObject.Find(waypoint.waypointName);
            if (waypointObj == null)
            {
                Debug.LogWarning($"CinematicPlayerController: Waypoint '{waypoint.waypointName}' not found, skipping");
                currentWaypointIndex++;
                continue;
            }

            currentTargetPosition = waypointObj.transform.position;
            currentMoveSpeed = waypoint.moveSpeed;

            Debug.Log($"CinematicPlayerController: Walking to waypoint '{waypoint.waypointName}'");

            // Walk to waypoint
            yield return StartCoroutine(WalkToPosition(currentTargetPosition, currentMoveSpeed));

            // Wait at waypoint if configured
            if (waypoint.waitTime > 0)
            {
                SetWalkingAnimation(false);
                yield return new WaitForSeconds(waypoint.waitTime);
            }

            currentWaypointIndex++;
        }

        // Stop walking animation
        SetWalkingAnimation(false);

        Debug.Log("CinematicPlayerController: All waypoints reached");
        OnCinematicWalkComplete?.Invoke();

        // Delay before fade
        if (currentCinematic.delayBeforeFade > 0)
        {
            yield return new WaitForSeconds(currentCinematic.delayBeforeFade);
        }

        // Fade to black
        yield return StartCoroutine(FadeToBlack(currentCinematic.fadeOutDuration));

        // Show ending screen
        yield return StartCoroutine(ShowEndingScreen());

        // Complete
        OnCinematicComplete?.Invoke();
    }

    private IEnumerator WalkToPosition(Vector3 targetPosition, float speed)
    {
        isWalking = true;
        SetWalkingAnimation(true);
        lastFootstepTime = Time.time;

        // Calculate footstep interval based on speed
        footstepInterval = 0.5f / (speed / 2f);

        float arrivalThreshold = 0.5f;

        while (true)
        {
            // Calculate direction to target (ignore Y for direction)
            Vector3 directionToTarget = targetPosition - transform.position;
            directionToTarget.y = 0;

            float distanceToTarget = directionToTarget.magnitude;

            // Check if arrived
            if (distanceToTarget <= arrivalThreshold)
            {
                break;
            }

            // Rotate toward target
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Move forward
            Vector3 moveDirection = transform.forward * speed;

            // Apply gravity
            if (!characterController.isGrounded)
            {
                moveDirection.y = -9.81f;
            }
            else
            {
                moveDirection.y = -0.1f; // Small downward force to keep grounded
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

        isWalking = false;
    }

    private void PlayFootstepSound()
    {
        // Try to use the Movement component's footstep system
        if (playerMovement != null && playerMovement.footstepAudioSource != null)
        {
            AudioClip[] clips = playerMovement.walkingFootstepSounds;
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    playerMovement.footstepAudioSource.PlayOneShot(clip, playerMovement.footstepVolume);
                }
            }
        }
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(walkingAnimationBool))
        {
            // Check if the parameter exists before setting it
            foreach (var param in playerAnimator.parameters)
            {
                if (param.name == walkingAnimationBool && param.type == AnimatorControllerParameterType.Bool)
                {
                    playerAnimator.SetBool(walkingAnimationBool, walking);
                    return;
                }
            }
        }
        // No animator or parameter found - that's fine for first-person games
    }

    private void DisablePlayerInput()
    {
        // Disable Movement component
        if (playerMovement != null)
        {
            playerMovement.canMove = false;
            playerMovement.canControlCamera = false;
            playerMovement.canRun = false;
            playerMovement.canJump = false;
        }

        // Also try PlayerEvents if available
        PlayerEvents playerEvents = GetComponent<PlayerEvents>();
        if (playerEvents != null)
        {
            playerEvents.DisableAllInput();
        }

        // Unlock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("CinematicPlayerController: Player input disabled");
    }

    private IEnumerator FadeOutCurrentAudio()
    {
        // Find all active audio sources and fade them out
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        float fadeDuration = currentCinematic.audioFadeOutDuration;
        float elapsed = 0f;

        // Store original volumes
        float[] originalVolumes = new float[audioSources.Length];
        for (int i = 0; i < audioSources.Length; i++)
        {
            originalVolumes[i] = audioSources[i].volume;
        }

        // Fade out
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null && audioSources[i] != musicAudioSource)
                {
                    audioSources[i].volume = Mathf.Lerp(originalVolumes[i], 0f, t);
                }
            }

            yield return null;
        }

        // Stop all audio except our music
        foreach (AudioSource source in audioSources)
        {
            if (source != null && source != musicAudioSource)
            {
                source.Stop();
            }
        }

        Debug.Log("CinematicPlayerController: Audio faded out");
    }

    private void PlayCinematicMusic()
    {
        if (musicAudioSource == null)
        {
            musicAudioSource = gameObject.AddComponent<AudioSource>();
        }

        musicAudioSource.clip = currentCinematic.cinematicMusic;
        musicAudioSource.volume = currentCinematic.musicVolume;
        musicAudioSource.loop = true;
        musicAudioSource.Play();

        Debug.Log("CinematicPlayerController: Playing cinematic music");
    }

    private IEnumerator FadeToBlack(float duration)
    {
        // Create fade canvas if not assigned
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = CreateFadeCanvas();
        }

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        Debug.Log("CinematicPlayerController: Faded to black");
    }

    private CanvasGroup CreateFadeCanvas()
    {
        // Create a full-screen black fade overlay
        GameObject canvasObj = new GameObject("CinematicFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // On top of everything

        CanvasGroup group = canvasObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;

        // Add black image
        GameObject imageObj = new GameObject("BlackOverlay");
        imageObj.transform.SetParent(canvasObj.transform, false);

        UnityEngine.UI.Image image = imageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;

        // Make it fill the screen
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return group;
    }

    private IEnumerator ShowEndingScreen()
    {
        // Find or create EndingScreenUI
        EndingScreenUI endingUI = FindObjectOfType<EndingScreenUI>();

        if (endingUI == null)
        {
            // Create a basic ending screen
            endingUI = CreateBasicEndingScreen();
        }

        // Show the ending screen
        endingUI.Show(
            currentCinematic.endingTitle,
            currentCinematic.endingDescription,
            currentCinematic.endingScreenDuration,
            currentCinematic.menuSceneName
        );

        // Wait for the ending screen duration
        yield return new WaitForSeconds(currentCinematic.endingScreenDuration);
    }

    private EndingScreenUI CreateBasicEndingScreen()
    {
        GameObject uiObj = new GameObject("EndingScreenUI");
        EndingScreenUI endingUI = uiObj.AddComponent<EndingScreenUI>();
        return endingUI;
    }

    /// <summary>
    /// Static helper to start a cinematic on the player.
    /// </summary>
    public static void StartCinematicOnPlayer(CinematicEndingData cinematicData)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("CinematicPlayerController: No player found!");
            return;
        }

        CinematicPlayerController controller = player.GetComponent<CinematicPlayerController>();
        if (controller == null)
        {
            controller = player.AddComponent<CinematicPlayerController>();
        }

        controller.StartCinematic(cinematicData);
    }
}
