using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized controller that periodically turns off lanterns during specific phases.
/// Works with lanterns registered in SanityManager.
/// </summary>
public class LanternEventController : MonoBehaviour
{
    #region Singleton
    public static LanternEventController Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Phase Settings")]
    [Tooltip("Phases during which lanterns can be turned off")]
    [SerializeField] private int[] activePhases = new int[] { 2, 3 };

    [Tooltip("Phases where lantern turn-off is explicitly disabled (overrides activePhases)")]
    [SerializeField] private int[] disabledPhases = new int[] { 4 };

    [Header("Timing Settings")]
    [Tooltip("Minimum time between lantern turn-offs (seconds)")]
    [SerializeField] private float minInterval = 20f;

    [Tooltip("Maximum time between lantern turn-offs (seconds)")]
    [SerializeField] private float maxInterval = 45f;

    [Tooltip("Initial delay before first lantern can turn off (seconds)")]
    [SerializeField] private float initialDelay = 10f;

    [Header("Limits")]
    [Tooltip("Maximum number of lanterns that can be unlit at once (0 = no limit)")]
    [SerializeField] private int maxUnlitLanterns = 0;

    [Tooltip("If true, won't turn off a lantern if player is within protection range of it")]
    [SerializeField] private bool protectNearbyLanterns = false;

    [Tooltip("Distance within which a lantern is considered 'nearby' the player")]
    [SerializeField] private float nearbyDistance = 5f;

    [Header("Escalation (Optional)")]
    [Tooltip("If true, intervals get shorter over time")]
    [SerializeField] private bool enableEscalation = false;

    [Tooltip("How much to reduce interval per turn-off (seconds)")]
    [SerializeField] private float escalationReduction = 2f;

    [Tooltip("Minimum interval after escalation")]
    [SerializeField] private float minEscalatedInterval = 10f;

    [Header("Audio")]
    [Tooltip("Sound to play when a lantern turns off (played at lantern position)")]
    [SerializeField] private AudioClip lanternOffSound;

    [Tooltip("Volume for lantern off sound")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool showDebugUI = false;
    #endregion

    #region Properties
    public bool IsActive => isActive;
    public float TimeUntilNextEvent => nextEventTime - Time.time;
    public int CurrentUnlitCount => GetUnlitCount();
    #endregion

    #region Private Fields
    private bool isActive = false;
    private float nextEventTime;
    private float currentMinInterval;
    private float currentMaxInterval;
    private int turnOffCount = 0;
    private Transform playerTransform;
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
        // Cache player transform
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Initialize intervals
        currentMinInterval = minInterval;
        currentMaxInterval = maxInterval;

        // Subscribe to phase changes
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            // Check if we should be active based on current phase
            CheckPhaseActivation(GameFlowManager.Instance.CurrentPhase);
        }
    }

    private void OnDestroy()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void Update()
    {
        if (!isActive) return;

        if (Time.time >= nextEventTime)
        {
            TryTurnOffRandomLantern();
            ScheduleNextEvent();
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float yOffset = 10f;
        if (GameManager.Instance != null && GameManager.Instance.showDebugStats) yOffset += 240f;
        if (GraveyardProtectionManager.Instance != null) yOffset += 60f;
        if (SideGameEventManager.Instance != null) yOffset += 100f;
        if (SanityManager.Instance != null) yOffset += 80f;

        GUI.Box(new Rect(10, yOffset, 220, 70), "Lantern Events");
        GUI.Label(new Rect(20, yOffset + 20, 200, 20), $"Active: {isActive} | Unlit: {CurrentUnlitCount}");
        GUI.Label(new Rect(20, yOffset + 40, 200, 20), $"Next in: {(isActive ? Mathf.Max(0, TimeUntilNextEvent).ToString("F1") + "s" : "N/A")}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Manually activate the lantern event system.
    /// </summary>
    public void Activate()
    {
        if (isActive) return;

        isActive = true;
        ScheduleNextEvent(initialDelay);
        Debug.Log($"LanternEventController: Activated. First event in {initialDelay}s");
    }

    /// <summary>
    /// Manually deactivate the lantern event system.
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        Debug.Log("LanternEventController: Deactivated");
    }

    /// <summary>
    /// Reset the controller (intervals, escalation, etc.)
    /// </summary>
    public void Reset()
    {
        currentMinInterval = minInterval;
        currentMaxInterval = maxInterval;
        turnOffCount = 0;
        isActive = false;
    }

    /// <summary>
    /// Force turn off a random lantern immediately.
    /// </summary>
    public void ForceTurnOffRandom()
    {
        TryTurnOffRandomLantern();
    }

    /// <summary>
    /// Turn all lanterns back on.
    /// </summary>
    public void TurnAllLanternsOn()
    {
        var lanterns = GetRegisteredLanterns();
        foreach (var lantern in lanterns)
        {
            if (lantern != null && !lantern.IsLit)
            {
                lantern.TurnOn();
            }
        }
        Debug.Log("LanternEventController: All lanterns turned on");
    }
    #endregion

    #region Private Methods
    private void HandlePhaseChanged(int newPhase)
    {
        CheckPhaseActivation(newPhase);
    }

    private void CheckPhaseActivation(int phase)
    {
        // Check if phase is explicitly disabled
        foreach (int disabled in disabledPhases)
        {
            if (disabled == phase)
            {
                if (isActive)
                {
                    Deactivate();
                    Debug.Log($"LanternEventController: Deactivated - phase {phase} is disabled");
                }
                return;
            }
        }

        // Check if phase is in active list
        foreach (int active in activePhases)
        {
            if (active == phase)
            {
                if (!isActive)
                {
                    Activate();
                    Debug.Log($"LanternEventController: Activated - phase {phase} is active");
                }
                return;
            }
        }

        // Phase not in active list - deactivate
        if (isActive)
        {
            Deactivate();
            Debug.Log($"LanternEventController: Deactivated - phase {phase} not in active phases");
        }
    }

    private void ScheduleNextEvent(float delay = -1f)
    {
        if (delay < 0)
        {
            delay = Random.Range(currentMinInterval, currentMaxInterval);
        }
        nextEventTime = Time.time + delay;
    }

    private void TryTurnOffRandomLantern()
    {
        var lanterns = GetRegisteredLanterns();
        if (lanterns.Count == 0)
        {
            Debug.Log("LanternEventController: No lanterns registered");
            return;
        }

        // Check max unlit limit
        if (maxUnlitLanterns > 0)
        {
            int currentUnlit = GetUnlitCount();
            if (currentUnlit >= maxUnlitLanterns)
            {
                Debug.Log($"LanternEventController: Max unlit lanterns reached ({currentUnlit}/{maxUnlitLanterns})");
                return;
            }
        }

        // Get list of lit lanterns that can be turned off
        List<LanternInteractable> candidates = new List<LanternInteractable>();
        foreach (var lantern in lanterns)
        {
            if (lantern != null && lantern.IsLit)
            {
                // Check if lantern is excluded (e.g., cabin lantern)
                if (lantern.ExcludeFromRandomTurnOff)
                {
                    continue; // Skip this lantern
                }

                // Check if lantern is protected (near player)
                if (protectNearbyLanterns && playerTransform != null)
                {
                    float distance = Vector3.Distance(lantern.transform.position, playerTransform.position);
                    if (distance < nearbyDistance)
                    {
                        continue; // Skip this lantern
                    }
                }

                candidates.Add(lantern);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.Log("LanternEventController: No valid lanterns to turn off");
            return;
        }

        // Pick a random lantern
        LanternInteractable target = candidates[Random.Range(0, candidates.Count)];
        target.TurnOff();
        turnOffCount++;

        Debug.Log($"LanternEventController: Turned off lantern '{target.gameObject.name}'");

        // Play sound at lantern position
        if (lanternOffSound != null)
        {
            AudioSource.PlayClipAtPoint(lanternOffSound, target.transform.position, soundVolume);
        }

        // Apply escalation
        if (enableEscalation)
        {
            currentMinInterval = Mathf.Max(minEscalatedInterval, currentMinInterval - escalationReduction);
            currentMaxInterval = Mathf.Max(minEscalatedInterval + 5f, currentMaxInterval - escalationReduction);
            Debug.Log($"LanternEventController: Escalated - new interval range: {currentMinInterval:F1}s - {currentMaxInterval:F1}s");
        }

        // Fire global event
        if (GameEventsManager.instance?.sanityEvents != null)
        {
            GameEventsManager.instance.sanityEvents.LampStateChanged(target.gameObject.name, false);
        }
    }

    private List<LanternInteractable> GetRegisteredLanterns()
    {
        // Access SanityManager's registered lamps
        // We need to make this list accessible
        if (SanityManager.Instance != null)
        {
            return SanityManager.Instance.GetRegisteredLanterns();
        }
        return new List<LanternInteractable>();
    }

    private int GetUnlitCount()
    {
        if (SanityManager.Instance != null)
        {
            return SanityManager.Instance.GetUnlitLampCount();
        }
        return 0;
    }
    #endregion
}
