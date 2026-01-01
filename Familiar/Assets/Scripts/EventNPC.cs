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
    [HideInInspector] public ExitDialogueData[] exitDialogues;
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
    private DialogueUI dialogueUI;

    private int currentWaypointIndex = 0;
    private NPCState currentState = NPCState.Idle;
    private Transform currentTargetWaypoint;
    private bool hasCompletedDialogue = false;
    private string currentIdleAnimationBool = "";
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
        dialogueUI = FindFirstObjectByType<DialogueUI>();

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
                           NPCExitBehavior exit, string exitPoint, string name = null,
                           ExitDialogueData[] exitDialogueData = null)
    {
        waypoints = waypointData;
        inkDialogue = dialogue;
        dialogueKnot = knot;
        exitBehavior = exit;
        exitPointName = exitPoint;
        exitDialogues = exitDialogueData;

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

        // Get dialogue from current waypoint, or fall back to event default
        WaypointData currentWaypoint = waypoints[currentWaypointIndex];
        TextAsset dialogueToUse = currentWaypoint.inkDialogue != null ? currentWaypoint.inkDialogue : inkDialogue;
        string knotToUse = !string.IsNullOrEmpty(currentWaypoint.dialogueKnot) ? currentWaypoint.dialogueKnot : dialogueKnot;

        if (dialogueToUse == null)
        {
            Debug.LogError($"EventNPC {npcName}: No Ink dialogue assigned for waypoint or event!");
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
        Debug.Log($"EventNPC {npcName}: Starting dialogue (knot: {(string.IsNullOrEmpty(knotToUse) ? "default" : knotToUse)})");

        // Start dialogue
        if (!string.IsNullOrEmpty(knotToUse))
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, knotToUse, transform);
        }
        else
        {
            dialogueManager.EnterDialogueMode(dialogueToUse, transform);
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

        // Clear any previous idle animation
        ClearCurrentIdleAnimation();

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

        // Trigger custom movement animation if specified
        if (!string.IsNullOrEmpty(waypoint.movementAnimationTrigger))
        {
            TriggerAnimation(waypoint.movementAnimationTrigger);
            Debug.Log($"EventNPC {npcName}: Playing movement animation '{waypoint.movementAnimationTrigger}'");
        }

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

        // Trigger arrival animation if specified
        if (!string.IsNullOrEmpty(waypoint.arrivalAnimationTrigger))
        {
            TriggerAnimation(waypoint.arrivalAnimationTrigger);
            Debug.Log($"EventNPC {npcName}: Playing arrival animation '{waypoint.arrivalAnimationTrigger}'");
        }

        // Set idle animation bool if specified (for looping idle animations)
        if (!string.IsNullOrEmpty(waypoint.idleAnimationBool))
        {
            SetIdleAnimation(waypoint.idleAnimationBool, true);
        }

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
            AdvanceToNextWaypoint();
        }
    }

    private IEnumerator WaitAtWaypoint(float waitTime)
    {
        Debug.Log($"EventNPC {npcName}: Waiting for {waitTime} seconds");
        yield return new WaitForSeconds(waitTime);

        AdvanceToNextWaypoint();
    }

    private void HandleDialogueEnded()
    {
        if (currentState != NPCState.InDialogue) return;

        Debug.Log($"EventNPC {npcName}: Dialogue ended, continuing to next waypoint");
        hasCompletedDialogue = true;

        // Start exit dialogues right after main dialogue ends
        if (exitDialogues != null && exitDialogues.Length > 0)
        {
            foreach (var exitDialogue in exitDialogues)
            {
                if (!string.IsNullOrEmpty(exitDialogue.dialogueText))
                {
                    StartCoroutine(ShowExitDialogue(exitDialogue));
                }
            }
        }

        AdvanceToNextWaypoint();
    }

    private void AdvanceToNextWaypoint()
    {
        WaypointData currentWaypoint = waypoints[currentWaypointIndex];

        // Check for branching condition
        if (!string.IsNullOrEmpty(currentWaypoint.branchVariable) &&
            !string.IsNullOrEmpty(currentWaypoint.branchToWaypoint))
        {
            bool conditionMet = EventVariables.CheckVariable(
                currentWaypoint.branchVariable,
                currentWaypoint.branchValue
            );

            if (conditionMet)
            {
                int branchIndex = FindWaypointIndex(currentWaypoint.branchToWaypoint);
                if (branchIndex >= 0)
                {
                    Debug.Log($"EventNPC {npcName}: Branch condition met ({currentWaypoint.branchVariable} = {currentWaypoint.branchValue}), jumping to '{currentWaypoint.branchToWaypoint}'");
                    currentWaypointIndex = branchIndex;
                    MoveToNextWaypoint();
                    return;
                }
                else
                {
                    Debug.LogWarning($"EventNPC {npcName}: Branch target waypoint '{currentWaypoint.branchToWaypoint}' not found!");
                }
            }
            else
            {
                Debug.Log($"EventNPC {npcName}: Branch condition not met ({currentWaypoint.branchVariable} != {currentWaypoint.branchValue}), continuing normally");
            }
        }

        // Default: advance to next waypoint in sequence
        currentWaypointIndex++;
        MoveToNextWaypoint();
    }

    private int FindWaypointIndex(string waypointNameOrId)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            // Check waypointId first (if set), then waypointName
            string id = !string.IsNullOrEmpty(waypoints[i].waypointId)
                ? waypoints[i].waypointId
                : waypoints[i].waypointName;

            if (id == waypointNameOrId)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleWaypointsCompleted()
    {
        Debug.Log($"EventNPC {npcName}: All waypoints completed, handling exit behavior: {exitBehavior}");

        // Clear any idle animation before finishing
        ClearCurrentIdleAnimation();

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
        // Clear any idle animation before exiting
        ClearCurrentIdleAnimation();

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

    private IEnumerator ShowExitDialogue(ExitDialogueData dialogueData)
    {
        yield return new WaitForSeconds(dialogueData.delayAfterExit);

        if (dialogueUI == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot show exit dialogue - DialogueUI not found");
            yield break;
        }

        // Don't show if main dialogue is playing
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            Debug.Log($"EventNPC {npcName}: Skipping exit dialogue - main dialogue is playing");
            yield break;
        }

        Debug.Log($"EventNPC {npcName}: Showing exit dialogue");

        dialogueUI.Show();
        string speaker = string.IsNullOrEmpty(dialogueData.speakerName) ? null : dialogueData.speakerName;
        dialogueUI.SetDialogueText(dialogueData.dialogueText, speaker);

        yield return new WaitForSeconds(dialogueData.displayDuration);

        // Only hide if we're not in a main dialogue
        if (dialogueManager == null || !dialogueManager.IsDialoguePlaying())
        {
            dialogueUI.Hide();
        }
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

    private void TriggerAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
        else if (animator == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot trigger animation '{triggerName}' - no Animator!");
        }
    }

    private void SetIdleAnimation(string boolName, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(boolName))
        {
            animator.SetBool(boolName, value);
            if (value)
            {
                currentIdleAnimationBool = boolName;
            }
            Debug.Log($"EventNPC {npcName}: Set idle animation '{boolName}' = {value}");
        }
        else if (animator == null)
        {
            Debug.LogWarning($"EventNPC {npcName}: Cannot set idle animation '{boolName}' - no Animator!");
        }
    }

    private void ClearCurrentIdleAnimation()
    {
        if (!string.IsNullOrEmpty(currentIdleAnimationBool) && animator != null)
        {
            animator.SetBool(currentIdleAnimationBool, false);
            Debug.Log($"EventNPC {npcName}: Cleared idle animation '{currentIdleAnimationBool}'");
            currentIdleAnimationBool = "";
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
