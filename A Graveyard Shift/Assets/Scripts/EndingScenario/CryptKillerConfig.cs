using UnityEngine;

/// <summary>
/// Defines the behavior type for crypt killers.
/// </summary>
public enum KillerBehaviorType
{
    FastChaser,           // Roams, faster chase speed when spotted (player_mean)
    PersistentStalker,    // Always knows player location, slow but relentless (player_scared)
    FlashlightDisabler    // Roams, permanently disables flashlight on spawn (player_stupid)
}

/// <summary>
/// ScriptableObject configuration for crypt killers.
/// Allows inspector-based customization of killer behaviors.
/// </summary>
[CreateAssetMenu(fileName = "CryptKillerConfig", menuName = "Familiar/Crypt Killer Config")]
public class CryptKillerConfig : ScriptableObject
{
    [Header("Killer Identity")]
    [Tooltip("The prefab to spawn for this killer")]
    public GameObject killerPrefab;

    [Tooltip("The behavior type that determines special abilities")]
    public KillerBehaviorType behaviorType;

    [Header("Movement Speeds")]
    [Tooltip("Speed when roaming/patrolling")]
    public float roamSpeed = 2f;

    [Tooltip("Speed when chasing the player")]
    public float chaseSpeed = 5f;

    [Tooltip("Multiplier applied to chase speed for FastChaser type")]
    public float fastChaserSpeedMultiplier = 1.5f;

    [Header("Roaming Settings")]
    [Tooltip("Minimum distance to pick a new random roam point")]
    public float minRoamDistance = 5f;

    [Tooltip("Maximum distance to pick a new random roam point")]
    public float maxRoamDistance = 15f;

    [Tooltip("Time to wait at each roam point before moving to the next")]
    public float roamWaitTime = 2f;

    [Tooltip("How close the killer needs to be to a roam point to consider it reached")]
    public float roamPointReachedDistance = 1f;

    [Header("Vision Settings")]
    [Tooltip("How far the killer can see")]
    public float visionDistance = 15f;

    [Tooltip("Field of view angle (degrees from center)")]
    public float visionAngle = 60f;

    [Tooltip("Number of raycasts to use for vision cone (more = more reliable, but more expensive)")]
    [Range(3, 15)]
    public int visionRayCount = 7;

    [Tooltip("Layer mask for vision raycasts (what blocks vision)")]
    public LayerMask visionBlockingLayers;

    [Tooltip("Height offset for vision raycasts (from killer's position)")]
    public float visionHeight = 1.5f;

    [Header("Chase Persistence")]
    [Tooltip("How long the killer remembers the player's last position after losing sight")]
    public float lostSightDuration = 3f;

    [Header("Kill Settings")]
    [Tooltip("Distance at which the killer triggers game over")]
    public float killDistance = 1.5f;

    [Header("Audio - Spot Sound")]
    [Tooltip("Sound to play when the killer spots the player")]
    public AudioClip spotPlayerSound;

    [Tooltip("Volume of the spot sound")]
    [Range(0f, 1f)]
    public float spotSoundVolume = 1f;

    [Header("Terror Radius (Heartbeat)")]
    [Tooltip("Heartbeat sound that plays when player is near")]
    public AudioClip heartbeatSound;

    [Tooltip("Maximum distance at which heartbeat can be heard")]
    public float terrorRadius = 20f;

    [Tooltip("Distance at which heartbeat is at maximum volume")]
    public float terrorMaxVolumeDistance = 3f;

    [Tooltip("Maximum volume of the heartbeat")]
    [Range(0f, 1f)]
    public float heartbeatMaxVolume = 1f;

    [Header("Ambient Sound (Gurgling)")]
    [Tooltip("Ambient gurgling sound while roaming")]
    public AudioClip ambientGurglingSound;

    [Tooltip("Radius at which ambient sound can be heard")]
    public float ambientSoundRadius = 15f;

    [Tooltip("Volume of the ambient gurgling sound")]
    [Range(0f, 1f)]
    public float ambientSoundVolume = 0.5f;

    [Header("Animation")]
    [Tooltip("Animation bool for roaming/walking")]
    public string roamAnimationBool = "IsWalking";

    [Tooltip("Animation bool for chasing/running")]
    public string chaseAnimationBool = "IsRunning";

    [Tooltip("Animation trigger when spotting player")]
    public string spotPlayerTrigger = "SpotPlayer";

    [Header("Flashlight Disabler Settings")]
    [Tooltip("Dialogue text shown when player tries to use disabled flashlight")]
    [TextArea(2, 4)]
    public string flashlightDisabledDialogue = "The flashlight won't turn on...";

    [Tooltip("Speaker name for the flashlight disabled dialogue (leave empty for no speaker)")]
    public string flashlightDisabledSpeaker = "";

    [Tooltip("How long to display the flashlight disabled dialogue after typing completes")]
    public float flashlightDialogueDuration = 2f;

    [Tooltip("Characters per second for typewriter effect (0 = instant)")]
    public float flashlightDialogueTypewriterSpeed = 30f;

    [Tooltip("Cooldown before the dialogue can be shown again (0 = no cooldown)")]
    public float flashlightDialogueCooldown = 5f;
}
