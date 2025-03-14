using UnityEngine;

public class MonsterAnimationController : MonoBehaviour
{
    public Transform player;              // Reference to the player
    public float attackDistance = 2.0f;   // Distance at which to attack
    public float movementThreshold = 0.1f; // Minimum speed to consider "moving"
    
    private Animator animator;
    private MonsterFollow moveScript;
    private Vector3 lastPosition;
    
    void Start()
    {
        // Look for the animator in a sibling GameObject instead of a child
        Transform prefabTransform = transform.parent.Find("IDOL_Prefab2");
        
        if (prefabTransform != null)
        {
            animator = prefabTransform.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found on IDOL_Prefab2!");
                
                // Try adding it
                animator = prefabTransform.gameObject.AddComponent<Animator>();
                Debug.Log("Added Animator component to IDOL_Prefab2");
            }
        }
        else
        {
            Debug.LogError("IDOL_Prefab2 not found as a sibling GameObject! Check your hierarchy.");
            
            // Try to find it another way, by searching all children of the parent
            if (transform.parent != null)
            {
                foreach (Transform child in transform.parent)
                {
                    if (child.name.Contains("IDOL"))
                    {
                        prefabTransform = child;
                        animator = prefabTransform.GetComponent<Animator>();
                        if (animator == null)
                        {
                            animator = prefabTransform.gameObject.AddComponent<Animator>();
                        }
                        Debug.Log("Found and using animator on " + prefabTransform.name);
                        break;
                    }
                }
            }
        }
        
        // Try to get the MonsterFollow from this GameObject first
        moveScript = GetComponent<MonsterFollow>();
        
        // If not found, try to find it in parent or siblings
        if (moveScript == null) 
        {
            if (transform.parent != null)
            {
                moveScript = transform.parent.GetComponent<MonsterFollow>();
                
                // If not on parent, check siblings
                if (moveScript == null && transform.parent != null)
                {
                    foreach (Transform sibling in transform.parent)
                    {
                        moveScript = sibling.GetComponent<MonsterFollow>();
                        if (moveScript != null)
                        {
                            Debug.Log("Found MonsterFollow on sibling: " + sibling.name);
                            break;
                        }
                    }
                }
            }
        }
        
        // If still not found, look for script with a similar name
        if (moveScript == null)
        {
            var allComponents = GetComponents<Component>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().Name.Contains("Follow"))
                {
                    Debug.Log("Found movement script with type: " + comp.GetType().Name);
                    break;
                }
            }
            
            Debug.LogWarning("MonsterFollow component not found, will use position tracking instead");
        }
        
        if (player == null)
        {
            Debug.LogWarning("Player reference not set! Trying to find by tag.");
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                Debug.Log("Found player by tag.");
            }
            else
            {
                Debug.LogError("Player not found by tag! Please assign manually.");
            }
        }
        
        lastPosition = transform.position;
    }
    
    void Update()
    {
        // Only proceed if we have animator
        if (animator == null) 
        {
            Debug.LogWarning("No animator found, skipping animation update");
            return;
        }
        
        bool isMoving = false;
        
        // If we have player reference, check distance for attack
        if (player != null)
        {
            // Calculate distance to player
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            
            // If player is within attack distance, trigger attack
            if (distanceToPlayer <= attackDistance)
            {
                animator.SetTrigger("AttackTrigger");
            }
        }
        
        // Determine if moving based on available components
        if (moveScript != null && player != null)
        {
            // Monster is moving if it's following the player and not at minimum distance
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            isMoving = distanceToPlayer > moveScript.minimumDistance;
            Debug.Log($"Using moveScript to determine movement. IsMoving: {isMoving}");
        }
        else
        {
            // Fall back to position tracking if we don't have the movement script
            Vector3 movement = transform.position - lastPosition;
            float speed = movement.magnitude / Time.deltaTime;
            isMoving = speed > movementThreshold;
            Debug.Log($"Using position tracking for movement. Speed: {speed}, IsMoving: {isMoving}");
        }
        
        // Store current position for next frame
        lastPosition = transform.position;
        
        // Update the IsMoving parameter
        animator.SetBool("IsMoving", isMoving);
    }
}