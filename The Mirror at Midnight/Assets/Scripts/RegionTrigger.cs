using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("The tag of the object that can trigger this region (default is 'Player')")]
    public string targetTag = "Player";
    [Tooltip("Should the trigger only activate once? (Recommended to leave this enabled)")]
    public bool triggerOnce = true;
    
    [Header("Audio Settings")]
    [Tooltip("Enable to play a sound when trigger activates")]
    public bool playSound = false;
    [Tooltip("The audio clip to play when triggered")]
    public AudioClip triggerSound;
    [Tooltip("Volume for the trigger sound (0-1)")]
    [Range(0f, 1f)]
    public float soundVolume = 1f;
    [Tooltip("Delay before playing the sound (seconds)")]
    public float soundDelay = 0f;

    [Header("Actions")]
    [Tooltip("Enable to start a dialogue when trigger activates")]
    public bool startDialogue = false;
    [Tooltip("Delay before starting dialogue (seconds)")]
    public float dialogueDelay = 0f;
    [Tooltip("Enable to spawn an object when trigger activates")]
    public bool spawnObject = false;
    [Tooltip("Delay before spawning the object (seconds)")]
    public float spawnDelay = 0f;

    [Header("Dialogue Settings")]
    [Tooltip("Reference to the DialogueSystem component")]
    public DialogueSystem dialogueSystem;
    [Tooltip("Optional: Specify which dialogue tree to use (leave at -1 to use current)")]
    public int dialogueTreeIndex = -1;

    [Header("Spawn Settings")]
    [Tooltip("The prefab to spawn")]
    public GameObject objectToSpawn;
    [Tooltip("Where to spawn the object")]
    public Transform spawnPoint;
    [Tooltip("Random position offset range")]
    public Vector3 randomPositionOffset = Vector3.zero;
    [Tooltip("Destroy the spawned object after seconds (0 = never)")]
    public float destroyAfterSeconds = 0f;

    // State tracking
    private bool hasTriggered = false;
    private AudioSource audioSource;

    private void Awake()
    {
        // Check if we have an AudioSource component, add one if needed
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && playSound)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D sound
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object has the target tag and hasn't been triggered yet
        if (other.CompareTag(targetTag) && (!triggerOnce || !hasTriggered))
        {
            // Mark as triggered immediately to prevent any potential double-triggering
            hasTriggered = true;
            
            ExecuteTrigger();
        }
    }

    private void ExecuteTrigger()
    {
        // Start all action coroutines with their specific delays
        if (playSound)
        {
            StartCoroutine(PlaySoundWithDelay());
        }

        if (startDialogue)
        {
            StartCoroutine(StartDialogueWithDelay());
        }

        if (spawnObject)
        {
            StartCoroutine(SpawnObjectWithDelay());
        }
        
        // Option to disable the collider after triggering to ensure it can't be triggered again
        if (triggerOnce)
        {
            // Disable the collider to prevent any possibility of retriggering
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }

    private IEnumerator PlaySoundWithDelay()
    {
        if (soundDelay > 0)
        {
            yield return new WaitForSeconds(soundDelay);
        }

        if (triggerSound != null && audioSource != null)
        {
            audioSource.clip = triggerSound;
            audioSource.volume = soundVolume;
            audioSource.Play();
        }
        else if (triggerSound == null)
        {
            Debug.LogWarning("RegionTrigger: Trigger sound is missing!");
        }
    }

    private IEnumerator StartDialogueWithDelay()
    {
        if (dialogueDelay > 0)
        {
            yield return new WaitForSeconds(dialogueDelay);
        }
        
        if (dialogueSystem == null)
        {
            Debug.LogError("RegionTrigger: DialogueSystem reference is missing!");
            yield break;
        }

        // Change dialogue tree if specified
        if (dialogueTreeIndex >= 0)
        {
            dialogueSystem.SetActiveDialogueTree(dialogueTreeIndex);
        }

        // Start the dialogue
        if (!dialogueSystem.IsInDialogue())
        {
            dialogueSystem.StartDialogue();
        }
    }

    private IEnumerator SpawnObjectWithDelay()
    {
        if (spawnDelay > 0)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        if (objectToSpawn == null)
        {
            Debug.LogError("RegionTrigger: Object to spawn is missing!");
            yield break;
        }

        // Determine spawn position
        Vector3 position;
        
        if (spawnPoint != null)
        {
            position = spawnPoint.position;
        }
        else
        {
            position = transform.position;
        }

        // Add random offset if specified
        if (randomPositionOffset != Vector3.zero)
        {
            position += new Vector3(
                Random.Range(-randomPositionOffset.x, randomPositionOffset.x),
                Random.Range(-randomPositionOffset.y, randomPositionOffset.y),
                Random.Range(-randomPositionOffset.z, randomPositionOffset.z)
            );
        }

        // Spawn the object
        GameObject spawnedObject = Instantiate(objectToSpawn, position, Quaternion.identity);

        // Destroy after time if specified
        if (destroyAfterSeconds > 0)
        {
            Destroy(spawnedObject, destroyAfterSeconds);
        }
    }

    // Helper method to reset the trigger (can be called from other scripts)
    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    // Visual representation in the editor
    private void OnDrawGizmos()
    {
        // Get the collider to determine the size of the region
        Collider col = GetComponent<Collider>();
        
        if (col != null)
        {
            // Set gizmo color based on trigger type
            if (startDialogue && spawnObject)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for both
            else if (startDialogue)
                Gizmos.color = new Color(0f, 0.6f, 1f, 0.3f); // Blue for dialogue
            else if (spawnObject)
                Gizmos.color = new Color(0.5f, 1f, 0f, 0.3f); // Green for spawn
            else
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Yellow for no action selected
            
            // Add sound indicator to color
            if (playSound)
            {
                // Make the color slightly more red to indicate sound
                Gizmos.color = new Color(
                    Mathf.Min(1f, Gizmos.color.r + 0.2f),
                    Gizmos.color.g * 0.9f,
                    Gizmos.color.b * 0.9f,
                    Gizmos.color.a
                );
            }
            
            // Draw appropriate gizmo based on collider type
            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawCube(box.center, box.size);
                
                // Reset matrix
                Gizmos.matrix = Matrix4x4.identity;
                
                // Draw a small icon to indicate this is a player trigger
                Gizmos.color = Color.white;
                Vector3 iconPos = transform.position + new Vector3(0, box.size.y / 2 + 0.5f, 0);
                Gizmos.DrawIcon(iconPos, "sv_icon_dot6_pix16_gizmo", true);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius * transform.lossyScale.x);
                
                // Draw a small icon to indicate this is a player trigger
                Gizmos.color = Color.white;
                Vector3 iconPos = transform.position + new Vector3(0, sphere.radius + 0.5f, 0);
                Gizmos.DrawIcon(iconPos, "sv_icon_dot6_pix16_gizmo", true);
            }
            else if (col is CapsuleCollider)
            {
                // Simplified visualization for capsule
                CapsuleCollider capsule = col as CapsuleCollider;
                Gizmos.DrawSphere(transform.position, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
                
                // Draw a small icon to indicate this is a player trigger
                Gizmos.color = Color.white;
                Vector3 iconPos = transform.position + new Vector3(0, capsule.height / 2 + 0.5f, 0);
                Gizmos.DrawIcon(iconPos, "sv_icon_dot6_pix16_gizmo", true);
            }
        }
        
        // Draw spawn point if specified
        if (spawnObject && spawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(spawnPoint.position, 0.25f);
        }
        
        // Draw sound indicator if enabled
        if (playSound)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.7f);
            Gizmos.DrawIcon(transform.position + Vector3.up * 1.2f, "AudioSource Gizmo", true);
        }
    }
}