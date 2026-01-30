using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Singleton manager that tracks the Graveyard Protection stat.
/// Protection starts at max (100) and decreases when watchers stare at the player.
/// When protection reaches 0, triggers a jumpscare event.
/// </summary>
public class GraveyardProtectionManager : MonoBehaviour
{
    #region Singleton
    public static GraveyardProtectionManager Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Protection Settings")]
    [Tooltip("Maximum protection value")]
    [SerializeField] private int maxProtection = 100;

    [Tooltip("Starting protection value (usually same as max)")]
    [SerializeField] private int startingProtection = 100;

    [Tooltip("If true, protection regenerates over time when not being drained")]
    [SerializeField] private bool enableRegeneration = false;

    [Tooltip("Protection points regenerated per second (only if enableRegeneration is true)")]
    [SerializeField] private float regenerationRate = 1f;

    [Tooltip("Delay in seconds after last drain before regeneration starts")]
    [SerializeField] private float regenerationDelay = 5f;

    [Header("Debug")]
    [Tooltip("Show protection value in debug GUI")]
    [SerializeField] private bool showDebugUI = true;
    #endregion

    #region Events
    /// <summary>Fired when protection value changes. Parameters: (newValue, maxValue)</summary>
    public event Action<int, int> OnProtectionChanged;

    /// <summary>Fired when protection reaches zero</summary>
    public event Action OnProtectionDepleted;

    /// <summary>Fired when protection is fully restored to max</summary>
    public event Action OnProtectionRestored;
    #endregion

    #region Properties
    /// <summary>Current protection value (0 to MaxProtection)</summary>
    public int CurrentProtection => currentProtection;

    /// <summary>Maximum protection value</summary>
    public int MaxProtection => maxProtection;

    /// <summary>Protection as a percentage (0.0 to 1.0)</summary>
    public float ProtectionPercent => maxProtection > 0 ? (float)currentProtection / maxProtection : 0f;

    /// <summary>Returns true if protection is at zero</summary>
    public bool IsDepleted => currentProtection <= 0;

    /// <summary>Returns true if protection is currently being drained</summary>
    public bool IsBeingDrained => activedrainSources > 0;
    #endregion

    #region Private Fields
    private int currentProtection;
    private float lastDrainTime;
    private int activedrainSources = 0;
    private bool hasTriggeredDepletion = false;
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

        currentProtection = startingProtection;
    }

    private void Update()
    {
        // Handle regeneration if enabled and not being drained
        if (enableRegeneration && activedrainSources == 0 && currentProtection < maxProtection)
        {
            if (Time.time - lastDrainTime >= regenerationDelay)
            {
                RegenerateProtection(regenerationRate * Time.deltaTime);
            }
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        // Position below GameManager's debug UI
        float yOffset = GameManager.Instance != null && GameManager.Instance.showDebugStats ? 250f : 10f;

        GUI.Box(new Rect(10, yOffset, 200, 50), "Graveyard Protection");
        GUI.Label(new Rect(20, yOffset + 20, 180, 20), $"Protection: {currentProtection}/{maxProtection}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Drains protection by the specified amount. Called by WatcherNPCs.
    /// </summary>
    /// <param name="amount">Amount to drain (positive number)</param>
    public void DrainProtection(int amount)
    {
        if (amount <= 0 || hasTriggeredDepletion) return;

        int previousValue = currentProtection;
        currentProtection = Mathf.Max(0, currentProtection - amount);
        lastDrainTime = Time.time;

        if (currentProtection != previousValue)
        {
            Debug.Log($"GraveyardProtectionManager: Protection drained by {amount}. Now at {currentProtection}/{maxProtection}");
            OnProtectionChanged?.Invoke(currentProtection, maxProtection);

            if (currentProtection <= 0 && !hasTriggeredDepletion)
            {
                hasTriggeredDepletion = true;
                Debug.Log("GraveyardProtectionManager: Protection depleted! Triggering event.");
                OnProtectionDepleted?.Invoke();
            }
        }
    }

    /// <summary>
    /// Drains protection by the specified amount per second. Call this every frame.
    /// </summary>
    /// <param name="amountPerSecond">Amount to drain per second</param>
    public void DrainProtectionPerSecond(float amountPerSecond)
    {
        if (amountPerSecond <= 0 || hasTriggeredDepletion) return;

        float drainThisFrame = amountPerSecond * Time.deltaTime;

        // Use a fractional accumulator for sub-integer draining
        int wholeDrain = Mathf.FloorToInt(drainThisFrame);
        if (wholeDrain > 0 || Random.value < (drainThisFrame - wholeDrain))
        {
            DrainProtection(Mathf.Max(1, wholeDrain));
        }

        lastDrainTime = Time.time;
    }

    /// <summary>
    /// Restores protection by the specified amount.
    /// </summary>
    /// <param name="amount">Amount to restore (positive number)</param>
    public void RestoreProtection(int amount)
    {
        if (amount <= 0) return;

        int previousValue = currentProtection;
        currentProtection = Mathf.Min(maxProtection, currentProtection + amount);

        if (currentProtection != previousValue)
        {
            Debug.Log($"GraveyardProtectionManager: Protection restored by {amount}. Now at {currentProtection}/{maxProtection}");
            OnProtectionChanged?.Invoke(currentProtection, maxProtection);

            if (currentProtection >= maxProtection)
            {
                OnProtectionRestored?.Invoke();
            }
        }
    }

    /// <summary>
    /// Resets protection to starting value. Also resets the depletion trigger.
    /// </summary>
    public void ResetProtection()
    {
        currentProtection = startingProtection;
        hasTriggeredDepletion = false;
        activedrainSources = 0;
        Debug.Log($"GraveyardProtectionManager: Protection reset to {currentProtection}/{maxProtection}");
        OnProtectionChanged?.Invoke(currentProtection, maxProtection);
    }

    /// <summary>
    /// Registers an active drain source (e.g., a watcher staring at player).
    /// Used to track if protection should regenerate.
    /// </summary>
    public void RegisterDrainSource()
    {
        activedrainSources++;
        Debug.Log($"GraveyardProtectionManager: Drain source registered. Active sources: {activedrainSources}");
    }

    /// <summary>
    /// Unregisters an active drain source.
    /// </summary>
    public void UnregisterDrainSource()
    {
        activedrainSources = Mathf.Max(0, activedrainSources - 1);
        Debug.Log($"GraveyardProtectionManager: Drain source unregistered. Active sources: {activedrainSources}");
    }

    /// <summary>
    /// Sets protection to a specific value.
    /// </summary>
    public void SetProtection(int value)
    {
        currentProtection = Mathf.Clamp(value, 0, maxProtection);
        OnProtectionChanged?.Invoke(currentProtection, maxProtection);

        if (currentProtection <= 0 && !hasTriggeredDepletion)
        {
            hasTriggeredDepletion = true;
            OnProtectionDepleted?.Invoke();
        }
    }
    #endregion

    #region Private Methods
    private void RegenerateProtection(float amount)
    {
        if (amount <= 0) return;

        // Accumulate fractional regeneration
        int wholeRegen = Mathf.FloorToInt(amount);
        if (wholeRegen > 0 || Random.value < (amount - wholeRegen))
        {
            RestoreProtection(Mathf.Max(1, wholeRegen));
        }
    }
    #endregion
}
