using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sanity threshold definition for triggering effects at specific sanity levels.
/// </summary>
[System.Serializable]
public class SanityThreshold
{
    [Tooltip("Sanity value at which this threshold triggers (e.g., 75, 50, 25)")]
    public int sanityValue = 50;

    [Tooltip("Unique identifier for this threshold")]
    public string thresholdId = "";

    [Tooltip("Has this threshold been crossed (going down)?")]
    [HideInInspector]
    public bool hasTriggered = false;
}

/// <summary>
/// Singleton manager that tracks player sanity.
/// Sanity starts at max (100) and decreases based on various conditions.
/// When sanity reaches 0, triggers a jumpscare event.
/// </summary>
public class SanityManager : MonoBehaviour
{
    #region Singleton
    public static SanityManager Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Sanity Settings")]
    [Tooltip("Maximum sanity value")]
    [SerializeField] private int maxSanity = 100;

    [Tooltip("Starting sanity value")]
    [SerializeField] private int startingSanity = 100;

    [Header("Thresholds")]
    [Tooltip("Sanity thresholds that trigger effects")]
    [SerializeField] private List<SanityThreshold> thresholds = new List<SanityThreshold>()
    {
        new SanityThreshold { sanityValue = 75, thresholdId = "vhs_level_1" },
        new SanityThreshold { sanityValue = 50, thresholdId = "vhs_level_2_crucifix" },
        new SanityThreshold { sanityValue = 25, thresholdId = "vhs_level_3_sprint" }
    };

    [Header("Jumpscare Settings")]
    [Tooltip("Killer event to trigger when sanity reaches 0")]
    [SerializeField] private ConditionalKillerEvent sanityDepletedJumpscare;

    [Tooltip("Delay before spawning jumpscare after sanity depletes")]
    [SerializeField] private float jumpscareDelay = 0.5f;

    [Header("Phase-Based Drain Settings")]
    [Tooltip("If true, flashlight-on-tombstone drain is enabled")]
    [SerializeField] private bool enableTombstoneDrain = true;

    [Tooltip("Phases during which flashlight-on-tombstone drain is active")]
    [SerializeField] private int[] tombstoneDrainPhases = new int[] { 1, 2, 3 };

    [Tooltip("Phases during which unlit lamp drain is active")]
    [SerializeField] private int[] lampDrainPhases = new int[] { 2 };

    [Tooltip("Sanity drain per second for each unlit lamp")]
    [SerializeField] private float drainPerUnlitLamp = 0.5f;

    [Header("Phase 3 Crucifix")]
    [Tooltip("If true, crucifix falls from door when phase 3 begins")]
    [SerializeField] private bool dropCrucifixAtPhase3Start = true;

    [Tooltip("Delay before crucifix falls after phase 3 starts (seconds)")]
    [SerializeField] private float crucifixFallDelay = 2f;

    [Tooltip("If true, checks crucifix at end of phase 3")]
    [SerializeField] private bool checkCrucifixAtPhase3End = true;

    [Header("Phase 3 Crucifix Penalty")]
    [Tooltip("If true, drains sanity when crucifix not affixed at end of phase 3")]
    [SerializeField] private bool drainSanityIfCrucifixNotAffixed = false;

    [Tooltip("Amount of sanity to drain if crucifix not affixed")]
    [SerializeField] private int crucifixNotAffixedSanityDrain = 25;

    [Tooltip("If true, triggers jumpscare when crucifix not affixed (can be combined with sanity drain)")]
    [SerializeField] private bool jumpscareIfCrucifixNotAffixed = true;

    [Tooltip("Jumpscare to spawn if crucifix not affixed at end of phase 3")]
    [SerializeField] private ConditionalKillerEvent crucifixJumpscare;

    [Header("Regeneration")]
    [Tooltip("If true, sanity regenerates when not being drained")]
    [SerializeField] private bool enableRegeneration = false;

    [Tooltip("Sanity points regenerated per second")]
    [SerializeField] private float regenerationRate = 1f;

    [Tooltip("Delay after last drain before regeneration starts")]
    [SerializeField] private float regenerationDelay = 5f;

    [Header("Debug")]
    [Tooltip("Show sanity in debug GUI")]
    [SerializeField] private bool showDebugUI = true;
    #endregion

    #region Events
    /// <summary>Fired when sanity changes. Parameters: (newValue, maxValue)</summary>
    public event Action<int, int> OnSanityChanged;

    /// <summary>Fired when sanity reaches zero</summary>
    public event Action OnSanityDepleted;

    /// <summary>Fired when a threshold is crossed going DOWN. Parameter: threshold ID</summary>
    public event Action<string> OnThresholdCrossedDown;

    /// <summary>Fired when a threshold is crossed going UP (recovery). Parameter: threshold ID</summary>
    public event Action<string> OnThresholdCrossedUp;

    /// <summary>Fired when sanity is fully restored</summary>
    public event Action OnSanityRestored;
    #endregion

    #region Properties
    public int CurrentSanity => currentSanity;
    public int MaxSanity => maxSanity;
    public float SanityPercent => maxSanity > 0 ? (float)currentSanity / maxSanity : 0f;
    public bool IsDepleted => currentSanity <= 0;
    public bool IsBeingDrained => activeDrainSources > 0;
    public int CurrentPhase => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentPhase : 0;
    #endregion

    #region Private Fields
    private int currentSanity;
    private float lastDrainTime;
    private int activeDrainSources = 0;
    private bool jumpscareTriggered = false;
    private float sanityAccumulator = 0f; // For sub-integer drain
    private List<LanternInteractable> registeredLamps = new List<LanternInteractable>();
    private int previousPhase = 0;
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

        currentSanity = startingSanity;
    }

    private void Start()
    {
        // Subscribe to phase changes
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            previousPhase = GameFlowManager.Instance.CurrentPhase;
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
        // Handle lamp drain in appropriate phases
        if (IsLampDrainActiveInCurrentPhase())
        {
            UpdateLampDrain();
        }

        // Handle regeneration
        if (enableRegeneration && activeDrainSources == 0 && currentSanity < maxSanity)
        {
            if (Time.time - lastDrainTime >= regenerationDelay)
            {
                RegenerateSanity(regenerationRate * Time.deltaTime);
            }
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float yOffset = 10f;
        if (GameManager.Instance != null && GameManager.Instance.showDebugStats) yOffset += 240f;
        if (GraveyardProtectionManager.Instance != null) yOffset += 60f;
        if (SideGameEventManager.Instance != null) yOffset += 100f;

        GUI.Box(new Rect(10, yOffset, 200, 70), "Sanity");
        GUI.Label(new Rect(20, yOffset + 20, 180, 20), $"Sanity: {currentSanity}/{maxSanity}");
        GUI.Label(new Rect(20, yOffset + 40, 180, 20), $"Phase: {CurrentPhase} | Draining: {IsBeingDrained}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Drains sanity by the specified amount.
    /// </summary>
    public void DrainSanity(int amount)
    {
        if (amount <= 0 || jumpscareTriggered) return;

        int previousValue = currentSanity;
        currentSanity = Mathf.Max(0, currentSanity - amount);
        lastDrainTime = Time.time;

        if (currentSanity != previousValue)
        {
            Debug.Log($"SanityManager: Sanity drained by {amount}. Now at {currentSanity}/{maxSanity}");
            CheckThresholds(previousValue, currentSanity);
            OnSanityChanged?.Invoke(currentSanity, maxSanity);

            if (currentSanity <= 0 && !jumpscareTriggered)
            {
                TriggerSanityDepleted();
            }
        }
    }

    /// <summary>
    /// Drains sanity per second. Call every frame.
    /// </summary>
    public void DrainSanityPerSecond(float amountPerSecond)
    {
        if (amountPerSecond <= 0 || jumpscareTriggered) return;

        sanityAccumulator += amountPerSecond * Time.deltaTime;

        if (sanityAccumulator >= 1f)
        {
            int wholeDrain = Mathf.FloorToInt(sanityAccumulator);
            sanityAccumulator -= wholeDrain;
            DrainSanity(wholeDrain);
        }

        lastDrainTime = Time.time;
    }

    /// <summary>
    /// Restores sanity by the specified amount.
    /// </summary>
    public void RestoreSanity(int amount)
    {
        if (amount <= 0) return;

        int previousValue = currentSanity;
        currentSanity = Mathf.Min(maxSanity, currentSanity + amount);

        if (currentSanity != previousValue)
        {
            Debug.Log($"SanityManager: Sanity restored by {amount}. Now at {currentSanity}/{maxSanity}");
            CheckThresholdsRecovery(previousValue, currentSanity);
            OnSanityChanged?.Invoke(currentSanity, maxSanity);

            if (currentSanity >= maxSanity)
            {
                OnSanityRestored?.Invoke();
            }
        }
    }

    /// <summary>
    /// Registers a drain source (for tracking active drains).
    /// </summary>
    public void RegisterDrainSource()
    {
        activeDrainSources++;
    }

    /// <summary>
    /// Unregisters a drain source.
    /// </summary>
    public void UnregisterDrainSource()
    {
        activeDrainSources = Mathf.Max(0, activeDrainSources - 1);
    }

    /// <summary>
    /// Checks if tombstone drain is active in the current phase.
    /// </summary>
    public bool IsTombstoneDrainActiveInCurrentPhase()
    {
        if (!enableTombstoneDrain) return false;

        int phase = CurrentPhase;
        foreach (int p in tombstoneDrainPhases)
        {
            if (p == phase) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if lamp drain is active in the current phase.
    /// </summary>
    public bool IsLampDrainActiveInCurrentPhase()
    {
        int phase = CurrentPhase;
        foreach (int p in lampDrainPhases)
        {
            if (p == phase) return true;
        }
        return false;
    }

    /// <summary>
    /// Registers a lamp for tracking.
    /// </summary>
    public void RegisterLamp(LanternInteractable lamp)
    {
        if (!registeredLamps.Contains(lamp))
        {
            registeredLamps.Add(lamp);
            Debug.Log($"SanityManager: Lamp registered. Total lamps: {registeredLamps.Count}");
        }
    }

    /// <summary>
    /// Unregisters a lamp.
    /// </summary>
    public void UnregisterLamp(LanternInteractable lamp)
    {
        registeredLamps.Remove(lamp);
    }

    /// <summary>
    /// Gets the count of unlit lamps.
    /// </summary>
    public int GetUnlitLampCount()
    {
        int count = 0;
        foreach (var lamp in registeredLamps)
        {
            if (lamp != null && !lamp.IsLit)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Gets the list of registered lanterns (for LanternEventController).
    /// </summary>
    public List<LanternInteractable> GetRegisteredLanterns()
    {
        return registeredLamps;
    }

    /// <summary>
    /// Resets sanity to starting value and restores all effects.
    /// </summary>
    public void ResetSanity()
    {
        currentSanity = startingSanity;
        jumpscareTriggered = false;
        sanityAccumulator = 0f;
        activeDrainSources = 0;

        // Reset thresholds
        foreach (var threshold in thresholds)
        {
            threshold.hasTriggered = false;
        }

        OnSanityChanged?.Invoke(currentSanity, maxSanity);

        // Fire restored event so effects controllers can reset
        OnSanityRestored?.Invoke();

        Debug.Log($"SanityManager: Sanity reset to {currentSanity}");
    }

    /// <summary>
    /// Sets sanity to a specific value.
    /// </summary>
    public void SetSanity(int value)
    {
        int previousValue = currentSanity;
        currentSanity = Mathf.Clamp(value, 0, maxSanity);

        if (currentSanity < previousValue)
        {
            CheckThresholds(previousValue, currentSanity);
        }
        else if (currentSanity > previousValue)
        {
            CheckThresholdsRecovery(previousValue, currentSanity);
        }

        OnSanityChanged?.Invoke(currentSanity, maxSanity);

        if (currentSanity <= 0 && !jumpscareTriggered)
        {
            TriggerSanityDepleted();
        }
    }

    /// <summary>
    /// Manually trigger the phase 3 crucifix check.
    /// </summary>
    public void CheckCrucifixForPhase3()
    {
        if (CrucifixController.Instance != null && !CrucifixController.Instance.IsAffixed)
        {
            Debug.Log("SanityManager: Crucifix not affixed at phase 3 end!");

            // Apply sanity drain penalty if enabled
            if (drainSanityIfCrucifixNotAffixed && crucifixNotAffixedSanityDrain > 0)
            {
                Debug.Log($"SanityManager: Draining {crucifixNotAffixedSanityDrain} sanity as crucifix penalty.");
                DrainSanity(crucifixNotAffixedSanityDrain);
            }

            // Trigger jumpscare if enabled
            if (jumpscareIfCrucifixNotAffixed && crucifixJumpscare != null && GameFlowManager.Instance != null)
            {
                Debug.Log("SanityManager: Triggering crucifix jumpscare.");
                GameFlowManager.Instance.SpawnKillerEvent(crucifixJumpscare);
            }
        }
        else
        {
            Debug.Log("SanityManager: Crucifix is affixed. Player survives phase 3.");
        }
    }
    #endregion

    #region Private Methods
    private void HandlePhaseChanged(int newPhase)
    {
        Debug.Log($"SanityManager: Phase changed from {previousPhase} to {newPhase}");

        // Check for phase 3 start (entering phase 3) - drop crucifix
        if (dropCrucifixAtPhase3Start && previousPhase != 3 && newPhase == 3)
        {
            StartCoroutine(DropCrucifixDelayed());
        }

        // Check for phase 3 end (transitioning away from phase 3)
        if (checkCrucifixAtPhase3End && previousPhase == 3 && newPhase != 3)
        {
            CheckCrucifixForPhase3();
        }

        previousPhase = newPhase;
    }

    private IEnumerator DropCrucifixDelayed()
    {
        yield return new WaitForSeconds(crucifixFallDelay);

        if (CrucifixController.Instance != null && CrucifixController.Instance.IsAffixed)
        {
            Debug.Log("SanityManager: Phase 3 started - dropping crucifix!");
            CrucifixController.Instance.Fall();

            // Fire global event
            if (GameEventsManager.instance?.sanityEvents != null)
            {
                GameEventsManager.instance.sanityEvents.CrucifixFell();
            }
        }
        else
        {
            Debug.Log("SanityManager: Phase 3 started but crucifix already fallen or not found");
        }
    }

    private void UpdateLampDrain()
    {
        int unlitCount = GetUnlitLampCount();
        if (unlitCount > 0)
        {
            float totalDrain = drainPerUnlitLamp * unlitCount;
            DrainSanityPerSecond(totalDrain);
        }
    }

    private void CheckThresholds(int oldValue, int newValue)
    {
        // Check if we crossed any thresholds going DOWN
        foreach (var threshold in thresholds)
        {
            if (!threshold.hasTriggered && oldValue > threshold.sanityValue && newValue <= threshold.sanityValue)
            {
                threshold.hasTriggered = true;
                Debug.Log($"SanityManager: Threshold '{threshold.thresholdId}' crossed (down) at sanity {threshold.sanityValue}");
                OnThresholdCrossedDown?.Invoke(threshold.thresholdId);
            }
        }
    }

    private void CheckThresholdsRecovery(int oldValue, int newValue)
    {
        // Check if we crossed any thresholds going UP (recovery)
        foreach (var threshold in thresholds)
        {
            if (threshold.hasTriggered && oldValue <= threshold.sanityValue && newValue > threshold.sanityValue)
            {
                threshold.hasTriggered = false;
                Debug.Log($"SanityManager: Threshold '{threshold.thresholdId}' crossed (up/recovery) at sanity {threshold.sanityValue}");
                OnThresholdCrossedUp?.Invoke(threshold.thresholdId);
            }
        }
    }

    private void TriggerSanityDepleted()
    {
        jumpscareTriggered = true;
        Debug.Log("SanityManager: Sanity depleted! Triggering jumpscare.");
        OnSanityDepleted?.Invoke();

        if (sanityDepletedJumpscare != null)
        {
            StartCoroutine(TriggerJumpscareDelayed());
        }
    }

    private IEnumerator TriggerJumpscareDelayed()
    {
        yield return new WaitForSeconds(jumpscareDelay);

        if (GameFlowManager.Instance != null && sanityDepletedJumpscare != null)
        {
            Debug.Log($"SanityManager: Spawning sanity jumpscare '{sanityDepletedJumpscare.eventName}'");
            GameFlowManager.Instance.SpawnKillerEvent(sanityDepletedJumpscare);
        }
    }

    private void RegenerateSanity(float amount)
    {
        sanityAccumulator += amount;
        if (sanityAccumulator >= 1f)
        {
            int wholeRegen = Mathf.FloorToInt(sanityAccumulator);
            sanityAccumulator -= wholeRegen;
            RestoreSanity(wholeRegen);
        }
    }
    #endregion
}
