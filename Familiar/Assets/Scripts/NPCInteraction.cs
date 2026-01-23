using UnityEngine;

public class NPCInteraction : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private TextAsset inkJSONAsset;

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "Villager";
    [SerializeField] private string interactionPrompt = "Talk";

    [Header("Camera Settings")]
    [Tooltip("Camera look height from NPC origin (-1 to use default ~1.6). Increase to frame face when zoomed.")]
    [SerializeField] private float cameraHeight = -1f;

    [Tooltip("Camera FOV during dialogue (-1 to use default)")]
    [SerializeField] private float cameraZoom = -1f;

    [Header("Dialogue Sound")]
    [Tooltip("Sound clip to play during typewriter effect (overrides default)")]
    [SerializeField] private AudioClip dialogueSoundClip;

    [Tooltip("Volume of the dialogue sound (0-1, -1 to use default)")]
    [Range(-1f, 1f)]
    [SerializeField] private float dialogueSoundVolume = -1f;

    [Tooltip("Base pitch of the sound (1 = normal, <1 = lower, >1 = higher, -1 to use default)")]
    [Range(-1f, 2f)]
    [SerializeField] private float dialogueSoundBasePitch = -1f;

    [Tooltip("Pitch variation for dialogue sound (-1 to use default)")]
    [Range(-1f, 0.5f)]
    [SerializeField] private float dialogueSoundPitchVariation = -1f;

    [Tooltip("Play sound every N characters (-1 to use default)")]
    [SerializeField] private int dialogueSoundEveryN = -1;

    // Reference to dialogue manager
    private DialogueManager dialogueManager;

    private void Start()
    {
        // Get reference to DialogueManager
        dialogueManager = DialogueManager.GetInstance();

        // Make sure this NPC is on the correct layer for interaction
        // You should create an "Interactable" layer and assign it
        gameObject.layer = LayerMask.NameToLayer("Interactable");

        // Ensure the NPC has a collider for raycasting
        if (GetComponent<Collider>() == null)
        {
            // Add a box collider if none exists
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2f, 1f); // Adjust size as needed
            col.center = new Vector3(0f, 1f, 0f); // Center it on the NPC
        }
    }

    // Implementation of IInteractable interface
    public void Interact()
    {
        // Check if DialogueManager exists
        if (dialogueManager == null)
        {
            dialogueManager = DialogueManager.GetInstance();
            if (dialogueManager == null)
            {
                Debug.LogError("DialogueManager not found in scene!");
                return;
            }
        }

        // Check if ink file is assigned
        if (inkJSONAsset == null)
        {
            Debug.LogError($"No Ink JSON file assigned to {npcName}!");
            return;
        }

        // Check if dialogue is not already playing
        if (!dialogueManager.IsDialoguePlaying())
        {
            // Cancel any active simple dialogue first
            SimpleDialogueTrigger.CancelActiveSimpleDialogue();

            Debug.Log($"Starting dialogue with {npcName}");

            // Make NPC face the player
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                transform.LookAt(playerObj.transform);
            }

            StartDialogue();
        }
        else
        {
            Debug.Log("Dialogue is already playing");
        }
    }

    public string GetInteractionPrompt()
    {
        // This returns what shows up in the interaction prompt
        // Your InteractionSystem code has this commented out, but it's here if you need it
        return $"{interactionPrompt} to {npcName}";
    }

    private void StartDialogue()
    {
        // Set sound override if configured
        if (dialogueSoundClip != null)
        {
            dialogueManager.SetDialogueSoundOverride(dialogueSoundClip, dialogueSoundVolume, dialogueSoundBasePitch, dialogueSoundPitchVariation, dialogueSoundEveryN);
        }

        dialogueManager.EnterDialogueMode(inkJSONAsset, transform, cameraHeight, 0f, cameraZoom);
    }

    // Optional: Visual feedback when player is near
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // Draw a wire cube around the NPC to show interaction area
        if (GetComponent<Collider>() != null)
        {
            Gizmos.DrawWireCube(transform.position + Vector3.up, GetComponent<Collider>().bounds.size);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }
    }
}
