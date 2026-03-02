using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Prevents NPCs from being pushed by the player.
/// Uses agent.updatePosition = false so the NavMeshAgent's internal simulation
/// is decoupled from the transform — physics pushes on the transform don't
/// affect the agent's pathfinding position. We manually sync the two.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCAntiPush : MonoBehaviour
{
    private NavMeshAgent agent;
    private Vector3 anchorPosition;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        anchorPosition = transform.position;
        // Decouple agent simulation from transform so physics pushes are ignored
        agent.updatePosition = false;
    }

    void LateUpdate()
    {
        if (agent == null || !agent.enabled) return;

        // During OffMeshLink traversal, EventNPC controls position manually
        if (agent.isOnOffMeshLink)
        {
            anchorPosition = transform.position;
            return;
        }

        // Detect external teleport/warp (e.g. after OffMeshLink finishes)
        float drift = Vector3.Distance(agent.nextPosition, anchorPosition);
        if (drift > 3f)
        {
            anchorPosition = agent.nextPosition;
        }

        // Check if NPC is actively moving to a destination
        bool isMoving = agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

        if (isMoving)
        {
            // nextPosition is pure pathfinding — unaffected by physics
            // because updatePosition is false
            anchorPosition = agent.nextPosition;
        }

        // Set transform to the clean anchor position (overrides any physics push)
        transform.position = anchorPosition;
        // Keep agent's internal position in sync
        agent.nextPosition = anchorPosition;

        // Re-enforce updatePosition = false in case another script toggled it
        // (e.g. EventNPC sets it to true after OffMeshLink traversal)
        agent.updatePosition = false;
    }

    /// <summary>
    /// Call this when you want to update the anchor position externally
    /// (e.g., after teleporting the NPC or after an animation).
    /// </summary>
    public void UpdateAnchor()
    {
        anchorPosition = transform.position;
        if (agent != null)
        {
            agent.nextPosition = anchorPosition;
        }
    }
}
