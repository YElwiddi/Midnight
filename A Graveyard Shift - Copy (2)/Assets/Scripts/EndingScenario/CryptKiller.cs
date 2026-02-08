using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Defines the current state of the crypt killer.
/// </summary>
public enum CryptKillerState
{
    Roaming,    // Wandering randomly within the crypt
    Chasing,    // Actively pursuing the player
    Searching,  // Moving to last known player position after losing sight
    Killing     // Game over sequence triggered
}

/// <summary>
/// AI controller for crypt killers with roaming, vision-based detection,
/// and configurable special behaviors based on player stats.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(KillerVision))]
public class CryptKiller : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private CryptKillerConfig config;

    [Header("Terror Radius (Heartbeat)")]
    [Tooltip("Heartbeat sound that plays when player is near")]
    [SerializeField] private AudioClip heartbeatSound;
    [Tooltip("Maximum distance at which heartbeat can be heard")]
    [SerializeField] private float terrorRadius = 20f;
    [Tooltip("Distance at which heartbeat is at maximum volume")]
    [SerializeField] private float terrorMaxVolumeDistance = 3f;
    [Tooltip("Maximum volume of the heartbeat")]
    [SerializeField] [Range(0f, 1f)] private float heartbeatMaxVolume = 1f;

    [Header("Ambient Sound (Gurgling)")]
    [Tooltip("Ambient gurgling sound while roaming")]
    [SerializeField] private AudioClip ambientGurglingSound;
    [Tooltip("Radius at which ambient sound can be heard")]
    [SerializeField] private float ambientSoundRadius = 15f;
    [Tooltip("Volume of the ambient gurgling sound")]
    [SerializeField] [Range(0f, 1f)] private float ambientSoundVolume = 0.5f;

    [Header("Roaming Waypoints")]
    [Tooltip("Waypoints for the killer to roam between. If empty, uses random NavMesh points.")]
    [SerializeField] private Transform[] roamWaypoints;

    [Header("Runtime State (Read Only)")]
    [SerializeField] private CryptKillerState currentState = CryptKillerState.Roaming;
    [SerializeField] private KillerBehaviorType behaviorType;
    [SerializeField] private int currentWaypointIndex = -1;

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera playerCamera;

    // Components
    private NavMeshAgent navAgent;
    private KillerVision vision;
    private Animator animator;
    private AudioSource audioSource;
    private AudioSource heartbeatAudioSource;
    private AudioSource ambientAudioSource;
    private KillerJumpscare jumpscare; // For game over sequence

    // Roaming state
    private Vector3 currentRoamTarget;
    private float roamWaitTimer;
    private bool isWaitingAtRoamPoint;
    private float roamStartTime; // Time when we started moving to current target

    // Chase state
    private Vector3 lastKnownPlayerPosition;
    private float lostSightTimer;
    private bool hasSpottedPlayerOnce;

    // Flashlight reference (for FlashlightDisabler type)
    private SimpleFlashlight playerFlashlight;
    private bool hasDisabledFlashlight;

    // Persistent Stalker flashlight tracking
    private enum StalkerMode { Roaming, Chasing, Searching }
    private StalkerMode stalkerMode = StalkerMode.Roaming;
    private Vector3 stalkerLastKnownPlayerPos;
    private float stalkerSearchTimer;
    private bool stalkerHasSpottedPlayer = false; // For playing spot sound only on vision detection

    // Sanity drain tracking
    private bool isDrainingSanity = false;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        vision = GetComponent<KillerVision>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Setup heartbeat audio source (for terror radius)
        SetupHeartbeatAudio();

        // Setup ambient gurgling audio source
        SetupAmbientAudio();

        jumpscare = GetComponent<KillerJumpscare>();
    }

    private void OnDestroy()
    {
        // Clean up sanity drain source
        if (isDrainingSanity && SanityManager.Instance != null)
        {
            SanityManager.Instance.UnregisterDrainSource();
            isDrainingSanity = false;
        }
    }

    private void Start()
    {
        FindPlayer();

        if (config != null)
        {
            ApplyConfig(config);
        }

        // Start roaming
        EnterRoamingState();
    }

    private void FindPlayer()
    {
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

        playerFlashlight = FindObjectOfType<SimpleFlashlight>();
    }

    private void SetupHeartbeatAudio()
    {
        // Create dedicated audio source for heartbeat
        GameObject heartbeatObj = new GameObject("HeartbeatAudio");
        heartbeatObj.transform.SetParent(transform);
        heartbeatObj.transform.localPosition = Vector3.zero;

        heartbeatAudioSource = heartbeatObj.AddComponent<AudioSource>();
        heartbeatAudioSource.clip = heartbeatSound;
        heartbeatAudioSource.loop = true;
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.spatialBlend = 0f; // 2D sound - plays directly to player
        heartbeatAudioSource.volume = 0f;

        if (heartbeatSound != null)
        {
            heartbeatAudioSource.Play();
        }
    }

    private void SetupAmbientAudio()
    {
        // Create dedicated audio source for ambient gurgling
        GameObject ambientObj = new GameObject("AmbientGurglingAudio");
        ambientObj.transform.SetParent(transform);
        ambientObj.transform.localPosition = Vector3.zero;

        ambientAudioSource = ambientObj.AddComponent<AudioSource>();
        ambientAudioSource.clip = ambientGurglingSound;
        ambientAudioSource.loop = true;
        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.spatialBlend = 1f; // 3D sound - comes from killer position
        ambientAudioSource.rolloffMode = AudioRolloffMode.Linear;
        ambientAudioSource.minDistance = 1f;
        ambientAudioSource.maxDistance = ambientSoundRadius;
        ambientAudioSource.volume = ambientSoundVolume;

        if (ambientGurglingSound != null)
        {
            ambientAudioSource.Play();
        }
    }

    private void UpdateTerrorRadius()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Update heartbeat volume based on distance
        if (heartbeatAudioSource != null && heartbeatSound != null)
        {
            if (distanceToPlayer <= terrorRadius)
            {
                // Calculate volume: louder when closer
                // At terrorMaxVolumeDistance or closer = max volume
                // At terrorRadius = 0 volume
                float t = Mathf.InverseLerp(terrorRadius, terrorMaxVolumeDistance, distanceToPlayer);
                float targetVolume = Mathf.Lerp(0f, heartbeatMaxVolume, t);
                heartbeatAudioSource.volume = targetVolume;

                if (!heartbeatAudioSource.isPlaying)
                {
                    heartbeatAudioSource.Play();
                }
            }
            else
            {
                heartbeatAudioSource.volume = 0f;
            }
        }

        // Update ambient sound radius if changed at runtime
        if (ambientAudioSource != null)
        {
            ambientAudioSource.maxDistance = ambientSoundRadius;
            ambientAudioSource.volume = ambientSoundVolume;
        }
    }

    private void UpdateSanityDrain()
    {
        if (config == null || !config.enableSanityDrain) return;
        if (playerTransform == null || playerCamera == null) return;
        if (SanityManager.Instance == null) return;

        bool shouldDrain = false;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Check terror radius condition
        if (config.drainWhenInTerrorRadius && distanceToPlayer <= terrorRadius)
        {
            shouldDrain = true;
        }

        // Check looking at condition
        if (config.drainWhenLookingAt && IsPlayerLookingAtKiller())
        {
            shouldDrain = true;
        }

        // Apply drain
        if (shouldDrain)
        {
            if (!isDrainingSanity)
            {
                SanityManager.Instance.RegisterDrainSource();
                isDrainingSanity = true;
            }
            SanityManager.Instance.DrainSanityPerSecond(config.sanityDrainPerSecond);
        }
        else if (isDrainingSanity)
        {
            SanityManager.Instance.UnregisterDrainSource();
            isDrainingSanity = false;
        }
    }

    private bool IsPlayerLookingAtKiller()
    {
        if (playerCamera == null || config == null) return false;

        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 killerPos = transform.position + Vector3.up * 1f; // Aim at killer center mass

        // Check distance limit
        float distance = Vector3.Distance(cameraPos, killerPos);
        if (config.lookingAtMaxDistance > 0f && distance > config.lookingAtMaxDistance)
        {
            return false;
        }

        // Get direction from camera to killer
        Vector3 directionToKiller = (killerPos - cameraPos).normalized;

        // Calculate angle between camera forward and direction to killer
        float angle = Vector3.Angle(playerCamera.transform.forward, directionToKiller);

        // Check if within the looking at angle threshold
        if (angle > config.lookingAtAngle)
        {
            return false;
        }

        // Check line of sight (if blocking layers are set)
        if (config.lookingAtBlockingLayers != 0)
        {
            if (Physics.Raycast(cameraPos, directionToKiller, out RaycastHit hit, distance, config.lookingAtBlockingLayers))
            {
                // Something is blocking the view
                return false;
            }
        }

        return true;
    }

    private void StopTerrorSounds()
    {
        if (heartbeatAudioSource != null)
        {
            heartbeatAudioSource.Stop();
        }

        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }
    }

    /// <summary>
    /// Initialize the killer with a configuration asset.
    /// </summary>
    public void Initialize(CryptKillerConfig killerConfig)
    {
        config = killerConfig;
        ApplyConfig(config);

        // Find player references immediately (don't wait for Start)
        FindPlayer();

        // Apply behavior-specific effects immediately
        ApplyBehaviorEffects();

        EnterRoamingState();
    }

    /// <summary>
    /// Set the behavior type directly (used by EndingScenarioManager).
    /// </summary>
    public void SetBehaviorType(KillerBehaviorType type)
    {
        behaviorType = type;
        ApplyBehaviorEffects();
    }

    private void ApplyConfig(CryptKillerConfig cfg)
    {
        behaviorType = cfg.behaviorType;

        // Apply nav agent settings (with penalty multiplier for consistency)
        navAgent.speed = cfg.roamSpeed * speedMultiplier;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;

        // Apply vision settings
        if (vision != null)
        {
            vision.Configure(cfg);
        }

        // Apply terror radius settings
        if (cfg.heartbeatSound != null)
        {
            heartbeatSound = cfg.heartbeatSound;
        }
        terrorRadius = cfg.terrorRadius;
        terrorMaxVolumeDistance = cfg.terrorMaxVolumeDistance;
        heartbeatMaxVolume = cfg.heartbeatMaxVolume;

        // Apply ambient sound settings
        if (cfg.ambientGurglingSound != null)
        {
            ambientGurglingSound = cfg.ambientGurglingSound;
        }
        ambientSoundRadius = cfg.ambientSoundRadius;
        ambientSoundVolume = cfg.ambientSoundVolume;

        // Update audio sources if already created
        if (heartbeatAudioSource != null && heartbeatSound != null)
        {
            heartbeatAudioSource.clip = heartbeatSound;
            if (!heartbeatAudioSource.isPlaying)
            {
                heartbeatAudioSource.Play();
            }
        }

        if (ambientAudioSource != null && ambientGurglingSound != null)
        {
            ambientAudioSource.clip = ambientGurglingSound;
            ambientAudioSource.maxDistance = ambientSoundRadius;
            ambientAudioSource.volume = ambientSoundVolume;
            if (!ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Play();
            }
        }
    }

    private void ApplyBehaviorEffects()
    {
        switch (behaviorType)
        {
            case KillerBehaviorType.FlashlightDisabler:
                DisablePlayerFlashlight();
                break;

            case KillerBehaviorType.PersistentStalker:
                // Stalker always knows where player is - no special init needed
                Debug.Log("CryptKiller: Persistent Stalker initialized - always tracking player");
                break;

            case KillerBehaviorType.FastChaser:
                Debug.Log("CryptKiller: Fast Chaser initialized - increased chase speed");
                break;
        }
    }

    private void DisablePlayerFlashlight()
    {
        if (hasDisabledFlashlight) return;

        if (playerFlashlight == null)
        {
            playerFlashlight = FindObjectOfType<SimpleFlashlight>();
        }

        if (playerFlashlight != null)
        {
            // Get dialogue settings from config
            string dialogueText = config != null ? config.flashlightDisabledDialogue : "The flashlight won't turn on...";
            string speakerName = config != null ? config.flashlightDisabledSpeaker : "";
            float dialogueDuration = config != null ? config.flashlightDialogueDuration : 2f;
            float typewriterSpeed = config != null ? config.flashlightDialogueTypewriterSpeed : 30f;
            float cooldown = config != null ? config.flashlightDialogueCooldown : 5f;

            // Disable the flashlight with dialogue support
            playerFlashlight.DisableByKiller(dialogueText, speakerName, dialogueDuration, typewriterSpeed, cooldown);
            hasDisabledFlashlight = true;
            Debug.Log("CryptKiller: Flashlight permanently disabled - dialogue will show on toggle attempt");
        }
    }

    private void Update()
    {
        if (currentState == CryptKillerState.Killing)
        {
            return;
        }

        if (playerTransform == null || playerCamera == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // Update terror radius audio
        UpdateTerrorRadius();

        // Update sanity drain
        UpdateSanityDrain();

        // Check for kill distance
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= (config != null ? config.killDistance : 1.5f))
        {
            TriggerGameOver();
            return;
        }

        // Persistent Stalker always knows where player is (skips vision)
        if (behaviorType == KillerBehaviorType.PersistentStalker)
        {
            UpdatePersistentStalker();
            return;
        }

        // Normal behavior: vision-based detection
        switch (currentState)
        {
            case CryptKillerState.Roaming:
                UpdateRoamingState();
                break;

            case CryptKillerState.Chasing:
                UpdateChasingState();
                break;

            case CryptKillerState.Searching:
                UpdateSearchingState();
                break;
        }
    }

    #region Persistent Stalker Behavior

    private void UpdatePersistentStalker()
    {
        // Check if flashlight is on
        bool flashlightOn = playerFlashlight != null && playerFlashlight.IsFlashlightOn();

        // If direct pursuit is enabled (from 3 incorrect digs), always chase
        if (forceDirectPursuit)
        {
            UpdatePersistentStalkerChase();
            return;
        }

        if (flashlightOn)
        {
            // Flashlight ON: Chase the player directly with running animation
            UpdatePersistentStalkerChase();
        }
        else if (stalkerMode == StalkerMode.Chasing)
        {
            // Flashlight just turned OFF while chasing: enter searching mode
            EnterPersistentStalkerSearch();
        }
        else if (stalkerMode == StalkerMode.Searching)
        {
            // Continue searching until timer expires
            UpdatePersistentStalkerSearch();
        }
        else
        {
            // Flashlight OFF and not searching: roam normally
            UpdatePersistentStalkerRoam();
        }
    }

    private void UpdatePersistentStalkerChase()
    {
        // If we just started chasing, set up chase mode
        if (stalkerMode != StalkerMode.Chasing)
        {
            stalkerMode = StalkerMode.Chasing;

            navAgent.acceleration = 1000f;
            navAgent.angularSpeed = 1000f;
            navAgent.autoBraking = false;
            navAgent.updateRotation = false;
            navAgent.isStopped = false;

            // Set running animation
            if (animator != null && config != null)
            {
                animator.SetBool(config.roamAnimationBool, false);
                animator.SetBool(config.chaseAnimationBool, true);
            }

            Debug.Log($"CryptKiller: Persistent Stalker - chasing player! (forceDirectPursuit={forceDirectPursuit})");
        }

        // Always update chase speed (in case speed multiplier changed from incorrect digs)
        float chaseSpeed = config != null ? config.chaseSpeed : 5f;
        navAgent.speed = chaseSpeed * speedMultiplier;

        // Constantly move toward player and remember their position
        stalkerLastKnownPlayerPos = playerTransform.position;
        navAgent.SetDestination(playerTransform.position);

        // Snap rotation toward player
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0;
        if (directionToPlayer.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(directionToPlayer);
        }
    }

    private void EnterPersistentStalkerSearch()
    {
        stalkerMode = StalkerMode.Searching;
        stalkerSearchTimer = 0f;

        // Continue toward last known position at chase speed (still running)
        navAgent.SetDestination(stalkerLastKnownPlayerPos);

        Debug.Log("CryptKiller: Persistent Stalker - Flashlight OFF, searching last known position");
    }

    private void UpdatePersistentStalkerSearch()
    {
        // If flashlight turns back on during search, go back to chasing
        bool flashlightOn = playerFlashlight != null && playerFlashlight.IsFlashlightOn();
        if (flashlightOn)
        {
            UpdatePersistentStalkerChase();
            return;
        }

        // If we can see the player while searching, resume chase
        if (vision.CanSeePlayer())
        {
            // Re-spotted player through vision - play spot sound
            if (!stalkerHasSpottedPlayer)
            {
                stalkerHasSpottedPlayer = true;
                if (config != null && config.spotPlayerSound != null)
                {
                    audioSource.PlayOneShot(config.spotPlayerSound, config.spotSoundVolume);
                }
                Debug.Log("CryptKiller: Persistent Stalker re-spotted player through vision!");
            }

            stalkerLastKnownPlayerPos = playerTransform.position;
            stalkerSearchTimer = 0f; // Reset timer since we have visual
            UpdatePersistentStalkerChase();
            return;
        }

        // Increment search timer
        stalkerSearchTimer += Time.deltaTime;
        float lostDuration = config != null ? config.lostSightDuration : 3f;

        // Continue toward last known position
        navAgent.SetDestination(stalkerLastKnownPlayerPos);

        // Snap rotation toward movement direction
        if (navAgent.velocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(navAgent.velocity.normalized);
        }

        // Check if we've reached the last known position or timer expired
        float distanceToTarget = Vector3.Distance(transform.position, stalkerLastKnownPlayerPos);
        bool reachedTarget = distanceToTarget <= 2f || !navAgent.hasPath;
        bool timerExpired = stalkerSearchTimer >= lostDuration;

        if (reachedTarget || timerExpired)
        {
            // Give up and return to roaming
            EnterPersistentStalkerRoam();
        }
    }

    private void EnterPersistentStalkerRoam()
    {
        stalkerMode = StalkerMode.Roaming;

        // Reset spot sound flag so it plays again when player is re-spotted
        stalkerHasSpottedPlayer = false;

        // Set roam speed (with penalty multiplier)
        float roamSpeed = config != null ? config.roamSpeed : 2f;
        navAgent.speed = roamSpeed * speedMultiplier;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = true;
        navAgent.isStopped = false;

        // Set walking animation
        if (animator != null && config != null)
        {
            animator.SetBool(config.roamAnimationBool, true);
            animator.SetBool(config.chaseAnimationBool, false);
        }

        // Pick a new roam target
        isWaitingAtRoamPoint = false;
        PickNewRoamTarget();

        Debug.Log("CryptKiller: Persistent Stalker - Search complete, roaming normally");
    }

    private void UpdatePersistentStalkerRoam()
    {
        // Check if we can see the player (chase even with flashlight off)
        if (vision.CanSeePlayer())
        {
            // Spotted player through vision - play spot sound
            if (!stalkerHasSpottedPlayer)
            {
                stalkerHasSpottedPlayer = true;
                if (config != null && config.spotPlayerSound != null)
                {
                    audioSource.PlayOneShot(config.spotPlayerSound, config.spotSoundVolume);
                }
                Debug.Log("CryptKiller: Persistent Stalker spotted player through vision!");
            }

            // Start chasing
            stalkerLastKnownPlayerPos = playerTransform.position;
            UpdatePersistentStalkerChase();
            return;
        }

        // Handle waiting at roam point
        if (isWaitingAtRoamPoint)
        {
            roamWaitTimer -= Time.deltaTime;
            if (roamWaitTimer <= 0f)
            {
                isWaitingAtRoamPoint = false;
                PickNewRoamTarget();
            }
            return;
        }

        // Check if we've reached the roam target
        float distanceToTarget = Vector3.Distance(transform.position, currentRoamTarget);
        float reachedDistance = config != null ? config.roamPointReachedDistance : 1f;

        // Give the agent time to start moving before checking velocity
        float timeSinceRoamStart = Time.time - roamStartTime;
        bool hasHadTimeToMove = timeSinceRoamStart > 0.5f;

        bool reachedByDistance = distanceToTarget <= reachedDistance;
        bool stuckWithNoPath = hasHadTimeToMove && !navAgent.hasPath && !navAgent.pathPending;
        bool stuckNotMoving = hasHadTimeToMove && navAgent.velocity.sqrMagnitude < 0.01f && !navAgent.pathPending;

        if (reachedByDistance || stuckWithNoPath || stuckNotMoving)
        {
            // Start waiting at this roam point
            isWaitingAtRoamPoint = true;
            roamWaitTimer = config != null ? config.roamWaitTime : 2f;
            navAgent.ResetPath();
        }
    }

    #endregion

    #region Roaming State

    private void EnterRoamingState()
    {
        currentState = CryptKillerState.Roaming;
        isWaitingAtRoamPoint = false;
        roamWaitTimer = 0f;

        // Set roam speed and restore normal movement settings (with penalty multiplier)
        float roamSpeed = config != null ? config.roamSpeed : 2f;
        navAgent.speed = roamSpeed * speedMultiplier;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = true;
        navAgent.isStopped = false;

        // Set roam animation
        if (animator != null && config != null)
        {
            animator.SetBool(config.roamAnimationBool, true);
            animator.SetBool(config.chaseAnimationBool, false);
        }

        // Pick initial roam target
        PickNewRoamTarget();

        Debug.Log($"CryptKiller: Entered roaming state (speed: {roamSpeed * speedMultiplier}, multiplier: {speedMultiplier}x, isOnNavMesh: {navAgent.isOnNavMesh}, isStopped: {navAgent.isStopped})");
    }

    private void UpdateRoamingState()
    {
        // Check for player visibility
        if (vision.CanSeePlayer())
        {
            EnterChasingState();
            return;
        }

        // Handle waiting at roam point
        if (isWaitingAtRoamPoint)
        {
            roamWaitTimer -= Time.deltaTime;
            if (roamWaitTimer <= 0f)
            {
                isWaitingAtRoamPoint = false;
                PickNewRoamTarget();
            }
            return;
        }

        // Check if we've reached the roam target
        float distanceToTarget = Vector3.Distance(transform.position, currentRoamTarget);
        float reachedDistance = config != null ? config.roamPointReachedDistance : 1f;

        // Give the agent time to start moving before checking velocity (0.5 seconds grace period)
        float timeSinceRoamStart = Time.time - roamStartTime;
        bool hasHadTimeToMove = timeSinceRoamStart > 0.5f;

        // Check if reached destination
        bool reachedByDistance = distanceToTarget <= reachedDistance;
        bool stuckWithNoPath = hasHadTimeToMove && !navAgent.hasPath && !navAgent.pathPending;
        bool stuckNotMoving = hasHadTimeToMove && navAgent.velocity.sqrMagnitude < 0.01f && !navAgent.pathPending;

        if (reachedByDistance || stuckWithNoPath || stuckNotMoving)
        {
            Debug.Log($"CryptKiller: Reached roam point (byDistance: {reachedByDistance}, noPath: {stuckWithNoPath}, notMoving: {stuckNotMoving})");
            // Start waiting
            isWaitingAtRoamPoint = true;
            roamWaitTimer = config != null ? config.roamWaitTime : 2f;
            navAgent.ResetPath();
        }
    }

    private void PickNewRoamTarget()
    {
        // Check if NavMeshAgent is on a valid NavMesh
        if (!navAgent.isOnNavMesh)
        {
            Debug.LogError($"CryptKiller: NavMeshAgent is NOT on NavMesh! Position: {transform.position}");
            return;
        }

        // Use waypoints if available
        if (roamWaypoints != null && roamWaypoints.Length > 0)
        {
            PickRandomWaypoint();
            return;
        }

        // Fallback: random NavMesh points
        PickRandomNavMeshPoint();
    }

    private void PickRandomWaypoint()
    {
        if (roamWaypoints.Length == 1)
        {
            // Only one waypoint, just go there
            currentWaypointIndex = 0;
        }
        else
        {
            // Pick a random waypoint that's different from the current one
            int newIndex;
            int attempts = 0;
            do
            {
                newIndex = Random.Range(0, roamWaypoints.Length);
                attempts++;
            } while (newIndex == currentWaypointIndex && attempts < 10);

            currentWaypointIndex = newIndex;
        }

        Transform waypoint = roamWaypoints[currentWaypointIndex];
        if (waypoint == null)
        {
            Debug.LogWarning($"CryptKiller: Waypoint at index {currentWaypointIndex} is null!");
            isWaitingAtRoamPoint = true;
            roamWaitTimer = 1f;
            return;
        }

        currentRoamTarget = waypoint.position;
        navAgent.isStopped = false;
        navAgent.SetDestination(currentRoamTarget);
        roamStartTime = Time.time;
        Debug.Log($"CryptKiller: Moving to waypoint {currentWaypointIndex} ({waypoint.name}) at {currentRoamTarget}");
    }

    private void PickRandomNavMeshPoint()
    {
        float minDistance = config != null ? config.minRoamDistance : 5f;
        float maxDistance = config != null ? config.maxRoamDistance : 15f;

        // Try to find a valid random point on the NavMesh
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * Random.Range(minDistance, maxDistance);
            randomDirection.y = 0;
            Vector3 randomPoint = transform.position + randomDirection;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                currentRoamTarget = hit.position;
                navAgent.isStopped = false;
                navAgent.SetDestination(currentRoamTarget);
                roamStartTime = Time.time;
                Debug.Log($"CryptKiller: New random roam target at {currentRoamTarget}, distance: {Vector3.Distance(transform.position, currentRoamTarget):F1}");
                return;
            }
        }

        // Fallback: stay in place briefly
        Debug.LogWarning("CryptKiller: Could not find valid roam point after 30 attempts");
        isWaitingAtRoamPoint = true;
        roamWaitTimer = 1f;
    }

    #endregion

    #region Chasing State

    private void EnterChasingState(bool skipSpotSound = false)
    {
        currentState = CryptKillerState.Chasing;
        lostSightTimer = 0f;
        lastKnownPlayerPosition = playerTransform.position;

        // Calculate chase speed based on behavior type and speed multiplier
        float baseChaseSpeed = config != null ? config.chaseSpeed : 5f;
        if (behaviorType == KillerBehaviorType.FastChaser)
        {
            float multiplier = config != null ? config.fastChaserSpeedMultiplier : 1.5f;
            navAgent.speed = baseChaseSpeed * multiplier * speedMultiplier;
        }
        else
        {
            navAgent.speed = baseChaseSpeed * speedMultiplier;
        }

        // Make chase movement sharp and responsive
        navAgent.acceleration = 1000f;      // Near-instant acceleration
        navAgent.angularSpeed = 1000f;      // Very fast turning
        navAgent.autoBraking = false;       // Don't slow down when approaching
        navAgent.updateRotation = false;    // We handle rotation manually for snappier turning

        // Play spot sound (unless skipped, e.g., for direct pursuit penalty)
        if (!skipSpotSound && !hasSpottedPlayerOnce && config != null && config.spotPlayerSound != null)
        {
            audioSource.PlayOneShot(config.spotPlayerSound, config.spotSoundVolume);
        }
        hasSpottedPlayerOnce = true;

        // Set chase animation
        if (animator != null && config != null)
        {
            animator.SetBool(config.roamAnimationBool, false);
            animator.SetBool(config.chaseAnimationBool, true);

            if (!string.IsNullOrEmpty(config.spotPlayerTrigger))
            {
                animator.SetTrigger(config.spotPlayerTrigger);
            }
        }

        Debug.Log($"CryptKiller: Player spotted! Entering chase state (speed: {navAgent.speed})");
    }

    private void UpdateChasingState()
    {
        // If direct pursuit is enabled, always know where player is
        bool canSeePlayer = forceDirectPursuit || vision.CanSeePlayer();

        if (canSeePlayer)
        {
            // Reset lost sight timer
            lostSightTimer = 0f;
            lastKnownPlayerPosition = playerTransform.position;

            // Chase player - update destination every frame for tight tracking
            navAgent.SetDestination(playerTransform.position);

            // Snap rotation instantly toward player (no lerp = no sliding)
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(directionToPlayer);
            }
        }
        else
        {
            // Lost sight - start timer
            lostSightTimer += Time.deltaTime;

            // Continue toward last known position
            navAgent.SetDestination(lastKnownPlayerPosition);

            // Still snap rotation toward movement direction
            if (navAgent.velocity.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(navAgent.velocity.normalized);
            }

            float lostDuration = config != null ? config.lostSightDuration : 3f;
            if (lostSightTimer >= lostDuration)
            {
                // Give up and return to roaming
                EnterSearchingState();
            }
        }
    }

    #endregion

    #region Searching State

    private void EnterSearchingState()
    {
        currentState = CryptKillerState.Searching;

        // Reset spot sound flag so it plays again when player is re-spotted
        hasSpottedPlayerOnce = false;

        // Move to last known position at chase speed
        navAgent.SetDestination(lastKnownPlayerPosition);

        Debug.Log("CryptKiller: Lost sight of player, searching last known position");
    }

    private void UpdateSearchingState()
    {
        // Check if we can see the player again
        if (vision.CanSeePlayer())
        {
            EnterChasingState();
            return;
        }

        // Check if we've reached the last known position
        float distanceToTarget = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        if (distanceToTarget <= 2f || !navAgent.hasPath)
        {
            // Couldn't find player, return to roaming
            EnterRoamingState();
        }
    }

    #endregion

    #region Game Over

    private void TriggerGameOver()
    {
        if (currentState == CryptKillerState.Killing)
        {
            return;
        }

        currentState = CryptKillerState.Killing;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        // Stop terror radius sounds
        StopTerrorSounds();

        // Stop sanity drain
        if (isDrainingSanity && SanityManager.Instance != null)
        {
            SanityManager.Instance.UnregisterDrainSource();
            isDrainingSanity = false;
        }

        Debug.Log("CryptKiller: Kill distance reached - triggering game over");

        // Use KillerJumpscare for the game over sequence
        if (jumpscare != null)
        {
            jumpscare.TriggerJumpscare();
        }
        else
        {
            // Fallback: simple game over
            Debug.LogWarning("CryptKiller: No KillerJumpscare component found!");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current state of the killer.
    /// </summary>
    public CryptKillerState GetCurrentState() => currentState;

    /// <summary>
    /// Gets the behavior type of this killer.
    /// </summary>
    public KillerBehaviorType GetBehaviorType() => behaviorType;

    /// <summary>
    /// Force the killer to immediately chase the player.
    /// </summary>
    public void ForceChase()
    {
        if (currentState != CryptKillerState.Killing)
        {
            EnterChasingState();
        }
    }

    /// <summary>
    /// Set the roaming waypoints at runtime.
    /// </summary>
    public void SetRoamWaypoints(Transform[] waypoints)
    {
        roamWaypoints = waypoints;
        currentWaypointIndex = -1;
        Debug.Log($"CryptKiller: {waypoints.Length} roaming waypoints assigned");
    }

    // Speed modifier from dirt pile penalties
    private float speedMultiplier = 1f;
    private bool forceDirectPursuit = false;

    /// <summary>
    /// Applies a speed multiplier to the killer (stacks multiplicatively).
    /// </summary>
    /// <param name="multiplier">Speed multiplier (e.g., 1.2 for 20% faster)</param>
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier *= multiplier;
        Debug.Log($"CryptKiller: Speed multiplier applied. Total multiplier: {speedMultiplier:F2}x");
    }

    /// <summary>
    /// Forces the killer to always know where the player is (like Persistent Stalker).
    /// </summary>
    public void EnableDirectPursuit()
    {
        forceDirectPursuit = true;
        Debug.Log("CryptKiller: Direct pursuit enabled - killer always knows player location!");

        // Immediately start chasing (silently - no spot sound for penalty activation)
        if (currentState != CryptKillerState.Killing)
        {
            EnterChasingState(skipSpotSound: true);
        }
    }

    /// <summary>
    /// Gets the current speed multiplier.
    /// </summary>
    public float GetSpeedMultiplier() => speedMultiplier;

    /// <summary>
    /// Returns true if direct pursuit is enabled.
    /// </summary>
    public bool IsDirectPursuitEnabled() => forceDirectPursuit;

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        // Draw current roam target
        if (currentState == CryptKillerState.Roaming)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(currentRoamTarget, 0.5f);
            Gizmos.DrawLine(transform.position, currentRoamTarget);
        }

        // Draw last known player position when searching
        if (currentState == CryptKillerState.Searching || currentState == CryptKillerState.Chasing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastKnownPlayerPosition, 0.5f);
        }

        // Draw kill distance
        Gizmos.color = Color.red;
        float killDist = config != null ? config.killDistance : 1.5f;
        Gizmos.DrawWireSphere(transform.position, killDist);

        // Draw terror radius (heartbeat)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, terrorRadius);

        // Draw terror max volume distance
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, terrorMaxVolumeDistance);

        // Draw ambient sound radius
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, ambientSoundRadius);

        // Draw waypoints
        if (roamWaypoints != null && roamWaypoints.Length > 0)
        {
            for (int i = 0; i < roamWaypoints.Length; i++)
            {
                if (roamWaypoints[i] == null) continue;

                // Highlight current waypoint
                if (i == currentWaypointIndex)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(roamWaypoints[i].position, 0.8f);
                }
                else
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(roamWaypoints[i].position, 0.5f);
                }

                // Draw label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(roamWaypoints[i].position + Vector3.up, $"WP {i}");
                #endif
            }
        }
    }

    #endregion
}
