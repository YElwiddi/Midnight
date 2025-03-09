using UnityEngine;
using System.Collections;

public class InteractableItem : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    public string itemName = "Item";
    public string interactionVerb = "pick up";
    
    [Header("Interaction Effects")]
    public bool highlightWhenLookedAt = true;
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
    public bool rotateWhenLookedAt = true;
    public float rotationSpeed = 50f;
    
    [Header("On Interaction")]
    public bool destroyOnInteract = true;
    public float destroyDelay = 0.2f;
    public bool addToInventory = true;
    
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
            // Add to inventory system if you have one
            // For example: InventoryManager.Instance.AddItem(itemID);
            Debug.Log($"Added {itemName} to inventory");
        }
        
        // Play pickup sound if you have one
        // AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        
        // Create a pickup effect if you want one
        // Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        
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