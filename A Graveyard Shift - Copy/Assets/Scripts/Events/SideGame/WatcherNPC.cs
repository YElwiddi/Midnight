using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC component for watcher-type side game events.
/// Stares at the player, drains graveyard protection, and retreats when flashlight is shined on them.
/// </summary>
public class WatcherNPC : MonoBehaviour
{
    #region Events
    /// <summary>Fired when this watcher starts staring at the player.</summary>
    public event Action OnStartedStaring;

    /// <summary>Fired when this watcher stops staring (retreat or despawn).</summary>
    public event Action OnStoppedStaring;

    /// <summary>Fired when this watcher retreats due to flashlight.</summary>
    public event Action OnRetreated;

    /// <summary>Fired when this watcher's event is fully complete (despawned).</summary>
    public event Action OnEventCompleted;
    #endregion

    #region Private Fields
    private SideGameEvent eventConfig;
    private Transform playerTransform;
    private Camera playerCamera;
    private SimpleFlashlight playerFlashlight;
    private Animator animator;
    private AudioSource audioSource;
    private NavMeshAgent navAgent;

    // State tracking
    private bool isInitialized = false;
    private bool isEntering = false;
    private bool isStaring = false;
    private bool isRetreating = false;
    private bool isComplete = false;
    private float flashlightExposureTime = 0f;
    private float eventStartTime;
    private Vector3 retreatDirection;

    // Entrance settings
    private bool useEntranceMovement;
    private Vector3 entranceTargetPosition;
    private float entranceSpeed;
    private bool waitForEntranceBeforeStaring;
    private string entranceAnimTrigger;

    // Cached values
    private string eventName;
    private float drainPerSecond;
    private float maxStareDistance;
    private bool requireLineOfSight;
    private LayerMask lineOfSightLayers;
    private float flashlightAngle;
    private float flashlightMaxDist;
    private float flashlightExposureRequired;
    private bool requireFlashlightOn;
    private bool flashlightIgnoreObstacles;
    private string retreatTrigger;
    private float retreatSpeed;
    private float retreatDistanceMax;
    private bool fadeOnRetreat;
    private float fadeDuration;
    private float trackingSpeed;
    private string staringAnimBool;
    private float timeout;

    // Stare mode settings
    private WatcherStareMode stareMode;
    private Vector3 stareDirectionWorld;
    private string stareTargetName;
    private Transform stareTarget;

    // Retreat mode settings
    private WatcherRetreatMode retreatMode;
    private Vector3 retreatDirectionWorld;
    private string retreatTargetName;
    private Transform retreatTarget;
    private bool useRetreatAnimation;
    private Vector3 retreatStartPosition;
    private float distanceRetreated;

    // Fade tracking
    private Renderer[] renderers;
    private float fadeProgress = 0f;
    private Color[] originalColors;

    // Idle sound tracking
    private AudioClip idleSound;
    private float idleSoundVolume;
    private float idleSoundIntervalMin;
    private float idleSoundIntervalMax;
    private float nextIdleSoundTime;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (!isInitialized || isComplete) return;

        // Check timeout
        if (timeout > 0 && Time.time - eventStartTime >= timeout)
        {
            Debug.Log($"WatcherNPC '{eventName}': Event timed out, despawning");
            CompleteEvent();
            return;
        }

        if (isRetreating)
        {
            UpdateRetreat();
        }
        else if (isEntering)
        {
            UpdateEntrance();
            UpdateIdleSound();
        }
        else
        {
            UpdateStaring();
            UpdateFlashlightDetection();
            UpdateIdleSound();
        }
    }

    private void UpdateIdleSound()
    {
        if (idleSound == null || audioSource == null) return;

        if (Time.time >= nextIdleSoundTime)
        {
            audioSource.PlayOneShot(idleSound, idleSoundVolume);
            nextIdleSoundTime = Time.time + UnityEngine.Random.Range(idleSoundIntervalMin, idleSoundIntervalMax);
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes the watcher with configuration from a SideGameEvent.
    /// </summary>
    public void Initialize(SideGameEvent config)
    {
        if (config == null)
        {
            Debug.LogError("WatcherNPC: Cannot initialize with null config!");
            return;
        }

        eventConfig = config;
        eventName = config.eventName;

        // Cache config values
        drainPerSecond = config.protectionDrainPerSecond;
        maxStareDistance = config.maxStareDistance;
        requireLineOfSight = config.requireLineOfSight;
        lineOfSightLayers = config.lineOfSightBlockingLayers;
        flashlightAngle = config.flashlightDetectionAngle;
        flashlightMaxDist = config.flashlightMaxDistance;
        flashlightExposureRequired = config.flashlightExposureRequired;
        requireFlashlightOn = config.requireFlashlightOn;
        flashlightIgnoreObstacles = config.flashlightIgnoreObstacles;
        retreatTrigger = config.retreatAnimationTrigger;
        retreatSpeed = config.retreatSpeed;
        retreatDistanceMax = config.retreatDistance;
        fadeOnRetreat = config.fadeOnRetreat;
        fadeDuration = config.fadeDuration;
        trackingSpeed = config.playerTrackingSpeed;
        staringAnimBool = config.staringAnimationBool;
        timeout = config.eventTimeout;

        // Stare mode settings
        stareMode = config.stareMode;
        stareDirectionWorld = config.stareDirection.normalized;
        stareTargetName = config.stareTargetName;

        // Retreat mode settings
        retreatMode = config.retreatMode;
        retreatDirectionWorld = config.retreatDirection.normalized;
        retreatTargetName = config.retreatTargetName;
        useRetreatAnimation = config.useRetreatAnimation;

        // Entrance settings
        useEntranceMovement = config.useEntranceMovement;
        entranceSpeed = config.entranceSpeed;
        waitForEntranceBeforeStaring = config.waitForEntranceBeforeStaring;
        entranceAnimTrigger = config.entranceAnimationTrigger;

        // Idle sound settings
        idleSound = config.idleSound;
        idleSoundVolume = config.idleSoundVolume;
        idleSoundIntervalMin = config.idleSoundIntervalMin;
        idleSoundIntervalMax = config.idleSoundIntervalMax;

        // Find player references
        FindPlayerReferences();

        // Find stare target if needed
        if (stareMode == WatcherStareMode.FacePoint && !string.IsNullOrEmpty(stareTargetName))
        {
            GameObject stareTargetObj = GameObject.Find(stareTargetName);
            if (stareTargetObj != null)
            {
                stareTarget = stareTargetObj.transform;
            }
            else
            {
                Debug.LogWarning($"WatcherNPC '{eventName}': Stare target '{stareTargetName}' not found!");
            }
        }

        // Find retreat target if needed
        if (retreatMode == WatcherRetreatMode.TowardPoint && !string.IsNullOrEmpty(retreatTargetName))
        {
            GameObject retreatTargetObj = GameObject.Find(retreatTargetName);
            if (retreatTargetObj != null)
            {
                retreatTarget = retreatTargetObj.transform;
            }
            else
            {
                Debug.LogWarning($"WatcherNPC '{eventName}': Retreat target '{retreatTargetName}' not found!");
            }
        }

        // Get components
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }

        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false; // Disable for now, watchers stand still
        }

        // Cache renderers for fading
        renderers = GetComponentsInChildren<Renderer>();
        CacheOriginalColors();

        // Play spawn sound
        if (config.spawnSound != null)
        {
            audioSource.PlayOneShot(config.spawnSound, config.spawnSoundVolume);
        }

        // Start ambient sound loop
        if (config.ambientSound != null)
        {
            audioSource.clip = config.ambientSound;
            audioSource.volume = config.ambientSoundVolume;
            audioSource.loop = true;
            audioSource.Play();
        }

        eventStartTime = Time.time;
        isInitialized = true;

        // Initialize idle sound timer
        if (idleSound != null)
        {
            if (config.playIdleSoundOnSpawn)
            {
                // Play immediately and schedule next
                audioSource.PlayOneShot(idleSound, idleSoundVolume);
                nextIdleSoundTime = Time.time + UnityEngine.Random.Range(idleSoundIntervalMin, idleSoundIntervalMax);
            }
            else
            {
                // Schedule first play
                nextIdleSoundTime = Time.time + UnityEngine.Random.Range(idleSoundIntervalMin, idleSoundIntervalMax);
            }
        }

        // Start entrance movement or staring
        if (useEntranceMovement)
        {
            StartEntrance();
        }
        else
        {
            StartStaring();
        }

        Debug.Log($"WatcherNPC '{eventName}': Initialized and active (entrance={useEntranceMovement})");
    }

    /// <summary>
    /// Sets the target position for entrance movement. Call this after Initialize if using entrance movement.
    /// The watcher will move from its current position to this target.
    /// </summary>
    public void SetEntranceTarget(Vector3 targetPosition)
    {
        entranceTargetPosition = targetPosition;
        Debug.Log($"WatcherNPC '{eventName}': Entrance target set to {targetPosition}");
    }

    private void FindPlayerReferences()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerFlashlight = playerObj.GetComponentInChildren<SimpleFlashlight>();
            if (playerFlashlight == null)
            {
                playerFlashlight = FindFirstObjectByType<SimpleFlashlight>();
            }
        }

        // Find camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }

        if (playerTransform == null)
        {
            Debug.LogError("WatcherNPC: Player not found!");
        }
    }

    private void CacheOriginalColors()
    {
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.color;
            }
            else
            {
                originalColors[i] = Color.white;
            }
        }
    }
    #endregion

    #region Entrance Logic
    private void StartEntrance()
    {
        if (isEntering) return;

        isEntering = true;

        // Play entrance animation if configured
        if (animator != null && !string.IsNullOrEmpty(entranceAnimTrigger))
        {
            animator.SetTrigger(entranceAnimTrigger);
        }

        // If not waiting for entrance, start staring now (will drain while moving)
        if (!waitForEntranceBeforeStaring)
        {
            StartStaring();
        }

        Debug.Log($"WatcherNPC '{eventName}': Starting entrance movement to {entranceTargetPosition}");
    }

    private void UpdateEntrance()
    {
        if (!isEntering) return;

        // Move toward entrance target
        Vector3 direction = (entranceTargetPosition - transform.position);
        float distanceRemaining = direction.magnitude;

        if (distanceRemaining <= 0.05f)
        {
            // Arrived at target
            transform.position = entranceTargetPosition;
            CompleteEntrance();
            return;
        }

        // Move toward target
        float moveDistance = entranceSpeed * Time.deltaTime;
        if (moveDistance >= distanceRemaining)
        {
            transform.position = entranceTargetPosition;
            CompleteEntrance();
        }
        else
        {
            transform.position += direction.normalized * moveDistance;
        }

        // Update facing while entering (if not UseSpawnRotation)
        if (stareMode != WatcherStareMode.UseSpawnRotation)
        {
            UpdateFacing();
        }
    }

    private void CompleteEntrance()
    {
        isEntering = false;

        Debug.Log($"WatcherNPC '{eventName}': Entrance complete, now staring");

        // Start staring if we were waiting
        if (waitForEntranceBeforeStaring && !isStaring)
        {
            StartStaring();
        }
    }
    #endregion

    #region Staring Logic
    private void StartStaring()
    {
        if (isStaring) return;

        isStaring = true;

        // Set animation
        if (animator != null && !string.IsNullOrEmpty(staringAnimBool))
        {
            animator.SetBool(staringAnimBool, true);
        }

        // Register as drain source
        if (GraveyardProtectionManager.Instance != null)
        {
            GraveyardProtectionManager.Instance.RegisterDrainSource();
        }

        OnStartedStaring?.Invoke();

        // Notify event system
        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.WatcherStartedStaring(eventName);
        }

        Debug.Log($"WatcherNPC '{eventName}': Started staring at player");
    }

    private void StopStaring()
    {
        if (!isStaring) return;

        isStaring = false;

        // Clear animation
        if (animator != null && !string.IsNullOrEmpty(staringAnimBool))
        {
            animator.SetBool(staringAnimBool, false);
        }

        // Unregister as drain source
        if (GraveyardProtectionManager.Instance != null)
        {
            GraveyardProtectionManager.Instance.UnregisterDrainSource();
        }

        OnStoppedStaring?.Invoke();

        // Notify event system
        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.WatcherStoppedStaring(eventName);
        }

        Debug.Log($"WatcherNPC '{eventName}': Stopped staring");
    }

    private void UpdateStaring()
    {
        if (playerTransform == null || !isStaring) return;

        // Update facing based on stare mode
        UpdateFacing();

        // Check if player is within stare distance
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > maxStareDistance)
        {
            // Too far, no drain
            return;
        }

        // Check line of sight if required
        if (requireLineOfSight && !HasLineOfSightToPlayer())
        {
            // Blocked, no drain
            return;
        }

        // Drain protection
        if (GraveyardProtectionManager.Instance != null)
        {
            GraveyardProtectionManager.Instance.DrainProtectionPerSecond(drainPerSecond);
        }
    }

    private void UpdateFacing()
    {
        Vector3 targetDirection = Vector3.zero;

        switch (stareMode)
        {
            case WatcherStareMode.FacePlayer:
                if (playerTransform != null)
                {
                    targetDirection = playerTransform.position - transform.position;
                }
                break;

            case WatcherStareMode.FaceDirection:
                targetDirection = stareDirectionWorld;
                break;

            case WatcherStareMode.FacePoint:
                if (stareTarget != null)
                {
                    targetDirection = stareTarget.position - transform.position;
                }
                break;

            case WatcherStareMode.UseSpawnRotation:
                // Don't rotate at all
                return;
        }

        // Keep upright (zero out Y)
        targetDirection.y = 0;

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                trackingSpeed * Time.deltaTime
            );
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (playerTransform == null) return false;

        Vector3 startPos = transform.position + Vector3.up * 1.5f; // Eye height
        Vector3 endPos = playerTransform.position + Vector3.up * 1.5f;
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        if (Physics.Raycast(startPos, direction.normalized, out RaycastHit hit, distance, lineOfSightLayers))
        {
            // Check if we hit the player or something else
            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
            {
                return true;
            }
            // Hit something else - blocked
            return false;
        }

        // No hit means clear line of sight
        return true;
    }
    #endregion

    #region Flashlight Detection
    private void UpdateFlashlightDetection()
    {
        if (playerFlashlight == null || playerCamera == null) return;

        bool isFlashlightOnWatcher = IsFlashlightAimedAtMe();

        if (isFlashlightOnWatcher)
        {
            flashlightExposureTime += Time.deltaTime;

            if (flashlightExposureTime >= flashlightExposureRequired)
            {
                Debug.Log($"WatcherNPC '{eventName}': Flashlight exposure threshold reached, retreating!");
                StartRetreat();
            }
        }
        else
        {
            // Reset exposure time if flashlight moves away
            flashlightExposureTime = Mathf.Max(0, flashlightExposureTime - Time.deltaTime * 2f);
        }
    }

    private bool IsFlashlightAimedAtMe()
    {
        if (playerFlashlight == null) return false;

        // Check if flashlight is on (if required)
        if (requireFlashlightOn && !playerFlashlight.IsFlashlightOn())
        {
            return false;
        }

        // Get flashlight direction (from camera since flashlight follows camera)
        if (playerCamera == null) return false;

        Vector3 flashlightPos = playerCamera.transform.position;
        Vector3 flashlightDir = playerCamera.transform.forward;
        Vector3 toWatcher = transform.position + Vector3.up * 1.2f - flashlightPos; // Aim at chest height

        float distance = toWatcher.magnitude;

        // Check distance
        if (distance > flashlightMaxDist)
        {
            return false;
        }

        // Check angle
        float angle = Vector3.Angle(flashlightDir, toWatcher.normalized);
        if (angle > flashlightAngle)
        {
            return false;
        }

        // Raycast to ensure flashlight can reach us (unless ignoring obstacles)
        if (!flashlightIgnoreObstacles)
        {
            if (Physics.Raycast(flashlightPos, toWatcher.normalized, out RaycastHit hit, distance, lineOfSightLayers))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    return false; // Something blocking the flashlight
                }
            }
        }

        return true;
    }
    #endregion

    #region Retreat Logic
    private void StartRetreat()
    {
        if (isRetreating) return;

        isRetreating = true;
        StopStaring();

        // Store start position for distance tracking
        retreatStartPosition = transform.position;
        distanceRetreated = 0f;

        // Calculate retreat direction based on retreat mode
        retreatDirection = CalculateRetreatDirection();

        // Play retreat animation (only if configured to use one)
        if (useRetreatAnimation && animator != null && !string.IsNullOrEmpty(retreatTrigger))
        {
            animator.SetTrigger(retreatTrigger);
        }

        // Stop ambient sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Play retreat sound
        if (eventConfig.retreatSound != null)
        {
            audioSource.PlayOneShot(eventConfig.retreatSound, eventConfig.retreatSoundVolume);
        }

        OnRetreated?.Invoke();

        // Notify event system
        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.WatcherRetreated(eventName);
        }

        Debug.Log($"WatcherNPC '{eventName}': Retreating (mode: {retreatMode})");
    }

    private Vector3 CalculateRetreatDirection()
    {
        Vector3 direction = Vector3.zero;

        switch (retreatMode)
        {
            case WatcherRetreatMode.AwayFromPlayer:
                if (playerTransform != null)
                {
                    direction = (transform.position - playerTransform.position).normalized;
                    direction.y = 0; // Keep horizontal
                }
                else
                {
                    direction = -transform.forward;
                }
                break;

            case WatcherRetreatMode.Down:
                direction = Vector3.down;
                break;

            case WatcherRetreatMode.Up:
                direction = Vector3.up;
                break;

            case WatcherRetreatMode.TowardPoint:
                if (retreatTarget != null)
                {
                    direction = (retreatTarget.position - transform.position).normalized;
                }
                else
                {
                    // Fallback to down if target not found
                    direction = Vector3.down;
                    Debug.LogWarning($"WatcherNPC '{eventName}': Retreat target not found, falling back to down");
                }
                break;

            case WatcherRetreatMode.CustomDirection:
                direction = retreatDirectionWorld.normalized;
                break;

            case WatcherRetreatMode.BackwardFromFacing:
                direction = -transform.forward;
                break;
        }

        return direction.normalized;
    }

    private void UpdateRetreat()
    {
        // For TowardPoint mode, check if we've reached the target
        if (retreatMode == WatcherRetreatMode.TowardPoint && retreatTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, retreatTarget.position);
            if (distanceToTarget < 0.5f)
            {
                CompleteEvent();
                return;
            }

            // Recalculate direction to ensure we're moving toward the target
            retreatDirection = (retreatTarget.position - transform.position).normalized;
        }

        // Move in retreat direction
        Vector3 movement = retreatDirection * retreatSpeed * Time.deltaTime;
        transform.position += movement;
        distanceRetreated += movement.magnitude;

        // Update fade if enabled
        if (fadeOnRetreat)
        {
            fadeProgress += Time.deltaTime / fadeDuration;
            UpdateFade(1f - fadeProgress);

            if (fadeProgress >= 1f)
            {
                CompleteEvent();
            }
        }
        else
        {
            // Check if we've moved far enough (except for TowardPoint which uses distance to target)
            if (retreatMode != WatcherRetreatMode.TowardPoint && distanceRetreated >= retreatDistanceMax)
            {
                CompleteEvent();
            }
        }
    }

    private void UpdateFade(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Material mat = renderers[i].material;
            if (mat.HasProperty("_Color"))
            {
                Color color = originalColors[i];
                color.a = Mathf.Clamp01(alpha);
                mat.color = color;
            }
        }
    }
    #endregion

    #region Event Completion
    private void CompleteEvent()
    {
        if (isComplete) return;

        isComplete = true;
        StopStaring();

        // Stop all audio
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        OnEventCompleted?.Invoke();

        Debug.Log($"WatcherNPC '{eventName}': Event completed, destroying");

        // Destroy the watcher
        Destroy(gameObject);
    }

    /// <summary>
    /// Forces the watcher to despawn immediately (used by SideGameEventManager for cleanup).
    /// </summary>
    public void ForceDespawn()
    {
        CompleteEvent();
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Draw stare distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxStareDistance > 0 ? maxStareDistance : 50f);

        // Draw line to player if available
        if (playerTransform != null)
        {
            Gizmos.color = isStaring ? Color.red : Color.gray;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, playerTransform.position + Vector3.up * 1.5f);
        }
    }
    #endregion
}
