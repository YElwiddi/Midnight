using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC states for event-driven NPCs.
/// </summary>
public enum NPCState
{
    Idle,
    Walking,
    WaitingForInteraction,
    InDialogue,
    Exiting
}

/// <summary>
/// Component for NPCs that are part of game events.
/// Handles waypoint-based movement using NavMeshAgent, dialogue interaction, and exit behavior.
/// This component is added at runtime by GameFlowManager.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EventNPC : MonoBehaviour, IInteractable
{
    #region Runtime Configuration (Set by GameFlowManager)
    [HideInInspector] public WaypointData[] waypoints;
    [HideInInspector] public TextAsset inkDialogue;
    [HideInInspector] public string dialogueKnot;
    [HideInInspector] public NPCExitBehavior exitBehavior;
    [HideInInspector] public string exitPointName;
    #endregion

    #region Inspector Settings
    [Header("Interaction Settings")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private string interactionPrompt = "Talk";

    [Header("Movement Settings")]
    [SerializeField] private float arrivalThreshold = 0.5f;
    #endregion

    #region Events
    /// <summary>Fired when this NPC has completed its entire event sequence.</summary>
    public event Action OnNPCEventCompleted;
    #endregion

    #region Private Fields
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    private NavMeshAgent agent;
    private Animator animator;
    private DialogueManager dialogueManager;

    private int currentWaypointIndex = 0;
    private NPCState currentState = NPCState.Idle;
    private Transform currentTargetWaypoint;
    private bool hasCompletedDialogue = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Look for Animator on this object or any children (model is often a child)
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"EventNPC: No Animator found on {gameObject.name} or its children!");
        }
        else
        {
            Debug.Log($"EventNPC: Found Animator on {animator.gameObject.name}");
        }

        // Set layer for interaction raycast
        gameObject.layer = LayerMask.NameToLayer("Interactable");

        // Ensure collider exists
        if (GetComponent<Collider>() == null)
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2f, 1f);
            col.center = new Vector3(0f, 1f, 0f);
        }
    }

    private void Start()
    {
        dialogueManager = DialogueManager.GetInstance();

        if (dialogueManager != null)
        {
            dialogueManager.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnDestroy()
    {
        if (dialogueManager != null)
        {
            dialogueManager.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    private void Update()
    {
        switch (currentState)
        {
            case NPCState.Walking:
            case NPCState.Exiting:
                CheckArrival();
                break;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Initializes the NPC with event data and starts the waypoint sequence.
    /// Called by GameFlowManager after spawning.
    /// </summary>
    public void Initialize(WaypointData[] waypointData, TextAsset dialogue, string knot,
                           NPCExitBehavior exit, string exitPoint, string name = null)
    {
        waypoints = waypointData;
        inkDialogue = dialogue;
        dialogueKnot = knot;
        exitBehavior = exit;
        exitPointName = exitPoint;

        if (!string.IsNullOrEmpty(name))
        {
            npcName = name;
        }

        // Start moving to first waypoint
        if (waypoints != null && waypoints.Length > 0)
        {
            MoveToNextWaypoint();
        }
        else
        {
            Debug.LogWarning($"EventNPC {npcName}: No waypoints configured!");
            CompleteEvent();
        }
    }

    /// <summary>
    /// Sets the NPC's display name.
    /// </summary>
    public void SetNPCName(string name)
    {
        npcName = name;
    }
    #endregion

    #region IInteractable Implementation
    public void Interact()
    {
        if (currentState != NPCState.WaitingForInteraction)
        {
            Debug.Log($"EventNPC {npcName}: Cannot interact - NPC is not waiting for interaction");
            return;
        }

        if (dialogueManager == null)
        {
            dialogueManager = DialogueManager.GetInstance();
            if (dialogueManager == null)
            {
                Debug.LogError("EventNPC: DialogueManager not found!");
                return;
            }
        }

        if (inkDialogue == null)
        {
            Debug.LogError($"EventNPC {npcName}: No Ink dialogue assigned!");
            return;
        }

        if (dialogueManager.IsDialoguePlaying())
        {
            return;
        }

        // Cancel any active simple dialogue first
        SimpleDialogueTrigger.CancelActiveSimpleDialogue();

        // Face the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        currentState = NPCState.InDialogue;
        Debug.Log($"EventNPC {npcName}: Starting dialogue");

        // Start dialogue
        if (!string.IsNullOrEmpty(dialogueKnot))
        {
            dialogueManager.EnterDialogueMode(inkDialogue, dialogueKnot, transform);
        }
        else
        {
            dialogueManager.EnterDialogueMode(inkDialogue, transform);
        }
    }

    public string GetInteractionPrompt()
    {
        return $"{interactionPrompt} to {npcName}";
    }
    #endregion

    #region Private Methods
    private void MoveToNextWaypoint()
    {
        if (currentWaypointIndex >= waypoints.Length)
        {
            HandleWaypointsCompleted();
            return;
        }

        WaypointData waypoint = waypoints[currentWaypointIndex];
        GameObject waypointObj = GameObject.Find(waypoint.waypointName);

        if (waypointObj == null)
        {
            Debug.LogError($"EventNPC {npcName}: Waypoint '{waypoint.waypointName}' not found!");
            currentWaypointIndex++;
            MoveToNextWaypoint();
            return;
        }

        currentTargetWaypoint = waypointObj.transform;
        agent.speed = waypoint.moveSpeed;
        agent.SetDestination(currentTargetWaypoint.position);

        SetWalkingState(true);
        currentState = NPCState.Walking;

        Debug.Log($"EventNPC {npcName}: Moving to waypoint '{waypoint.waypointName}' at speed {waypoint.moveSpeed}");
    }

    private void CheckArrival()
    {
        if (agent.pathPending) return;

        float remainingDistance = agent.remainingDistance;

        if (remainingDistance <= arrivalThreshold)
        {
            agent.ResetPath();
            SetWalkingState(false);

            if (currentState == NPCState.Exiting)
            {
                CompleteEvent();
                return;
            }

            HandleWaypointArrival();
        }
    }

    private void HandleWaypointArrival()
    {
        WaypointData waypoint = waypoints[currentWaypointIndex];
        Debug.Log($"EventNPC {npcName}: Arrived at waypoint '{waypoint.waypointName}'");

        if (waypoint.waitForInteraction)
        {
            currentState = NPCState.WaitingForInteraction;
            Debug.Log($"EventNPC {npcName}: Waiting for player interaction");
        }
        else if (waypoint.waitTime > 0)
        {
            currentState = NPCState.Idle;
            StartCoroutine(WaitAtWaypoint(waypoint.waitTime));
        }
        else
        {
            currentWaypointIndex++;
            MoveToNextWaypoint();
        }
    }

    private IEnumerator WaitAtWaypoint(float waitTime)
    {
        Debug.Log($"EventNPC {npcName}: Waiting for {waitTime} seconds");
        yield return new WaitForSeconds(waitTime);

        currentWaypointIndex++;
        MoveToNextWaypoint();
    }

    private void HandleDialogueEnded()
    {
        if (currentState != NPCState.InDialogue) return;

        Debug.Log($"EventNPC {npcName}: Dialogue ended, continuing to next waypoint");
        hasCompletedDialogue = true;

        currentWaypointIndex++;
        MoveToNextWaypoint();
    }

    private void HandleWaypointsCompleted()
    {
        Debug.Log($"EventNPC {npcName}: All waypoints completed, handling exit behavior: {exitBehavior}");

        switch (exitBehavior)
        {
            case NPCExitBehavior.Destroy:
                CompleteEvent();
                break;

            case NPCExitBehavior.Disable:
                gameObject.SetActive(false);
                CompleteEvent();
                break;

            case NPCExitBehavior.ContinueWalking:
                MoveToExitPoint();
                break;
        }
    }

    private void MoveToExitPoint()
    {
        GameObject exitObj = GameObject.Find(exitPointName);

        if (exitObj == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Exit point '{exitPointName}' not found! Destroying immediately.");
            CompleteEvent();
            return;
        }

        currentState = NPCState.Exiting;
        agent.speed = waypoints.Length > 0 ? waypoints[waypoints.Length - 1].moveSpeed : 3f;
        agent.SetDestination(exitObj.transform.position);

        SetWalkingState(true);
        Debug.Log($"EventNPC {npcName}: Walking to exit point '{exitPointName}'");
    }

    private void CompleteEvent()
    {
        Debug.Log($"EventNPC {npcName}: Event completed");
        OnNPCEventCompleted?.Invoke();

        if (exitBehavior != NPCExitBehavior.Disable)
        {
            Destroy(gameObject);
        }
    }

    private void SetWalkingState(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, isWalking);
            Debug.Log($"EventNPC {npcName}: SetWalkingState({isWalking})");
        }
        else
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot set walking state - no Animator!");
        }
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Draw current state
        Gizmos.color = currentState switch
        {
            NPCState.Walking => Color.green,
            NPCState.WaitingForInteraction => Color.yellow,
            NPCState.InDialogue => Color.cyan,
            NPCState.Exiting => Color.red,
            _ => Color.gray
        };

        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);

        // Draw path to target
        if (currentTargetWaypoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentTargetWaypoint.position);
        }
    }
    #endregion
}
