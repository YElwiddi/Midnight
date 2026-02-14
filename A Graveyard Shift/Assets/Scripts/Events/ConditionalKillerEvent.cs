using UnityEngine;

/// <summary>
/// A special game event that checks a stat condition before playing.
/// If the condition is met, spawns a killer NPC that can end the game on player contact.
/// If the condition is not met, the event is skipped entirely.
/// </summary>
[CreateAssetMenu(fileName = "New Conditional Killer Event", menuName = "Game/Conditional Killer Event")]
public class ConditionalKillerEvent : ScriptableObject
{
    [Header("Event Info")]
    [Tooltip("Unique name for this event (used for debugging and event lookup)")]
    public string eventName = "New Killer Event";

    [Header("Condition Settings")]
    [Tooltip("Name of the stat to check (e.g., 'GraveRobberSetup', 'SpiritAngered')")]
    public string statName = "";

    [Tooltip("How to compare the stat value")]
    public StatComparison statComparison = StatComparison.GreaterOrEqual;

    [Tooltip("Value to compare against")]
    public int thresholdValue = 1;

    [Header("Sanity Condition (Optional)")]
    [Tooltip("If true, also checks sanity level. Event fires if EITHER stat OR sanity condition is met.")]
    public bool checkSanity = false;

    [Tooltip("How to compare sanity value")]
    public StatComparison sanityComparison = StatComparison.LessOrEqual;

    [Tooltip("Sanity threshold value (0-100)")]
    [Range(0, 100)]
    public int sanityThreshold = 25;

    [Header("Graveyard Protection Condition (Optional)")]
    [Tooltip("If true, also checks graveyard protection. Event fires if ANY enabled condition is met.")]
    public bool checkGraveyardProtection = false;

    [Tooltip("How to compare graveyard protection value")]
    public StatComparison protectionComparison = StatComparison.LessOrEqual;

    [Tooltip("Graveyard protection threshold value (0-100)")]
    [Range(0, 100)]
    public int protectionThreshold = 25;

    [Header("NPC Configuration")]
    [Tooltip("The killer NPC prefab to spawn for this event")]
    public GameObject killerPrefab;

    [Tooltip("If true, spawns in front of the player instead of at a spawn point")]
    public bool spawnInFrontOfPlayer = false;

    [Tooltip("Distance in front of the player to spawn (only used if spawnInFrontOfPlayer is true)")]
    [Range(0.5f, 50f)]
    public float spawnDistanceFromPlayer = 5f;

    [Tooltip("Height offset from player's Y position (only used if spawnInFrontOfPlayer is true). 0 = same height as player.")]
    public float spawnHeightOffset = 0f;

    [Tooltip("Name of the GameObject where the NPC will spawn (ignored if spawnInFrontOfPlayer is true)")]
    public string spawnPointName = "KillerSpawnPoint";

    [Header("Spawn Rotation")]
    [Tooltip("If true, the spawned NPC will face the player upon spawn")]
    public bool facePlayerOnSpawn = true;

    [Tooltip("If true, the spawned NPC will have its back to the player upon spawn (faces away)")]
    public bool backToPlayerOnSpawn = false;

    [Tooltip("Custom rotation offset (X, Y, Z) applied to the spawned NPC. Applied after face/back to player.")]
    public Vector3 spawnRotationOffset = Vector3.zero;

    [Tooltip("If true, uses the custom spawnRotationOffset instead of the spawn point's rotation")]
    public bool useCustomRotation = false;

    [Header("Waypoints")]
    [Tooltip("Ordered list of waypoints the killer NPC will travel through")]
    public WaypointData[] waypoints;

    [Header("Killer Behavior")]
    [Tooltip("If true, the game ends when the killer touches the player")]
    public bool endGameOnPlayerTouch = true;

    [Tooltip("Distance at which the killer triggers the game over (collision radius)")]
    [Range(0.5f, 5f)]
    public float killDistance = 1.5f;

    [Tooltip("If true, uses the MonsterJumpscare system for game over. If false, triggers instant game over.")]
    public bool useJumpscare = true;

    [Header("Ambush Behavior")]
    [Tooltip("If true, killer stays idle until player is close enough and looking at them, then chases")]
    public bool useAmbushMode = false;

    [Tooltip("Animation bool to set while idle/waiting (e.g., 'IsIdle', 'IsWaiting'). Set to false when chase begins.")]
    public string idleAnimationBool = "IsIdle";

    [Tooltip("Distance at which the killer can be triggered (player must be within this range)")]
    [Range(1f, 50f)]
    public float activationDistance = 10f;

    [Tooltip("If true, player must be looking at the killer to trigger the chase")]
    public bool requirePlayerLooking = true;

    [Tooltip("How long (in seconds) the player must look at the killer before it activates. 0 = instant.")]
    [Range(0f, 5f)]
    public float lookDurationRequired = 0f;

    [Tooltip("Field of view angle for detecting if player is looking at the killer (degrees from center)")]
    [Range(5f, 90f)]
    public float playerLookAngle = 30f;

    [Tooltip("Animation trigger to play when starting to chase (e.g., 'StartChase', 'Run')")]
    public string chaseAnimationTrigger = "StartChase";

    [Tooltip("Animation bool to set while chasing (e.g., 'IsRunning'). Optional.")]
    public string chaseAnimationBool = "";

    [Tooltip("Movement speed when chasing the player")]
    [Range(1f, 100f)]
    public float chaseSpeed = 6f;

    [Tooltip("If true, killer instantly reaches max speed with no acceleration ramp-up")]
    public bool instantAcceleration = false;

    [Tooltip("Looping sound to play at the killer's position while idle (stops when chase begins)")]
    public AudioClip idleLoopSound;

    [Tooltip("Volume of the idle loop sound")]
    [Range(0f, 5f)]
    public float idleLoopVolume = 0.5f;

    [Tooltip("Sound to play when the killer activates and starts chasing")]
    public AudioClip activationSound;

    [Tooltip("Volume of the activation sound")]
    [Range(0f, 1f)]
    public float activationSoundVolume = 1f;

    [Tooltip("If true, activation sound plays at constant volume (2D) instead of from the killer's position")]
    public bool activationSoundConstant = false;

    [Header("Kill Sequence Settings")]
    [Tooltip("Distance from player where killer stops to perform kill animation")]
    [Range(0.5f, 5f)]
    public float killStopDistance = 1.5f;

    [Tooltip("Animation trigger to play when killing the player (e.g., 'Kill', 'Attack')")]
    public string killAnimationTrigger = "Kill";

    [Tooltip("If true, slows down time during the kill sequence")]
    public bool useSlowMotion = true;

    [Tooltip("Time scale during kill sequence (0.1 = 10% speed, 0.5 = 50% speed)")]
    [Range(0.05f, 1f)]
    public float slowMotionTimeScale = 0.3f;

    [Tooltip("How long the slow motion lasts before game over (in real seconds, not affected by time scale)")]
    [Range(0.5f, 5f)]
    public float slowMotionDuration = 2f;

    [Header("Jumpscare Settings")]
    [Tooltip("Reference to the killer's face/head transform for camera focus")]
    public string killerFaceObjectName = "Head";

    [Tooltip("If no face transform found, camera looks at killer position + this height")]
    public float faceHeightOffset = 1.6f;

    [Tooltip("Vertical offset applied to face position (use negative to look lower, e.g., -0.2 to look at eyes instead of top of head)")]
    public float faceLookVerticalOffset = 0f;

    [Tooltip("Sound to play during kill sequence")]
    public AudioClip jumpscareSound;

    [Tooltip("Delay before game over after kill sequence (in real seconds)")]
    [Range(0f, 5f)]
    public float gameOverDelay = 2f;

    [Tooltip("Camera shake intensity during jumpscare")]
    [Range(0f, 2f)]
    public float shakeIntensity = 0.5f;

    [Tooltip("Camera shake duration")]
    [Range(0f, 5f)]
    public float shakeDuration = 2f;

    [Header("Player Position During Jumpscare")]
    [Tooltip("How much to lower the player during jumpscare so they look UP at the killer (negative = lower). Use -0.3 to -0.5 for a dramatic upward angle.")]
    [Range(-1f, 0.5f)]
    public float jumpscarePlayerHeightOffset = -0.3f;

    [Header("Flashlight Settings")]
    [Tooltip("Height offset for flashlight target (0 = killer's feet, 1.6 = typical face height)")]
    [Range(0f, 2.5f)]
    public float flashlightTargetHeight = 1.2f;

    [Tooltip("Flashlight intensity during jumpscare")]
    [Range(0.5f, 10f)]
    public float jumpscareFlashlightIntensity = 3f;

    [Tooltip("Flashlight range during jumpscare")]
    [Range(5f, 30f)]
    public float jumpscareFlashlightRange = 15f;

    [Header("Screen Effect")]
    [Tooltip("Prefab to instantiate for screen effect during kill (e.g., static overlay, glitch effect). Should be a Canvas with a full-screen image.")]
    public GameObject screenEffectPrefab;

    [Tooltip("Delay before showing screen effect (in real seconds)")]
    [Range(0f, 2f)]
    public float screenEffectDelay = 0f;

    [Header("VHS Effect Intensify (if VHSRetroFeature on camera)")]
    [Tooltip("If true, intensifies the VHS effect on the camera during kill")]
    public bool intensifyVHSOnKill = true;

    [Tooltip("Glitch intensity during kill (normal is ~0.25)")]
    [Range(0f, 1f)]
    public float killGlitchIntensity = 0.7f;

    [Tooltip("RGB shift amount during kill (normal is ~0.012)")]
    [Range(0f, 0.1f)]
    public float killRGBShift = 0.04f;

    [Tooltip("Noise intensity during kill (normal is ~0.08)")]
    [Range(0f, 0.5f)]
    public float killNoiseIntensity = 0.25f;

    [Tooltip("Scanline intensity during kill (normal is ~0.45)")]
    [Range(0f, 1f)]
    public float killScanlineIntensity = 0.8f;

    [Tooltip("Tracking noise during kill (normal is ~0.03)")]
    [Range(0f, 0.2f)]
    public float killTrackingNoise = 0.1f;

    [Header("Lantern Override")]
    [Tooltip("If true, all lanterns turn on, change color, and become locked during chase")]
    public bool overrideLanternsOnJumpscare = false;
    [Tooltip("Color to set all lanterns to")]
    public Color jumpscareLanternColor = Color.red;

    [Header("Game Over")]
    [Tooltip("Scene to load after jumpscare completes (e.g., 'MainMenu'). Leave empty to stay in current scene.")]
    public string gameOverSceneName = "MainMenu";

    [Header("Exit Behavior")]
    [Tooltip("What happens to the killer NPC after completing all waypoints (if player survives)")]
    public NPCExitBehavior exitBehavior = NPCExitBehavior.Destroy;

    [Tooltip("Name of the exit point GameObject (only used if exitBehavior is ContinueWalking)")]
    public string exitPointName = "KillerExitPoint";

    [Header("Optional Dialogue")]
    [Tooltip("Ink dialogue for the killer NPC (if you want them interactable)")]
    public TextAsset inkDialogue;

    [Tooltip("Starting knot/stitch for the dialogue")]
    public string dialogueKnot = "";

    [Tooltip("Camera look height from NPC origin during dialogue (-1 to use default ~1.6). Increase to frame face when zoomed.")]
    public float cameraHeight = -1f;

    [Tooltip("Camera FOV during dialogue (-1 to use default). Lower values = more zoomed in.")]
    public float cameraZoom = -1f;

    /// <summary>
    /// Checks if the condition for this event is met.
    /// Returns true if ANY enabled condition passes (OR logic).
    /// Conditions: stat check, sanity check, graveyard protection check.
    /// </summary>
    public bool CheckCondition()
    {
        bool hasAnyCondition = false;
        bool anyConditionPassed = false;

        // Check stat condition
        if (!string.IsNullOrEmpty(statName))
        {
            hasAnyCondition = true;
            bool statResult = CheckStatCondition();
            if (statResult)
            {
                anyConditionPassed = true;
                Debug.Log($"ConditionalKillerEvent '{eventName}': Stat condition PASSED");
            }
        }

        // Check sanity condition
        if (checkSanity)
        {
            hasAnyCondition = true;
            bool sanityResult = CheckSanityCondition();
            if (sanityResult)
            {
                anyConditionPassed = true;
                Debug.Log($"ConditionalKillerEvent '{eventName}': Sanity condition PASSED");
            }
        }

        // Check graveyard protection condition
        if (checkGraveyardProtection)
        {
            hasAnyCondition = true;
            bool protectionResult = CheckProtectionCondition();
            if (protectionResult)
            {
                anyConditionPassed = true;
                Debug.Log($"ConditionalKillerEvent '{eventName}': Protection condition PASSED");
            }
        }

        // If no conditions are configured, pass by default
        if (!hasAnyCondition)
        {
            Debug.LogWarning($"ConditionalKillerEvent '{eventName}': No conditions configured, passing by default");
            return true;
        }

        Debug.Log($"ConditionalKillerEvent '{eventName}': Final result = {anyConditionPassed}");
        return anyConditionPassed;
    }

    /// <summary>
    /// Checks the stat-based condition.
    /// </summary>
    private bool CheckStatCondition()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogWarning($"ConditionalKillerEvent '{eventName}': GameManager not found for stat check");
            return false;
        }

        int currentValue = gameManager.GetStatValue(statName);
        bool result = CompareValues(currentValue, statComparison, thresholdValue);

        Debug.Log($"ConditionalKillerEvent '{eventName}': Stat check - {statName} ({currentValue}) {statComparison} {thresholdValue} = {result}");
        return result;
    }

    /// <summary>
    /// Checks the sanity-based condition.
    /// </summary>
    private bool CheckSanityCondition()
    {
        if (SanityManager.Instance == null)
        {
            Debug.LogWarning($"ConditionalKillerEvent '{eventName}': SanityManager not found for sanity check");
            return false;
        }

        int currentSanity = SanityManager.Instance.CurrentSanity;
        bool result = CompareValues(currentSanity, sanityComparison, sanityThreshold);

        Debug.Log($"ConditionalKillerEvent '{eventName}': Sanity check - {currentSanity} {sanityComparison} {sanityThreshold} = {result}");
        return result;
    }

    /// <summary>
    /// Checks the graveyard protection condition.
    /// </summary>
    private bool CheckProtectionCondition()
    {
        if (GraveyardProtectionManager.Instance == null)
        {
            Debug.LogWarning($"ConditionalKillerEvent '{eventName}': GraveyardProtectionManager not found for protection check");
            return false;
        }

        int currentProtection = GraveyardProtectionManager.Instance.CurrentProtection;
        bool result = CompareValues(currentProtection, protectionComparison, protectionThreshold);

        Debug.Log($"ConditionalKillerEvent '{eventName}': Protection check - {currentProtection} {protectionComparison} {protectionThreshold} = {result}");
        return result;
    }

    /// <summary>
    /// Compares two integer values using the specified comparison.
    /// </summary>
    private bool CompareValues(int currentValue, StatComparison comparison, int threshold)
    {
        return comparison switch
        {
            StatComparison.Equals => currentValue == threshold,
            StatComparison.NotEquals => currentValue != threshold,
            StatComparison.GreaterThan => currentValue > threshold,
            StatComparison.LessThan => currentValue < threshold,
            StatComparison.GreaterOrEqual => currentValue >= threshold,
            StatComparison.LessOrEqual => currentValue <= threshold,
            _ => true
        };
    }
}
