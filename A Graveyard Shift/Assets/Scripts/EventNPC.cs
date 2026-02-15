using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC states for event-driven NPCs.
/// </summary>
public enum NPCState
{
    Idle,
    Walking,
    WaitingForInteraction,
    InDialogue,
    Exiting
}

/// <summary>
/// Component for NPCs that are part of game events.
/// Handles waypoint-based movement using NavMeshAgent, dialogue interaction, and exit behavior.
/// This component is added at runtime by GameFlowManager.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EventNPC : MonoBehaviour, IInteractable
{
    #region Runtime Configuration (Set by GameFlowManager)
    [HideInInspector] public WaypointData[] waypoints;
    [HideInInspector] public TextAsset inkDialogue;
    [HideInInspector] public string dialogueKnot;
    [HideInInspector] public NPCExitBehavior exitBehavior;
    [HideInInspector] public string exitPointName;
    [HideInInspector] public ExitDialogueData[] exitDialogues;
    [HideInInspector] public float typewriterSpeed;
    [HideInInspector] public float eventCameraHeight = -1f;
    [HideInInspector] public AudioClip eventDialogueSoundClip;
    [HideInInspector] public float eventDialogueSoundVolume = -1f;
    [HideInInspector] public float eventDialogueSoundBasePitch = -1f;
    [HideInInspector] public float eventDialogueSoundPitchVariation = -1f;
    [HideInInspector] public int eventDialogueSoundEveryN = -1;
    [HideInInspector] public CinematicEndingData postDialogueCinematic;
    #endregion

    #region Inspector Settings
    [Header("Interaction Settings")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private string interactionPrompt = "Talk";

    [Header("Movement Settings")]
    [SerializeField] private float arrivalThreshold = 0.5f;

    [Header("Camera Settings")]
    [Tooltip("Camera look height when NPC is in a lowered pose (kneeling, praying, crouching, etc.)")]
    [SerializeField] private float loweredCameraHeight = 0.5f;
    [Tooltip("Camera FOV during dialogue (-1 to use default)")]
    [SerializeField] private float cameraZoom = -1f;

    #endregion

    #region Events
    /// <summary>Fired when this NPC has completed its entire event sequence.</summary>
    public event Action OnNPCEventCompleted;

    /// <summary>Fired when this NPC arrives at a waypoint. Parameter is the waypoint name.</summary>
    public event Action<string> OnWaypointReached;
    #endregion

    #region Private Fields
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    private NavMeshAgent agent;
    private Animator animator;
    private DialogueManager dialogueManager;
    private DialogueUI dialogueUI;

    private int currentWaypointIndex = 0;
    private NPCState currentState = NPCState.Idle;
    private Transform currentTargetWaypoint;
    private bool hasCompletedDialogue = false;
    private string currentIdleAnimationBool = "";

    // Interaction delay tracking
    private float interactableAtTime = 0f;
    private bool isWaitingToBeInteractable = false;
    private Coroutine waitingDialogueCoroutine;

    // Proximity sound tracking
    private Transform playerTransform;
    private Camera mainCamera;
    private bool wasInProximity = false;
    private Renderer npcRenderer;
    private int lastSoundPlayedForWaypoint = -1;

    // Face/back to player tracking
    private bool shouldTrackPlayer = false;
    private bool trackingBackToPlayer = false;
    private float playerTrackingSpeed = 120f;

    // Background NPC blocking
    private bool isWaitingForBackgroundNPC = false;

    // Cinematic tracking - prevent multiple triggers
    private bool cinematicStarted = false;

    // OffMeshLink traversal
    private bool isTraversingLink = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoTraverseOffMeshLink = false;

        // Look for Animator on this object or any children (model is often a child)
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"EventNPC: No Animator found on {gameObject.name} or its children!");
        }
        else
        {
            Debug.Log($"EventNPC: Found Animator on {animator.gameObject.name}");
        }

        // Set layer for interaction raycast
        gameObject.layer = LayerMask.NameToLayer("Interactable");

        // Ensure collider exists
        if (GetComponent<Collider>() == null)
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2f, 1f);
            col.center = new Vector3(0f, 1f, 0f);
        }
    }

    private void Start()
    {
        dialogueManager = DialogueManager.GetInstance();
        dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (dialogueManager != null)
        {
            dialogueManager.OnDialogueEnded += HandleDialogueEnded;
            dialogueManager.OnDialogueSuspended += HandleDialogueSuspended;
        }

        // Setup for proximity sound
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        mainCamera = Camera.main;
        npcRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnDestroy()
    {
        if (dialogueManager != null)
        {
            dialogueManager.OnDialogueEnded -= HandleDialogueEnded;
            dialogueManager.OnDialogueSuspended -= HandleDialogueSuspended;
        }
    }

    private void Update()
    {
        switch (currentState)
        {
            case NPCState.Walking:
            case NPCState.Exiting:
                // Handle OffMeshLink traversal (stairs, jumps, etc.)
                if (!isTraversingLink && agent.isOnOffMeshLink)
                {
                    StartCoroutine(TraverseOffMeshLink());
                }
                if (!isTraversingLink)
                {
                    CheckArrival();
                }
                break;
        }

        // Handle continuous player tracking rotation
        if (shouldTrackPlayer && (currentState == NPCState.Idle || currentState == NPCState.WaitingForInteraction))
        {
            RotateRelativeToPlayer();
        }

        CheckProximitySound();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Initializes the NPC with event data and starts the waypoint sequence.
    /// Called by GameFlowManager after spawning.
    /// </summary>
    public void Initialize(WaypointData[] waypointData, TextAsset dialogue, string knot,
                           NPCExitBehavior exit, string exitPoint, string name = null,
                           ExitDialogueData[] exitDialogueData = null, float dialogueTypewriterSpeed = 0f,
                           float dialogueCameraZoom = -1f, float dialogueCameraHeight = -1f,
                           AudioClip soundClip = null, float soundVolume = -1f, float soundBasePitch = -1f,
                           float soundPitchVariation = -1f, int soundEveryN = -1,
                           CinematicEndingData cinematicData = null)
    {
        waypoints = waypointData;
        inkDialogue = dialogue;
        dialogueKnot = knot;
        exitBehavior = exit;
        exitPointName = exitPoint;
        exitDialogues = exitDialogueData;
        typewriterSpeed = dialogueTypewriterSpeed;
        cameraZoom = dialogueCameraZoom;
        eventCameraHeight = dialogueCameraHeight;
        Debug.Log($"EventNPC Initialize: Received camera settings - height={dialogueCameraHeight}, zoom={dialogueCameraZoom}");
        eventDialogueSoundClip = soundClip;
        eventDialogueSoundVolume = soundVolume;
        eventDialogueSoundBasePitch = soundBasePitch;
        eventDialogueSoundPitchVariation = soundPitchVariation;
        eventDialogueSoundEveryN = soundEveryN;
        postDialogueCinematic = cinematicData;

        if (!string.IsNullOrEmpty(name))
        {
            npcName = name;
        }

        // Start moving to first waypoint
        if (waypoints != null && waypoints.Length > 0)
        {
            MoveToNextWaypoint();
        }
        else
        {
            Debug.LogWarning($"EventNPC {npcName}: No waypoints configured!");
            CompleteEvent();
        }
    }

    /// <summary>
    /// Sets the NPC's display name.
    /// </summary>
    public void SetNPCName(string name)
    {
        npcName = name;
    }

    /// <summary>
    /// Sets whether this NPC should wait for a background NPC to complete.
    /// Called by GameFlowManager when a blocking background NPC is spawned.
    /// </summary>
    public void SetWaitingForBackgroundNPC(bool waiting)
    {
        isWaitingForBackgroundNPC = waiting;
        Debug.Log($"EventNPC {npcName}: SetWaitingForBackgroundNPC({waiting})");
    }

    /// <summary>
    /// Resumes this NPC after a blocking background NPC has completed.
    /// Called by GameFlowManager when the background NPC fires OnNPCEventCompleted.
    /// </summary>
    public void ResumeFromBackgroundNPCWait()
    {
        if (isWaitingForBackgroundNPC)
        {
            isWaitingForBackgroundNPC = false;
            Debug.Log($"EventNPC {npcName}: Resuming after background NPC completed");
            AdvanceToNextWaypoint();
        }
    }

    /// <summary>
    /// Returns true if this NPC is currently waiting for a background NPC to complete.
    /// </summary>
    public bool IsWaitingForBackgroundNPC()
    {
        return isWaitingForBackgroundNPC;
    }
    #endregion

    #region IInteractable Implementation
    public void Interact()
    {
        if (currentState != NPCState.WaitingForInteraction)
        {
            Debug.Log($"EventNPC {npcName}: Cannot interact - NPC is not waiting for interaction");
            return;
        }

        // Check if NPC is still in the "not yet interactable" period
        if (isWaitingToBeInteractable && Time.time < interactableAtTime)
        {
            WaypointData currentWaypoint = waypoints[currentWaypointIndex];
            if (!string.IsNullOrEmpty(currentWaypoint.waitingDialogueText))
            {
                ShowWaitingDialogue(currentWaypoint.waitingDialogueText, currentWaypoint.waitingDialogueSpeaker);
            }
            Debug.Log($"EventNPC {npcName}: Not yet interactable, {interactableAtTime - Time.time:F1}s remaining");
            return;
        }

        // Clear the waiting flag now that we're interactable
        isWaitingToBeInteractable = false;

        if (dialogueManager == null)
        {
            dialogueManager = DialogueManager.GetInstance();
            if (dialogueManager == null)
            {
                Debug.LogError("EventNPC: DialogueManager not found!");
                return;
            }
        }

        // Get dialogue from current waypoint, or fall back to event default
        WaypointData currentWaypoint2 = waypoints[currentWaypointIndex];
        TextAsset dialogueToUse = currentWaypoint2.inkDialogue != null ? currentWaypoint2.inkDialogue : inkDialogue;
        string knotToUse = !string.IsNullOrEmpty(currentWaypoint2.dialogueKnot) ? currentWaypoint2.dialogueKnot : dialogueKnot;

        if (dialogueToUse == null)
        {
            Debug.LogError($"EventNPC {npcName}: No Ink dialogue assigned for waypoint or event!");
            return;
        }

        if (dialogueManager.IsDialoguePlaying())
        {
            return;
        }

        // Get player reference for facing
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // Check if there's suspended dialogue to resume
        if (dialogueManager.HasSuspendedDialogue(transform))
        {
            Debug.Log($"EventNPC {npcName}: Resuming suspended dialogue");

            // Cancel any waiting dialogue that might be showing
            if (waitingDialogueCoroutine != null)
            {
                StopCoroutine(waitingDialogueCoroutine);
                waitingDialogueCoroutine = null;
                if (dialogueUI != null)
                {
                    dialogueUI.Hide();
                }
            }

            // Face the player
            if (player != null)
            {
                Vector3 lookDir = player.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            currentState = NPCState.InDialogue;
            dialogueManager.ResumeDialogue(transform);
            return;
        }

        // Cancel any active simple dialogue first
        SimpleDialogueTrigger.CancelActiveSimpleDialogue();

        // Cancel any waiting dialogue that might be showing
        if (waitingDialogueCoroutine != null)
        {
            StopCoroutine(waitingDialogueCoroutine);
            waitingDialogueCoroutine = null;
            if (dialogueUI != null)
            {
                dialogueUI.Hide();
            }
        }

        // Face the player
        if (player != null)
        {
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        currentState = NPCState.InDialogue;
        Debug.Log($"EventNPC {npcName}: Starting dialogue (knot: {(string.IsNullOrEmpty(knotToUse) ? "default" : knotToUse)})");

        // Determine camera height: waypoint > event > lowered pose > default
        float camHeight;
        if (currentWaypoint2.cameraHeight >= 0)
        {
            camHeight = currentWaypoint2.cameraHeight;
            Debug.Log($"EventNPC {npcName}: Using waypoint camera height: {camHeight}");
        }
        else if (eventCameraHeight >= 0)
        {
            camHeight = eventCameraHeight;
            Debug.Log($"EventNPC {npcName}: Using event camera height: {camHeight}");
        }
        else
        {
            bool isLoweredPose = IsLoweredPoseAnimation(currentIdleAnimationBool);
            camHeight = isLoweredPose ? loweredCameraHeight : -1f;
            Debug.Log($"EventNPC {npcName}: Idle animation '{currentIdleAnimationBool}', isLoweredPose={isLoweredPose}, cameraHeight={camHeight}");
        }

        // Determine camera zoom: waypoint > event > default
        float camZoom = currentWaypoint2.cameraZoom >= 0 ? currentWaypoint2.cameraZoom : cameraZoom;

        // Determine dialogue sound: waypoint > event > default
        AudioClip soundClip = currentWaypoint2.dialogueSoundClip != null ? currentWaypoint2.dialogueSoundClip : eventDialogueSoundClip;
        float soundVolume = currentWaypoint2.dialogueSoundVolume >= 0 ? currentWaypoint2.dialogueSoundVolume : eventDialogueSoundVolume;
        float soundBasePitch = currentWaypoint2.dialogueSoundBasePitch >= 0 ? currentWaypoint2.dialogueSoundBasePitch : eventDialogueSoundBasePitch;
        float soundPitchVariation = currentWaypoint2.dialogueSoundPitchVariation >= 0 ? currentWaypoint2.dialogueSoundPitchVariation : eventDialogueSoundPitchVariation;
        int soundEveryN = currentWaypoint2.dialogueSoundEveryN > 0 ? currentWaypoint2.dialogueSoundEveryN : eventDialogueSoundEveryN;

        // Set sound override if configured
        if (soundClip != null)
        {
            dialogueManager.SetDialogueSoundOverride(soundClip, soundVolume, soundBasePitch, soundPitchVariation, soundEveryN);
        }

        // Start dialogue
        if (!string.IsNullOrEmpty(knotToUse))
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, knotToUse, transform, camHeight, typewriterSpeed, camZoom);
        }
        else
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, transform, camHeight, typewriterSpeed, camZoom);
        }
    }

    private void ShowWaitingDialogue(string text, string speaker)
    {
        // Don't show if already showing a waiting dialogue
        if (waitingDialogueCoroutine != null)
        {
            return;
        }

        waitingDialogueCoroutine = StartCoroutine(ShowWaitingDialogueCoroutine(text, speaker));
    }

    private IEnumerator ShowWaitingDialogueCoroutine(string text, string speaker)
    {
        if (dialogueUI == null)
        {
            dialogueUI = FindFirstObjectByType<DialogueUI>();
            if (dialogueUI == null)
            {
                Debug.LogWarning($"EventNPC {npcName}: Cannot show waiting dialogue - DialogueUI not found");
                waitingDialogueCoroutine = null;
                yield break;
            }
        }

        // Don't show if main dialogue is playing
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            waitingDialogueCoroutine = null;
            yield break;
        }

        dialogueUI.Show();
        string speakerName = string.IsNullOrEmpty(speaker) ? null : speaker;
        dialogueUI.SetDialogueText(text, speakerName);

        // Show for 2 seconds
        yield return new WaitForSeconds(2f);

        // Only hide if we're not in a main dialogue
        if (dialogueManager == null || !dialogueManager.IsDialoguePlaying())
        {
            dialogueUI.Hide();
        }

        waitingDialogueCoroutine = null;
    }

    public string GetInteractionPrompt()
    {
        return $"{interactionPrompt} to {npcName}";
    }
    #endregion

    #region Private Methods
    private void MoveToNextWaypoint()
    {
        if (currentWaypointIndex >= waypoints.Length)
        {
            HandleWaypointsCompleted();
            return;
        }

        // Clear any previous idle animation and player tracking state
        ClearCurrentIdleAnimation();
        shouldTrackPlayer = false;

        // Re-enable NavMeshAgent in case it was disabled for an arrival animation
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;

        WaypointData waypoint = waypoints[currentWaypointIndex];
        Vector3 targetPosition;
        string waypointDescription;

        if (waypoint.usePlayerRelativePosition)
        {
            // Calculate position relative to player
            targetPosition = CalculatePlayerRelativePosition(waypoint.distanceFromPlayer, waypoint.heightOffset);
            waypointDescription = $"in front of player ({waypoint.distanceFromPlayer}m)";

            if (targetPosition == Vector3.zero)
            {
                Debug.LogError($"EventNPC {npcName}: Failed to calculate player-relative waypoint position!");
                currentWaypointIndex++;
                MoveToNextWaypoint();
                return;
            }

            // Create a temporary transform reference for arrival handling
            currentTargetWaypoint = null;
        }
        else
        {
            // Use named waypoint object
            GameObject waypointObj = GameObject.Find(waypoint.waypointName);

            if (waypointObj == null)
            {
                Debug.LogError($"EventNPC {npcName}: Waypoint '{waypoint.waypointName}' not found!");
                currentWaypointIndex++;
                MoveToNextWaypoint();
                return;
            }

            targetPosition = waypointObj.transform.position;
            currentTargetWaypoint = waypointObj.transform;
            waypointDescription = waypoint.waypointName;
        }

        agent.speed = waypoint.moveSpeed;
        agent.acceleration = 999f; // Instant acceleration
        agent.SetDestination(targetPosition);

        // DEBUG: Log path validity for stair/elevation issues
        StartCoroutine(DebugLogPathStatus(waypointDescription, targetPosition));

        SetWalkingState(true);
        currentState = NPCState.Walking;

        // Trigger custom movement animation if specified
        if (!string.IsNullOrEmpty(waypoint.movementAnimationTrigger))
        {
            TriggerAnimation(waypoint.movementAnimationTrigger);
            Debug.Log($"EventNPC {npcName}: Playing movement animation '{waypoint.movementAnimationTrigger}'");
        }

        Debug.Log($"EventNPC {npcName}: Moving to waypoint '{waypointDescription}' at speed {waypoint.moveSpeed}");
    }

    private Vector3 CalculatePlayerRelativePosition(float distance, float heightOffset)
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform == null)
        {
            Debug.LogError("EventNPC: Cannot calculate player-relative position - no player found!");
            return Vector3.zero;
        }

        // Use camera forward direction if available, otherwise player forward
        Vector3 forwardDirection;
        if (mainCamera != null)
        {
            forwardDirection = mainCamera.transform.forward;
        }
        else
        {
            forwardDirection = playerTransform.forward;
        }

        // Flatten to horizontal plane
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        // Calculate position in front of player
        Vector3 position = playerTransform.position + forwardDirection * distance;
        position.y = playerTransform.position.y + heightOffset;

        return position;
    }

    private void CheckArrival()
    {
        if (agent.pathPending) return;

        float remainingDistance = agent.remainingDistance;

        if (remainingDistance <= arrivalThreshold)
        {
            agent.ResetPath();
            SetWalkingState(false);

            if (currentState == NPCState.Exiting)
            {
                CompleteEvent();
                return;
            }

            HandleWaypointArrival();
        }
    }

    private void CheckProximitySound()
    {
        // Skip if no waypoints or invalid index
        if (waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex >= waypoints.Length) return;
        if (playerTransform == null || mainCamera == null) return;

        WaypointData waypoint = waypoints[currentWaypointIndex];

        // Skip if no sound configured for this waypoint
        if (waypoint.proximitySound == null || waypoint.proximitySoundTrigger == ProximitySoundTrigger.None) return;

        // Check condition variable if specified (e.g., "allowed_inside" must equal "true")
        if (!string.IsNullOrEmpty(waypoint.proximitySoundConditionVariable))
        {
            bool conditionMet = EventVariables.CheckVariable(
                waypoint.proximitySoundConditionVariable,
                waypoint.proximitySoundConditionValue
            );
            if (!conditionMet) return;
        }

        // Check if sound should play based on current state
        bool shouldCheckSound = false;
        switch (waypoint.proximitySoundTrigger)
        {
            case ProximitySoundTrigger.WhileMoving:
                shouldCheckSound = (currentState == NPCState.Walking);
                break;
            case ProximitySoundTrigger.WhileAtWaypoint:
                shouldCheckSound = (currentState == NPCState.Idle || currentState == NPCState.WaitingForInteraction);
                break;
            case ProximitySoundTrigger.Both:
                shouldCheckSound = (currentState == NPCState.Walking || currentState == NPCState.Idle || currentState == NPCState.WaitingForInteraction);
                break;
        }

        if (!shouldCheckSound) return;

        // Skip if already played sound for this waypoint
        if (lastSoundPlayedForWaypoint == currentWaypointIndex) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        bool isInRange = distance <= waypoint.proximitySoundRange;

        // Check if NPC is visible to camera (only if required)
        bool isVisible = true;
        if (isInRange && waypoint.requirePlayerLooking)
        {
            // Use renderer visibility (set by Unity when in camera frustum)
            if (npcRenderer != null)
            {
                isVisible = npcRenderer.isVisible;
            }
            else
            {
                // Fallback: check if in front of camera
                Vector3 toNPC = (transform.position - mainCamera.transform.position).normalized;
                isVisible = Vector3.Dot(mainCamera.transform.forward, toNPC) > 0.5f;
            }

            // Occlusion check - raycast to see if NPC is behind a wall
            if (isVisible)
            {
                Vector3 npcCenter = transform.position + Vector3.up; // Aim at NPC's chest height
                Vector3 dirToNPC = npcCenter - mainCamera.transform.position;
                if (Physics.Raycast(mainCamera.transform.position, dirToNPC.normalized, out RaycastHit hit, dirToNPC.magnitude))
                {
                    // If we hit something that isn't this NPC, they're occluded
                    if (!hit.transform.IsChildOf(transform) && hit.transform != transform)
                    {
                        isVisible = false;
                    }
                }
            }
        }

        bool isInProximity = isInRange && isVisible;

        // Play sound when entering proximity
        if (isInProximity && !wasInProximity)
        {
            AudioSource.PlayClipAtPoint(waypoint.proximitySound, transform.position, waypoint.proximitySoundVolume);
            lastSoundPlayedForWaypoint = currentWaypointIndex;
        }

        wasInProximity = isInProximity;
    }

    private void HandleWaypointArrival()
    {
        WaypointData waypoint = waypoints[currentWaypointIndex];
        string waypointDescription = waypoint.usePlayerRelativePosition
            ? $"in front of player ({waypoint.distanceFromPlayer}m)"
            : waypoint.waypointName;
        Debug.Log($"EventNPC {npcName}: Arrived at waypoint '{waypointDescription}'");

        // Fire waypoint reached event (use waypointId if available, otherwise waypointName or description)
        string waypointId = !string.IsNullOrEmpty(waypoint.waypointId) ? waypoint.waypointId
            : !string.IsNullOrEmpty(waypoint.waypointName) ? waypoint.waypointName
            : waypointDescription;
        OnWaypointReached?.Invoke(waypointId);

        // If there's an arrival animation, wait for NavMeshAgent to fully settle before playing it
        if (!string.IsNullOrEmpty(waypoint.arrivalAnimationTrigger))
        {
            StartCoroutine(PlayArrivalAnimationAfterSettling(waypoint));
        }
        else
        {
            // No arrival animation, proceed immediately
            FinalizeWaypointArrival(waypoint);
        }
    }

    private IEnumerator PlayArrivalAnimationAfterSettling(WaypointData waypoint)
    {
        // Stop the NavMeshAgent from controlling position during animation
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        // Wait a frame for everything to settle
        yield return null;

        // Raycast down to find the actual ground position
        Vector3 rayOrigin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            Debug.Log($"EventNPC {npcName}: Snapped to ground at Y={hit.point.y}");
        }
        else if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            // Fallback to NavMesh sampling if raycast fails
            transform.position = navHit.position;
        }

        TriggerAnimation(waypoint.arrivalAnimationTrigger);
        Debug.Log($"EventNPC {npcName}: Playing arrival animation '{waypoint.arrivalAnimationTrigger}'");

        FinalizeWaypointArrival(waypoint);
    }

    private void FinalizeWaypointArrival(WaypointData waypoint)
    {
        // Set idle animation bool if specified (for looping idle animations)
        if (!string.IsNullOrEmpty(waypoint.idleAnimationBool))
        {
            SetIdleAnimation(waypoint.idleAnimationBool, true);
        }

        // Set up player tracking behavior
        shouldTrackPlayer = waypoint.facePlayerWhileWaiting || waypoint.backToPlayerWhileWaiting;
        trackingBackToPlayer = waypoint.backToPlayerWhileWaiting;
        playerTrackingSpeed = waypoint.playerTrackingRotationSpeed;

        string trackingStatus = shouldTrackPlayer
            ? (trackingBackToPlayer ? " [back to player]" : " [facing player]")
            : "";

        if (waypoint.autoStartDialogue)
        {
            // Auto-start dialogue without requiring interaction
            // Set state to Idle to prevent CheckArrival() from triggering multiple times
            currentState = NPCState.Idle;
            Debug.Log($"EventNPC {npcName}: Auto-starting dialogue{trackingStatus}");
            StartCoroutine(AutoStartDialogueAfterDelay(waypoint));
        }
        else if (waypoint.waitForInteraction)
        {
            currentState = NPCState.WaitingForInteraction;

            // Handle interaction delay
            if (waypoint.timeUntilInteractable > 0)
            {
                isWaitingToBeInteractable = true;
                interactableAtTime = Time.time + waypoint.timeUntilInteractable;
                Debug.Log($"EventNPC {npcName}: Waiting for player interaction (interactable in {waypoint.timeUntilInteractable}s){trackingStatus}");
            }
            else
            {
                isWaitingToBeInteractable = false;
                Debug.Log($"EventNPC {npcName}: Waiting for player interaction{trackingStatus}");
            }
        }
        else if (waypoint.waitTime > 0)
        {
            currentState = NPCState.Idle;
            Debug.Log($"EventNPC {npcName}: Waiting for {waypoint.waitTime}s{trackingStatus}");
            StartCoroutine(WaitAtWaypoint(waypoint.waitTime));
        }
        else if (isWaitingForBackgroundNPC)
        {
            // Blocking background NPC was spawned - wait until it completes
            currentState = NPCState.Idle;
            Debug.Log($"EventNPC {npcName}: Waiting for blocking background NPC to complete{trackingStatus}");
        }
        else
        {
            shouldTrackPlayer = false; // Not waiting, so don't need to track player
            AdvanceToNextWaypoint();
        }
    }

    private void RotateRelativeToPlayer()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0;

        if (directionToPlayer.sqrMagnitude < 0.001f) return;

        // If back to player, flip the direction
        if (trackingBackToPlayer)
        {
            directionToPlayer = -directionToPlayer;
        }

        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            playerTrackingSpeed * Time.deltaTime
        );
    }

    private IEnumerator WaitAtWaypoint(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        shouldTrackPlayer = false; // Stop tracking player when done waiting

        // If waiting for a blocking background NPC, don't advance yet
        // ResumeFromBackgroundNPCWait() will be called when the background NPC completes
        if (isWaitingForBackgroundNPC)
        {
            Debug.Log($"EventNPC {npcName}: Wait time elapsed but still waiting for blocking background NPC");
            yield break;
        }

        AdvanceToNextWaypoint();
    }

    private IEnumerator AutoStartDialogueAfterDelay(WaypointData waypoint)
    {
        // Small delay to let everything settle (NPC stop moving, face player, etc.)
        float delay = waypoint.timeUntilInteractable > 0 ? waypoint.timeUntilInteractable : 0.5f;
        yield return new WaitForSeconds(delay);

        // Face the player before starting dialogue
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Start dialogue automatically
        StartDialogueForWaypoint(waypoint);
    }

    private void StartDialogueForWaypoint(WaypointData waypoint)
    {
        if (dialogueManager == null)
        {
            dialogueManager = DialogueManager.GetInstance();
            if (dialogueManager == null)
            {
                Debug.LogError("EventNPC: DialogueManager not found!");
                AdvanceToNextWaypoint();
                return;
            }
        }

        // Get dialogue from current waypoint, or fall back to event default
        TextAsset dialogueToUse = waypoint.inkDialogue != null ? waypoint.inkDialogue : inkDialogue;
        string knotToUse = !string.IsNullOrEmpty(waypoint.dialogueKnot) ? waypoint.dialogueKnot : dialogueKnot;

        if (dialogueToUse == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: No Ink dialogue assigned for auto-start waypoint, skipping dialogue");
            AdvanceToNextWaypoint();
            return;
        }

        if (dialogueManager.IsDialoguePlaying())
        {
            Debug.Log($"EventNPC {npcName}: Dialogue already playing, waiting...");
            StartCoroutine(WaitForDialogueAndRetry(waypoint));
            return;
        }

        // Cancel any active simple dialogue first
        SimpleDialogueTrigger.CancelActiveSimpleDialogue();

        currentState = NPCState.InDialogue;
        Debug.Log($"EventNPC {npcName}: Auto-starting dialogue (knot: {(string.IsNullOrEmpty(knotToUse) ? "default" : knotToUse)})");

        // Determine camera height: waypoint > event > lowered pose > default
        float camHeight;
        if (waypoint.cameraHeight >= 0)
        {
            camHeight = waypoint.cameraHeight;
            Debug.Log($"EventNPC {npcName}: Using WAYPOINT camera height: {camHeight}");
        }
        else if (eventCameraHeight >= 0)
        {
            camHeight = eventCameraHeight;
            Debug.Log($"EventNPC {npcName}: Using EVENT camera height: {camHeight}");
        }
        else
        {
            bool isLoweredPose = IsLoweredPoseAnimation(currentIdleAnimationBool);
            camHeight = isLoweredPose ? loweredCameraHeight : -1f;
            Debug.Log($"EventNPC {npcName}: Using DEFAULT camera height: {camHeight} (loweredPose={isLoweredPose})");
        }

        // Determine camera zoom: waypoint > event > default
        float camZoom = waypoint.cameraZoom >= 0 ? waypoint.cameraZoom : cameraZoom;
        Debug.Log($"EventNPC {npcName}: Camera settings - height={camHeight}, zoom={camZoom} (event values: height={eventCameraHeight}, zoom={cameraZoom})");

        // Determine dialogue sound: waypoint > event > default
        AudioClip soundClip = waypoint.dialogueSoundClip != null ? waypoint.dialogueSoundClip : eventDialogueSoundClip;
        float soundVolume = waypoint.dialogueSoundVolume >= 0 ? waypoint.dialogueSoundVolume : eventDialogueSoundVolume;
        float soundBasePitch = waypoint.dialogueSoundBasePitch >= 0 ? waypoint.dialogueSoundBasePitch : eventDialogueSoundBasePitch;
        float soundPitchVariation = waypoint.dialogueSoundPitchVariation >= 0 ? waypoint.dialogueSoundPitchVariation : eventDialogueSoundPitchVariation;
        int soundEveryN = waypoint.dialogueSoundEveryN > 0 ? waypoint.dialogueSoundEveryN : eventDialogueSoundEveryN;

        // Set sound override if configured
        if (soundClip != null)
        {
            dialogueManager.SetDialogueSoundOverride(soundClip, soundVolume, soundBasePitch, soundPitchVariation, soundEveryN);
        }

        // Start dialogue
        if (!string.IsNullOrEmpty(knotToUse))
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, knotToUse, transform, camHeight, typewriterSpeed, camZoom);
        }
        else
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, transform, camHeight, typewriterSpeed, camZoom);
        }
    }

    private IEnumerator WaitForDialogueAndRetry(WaypointData waypoint)
    {
        while (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            yield return new WaitForSeconds(0.1f);
        }
        StartDialogueForWaypoint(waypoint);
    }

    private void HandleDialogueSuspended(Transform npcTransform)
    {
        // Only handle if this is OUR suspended dialogue
        if (npcTransform != transform) return;
        if (currentState != NPCState.InDialogue) return;

        Debug.Log($"EventNPC {npcName}: Dialogue suspended - NPC remains interactable for resume");

        // Return to WaitingForInteraction so player can talk again
        currentState = NPCState.WaitingForInteraction;
        isWaitingToBeInteractable = false; // Immediately interactable
    }

    private void HandleDialogueEnded()
    {
        if (currentState != NPCState.InDialogue) return;

        Debug.Log($"EventNPC {npcName}: Dialogue ended, continuing to next waypoint");
        hasCompletedDialogue = true;

        // Trigger cinematic immediately after dialogue so player and NPC walk together
        // The cinematic will lock player controls and walk them alongside the NPC
        if (postDialogueCinematic != null && !cinematicStarted)
        {
            cinematicStarted = true;
            Debug.Log($"EventNPC {npcName}: Starting cinematic '{postDialogueCinematic.cinematicName}' - player and NPC will walk together");
            CinematicPlayerController.StartCinematicOnPlayer(postDialogueCinematic);
        }

        // If waiting for a blocking background NPC, don't advance yet
        if (isWaitingForBackgroundNPC)
        {
            currentState = NPCState.Idle;
            Debug.Log($"EventNPC {npcName}: Dialogue ended but still waiting for blocking background NPC");
            return;
        }

        AdvanceToNextWaypoint();
    }

    private void AdvanceToNextWaypoint()
    {
        WaypointData currentWaypoint = waypoints[currentWaypointIndex];

        // Check for branching condition
        if (!string.IsNullOrEmpty(currentWaypoint.branchVariable) &&
            !string.IsNullOrEmpty(currentWaypoint.branchToWaypoint))
        {
            bool conditionMet = EventVariables.CheckVariable(
                currentWaypoint.branchVariable,
                currentWaypoint.branchValue
            );

            if (conditionMet)
            {
                int branchIndex = FindWaypointIndex(currentWaypoint.branchToWaypoint);
                if (branchIndex >= 0)
                {
                    Debug.Log($"EventNPC {npcName}: Branch condition met ({currentWaypoint.branchVariable} = {currentWaypoint.branchValue}), jumping to '{currentWaypoint.branchToWaypoint}'");
                    currentWaypointIndex = branchIndex;
                    MoveToNextWaypoint();
                    return;
                }
                else
                {
                    Debug.LogWarning($"EventNPC {npcName}: Branch target waypoint '{currentWaypoint.branchToWaypoint}' not found!");
                }
            }
            else
            {
                Debug.Log($"EventNPC {npcName}: Branch condition not met ({currentWaypoint.branchVariable} != {currentWaypoint.branchValue}), continuing normally");
            }
        }

        // Default: advance to next waypoint in sequence
        currentWaypointIndex++;
        MoveToNextWaypoint();
    }

    private int FindWaypointIndex(string waypointNameOrId)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            // Check waypointId first (if set), then waypointName
            string id = !string.IsNullOrEmpty(waypoints[i].waypointId)
                ? waypoints[i].waypointId
                : waypoints[i].waypointName;

            if (id == waypointNameOrId)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleWaypointsCompleted()
    {
        Debug.Log($"EventNPC {npcName}: All waypoints completed, handling exit behavior: {exitBehavior}");

        // Clear any idle animation before finishing
        ClearCurrentIdleAnimation();

        // Start exit dialogues when NPC finishes all waypoints
        if (exitDialogues != null && exitDialogues.Length > 0)
        {
            foreach (var exitDialogue in exitDialogues)
            {
                if (!string.IsNullOrEmpty(exitDialogue.dialogueText))
                {
                    StartCoroutine(ShowExitDialogue(exitDialogue));
                }
            }
        }

        switch (exitBehavior)
        {
            case NPCExitBehavior.Destroy:
                CompleteEvent();
                break;

            case NPCExitBehavior.Disable:
                gameObject.SetActive(false);
                CompleteEvent();
                break;

            case NPCExitBehavior.ContinueWalking:
                MoveToExitPoint();
                break;
        }
    }

    private void MoveToExitPoint()
    {
        // Clear any idle animation before exiting
        ClearCurrentIdleAnimation();

        GameObject exitObj = GameObject.Find(exitPointName);

        if (exitObj == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Exit point '{exitPointName}' not found! Destroying immediately.");
            CompleteEvent();
            return;
        }

        currentState = NPCState.Exiting;
        agent.speed = waypoints.Length > 0 ? waypoints[waypoints.Length - 1].moveSpeed : 3f;
        agent.acceleration = 999f; // Instant acceleration
        agent.SetDestination(exitObj.transform.position);

        SetWalkingState(true);
        Debug.Log($"EventNPC {npcName}: Walking to exit point '{exitPointName}'");
    }

    private IEnumerator ShowExitDialogue(ExitDialogueData dialogueData)
    {
        yield return new WaitForSeconds(dialogueData.delayAfterExit);

        if (dialogueUI == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot show exit dialogue - DialogueUI not found");
            yield break;
        }

        // Don't show if main dialogue is playing
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            Debug.Log($"EventNPC {npcName}: Skipping exit dialogue - main dialogue is playing");
            yield break;
        }

        Debug.Log($"EventNPC {npcName}: Showing exit dialogue");

        dialogueUI.Show();
        string speaker = string.IsNullOrEmpty(dialogueData.speakerName) ? null : dialogueData.speakerName;
        dialogueUI.SetDialogueText(dialogueData.dialogueText, speaker);

        yield return new WaitForSeconds(dialogueData.displayDuration);

        // Only hide if we're not in a main dialogue
        if (dialogueManager == null || !dialogueManager.IsDialoguePlaying())
        {
            dialogueUI.Hide();
        }
    }

    private void CompleteEvent()
    {
        Debug.Log($"EventNPC {npcName}: Event completed");
        OnNPCEventCompleted?.Invoke();

        if (exitBehavior != NPCExitBehavior.Disable)
        {
            Destroy(gameObject);
        }
    }

    private void SetWalkingState(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, isWalking);
            Debug.Log($"EventNPC {npcName}: SetWalkingState({isWalking})");
        }
        else
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot set walking state - no Animator!");
        }
    }

    private void TriggerAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
        else if (animator == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot trigger animation '{triggerName}' - no Animator!");
        }
    }

    private void SetIdleAnimation(string boolName, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(boolName))
        {
            animator.SetBool(boolName, value);
            if (value)
            {
                currentIdleAnimationBool = boolName;
            }
            Debug.Log($"EventNPC {npcName}: Set idle animation '{boolName}' = {value}");
        }
        else if (animator == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot set idle animation '{boolName}' - no Animator!");
        }
    }

    private void ClearCurrentIdleAnimation()
    {
        if (!string.IsNullOrEmpty(currentIdleAnimationBool) && animator != null)
        {
            animator.SetBool(currentIdleAnimationBool, false);
            Debug.Log($"EventNPC {npcName}: Cleared idle animation '{currentIdleAnimationBool}'");
            currentIdleAnimationBool = "";
        }
    }

    private IEnumerator TraverseOffMeshLink()
    {
        isTraversingLink = true;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * agent.baseOffset;
        float speed = agent.speed;

        // Fully stop the agent from controlling the transform
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        // Face movement direction
        Vector3 moveDir = (endPos - startPos);
        moveDir.y = 0;
        if (moveDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        // Build a layer mask that excludes this NPC's own layer
        int npcLayer = gameObject.layer;
        int raycastMask = ~(1 << npcLayer);

        // Cache the facing rotation so we can enforce it every frame
        Quaternion facingRotation = transform.rotation;

        // Move toward the end position at the agent's speed
        while (Vector3.Distance(transform.position, endPos) > 0.1f)
        {
            Vector3 newPos = Vector3.MoveTowards(transform.position, endPos, speed * Time.deltaTime);

            // Raycast down to snap to the stair/ground surface, ignoring NPC's own layer
            Vector3 rayOrigin = newPos + Vector3.up * 3f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, raycastMask, QueryTriggerInteraction.Ignore))
            {
                newPos.y = hit.point.y;
            }

            transform.position = newPos;
            // Enforce rotation every frame to prevent animation root motion from rotating the model
            transform.rotation = facingRotation;
            yield return null;
        }

        // Snap to final position
        transform.position = endPos;

        // Complete the link and give control back to the agent
        agent.CompleteOffMeshLink();
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.nextPosition = transform.position;

        isTraversingLink = false;
    }

    private IEnumerator DebugLogPathStatus(string waypointName, Vector3 targetPos)
    {
        // Wait for path calculation
        while (agent.pathPending)
            yield return null;

        Debug.Log($"EventNPC DEBUG [{npcName}]: Path to '{waypointName}' status={agent.pathStatus}, " +
                  $"hasPath={agent.hasPath}, remainingDist={agent.remainingDistance:F2}, " +
                  $"NPC Y={transform.position.y:F2}, Target Y={targetPos.y:F2}");

        if (agent.path != null && agent.path.corners.Length > 0)
        {
            for (int i = 0; i < agent.path.corners.Length; i++)
            {
                Vector3 c = agent.path.corners[i];
                Debug.Log($"EventNPC DEBUG [{npcName}]: Path corner {i}: ({c.x:F2}, {c.y:F2}, {c.z:F2})");
            }
        }
        else
        {
            Debug.LogWarning($"EventNPC DEBUG [{npcName}]: NO PATH CORNERS - agent cannot reach '{waypointName}'!");
        }
    }

    private bool IsLoweredPoseAnimation(string animationBool)
    {
        if (string.IsNullOrEmpty(animationBool)) return false;

        string lowerName = animationBool.ToLower();
        return lowerName.Contains("kneel") ||
               lowerName.Contains("pray") ||
               lowerName.Contains("crouch") ||
               lowerName.Contains("sit") ||
               lowerName.Contains("squat") ||
               lowerName.Contains("bow");
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Draw current state
        Gizmos.color = currentState switch
        {
            NPCState.Walking => Color.green,
            NPCState.WaitingForInteraction => Color.yellow,
            NPCState.InDialogue => Color.cyan,
            NPCState.Exiting => Color.red,
            _ => Color.gray
        };

        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);

        // Draw path to target
        if (currentTargetWaypoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentTargetWaypoint.position);
        }
    }
    #endregion
}
