using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that orchestrates game events in sequence.
/// Handles spawning NPCs, managing event flow, and transitioning between events.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    #region Singleton
    public static GameFlowManager Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Event Queue")]
    [Tooltip("List of event entries to execute in order. Each entry can be a single event or a random selection from a group.")]
    [SerializeField] private List<EventQueueEntry> eventQueue = new List<EventQueueEntry>();

    [Header("Killer Event Queue")]
    [Tooltip("List of conditional killer events. These check their stat condition and spawn killer NPCs if met.")]
    [SerializeField] private List<KillerEventQueueEntry> killerEventQueue = new List<KillerEventQueueEntry>();

    [Header("Settings")]
    [Tooltip("Automatically start the first event when the scene loads")]
    [SerializeField] private bool autoStartOnAwake = true;

    [Tooltip("Delay before starting the first event (seconds)")]
    [SerializeField] private float startDelay = 1f;

    [Tooltip("Delay between events (seconds)")]
    [SerializeField] private float eventTransitionDelay = 0.5f;

    [Header("Player References (for Spawn Conditions)")]
    [Tooltip("The player's transform - used for zone spawn conditions")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("The player's camera - used for facing spawn conditions")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("How often to check spawn conditions (seconds)")]
    [SerializeField] private float spawnConditionCheckInterval = 0.1f;
    #endregion

    #region Events
    /// <summary>Fired when an event starts. Parameter is the event name.</summary>
    public event Action<string> OnEventStarted;

    /// <summary>Fired when an event completes. Parameter is the event name.</summary>
    public event Action<string> OnEventCompleted;

    /// <summary>Fired when all events have been completed.</summary>
    public event Action OnAllEventsCompleted;
    #endregion

    #region Private Fields
    private int currentEventIndex = 0;
    private GameEvent currentEvent;
    private EventNPC currentNPC;
    private bool isRunning = false;
    private HashSet<GameEvent> previouslySelectedEvents = new HashSet<GameEvent>();
    private List<BackgroundNPCData> pendingWaypointTriggeredNPCs = new List<BackgroundNPCData>();
    private Coroutine waitingForSpawnConditionsCoroutine;
    private bool isWaitingForSpawnConditions = false;
    private List<EventNPC> blockingBackgroundNPCs = new List<EventNPC>();

    // Killer event tracking
    private int currentKillerEventIndex = 0;
    private ConditionalKillerEvent currentKillerEvent;
    private KillerNPC currentKillerNPC;
    private Coroutine waitingForKillerSpawnConditionsCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (autoStartOnAwake && eventQueue.Count > 0)
        {
            if (startDelay > 0)
            {
                Invoke(nameof(StartFirstEvent), startDelay);
            }
            else
            {
                StartFirstEvent();
            }
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Starts the first event in the queue.
    /// </summary>
    public void StartFirstEvent()
    {
        currentEventIndex = 0;
        previouslySelectedEvents.Clear();
        StartCurrentEvent();
    }

    /// <summary>
    /// Starts the next event in the queue.
    /// </summary>
    public void StartNextEvent()
    {
        currentEventIndex++;

        if (currentEventIndex >= eventQueue.Count)
        {
            Debug.Log("GameFlowManager: All events completed!");
            isRunning = false;
            OnAllEventsCompleted?.Invoke();

            if (GameEventsManager.instance != null)
            {
                GameEventsManager.instance.gameFlowEvents?.AllEventsCompleted();
            }
            return;
        }

        if (eventTransitionDelay > 0)
        {
            Invoke(nameof(StartCurrentEvent), eventTransitionDelay);
        }
        else
        {
            StartCurrentEvent();
        }
    }

    /// <summary>
    /// Starts a specific event by name. Searches through single events and event groups.
    /// </summary>
    public void StartEventByName(string eventName)
    {
        for (int i = 0; i < eventQueue.Count; i++)
        {
            EventQueueEntry entry = eventQueue[i];
            if (entry == null) continue;

            if (entry.selectionMode == EventSelectionMode.Single)
            {
                if (entry.singleEvent != null && entry.singleEvent.eventName == eventName)
                {
                    currentEventIndex = i;
                    StartCurrentEvent();
                    return;
                }
            }
            else if (entry.selectionMode == EventSelectionMode.RandomFromGroup)
            {
                // Check if any event in the group matches
                foreach (var groupEvent in entry.eventGroup)
                {
                    if (groupEvent != null && groupEvent.eventName == eventName)
                    {
                        // Force this specific event instead of random selection
                        currentEventIndex = i;
                        currentEvent = groupEvent;
                        StartEventDirectly(groupEvent);
                        return;
                    }
                }
            }
        }

        Debug.LogWarning($"GameFlowManager: Event '{eventName}' not found in queue!");
    }

    /// <summary>
    /// Adds a single event to the queue.
    /// </summary>
    public void AddEvent(GameEvent gameEvent)
    {
        var entry = new EventQueueEntry
        {
            selectionMode = EventSelectionMode.Single,
            singleEvent = gameEvent
        };
        eventQueue.Add(entry);
    }

    /// <summary>
    /// Adds an event queue entry to the queue.
    /// </summary>
    public void AddEventEntry(EventQueueEntry entry)
    {
        eventQueue.Add(entry);
    }

    /// <summary>
    /// Inserts a single event at a specific index.
    /// </summary>
    public void InsertEvent(int index, GameEvent gameEvent)
    {
        var entry = new EventQueueEntry
        {
            selectionMode = EventSelectionMode.Single,
            singleEvent = gameEvent
        };
        eventQueue.Insert(index, entry);
    }

    /// <summary>
    /// Inserts an event queue entry at a specific index.
    /// </summary>
    public void InsertEventEntry(int index, EventQueueEntry entry)
    {
        eventQueue.Insert(index, entry);
    }

    /// <summary>
    /// Gets the current event index.
    /// </summary>
    public int GetCurrentEventIndex() => currentEventIndex;

    /// <summary>
    /// Gets the total number of events.
    /// </summary>
    public int GetEventCount() => eventQueue.Count;

    /// <summary>
    /// Returns true if events are currently running.
    /// </summary>
    public bool IsRunning() => isRunning;

    /// <summary>
    /// Gets the current event (may be null if not running).
    /// </summary>
    public GameEvent GetCurrentEvent() => currentEvent;

    /// <summary>
    /// Returns true if currently waiting for spawn conditions to be met.
    /// </summary>
    public bool IsWaitingForSpawnConditions() => isWaitingForSpawnConditions;

    /// <summary>
    /// Starts the first killer event in the queue (checks conditions and spawns if met).
    /// </summary>
    public void StartFirstKillerEvent()
    {
        currentKillerEventIndex = 0;
        StartCurrentKillerEvent();
    }

    /// <summary>
    /// Starts the next killer event in the queue.
    /// </summary>
    public void StartNextKillerEvent()
    {
        currentKillerEventIndex++;

        if (currentKillerEventIndex >= killerEventQueue.Count)
        {
            Debug.Log("GameFlowManager: All killer events processed!");
            return;
        }

        StartCurrentKillerEvent();
    }

    /// <summary>
    /// Triggers all killer events whose conditions are met (can run in parallel with regular events).
    /// </summary>
    public void TriggerKillerEventsIfConditionsMet()
    {
        foreach (var entry in killerEventQueue)
        {
            if (entry == null || entry.killerEvent == null)
            {
                continue;
            }

            if (entry.CheckCondition())
            {
                Debug.Log($"GameFlowManager: Killer event '{entry.killerEvent.eventName}' condition met, spawning...");

                if (entry.HasSpawnConditions())
                {
                    StartCoroutine(WaitForKillerSpawnConditions(entry));
                }
                else
                {
                    StartKillerEventDirectly(entry.killerEvent);
                }
            }
            else
            {
                Debug.Log($"GameFlowManager: Killer event '{entry.killerEvent.eventName}' condition NOT met, skipping...");
            }
        }
    }

    /// <summary>
    /// Manually trigger a specific killer event by name (ignores condition check).
    /// </summary>
    public void ForceStartKillerEvent(string eventName)
    {
        foreach (var entry in killerEventQueue)
        {
            if (entry?.killerEvent != null && entry.killerEvent.eventName == eventName)
            {
                StartKillerEventDirectly(entry.killerEvent);
                return;
            }
        }
        Debug.LogWarning($"GameFlowManager: Killer event '{eventName}' not found!");
    }

    /// <summary>
    /// Gets the killer event queue count.
    /// </summary>
    public int GetKillerEventCount() => killerEventQueue.Count;
    #endregion

    #region Private Methods
    /// <summary>
    /// Calculates spawn position in front of the player.
    /// </summary>
    private Vector3 CalculateSpawnPositionInFrontOfPlayer(float distance, float heightOffset)
    {
        Transform player = playerTransform;
        Camera cam = playerCamera;

        // Try to find player if not cached
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Try to find camera if not cached
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (player == null)
        {
            Debug.LogError("GameFlowManager: Cannot spawn in front of player - no player found!");
            return Vector3.zero;
        }

        // Use camera forward direction (horizontal only) if available, otherwise use player forward
        Vector3 forwardDirection;
        if (cam != null)
        {
            forwardDirection = cam.transform.forward;
        }
        else
        {
            forwardDirection = player.forward;
        }

        // Flatten to horizontal plane
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        // Calculate spawn position
        Vector3 spawnPos = player.position + forwardDirection * distance;
        spawnPos.y = player.position.y + heightOffset;

        return spawnPos;
    }

    /// <summary>
    /// Calculates the spawn rotation based on event settings.
    /// </summary>
    private Quaternion CalculateSpawnRotation(Transform spawnPoint, bool facePlayer, bool backToPlayer, bool useCustomRotation, Vector3 rotationOffset, Vector3 spawnPosition)
    {
        Quaternion baseRotation;

        if (facePlayer || backToPlayer)
        {
            // Find player and face them (or away from them)
            Transform player = playerTransform;
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }

            if (player != null)
            {
                // Calculate direction to player (ignoring Y to keep NPC upright)
                Vector3 directionToPlayer = player.position - spawnPosition;
                directionToPlayer.y = 0;

                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    // If backToPlayer, flip the direction
                    if (backToPlayer)
                    {
                        directionToPlayer = -directionToPlayer;
                    }
                    baseRotation = Quaternion.LookRotation(directionToPlayer);
                }
                else
                {
                    baseRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
                }
            }
            else
            {
                Debug.LogWarning("GameFlowManager: facePlayerOnSpawn/backToPlayerOnSpawn is true but no player found! Using spawn point rotation.");
                baseRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            }
        }
        else if (useCustomRotation)
        {
            // Use custom rotation as the base (absolute rotation)
            baseRotation = Quaternion.Euler(rotationOffset);
            return baseRotation; // Return early, no offset needed
        }
        else
        {
            // Use spawn point rotation as base (or identity if no spawn point)
            baseRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        }

        // Apply rotation offset
        if (rotationOffset != Vector3.zero)
        {
            baseRotation *= Quaternion.Euler(rotationOffset);
        }

        return baseRotation;
    }

    private void StartEventDirectly(GameEvent gameEvent)
    {
        if (gameEvent == null)
        {
            Debug.LogError("GameFlowManager: Cannot start null event!");
            return;
        }

        currentEvent = gameEvent;

        if (currentEvent.npcPrefab == null)
        {
            Debug.LogError($"GameFlowManager: Event '{currentEvent.eventName}' has no NPC prefab assigned!");
            StartNextEvent();
            return;
        }

        isRunning = true;
        Debug.Log($"GameFlowManager: Starting event '{currentEvent.eventName}'");

        // Determine spawn position
        Vector3 spawnPosition;
        Transform spawnPointTransform = null;

        if (currentEvent.spawnInFrontOfPlayer)
        {
            spawnPosition = CalculateSpawnPositionInFrontOfPlayer(
                currentEvent.spawnDistanceFromPlayer,
                currentEvent.spawnHeightOffset
            );

            if (spawnPosition == Vector3.zero)
            {
                Debug.LogError($"GameFlowManager: Failed to calculate spawn position in front of player for event '{currentEvent.eventName}'!");
                StartNextEvent();
                return;
            }
        }
        else
        {
            // Find spawn point
            GameObject spawnPoint = GameObject.Find(currentEvent.spawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogError($"GameFlowManager: Spawn point '{currentEvent.spawnPointName}' not found!");
                StartNextEvent();
                return;
            }
            spawnPosition = spawnPoint.transform.position;
            spawnPointTransform = spawnPoint.transform;
        }

        // Calculate spawn rotation
        Quaternion spawnRotation = CalculateSpawnRotation(
            spawnPointTransform,
            currentEvent.facePlayerOnSpawn,
            currentEvent.backToPlayerOnSpawn,
            currentEvent.useCustomRotation,
            currentEvent.spawnRotationOffset,
            spawnPosition
        );

        // Spawn NPC
        GameObject npcObject = Instantiate(currentEvent.npcPrefab,
                                           spawnPosition,
                                           spawnRotation);

        // Add or get EventNPC component
        currentNPC = npcObject.GetComponent<EventNPC>();
        if (currentNPC == null)
        {
            currentNPC = npcObject.AddComponent<EventNPC>();
        }

        // Subscribe to NPC events
        currentNPC.OnNPCEventCompleted += HandleNPCEventCompleted;
        currentNPC.OnWaypointReached += HandleMainNPCWaypointReached;

        // Initialize the NPC
        currentNPC.Initialize(
            currentEvent.waypoints,
            currentEvent.inkDialogue,
            currentEvent.dialogueKnot,
            currentEvent.exitBehavior,
            currentEvent.exitPointName,
            currentEvent.eventName,
            currentEvent.exitDialogues,
            currentEvent.typewriterSpeed,
            currentEvent.cameraZoom,
            currentEvent.cameraHeight,
            currentEvent.dialogueSoundClip,
            currentEvent.dialogueSoundVolume,
            currentEvent.dialogueSoundBasePitch,
            currentEvent.dialogueSoundPitchVariation,
            currentEvent.dialogueSoundEveryN
        );

        // Fire event started
        OnEventStarted?.Invoke(currentEvent.eventName);

        if (GameEventsManager.instance != null)
        {
            GameEventsManager.instance.gameFlowEvents?.EventStarted(currentEvent.eventName);
        }

        // Spawn background NPCs (concurrent, non-blocking)
        SpawnBackgroundNPCs(currentEvent);
    }

    private void SpawnBackgroundNPCs(GameEvent gameEvent)
    {
        // Clear any pending waypoint-triggered NPCs from previous events
        pendingWaypointTriggeredNPCs.Clear();

        if (gameEvent.backgroundNPCs == null || gameEvent.backgroundNPCs.Length == 0)
        {
            return;
        }

        foreach (var bgNPC in gameEvent.backgroundNPCs)
        {
            if (bgNPC == null || bgNPC.npcPrefab == null)
            {
                continue;
            }

            switch (bgNPC.spawnTrigger)
            {
                case BackgroundNPCSpawnTrigger.OnEventStart:
                    if (bgNPC.spawnDelay > 0)
                    {
                        StartCoroutine(SpawnBackgroundNPCDelayed(bgNPC));
                    }
                    else
                    {
                        SpawnBackgroundNPC(bgNPC);
                    }
                    break;

                case BackgroundNPCSpawnTrigger.OnWaypointReached:
                    // Queue this NPC to spawn when the main NPC reaches the trigger waypoint
                    if (!string.IsNullOrEmpty(bgNPC.triggerWaypointName))
                    {
                        pendingWaypointTriggeredNPCs.Add(bgNPC);
                        Debug.Log($"GameFlowManager: Background NPC '{bgNPC.npcName}' will spawn when main NPC reaches '{bgNPC.triggerWaypointName}'");
                    }
                    else
                    {
                        Debug.LogWarning($"GameFlowManager: Background NPC '{bgNPC.npcName}' has OnWaypointReached trigger but no waypoint name specified!");
                    }
                    break;
            }
        }
    }

    private void HandleMainNPCWaypointReached(string waypointName)
    {
        // Check if any pending background NPCs should spawn at this waypoint
        for (int i = pendingWaypointTriggeredNPCs.Count - 1; i >= 0; i--)
        {
            var bgNPC = pendingWaypointTriggeredNPCs[i];
            if (bgNPC.triggerWaypointName == waypointName)
            {
                Debug.Log($"GameFlowManager: Waypoint '{waypointName}' reached - spawning background NPC '{bgNPC.npcName}'");

                // IMPORTANT: Set the waiting flag IMMEDIATELY if this is a blocking NPC
                // This must happen before FinalizeWaypointArrival runs in EventNPC
                if (bgNPC.blockMainNPCUntilComplete && currentNPC != null)
                {
                    currentNPC.SetWaitingForBackgroundNPC(true);
                    Debug.Log($"GameFlowManager: Main NPC set to wait for blocking background NPC '{bgNPC.npcName}'");
                }

                if (bgNPC.spawnDelay > 0)
                {
                    StartCoroutine(SpawnBackgroundNPCDelayed(bgNPC));
                }
                else
                {
                    SpawnBackgroundNPC(bgNPC);
                }

                // Remove from pending list
                pendingWaypointTriggeredNPCs.RemoveAt(i);
            }
        }
    }

    private IEnumerator SpawnBackgroundNPCDelayed(BackgroundNPCData bgNPC)
    {
        yield return new WaitForSeconds(bgNPC.spawnDelay);
        SpawnBackgroundNPC(bgNPC);
    }

    private void SpawnBackgroundNPC(BackgroundNPCData bgNPC)
    {
        // Determine spawn position
        Vector3 spawnPosition;
        Transform spawnPointTransform = null;

        if (bgNPC.spawnInFrontOfPlayer)
        {
            spawnPosition = CalculateSpawnPositionInFrontOfPlayer(
                bgNPC.spawnDistanceFromPlayer,
                bgNPC.spawnHeightOffset
            );

            if (spawnPosition == Vector3.zero)
            {
                Debug.LogError($"GameFlowManager: Failed to calculate spawn position in front of player for background NPC '{bgNPC.npcName}'!");
                return;
            }
        }
        else
        {
            // Find spawn point
            GameObject spawnPoint = GameObject.Find(bgNPC.spawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogError($"GameFlowManager: Background NPC spawn point '{bgNPC.spawnPointName}' not found!");
                return;
            }
            spawnPosition = spawnPoint.transform.position;
            spawnPointTransform = spawnPoint.transform;
        }

        // Calculate spawn rotation
        Quaternion spawnRotation = CalculateSpawnRotation(
            spawnPointTransform,
            bgNPC.facePlayerOnSpawn,
            bgNPC.backToPlayerOnSpawn,
            bgNPC.useCustomRotation,
            bgNPC.spawnRotationOffset,
            spawnPosition
        );

        // Spawn NPC
        GameObject npcObject = Instantiate(bgNPC.npcPrefab,
                                           spawnPosition,
                                           spawnRotation);

        // Add or get EventNPC component
        EventNPC eventNPC = npcObject.GetComponent<EventNPC>();
        if (eventNPC == null)
        {
            eventNPC = npcObject.AddComponent<EventNPC>();
        }

        // Initialize the background NPC with optional dialogue
        eventNPC.Initialize(
            bgNPC.waypoints,
            bgNPC.inkDialogue,
            bgNPC.dialogueKnot,
            bgNPC.exitBehavior,
            bgNPC.exitPointName,
            bgNPC.npcName,
            null,  // No exit dialogues for background NPCs
            bgNPC.typewriterSpeed,
            bgNPC.cameraZoom,
            bgNPC.cameraHeight,
            bgNPC.dialogueSoundClip,
            bgNPC.dialogueSoundVolume,
            bgNPC.dialogueSoundBasePitch,
            bgNPC.dialogueSoundPitchVariation,
            bgNPC.dialogueSoundEveryN
        );

        // Handle blocking behavior - track the NPC and subscribe to its completion
        // For OnWaypointReached triggers, SetWaitingForBackgroundNPC was already called in HandleMainNPCWaypointReached
        // For OnEventStart triggers, we need to set it here (though blocking at event start is less common)
        if (bgNPC.blockMainNPCUntilComplete && currentNPC != null)
        {
            if (!currentNPC.IsWaitingForBackgroundNPC())
            {
                currentNPC.SetWaitingForBackgroundNPC(true);
            }
            blockingBackgroundNPCs.Add(eventNPC);
            eventNPC.OnNPCEventCompleted += () => HandleBlockingBackgroundNPCCompleted(eventNPC);
            Debug.Log($"GameFlowManager: Tracking blocking background NPC '{bgNPC.npcName}' for completion");
        }

        Debug.Log($"GameFlowManager: Spawned background NPC '{bgNPC.npcName}'");
    }

    private void HandleBlockingBackgroundNPCCompleted(EventNPC completedNPC)
    {
        // Remove from tracking list
        blockingBackgroundNPCs.Remove(completedNPC);

        Debug.Log($"GameFlowManager: Blocking background NPC completed, {blockingBackgroundNPCs.Count} remaining");

        // If no more blocking NPCs, resume the main NPC
        if (blockingBackgroundNPCs.Count == 0 && currentNPC != null)
        {
            currentNPC.ResumeFromBackgroundNPCWait();
        }
    }

    private void StartCurrentEvent()
    {
        if (currentEventIndex >= eventQueue.Count)
        {
            Debug.LogWarning("GameFlowManager: No more events to start!");
            return;
        }

        EventQueueEntry entry = eventQueue[currentEventIndex];

        if (entry == null)
        {
            Debug.LogError($"GameFlowManager: Event entry at index {currentEventIndex} is null!");
            StartNextEvent();
            return;
        }

        // Check if the condition for this entry is met
        if (!entry.CheckCondition())
        {
            Debug.Log($"GameFlowManager: Condition not met for entry at index {currentEventIndex} ({entry.GetDisplayName()}), skipping...");
            StartNextEvent();
            return;
        }

        // Handle killer events separately
        if (entry.IsKillerEvent())
        {
            ConditionalKillerEvent killerEvent = entry.GetKillerEvent();
            if (killerEvent == null)
            {
                Debug.LogError($"GameFlowManager: Killer event is null at index {currentEventIndex}!");
                StartNextEvent();
                return;
            }

            // Check spawn conditions for killer event
            if (entry.HasSpawnConditions())
            {
                if (waitingForSpawnConditionsCoroutine != null)
                {
                    StopCoroutine(waitingForSpawnConditionsCoroutine);
                }
                waitingForSpawnConditionsCoroutine = StartCoroutine(WaitForKillerSpawnConditionsInQueue(entry, killerEvent));
            }
            else
            {
                StartKillerEventDirectly(killerEvent);
            }
            return;
        }

        // Handle regular events
        GameEvent selectedEvent = entry.GetEvent(previouslySelectedEvents);

        if (selectedEvent == null)
        {
            Debug.LogError($"GameFlowManager: No valid event from entry at index {currentEventIndex} ({entry.GetDisplayName()})!");
            StartNextEvent();
            return;
        }

        // Track this event as selected for future exclusion
        previouslySelectedEvents.Add(selectedEvent);

        // Check if this entry has spawn conditions that need to be met
        if (entry.HasSpawnConditions())
        {
            // Start waiting for spawn conditions
            if (waitingForSpawnConditionsCoroutine != null)
            {
                StopCoroutine(waitingForSpawnConditionsCoroutine);
            }
            waitingForSpawnConditionsCoroutine = StartCoroutine(WaitForSpawnConditions(entry, selectedEvent));
        }
        else
        {
            // No spawn conditions, start immediately
            StartEventDirectly(selectedEvent);
        }
    }

    private IEnumerator WaitForKillerSpawnConditionsInQueue(EventQueueEntry entry, ConditionalKillerEvent killerEvent)
    {
        isWaitingForSpawnConditions = true;
        Debug.Log($"GameFlowManager: Waiting for spawn conditions for killer event '{killerEvent.eventName}'...");

        // Try to auto-find player references if not set
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

        // Wait until spawn conditions are met
        while (!entry.CheckSpawnConditions(playerTransform, playerCamera))
        {
            yield return new WaitForSeconds(spawnConditionCheckInterval);
        }

        Debug.Log($"GameFlowManager: Spawn conditions met for killer event '{killerEvent.eventName}'!");
        isWaitingForSpawnConditions = false;
        waitingForSpawnConditionsCoroutine = null;

        StartKillerEventDirectly(killerEvent);
    }

    private IEnumerator WaitForSpawnConditions(EventQueueEntry entry, GameEvent selectedEvent)
    {
        isWaitingForSpawnConditions = true;
        Debug.Log($"GameFlowManager: Waiting for spawn conditions for event '{selectedEvent.eventName}'...");

        // Try to auto-find player references if not set
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

        // Wait until spawn conditions are met
        while (!entry.CheckSpawnConditions(playerTransform, playerCamera))
        {
            yield return new WaitForSeconds(spawnConditionCheckInterval);
        }

        Debug.Log($"GameFlowManager: Spawn conditions met for event '{selectedEvent.eventName}'!");
        isWaitingForSpawnConditions = false;
        waitingForSpawnConditionsCoroutine = null;

        StartEventDirectly(selectedEvent);
    }

    private void HandleNPCEventCompleted()
    {
        if (currentNPC != null)
        {
            currentNPC.OnNPCEventCompleted -= HandleNPCEventCompleted;
            currentNPC.OnWaypointReached -= HandleMainNPCWaypointReached;
        }

        // Clear any pending waypoint-triggered NPCs that didn't spawn
        pendingWaypointTriggeredNPCs.Clear();

        // Clear any blocking background NPCs (they should have completed, but clean up just in case)
        blockingBackgroundNPCs.Clear();

        string eventName = currentEvent?.eventName ?? "Unknown";
        Debug.Log($"GameFlowManager: Event '{eventName}' completed");

        // Fire event completed
        OnEventCompleted?.Invoke(eventName);

        if (GameEventsManager.instance != null)
        {
            GameEventsManager.instance.gameFlowEvents?.EventCompleted(eventName);
        }

        // Get wait time from current entry before clearing state
        float waitTime = 0f;
        if (currentEventIndex >= 0 && currentEventIndex < eventQueue.Count)
        {
            EventQueueEntry currentEntry = eventQueue[currentEventIndex];
            if (currentEntry != null)
            {
                waitTime = currentEntry.waitTimeBeforeNextEvent;
            }
        }

        currentNPC = null;
        currentEvent = null;

        // Move to next event (with optional delay)
        if (waitTime > 0f)
        {
            Debug.Log($"GameFlowManager: Waiting {waitTime}s before next event");
            Invoke(nameof(StartNextEvent), waitTime);
        }
        else
        {
            StartNextEvent();
        }
    }

    private void StartCurrentKillerEvent()
    {
        if (currentKillerEventIndex >= killerEventQueue.Count)
        {
            Debug.Log("GameFlowManager: No more killer events to process!");
            return;
        }

        KillerEventQueueEntry entry = killerEventQueue[currentKillerEventIndex];

        if (entry == null || entry.killerEvent == null)
        {
            Debug.LogError($"GameFlowManager: Killer event entry at index {currentKillerEventIndex} is null!");
            StartNextKillerEvent();
            return;
        }

        // Check if the condition is met
        if (!entry.CheckCondition())
        {
            Debug.Log($"GameFlowManager: Condition not met for killer event '{entry.killerEvent.eventName}', skipping...");
            StartNextKillerEvent();
            return;
        }

        // Check spawn conditions if any
        if (entry.HasSpawnConditions())
        {
            if (waitingForKillerSpawnConditionsCoroutine != null)
            {
                StopCoroutine(waitingForKillerSpawnConditionsCoroutine);
            }
            waitingForKillerSpawnConditionsCoroutine = StartCoroutine(WaitForKillerSpawnConditions(entry));
        }
        else
        {
            StartKillerEventDirectly(entry.killerEvent);
        }
    }

    private IEnumerator WaitForKillerSpawnConditions(KillerEventQueueEntry entry)
    {
        Debug.Log($"GameFlowManager: Waiting for spawn conditions for killer event '{entry.killerEvent.eventName}'...");

        // Auto-find player references if not set
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

        // Wait until spawn conditions are met
        while (!entry.CheckSpawnConditions(playerTransform, playerCamera))
        {
            yield return new WaitForSeconds(spawnConditionCheckInterval);
        }

        Debug.Log($"GameFlowManager: Spawn conditions met for killer event '{entry.killerEvent.eventName}'!");
        waitingForKillerSpawnConditionsCoroutine = null;

        StartKillerEventDirectly(entry.killerEvent);
    }

    private void StartKillerEventDirectly(ConditionalKillerEvent killerEvent)
    {
        if (killerEvent == null)
        {
            Debug.LogError("GameFlowManager: Cannot start null killer event!");
            return;
        }

        if (killerEvent.killerPrefab == null)
        {
            Debug.LogError($"GameFlowManager: Killer event '{killerEvent.eventName}' has no killer prefab assigned!");
            return;
        }

        currentKillerEvent = killerEvent;
        Debug.Log($"GameFlowManager: Starting killer event '{killerEvent.eventName}'");

        // Determine spawn position
        Vector3 spawnPosition;
        Transform spawnPointTransform = null;

        if (killerEvent.spawnInFrontOfPlayer)
        {
            spawnPosition = CalculateSpawnPositionInFrontOfPlayer(
                killerEvent.spawnDistanceFromPlayer,
                killerEvent.spawnHeightOffset
            );

            if (spawnPosition == Vector3.zero)
            {
                Debug.LogError($"GameFlowManager: Failed to calculate spawn position for killer '{killerEvent.eventName}'!");
                return;
            }
        }
        else
        {
            GameObject spawnPoint = GameObject.Find(killerEvent.spawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogError($"GameFlowManager: Killer spawn point '{killerEvent.spawnPointName}' not found!");
                return;
            }
            spawnPosition = spawnPoint.transform.position;
            spawnPointTransform = spawnPoint.transform;
        }

        // Calculate spawn rotation
        Quaternion spawnRotation = CalculateSpawnRotation(
            spawnPointTransform,
            killerEvent.facePlayerOnSpawn,
            killerEvent.backToPlayerOnSpawn,
            killerEvent.useCustomRotation,
            killerEvent.spawnRotationOffset,
            spawnPosition
        );

        // Spawn killer NPC
        GameObject killerObject = Instantiate(killerEvent.killerPrefab, spawnPosition, spawnRotation);

        // Add or get KillerNPC component
        currentKillerNPC = killerObject.GetComponent<KillerNPC>();
        if (currentKillerNPC == null)
        {
            currentKillerNPC = killerObject.AddComponent<KillerNPC>();
        }

        // Initialize the killer with event settings
        currentKillerNPC.Initialize(killerEvent);

        // Add EventNPC for waypoint movement if waypoints are configured
        if (killerEvent.waypoints != null && killerEvent.waypoints.Length > 0)
        {
            EventNPC eventNPC = killerObject.GetComponent<EventNPC>();
            if (eventNPC == null)
            {
                eventNPC = killerObject.AddComponent<EventNPC>();
            }

            // Subscribe to completion
            eventNPC.OnNPCEventCompleted += HandleKillerNPCCompleted;

            // Initialize waypoint movement
            eventNPC.Initialize(
                killerEvent.waypoints,
                killerEvent.inkDialogue,
                killerEvent.dialogueKnot,
                killerEvent.exitBehavior,
                killerEvent.exitPointName,
                killerEvent.eventName,
                null, // No exit dialogues for killers
                0f,   // Default typewriter speed
                killerEvent.cameraZoom,
                killerEvent.cameraHeight,
                null, // No dialogue sound for killers
                -1f, -1f, -1f, -1
            );
        }

        // Fire event started
        OnEventStarted?.Invoke(killerEvent.eventName);

        if (GameEventsManager.instance != null)
        {
            GameEventsManager.instance.gameFlowEvents?.EventStarted(killerEvent.eventName);
        }
    }

    private void HandleKillerNPCCompleted()
    {
        string eventName = currentKillerEvent?.eventName ?? "Unknown Killer";
        Debug.Log($"GameFlowManager: Killer event '{eventName}' completed (player survived)");

        // Fire event completed
        OnEventCompleted?.Invoke(eventName);

        if (GameEventsManager.instance != null)
        {
            GameEventsManager.instance.gameFlowEvents?.EventCompleted(eventName);
        }

        // Get wait time from current entry before clearing state
        float waitTime = 0f;
        if (currentEventIndex >= 0 && currentEventIndex < eventQueue.Count)
        {
            EventQueueEntry currentEntry = eventQueue[currentEventIndex];
            if (currentEntry != null)
            {
                waitTime = currentEntry.waitTimeBeforeNextEvent;
            }
        }

        currentKillerNPC = null;
        currentKillerEvent = null;

        // Move to next event in queue (with optional delay)
        if (waitTime > 0f)
        {
            Debug.Log($"GameFlowManager: Waiting {waitTime}s before next event");
            Invoke(nameof(StartNextEvent), waitTime);
        }
        else
        {
            StartNextEvent();
        }
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Visualize spawn points and waypoints for configured events
        foreach (var entry in eventQueue)
        {
            if (entry == null) continue;

            // Get all events to visualize from this entry
            List<GameEvent> eventsToVisualize = new List<GameEvent>();
            if (entry.selectionMode == EventSelectionMode.Single && entry.singleEvent != null)
            {
                eventsToVisualize.Add(entry.singleEvent);
            }
            else if (entry.selectionMode == EventSelectionMode.RandomFromGroup)
            {
                foreach (var groupEvent in entry.eventGroup)
                {
                    if (groupEvent != null)
                        eventsToVisualize.Add(groupEvent);
                }
            }

            foreach (var gameEvent in eventsToVisualize)
            {
                // Draw spawn point
                GameObject spawnPoint = GameObject.Find(gameEvent.spawnPointName);
                if (spawnPoint != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(spawnPoint.transform.position, 0.5f);
                }

                // Draw waypoints
                Vector3? previousPos = spawnPoint?.transform.position;
                Gizmos.color = Color.yellow;

                if (gameEvent.waypoints != null)
                {
                    foreach (var waypoint in gameEvent.waypoints)
                    {
                        GameObject waypointObj = GameObject.Find(waypoint.waypointName);
                        if (waypointObj != null)
                        {
                            Gizmos.DrawWireSphere(waypointObj.transform.position, 0.3f);

                            if (previousPos.HasValue)
                            {
                                Gizmos.color = Color.cyan;
                                Gizmos.DrawLine(previousPos.Value, waypointObj.transform.position);
                                Gizmos.color = Color.yellow;
                            }

                            previousPos = waypointObj.transform.position;
                        }
                    }
                }

                // Draw exit point
                GameObject exitPoint = GameObject.Find(gameEvent.exitPointName);
                if (exitPoint != null && previousPos.HasValue)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(exitPoint.transform.position, 0.3f);
                    Gizmos.DrawLine(previousPos.Value, exitPoint.transform.position);
                }
            }
        }
    }
    #endregion
}
