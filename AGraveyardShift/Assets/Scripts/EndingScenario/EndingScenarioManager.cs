using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the ending scenario when the player enters the crypt.
/// Spawns a killer based on player stats (mean, scared, stupid) - not right away, but once
/// the player walks out of the crypt's arrival nook (the grace zone).
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

    [Header("Grace Period")]
    [Tooltip("The crypt's arrival nook. The killer only spawns once the player has been in here and steps back out - when they first enter the crypt and again after every checkpoint respawn. Leave empty to spawn the killer the moment the player enters.")]
    [SerializeField] private CryptGraceZone graceZone;

    [Header("Sanity")]
    [Tooltip("If true, restores player sanity to full when entering the crypt")]
    [SerializeField] private bool restoreSanityOnEnter = true;

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

    // Grace period: a queued spawn waits until the player has been in the nook and walked out of it
    private bool killerSpawnPending = false;
    private bool pendingSpawnIsRespawn = false;
    private bool playerSeenInGraceZone = false;
    private Transform playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!killerSpawnPending) return;

        // The good ending (3 correct graves) removes the killer for good - never bring it back
        if (GameManager.Instance != null && GameManager.Instance.GraveyardEndingTriggered)
        {
            killerSpawnPending = false;
            return;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            playerTransform = player.transform;
        }

        if (graceZone != null)
        {
            if (graceZone.Contains(playerTransform.position))
            {
                // In the nook (just arrived, or came back): the killer waits until they leave
                playerSeenInGraceZone = true;
                return;
            }

            // Not in the crypt yet (e.g. respawned at the entrance): wait for them to come down
            if (!playerSeenInGraceZone) return;
        }

        killerSpawnPending = false;
        Debug.Log("EndingScenarioManager: Player left the arrival nook - releasing the killer");
        SpawnQueuedKiller();
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

        // Restore player sanity to full when entering crypt (if enabled)
        if (restoreSanityOnEnter && SanityManager.Instance != null)
        {
            SanityManager.Instance.ResetSanity();
            Debug.Log("EndingScenarioManager: Player sanity restored to full");
        }

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

        // Spawn the appropriate killer once the player leaves the arrival nook
        QueueKillerSpawn(false);
    }

    /// <summary>
    /// Spawns the killer (for lastDominantStat) as soon as the player has been in the grace zone
    /// and stepped out of it - or right away if there is no grace zone.
    /// </summary>
    private void QueueKillerSpawn(bool isRespawn)
    {
        pendingSpawnIsRespawn = isRespawn;

        if (graceZone == null)
        {
            killerSpawnPending = false;
            SpawnQueuedKiller();
            return;
        }

        killerSpawnPending = true;
        playerSeenInGraceZone = false;
        Debug.Log("EndingScenarioManager: Killer queued - it spawns once the player leaves the arrival nook");
    }

    private void SpawnQueuedKiller()
    {
        SpawnKiller(lastDominantStat);

        // Configure a respawned killer for checkpoint respawn
        if (pendingSpawnIsRespawn && spawnedKiller != null)
        {
            KillerJumpscare jumpscare = spawnedKiller.GetComponent<KillerJumpscare>();
            if (jumpscare != null)
            {
                jumpscare.SetUseCheckpointRespawn(true);
            }
        }
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
    /// True while a killer is queued and waiting for the player to leave the arrival nook.
    /// </summary>
    public bool IsKillerSpawnPending() => killerSpawnPending;

    /// <summary>
    /// Force start the ending scenario (for testing). The killer still waits for the player to leave the nook.
    /// </summary>
    [ContextMenu("Force Start Ending Scenario")]
    public void ForceStart()
    {
        hasStarted = false;
        OnCryptEntered();
    }

    /// <summary>
    /// Spawns a queued killer right now, wherever the player is (for testing).
    /// </summary>
    [ContextMenu("Release Queued Killer Now")]
    public void ReleaseQueuedKillerNow()
    {
        if (!killerSpawnPending) return;
        killerSpawnPending = false;
        SpawnQueuedKiller();
    }

    /// <summary>
    /// Respawns the killer using the same dominant stat that was determined on crypt entry.
    /// Used by checkpoint respawn system. Like the first spawn, it waits until the player is back
    /// in the crypt and has walked out of the arrival nook.
    /// </summary>
    public void RespawnKiller()
    {
        // Destroy existing killer if present
        if (spawnedKiller != null)
        {
            Destroy(spawnedKiller);
            spawnedKiller = null;
        }

        // Also clean up any killers that might exist
        CryptKiller[] existingKillers = FindObjectsOfType<CryptKiller>();
        foreach (var killer in existingKillers)
        {
            Destroy(killer.gameObject);
        }

        // If we haven't started yet (shouldn't happen), use current stats
        if (string.IsNullOrEmpty(lastDominantStat))
        {
            if (GameManager.Instance != null)
            {
                int meanStat = GameManager.Instance.player_mean;
                int scaredStat = GameManager.Instance.player_scared;
                int stupidStat = GameManager.Instance.player_stupid;
                lastDominantStat = GetDominantStat(meanStat, scaredStat, stupidStat);
            }
            else
            {
                lastDominantStat = "mean"; // Default fallback
            }
        }

        Debug.Log($"EndingScenarioManager: Respawning killer for dominant stat '{lastDominantStat}'");
        QueueKillerSpawn(true);
    }

    /// <summary>
    /// Resets the ending scenario state so it can be triggered again.
    /// Used by checkpoint respawn system.
    /// </summary>
    public void ResetForRespawn()
    {
        // Don't reset hasStarted - we want to keep the same dominant stat
        // Just clean up the current killer reference (and any spawn still waiting on the nook)
        spawnedKiller = null;
        killerSpawnPending = false;
    }

    /// <summary>
    /// Gets the last dominant stat that was used to spawn the killer.
    /// </summary>
    public string GetLastDominantStat() => lastDominantStat;

    /// <summary>
    /// Resolves which crypt fiend stalked the player this run (from the dominant stat
    /// that spawned the killer). Returns false if no killer was spawned yet.
    /// Used by the good-ending unlock to reveal the fiend in the main-menu gallery.
    /// </summary>
    public bool TryGetEncounteredFiend(out CryptFiend fiend)
    {
        return CryptFiendInfo.TryFromDominantStat(lastDominantStat, out fiend);
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
