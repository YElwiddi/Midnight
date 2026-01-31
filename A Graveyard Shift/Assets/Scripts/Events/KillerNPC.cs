using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Defines the current state of the killer NPC.
/// </summary>
public enum KillerState
{
    Idle,       // Waiting for player (ambush mode)
    Chasing,    // Actively chasing the player
    Killing     // Game over triggered
}

/// <summary>
/// Component that makes an NPC a "killer" that can end the game when touching the player.
/// Can be configured to use a jumpscare or instant game over.
/// Supports ambush mode where the killer waits until the player is close and looking at them.
/// </summary>
public class KillerNPC : MonoBehaviour
{
    [Header("Kill Settings")]
    [Tooltip("If true, the game ends when the killer touches the player")]
    [SerializeField] private bool endGameOnPlayerTouch = true;

    [Tooltip("Distance at which the killer triggers the game over")]
    [SerializeField] private float killDistance = 1.5f;

    [Tooltip("If true, uses jumpscare effect before game over")]
    [SerializeField] private bool useJumpscare = true;

    [Header("Ambush Settings")]
    [Tooltip("If true, killer stays idle until player is close and looking at them")]
    [SerializeField] private bool useAmbushMode = false;

    [Tooltip("Animation bool to set while idle/waiting")]
    [SerializeField] private string idleAnimationBool = "IsIdle";

    [Tooltip("Distance at which the killer can be triggered")]
    [SerializeField] private float activationDistance = 10f;

    [Tooltip("If true, player must be looking at the killer to trigger chase")]
    [SerializeField] private bool requirePlayerLooking = true;

    [Tooltip("How long the player must look at the killer before it activates (seconds)")]
    [SerializeField] private float lookDurationRequired = 0f;

    [Tooltip("Field of view angle for detecting if player is looking (degrees from center)")]
    [SerializeField] private float playerLookAngle = 30f;

    [Tooltip("Animation trigger to play when starting to chase")]
    [SerializeField] private string chaseAnimationTrigger = "StartChase";

    [Tooltip("Animation bool to set while chasing")]
    [SerializeField] private string chaseAnimationBool = "";

    [Tooltip("Movement speed when chasing the player")]
    [SerializeField] private float chaseSpeed = 6f;

    [Tooltip("Sound to play when the killer activates and starts chasing")]
    [SerializeField] private AudioClip activationSound;

    [Tooltip("Volume of the activation sound")]
    [SerializeField] private float activationSoundVolume = 1f;

    [Header("Kill Sequence Settings")]
    [Tooltip("Distance from player where killer stops to perform kill")]
    [SerializeField] private float killStopDistance = 1.5f;

    [Tooltip("Animation trigger to play when killing")]
    [SerializeField] private string killAnimationTrigger = "Kill";

    [Tooltip("If true, slows down time during kill sequence")]
    [SerializeField] private bool useSlowMotion = true;

    [Tooltip("Time scale during kill sequence")]
    [SerializeField] private float slowMotionTimeScale = 0.3f;

    [Tooltip("Duration of slow motion in real seconds")]
    [SerializeField] private float slowMotionDuration = 2f;

    [Header("Jumpscare Settings")]
    [Tooltip("Reference to the killer's face transform for camera focus")]
    [SerializeField] private Transform killerFace;

    [Tooltip("If no face transform, camera looks at killer position + this height")]
    [SerializeField] private float faceHeightOffset = 1.6f;

    [Tooltip("Vertical offset applied to face position (use negative to look lower, e.g., -0.2 to look at eyes instead of top of head)")]
    [SerializeField] private float faceLookVerticalOffset = 0f;

    [Tooltip("Sound to play during kill sequence")]
    [SerializeField] private AudioClip jumpscareSound;

    [Tooltip("Delay before game over in real seconds")]
    [SerializeField] private float gameOverDelay = 2f;

    [Tooltip("Camera shake intensity")]
    [SerializeField] private float shakeIntensity = 0.5f;

    [Tooltip("Camera shake duration")]
    [SerializeField] private float shakeDuration = 2f;

    [Header("Player Position During Jumpscare")]
    [Tooltip("How much to lower the player during jumpscare so they look UP at the killer (negative = lower)")]
    [SerializeField] private float jumpscarePlayerHeightOffset = -0.3f;

    [Header("Flashlight Settings")]
    [Tooltip("Height offset for flashlight target (0 = killer's feet, 1.6 = typical face height)")]
    [SerializeField] private float flashlightTargetHeight = 1.2f;

    [Tooltip("Flashlight intensity during jumpscare")]
    [SerializeField] private float jumpscareFlashlightIntensity = 3f;

    [Tooltip("Flashlight range during jumpscare")]
    [SerializeField] private float jumpscareFlashlightRange = 15f;

    [Header("Screen Effect")]
    [Tooltip("Prefab to instantiate for screen effect during kill")]
    [SerializeField] private GameObject screenEffectPrefab;

    [Tooltip("Delay before showing screen effect")]
    [SerializeField] private float screenEffectDelay = 0f;

    [Header("VHS Effect Intensify")]
    [SerializeField] private bool intensifyVHSOnKill = true;
    [SerializeField] private float killGlitchIntensity = 0.7f;
    [SerializeField] private float killRGBShift = 0.04f;
    [SerializeField] private float killNoiseIntensity = 0.25f;
    [SerializeField] private float killScanlineIntensity = 0.8f;
    [SerializeField] private float killTrackingNoise = 0.1f;

    [Header("Game Over Settings")]
    [Tooltip("Optional: UI canvas group to fade in on game over")]
    [SerializeField] private CanvasGroup gameOverUI;

    [Tooltip("Optional: Scene to load on game over (leave empty to just show UI)")]
    [SerializeField] private string gameOverSceneName = "";

    [Header("References")]
    [Tooltip("Player transform (auto-found if not set)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Player camera (auto-found if not set)")]
    [SerializeField] private Camera playerCamera;

    // Runtime state
    private KillerState currentState = KillerState.Idle;
    private bool hasTriggeredGameOver = false;
    private AudioSource audioSource;
    private Animator animator;
    private NavMeshAgent navAgent;
    private float currentShakeAmount = 0f;
    private float currentShakeDuration = 0f;
    private bool isShaking = false;

    // Ambush state tracking
    private float playerLookTimer = 0f;
    private bool isPlayerLooking = false;
    private bool hasBeenInitialized = false;

    // Jumpscare spotlight tracking
    private Light jumpscareSpotlight;


    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Try to find Animator on this object first, then in children
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Auto-find player if not set
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Auto-find camera if not set
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Try to find killer face if not set
        if (killerFace == null)
        {
            // Try common names for head/face (search recursively)
            string[] faceNames = { "Head", "head", "Face", "face", "Skull", "skull" };
            foreach (string faceName in faceNames)
            {
                Transform found = FindChildRecursive(transform, faceName);
                if (found != null)
                {
                    killerFace = found;
                    Debug.Log($"KillerNPC: Found face transform '{faceName}' at {found.name}");
                    break;
                }
            }

            // If still not found, use self
            if (killerFace == null)
            {
                killerFace = transform;
                Debug.LogWarning("KillerNPC: Could not find face transform, using root transform");
            }
        }

        // Only initialize state if not already done via Initialize()
        if (!hasBeenInitialized)
        {
            if (useAmbushMode)
            {
                EnterIdleState();
            }
            else if (navAgent != null && playerTransform != null)
            {
                EnterChaseState();
            }
        }
    }

    private void Update()
    {
        if (hasTriggeredGameOver)
        {
            if (playerCamera != null)
            {
                // Handle camera shake during jumpscare (includes face lock)
                if (isShaking && currentShakeDuration > 0)
                {
                    ApplyCameraShake();
                }
                else
                {
                    // Keep camera locked on killer's face when not shaking
                    LockCameraOnKiller();
                }
            }

            // Keep spotlight pointed at killer throughout jumpscare
            UpdateSpotlightTarget();

            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case KillerState.Idle:
                UpdateIdleState(distanceToPlayer);
                break;

            case KillerState.Chasing:
                UpdateChaseState(distanceToPlayer);
                break;

            case KillerState.Killing:
                // Already triggered game over, handled above
                break;
        }
    }

    private void UpdateIdleState(float distanceToPlayer)
    {
        // Check if player is within activation distance
        if (distanceToPlayer > activationDistance)
        {
            // Player too far, reset look timer
            playerLookTimer = 0f;
            isPlayerLooking = false;
            return;
        }

        // Player is close enough, check if looking (if required)
        if (requirePlayerLooking)
        {
            isPlayerLooking = IsPlayerLookingAtMe();

            if (isPlayerLooking)
            {
                // Accumulate look time
                playerLookTimer += Time.deltaTime;

                // Check if looked long enough
                if (playerLookTimer >= lookDurationRequired)
                {
                    ActivateChase();
                }
            }
            else
            {
                // Reset timer if player looks away
                playerLookTimer = 0f;
            }
        }
        else
        {
            // No looking required, activate immediately when in range
            ActivateChase();
        }
    }

    private void UpdateChaseState(float distanceToPlayer)
    {
        if (playerTransform == null)
        {
            return;
        }

        // Use horizontal distance only (ignore Y difference) for more reliable detection
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0;
        float horizontalDistance = toPlayer.magnitude;

        // Always face the player during chase
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toPlayer);
            transform.rotation = targetRotation;
        }

        // Check for kill distance - pure distance check
        if (endGameOnPlayerTouch && horizontalDistance <= killStopDistance)
        {
            // Stop movement immediately
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }

            Debug.Log($"KillerNPC: Kill range reached! Distance: {horizontalDistance}");
            TriggerGameOver();
            return;
        }

        // Only chase if outside kill range
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(playerTransform.position);
        }
    }

    private bool IsPlayerLookingAtMe()
    {
        if (playerCamera == null)
        {
            return false;
        }

        // Get direction from camera to killer
        Vector3 directionToKiller = (transform.position - playerCamera.transform.position).normalized;

        // Get camera forward direction
        Vector3 cameraForward = playerCamera.transform.forward;

        // Calculate angle between camera forward and direction to killer
        float angle = Vector3.Angle(cameraForward, directionToKiller);

        // Player is looking if angle is within the look angle threshold
        return angle <= playerLookAngle;
    }

    private void EnterIdleState()
    {
        currentState = KillerState.Idle;
        playerLookTimer = 0f;
        isPlayerLooking = false;

        // Stop movement
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Set idle animation
        if (animator != null && !string.IsNullOrEmpty(idleAnimationBool))
        {
            animator.SetBool(idleAnimationBool, true);
        }

        Debug.Log($"KillerNPC '{gameObject.name}': Entered idle state, waiting for player...");
    }

    private void EnterChaseState()
    {
        currentState = KillerState.Chasing;

        Debug.Log($"KillerNPC '{gameObject.name}': EnterChaseState called. Animator: {animator}, chaseAnimationBool: '{chaseAnimationBool}'");

        // Clear idle animation
        if (animator != null && !string.IsNullOrEmpty(idleAnimationBool))
        {
            animator.SetBool(idleAnimationBool, false);
            Debug.Log($"KillerNPC: Set {idleAnimationBool} = false");
        }

        // Set chase animation
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(chaseAnimationTrigger))
            {
                animator.SetTrigger(chaseAnimationTrigger);
                Debug.Log($"KillerNPC: Triggered {chaseAnimationTrigger}");
            }
            if (!string.IsNullOrEmpty(chaseAnimationBool))
            {
                animator.SetBool(chaseAnimationBool, true);
                Debug.Log($"KillerNPC: Set {chaseAnimationBool} = true");
            }
            else
            {
                Debug.LogWarning($"KillerNPC: chaseAnimationBool is empty!");
            }
        }
        else
        {
            Debug.LogWarning($"KillerNPC: Animator is null!");
        }

        // Start movement with sharp, responsive settings
        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.speed = chaseSpeed;
            navAgent.acceleration = 1000f;      // Near-instant acceleration
            navAgent.angularSpeed = 1000f;      // Very fast turning
            navAgent.autoBraking = false;       // Don't slow down when approaching destination
            navAgent.updateRotation = false;    // We handle rotation manually to always face player
            navAgent.stoppingDistance = 0f;     // We handle stopping via distance check
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Don't get stuck on player

            if (playerTransform != null)
            {
                navAgent.SetDestination(playerTransform.position);
            }
        }

        Debug.Log($"KillerNPC '{gameObject.name}': Entered chase state!");
    }

    private void ActivateChase()
    {
        Debug.Log($"KillerNPC '{gameObject.name}': Player detected! Activating chase!");

        // Play activation sound
        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound, activationSoundVolume);
        }

        EnterChaseState();
    }

    /// <summary>
    /// Gets the current state of the killer.
    /// </summary>
    public KillerState GetCurrentState() => currentState;

    /// <summary>
    /// Returns true if the killer is currently in ambush/idle mode.
    /// </summary>
    public bool IsInAmbushMode() => useAmbushMode && currentState == KillerState.Idle;

    /// <summary>
    /// Manually trigger the chase (bypasses ambush conditions).
    /// </summary>
    public void ForceActivateChase()
    {
        if (currentState == KillerState.Idle)
        {
            ActivateChase();
        }
    }

    /// <summary>
    /// Initialize the killer NPC with settings from a ConditionalKillerEvent.
    /// </summary>
    public void Initialize(ConditionalKillerEvent killerEvent)
    {
        // Kill settings
        endGameOnPlayerTouch = killerEvent.endGameOnPlayerTouch;
        killDistance = killerEvent.killDistance;
        useJumpscare = killerEvent.useJumpscare;

        // Kill sequence settings
        killStopDistance = killerEvent.killStopDistance;
        killAnimationTrigger = killerEvent.killAnimationTrigger;
        useSlowMotion = killerEvent.useSlowMotion;
        slowMotionTimeScale = killerEvent.slowMotionTimeScale;
        slowMotionDuration = killerEvent.slowMotionDuration;

        // Jumpscare settings
        jumpscareSound = killerEvent.jumpscareSound;
        gameOverDelay = killerEvent.gameOverDelay;
        shakeIntensity = killerEvent.shakeIntensity;
        shakeDuration = killerEvent.shakeDuration;
        faceHeightOffset = killerEvent.faceHeightOffset;
        faceLookVerticalOffset = killerEvent.faceLookVerticalOffset;

        // Player position during jumpscare
        jumpscarePlayerHeightOffset = killerEvent.jumpscarePlayerHeightOffset;

        // Flashlight settings
        flashlightTargetHeight = killerEvent.flashlightTargetHeight;
        jumpscareFlashlightIntensity = killerEvent.jumpscareFlashlightIntensity;
        jumpscareFlashlightRange = killerEvent.jumpscareFlashlightRange;

        // Screen effect settings
        screenEffectPrefab = killerEvent.screenEffectPrefab;
        screenEffectDelay = killerEvent.screenEffectDelay;

        // VHS intensify settings
        intensifyVHSOnKill = killerEvent.intensifyVHSOnKill;
        killGlitchIntensity = killerEvent.killGlitchIntensity;
        killRGBShift = killerEvent.killRGBShift;
        killNoiseIntensity = killerEvent.killNoiseIntensity;
        killScanlineIntensity = killerEvent.killScanlineIntensity;
        killTrackingNoise = killerEvent.killTrackingNoise;

        // Game over settings
        gameOverSceneName = killerEvent.gameOverSceneName;

        // Ambush settings
        useAmbushMode = killerEvent.useAmbushMode;
        idleAnimationBool = killerEvent.idleAnimationBool;
        activationDistance = killerEvent.activationDistance;
        requirePlayerLooking = killerEvent.requirePlayerLooking;
        lookDurationRequired = killerEvent.lookDurationRequired;
        playerLookAngle = killerEvent.playerLookAngle;
        chaseAnimationTrigger = killerEvent.chaseAnimationTrigger;
        chaseAnimationBool = killerEvent.chaseAnimationBool;
        chaseSpeed = killerEvent.chaseSpeed;
        activationSound = killerEvent.activationSound;
        activationSoundVolume = killerEvent.activationSoundVolume;

        // Try to find the face object by name (search recursively)
        if (!string.IsNullOrEmpty(killerEvent.killerFaceObjectName))
        {
            Transform foundFace = FindChildRecursive(transform, killerEvent.killerFaceObjectName);
            if (foundFace != null)
            {
                killerFace = foundFace;
                Debug.Log($"KillerNPC: Found face transform '{killerEvent.killerFaceObjectName}'");
            }
            else
            {
                Debug.LogWarning($"KillerNPC: Could not find face transform '{killerEvent.killerFaceObjectName}'");
            }
        }

        // Ensure we have references (Initialize is called before Start)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }
        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Mark as initialized so Start() doesn't re-initialize
        hasBeenInitialized = true;

        Debug.Log($"KillerNPC Initialize: useAmbushMode={useAmbushMode}, chaseAnimationBool='{chaseAnimationBool}', animator={(animator != null ? "found" : "NULL")}");

        // Initialize state based on ambush mode
        if (useAmbushMode)
        {
            EnterIdleState();
        }
        else
        {
            EnterChaseState();
        }
    }

    /// <summary>
    /// Configure killer settings at runtime.
    /// </summary>
    public void Configure(bool endGame, float distance, bool jumpscare, AudioClip sound, float delay, float shake, float shakeDur)
    {
        endGameOnPlayerTouch = endGame;
        killDistance = distance;
        useJumpscare = jumpscare;
        jumpscareSound = sound;
        gameOverDelay = delay;
        shakeIntensity = shake;
        shakeDuration = shakeDur;
    }

    /// <summary>
    /// Set the kill distance at runtime.
    /// </summary>
    public void SetKillDistance(float distance)
    {
        killDistance = distance;
    }

    /// <summary>
    /// Enable or disable the game over trigger.
    /// </summary>
    public void SetEndGameOnTouch(bool enabled)
    {
        endGameOnPlayerTouch = enabled;
    }

    /// <summary>
    /// Triggers the game over sequence.
    /// </summary>
    public void TriggerGameOver()
    {
        if (hasTriggeredGameOver)
        {
            return;
        }

        hasTriggeredGameOver = true;
        Debug.Log($"KillerNPC: Game over triggered by {gameObject.name}!");

        if (useJumpscare)
        {
            StartCoroutine(JumpscareSequence());
        }
        else
        {
            // Instant game over
            ExecuteGameOver();
        }
    }

    private IEnumerator JumpscareSequence()
    {
        currentState = KillerState.Killing;

        // Stop the killer's movement
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Stop chase animation
        if (animator != null && !string.IsNullOrEmpty(chaseAnimationBool))
        {
            animator.SetBool(chaseAnimationBool, false);
        }

        // Freeze player movement
        FreezePlayer(true);

        // Ground the player (in case they're jumping)
        GroundPlayer();

        // Lower the player position so they look UP at the killer
        if (playerTransform != null && jumpscarePlayerHeightOffset != 0f)
        {
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 loweredPosition = playerTransform.position;
            loweredPosition.y += jumpscarePlayerHeightOffset;
            playerTransform.position = loweredPosition;

            // Keep controller disabled (already disabled by FreezePlayer)
            Debug.Log($"KillerNPC: Lowered player by {jumpscarePlayerHeightOffset} for dramatic upward angle");
        }

        // Lock flashlight on (silently, no click sound)
        LockFlashlightOn();

        // Position killer in front of the player camera
        if (playerCamera != null)
        {
            // Calculate position in front of camera
            Vector3 cameraForward = playerCamera.transform.forward;
            cameraForward.y = 0; // Keep on horizontal plane
            cameraForward.Normalize();

            Vector3 targetPosition = playerCamera.transform.position + cameraForward * killStopDistance;

            // Ground the killer at this position
            if (Physics.Raycast(targetPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            {
                targetPosition.y = hit.point.y;
            }
            else
            {
                targetPosition.y = playerTransform.position.y; // Fall back to player height
            }

            // Move killer to position
            transform.position = targetPosition;

            // Make killer face the player directly
            Vector3 directionToPlayer = playerCamera.transform.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(directionToPlayer);
            }

            // Make camera look at killer's face position
            Vector3 lookTarget = GetKillerFacePosition();
            playerCamera.transform.LookAt(lookTarget);

            // Point flashlight at killer's face
            PointFlashlightAtKiller();
        }

        // Play jumpscare sound
        if (jumpscareSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        // Play kill animation
        if (animator != null && !string.IsNullOrEmpty(killAnimationTrigger))
        {
            animator.SetTrigger(killAnimationTrigger);
            Debug.Log($"KillerNPC: Playing kill animation '{killAnimationTrigger}'");
        }

        // Spawn screen effect (checks for VHS camera effect first, then prefab)
        StartCoroutine(SpawnScreenEffect());

        // Start slow motion
        float originalTimeScale = Time.timeScale;
        float originalFixedDeltaTime = Time.fixedDeltaTime;

        if (useSlowMotion)
        {
            Time.timeScale = slowMotionTimeScale;
            Time.fixedDeltaTime = 0.02f * slowMotionTimeScale; // Adjust physics rate
            Debug.Log($"KillerNPC: Slow motion started (timeScale = {slowMotionTimeScale})");
        }

        // Start camera shake
        StartCameraShake();

        // Show game over UI with fade
        if (gameOverUI != null)
        {
            StartCoroutine(FadeInUI());
        }

        // Wait for slow motion duration (use unscaled time so it's real seconds)
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        // Wait additional delay before game over
        if (gameOverDelay > 0)
        {
            yield return new WaitForSecondsRealtime(gameOverDelay);
        }

        // Restore time scale before game over
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        // Execute game over
        ExecuteGameOver();
    }

    private void StartCameraShake()
    {
        currentShakeAmount = shakeIntensity;
        currentShakeDuration = shakeDuration;
        isShaking = true;
    }

    private Vector3 GetKillerFacePosition()
    {
        Vector3 facePos;
        if (killerFace != null)
        {
            facePos = killerFace.position;
        }
        else
        {
            // Estimate face height if no face transform assigned
            facePos = transform.position + Vector3.up * faceHeightOffset;
        }

        // Apply vertical offset (use negative to look lower on the face)
        facePos.y += faceLookVerticalOffset;
        return facePos;
    }

    private void LockCameraOnKiller()
    {
        Vector3 facePosition = GetKillerFacePosition();
        playerCamera.transform.LookAt(facePosition);
    }

    private void ApplyCameraShake()
    {
        if (playerCamera == null)
        {
            return;
        }

        // Get the base rotation looking at the killer's face
        Vector3 facePosition = GetKillerFacePosition();
        Vector3 lookDir = (facePosition - playerCamera.transform.position).normalized;
        Quaternion baseRotation = Quaternion.LookRotation(lookDir);

        // Apply small random shake offset from the base rotation
        Vector3 shakeOffset = new Vector3(
            Random.Range(-currentShakeAmount, currentShakeAmount),
            Random.Range(-currentShakeAmount, currentShakeAmount),
            0
        );

        // Apply shake as offset from base (keeps camera centered on face)
        playerCamera.transform.rotation = baseRotation * Quaternion.Euler(shakeOffset);

        // Keep spotlight pointed at killer during shake
        UpdateSpotlightTarget();

        // Decrease shake over time (use unscaled time for slow-mo)
        currentShakeDuration -= Time.unscaledDeltaTime;
        currentShakeAmount = Mathf.Lerp(0, shakeIntensity, currentShakeDuration / shakeDuration);

        if (currentShakeDuration <= 0)
        {
            isShaking = false;
            currentShakeDuration = 0f;
            currentShakeAmount = 0f;
        }
    }

    private IEnumerator FadeInUI()
    {
        if (gameOverUI == null)
        {
            yield break;
        }

        gameOverUI.gameObject.SetActive(true);
        float startAlpha = 0f;
        float endAlpha = 1f;
        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            gameOverUI.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        gameOverUI.alpha = endAlpha;
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerTransform == null)
        {
            return;
        }

        // Try to disable common player controller components
        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = !freeze;
        }

        // Disable scripts with Controller or Movement in the name
        MonoBehaviour[] scripts = playerTransform.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("Controller") || typeName.Contains("Movement") || typeName.Contains("Player"))
            {
                script.enabled = !freeze;
            }
        }

        // Also check parent for player controller
        if (playerTransform.parent != null)
        {
            scripts = playerTransform.parent.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                string typeName = script.GetType().Name;
                if (typeName.Contains("Controller") || typeName.Contains("Movement") || typeName.Contains("Player"))
                {
                    script.enabled = !freeze;
                }
            }
        }
    }

    private void GroundPlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        // Raycast down from player to find ground
        RaycastHit hit;
        Vector3 rayStart = playerTransform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f))
        {
            // Move player to ground level
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null)
            {
                // Disable controller temporarily to move player directly
                controller.enabled = false;
                playerTransform.position = hit.point;
                controller.enabled = true;
            }
            else
            {
                playerTransform.position = hit.point;
            }

            Debug.Log($"KillerNPC: Player grounded at Y={hit.point.y}");
        }
    }

    private void LockFlashlightOn()
    {
        // Find the flashlight and force it on without playing sound
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null)
        {
            // Disable the component so player can't toggle it
            flashlight.enabled = false;

            // Directly enable the spotlight without playing sound
            if (flashlight.spotLight != null)
            {
                flashlight.spotLight.enabled = true;
            }

            Debug.Log("KillerNPC: Flashlight locked on (player control disabled)");
        }

        // Hide the crosshair
        CrosshairManager crosshair = FindObjectOfType<CrosshairManager>();
        if (crosshair != null)
        {
            crosshair.Hide();
            Debug.Log("KillerNPC: Crosshair hidden");
        }
    }

    private void PointFlashlightAtKiller()
    {
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null && flashlight.spotLight != null)
        {
            jumpscareSpotlight = flashlight.spotLight;

            // Ensure spotlight is enabled
            jumpscareSpotlight.enabled = true;

            // Apply jumpscare light settings
            jumpscareSpotlight.intensity = jumpscareFlashlightIntensity;
            jumpscareSpotlight.range = jumpscareFlashlightRange;
            jumpscareSpotlight.spotAngle = 60f;

            // Position and point spotlight at killer
            UpdateSpotlightTarget();

            Debug.Log($"KillerNPC: Flashlight pointed at height {flashlightTargetHeight} (intensity: {jumpscareSpotlight.intensity}, range: {jumpscareSpotlight.range})");
        }
    }

    private void UpdateSpotlightTarget()
    {
        if (jumpscareSpotlight == null || playerCamera == null)
        {
            return;
        }

        // Position spotlight at camera position
        jumpscareSpotlight.transform.position = playerCamera.transform.position;

        // Point at killer's configured height
        Vector3 targetPosition = transform.position + Vector3.up * flashlightTargetHeight;
        jumpscareSpotlight.transform.LookAt(targetPosition);
    }

    private IEnumerator SpawnScreenEffect()
    {
        // Wait for delay if specified (using real time since we may be in slow motion)
        if (screenEffectDelay > 0)
        {
            yield return new WaitForSecondsRealtime(screenEffectDelay);
        }

        // Try to intensify VHS effect on camera
        if (playerCamera != null)
        {
            VHSRetroFeature vhsEffect = playerCamera.GetComponent<VHSRetroFeature>();
            if (vhsEffect != null)
            {
                vhsEffect.enabled = true;

                // Apply intensified VHS settings for the kill sequence
                if (intensifyVHSOnKill)
                {
                    vhsEffect.glitchIntensity = killGlitchIntensity;
                    vhsEffect.rgbShiftAmount = killRGBShift;
                    vhsEffect.noiseIntensity = killNoiseIntensity;
                    vhsEffect.scanlineIntensity = killScanlineIntensity;
                    vhsEffect.trackingNoise = killTrackingNoise;
                    Debug.Log($"KillerNPC: VHS effect intensified (glitch: {killGlitchIntensity}, RGB: {killRGBShift}, noise: {killNoiseIntensity})");
                }
                else
                {
                    Debug.Log("KillerNPC: VHS screen effect enabled");
                }
                yield break;
            }
        }

        // Fallback: Instantiate the screen effect prefab
        if (screenEffectPrefab != null)
        {
            GameObject effect = Instantiate(screenEffectPrefab);
            Debug.Log("KillerNPC: Screen effect prefab spawned");
        }
    }

    private void ExecuteGameOver()
    {
        Debug.Log("KillerNPC: Executing game over!");

        // Show game over UI if available
        if (gameOverUI != null)
        {
            gameOverUI.alpha = 1f;
            gameOverUI.gameObject.SetActive(true);
        }

        // Reset game state before loading new scene
        // GameManager persists across scenes (DontDestroyOnLoad), so reset it here
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAllFlags();
            Debug.Log("KillerNPC: GameManager stats reset");
        }

        // Reset time scale in case slow motion was active
        Time.timeScale = 1f;

        // Load game over scene if specified
        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }

        // Fire game over event if GameEventsManager exists
        // You can extend this to call a custom game over handler
    }

    /// <summary>
    /// Recursively searches for a child transform by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        // Check direct children first
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            // Recursively search in this child's children
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // Visualization in editor
    private void OnDrawGizmosSelected()
    {
        // Kill distance (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, killDistance);

        // Activation distance for ambush mode (yellow)
        if (useAmbushMode)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
    }
}
