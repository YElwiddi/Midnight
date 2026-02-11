using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that runs side game events concurrently with the main game flow.
/// Events are time-based and randomly triggered, respecting the current game phase.
/// Only one side event can be active at a time.
/// </summary>
public class SideGameEventManager : MonoBehaviour
{
    #region Singleton
    public static SideGameEventManager Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Event Pool")]
    [Tooltip("List of side game events that can be randomly triggered")]
    [SerializeField] private List<SideGameEvent> eventPool = new List<SideGameEvent>();

    [Header("Timing Settings")]
    [Tooltip("Minimum time between side events (seconds)")]
    [SerializeField] private float minTimeBetweenEvents = 30f;

    [Tooltip("Maximum time between side events (seconds)")]
    [SerializeField] private float maxTimeBetweenEvents = 90f;

    [Tooltip("Initial delay before first side event can occur (seconds)")]
    [SerializeField] private float initialDelay = 60f;

    [Tooltip("If true, the timer resets after each event completes. If false, timer runs continuously.")]
    [SerializeField] private bool resetTimerAfterEvent = true;

    [Header("Phase Settings")]
    [Tooltip("If true, side events only spawn when the game phase is > 0")]
    [SerializeField] private bool requirePhaseGreaterThanZero = true;

    [Tooltip("Phases during which side events are completely disabled")]
    [SerializeField] private int[] disabledPhases = new int[0];

    [Header("Jumpscare Settings")]
    [Tooltip("Killer event to trigger when graveyard protection is depleted")]
    [SerializeField] private ConditionalKillerEvent protectionDepletedJumpscare;

    [Tooltip("Delay before spawning jumpscare after protection depletes (seconds)")]
    [SerializeField] private float jumpscareDelay = 0.5f;

    [Header("Control")]
    [Tooltip("If true, side events start automatically")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("If true, side events are currently enabled")]
    [SerializeField] private bool isEnabled = true;

    [Header("Debug")]
    [Tooltip("Show debug info in GUI")]
    [SerializeField] private bool showDebugUI = true;
    #endregion

    #region Events
    /// <summary>Fired when a side event starts. Parameter is the event name.</summary>
    public event Action<string> OnSideEventStarted;

    /// <summary>Fired when a side event completes. Parameter is the event name.</summary>
    public event Action<string> OnSideEventCompleted;
    #endregion

    #region Properties
    /// <summary>Returns true if a side event is currently active.</summary>
    public bool IsEventActive => currentEvent != null;

    /// <summary>Returns the current active side event (null if none).</summary>
    public SideGameEvent CurrentEvent => currentEvent;

    /// <summary>Returns true if side events are enabled.</summary>
    public bool IsEnabled => isEnabled;

    /// <summary>Time until next event attempt (may not spawn if conditions not met).</summary>
    public float TimeUntilNextEvent => nextEventTime - Time.time;
    #endregion

    #region Private Fields
    private SideGameEvent currentEvent;
    private WatcherNPC currentWatcher;
    private float nextEventTime;
    private bool hasStarted = false;
    private bool jumpscareTriggered = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Subscribe to protection depletion
        if (GraveyardProtectionManager.Instance != null)
        {
            GraveyardProtectionManager.Instance.OnProtectionDepleted += HandleProtectionDepleted;
        }

        // Subscribe to phase changes
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        if (autoStart)
        {
            StartSideEvents();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GraveyardProtectionManager.Instance != null)
        {
            GraveyardProtectionManager.Instance.OnProtectionDepleted -= HandleProtectionDepleted;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void Update()
    {
        if (!hasStarted || !isEnabled) return;

        // Check if it's time to attempt spawning an event
        if (currentEvent == null && Time.time >= nextEventTime)
        {
            TrySpawnRandomEvent();
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        // Position below other debug UIs
        float yOffset = 10f;
        if (GameManager.Instance != null && GameManager.Instance.showDebugStats) yOffset += 240f;
        if (GraveyardProtectionManager.Instance != null) yOffset += 60f;

        int currentPhase = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentPhase : 0;

        GUI.Box(new Rect(10, yOffset, 220, 90), "Side Game Events");
        GUI.Label(new Rect(20, yOffset + 20, 200, 20), $"Enabled: {isEnabled} | Phase: {currentPhase}");
        GUI.Label(new Rect(20, yOffset + 40, 200, 20), $"Active Event: {(currentEvent != null ? currentEvent.eventName : "None")}");
        GUI.Label(new Rect(20, yOffset + 60, 200, 20), $"Next Event In: {(currentEvent == null ? Mathf.Max(0, TimeUntilNextEvent).ToString("F1") + "s" : "N/A")}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Starts the side event system. Called automatically if autoStart is true.
    /// </summary>
    public void StartSideEvents()
    {
        if (hasStarted) return;

        hasStarted = true;
        ScheduleNextEvent(initialDelay);
        Debug.Log($"SideGameEventManager: Started. First event possible in {initialDelay}s");
    }

    /// <summary>
    /// Enables or disables side events.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        Debug.Log($"SideGameEventManager: {(enabled ? "Enabled" : "Disabled")}");

        if (!enabled && currentEvent != null)
        {
            // Cancel current event
            CancelCurrentEvent();
        }
    }

    /// <summary>
    /// Forces a specific side event to spawn immediately (ignores phase and timing).
    /// </summary>
    public void ForceSpawnEvent(SideGameEvent sideEvent)
    {
        if (sideEvent == null)
        {
            Debug.LogWarning("SideGameEventManager: Cannot force spawn null event!");
            return;
        }

        // Cancel any current event
        if (currentEvent != null)
        {
            CancelCurrentEvent();
        }

        SpawnEvent(sideEvent);
    }

    /// <summary>
    /// Forces a random side event to spawn immediately (still respects phase restrictions).
    /// </summary>
    public void ForceSpawnRandomEvent()
    {
        TrySpawnRandomEvent();
    }

    /// <summary>
    /// Cancels the current side event if one is active.
    /// </summary>
    public void CancelCurrentEvent()
    {
        if (currentEvent == null) return;

        Debug.Log($"SideGameEventManager: Cancelling event '{currentEvent.eventName}'");

        if (currentWatcher != null)
        {
            currentWatcher.OnEventCompleted -= HandleEventCompleted;
            currentWatcher.ForceDespawn();
            currentWatcher = null;
        }

        string eventName = currentEvent.eventName;
        currentEvent = null;

        OnSideEventCompleted?.Invoke(eventName);

        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.SideEventCompleted(eventName);
        }

        if (resetTimerAfterEvent)
        {
            ScheduleNextEvent();
        }
    }

    /// <summary>
    /// Adds an event to the pool at runtime.
    /// </summary>
    public void AddEventToPool(SideGameEvent sideEvent)
    {
        if (sideEvent != null && !eventPool.Contains(sideEvent))
        {
            eventPool.Add(sideEvent);
        }
    }

    /// <summary>
    /// Removes an event from the pool at runtime.
    /// </summary>
    public void RemoveEventFromPool(SideGameEvent sideEvent)
    {
        eventPool.Remove(sideEvent);
    }

    /// <summary>
    /// Resets the side event system (clears current event, resets timer).
    /// </summary>
    public void Reset()
    {
        CancelCurrentEvent();
        jumpscareTriggered = false;
        ScheduleNextEvent(initialDelay);
    }
    #endregion

    #region Private Methods
    private void ScheduleNextEvent(float delay = -1f)
    {
        if (delay < 0)
        {
            delay = UnityEngine.Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
        }
        nextEventTime = Time.time + delay;
        Debug.Log($"SideGameEventManager: Next event scheduled in {delay:F1}s");
    }

    private void TrySpawnRandomEvent()
    {
        // Get current phase
        int currentPhase = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentPhase : 0;

        // Check if phase requirements are met
        if (requirePhaseGreaterThanZero && currentPhase <= 0)
        {
            Debug.Log($"SideGameEventManager: Skipping event spawn - phase {currentPhase} <= 0");
            ScheduleNextEvent();
            return;
        }

        // Check if current phase is disabled
        foreach (int disabledPhase in disabledPhases)
        {
            if (disabledPhase == currentPhase)
            {
                Debug.Log($"SideGameEventManager: Skipping event spawn - phase {currentPhase} is disabled");
                ScheduleNextEvent();
                return;
            }
        }

        // Get events valid for current phase
        List<SideGameEvent> validEvents = GetValidEventsForPhase(currentPhase);

        if (validEvents.Count == 0)
        {
            Debug.Log($"SideGameEventManager: No valid events for phase {currentPhase}");
            ScheduleNextEvent();
            return;
        }

        // Select random event
        SideGameEvent selectedEvent = validEvents[UnityEngine.Random.Range(0, validEvents.Count)];
        SpawnEvent(selectedEvent);
    }

    private List<SideGameEvent> GetValidEventsForPhase(int phase)
    {
        List<SideGameEvent> valid = new List<SideGameEvent>();

        foreach (SideGameEvent evt in eventPool)
        {
            if (evt != null && evt.CanSpawnInPhase(phase))
            {
                valid.Add(evt);
            }
        }

        return valid;
    }

    private void SpawnEvent(SideGameEvent sideEvent)
    {
        if (sideEvent.npcPrefab == null)
        {
            Debug.LogError($"SideGameEventManager: Event '{sideEvent.eventName}' has no NPC prefab!");
            ScheduleNextEvent();
            return;
        }

        // Get spawn location
        SpawnLocationData spawnLocation = sideEvent.GetRandomSpawnLocation();
        if (spawnLocation == null)
        {
            Debug.LogError($"SideGameEventManager: Event '{sideEvent.eventName}' has no valid spawn locations!");
            ScheduleNextEvent();
            return;
        }

        // Find spawn point
        GameObject spawnPoint = GameObject.Find(spawnLocation.spawnPointName);
        if (spawnPoint == null)
        {
            Debug.LogError($"SideGameEventManager: Spawn point '{spawnLocation.spawnPointName}' not found!");
            ScheduleNextEvent();
            return;
        }

        // Calculate spawn position (with entrance offset if enabled)
        Vector3 targetPosition = spawnPoint.transform.position;
        Vector3 actualSpawnPosition = targetPosition;

        if (sideEvent.useEntranceMovement)
        {
            // Apply entrance offset - spawn at offset position, will move to target
            actualSpawnPosition = targetPosition + sideEvent.entranceSpawnOffset;
            Debug.Log($"SideGameEventManager: Using entrance movement - spawning at {actualSpawnPosition}, target is {targetPosition}");
        }

        // Calculate spawn rotation
        Quaternion spawnRotation = CalculateSpawnRotation(spawnPoint.transform, spawnLocation);

        // Spawn NPC at the actual spawn position (may be offset)
        GameObject npcObject = Instantiate(sideEvent.npcPrefab, actualSpawnPosition, spawnRotation);

        // Set up based on event type
        switch (sideEvent.eventType)
        {
            case SideGameEventType.Watcher:
                SetupWatcherNPC(npcObject, sideEvent, targetPosition);
                break;
            // Future event types can be handled here
        }

        currentEvent = sideEvent;

        // Fire events
        OnSideEventStarted?.Invoke(sideEvent.eventName);

        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.SideEventStarted(sideEvent.eventName);
        }

        Debug.Log($"SideGameEventManager: Spawned event '{sideEvent.eventName}' at '{spawnLocation.spawnPointName}'");
    }

    private Quaternion CalculateSpawnRotation(Transform spawnPoint, SpawnLocationData locationData)
    {
        Quaternion baseRotation;

        if (locationData.facePlayerOnSpawn)
        {
            // Face the player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector3 directionToPlayer = player.transform.position - spawnPoint.position;
                directionToPlayer.y = 0;

                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    baseRotation = Quaternion.LookRotation(directionToPlayer);
                }
                else
                {
                    baseRotation = spawnPoint.rotation;
                }
            }
            else
            {
                baseRotation = spawnPoint.rotation;
            }
        }
        else
        {
            baseRotation = spawnPoint.rotation;
        }

        // Apply rotation offset
        if (locationData.rotationOffset != Vector3.zero)
        {
            baseRotation *= Quaternion.Euler(locationData.rotationOffset);
        }

        return baseRotation;
    }

    private void SetupWatcherNPC(GameObject npcObject, SideGameEvent sideEvent, Vector3 entranceTargetPosition)
    {
        // Add or get WatcherNPC component
        currentWatcher = npcObject.GetComponent<WatcherNPC>();
        if (currentWatcher == null)
        {
            currentWatcher = npcObject.AddComponent<WatcherNPC>();
        }

        // Subscribe to completion
        currentWatcher.OnEventCompleted += HandleEventCompleted;

        // Set entrance target BEFORE Initialize if using entrance movement
        // (Initialize will start the entrance immediately if enabled)
        if (sideEvent.useEntranceMovement)
        {
            currentWatcher.SetEntranceTarget(entranceTargetPosition);
        }

        // Initialize (will start entrance or staring based on config)
        currentWatcher.Initialize(sideEvent);
    }

    private void HandleEventCompleted()
    {
        if (currentWatcher != null)
        {
            currentWatcher.OnEventCompleted -= HandleEventCompleted;
            currentWatcher = null;
        }

        string eventName = currentEvent != null ? currentEvent.eventName : "Unknown";
        currentEvent = null;

        Debug.Log($"SideGameEventManager: Event '{eventName}' completed");

        OnSideEventCompleted?.Invoke(eventName);

        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.SideEventCompleted(eventName);
        }

        if (resetTimerAfterEvent)
        {
            ScheduleNextEvent();
        }
    }

    private void HandleProtectionDepleted()
    {
        if (jumpscareTriggered) return;
        jumpscareTriggered = true;

        Debug.Log("SideGameEventManager: Protection depleted! Triggering jumpscare...");

        // Notify event system
        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.ProtectionDepletedJumpscare();
        }

        // Cancel any current side event
        if (currentEvent != null)
        {
            CancelCurrentEvent();
        }

        // Disable further side events
        isEnabled = false;

        // Trigger jumpscare after delay
        if (protectionDepletedJumpscare != null)
        {
            StartCoroutine(TriggerJumpscareDelayed());
        }
    }

    private IEnumerator TriggerJumpscareDelayed()
    {
        yield return new WaitForSeconds(jumpscareDelay);

        if (GameFlowManager.Instance != null && protectionDepletedJumpscare != null)
        {
            Debug.Log($"SideGameEventManager: Spawning jumpscare '{protectionDepletedJumpscare.eventName}'");

            // Use GameFlowManager to spawn the killer event directly (doesn't need to be in queue)
            GameFlowManager.Instance.SpawnKillerEvent(protectionDepletedJumpscare);
        }
    }

    private void HandlePhaseChanged(int newPhase)
    {
        Debug.Log($"SideGameEventManager: Phase changed to {newPhase}");

        // Notify event system
        if (GameEventsManager.instance?.sideGameEvents != null)
        {
            GameEventsManager.instance.sideGameEvents.PhaseChanged(newPhase);
        }

        // Check if current event should be cancelled due to phase change
        if (currentEvent != null && !currentEvent.CanSpawnInPhase(newPhase))
        {
            Debug.Log($"SideGameEventManager: Cancelling event '{currentEvent.eventName}' - not valid for phase {newPhase}");
            CancelCurrentEvent();
        }
    }
    #endregion
}
