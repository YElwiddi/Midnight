using UnityEngine;

/// <summary>
/// Attach to a shovel object to allow the player to pick it up.
/// Sets GameManager.ShovelPickedUp = true and destroys the object.
/// </summary>
public class ShovelPickup : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [Tooltip("Text shown in the interaction prompt")]
    public string interactionPrompt = "Pick up Shovel";

    [Header("Audio")]
    [Tooltip("Sound played when picking up the shovel")]
    public AudioClip pickupSound;
    [Range(0f, 1f)]
    public float pickupSoundVolume = 1f;

    [Header("Visual Feedback")]
    [Tooltip("Highlight the object when looked at")]
    public bool highlightOnHover = true;
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Header("Dialogue (Optional)")]
    [Tooltip("Show dialogue when picked up")]
    public bool showDialogueOnPickup = false;
    public string pickupDialogue = "I picked up the shovel.";
    public string dialogueSpeaker = "";
    public float dialogueDuration = 2f;

    // Private
    private Material[] materials;
    private Color[] originalColors;
    private bool isPickedUp = false;

    void Start()
    {
        // Cache materials for highlighting
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            materials = renderer.materials;
            originalColors = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                originalColors[i] = materials[i].color;
            }
        }

        // Set to Interactable layer if it exists
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            gameObject.layer = interactableLayer;
        }
    }

    public void Interact()
    {
        if (isPickedUp) return;
        isPickedUp = true;

        // Set the flag in GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShovelPickedUp = true;
            Debug.Log("ShovelPickup: Shovel picked up! Flag set in GameManager.");
        }
        else
        {
            Debug.LogWarning("ShovelPickup: GameManager.Instance is null!");
        }

        // Play pickup sound
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupSoundVolume);
        }

        // Show dialogue if enabled
        if (showDialogueOnPickup && !string.IsNullOrEmpty(pickupDialogue))
        {
            SimpleDialogueTrigger.ShowDialogue(pickupDialogue, dialogueSpeaker, dialogueDuration);
        }

        // Destroy the shovel object
        Destroy(gameObject);
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }

    public void OnHoverEnter()
    {
        if (!highlightOnHover || materials == null) return;

        foreach (Material mat in materials)
        {
            mat.color = highlightColor;
        }
    }

    public void OnHoverExit()
    {
        if (!highlightOnHover || materials == null || originalColors == null) return;

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].color = originalColors[i];
        }
    }
}
