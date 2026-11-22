using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// NPC component for watcher-type side game events.
/// Stares at the player, drains graveyard protection, and retreats when flashlight is shined on them.
///
/// With <see cref="SideGameEvent.useCrackleRetreat"/> the retreat is a crack of the neck, after the torch has been on it
/// for a moment (look away before then and it settles). <see cref="WatcherCrackStyle.NeckStretch"/>: it freezes, its
/// neck stretches out and cracks a few times, then it glides back, still facing you, and is gone.
/// <see cref="WatcherCrackStyle.HeadShake"/>: a slight crack, its head cracks back and forth, then it sinks into the
/// ground or runs out of sight. <see cref="WatcherCrackStyle.Vanish"/>: it is simply gone. When its time is up it stops
/// costing protection and is gone the moment the player is not looking.
///
/// From the night's <see cref="SideGameEvent.changedFromAppearance"/>th watcher on, they come back changed: frozen dead
/// still, neck stretched out, head hanging over (<see cref="SetAppearance"/>).
///
/// The CCTV borrows the same figure for the gate feeds (<see cref="SpawnOnFeed"/>): there it only stands and looks
/// up at the lens until the CCTV scares it off, which plays the same crack.
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
    private bool useEntranceRotation;
    private Quaternion entranceTargetRotation;
    private Quaternion entranceRotationQuat; // offset applied on top of stare facing
    private Quaternion originalSpawnRotation; // rotation before entrance, used for ReverseEntranceRotation retreat
    private float entranceRotationSpeed;
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

    // Caught in the torch (neck crack, then gone): settings
    private bool useCrackle;
    private float neckCrackAngle;
    private float neckCrackHold;
    private float headShakeTime;
    private float headShakeRate;
    private float headShakeAngle;
    private float sinkChance;
    private float sinkTime;
    private float sinkDepth;
    private AnimationClip fleeClip;
    private float fleeClipSpeed;
    private float fleeSpeed;
    private float fleeDistance;
    private bool headTracksPlayer;
    private AudioClip neckCrackSound;
    private float neckCrackVolume;
    private AudioClip[] headCrackSounds;
    private float headCrackVolume;
    private AudioClip sinkSound;
    private float sinkVolume;
    private AudioClip fleeSound;
    private float fleeVolume;
    private WatcherCrackStyle crackStyle;
    private float neckStretchLength;
    private float neckStretchTime;
    private int neckStretchCracks;
    private float neckStretchCrackGap;
    private float neckStretchCrackAngle;
    private float neckStretchHold;
    private float glideBackTime;
    private float glideBackAcceleration;
    private float stopMotionRate;
    private float vanishGlitchTime;
    private AudioClip neckStretchSound;
    private float neckStretchVolume;
    private AudioClip vanishSound;
    private float vanishVolume;
    private int changedFromAppearance;
    private float changedNeckStretch;
    private Vector3 changedHeadPose;
    private float changedPoseTime;

    // Caught in the torch: state. Head Shake: Crack, Shake, then Run or Sink. Neck Stretch: Stretch, Cracks, Hold,
    // GlideBack, Vanish.
    private enum CrackPhase { None, Crack, Shake, Run, Sink, Stretch, Cracks, Hold, GlideBack, Vanish }
    private CrackPhase phase = CrackPhase.None;
    private float phaseStart;
    private float side = 1f;                // which way its head went last: +1 / -1
    private Vector3 crackPose;              // the first crack, in degrees: x = nod down, y = turn, z = tip toward a shoulder
    private Vector3 headTwist;              // the same, as put on top of the animation and the look this frame
    private float nextSnapTime;
    private int snapCount;
    private int lastCrackSound = -1;
    private bool timedOut;
    private float fleeTravelled;
    private float fleeBlend;                // 0..1, upright -> all fours
    private float unseenFor;
    private float sinkFrom;                 // root height when it started to sink
    private float sinkGround;               // ground height under it then
    private readonly List<Vector3> fleePath = new List<Vector3>();
    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private AudioSource voice;              // its cracks, where it stands
    private AudioSource speaker;            // on a CCTV feed: the TV's speaker
    private float speakerVolume = 1f;
    private float stretch;                  // how far its neck is drawn out right now (metres)
    private int cracksDone;
    private float glideLift;                // its feet's height above (or below) the ground where it stood
    private Vector3 neckRestLocal;
    private Vector3 headRestLocal;
    private VHSRetroFeature tornTape;       // the player's tape, torn for a moment as it vanishes in sight
    private float tapeGlitchBefore;
    private float tapeGlitchSet;
    private bool changedLook;               // a later appearance: frozen, neck stretched, head hanging over
    private bool freezePending;             // freeze the body once the animation has posed it
    private float stretchFrom;              // how far the neck already was when the stretch began
    private Vector3 twistFrom;

    // The body: the Animator sits on the model under the root
    private Animator bodyAnimator;
    private Transform headBone;
    private Transform neckBone;
    private Transform[] lookBones;
    private Transform chestBone;
    private Transform spineBone;
    private Vector3 headForwardLocal;
    private float lookWeight;
    private Vector3 lookPoint;
    private WatcherEyes eyes;

    // Borrowed animation (the all-fours run). Once it starts, this graph takes the body over from the controller,
    // carrying on the clip the controller was playing so nothing jumps.
    private PlayableGraph graph;
    private AnimationPlayableOutput graphOutput;
    private AnimationMixerPlayable graphMixer;
    private AnimationClipPlayable basePlayable;
    private AnimationClipPlayable fleePlayable;
    private bool graphReady;
    private bool hasBase;
    private bool hasFlee;

    // Stand-in on a CCTV feed
    private bool feedMode;
    private Transform feedLens;
    private bool feedScared;
    private Vector3 feedFleeDirection;
    private float feedFleeDistance;

    private static readonly float[] LookShares = { 0.15f, 0.2f, 0.45f, 1f };

    // How a crack is shared out down the spine: half in the neck, so the head swings right over, half in the head,
    // and a little the other way in the chest, so the whole top of it jerks.
    private const float HeadShare = 0.5f, NeckShare = 0.5f, ChestCounter = 0.05f;

    // Seconds it may run while still in sight before it drops into the ground where it is.
    private const float MaxRunSeconds = 1.6f;

    // How much of a neck stretch the bottom of the neck takes (drawn out of the shoulders); the head takes all of it.
    // Kept at 0: drawing the shoulders up into it makes a smooth bottle of a neck, where a thin one is what is wrong.
    private const float NeckStretchShare = 0f;

    // How much the stretching neck cranes toward whoever it is looking at, rather than straight up its own line. Less
    // on a CCTV feed: the lens looks down from high up, and a neck reaching toward it would hide behind the head.
    private const float NeckStretchCrane = 0.55f, FeedNeckStretchCrane = 0.15f;

    // Shortest dash (metres of open ground) worth running when nothing gets it out of sight; less than that, it sinks.
    private const int MinDash = 5;

    // A way out of sight nearer than this (metres) is a hop, only taken when there is no dash to make instead.
    private const int MinHide = 3;
    #endregion

    #region State (read-only, for other systems and for debugging)
    /// <summary>What it is doing right now.</summary>
    public string State =>
        isComplete ? "Done" : phase == CrackPhase.Crack ? "Cracked" : phase == CrackPhase.Shake ? "Shaking" : phase == CrackPhase.Run ? "Running" : phase == CrackPhase.Sink ? "Sinking" :
        phase == CrackPhase.Stretch ? "Stretching" : phase == CrackPhase.Cracks ? "Cracking" : phase == CrackPhase.Hold ? "Holding" : phase == CrackPhase.GlideBack ? "GlidingBack" : phase == CrackPhase.Vanish ? "Vanishing" :
        isRetreating ? "Retreating" : isEntering ? "Entering" : useCrackle && flashlightExposureTime > 0f ? "Caught" : timedOut ? "TimedOut" : isStaring ? "Staring" : "Idle";

    /// <summary>0..1: how far the torch has got toward cracking its neck.</summary>
    public float Caught => useCrackle && flashlightExposureRequired > 0f ? Mathf.Clamp01(flashlightExposureTime / flashlightExposureRequired) : 0f;

    /// <summary>How many times its head has cracked over while it shakes.</summary>
    public int Snaps => snapCount;

    /// <summary>True once it has started to leave, whichever way (for the neck crack: from the first crack on).</summary>
    public bool IsLeaving => isRetreating || phase != CrackPhase.None;

    /// <summary>True while its head (or its stretched neck) is cracking.</summary>
    public bool IsShaking => phase == CrackPhase.Shake || phase == CrackPhase.Cracks;

    /// <summary>True in the moment it vanishes (Neck Stretch).</summary>
    public bool IsVanishing => phase == CrackPhase.Vanish;

    /// <summary>How far its neck is stretched out right now (metres).</summary>
    public float NeckStretch => stretch;

    /// <summary>True while it runs.</summary>
    public bool IsRunning => phase == CrackPhase.Run;

    /// <summary>True for the stand-in the CCTV puts in front of a feed camera.</summary>
    public bool IsOnFeed => feedMode;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (!isInitialized || isComplete) return;
        UpdateFreezePending();

        if (feedMode)
        {
            if (phase != CrackPhase.None) UpdateCrack();
            if (!isComplete) UpdateBorrowedAnimation();
            return;
        }

        // Check timeout
        if (timeout > 0 && Time.time - eventStartTime >= timeout)
        {
            if (!useCrackle)
            {
                Debug.Log($"WatcherNPC '{eventName}': Event timed out, despawning");
                CompleteEvent();
                return;
            }
            if (!IsLeaving && UpdateTimedOut()) return;
        }

        if (phase != CrackPhase.None)
        {
            UpdateCrack();
        }
        else if (isRetreating)
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

        if (!isComplete && useCrackle) UpdateBorrowedAnimation();
    }

    private void LateUpdate()
    {
        if (!isInitialized || isComplete || headBone == null) return;
        if (!useCrackle && !feedMode) return;

        // Where it is looking, after the animation has posed it.
        bool hasTarget = false;
        float want = 0f;
        if (feedMode)
        {
            if (feedLens != null && phase != CrackPhase.Run) { lookPoint = feedLens.position; hasTarget = true; want = 1f; }
        }
        else if (phase == CrackPhase.Crack || phase == CrackPhase.Shake || phase == CrackPhase.Sink || phase >= CrackPhase.Stretch)
        {
            lookPoint = PlayerEye(); hasTarget = true; want = 1f;       // it cracks, and goes, looking at you
        }
        else if (!isEntering && !IsLeaving && playerTransform != null)
        {
            lookPoint = PlayerEye(); hasTarget = true;
            if (flashlightExposureTime > 0f) want = 1f;             // the torch is on it: its head comes round to you
            else if (headTracksPlayer) want = 0.6f;
        }
        float rate = want > lookWeight ? 8f : phase == CrackPhase.Run ? 12f : 3f;
        lookWeight = Mathf.MoveTowards(lookWeight, hasTarget ? want : 0f, Time.deltaTime * rate);
        if (lookWeight > 0.001f) LookAt(lookPoint, lookWeight);

        if (phase != CrackPhase.None || changedLook) ApplyHeadTwist();
        if (stretch > 0f) ApplyNeckStretch();
    }

    private void OnDestroy()
    {
        EndTapeTear();
        if (graphReady && graph.IsValid()) graph.Destroy();
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
        useEntranceRotation = config.useEntranceRotation;
        entranceRotationSpeed = config.entranceRotationSpeed;
        entranceRotationQuat = Quaternion.Euler(config.entranceRotationOffset);
        waitForEntranceBeforeStaring = config.waitForEntranceBeforeStaring;
        entranceAnimTrigger = config.entranceAnimationTrigger;

        // Idle sound settings
        idleSound = config.idleSound;
        idleSoundVolume = config.idleSoundVolume;
        idleSoundIntervalMin = config.idleSoundIntervalMin;
        idleSoundIntervalMax = config.idleSoundIntervalMax;

        CacheCrackSettings(config);

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
        FindBody();
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

        Debug.Log($"WatcherNPC '{eventName}': Initialized (entranceMove={useEntranceMovement}, entranceRotate={useEntranceRotation})");
    }

    /// <summary>
    /// Starts entrance movement/rotation or staring behavior.
    /// Call after Initialize() and ApplyLocationOverrides() so the correct flags are used.
    /// </summary>
    public void BeginBehavior()
    {
        if (useEntranceMovement || useEntranceRotation)
        {
            StartEntrance();
        }
        else
        {
            StartStaring();
        }

        Debug.Log($"WatcherNPC '{eventName}': Behavior started (entranceMove={useEntranceMovement}, entranceRotate={useEntranceRotation})");
    }

    /// <summary>
    /// Applies per-location overrides for entrance and retreat settings.
    /// Call after Initialize() to overwrite event-level defaults with location-specific values.
    /// </summary>
    public void ApplyLocationOverrides(SpawnLocationData location)
    {
        if (location == null) return;

        if (location.overrideEntrance)
        {
            useEntranceMovement = location.useEntranceMovement;
            useEntranceRotation = location.useEntranceRotation;
            entranceRotationQuat = Quaternion.Euler(location.entranceRotationOffset);
            Debug.Log($"WatcherNPC '{eventName}': Applied entrance overrides from location (move={useEntranceMovement}, rotate={useEntranceRotation})");
        }

        if (location.overrideRetreat)
        {
            retreatMode = location.retreatMode;
            retreatDirectionWorld = location.retreatDirection.normalized;
            retreatTargetName = location.retreatTargetName;

            // Re-resolve retreat target if needed
            if (retreatMode == WatcherRetreatMode.TowardPoint && !string.IsNullOrEmpty(retreatTargetName))
            {
                GameObject retreatTargetObj = GameObject.Find(retreatTargetName);
                if (retreatTargetObj != null)
                {
                    retreatTarget = retreatTargetObj.transform;
                }
                else
                {
                    Debug.LogWarning($"WatcherNPC '{eventName}': Location retreat target '{retreatTargetName}' not found!");
                }
            }

            Debug.Log($"WatcherNPC '{eventName}': Applied retreat overrides from location (mode={retreatMode})");
        }
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

    /// <summary>
    /// Sets the target rotation for entrance rotation. Call before Initialize if using entrance rotation.
    /// The watcher spawns with a rotation offset and rotates toward this target rotation.
    /// </summary>
    public void SetEntranceTargetRotation(Quaternion targetRotation)
    {
        entranceTargetRotation = targetRotation;
        Debug.Log($"WatcherNPC '{eventName}': Entrance target rotation set to {targetRotation.eulerAngles}");
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

        // Cache the spawn rotation before any entrance rotation happens
        originalSpawnRotation = transform.rotation;

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

        bool movementDone = !useEntranceMovement;
        bool rotationDone = !useEntranceRotation;

        // Move toward entrance target (if using entrance movement)
        if (useEntranceMovement)
        {
            Vector3 direction = (entranceTargetPosition - transform.position);
            float distanceRemaining = direction.magnitude;

            if (distanceRemaining <= 0.05f)
            {
                transform.position = entranceTargetPosition;
                movementDone = true;
            }
            else
            {
                float moveDistance = entranceSpeed * Time.deltaTime;
                if (moveDistance >= distanceRemaining)
                {
                    transform.position = entranceTargetPosition;
                    movementDone = true;
                }
                else
                {
                    transform.position += direction.normalized * moveDistance;
                }
            }
        }

        // Rotate toward target rotation (if using entrance rotation)
        if (useEntranceRotation)
        {
            float angleDiff = Quaternion.Angle(transform.rotation, entranceTargetRotation);
            if (angleDiff <= 0.5f)
            {
                transform.rotation = entranceTargetRotation;
                rotationDone = true;
            }
            else
            {
                float step = entranceRotationSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, entranceTargetRotation, step);
            }
        }

        // Update facing while entering (only if not using entrance rotation and not UseSpawnRotation)
        if (!useEntranceRotation && stareMode != WatcherStareMode.UseSpawnRotation)
        {
            UpdateFacing();
        }

        // Complete entrance when both movement and rotation are done
        if (movementDone && rotationDone)
        {
            CompleteEntrance();
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

        // Update facing based on stare mode (held in the torch it stops turning: only its head follows you)
        if (!useCrackle || flashlightExposureTime <= 0f)
        {
            UpdateFacing();
        }

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

            // Apply entrance rotation offset so the watcher maintains its peeked pose while facing
            if (useEntranceRotation)
            {
                targetRotation *= entranceRotationQuat;
            }

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

            if (!useCrackle && flashlightExposureTime >= flashlightExposureRequired)
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

        if (!useCrackle || isRetreating) return;

        // Held in the torch long enough: its neck cracks.
        if (flashlightExposureTime >= flashlightExposureRequired)
        {
            Debug.Log($"WatcherNPC '{eventName}': Held in the torch for {flashlightExposureRequired:F1}s, its neck cracks");
            StartCrack();
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

            case WatcherRetreatMode.ReverseEntranceRotation:
                // No translation direction needed, retreat is handled via rotation
                break;
        }

        return direction.normalized;
    }

    private void UpdateRetreat()
    {
        // ReverseEntranceRotation mode: rotate back instead of moving
        if (retreatMode == WatcherRetreatMode.ReverseEntranceRotation)
        {
            float angleDiff = Quaternion.Angle(transform.rotation, originalSpawnRotation);
            if (angleDiff <= 0.5f)
            {
                transform.rotation = originalSpawnRotation;
                CompleteEvent();
                return;
            }

            float step = entranceRotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, originalSpawnRotation, step);

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

            return;
        }

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

    #region Caught in the Torch (neck crack, then gone)
    private void CacheCrackSettings(SideGameEvent config)
    {
        useCrackle = config.useCrackleRetreat;
        neckCrackAngle = config.neckCrackAngle;
        neckCrackHold = Mathf.Max(0f, config.neckCrackHold);
        headShakeTime = Mathf.Max(0f, config.headShakeTime);
        headShakeRate = Mathf.Max(1f, config.headShakeRate);
        headShakeAngle = config.headShakeAngle;
        sinkChance = Mathf.Clamp01(config.sinkChance);
        sinkTime = Mathf.Max(0.05f, config.sinkTime);
        sinkDepth = Mathf.Max(0.5f, config.sinkDepth);
        fleeClip = config.fleeClip;
        fleeClipSpeed = config.fleeClipSpeed;
        fleeSpeed = Mathf.Max(1f, config.fleeSpeed);
        fleeDistance = config.fleeDistance;
        headTracksPlayer = config.headTracksPlayer;
        neckCrackSound = config.neckCrackSound;
        neckCrackVolume = config.neckCrackVolume;
        headCrackSounds = config.headCrackSounds;
        headCrackVolume = config.headCrackVolume;
        sinkSound = config.sinkSound;
        sinkVolume = config.sinkVolume;
        fleeSound = config.fleeSound;
        fleeVolume = config.fleeVolume;
        crackStyle = config.crackStyle;
        neckStretchLength = Mathf.Max(0f, config.neckStretchLength);
        neckStretchTime = Mathf.Max(0.01f, config.neckStretchTime);
        neckStretchCracks = Mathf.Max(0, config.neckStretchCracks);
        neckStretchCrackGap = Mathf.Max(0.02f, config.neckStretchCrackGap);
        neckStretchCrackAngle = config.neckStretchCrackAngle;
        neckStretchHold = Mathf.Max(0f, config.neckStretchHold);
        glideBackTime = Mathf.Max(0f, config.glideBackTime);
        glideBackAcceleration = Mathf.Max(0.1f, config.glideBackAcceleration);
        stopMotionRate = Mathf.Max(0f, config.stopMotionRate);
        vanishGlitchTime = Mathf.Max(0f, config.vanishGlitchTime);
        neckStretchSound = config.neckStretchSound;
        neckStretchVolume = config.neckStretchVolume;
        vanishSound = config.vanishSound;
        vanishVolume = config.vanishVolume;
        changedFromAppearance = Mathf.Max(0, config.changedFromAppearance);
        changedNeckStretch = Mathf.Max(0f, config.changedNeckStretch);
        changedHeadPose = config.changedHeadPose;
        changedPoseTime = Mathf.Max(0f, config.changedPoseTime);
    }

    /// <summary>
    /// Which appearance of the night this is (1 = the first watcher). From the event's Changed From Appearance on it
    /// stands changed: frozen dead still, neck stretched out, head hanging over. Call after Initialize.
    /// </summary>
    public void SetAppearance(int number)
    {
        changedLook = useCrackle && changedFromAppearance > 0 && number >= changedFromAppearance;
        if (!changedLook) return;
        stretch = changedNeckStretch;
        headTwist = changedHeadPose;
        freezePending = true;
        Debug.Log($"WatcherNPC '{eventName}': Appearance {number}, changed");
    }

    // Freezes the body as soon as the animation has posed it once (the graph needs a clip to hold).
    private void UpdateFreezePending()
    {
        if (!freezePending) return;
        bool posed = bodyAnimator == null || bodyAnimator.runtimeAnimatorController == null || !bodyAnimator.isActiveAndEnabled
            || bodyAnimator.GetCurrentAnimatorClipInfo(0).Length > 0 || Time.time - eventStartTime > 0.5f;
        if (!posed) return;
        freezePending = false;
        FreezeBody(changedPoseTime);
    }

    private void FindBody()
    {
        bodyAnimator = GetComponentInChildren<Animator>();
        eyes = GetComponent<WatcherEyes>();
        if (bodyAnimator == null || !bodyAnimator.isHuman) return;

        headBone = bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null) return;
        spineBone = bodyAnimator.GetBoneTransform(HumanBodyBones.Spine);
        chestBone = bodyAnimator.GetBoneTransform(HumanBodyBones.Chest);
        neckBone = bodyAnimator.GetBoneTransform(HumanBodyBones.Neck);
        lookBones = new[] { spineBone, chestBone, neckBone, headBone };

        // Read before any animation has posed it: at rest the face looks down the root's forward.
        headForwardLocal = Quaternion.Inverse(headBone.rotation) * transform.forward;
        neckRestLocal = neckBone != null ? neckBone.localPosition : Vector3.zero;
        headRestLocal = headBone.localPosition;
    }

    private Vector3 PlayerEye()
    {
        if (playerCamera != null) return playerCamera.transform.position;
        return playerTransform != null ? playerTransform.position + Vector3.up * 1.9f : transform.position + transform.forward * 5f;
    }

    private Vector3 FaceForward => headBone.rotation * headForwardLocal;

    // Turns spine, neck and head so the face points at the target. Runs after the animation, every frame.
    private void LookAt(Vector3 target, float amount)
    {
        Vector3 want = target - headBone.position;
        if (want.sqrMagnitude < 1e-4f) return;
        want.Normalize();

        // Never wring the neck further than a body could: past that the head only goes part of the way.
        float away = Vector3.Angle(transform.forward, want);
        if (away > 85f) amount *= 85f / away;

        for (int i = 0; i < lookBones.Length; i++)
        {
            Transform bone = lookBones[i];
            if (bone == null) continue;
            Quaternion turn = Quaternion.FromToRotation(FaceForward, want);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, LookShares[i] * amount) * bone.rotation;
        }
    }

    // The crack as it stands this frame, put on top of the animation and the look. Never eased: it jumps from one
    // angle to the next.
    private void ApplyHeadTwist()
    {
        if (headTwist.sqrMagnitude < 1e-4f) return;
        if (chestBone != null) Twist(chestBone, -ChestCounter);
        if (neckBone != null) Twist(neckBone, NeckShare);
        Twist(headBone, neckBone != null ? HeadShare : 1f);
    }

    private void Twist(Transform bone, float share)
    {
        Vector3 face = FaceForward;
        Vector3 up = transform.up;
        Vector3 across = Vector3.Cross(up, face);
        if (across.sqrMagnitude < 1e-4f) across = transform.right;
        across.Normalize();
        bone.rotation = Quaternion.AngleAxis(headTwist.z * share, face)
                      * Quaternion.AngleAxis(headTwist.y * share, up)
                      * Quaternion.AngleAxis(headTwist.x * share, across)
                      * bone.rotation;
    }

    // The stretched neck, after the look and the cracks have turned it: the head sits further up the line of the neck,
    // craning toward whoever it is looking at.
    private void ApplyNeckStretch()
    {
        if (neckBone == null || headBone == null) return;
        neckBone.localPosition = neckRestLocal;
        headBone.localPosition = headRestLocal;
        Vector3 along = headBone.position - neckBone.position;
        along = along.sqrMagnitude > 1e-6f ? along.normalized : transform.up;
        Vector3 toward = lookPoint - headBone.position;
        toward.y = 0f;
        float crane = feedMode ? FeedNeckStretchCrane : NeckStretchCrane;
        if (toward.sqrMagnitude > 1e-4f) along = (along * (1f - crane) + toward.normalized * crane).normalized;
        Vector3 headAt = headBone.position + along * stretch;
        neckBone.position += along * (stretch * NeckStretchShare);
        headBone.position = headAt;
    }

    // Time held in frames (stop-motion), if it moves that way.
    private float Held(float seconds)
    {
        return stopMotionRate > 0f ? Mathf.Floor(seconds * stopMotionRate) / stopMotionRate : seconds;
    }

    // 0..1 with a slight overshoot at the end: the neck goes a touch too far and comes back.
    private static float EaseOutBack(float u)
    {
        const float c1 = 1.2f, c3 = c1 + 1f;
        float v = u - 1f;
        return 1f + c3 * v * v * v + c1 * v * v;
    }

    private AudioSource Voice()
    {
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.85f;
            voice.rolloffMode = AudioRolloffMode.Logarithmic;
            voice.minDistance = 6f;
            voice.maxDistance = 70f;
            voice.dopplerLevel = 0f;
        }
        return voice;
    }

    // Its sounds: in the yard from where it stands; on a CCTV feed through the TV's speaker (silent without one).
    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        if (feedMode)
        {
            if (speaker != null) speaker.PlayOneShot(clip, volume * speakerVolume);
            return;
        }
        Voice().PlayOneShot(clip, volume);
    }

    // Its time is up: it stops costing protection, and is gone the moment the player is not looking.
    private bool UpdateTimedOut()
    {
        if (!timedOut)
        {
            timedOut = true;
            StopStaring();
            Debug.Log($"WatcherNPC '{eventName}': Event timed out, leaving when unseen");
        }
        if (flashlightExposureTime > 0f) return false;      // the torch is on it: let that play out
        if (isEntering || !SeenByPlayer() || Time.time - eventStartTime >= timeout + 45f)
        {
            Debug.Log($"WatcherNPC '{eventName}': Gone");
            CompleteEvent();
            return true;
        }
        return false;
    }

    // In the player's view with nothing drawn in between (head or body; on all fours the body is low).
    private bool SeenByPlayer()
    {
        if (playerCamera == null || !playerCamera.isActiveAndEnabled) return false;
        float body = fleeBlend > 0.5f ? 0.5f : 1.1f;
        if (InPlayerSight(transform.position + Vector3.up * body, 0.05f)) return true;
        return InPlayerSight(headBone != null ? headBone.position : transform.position + Vector3.up * 2.3f, 0.05f);
    }

    private static readonly RaycastHit[] sightHits = new RaycastHit[16];

    // A point the player could see right now: in the picture (with a margin), and nothing drawn between it and their
    // eye. Its own body, the player's, and the invisible barriers hide nothing.
    private bool InPlayerSight(Vector3 point, float margin)
    {
        Vector3 v = playerCamera.WorldToViewportPoint(point);
        if (v.z <= 0f || v.z > 60f || v.x < -margin || v.x > 1f + margin || v.y < -margin || v.y > 1f + margin) return false;

        Vector3 eye = playerCamera.transform.position;
        Vector3 d = point - eye;
        float length = d.magnitude;
        if (length < 1e-3f) return true;
        int count = Physics.RaycastNonAlloc(eye, d / length, sightHits, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = sightHits[i].transform;
            if (t.IsChildOf(transform)) continue;
            if (playerTransform != null && t.IsChildOf(playerTransform)) continue;
            if (IsInvisible(sightHits[i].collider)) continue;
            return false;
        }
        return true;
    }

    // Held in the torch long enough (on a feed: watched long enough): its neck cracks. From here on nothing stops it.
    // Neck Stretch: it freezes and its neck draws out. Head Shake: just a slight crack, then its head cracks back and
    // forth.
    private void StartCrack()
    {
        if (phase != CrackPhase.None || isComplete) return;
        phaseStart = Time.time;
        side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        StopStaring();

        if (!feedMode && audioSource != null && audioSource.isPlaying) audioSource.Stop();

        // It must not shove the player aside, and its own body must not stop its ground rays.
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (!feedMode)
        {
            OnRetreated?.Invoke();
            if (GameEventsManager.instance?.sideGameEvents != null)
            {
                GameEventsManager.instance.sideGameEvents.WatcherRetreated(eventName);
            }
        }

        switch (crackStyle)
        {
            case WatcherCrackStyle.Vanish:
                // Simply gone: no animation, no sound, nothing left behind.
                Debug.Log($"WatcherNPC '{eventName}': Vanished");
                if (eyes != null) eyes.Open = 0f;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
                CompleteEvent();
                return;

            case WatcherCrackStyle.NeckStretch:
                phase = CrackPhase.Stretch;
                stretchFrom = stretch;             // a changed watcher is already stretched: it goes on from there
                twistFrom = headTwist;
                FreezeBody();
                PlaySound(neckCrackSound, neckCrackVolume);
                PlaySound(neckStretchSound, neckStretchVolume);
                break;

            default:
                phase = CrackPhase.Crack;
                crackPose = new Vector3(neckCrackAngle * 0.25f, neckCrackAngle * 0.3f * side, neckCrackAngle * side);
                headTwist = crackPose * 1.3f;      // a touch too far, then it settles
                PlaySound(neckCrackSound, neckCrackVolume);
                break;
        }

        Debug.Log($"WatcherNPC '{eventName}': Neck cracked");
    }

    private void UpdateCrack()
    {
        float t = Time.time - phaseStart;
        switch (phase)
        {
            case CrackPhase.Crack:
                headTwist = t < 0.06f ? crackPose * 1.3f : crackPose;
                if (t >= neckCrackHold) StartShake();
                break;

            case CrackPhase.Shake:
                if (Time.time >= nextSnapTime) Snap();
                if (t >= headShakeTime) StartExit();
                break;

            case CrackPhase.Run:
                UpdateRun();
                break;

            case CrackPhase.Sink:
                UpdateSink();
                break;

            case CrackPhase.Stretch:
            {
                float u = Mathf.Clamp01(Held(t) / neckStretchTime);
                stretch = Mathf.LerpUnclamped(stretchFrom, Mathf.Max(stretchFrom, neckStretchLength), EaseOutBack(u));
                headTwist = Vector3.Lerp(twistFrom, new Vector3(30f, 6f * side, 18f * side), u);     // it lolls forward and over as it goes
                if (t >= neckStretchTime) StartStretchCracks();
                break;
            }

            case CrackPhase.Cracks:
                if (Time.time >= nextSnapTime)
                {
                    if (cracksDone < neckStretchCracks) StretchCrack();
                    else { phase = CrackPhase.Hold; phaseStart = Time.time; }
                }
                break;

            case CrackPhase.Hold:
                if (t >= neckStretchHold) StartGlideBack();
                break;

            case CrackPhase.GlideBack:
                UpdateGlideBack();
                break;

            case CrackPhase.Vanish:
                if (t >= vanishGlitchTime)
                {
                    EndTapeTear();
                    CompleteEvent();
                }
                break;
        }
    }

    // From the first crack it stops moving: the pose it was in, held every frame. Only its neck moves after that.
    // A changed watcher is held at a chosen moment of its standing animation instead (poseTime, seconds).
    private void FreezeBody(float poseTime = -1f)
    {
        EnsureGraph();
        if (graphReady && hasBase)
        {
            AnimationClip clip = basePlayable.GetAnimationClip();
            if (poseTime >= 0f && clip != null && clip.length > 0f) basePlayable.SetTime(Mathf.Repeat(poseTime, clip.length));
            basePlayable.SetSpeed(0);
            graphMixer.SetInputWeight(0, 1f);
            graphMixer.SetInputWeight(1, 0f);
            graphOutput.SetWeight(1f);
        }
        else if (bodyAnimator != null)
        {
            bodyAnimator.speed = 0f;
        }
    }

    private void StartStretchCracks()
    {
        phase = CrackPhase.Cracks;
        phaseStart = Time.time;
        stretch = Mathf.Max(stretchFrom, neckStretchLength);
        cracksDone = 0;
        nextSnapTime = Time.time;              // the first crack straight away
    }

    // A crack of the stretched neck: the head is thrown over to a new wrong angle, and the neck draws out a little more.
    private void StretchCrack()
    {
        side = -side;
        float amount = neckStretchCrackAngle * UnityEngine.Random.Range(0.75f, 1f);
        headTwist = new Vector3(UnityEngine.Random.Range(0.1f, 0.7f) * amount, 0.4f * amount * side, amount * side);     // over, and down
        stretch += Mathf.Max(stretchFrom, neckStretchLength) * 0.08f;
        cracksDone++;
        snapCount++;
        nextSnapTime = Time.time + neckStretchCrackGap * UnityEngine.Random.Range(0.75f, 1.25f);
        PlayCrackSound();
    }

    // It goes the way it came: backwards, still facing you, frozen, pulled away faster and faster.
    private void StartGlideBack()
    {
        phase = CrackPhase.GlideBack;
        phaseStart = Time.time;
        unseenFor = 0f;
        float g = GroundAt(transform.position, transform.position.y, transform);
        glideLift = float.IsNaN(g) ? 0f : transform.position.y - g;
        PlanGlideBack();
        if (fleePath.Count < 2) Vanish();      // backed up against something: it is simply gone
    }

    // Straight back from whoever is looking (the player, or a feed's lens), or as near to that as the ground allows:
    // up to 50 degrees either side, whichever way it can go furthest.
    private void PlanGlideBack()
    {
        fleePath.Clear();
        Vector3 from = transform.position;
        float g0 = GroundAt(from, from.y, transform);
        Vector3 start = new Vector3(from.x, float.IsNaN(g0) ? from.y : g0, from.z);
        Vector3 viewer = feedMode && feedLens != null ? feedLens.position : PlayerEye();
        Vector3 away = start - viewer;
        away.y = 0f;
        away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;

        float reach = 0.4f * glideBackTime + 0.5f * glideBackAcceleration * glideBackTime * glideBackTime + 1f;
        var points = new List<Vector3>();
        List<Vector3> best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = -4; i <= 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 12.5f, 0f) * away;
            points.Clear();
            ClearRunLength(start, dir, reach, transform, points, feedMode);
            float score = points.Count - Mathf.Abs(i) * 0.3f;
            if (points.Count > 0 && score > bestScore)
            {
                bestScore = score;
                best = new List<Vector3>(points);
            }
        }
        fleePath.Add(from);
        if (best != null) fleePath.AddRange(best);
    }

    private void UpdateGlideBack()
    {
        float t = Held(Time.time - phaseStart);
        float travelled = 0.4f * t + 0.5f * glideBackAcceleration * t * t;
        Vector3 p = PointOnPath(travelled, out _, out bool finished);
        transform.position = new Vector3(p.x, GroundHeight(p) + glideLift, p.z);

        if (!feedMode)
        {
            // Gone the moment the player loses sight of it.
            unseenFor = SeenByPlayer() ? 0f : unseenFor + Time.deltaTime;
            if (unseenFor >= 0.08f)
            {
                Vanish();
                return;
            }
        }
        if (finished || Time.time - phaseStart >= glideBackTime) Vanish();
    }

    // Gone: its body and its eyes at once. If the player was looking right at it, the tape tears over the moment.
    private void Vanish()
    {
        bool inSight = !feedMode && SeenByPlayer();
        phase = CrackPhase.Vanish;
        phaseStart = Time.time;
        if (eyes != null) eyes.Open = 0f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        PlaySound(vanishSound, vanishVolume);
        if (inSight) TearTape();
        Debug.Log($"WatcherNPC '{eventName}': Vanished" + (inSight ? " in sight" : ""));
    }

    private void TearTape()
    {
        if (vanishGlitchTime <= 0f || playerCamera == null) return;
        tornTape = playerCamera.GetComponent<VHSRetroFeature>();
        if (tornTape == null) return;
        tapeGlitchBefore = tornTape.glitchIntensity;
        tapeGlitchSet = Mathf.Max(tapeGlitchBefore, 0.7f);
        tornTape.glitchIntensity = tapeGlitchSet;
    }

    // Puts the tape back as it was, unless something else has changed it since.
    private void EndTapeTear()
    {
        if (tornTape != null && Mathf.Approximately(tornTape.glitchIntensity, tapeGlitchSet)) tornTape.glitchIntensity = tapeGlitchBefore;
        tornTape = null;
    }

    // Its head cracks back and forth: every crack throws it over to the other side.
    private void StartShake()
    {
        phase = CrackPhase.Shake;
        phaseStart = Time.time;
        snapCount = 0;
        Snap();
    }

    private void Snap()
    {
        side = -side;
        float amount = headShakeAngle * UnityEngine.Random.Range(0.7f, 1f);
        headTwist = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f) * amount, 0.45f * amount * side, amount * side);
        nextSnapTime = Time.time + UnityEngine.Random.Range(0.75f, 1.25f) / headShakeRate;
        snapCount++;
        PlayCrackSound();
    }

    // One of the crack takes, never the same one twice running.
    private void PlayCrackSound()
    {
        if (headCrackSounds == null || headCrackSounds.Length == 0) return;
        int pick = UnityEngine.Random.Range(0, headCrackSounds.Length);
        if (pick == lastCrackSound && headCrackSounds.Length > 1)
        {
            pick = (pick + 1 + UnityEngine.Random.Range(0, headCrackSounds.Length - 1)) % headCrackSounds.Length;
        }
        lastCrackSound = pick;
        PlaySound(headCrackSounds[pick], headCrackVolume * UnityEngine.Random.Range(0.8f, 1f));
    }

    // Then it is gone: down into the ground, or off out of sight. With no quick way out of sight it sinks.
    private void StartExit()
    {
        bool sink = UnityEngine.Random.value < sinkChance;
        if (!sink && !PlanRun()) sink = true;
        if (sink) StartSink();
        else StartRun();
    }

    // The way it runs. On a feed the CCTV has already picked the way out of the picture. In the yard it is the way
    // that gets it out of the player's sight soonest (out of the picture, or behind something it fits behind),
    // never toward the player. If nothing gets it out of sight within its run, it dashes off sideways into the dark
    // as far as it can, and drops into the ground where the dash ends.
    private bool PlanRun()
    {
        fleePath.Clear();
        Vector3 from = transform.position;
        float g0 = GroundAt(from, from.y, transform);
        Vector3 start = new Vector3(from.x, float.IsNaN(g0) ? from.y : g0, from.z);
        var points = new List<Vector3>();

        if (feedMode)
        {
            // Past the walls only the player is kept in.
            ClearRunLength(start, feedFleeDirection, feedFleeDistance > 0f ? feedFleeDistance : fleeDistance, transform, points, true);
            if (points.Count < 2) return false;
            fleePath.Add(from);
            fleePath.AddRange(points);
            return true;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled) return false;
        Vector3 toPlayer = PlayerEye() - start;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : transform.forward;

        // First choice: the quickest way out of sight that is still a run (a few strides at least). Then the longest
        // dash off across the view. Last, a hop out of sight right beside it.
        int hideSteps = int.MaxValue, hopSteps = int.MaxValue;
        float dashScore = float.NegativeInfinity;
        List<Vector3> hide = null, hop = null, dash = null;
        float turn = UnityEngine.Random.value * 15f;      // so two equally good ways do not always resolve the same way
        for (int i = 0; i < 24; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, turn + i * 15f, 0f) * Vector3.forward;
            float toward = Vector3.Dot(dir, toPlayer);
            if (toward > 0.3f) continue;                    // never at the player
            points.Clear();
            ClearRunLength(start, dir, fleeDistance, transform, points);
            for (int s = 0; s < points.Count; s++)
            {
                if (!OutOfSight(points[s])) continue;
                int steps = s + 1;
                if (steps >= MinHide ? steps < hideSteps : steps < hopSteps)
                {
                    List<Vector3> run = points.GetRange(0, Mathf.Min(points.Count, s + 3));     // a couple of metres past that point
                    if (steps >= MinHide) { hideSteps = steps; hide = run; }
                    else { hopSteps = steps; hop = run; }
                }
                break;
            }
            if (points.Count >= MinDash)
            {
                float score = Mathf.Min(points.Count, 10) + 4f * Mathf.Sqrt(Mathf.Max(0f, 1f - toward * toward));   // long, and across the view
                if (score > dashScore) { dashScore = score; dash = new List<Vector3>(points); }
            }
        }
        List<Vector3> pick = hide ?? dash ?? hop;
        if (pick == null) return false;
        fleePath.Add(from);
        fleePath.AddRange(pick);
        return true;
    }

    // Would the player lose it there, the way they are looking now? All of it: low on all fours, its back, and its
    // head, which pokes up over headstones.
    private bool OutOfSight(Vector3 ground)
    {
        return !InPlayerSight(ground + Vector3.up * 0.5f, 0.08f) && !InPlayerSight(ground + Vector3.up * 1.1f, 0.08f)
            && !InPlayerSight(ground + Vector3.up * 1.7f, 0.08f);
    }

    // Down onto all fours and away, fast. It turns on the spot in one jump.
    private void StartRun()
    {
        phase = CrackPhase.Run;
        phaseStart = Time.time;
        fleeTravelled = 0f;
        fleeBlend = 0f;
        unseenFor = 0f;
        headTwist = new Vector3(0f, 0f, side * headShakeAngle * 0.5f);     // it runs with its head still over on one side
        if (eyes != null) eyes.Open = 0f;
        PlaySound(fleeSound, fleeVolume);

        Vector3 heading = fleePath.Count > 1 ? fleePath[1] - fleePath[0] : transform.forward;
        heading.y = 0f;
        if (heading.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(heading.normalized);

        Debug.Log($"WatcherNPC '{eventName}': Running ({fleePath.Count} points, {PathLength():F1}m)");
    }

    private void UpdateRun()
    {
        float dt = Time.deltaTime;
        fleeBlend = Mathf.MoveTowards(fleeBlend, 1f, dt / 0.1f);
        fleeTravelled += fleeSpeed * Mathf.Lerp(0.5f, 1f, fleeBlend) * dt;

        Vector3 p = PointOnPath(fleeTravelled, out Vector3 heading, out bool finished);
        float y = Mathf.MoveTowards(transform.position.y, GroundHeight(p), 9f * dt);
        transform.position = new Vector3(p.x, y, p.z);

        heading.y = 0f;
        if (heading.sqrMagnitude > 1e-4f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading.normalized), 1080f * dt);
        }

        if (!feedMode)
        {
            // Gone the moment the player loses sight of it.
            unseenFor = SeenByPlayer() ? 0f : unseenFor + dt;
            if (unseenFor >= 0.08f)
            {
                CompleteEvent();
                return;
            }
        }

        // Still in sight where its run ends (on a feed: still in the picture): it drops into the ground there.
        if (finished || Time.time - phaseStart > MaxRunSeconds) StartSink();
    }

    // Straight down into the ground, faster and faster, still looking at whoever is looking at it.
    private void StartSink()
    {
        phase = CrackPhase.Sink;
        phaseStart = Time.time;
        sinkFrom = transform.position.y;
        float g = GroundAt(transform.position, transform.position.y, transform);
        sinkGround = float.IsNaN(g) ? sinkFrom : g;
        PlaySound(sinkSound, sinkVolume);
        Debug.Log($"WatcherNPC '{eventName}': Sinking");
    }

    private void UpdateSink()
    {
        float u = Mathf.Clamp01((Time.time - phaseStart) / sinkTime);
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, sinkFrom - sinkDepth * u * u, p.z);
        if (eyes != null && headBone != null && headBone.position.y < sinkGround + 0.15f) eyes.Open = 0f;
        if (u >= 1f) CompleteEvent();
    }

    private static readonly RaycastHit[] probeHits = new RaycastHit[16];
    private static readonly RaycastHit[] sweepHits = new RaycastHit[16];
    private static readonly Dictionary<int, bool> invisibleColliders = new Dictionary<int, bool>();

    /// <summary>
    /// True for a collider nothing draws: the invisible barriers that keep the player in (cubes whose renderer is
    /// switched off, or bare colliders). A bare collider on a drawn object (a tree's trunk collider on a child, say)
    /// counts as drawn, and so does terrain.
    /// </summary>
    public static bool IsInvisible(Collider c)
    {
        int id = c.GetInstanceID();
        if (!invisibleColliders.TryGetValue(id, out bool invisible))
        {
            if (c is TerrainCollider) invisible = false;
            else
            {
                Renderer own = c.GetComponent<Renderer>();
                invisible = own != null ? !own.enabled : !HasDrawnRenderer(c.GetComponentsInParent<Renderer>(true)) && !HasDrawnRenderer(c.GetComponentsInChildren<Renderer>(true));
            }
            invisibleColliders[id] = invisible;
        }
        return invisible;
    }

    private static bool HasDrawnRenderer(Renderer[] renderers)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null && r.enabled) return true;
        }
        return false;
    }

    /// <summary>
    /// How far something on all fours can run from <paramref name="from"/> along <paramref name="direction"/> (flat),
    /// stepping over the ground a metre at a time: it stops at anything it would run into (a headstone, a wall, an
    /// invisible barrier, a tree), at a step up it could not take (it drops down up to 1.6 m), or at <paramref name="maxLength"/>. If
    /// <paramref name="points"/> is given, the ground point of every step is added to it. With
    /// <paramref name="throughInvisible"/> it runs through colliders nothing draws.
    /// </summary>
    public static float ClearRunLength(Vector3 from, Vector3 direction, float maxLength, Transform ignore, List<Vector3> points = null, bool throughInvisible = false)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-4f) return 0f;
        direction.Normalize();

        float ground = GroundAt(from, from.y, ignore);
        Vector3 prev = new Vector3(from.x, float.IsNaN(ground) ? from.y : ground, from.z);
        float travelled = 0f;
        while (travelled < maxLength - 0.01f)
        {
            float step = Mathf.Min(1f, maxLength - travelled);
            Vector3 next = prev + direction * step;
            float g = GroundAt(next, prev.y, ignore);
            if (float.IsNaN(g) || g - prev.y > 0.7f || prev.y - g > 1.6f) break;
            next.y = g;

            Vector3 a = prev + Vector3.up * 0.75f;
            Vector3 ab = next + Vector3.up * 0.75f - a;
            int count = Physics.SphereCastNonAlloc(a, 0.3f, ab.normalized, sweepHits, ab.magnitude + 0.35f, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count && !blocked; i++)
            {
                Collider c = sweepHits[i].collider;
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                if (throughInvisible && IsInvisible(c)) continue;
                blocked = true;
            }
            if (blocked) break;

            travelled += step;
            prev = next;
            points?.Add(next);
        }
        return travelled;
    }

    /// <summary>The ground under a point: the highest upward-facing surface near the given height (NaN if none).</summary>
    public static float GroundAt(Vector3 p, float nearY, Transform ignore)
    {
        int count = Physics.RaycastNonAlloc(new Vector3(p.x, nearY + 1.5f, p.z), Vector3.down, probeHits, 3.5f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = probeHits[i];
            if (hit.normal.y < 0.5f) continue;
            if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
            if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
        }
        return best;
    }

    private float PathLength()
    {
        float length = 0f;
        for (int i = 1; i < fleePath.Count; i++) length += Vector3.Distance(fleePath[i - 1], fleePath[i]);
        return length;
    }

    private Vector3 PointOnPath(float travelled, out Vector3 heading, out bool finished)
    {
        heading = transform.forward;
        finished = false;
        for (int i = 1; i < fleePath.Count; i++)
        {
            Vector3 a = fleePath[i - 1], b = fleePath[i];
            float segment = Vector3.Distance(a, b);
            heading = b - a;
            if (travelled <= segment) return Vector3.Lerp(a, b, segment > 1e-4f ? travelled / segment : 1f);
            travelled -= segment;
        }
        finished = true;
        return fleePath.Count > 0 ? fleePath[fleePath.Count - 1] : transform.position;
    }

    // The ground under a point of the route: the highest upward-facing surface near the route's own height.
    private float GroundHeight(Vector3 p)
    {
        int count = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 1.2f, p.z), Vector3.down, groundHits, 3.4f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.normal.y < 0.5f) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (playerTransform != null && hit.collider.transform.IsChildOf(playerTransform)) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return float.IsNegativeInfinity(best) ? p.y : best;
    }

    private void EnsureGraph()
    {
        if (graphReady || bodyAnimator == null) return;

        // Take over from the controller with the clip it is playing right now, at the same moment, so nothing jumps.
        AnimationClip baseClip = null;
        float baseTime = 0f;
        if (bodyAnimator.runtimeAnimatorController != null && bodyAnimator.isActiveAndEnabled)
        {
            AnimatorClipInfo[] playing = bodyAnimator.GetCurrentAnimatorClipInfo(0);
            if (playing.Length > 0 && playing[0].clip != null)
            {
                baseClip = playing[0].clip;
                baseTime = bodyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime * baseClip.length;
            }
        }

        graph = PlayableGraph.Create("Watcher " + eventName);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        graphOutput = AnimationPlayableOutput.Create(graph, "Watcher", bodyAnimator);
        graphMixer = AnimationMixerPlayable.Create(graph, 2);
        if (baseClip != null)
        {
            basePlayable = AnimationClipPlayable.Create(graph, baseClip);
            basePlayable.SetApplyFootIK(false);
            basePlayable.SetTime(baseTime);
            graph.Connect(basePlayable, 0, graphMixer, 0);
            hasBase = true;
        }
        if (fleeClip != null)
        {
            fleePlayable = AnimationClipPlayable.Create(graph, fleeClip);
            fleePlayable.SetApplyFootIK(false);
            graph.Connect(fleePlayable, 0, graphMixer, 1);
            hasFlee = true;
        }
        if (!hasBase && !hasFlee)
        {
            graph.Destroy();
            return;
        }
        graphOutput.SetSourcePlayable(graphMixer);
        graphOutput.SetWeight(0f);
        graph.Play();
        graphReady = true;
    }

    // The all-fours run (Head Shake): once it starts running, this graph takes the body over from the controller.
    // (Neck Stretch freezes the body once, in FreezeBody.)
    private void UpdateBorrowedAnimation()
    {
        if (crackStyle == WatcherCrackStyle.NeckStretch) return;
        if (!graphReady)
        {
            if (phase != CrackPhase.Run) return;
            EnsureGraph();
            if (!graphReady) return;
        }
        if (!hasFlee) return;

        // Frozen mid-stride if it ends up sinking.
        fleePlayable.SetSpeed(phase == CrackPhase.Sink ? 0f : fleeSpeed * Mathf.Lerp(0.5f, 1f, fleeBlend) / Mathf.Max(0.5f, fleeClipSpeed));

        float flee = fleeBlend;
        float baseWeight = hasBase ? Mathf.Max(0f, 1f - flee) : 0f;
        float sum = baseWeight + flee;
        if (sum < 1e-4f)
        {
            graphOutput.SetWeight(0f);
            return;
        }
        graphMixer.SetInputWeight(0, baseWeight / sum);
        graphMixer.SetInputWeight(1, flee / sum);
        graphOutput.SetWeight(hasBase ? 1f : Mathf.Clamp01(sum));
    }
    #endregion

    #region CCTV Stand-In
    /// <summary>
    /// Puts a watcher in front of a CCTV feed camera. It stands and looks up at the lens, and does nothing else:
    /// no drain, no sound, no timeout. The CCTV decides when it goes (<see cref="ScareOff"/> or
    /// <see cref="ForceDespawn"/>). <paramref name="appearance"/> is how many watchers have come into the yard so far:
    /// from the event's Changed From Appearance on, the stand-in stands changed too.
    /// </summary>
    public static WatcherNPC SpawnOnFeed(SideGameEvent config, Vector3 groundPosition, Transform lens, int appearance = 0)
    {
        if (config == null || config.npcPrefab == null) return null;

        Vector3 toLens = lens != null ? lens.position - groundPosition : Vector3.forward;
        toLens.y = 0f;
        Quaternion facing = toLens.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toLens.normalized) : Quaternion.identity;

        // Made under an inactive parent so its NavMeshAgent is off before it ever looks for a NavMesh: the feeds
        // look out past the walls, where there is none.
        var holder = new GameObject("Watcher spawn");
        holder.SetActive(false);
        GameObject go = Instantiate(config.npcPrefab, groundPosition, facing, holder.transform);
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        go.transform.SetParent(null, true);
        Destroy(holder);

        go.name = config.npcPrefab.name + " (on the feed)";
        WatcherNPC watcher = go.GetComponent<WatcherNPC>();
        if (watcher == null) watcher = go.AddComponent<WatcherNPC>();
        watcher.InitializeForFeed(config, lens);
        watcher.SetAppearance(appearance);
        return watcher;
    }

    private void InitializeForFeed(SideGameEvent config, Transform lens)
    {
        eventConfig = config;
        eventName = config.eventName + " (feed)";
        feedMode = true;
        feedLens = lens;
        CacheCrackSettings(config);

        animator = GetComponent<Animator>();
        FindBody();
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        eventStartTime = Time.time;
        lookWeight = 1f;                 // already looking up at the lens when the picture comes up
        isInitialized = true;
    }

    /// <summary>
    /// Feed stand-in only: where its sounds go (the TV's speaker) and how loud. Without a speaker it is silent.
    /// </summary>
    public void SetSpeaker(AudioSource source, float volume)
    {
        speaker = source;
        speakerVolume = volume;
    }

    /// <summary>
    /// Feed stand-in only: it has been watched long enough, and it cracks the way the event's Crack Style says.
    /// Neck Stretch: it glides back from the lens and vanishes. Head Shake: it sinks, or runs off along
    /// <paramref name="direction"/> (the way out of the picture).
    /// </summary>
    public void ScareOff(Vector3 direction, float distance)
    {
        if (!feedMode || feedScared || isComplete) return;
        feedScared = true;
        feedFleeDirection = direction;
        feedFleeDistance = distance;
        StartCrack();
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
