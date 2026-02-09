using UnityEngine;

public class MonsterFollow : MonoBehaviour
{
    // Reference to the player
    public Transform player;
    
    // Monster movement speed
    public float moveSpeed = 3.0f;
    
    // Minimum distance to keep from player
    public float minimumDistance = 0.5f;
    
    // Optional: if the monster should rotate to face the player
    public bool rotateToFacePlayer = true;
    
    // Optional: rotation speed when turning to face player
    public float rotationSpeed = 5.0f;
    
    // Optional: force to apply when using physics movement
    public float movementForce = 10.0f;
    
    // Optional: raycast distance for collision detection
    public float raycastDistance = 1.0f;
    
    // Layer mask for walls/obstacles
    public LayerMask obstacleLayerMask = -1; // Default to all layers
    
    // Optional: navmesh agent component
    private UnityEngine.AI.NavMeshAgent navMeshAgent;
    
    // For physics-based movement
    private Rigidbody rb;
    
    // For tracking if we're obstructed
    private bool isObstructed = false;
    
    private void Start()
    {
        // If no player is assigned, try to find one with the "Player" tag
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else
            {
                Debug.LogWarning("No player found with 'Player' tag. Please assign the player manually.");
            }
        }
        
        // Check if this object has a NavMeshAgent (for path following)
        navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.stoppingDistance = minimumDistance;
            
            // Fix for hovering - adjust the base offset to match your model
            navMeshAgent.baseOffset = 0.8f; // Set this to a negative value if needed
            
            // Make sure the agent doesn't automatically adjust y-position when following path
            navMeshAgent.updatePosition = true;
            navMeshAgent.updateRotation = rotateToFacePlayer;
        }
        
        // Get Rigidbody for physics movement
        rb = GetComponent<Rigidbody>();
        if (rb != null && navMeshAgent == null)
        {
            // If using physics, make sure we're not using kinematic
            rb.isKinematic = false;
        }
    }
    
    private void Update()
    {
        if (player == null)
            return;
            
        // Calculate distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Calculate direction towards player (we'll need this regardless of movement type)
        Vector3 direction = (player.position - transform.position).normalized;
        
        // Rotate to face player if enabled (for any movement type)
        if (rotateToFacePlayer)
        {
            // Calculate the rotation needed to look at player
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // Smoothly rotate towards that direction
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
        
        // Move towards player if we're farther than the minimum distance
        if (distanceToPlayer > minimumDistance)
        {
            // If we have a NavMeshAgent, use it for pathfinding (best for obstacle avoidance)
            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                // Check if the agent is on a NavMesh before setting destination
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.SetDestination(player.position);
                }
                else
                {
                    Debug.LogWarning("Monster is not on a NavMesh. Make sure to bake a NavMesh and position the monster on it.");
                    
                    // Fall back to physics-based movement if we have a Rigidbody
                    if (rb != null)
                    {
                        rb.AddForce(direction * movementForce);
                    }
                    // Otherwise use transform-based movement
                    else
                    {
                        transform.position += direction * moveSpeed * Time.deltaTime;
                    }
                }
            }
            // If we have a Rigidbody, use physics-based movement (respects collisions)
            else if (rb != null)
            {
                // Check for obstacles in the way
                CheckForObstacles(direction);
                
                // Only apply force if not obstructed
                if (!isObstructed)
                {
                    // Apply force in the direction of the player
                    rb.AddForce(direction * movementForce);
                    
                    // Optional: cap maximum velocity to prevent excessive momentum
                    if (rb.linearVelocity.magnitude > moveSpeed)
                    {
                        rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
                    }
                }
            }
            // Otherwise fall back to simple transform movement (no collision support)
            else
            {
                transform.position += direction * moveSpeed * Time.deltaTime;
                Debug.LogWarning("Monster has no Rigidbody or NavMeshAgent. Walls will be ignored.");
            }
        }
    }
    
    // Check for obstacles using raycasting
    private void CheckForObstacles(Vector3 direction)
    {
        // Cast a ray in the movement direction
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, raycastDistance, obstacleLayerMask))
        {
            // We hit something - check if it's not the player
            if (hit.transform != player)
            {
                isObstructed = true;
                
                // Try to find a way around the obstacle
                TryFindAlternativePath();
                return;
            }
        }
        
        // No obstacle detected
        isObstructed = false;
    }
    
    // Simple obstacle avoidance
    private void TryFindAlternativePath()
    {
        // This is a very simple implementation
        // For better obstacle avoidance, consider using NavMeshAgent instead
        
        // Try moving slightly to the right or left
        Vector3 rightDirection = Quaternion.Euler(0, 45, 0) * (player.position - transform.position).normalized;
        Vector3 leftDirection = Quaternion.Euler(0, -45, 0) * (player.position - transform.position).normalized;
        
        RaycastHit hit;
        
        // Check if right direction is clear
        if (!Physics.Raycast(transform.position, rightDirection, out hit, raycastDistance, obstacleLayerMask) ||
            (hit.transform == player))
        {
            rb.AddForce(rightDirection * movementForce);
        }
        // Check if left direction is clear
        else if (!Physics.Raycast(transform.position, leftDirection, out hit, raycastDistance, obstacleLayerMask) ||
                 (hit.transform == player))
        {
            rb.AddForce(leftDirection * movementForce);
        }
    }
    
    // Visualize detection rays in Scene view (helpful for debugging)
    private void OnDrawGizmos()
    {
        if (player != null)
        {
            // Draw line to player
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, player.position);
            
            // Draw obstacle detection ray
            Gizmos.color = isObstructed ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, (player.position - transform.position).normalized * raycastDistance);
        }
    }
}