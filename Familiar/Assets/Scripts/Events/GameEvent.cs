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
    [Range(0.5f, 10f)]
    public float moveSpeed = 3f;

    [Tooltip("If true, NPC will wait for player interaction (E key) before continuing")]
    public bool waitForInteraction = false;

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
}
