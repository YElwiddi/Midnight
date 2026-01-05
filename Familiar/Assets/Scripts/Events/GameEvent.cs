using UnityEngine;

/// <summary>
/// Defines how an NPC exits after completing their event.
/// </summary>
public enum NPCExitBehavior
{
    Destroy,            // Destroy the NPC GameObject
    Disable,            // Disable the NPC GameObject (can be re-enabled later)
    ContinueWalking     // NPC continues walking to exit point and then is destroyed
}

/// <summary>
/// Configuration for a simple dialogue that plays during the NPC's exit.
/// </summary>
[System.Serializable]
public class ExitDialogueData
{
    [Tooltip("The dialogue text to display")]
    [TextArea(2, 5)]
    public string dialogueText = "";

    [Tooltip("Speaker name (leave empty for no speaker)")]
    public string speakerName = "";

    [Tooltip("Delay in seconds after the NPC starts walking away before this dialogue plays")]
    [Range(0f, 30f)]
    public float delayAfterExit = 2f;

    [Tooltip("How long the dialogue stays on screen")]
    [Range(1f, 10f)]
    public float displayDuration = 3f;
}

/// <summary>
/// Configuration for a single waypoint in the NPC's path.
/// </summary>
[System.Serializable]
public class WaypointData
{
    [Tooltip("Name of the GameObject to use as the waypoint target")]
    public string waypointName;

    [Tooltip("Movement speed to reach this waypoint")]
    [Range(0.5f, 25f)]
    public float moveSpeed = 3f;

    [Tooltip("If true, NPC will wait for player interaction (E key) before continuing")]
    public bool waitForInteraction = false;

    [Tooltip("Time in seconds before the NPC becomes interactable after arriving (only used if waitForInteraction is true)")]
    [Range(0f, 30f)]
    public float timeUntilInteractable = 0f;

    [Tooltip("Simple dialogue shown if player tries to interact before timeUntilInteractable has passed")]
    [TextArea(1, 3)]
    public string waitingDialogueText = "";

    [Tooltip("Speaker name for the waiting dialogue (leave empty for no speaker)")]
    public string waitingDialogueSpeaker = "";

    [Tooltip("Time to wait at this waypoint before continuing (ignored if waitForInteraction is true)")]
    [Range(0f, 30f)]
    public float waitTime = 0f;

    [Header("Dialogue (Optional - overrides event default)")]
    [Tooltip("Ink dialogue to use at this waypoint. If empty, uses the event's default dialogue.")]
    public TextAsset inkDialogue;

    [Tooltip("Knot/stitch to start from in the dialogue. If empty, starts from beginning or 'start' knot.")]
    public string dialogueKnot = "";

    [Header("Animation Settings")]
    [Tooltip("Animation trigger to set when moving TO this waypoint (e.g., 'Run', 'Sneak'). Leave empty for default walk.")]
    public string movementAnimationTrigger = "";

    [Tooltip("Animation trigger to play once when arriving at this waypoint (e.g., 'Kneel', 'Wave')")]
    public string arrivalAnimationTrigger = "";

    [Tooltip("Animation bool to set while idle/waiting at this waypoint (e.g., 'IsKneeling'). Will be set to false when leaving.")]
    public string idleAnimationBool = "";

    [Header("Branching (Optional)")]
    [Tooltip("Event variable name to check after this waypoint completes (e.g., 'allowed_inside'). Set via Ink: ~ SetEventVar(\"allowed_inside\", true)")]
    public string branchVariable = "";

    [Tooltip("Value to compare against (e.g., 'true', 'false', or any string)")]
    public string branchValue = "";

    [Tooltip("Name of the waypoint to jump to if condition is met. Must match another waypoint's waypointName.")]
    public string branchToWaypoint = "";

    [Tooltip("Optional: Unique ID for this waypoint (used as a branch target). If empty, waypointName is used.")]
    public string waypointId = "";

    [Header("Proximity Sound (Optional)")]
    [Tooltip("Sound to play when player sees NPC and is within range")]
    public AudioClip proximitySound;

    [Tooltip("Maximum distance from player to trigger the sound")]
    [Range(1f, 50f)]
    public float proximitySoundRange = 10f;

    [Tooltip("Volume of the proximity sound")]
    [Range(0f, 1f)]
    public float proximitySoundVolume = 1f;

    [Tooltip("When to check for proximity sound")]
    public ProximitySoundTrigger proximitySoundTrigger = ProximitySoundTrigger.None;

    [Tooltip("If true, sound only plays when player is looking at the NPC. If false, plays when in range regardless of view direction.")]
    public bool requirePlayerLooking = true;
}

/// <summary>
/// When the proximity sound should be checked/played.
/// </summary>
public enum ProximitySoundTrigger
{
    None,               // No proximity sound for this waypoint
    WhileMoving,        // Play while NPC is moving TO this waypoint
    WhileAtWaypoint,    // Play while NPC is idle/waiting AT this waypoint
    Both                // Play during both moving and waiting
}

/// <summary>
/// Defines when a background NPC should spawn.
/// </summary>
public enum BackgroundNPCSpawnTrigger
{
    OnEventStart,       // Spawn when the main event starts (with optional delay)
    OnWaypointReached   // Spawn when the main NPC reaches a specific waypoint
}

/// <summary>
/// Configuration for a background NPC that spawns concurrently with the main event.
/// These NPCs run independently and don't block the main event flow.
/// </summary>
[System.Serializable]
public class BackgroundNPCData
{
    [Tooltip("Name for this background NPC (for debugging)")]
    public string npcName = "Background NPC";

    [Tooltip("The NPC prefab to spawn")]
    public GameObject npcPrefab;

    [Tooltip("Name of the GameObject where this NPC will spawn")]
    public string spawnPointName;

    [Header("Spawn Trigger")]
    [Tooltip("When to spawn this background NPC")]
    public BackgroundNPCSpawnTrigger spawnTrigger = BackgroundNPCSpawnTrigger.OnEventStart;

    [Tooltip("Delay in seconds before spawning (used with OnEventStart trigger)")]
    [Range(0f, 60f)]
    public float spawnDelay = 0f;

    [Tooltip("Name of the waypoint that triggers spawning (used with OnWaypointReached trigger)")]
    public string triggerWaypointName = "";

    [Header("Movement")]
    [Tooltip("Ordered list of waypoints this NPC will travel through")]
    public WaypointData[] waypoints;

    [Tooltip("What happens to this NPC after completing all waypoints")]
    public NPCExitBehavior exitBehavior = NPCExitBehavior.Destroy;

    [Tooltip("Name of the exit point GameObject (only used if exitBehavior is ContinueWalking)")]
    public string exitPointName = "";
}

/// <summary>
/// ScriptableObject that defines a game event with NPC spawning, movement, and dialogue.
/// Create via: Right-click → Create → Game → Game Event
/// </summary>
[CreateAssetMenu(fileName = "New Game Event", menuName = "Game/Game Event")]
public class GameEvent : ScriptableObject
{
    [Header("Event Info")]
    [Tooltip("Unique name for this event (used for debugging and event lookup)")]
    public string eventName = "New Event";

    [Header("NPC Configuration")]
    [Tooltip("The NPC prefab to spawn for this event")]
    public GameObject npcPrefab;

    [Tooltip("Name of the GameObject where the NPC will spawn")]
    public string spawnPointName = "NPCLeftPoint";

    [Header("Waypoints")]
    [Tooltip("Ordered list of waypoints the NPC will travel through")]
    public WaypointData[] waypoints;

    [Header("Dialogue")]
    [Tooltip("The compiled Ink JSON file for this NPC's dialogue")]
    public TextAsset inkDialogue;

    [Tooltip("Optional: specific knot/stitch to start dialogue from")]
    public string dialogueKnot = "";

    [Header("Exit Behavior")]
    [Tooltip("What happens to the NPC after completing all waypoints")]
    public NPCExitBehavior exitBehavior = NPCExitBehavior.ContinueWalking;

    [Tooltip("Name of the exit point GameObject (only used if exitBehavior is ContinueWalking)")]
    public string exitPointName = "NPCExitPoint";

    [Header("Exit Dialogues")]
    [Tooltip("Simple dialogues that play while the NPC is walking away (after main dialogue)")]
    public ExitDialogueData[] exitDialogues;

    [Header("Background NPCs (Concurrent)")]
    [Tooltip("Additional NPCs that spawn and run concurrently with this event. They don't block the main event flow.")]
    public BackgroundNPCData[] backgroundNPCs;
}
