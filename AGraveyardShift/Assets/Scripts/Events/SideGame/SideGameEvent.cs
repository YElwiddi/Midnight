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
    AwayFromPlayer,         // Move directly away from the player (default)
    Down,                   // Move straight down (Y axis)
    Up,                     // Move straight up (Y axis)
    TowardPoint,            // Move toward a named GameObject
    CustomDirection,        // Move in a fixed world direction (use retreatDirection vector)
    BackwardFromFacing,     // Move backward relative to current facing direction
    ReverseEntranceRotation // Rotate back to pre-entrance rotation (undo the peek)
}

/// <summary>
/// How a watcher held in the torch goes (with Use Crackle Retreat on).
/// </summary>
public enum WatcherCrackStyle
{
    NeckStretch,    // It freezes, its neck stretches out and cracks a few times, then it glides back and is gone
    HeadShake,      // A slight crack, its head cracks back and forth, then it sinks into the ground or runs off
    Vanish          // It is simply gone, the moment the torch has held it long enough: no animation, no sound
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

    [Header("Entrance Override")]
    [Tooltip("If true, this location overrides the event-level entrance settings")]
    public bool overrideEntrance = false;

    [Tooltip("If true, watcher spawns offset and moves to spawn point (location override)")]
    public bool useEntranceMovement;

    [Tooltip("Offset from spawn point where watcher actually spawns (location override)")]
    public Vector3 entranceSpawnOffset;

    [Tooltip("If true, watcher rotates from spawn rotation to offset rotation (location override)")]
    public bool useEntranceRotation;

    [Tooltip("Rotation offset applied during entrance (location override)")]
    public Vector3 entranceRotationOffset;

    [Header("Retreat Override")]
    [Tooltip("If true, this location overrides the event-level retreat settings")]
    public bool overrideRetreat = false;

    [Tooltip("How the watcher retreats at this location (location override)")]
    public WatcherRetreatMode retreatMode;

    [Tooltip("Custom retreat direction (location override, used with CustomDirection mode)")]
    public Vector3 retreatDirection;

    [Tooltip("Name of retreat target GameObject (location override, used with TowardPoint mode)")]
    public string retreatTargetName;
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

    [Tooltip("Don't spawn the watcher at a tombstone if the player is within this distance (meters, horizontal). The spawn is blocked and retried until the player moves away. Set to 0 to disable the check.")]
    public float minPlayerSpawnDistance = 8f;

    [Header("Entrance Behavior")]
    [Tooltip("If true, watcher spawns offset from the spawn point and moves to it")]
    public bool useEntranceMovement = false;

    [Tooltip("Offset from spawn point where watcher actually spawns (e.g., (0, -3, 0) to spawn 3 units below)")]
    public Vector3 entranceSpawnOffset = new Vector3(0f, -3f, 0f);

    [Tooltip("Speed at which watcher moves from spawn offset to the spawn point")]
    [Range(0.5f, 10f)]
    public float entranceSpeed = 2f;

    [Tooltip("If true, watcher spawns at normal rotation and then rotates to an offset position (e.g., peeking out from behind a tree)")]
    public bool useEntranceRotation = false;

    [Tooltip("Rotation the watcher rotates TO after spawning (e.g., (90, 0, 0) to tilt forward on X axis to peek out). Applied relative to spawn rotation.")]
    public Vector3 entranceRotationOffset = new Vector3(90f, 0f, 0f);

    [Tooltip("Speed at which watcher rotates from offset to target rotation (degrees per second)")]
    [Range(5f, 360f)]
    public float entranceRotationSpeed = 45f;

    [Tooltip("If true, watcher won't drain protection until entrance movement/rotation completes")]
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

    [Header("Caught in the Torch (neck crack, then gone)")]
    [Tooltip("If true, a watcher held in the torch does not use Retreat Behavior above. Once the torch has been on it for Flashlight Exposure Required it cracks (see Crack Style) and is gone. Looking away before that lets it recover. If false, the old retreat plays as soon as Flashlight Exposure Required is reached.")]
    public bool useCrackleRetreat = false;

    [Tooltip("Neck Stretch: it freezes, its neck stretches out and cracks a few times, then it glides back and is gone. Head Shake: a slight crack, its head cracks back and forth, then it sinks into the ground or runs off. Vanish: it is simply gone, with no animation and no sound.")]
    public WatcherCrackStyle crackStyle = WatcherCrackStyle.NeckStretch;

    [Tooltip("If true, the head follows the player while it stares (the body already turns to face you).")]
    public bool headTracksPlayer = true;

    [Header("Changed (later appearances)")]
    [Tooltip("From this appearance on (counting every watcher that has come into the yard since the scene loaded), watchers stand changed: frozen dead still, neck stretched out, head hanging over. The CCTV stand-ins follow the same count. 0 = never.")]
    [Min(0)]
    public int changedFromAppearance = 2;

    [Tooltip("How far a changed watcher's neck is stretched out (metres).")]
    [Range(0f, 1.5f)]
    public float changedNeckStretch = 0.68f;

    [Tooltip("How a changed watcher's head hangs, in degrees: x = nod down, y = turn, z = tip toward a shoulder. It still looks at you.")]
    public Vector3 changedHeadPose = new Vector3(19f, 13f, 32f);

    [Tooltip("Where in its standing animation a changed watcher is frozen (seconds; wraps round the clip). The stretch and the head pose sit on top of this pose, so it changes which way the head seems to hang.")]
    [Min(0f)]
    public float changedPoseTime = 4.27f;

    [Header("Neck Stretch (Crack Style = Neck Stretch)")]
    [Tooltip("How far its neck stretches out (metres).")]
    [Range(0f, 1.5f)]
    public float neckStretchLength = 0.55f;

    [Tooltip("Seconds the neck takes to stretch out.")]
    [Range(0.05f, 2f)]
    public float neckStretchTime = 0.4f;

    [Tooltip("How many times the stretched neck cracks. Each crack throws the head over to a new wrong angle and draws the neck out a little further.")]
    [Range(0, 8)]
    public int neckStretchCracks = 3;

    [Tooltip("Seconds between those cracks (each gap varies a little).")]
    [Range(0.05f, 1f)]
    public float neckStretchCrackGap = 0.2f;

    [Tooltip("How far each of those cracks throws the head over (degrees).")]
    [Range(0f, 70f)]
    public float neckStretchCrackAngle = 36f;

    [Tooltip("Seconds it holds still, stretched and cracked, before it goes.")]
    [Range(0f, 2f)]
    public float neckStretchHold = 0.3f;

    [Tooltip("Longest it glides back, still facing you, before it is gone (seconds). It is gone sooner if you lose sight of it or it backs into something.")]
    [Range(0f, 3f)]
    public float glideBackTime = 1f;

    [Tooltip("How hard it is pulled back (m/s per second): it starts slow and goes faster and faster.")]
    [Range(1f, 30f)]
    public float glideBackAcceleration = 9f;

    [Tooltip("The stretch and the glide move in held frames, this many a second (stop-motion). 0 = smooth.")]
    [Range(0f, 30f)]
    public float stopMotionRate = 12f;

    [Tooltip("Seconds of torn tape over the moment it vanishes, when you can still see it. 0 = it just vanishes.")]
    [Range(0f, 0.5f)]
    public float vanishGlitchTime = 0.12f;

    [Header("Head Shake (Crack Style = Head Shake)")]
    [Tooltip("How far the first crack tips its head over toward a shoulder (degrees). Kept small: a slight crack.")]
    [Range(0f, 40f)]
    public float neckCrackAngle = 15f;

    [Tooltip("Seconds it holds still after the first crack, before its head starts cracking back and forth.")]
    [Range(0f, 1.5f)]
    public float neckCrackHold = 0.35f;

    [Tooltip("Seconds its head cracks back and forth.")]
    [Range(0f, 2f)]
    public float headShakeTime = 0.5f;

    [Tooltip("Cracks a second while its head goes back and forth. Each crack throws the head over to the other side; the gaps between them vary a little.")]
    [Range(4f, 30f)]
    public float headShakeRate = 15f;

    [Tooltip("How far each of those cracks throws its head over (degrees).")]
    [Range(0f, 70f)]
    public float headShakeAngle = 48f;

    [Tooltip("Chance it sinks into the ground rather than running out of sight. With no quick way out of sight it always sinks, and if it runs and is still in sight where its run ends, it sinks there.")]
    [Range(0f, 1f)]
    public float sinkChance = 0.5f;

    [Tooltip("Seconds it takes to sink into the ground. It drops faster and faster.")]
    [Range(0.1f, 2f)]
    public float sinkTime = 0.35f;

    [Tooltip("How far down it goes before it is gone (metres). More than its height.")]
    public float sinkDepth = 3f;

    [Tooltip("All-fours run cycle used when it runs (optional; without it the watcher keeps its pose while it moves).")]
    public AnimationClip fleeClip;

    [Tooltip("Ground speed (m/s) at which Flee Clip's hands and feet do not slide when played at normal speed.")]
    public float fleeClipSpeed = 4.45f;

    [Tooltip("How fast it runs (m/s).")]
    [Range(2f, 20f)]
    public float fleeSpeed = 13f;

    [Tooltip("Furthest it runs (metres). It takes the way that gets it out of the player's sight soonest (out of the picture, or behind something), never toward the player, and is gone the moment the player cannot see it. If no way gets it out of sight within this distance, it sinks instead.")]
    [Range(4f, 40f)]
    public float fleeDistance = 16f;

    [Header("Caught in the Torch: Sound")]
    [Tooltip("The first crack of its neck (Head Shake: the slight crack; Neck Stretch: the pop as the neck lets go).")]
    public AudioClip neckCrackSound;

    [Range(0f, 1f)]
    public float neckCrackVolume = 0.8f;

    [Tooltip("One of these plays on every crack of its head or its stretched neck (never the same one twice running).")]
    public AudioClip[] headCrackSounds;

    [Range(0f, 1f)]
    public float headCrackVolume = 0.9f;

    [Tooltip("Neck Stretch: played as its neck stretches out.")]
    public AudioClip neckStretchSound;

    [Range(0f, 1f)]
    public float neckStretchVolume = 0.9f;

    [Tooltip("Neck Stretch: played the moment it vanishes (optional).")]
    public AudioClip vanishSound;

    [Range(0f, 1f)]
    public float vanishVolume = 0.8f;

    [Tooltip("Head Shake: played as it sinks into the ground.")]
    public AudioClip sinkSound;

    [Range(0f, 1f)]
    public float sinkVolume = 0.8f;

    [Tooltip("Head Shake: played once as it runs (optional; empty = it runs silently).")]
    public AudioClip fleeSound;

    [Range(0f, 1f)]
    public float fleeVolume = 0.9f;

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
