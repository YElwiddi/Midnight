using System;
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
    [Tooltip("List of events to execute in order")]
    [SerializeField] private List<GameEvent> eventQueue = new List<GameEvent>();

    [Header("Settings")]
    [Tooltip("Automatically start the first event when the scene loads")]
    [SerializeField] private bool autoStartOnAwake = true;

    [Tooltip("Delay before starting the first event (seconds)")]
    [SerializeField] private float startDelay = 1f;

    [Tooltip("Delay between events (seconds)")]
    [SerializeField] private float eventTransitionDelay = 0.5f;
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
    /// Starts a specific event by name.
    /// </summary>
    public void StartEventByName(string eventName)
    {
        for (int i = 0; i < eventQueue.Count; i++)
        {
            if (eventQueue[i].eventName == eventName)
            {
                currentEventIndex = i;
                StartCurrentEvent();
                return;
            }
        }

        Debug.LogWarning($"GameFlowManager: Event '{eventName}' not found in queue!");
    }

    /// <summary>
    /// Adds an event to the queue.
    /// </summary>
    public void AddEvent(GameEvent gameEvent)
    {
        eventQueue.Add(gameEvent);
    }

    /// <summary>
    /// Inserts an event at a specific index.
    /// </summary>
    public void InsertEvent(int index, GameEvent gameEvent)
    {
        eventQueue.Insert(index, gameEvent);
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
    #endregion

    #region Private Methods
    private void StartCurrentEvent()
    {
        if (currentEventIndex >= eventQueue.Count)
        {
            Debug.LogWarning("GameFlowManager: No more events to start!");
            return;
        }

        currentEvent = eventQueue[currentEventIndex];

        if (currentEvent == null)
        {
            Debug.LogError($"GameFlowManager: Event at index {currentEventIndex} is null!");
            StartNextEvent();
            return;
        }

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

        // Subscribe to NPC completion event
        currentNPC.OnNPCEventCompleted += HandleNPCEventCompleted;

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
    }

    private void HandleNPCEventCompleted()
    {
        if (currentNPC != null)
        {
            currentNPC.OnNPCEventCompleted -= HandleNPCEventCompleted;
        }

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
        foreach (var gameEvent in eventQueue)
        {
            if (gameEvent == null) continue;

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
    #endregion
}
