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

        // Apply nav agent settings
        navAgent.speed = cfg.roamSpeed;
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
            // Disable the flashlight
            playerFlashlight.SetFlashlightState(false);
            // Prevent player from turning it back on
            playerFlashlight.enabled = false;
            hasDisabledFlashlight = true;
            Debug.Log("CryptKiller: Flashlight permanently disabled");
        }
    }

    private void Update()
    {
        if (currentState == CryptKillerState.Killing)
        {
            return;
        }

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // Update terror radius audio
        UpdateTerrorRadius();

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
        // Always move toward player, but slowly
        float stalkerSpeed = config != null ? config.roamSpeed : 2f;
        navAgent.speed = stalkerSpeed;

        navAgent.SetDestination(playerTransform.position);

        // Face the player
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0;
        if (directionToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // Set walking animation (not running - stalker is slow but persistent)
        if (animator != null && config != null)
        {
            animator.SetBool(config.roamAnimationBool, true);
            animator.SetBool(config.chaseAnimationBool, false);
        }
    }

    #endregion

    #region Roaming State

    private void EnterRoamingState()
    {
        currentState = CryptKillerState.Roaming;
        isWaitingAtRoamPoint = false;
        roamWaitTimer = 0f;

        // Set roam speed and restore normal movement settings
        float roamSpeed = config != null ? config.roamSpeed : 2f;
        navAgent.speed = roamSpeed;
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

        Debug.Log($"CryptKiller: Entered roaming state (speed: {roamSpeed}, isOnNavMesh: {navAgent.isOnNavMesh}, isStopped: {navAgent.isStopped})");
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

    private void EnterChasingState()
    {
        currentState = CryptKillerState.Chasing;
        lostSightTimer = 0f;
        lastKnownPlayerPosition = playerTransform.position;

        // Calculate chase speed based on behavior type
        float baseChaseSpeed = config != null ? config.chaseSpeed : 5f;
        if (behaviorType == KillerBehaviorType.FastChaser)
        {
            float multiplier = config != null ? config.fastChaserSpeedMultiplier : 1.5f;
            navAgent.speed = baseChaseSpeed * multiplier;
        }
        else
        {
            navAgent.speed = baseChaseSpeed;
        }

        // Make chase movement sharp and responsive
        navAgent.acceleration = 1000f;      // Near-instant acceleration
        navAgent.angularSpeed = 1000f;      // Very fast turning
        navAgent.autoBraking = false;       // Don't slow down when approaching
        navAgent.updateRotation = false;    // We handle rotation manually for snappier turning

        // Play spot sound
        if (!hasSpottedPlayerOnce && config != null && config.spotPlayerSound != null)
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
        bool canSeePlayer = vision.CanSeePlayer();

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
