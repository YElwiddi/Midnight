using UnityEngine;
using System.Collections;

public class InteractableItem : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    public string itemName = "Item";
    public string interactionVerb = "pick up";
    public string itemDescription = "";
    public Sprite itemIcon;
    
    [Header("Inventory Settings")]
    public bool addToInventory = true;
    public bool isStackable = true;
    public int quantity = 1;
    public int maxStackSize = 99;
    
    [Header("Interaction Effects")]
    public bool highlightWhenLookedAt = true;
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
    public bool rotateWhenLookedAt = true;
    public float rotationSpeed = 50f;
    
    [Header("On Interaction")]
    public bool destroyOnInteract = true;
    public float destroyDelay = 0.2f;
    public AudioClip pickupSound;
    public GameObject pickupEffectPrefab;
    
    // Private variables
    private Color originalColor;
    private Material[] materials;
    private bool isHighlighted = false;
    
    void Start()
    {
        // Cache original materials and colors
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            materials = renderer.materials;
            if (materials.Length > 0)
            {
                originalColor = materials[0].color;
            }
        }
        
        // Make sure this object is on the right layer for interaction
        // You'll need to set up this layer in Unity's Layer settings
        if (LayerMask.NameToLayer("Interactable") != -1)
            gameObject.layer = LayerMask.NameToLayer("Interactable");
    }
    
    void Update()
    {
        // Rotate the item if enabled and highlighted
        if (rotateWhenLookedAt && isHighlighted)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
    
    // IInteractable implementation
    public void Interact()
    {
        Debug.Log($"Player interacted with {itemName}");
        
        // Perform interaction logic
        if (addToInventory)
        {
            // Add to player's inventory
            PlayerInventory inventory = FindObjectOfType<PlayerInventory>();
            if (inventory != null)
            {
                // Create inventory item
                PlayerInventory.InventoryItem item = new PlayerInventory.InventoryItem(itemName, itemDescription, itemIcon);
                item.quantity = quantity;
                item.isStackable = isStackable;
                item.maxStackSize = maxStackSize;
                
                // Add the item to inventory
                bool added = inventory.AddItem(item);
                
                if (!added)
                {
                    Debug.LogWarning($"Couldn't add {itemName} to inventory. Inventory might be full.");
                    return; // Don't destroy the item if it couldn't be added
                }
            }
            else
            {
                Debug.LogWarning("No PlayerInventory component found in scene.");
            }
        }
        
        // Play pickup sound if you have one
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }
        
        // Create a pickup effect if you want one
        if (pickupEffectPrefab != null)
        {
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Destroy the object if set to do so
        if (destroyOnInteract)
        {
            if (destroyDelay > 0)
                StartCoroutine(DestroyAfterDelay());
            else
                Destroy(gameObject);
        }
    }
    
    public string GetInteractionPrompt()
    {
        return interactionVerb + " " + itemName;
    }
    
    // Called when the crosshair hovers over this item
    public void OnHoverEnter()
    {
        isHighlighted = true;
        
        // Highlight the item if enabled
        if (highlightWhenLookedAt && materials != null)
        {
            foreach (Material mat in materials)
            {
                mat.color = highlightColor;
            }
        }
    }
    
    // Called when the crosshair moves away from this item
    public void OnHoverExit()
    {
        isHighlighted = false;
        
        // Restore original color
        if (highlightWhenLookedAt && materials != null)
        {
            foreach (Material mat in materials)
            {
                mat.color = originalColor;
            }
        }
    }
    
    private IEnumerator DestroyAfterDelay()
    {
        // Optional: Add pickup animation here
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}