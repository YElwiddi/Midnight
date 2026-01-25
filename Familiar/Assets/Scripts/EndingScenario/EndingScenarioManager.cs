using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the ending scenario when the player enters the crypt.
/// Spawns a killer based on player stats (mean, scared, stupid).
/// </summary>
public class EndingScenarioManager : MonoBehaviour
{
    public static EndingScenarioManager Instance { get; private set; }

    [Header("Killer Prefabs")]
    [Tooltip("Killer for highest player_mean - faster chase speed")]
    [SerializeField] private GameObject meanKillerPrefab;

    [Tooltip("Killer for highest player_scared - always knows player location")]
    [SerializeField] private GameObject scaredKillerPrefab;

    [Tooltip("Killer for highest player_stupid - disables flashlight")]
    [SerializeField] private GameObject stupidKillerPrefab;

    [Header("Killer Configurations (Optional)")]
    [Tooltip("Config for mean killer (FastChaser)")]
    [SerializeField] private CryptKillerConfig meanKillerConfig;

    [Tooltip("Config for scared killer (PersistentStalker)")]
    [SerializeField] private CryptKillerConfig scaredKillerConfig;

    [Tooltip("Config for stupid killer (FlashlightDisabler)")]
    [SerializeField] private CryptKillerConfig stupidKillerConfig;

    [Header("Spawn Settings")]
    [Tooltip("Where to spawn the killer in the crypt")]
    [SerializeField] private Transform killerSpawnPoint;

    [Header("Roaming Waypoints")]
    [Tooltip("Waypoints for the killer to roam between")]
    [SerializeField] private Transform[] roamWaypoints;

    [Header("Events")]
    [Tooltip("Called when the ending scenario begins")]
    public UnityEvent OnEndingScenarioStarted;

    [Tooltip("Called when a killer is spawned")]
    public UnityEvent<GameObject> OnKillerSpawned;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private string lastDominantStat = "";

    private bool hasStarted = false;
    private GameObject spawnedKiller;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Called when the player enters the crypt.
    /// Determines which killer to spawn based on player stats.
    /// </summary>
    public void OnCryptEntered()
    {
        if (hasStarted)
        {
            Debug.LogWarning("EndingScenarioManager: Ending scenario already started");
            return;
        }

        hasStarted = true;
        Debug.Log("EndingScenarioManager: Player entered crypt - starting ending scenario");

        OnEndingScenarioStarted?.Invoke();

        // Get player stats from GameManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("EndingScenarioManager: GameManager not found!");
            return;
        }

        int meanStat = GameManager.Instance.player_mean;
        int scaredStat = GameManager.Instance.player_scared;
        int stupidStat = GameManager.Instance.player_stupid;

        Debug.Log($"EndingScenarioManager: Player stats - Mean: {meanStat}, Scared: {scaredStat}, Stupid: {stupidStat}");

        // Determine dominant stat
        string dominantStat = GetDominantStat(meanStat, scaredStat, stupidStat);
        lastDominantStat = dominantStat;

        Debug.Log($"EndingScenarioManager: Dominant stat is '{dominantStat}'");

        // Spawn the appropriate killer
        SpawnKiller(dominantStat);
    }

    /// <summary>
    /// Determines which stat is highest. If there's a tie, picks randomly.
    /// </summary>
    private string GetDominantStat(int mean, int scared, int stupid)
    {
        int maxValue = Mathf.Max(mean, Mathf.Max(scared, stupid));

        // Collect all stats that match the max value
        System.Collections.Generic.List<string> tiedStats = new System.Collections.Generic.List<string>();

        if (mean == maxValue) tiedStats.Add("mean");
        if (scared == maxValue) tiedStats.Add("scared");
        if (stupid == maxValue) tiedStats.Add("stupid");

        // If there's a tie, pick randomly
        if (tiedStats.Count > 1)
        {
            int randomIndex = Random.Range(0, tiedStats.Count);
            Debug.Log($"EndingScenarioManager: Tie detected between {string.Join(", ", tiedStats)}. Randomly selected: {tiedStats[randomIndex]}");
            return tiedStats[randomIndex];
        }

        return tiedStats[0];
    }

    /// <summary>
    /// Spawns the killer based on the dominant stat.
    /// </summary>
    private void SpawnKiller(string dominantStat)
    {
        GameObject prefabToSpawn = null;
        CryptKillerConfig configToUse = null;
        KillerBehaviorType behaviorType = KillerBehaviorType.FastChaser;

        switch (dominantStat)
        {
            case "mean":
                prefabToSpawn = meanKillerPrefab;
                configToUse = meanKillerConfig;
                behaviorType = KillerBehaviorType.FastChaser;
                Debug.Log("EndingScenarioManager: Spawning FAST CHASER (player was mean)");
                break;

            case "scared":
                prefabToSpawn = scaredKillerPrefab;
                configToUse = scaredKillerConfig;
                behaviorType = KillerBehaviorType.PersistentStalker;
                Debug.Log("EndingScenarioManager: Spawning PERSISTENT STALKER (player was scared)");
                break;

            case "stupid":
                prefabToSpawn = stupidKillerPrefab;
                configToUse = stupidKillerConfig;
                behaviorType = KillerBehaviorType.FlashlightDisabler;
                Debug.Log("EndingScenarioManager: Spawning FLASHLIGHT DISABLER (player was stupid)");
                break;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"EndingScenarioManager: No prefab assigned for '{dominantStat}' killer!");
            return;
        }

        // Determine spawn position
        Vector3 spawnPosition = killerSpawnPoint != null ? killerSpawnPoint.position : transform.position;
        Quaternion spawnRotation = killerSpawnPoint != null ? killerSpawnPoint.rotation : Quaternion.identity;

        // Spawn the killer
        spawnedKiller = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        spawnedKiller.name = $"CryptKiller_{dominantStat}";

        // Get or add CryptKiller component
        CryptKiller cryptKiller = spawnedKiller.GetComponent<CryptKiller>();
        if (cryptKiller == null)
        {
            cryptKiller = spawnedKiller.AddComponent<CryptKiller>();
        }

        // Get or add KillerVision component
        KillerVision killerVision = spawnedKiller.GetComponent<KillerVision>();
        if (killerVision == null)
        {
            killerVision = spawnedKiller.AddComponent<KillerVision>();
        }

        // Initialize with config if available
        if (configToUse != null)
        {
            cryptKiller.Initialize(configToUse);
        }
        else
        {
            // Just set the behavior type
            cryptKiller.SetBehaviorType(behaviorType);
        }

        // Assign roaming waypoints
        if (roamWaypoints != null && roamWaypoints.Length > 0)
        {
            cryptKiller.SetRoamWaypoints(roamWaypoints);
        }

        Debug.Log($"EndingScenarioManager: Killer spawned at {spawnPosition}");

        OnKillerSpawned?.Invoke(spawnedKiller);
    }

    /// <summary>
    /// Gets the currently spawned killer (if any).
    /// </summary>
    public GameObject GetSpawnedKiller() => spawnedKiller;

    /// <summary>
    /// Returns true if the ending scenario has started.
    /// </summary>
    public bool HasStarted() => hasStarted;

    /// <summary>
    /// Force start the ending scenario (for testing).
    /// </summary>
    [ContextMenu("Force Start Ending Scenario")]
    public void ForceStart()
    {
        hasStarted = false;
        OnCryptEntered();
    }

    private void OnDrawGizmosSelected()
    {
        // Draw spawn point
        if (killerSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(killerSpawnPoint.position, 1f);
            Gizmos.DrawLine(killerSpawnPoint.position, killerSpawnPoint.position + killerSpawnPoint.forward * 2f);
        }
    }
}
