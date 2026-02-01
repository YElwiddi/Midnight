using UnityEngine;

/// <summary>
/// Defines the type of side game event behavior.
/// </summary>
public enum SideGameEventType
{
    Watcher,        // NPC that stares at player, drains protection, retreats on flashlight
    // Future types can be added here (e.g., Ambient, Distraction, etc.)
}

/// <summary>
/// Defines how the watcher faces/stares while active.
/// </summary>
public enum WatcherStareMode
{
    FacePlayer,         // Continuously rotate to face the player (default behavior)
    FaceDirection,      // Face a fixed world direction (use stareDirection vector)
    FacePoint,          // Face toward a named GameObject
    UseSpawnRotation    // Keep the rotation from spawn, don't rotate
}

/// <summary>
/// Defines how the watcher retreats when flashlight is shined on them.
/// </summary>
public enum WatcherRetreatMode
{
    AwayFromPlayer,     // Move directly away from the player (default)
    Down,               // Move straight down (Y axis)
    Up,                 // Move straight up (Y axis)
    TowardPoint,        // Move toward a named GameObject
    CustomDirection,    // Move in a fixed world direction (use retreatDirection vector)
    BackwardFromFacing  // Move backward relative to current facing direction
}

/// <summary>
/// Configuration for a spawn location that can be randomly selected.
/// </summary>
[System.Serializable]
public class SpawnLocationData
{
    [Tooltip("Name of the spawn point GameObject")]
    public string spawnPointName;

    [Tooltip("Weight for random selection (higher = more likely). Default is 1.")]
    [Range(0.1f, 10f)]
    public float selectionWeight = 1f;

    [Tooltip("If true, this location will face the player on spawn")]
    public bool facePlayerOnSpawn = true;

    [Tooltip("Custom rotation offset applied after facing calculation")]
    public Vector3 rotationOffset = Vector3.zero;
}

/// <summary>
/// ScriptableObject that defines a side game event.
/// Side events run concurrently with the main game flow and are triggered randomly based on time.
/// Create via: Right-click -> Create -> Game -> Side Game Event
/// </summary>
[CreateAssetMenu(fileName = "New Side Game Event", menuName = "Game/Side Game Event")]
public class SideGameEvent : ScriptableObject
{
    [Header("Event Info")]
    [Tooltip("Unique name for this event (used for debugging and event lookup)")]
    public string eventName = "New Side Event";

    [Tooltip("Type of side game event behavior")]
    public SideGameEventType eventType = SideGameEventType.Watcher;

    [Header("Phase Restrictions")]
    [Tooltip("Game phases during which this event can spawn. Empty array = can spawn in any phase.")]
    public int[] allowedPhases = new int[0];

    [Header("NPC Configuration")]
    [Tooltip("The NPC prefab to spawn for this event")]
    public GameObject npcPrefab;

    [Tooltip("List of possible spawn locations (one will be randomly selected)")]
    public SpawnLocationData[] spawnLocations;

    [Header("Entrance Behavior")]
    [Tooltip("If true, watcher spawns offset from the spawn point and moves to it")]
    public bool useEntranceMovement = false;

    [Tooltip("Offset from spawn point where watcher actually spawns (e.g., (0, -3, 0) to spawn 3 units below)")]
    public Vector3 entranceSpawnOffset = new Vector3(0f, -3f, 0f);

    [Tooltip("Speed at which watcher moves from spawn offset to the spawn point")]
    [Range(0.5f, 10f)]
    public float entranceSpeed = 2f;

    [Tooltip("If true, watcher won't drain protection until entrance movement completes")]
    public bool waitForEntranceBeforeStaring = true;

    [Tooltip("Animation trigger to play during entrance movement (leave empty to use idle)")]
    public string entranceAnimationTrigger = "";

    [Header("Watcher Behavior (for Watcher type)")]
    [Tooltip("Protection points drained per second while staring at player")]
    [Range(0.1f, 10f)]
    public float protectionDrainPerSecond = 1f;

    [Tooltip("Maximum distance at which the watcher can see and stare at the player")]
    [Range(5f, 100f)]
    public float maxStareDistance = 50f;

    [Tooltip("If true, watcher must have line of sight to player to drain protection")]
    public bool requireLineOfSight = true;

    [Tooltip("Layer mask for line of sight checks")]
    public LayerMask lineOfSightBlockingLayers = ~0; // Default to all layers

    [Header("Stare Direction")]
    [Tooltip("How the watcher faces while staring")]
    public WatcherStareMode stareMode = WatcherStareMode.FacePlayer;

    [Tooltip("World direction to face (only used if stareMode is FaceDirection). Will be normalized.")]
    public Vector3 stareDirection = Vector3.forward;

    [Tooltip("Name of GameObject to face toward (only used if stareMode is FacePoint)")]
    public string stareTargetName = "";

    [Header("Flashlight Detection")]
    [Tooltip("Angle threshold for flashlight detection (degrees from flashlight center)")]
    [Range(5f, 45f)]
    public float flashlightDetectionAngle = 25f;

    [Tooltip("Maximum distance for flashlight to affect the watcher")]
    [Range(5f, 50f)]
    public float flashlightMaxDistance = 20f;

    [Tooltip("How long the flashlight must be on the watcher before they retreat (seconds)")]
    [Range(0f, 5f)]
    public float flashlightExposureRequired = 0.5f;

    [Tooltip("If true, flashlight must be on (not just aimed) to trigger retreat")]
    public bool requireFlashlightOn = true;

    [Tooltip("If true, flashlight can dispel watcher through walls/obstacles (ignores line of sight for flashlight only)")]
    public bool flashlightIgnoreObstacles = false;

    [Header("Retreat Behavior")]
    [Tooltip("How the watcher moves when retreating")]
    public WatcherRetreatMode retreatMode = WatcherRetreatMode.AwayFromPlayer;

    [Tooltip("World direction to retreat (only used if retreatMode is CustomDirection). Will be normalized.")]
    public Vector3 retreatDirection = Vector3.back;

    [Tooltip("Name of GameObject to retreat toward (only used if retreatMode is TowardPoint)")]
    public string retreatTargetName = "";

    [Tooltip("Speed at which the watcher retreats/moves")]
    [Range(1f, 20f)]
    public float retreatSpeed = 5f;

    [Tooltip("Distance to move before despawning during retreat (ignored if retreatMode is TowardPoint)")]
    [Range(1f, 30f)]
    public float retreatDistance = 10f;

    [Tooltip("If true, watcher fades out during retreat. If false, instant despawn after retreat distance.")]
    public bool fadeOnRetreat = true;

    [Tooltip("Time to fade out during retreat (seconds)")]
    [Range(0.5f, 5f)]
    public float fadeDuration = 1.5f;

    [Header("Retreat Animation")]
    [Tooltip("If true, triggers retreatAnimationTrigger when retreating. If false, keeps current animation.")]
    public bool useRetreatAnimation = true;

    [Tooltip("Animation trigger to play when retreating (only used if useRetreatAnimation is true)")]
    public string retreatAnimationTrigger = "Retreat";

    [Header("Audio")]
    [Tooltip("Sound to play when watcher spawns/appears")]
    public AudioClip spawnSound;

    [Tooltip("Volume for spawn sound")]
    [Range(0f, 1f)]
    public float spawnSoundVolume = 0.5f;

    [Tooltip("Ambient/staring sound that loops while watcher is active")]
    public AudioClip ambientSound;

    [Tooltip("Volume for ambient sound")]
    [Range(0f, 1f)]
    public float ambientSoundVolume = 0.3f;

    [Tooltip("Sound to play when watcher retreats")]
    public AudioClip retreatSound;

    [Tooltip("Volume for retreat sound")]
    [Range(0f, 1f)]
    public float retreatSoundVolume = 0.7f;

    [Header("Idle Sound")]
    [Tooltip("Sound to play at random intervals while watcher is active")]
    public AudioClip idleSound;

    [Tooltip("Volume for idle sound")]
    [Range(0f, 1f)]
    public float idleSoundVolume = 0.5f;

    [Tooltip("Minimum time between idle sounds (seconds)")]
    [Range(1f, 60f)]
    public float idleSoundIntervalMin = 5f;

    [Tooltip("Maximum time between idle sounds (seconds). Set equal to min for fixed interval.")]
    [Range(1f, 120f)]
    public float idleSoundIntervalMax = 15f;

    [Tooltip("If true, plays the first idle sound immediately on spawn")]
    public bool playIdleSoundOnSpawn = false;

    [Header("Visual Settings")]
    [Tooltip("Rotation speed when tracking/facing target (degrees per second). Used when stareMode is not UseSpawnRotation.")]
    [Range(1f, 360f)]
    public float playerTrackingSpeed = 90f;

    [Tooltip("Animation bool to set while staring (e.g., 'IsStaring')")]
    public string staringAnimationBool = "IsStaring";

    [Header("Timeout")]
    [Tooltip("Maximum time (seconds) the event can last before auto-despawning. 0 = no timeout.")]
    [Range(0f, 300f)]
    public float eventTimeout = 60f;

    /// <summary>
    /// Checks if this event can spawn during the specified phase.
    /// </summary>
    public bool CanSpawnInPhase(int currentPhase)
    {
        // Empty array means no phase restriction
        if (allowedPhases == null || allowedPhases.Length == 0)
        {
            return true;
        }

        foreach (int phase in allowedPhases)
        {
            if (phase == currentPhase)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a random spawn location based on weights.
    /// Returns null if no spawn locations are configured.
    /// </summary>
    public SpawnLocationData GetRandomSpawnLocation()
    {
        if (spawnLocations == null || spawnLocations.Length == 0)
        {
            Debug.LogWarning($"SideGameEvent '{eventName}': No spawn locations configured!");
            return null;
        }

        // Single location - no randomization needed
        if (spawnLocations.Length == 1)
        {
            Debug.Log($"SideGameEvent '{eventName}': Only one spawn location, using '{spawnLocations[0].spawnPointName}'");
            return spawnLocations[0];
        }

        // Calculate total weight, ensuring all weights are positive
        float totalWeight = 0f;
        foreach (var location in spawnLocations)
        {
            float weight = Mathf.Max(0.1f, location.selectionWeight); // Ensure minimum weight
            totalWeight += weight;
        }

        // Random selection based on weight
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        Debug.Log($"SideGameEvent '{eventName}': Random spawn selection - totalWeight={totalWeight}, randomValue={randomValue}");

        for (int i = 0; i < spawnLocations.Length; i++)
        {
            var location = spawnLocations[i];
            float weight = Mathf.Max(0.1f, location.selectionWeight);
            currentWeight += weight;

            Debug.Log($"  Location {i} '{location.spawnPointName}': weight={weight}, cumulative={currentWeight}, selected={randomValue < currentWeight}");

            if (randomValue < currentWeight)
            {
                Debug.Log($"SideGameEvent '{eventName}': Selected spawn location '{location.spawnPointName}'");
                return location;
            }
        }

        // Fallback to last location (shouldn't happen but safety)
        Debug.Log($"SideGameEvent '{eventName}': Fallback to last spawn location '{spawnLocations[spawnLocations.Length - 1].spawnPointName}'");
        return spawnLocations[spawnLocations.Length - 1];
    }
}
