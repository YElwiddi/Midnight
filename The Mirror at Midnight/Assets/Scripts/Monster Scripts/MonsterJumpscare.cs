using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MonsterJumpscare : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;       // The main player camera
    public Transform monsterFace;        // Reference to the monster's head/face transform
    public AudioSource scareAudioSource; // Audio source for the jumpscare sound
    public AudioClip scareSound;         // The jumpscare sound to play
    
    [Header("Settings")]
    public float triggerDistance = 2.0f; // Distance to trigger the jumpscare
    public float camPanSpeed = 5.0f;     // How fast the camera pans to the monster
    public float gameOverDelay = 3.0f;   // Seconds before game over after jumpscare
    
    [Header("Camera Shake")]
    public float shakeIntensity = 0.5f;  // How intense the shake effect is
    public float shakeDuration = 2.0f;   // How long the shake effect lasts
    public float decreaseAmount = 0.8f;  // How quickly the shake effect diminishes (lower = longer shake)
    
    [Header("Optional")]
    public CanvasGroup gameOverUI;       // Optional UI that fades in
    public float fadeSpeed = 1.0f;       // Speed for the UI to fade in
    
    private bool jumpscareTriggered = false;
    private Transform originalCameraParent;
    private Vector3 originalCameraPos;
    private Quaternion originalCameraRot;
    private float currentShakeAmount = 0f;
    private float currentShakeDuration = 0f;
    
    // Additional method for fading in the UI
    private IEnumerator FadeInUI()
    {
        float startAlpha = gameOverUI.alpha;
        float endAlpha = 1.0f;
        float duration = 1.0f;
        float startTime = Time.time;
        
        while (Time.time < startTime + duration)
        {
            float t = (Time.time - startTime) / duration;
            gameOverUI.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }
        
        gameOverUI.alpha = endAlpha;
    }
    
    void Update()
    {
        if (jumpscareTriggered)
        {
            // Apply camera shake if currently active
            if (currentShakeDuration > 0 && playerCamera != null)
            {
                // Generate random camera movement within the intensity range, but keep it rotational
                // rather than positional to maintain focus on the monster's face
                Vector3 randomRotation = new Vector3(
                    Random.Range(-currentShakeAmount, currentShakeAmount), 
                    Random.Range(-currentShakeAmount, currentShakeAmount), 
                    0) * 0.5f; // Reduce z-axis rotation to avoid disorienting roll
                
                // Apply the random rotation but keep looking at the monster face
                if (monsterFace != null)
                {
                    // Store the original rotation
                    Quaternion originalRotation = playerCamera.rotation;
                    
                    // Apply shake as a small offset to rotation
                    playerCamera.rotation = playerCamera.rotation * Quaternion.Euler(randomRotation);
                    
                    // Ensure we're still roughly pointed at the monster's face
                    Vector3 lookDir = (monsterFace.position - playerCamera.position).normalized;
                    Vector3 currentDir = playerCamera.forward;
                    
                    // If we've deviated too much from looking at the face, partially correct
                    if (Vector3.Angle(lookDir, currentDir) > 15f)
                    {
                        // Blend between current rotation and looking at monster
                        Quaternion lookAtRotation = Quaternion.LookRotation(lookDir);
                        playerCamera.rotation = Quaternion.Slerp(playerCamera.rotation, lookAtRotation, 0.5f);
                    }
                }
                
                // Decrease the shake duration and amount over time
                currentShakeDuration -= Time.deltaTime * decreaseAmount;
                currentShakeAmount = Mathf.Lerp(0, shakeIntensity, currentShakeDuration / shakeDuration);
                
                // If shake effect is done, ensure camera is back to intended position
                if (currentShakeDuration <= 0)
                {
                    currentShakeDuration = 0f;
                    currentShakeAmount = 0f;
                    
                    // Make sure we end looking directly at the monster's face
                    if (monsterFace != null)
                    {
                        playerCamera.LookAt(monsterFace);
                    }
                }
            }
            return;
        }
            
        // Calculate distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, playerCamera.position);
        
        // Trigger jumpscare when player is too close
        if (distanceToPlayer <= triggerDistance)
        {
            StartCoroutine(TriggerJumpscare());
        }
    }
    
    // Method to start the camera shake
    public void StartCameraShake(float intensity = -1, float duration = -1)
    {
        // Use provided parameters or default to inspector values
        currentShakeAmount = (intensity > 0) ? intensity : shakeIntensity;
        currentShakeDuration = (duration > 0) ? duration : shakeDuration;
    }
    
IEnumerator TriggerJumpscare()
{
    jumpscareTriggered = true;
    
    // Store original camera settings and monster position
    originalCameraParent = playerCamera.parent;
    originalCameraPos = playerCamera.localPosition;
    originalCameraRot = playerCamera.localRotation;
    Vector3 originalMonsterPosition = transform.position;
    Quaternion originalMonsterRotation = transform.rotation;
    
    // Play the jumpscare sound
    if (scareAudioSource != null && scareSound != null)
    {
        scareAudioSource.clip = scareSound;
        scareAudioSource.Play();
    }
    else
    {
        // Create audio source if needed
        AudioSource tempSource = gameObject.AddComponent<AudioSource>();
        tempSource.clip = scareSound;
        tempSource.priority = 0; // Highest priority
        tempSource.volume = 1f;
        tempSource.Play();
    }
    
    // Freeze player movement
    FreezePlayer(true);
    
    // Temporarily detach camera from parent to move it freely
    Transform originalParent = playerCamera.parent;
    playerCamera.parent = null;
    
    // Disable NavMeshAgent and CharacterController to allow manual positioning
    UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    if (agent != null)
        agent.enabled = false;
    
    CharacterController controller = GetComponent<CharacterController>();
    if (controller != null)
        controller.enabled = false;
    
    // 1. First position the monster in front of the player
    Vector3 playerForward = playerCamera.forward;
    playerForward.y = 0; // Keep level with ground
    playerForward.Normalize();
    
    // Position monster 1.5 units in front of player
    Vector3 monsterTargetPosition = playerCamera.position + playerForward * 1.6f;
    monsterTargetPosition.y = transform.position.y; // Keep original height
    
    // Quickly move the monster to position
    float repositionDuration = 0.1f;
    float repositionStartTime = Time.time;
    
    while (Time.time < repositionStartTime + repositionDuration)
    {
        float t = (Time.time - repositionStartTime) / repositionDuration;
        transform.position = Vector3.Lerp(originalMonsterPosition, monsterTargetPosition, t);
        yield return null;
    }
    
    // Ensure exact position
    transform.position = monsterTargetPosition;
    
    // 2. Now directly face the monster toward the player (so player sees its face)
    Vector3 dirToPlayer = playerCamera.position - transform.position;
    dirToPlayer.y = 0;
    Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
    transform.rotation = targetRotation;
    
    // 3. No waiting - instantly snap to monster's face position
    
    // Calculate the position to view the monster's face
    Vector3 facePos = monsterFace.position;
    Vector3 offset = (playerCamera.position - facePos).normalized * 0.85f;
    Vector3 targetPosition = facePos + offset;
    
    // Instantly snap camera to the position
    playerCamera.position = targetPosition;
    playerCamera.LookAt(facePos);
    
    // Start the camera shake immediately at full intensity
    StartCameraShake(shakeIntensity * 1.2f); // Slightly increased for more impact
    
    // Fade in game over UI if available instantly
    if (gameOverUI != null)
    {
        // Start with partial visibility and continue fading in
        gameOverUI.alpha = 0.5f;
        StartCoroutine(FadeInUI());
    }
    
    // Wait a tiny beat for dramatic effect and to let the shake register
    yield return new WaitForSeconds(0.05f);
    
    // Camera is already looking at the face - just maintain focus
    // Keep the shake going during the wait period
    
    // Wait for the specified delay before game over
    yield return new WaitForSeconds(gameOverDelay);
    
    // Trigger game over
    GameOver();
}
    
    void FreezePlayer(bool freeze)
    {
        // Find player controller and disable it - this depends on your setup
        // Example for a character controller:
        CharacterController playerController = playerCamera.GetComponentInParent<CharacterController>();
        if (playerController != null)
            playerController.enabled = !freeze;
            
        // Example for a first person controller script
        MonoBehaviour[] scripts = playerCamera.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            // This is approximate, adjust for your actual player controller script name
            if (script.GetType().Name.Contains("Controller") || 
                script.GetType().Name.Contains("Movement"))
            {
                script.enabled = !freeze;
            }
        }
    }
    
    void GameOver()
    {
        // Implement your game over logic here
        Debug.Log("Game Over!");
        
        // Option 1: Reload the current scene
        // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
        // Option 2: Load a game over scene
        // SceneManager.LoadScene("GameOver");
        
        // Option 3: Show game over UI and allow restart
        if (gameOverUI != null)
        {
            gameOverUI.alpha = 1;
            // Enable restart button or other UI elements
        }
    }
}