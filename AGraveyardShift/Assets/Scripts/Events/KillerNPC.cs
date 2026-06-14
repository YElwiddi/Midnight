using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("Endings Integration")]
    [Tooltip("If true, killing the player unlocks an ending + shows the reveal, then returns to the main menu.")]
    public bool unlocksEnding = false;
    [Tooltip("Which ending to unlock when this killer kills the player. Copied from the ConditionalKillerEvent on spawn.")]
    public Ending endingToUnlock = Ending.Ambush;

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

    [Tooltip("If true, killer instantly reaches max speed with no acceleration")]
    [SerializeField] private bool instantAcceleration = false;

    [Tooltip("Looping sound to play at the killer's position while idle (stops when chase begins)")]
    [SerializeField] private AudioClip idleLoopSound;

    [Tooltip("Volume of the idle loop sound")]
    [SerializeField] private float idleLoopVolume = 0.5f;

    [Tooltip("Sound to play when the killer activates and starts chasing")]
    [SerializeField] private AudioClip activationSound;

    [Tooltip("Volume of the activation sound")]
    [SerializeField] private float activationSoundVolume = 1f;

    [Tooltip("If true, activation sound plays at constant volume (2D) instead of from killer's position")]
    [SerializeField] private bool activationSoundConstant = false;

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
    [SerializeField] private bool lockFlashlightOnDuringJumpscare = true;

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

    // Jumpscare head shake
    private bool jumpscareHeadShake = false;
    private string headShakeBoneName = "CC_Base_Head";
    private float headShakeTiltAmount = 35f;
    private float headShakeTurnAmount = 15f;
    private float headShakeSpeed = 30f;
    private float headShakeRandomness = 10f;
    private float headShakeActiveDuration = 1.5f;
    private float headShakePauseDuration = 0.6f;
    private float headShakeTimingVariance = 0.3f;
    private float headShakeStuckChance = 0.5f;
    private float headShakeStuckMaxAngle = 25f;
    private Transform headShakeBone;
    private float headShakeTimer = 0f;
    private float headShakeCurrentInterval = 0f;
    private bool headShakeActive = true;
    private bool headShakeStuck = false;
    private Quaternion headShakeStuckRotation;

    // Jumpscare dialogue
    private bool showJumpscareDialogue = false;
    private string jumpscareDialogueText = "";
    private TMP_FontAsset jumpscareDialogueFont;
    private Color jumpscareDialogueColor = Color.white;
    private float jumpscareDialogueFontSize = 36f;
    private float jumpscareDialogueDelay = 0.5f;
    private float jumpscareDialogueShakeIntensity = 0f;
    private float jumpscareDialogueShakeSpeed = 25f;
    private GameObject jumpscareDialogueInstance;
    private TextMeshProUGUI jumpscareDialogueTMP;

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

    // Stuck detection for NavMeshAgent
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 0.01f;
    private bool navAgentAbandoned = false;

    // Lantern override
    private bool overrideLanternsOnJumpscare = false;
    private Color jumpscareLanternColor = Color.red;


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

    private void LateUpdate()
    {
        if (!hasTriggeredGameOver || !jumpscareHeadShake || headShakeBone == null)
            return;

        // Advance timer
        headShakeTimer += Time.unscaledDeltaTime;

        if (headShakeTimer >= headShakeCurrentInterval)
        {
            // Switch state
            headShakeActive = !headShakeActive;
            headShakeTimer = 0f;

            float baseDuration = headShakeActive ? headShakeActiveDuration : headShakePauseDuration;
            headShakeCurrentInterval = baseDuration + Random.Range(-headShakeTimingVariance, headShakeTimingVariance);

            // When entering a pause, decide if it's stuck at a random angle
            if (!headShakeActive)
            {
                headShakeStuck = Random.value < headShakeStuckChance;
                if (headShakeStuck)
                {
                    // Pick a random stuck angle — biased toward looking slightly up with a lateral tilt
                    float stuckX = Random.Range(-headShakeStuckMaxAngle * 0.6f, -headShakeStuckMaxAngle * 0.15f); // negative X = look upward
                    float stuckY = Random.Range(-headShakeStuckMaxAngle * 0.5f, headShakeStuckMaxAngle * 0.5f);
                    float stuckZ = Random.Range(-headShakeStuckMaxAngle, headShakeStuckMaxAngle);
                    headShakeStuckRotation = Quaternion.Euler(stuckX, stuckY, stuckZ);
                }
            }
        }

        if (headShakeActive)
        {
            float t = Time.unscaledTime * headShakeSpeed;
            float tilt = Mathf.Sin(t) * headShakeTiltAmount;
            float turn = Mathf.Sin(t * 1.3f) * headShakeTurnAmount;
            float jitterX = Random.Range(-headShakeRandomness, headShakeRandomness);
            float jitterY = Random.Range(-headShakeRandomness * 0.5f, headShakeRandomness * 0.5f);

            headShakeBone.localRotation *= Quaternion.Euler(jitterX, turn + jitterY, tilt);
        }
        else
        {
            // Micro-vibrate during pauses (stuck or center)
            float microX = Random.Range(-1f, 1f);
            float microY = Random.Range(-0.5f, 0.5f);
            float microZ = Random.Range(-1f, 1f);
            Quaternion microShake = Quaternion.Euler(microX, microY, microZ);

            if (headShakeStuck)
            {
                headShakeBone.localRotation *= headShakeStuckRotation * microShake;
            }
            else
            {
                headShakeBone.localRotation *= microShake;
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

            // Apply text shake to jumpscare dialogue
            if (jumpscareDialogueTMP != null && jumpscareDialogueShakeIntensity > 0f)
            {
                ApplyJumpscareTextShake();
            }

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
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }

            Debug.Log($"KillerNPC: Kill range reached! Distance: {horizontalDistance}");
            TriggerGameOver();
            return;
        }

        // Chase the player
        if (!navAgentAbandoned && navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(playerTransform.position);

            // Detect if the agent is stuck (on NavMesh but not actually moving)
            if (navAgent.velocity.sqrMagnitude < 0.01f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= STUCK_THRESHOLD)
                {
                    Debug.LogWarning($"KillerNPC: NavMeshAgent stuck for {stuckTimer:F1}s (velocity={navAgent.velocity.magnitude:F3}, pathStatus={navAgent.pathStatus}, hasPath={navAgent.hasPath}, pending={navAgent.pathPending}). Switching to direct movement.");
                    navAgent.enabled = false;
                    navAgentAbandoned = true;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            // Direct movement toward the player (no pathfinding)
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.enabled = false;
            }
            Vector3 moveDir = toPlayer.normalized;
            transform.position += moveDir * chaseSpeed * Time.deltaTime;
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
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Set idle animation
        if (animator != null && !string.IsNullOrEmpty(idleAnimationBool))
        {
            animator.SetBool(idleAnimationBool, true);
        }

        // Start idle loop sound
        if (idleLoopSound != null && audioSource != null)
        {
            audioSource.clip = idleLoopSound;
            audioSource.volume = idleLoopVolume;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f; // 3D sound at killer's position
            audioSource.Play();
            Debug.Log("KillerNPC: Idle loop sound started");
        }

        Debug.Log($"KillerNPC '{gameObject.name}': Entered idle state, waiting for player...");
    }

    private void EnterChaseState()
    {
        currentState = KillerState.Chasing;

        // Note: idle loop sound is stopped in ActivateChase() before PlayOneShot.
        // Stopping here would kill the activation sound since audioSource.Stop() kills PlayOneShot too.
        // Safety: just clear loop flag and clip reference without calling Stop().
        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.clip = null;
        }

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
            // If the agent isn't on a NavMesh, warp it to the nearest valid point
            if (!navAgent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                    Debug.Log($"KillerNPC: Warped to nearest NavMesh point (offset: {Vector3.Distance(transform.position, hit.position):F2}m)");
                }
                else
                {
                    Debug.LogWarning("KillerNPC: No NavMesh found within 10m! Agent cannot pathfind.");
                }
            }

            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = chaseSpeed;
                navAgent.acceleration = instantAcceleration ? 999999f : 1000f;
                navAgent.angularSpeed = 1000f;      // Very fast turning
                navAgent.autoBraking = false;       // Don't slow down when approaching destination
                navAgent.updateRotation = false;    // We handle rotation manually to always face player
                navAgent.stoppingDistance = 0f;     // We handle stopping via distance check
                navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Don't get stuck on player

                if (playerTransform != null)
                {
                    navAgent.SetDestination(playerTransform.position);

                    // Force immediate full-speed velocity toward player
                    if (instantAcceleration)
                    {
                        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                        navAgent.velocity = dirToPlayer * chaseSpeed;
                    }
                }
            }
        }

        // Log NavMesh status for debugging
        if (navAgent != null)
        {
            Debug.Log($"KillerNPC: NavAgent status - isOnNavMesh={navAgent.isOnNavMesh}, enabled={navAgent.enabled}, position={transform.position}");
        }

        // Override lanterns (turn red, lock them) as soon as chase begins
        if (overrideLanternsOnJumpscare)
        {
            LanternInteractable.LockAllLanterns(jumpscareLanternColor);
        }

        Debug.Log($"KillerNPC '{gameObject.name}': Entered chase state!");
    }

    private void ActivateChase()
    {
        Debug.Log($"KillerNPC '{gameObject.name}': Player detected! Activating chase!");

        // Stop idle loop sound BEFORE playing activation sound
        // (audioSource.Stop() kills all sounds including PlayOneShot, so we must stop first)
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == idleLoopSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
            Debug.Log("KillerNPC: Idle loop sound stopped (chase activation)");
        }

        // Play activation sound (after idle loop is stopped)
        if (activationSound != null && audioSource != null)
        {
            if (activationSoundConstant)
            {
                // Play as 2D (constant volume regardless of distance/direction)
                audioSource.spatialBlend = 0f;
                audioSource.PlayOneShot(activationSound, activationSoundVolume);
            }
            else
            {
                audioSource.PlayOneShot(activationSound, activationSoundVolume);
            }
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
        lockFlashlightOnDuringJumpscare = killerEvent.lockFlashlightOnDuringJumpscare;
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

        // Lantern override settings
        overrideLanternsOnJumpscare = killerEvent.overrideLanternsOnJumpscare;
        jumpscareLanternColor = killerEvent.jumpscareLanternColor;

        // Jumpscare head shake settings
        jumpscareHeadShake = killerEvent.jumpscareHeadShake;
        headShakeBoneName = killerEvent.headShakeBoneName;
        headShakeTiltAmount = killerEvent.headShakeTiltAmount;
        headShakeTurnAmount = killerEvent.headShakeTurnAmount;
        headShakeSpeed = killerEvent.headShakeSpeed;
        headShakeRandomness = killerEvent.headShakeRandomness;
        headShakeActiveDuration = killerEvent.headShakeActiveDuration;
        headShakePauseDuration = killerEvent.headShakePauseDuration;
        headShakeTimingVariance = killerEvent.headShakeTimingVariance;
        headShakeStuckChance = killerEvent.headShakeStuckChance;
        headShakeStuckMaxAngle = killerEvent.headShakeStuckMaxAngle;

        // Find the head shake bone
        if (jumpscareHeadShake && !string.IsNullOrEmpty(headShakeBoneName))
        {
            headShakeBone = FindChildRecursive(transform, headShakeBoneName);
            if (headShakeBone != null)
            {
                headShakeCurrentInterval = headShakeActiveDuration;
                Debug.Log($"KillerNPC: Found head shake bone '{headShakeBoneName}'");
            }
            else
            {
                Debug.LogWarning($"KillerNPC: Could not find head shake bone '{headShakeBoneName}'");
            }
        }

        // Jumpscare dialogue settings
        showJumpscareDialogue = killerEvent.showJumpscareDialogue;
        jumpscareDialogueText = killerEvent.jumpscareDialogueText;
        jumpscareDialogueFont = killerEvent.jumpscareDialogueFont;
        jumpscareDialogueColor = killerEvent.jumpscareDialogueColor;
        jumpscareDialogueFontSize = killerEvent.jumpscareDialogueFontSize;
        jumpscareDialogueDelay = killerEvent.jumpscareDialogueDelay;
        jumpscareDialogueShakeIntensity = killerEvent.jumpscareDialogueShakeIntensity;
        jumpscareDialogueShakeSpeed = killerEvent.jumpscareDialogueShakeSpeed;

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
        instantAcceleration = killerEvent.instantAcceleration;
        idleLoopSound = killerEvent.idleLoopSound;
        idleLoopVolume = killerEvent.idleLoopVolume;
        activationSound = killerEvent.activationSound;
        activationSoundVolume = killerEvent.activationSoundVolume;
        activationSoundConstant = killerEvent.activationSoundConstant;

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

        // Endings integration — carry the unlock tag from the event config.
        unlocksEnding = killerEvent.unlocksEnding;
        endingToUnlock = killerEvent.endingToUnlock;

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
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Stop chase animation
        if (animator != null && !string.IsNullOrEmpty(chaseAnimationBool))
        {
            animator.SetBool(chaseAnimationBool, false);
        }

        // Close any open ReadableUI (notes, books, etc.)
        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
        {
            ReadableUI.Instance.Close();
        }

        // Close any active dialogues
        SimpleDialogueTrigger.CancelActiveSimpleDialogue();
        DialogueUI dialogueUI = FindObjectOfType<DialogueUI>();
        if (dialogueUI != null && dialogueUI.IsVisible)
        {
            dialogueUI.ClearChoices();
            dialogueUI.Hide();
        }
        DialogueManager dialogueManager = DialogueManager.GetInstance();
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            dialogueManager.ExitDialogueMode();
        }

        // Freeze player movement (including mouse look on children)
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
        if (lockFlashlightOnDuringJumpscare)
        {
            LockFlashlightOn();
        }
        else
        {
            LockFlashlightOff();
        }

        // Hide the crosshair
        HideCrosshair();

        // Position killer in front of the player camera
        if (playerCamera != null)
        {
            // Calculate position in front of camera
            Vector3 cameraForward = playerCamera.transform.forward;
            cameraForward.y = 0; // Keep on horizontal plane
            cameraForward.Normalize();

            Vector3 bestDirection = cameraForward;

            // Check if there's a wall blocking the killer placement
            Vector3 rayOrigin = playerCamera.transform.position;
            if (Physics.Raycast(rayOrigin, cameraForward, killStopDistance + 0.3f))
            {
                // Wall in front - find an open direction to turn the player
                bestDirection = FindOpenDirection(rayOrigin, cameraForward);

                // Rotate the player camera to face the new direction
                Quaternion targetRotation = Quaternion.LookRotation(bestDirection);
                playerCamera.transform.rotation = targetRotation;
            }

            Vector3 targetPosition = playerCamera.transform.position + bestDirection * killStopDistance;

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
            if (lockFlashlightOnDuringJumpscare)
            {
                PointFlashlightAtKiller();
            }
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

        // Show jumpscare dialogue
        if (showJumpscareDialogue && !string.IsNullOrEmpty(jumpscareDialogueText))
        {
            StartCoroutine(ShowJumpscareDialogue());
        }

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
            Debug.Log($"KillerNPC FACE DEBUG: Using killerFace '{killerFace.name}' at world pos {facePos}, killer root at {transform.position}, camera at {(playerCamera != null ? playerCamera.transform.position.ToString() : "null")}");
        }
        else
        {
            // Estimate face height if no face transform assigned
            facePos = transform.position + Vector3.up * faceHeightOffset;
            Debug.Log($"KillerNPC FACE DEBUG: No killerFace, using root + faceHeightOffset({faceHeightOffset}) = {facePos}");
        }

        // Apply vertical offset (use negative to look lower on the face)
        facePos.y += faceLookVerticalOffset;
        return facePos;
    }

    private Vector3 FindOpenDirection(Vector3 origin, Vector3 blockedForward)
    {
        float checkDistance = killStopDistance + 0.3f;

        // Try directions in order of preference: behind, left, right, then diagonals
        Vector3[] directionsToTry = new Vector3[]
        {
            -blockedForward,                                                    // Behind (180°)
            Quaternion.Euler(0, 90, 0) * blockedForward,                       // Right (90°)
            Quaternion.Euler(0, -90, 0) * blockedForward,                      // Left (-90°)
            Quaternion.Euler(0, 135, 0) * blockedForward,                      // Back-right (135°)
            Quaternion.Euler(0, -135, 0) * blockedForward,                     // Back-left (-135°)
            Quaternion.Euler(0, 45, 0) * blockedForward,                       // Front-right (45°)
            Quaternion.Euler(0, -45, 0) * blockedForward,                      // Front-left (-45°)
        };

        foreach (Vector3 dir in directionsToTry)
        {
            if (!Physics.Raycast(origin, dir, checkDistance))
            {
                Debug.Log($"KillerNPC: Found open direction, rotating player");
                return dir;
            }
        }

        // No open direction found - return behind as last resort
        Debug.LogWarning("KillerNPC: No open direction found, defaulting to behind player");
        return -blockedForward;
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

    private IEnumerator ShowJumpscareDialogue()
    {
        if (jumpscareDialogueDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(jumpscareDialogueDelay);
        }

        // Create canvas
        GameObject canvasObj = new GameObject("JumpscareDialogue");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();

        // Create text
        GameObject textObj = new GameObject("DialogueText");
        textObj.transform.SetParent(canvasObj.transform, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = jumpscareDialogueText;
        tmp.color = jumpscareDialogueColor;
        tmp.fontSize = jumpscareDialogueFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        if (jumpscareDialogueFont != null)
        {
            tmp.font = jumpscareDialogueFont;
        }

        // White outline via TMP material
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.white;

        // Position at lower-center of screen
        RectTransform rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.1f);
        rect.anchorMax = new Vector2(0.9f, 0.35f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        jumpscareDialogueTMP = tmp;
        jumpscareDialogueInstance = canvasObj;
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
            if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                typeName.Contains("Player") || typeName.Contains("Input"))
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
                if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                    typeName.Contains("Player") || typeName.Contains("Input"))
                {
                    script.enabled = !freeze;
                }
            }
        }

        // Also check children (camera rigs, mouse look, etc.)
        scripts = playerTransform.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("MouseLook") || typeName.Contains("CameraController") ||
                typeName.Contains("Look") || typeName.Contains("Input"))
            {
                script.enabled = !freeze;
            }
        }

        Debug.Log($"KillerNPC: Player {(freeze ? "frozen" : "unfrozen")}");
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
    }

    private void LockFlashlightOff()
    {
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null)
        {
            flashlight.enabled = false;

            if (flashlight.spotLight != null)
            {
                flashlight.spotLight.enabled = false;
            }

            Debug.Log("KillerNPC: Flashlight locked off (player control disabled)");
        }
    }

    private void ApplyJumpscareTextShake()
    {
        jumpscareDialogueTMP.ForceMeshUpdate();
        TMP_TextInfo textInfo = jumpscareDialogueTMP.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            Vector3[] vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
            int vertexIndex = charInfo.vertexIndex;

            float offsetX = Mathf.PerlinNoise((Time.unscaledTime * jumpscareDialogueShakeSpeed) + i * 0.3f, 0f) * 2f - 1f;
            float offsetY = Mathf.PerlinNoise(0f, (Time.unscaledTime * jumpscareDialogueShakeSpeed) + i * 0.3f) * 2f - 1f;
            Vector3 offset = new Vector3(offsetX, offsetY, 0f) * jumpscareDialogueShakeIntensity;

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            jumpscareDialogueTMP.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    private void HideCrosshair()
    {
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

        // Clean up jumpscare dialogue
        if (jumpscareDialogueInstance != null)
        {
            Destroy(jumpscareDialogueInstance);
        }

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

        // Endings: unlock it, show the reveal screen, then return to the main menu.
        if (unlocksEnding)
        {
            EndingFlow.Trigger(endingToUnlock);
            return;
        }

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
