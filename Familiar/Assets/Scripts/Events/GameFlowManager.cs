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
    #endregion

    #region Private Methods
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

        // Find spawn point
        GameObject spawnPoint = GameObject.Find(currentEvent.spawnPointName);
        if (spawnPoint == null)
        {
            Debug.LogError($"GameFlowManager: Spawn point '{currentEvent.spawnPointName}' not found!");
            StartNextEvent();
            return;
        }

        // Spawn NPC
        GameObject npcObject = Instantiate(currentEvent.npcPrefab,
                                           spawnPoint.transform.position,
                                           spawnPoint.transform.rotation);

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
            currentEvent.exitDialogues
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
        // Find spawn point
        GameObject spawnPoint = GameObject.Find(bgNPC.spawnPointName);
        if (spawnPoint == null)
        {
            Debug.LogError($"GameFlowManager: Background NPC spawn point '{bgNPC.spawnPointName}' not found!");
            return;
        }

        // Spawn NPC
        GameObject npcObject = Instantiate(bgNPC.npcPrefab,
                                           spawnPoint.transform.position,
                                           spawnPoint.transform.rotation);

        // Add or get EventNPC component
        EventNPC eventNPC = npcObject.GetComponent<EventNPC>();
        if (eventNPC == null)
        {
            eventNPC = npcObject.AddComponent<EventNPC>();
        }

        // Initialize the background NPC (no dialogue, no exit dialogues)
        eventNPC.Initialize(
            bgNPC.waypoints,
            null,  // No ink dialogue
            "",    // No dialogue knot
            bgNPC.exitBehavior,
            bgNPC.exitPointName,
            bgNPC.npcName,
            null   // No exit dialogues
        );

        Debug.Log($"GameFlowManager: Spawned background NPC '{bgNPC.npcName}'");
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

        // Pass previously selected events for exclusion filtering
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

        string eventName = currentEvent?.eventName ?? "Unknown";
        Debug.Log($"GameFlowManager: Event '{eventName}' completed");

        // Fire event completed
        OnEventCompleted?.Invoke(eventName);

        if (GameEventsManager.instance != null)
        {
            GameEventsManager.instance.gameFlowEvents?.EventCompleted(eventName);
        }

        currentNPC = null;
        currentEvent = null;

        // Move to next event
        StartNextEvent();
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
