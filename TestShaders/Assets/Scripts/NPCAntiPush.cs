using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Prevents NPCs from being pushed by the player's NavMeshObstacle
/// while still allowing normal NavMeshAgent pathfinding.
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
        // Store initial position as anchor
        anchorPosition = transform.position;
    }

    void LateUpdate()
    {
        if (agent == null || !agent.enabled) return;

        // Check if NPC is actively moving to a destination
        bool isMoving = agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

        if (isMoving)
        {
            // While moving, anchor follows the agent's calculated position
            anchorPosition = agent.nextPosition;
        }

        // Always reset transform to anchor position (prevents push)
        transform.position = anchorPosition;

        // Sync agent's internal position to match transform
        // This prevents the agent from thinking it's somewhere else
        agent.nextPosition = anchorPosition;
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
