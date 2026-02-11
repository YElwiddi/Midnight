using System.Collections.Generic;
using UnityEngine;
using System;

public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public class InventoryItem
    {
        public string itemName;
        public string itemDescription;
        public Sprite itemIcon;
        public int quantity = 1;
        public bool isStackable = true;
        public int maxStackSize = 99;
        
        // Optional: store additional item properties
        public Dictionary<string, object> properties = new Dictionary<string, object>();
        
        public InventoryItem(string name, string description = "", Sprite icon = null)
        {
            itemName = name;
            itemDescription = description;
            itemIcon = icon;
        }
    }
    
    [Header("Inventory Settings")]
    public int inventorySlots = 20;
    public bool autoEquipItems = false;
    
    [Header("Events")]
    public bool enableEvents = true;
    
    // The actual inventory storage
    private List<InventoryItem> items = new List<InventoryItem>();
    
    // Events
    public event Action<InventoryItem> OnItemAdded;
    public event Action<InventoryItem> OnItemRemoved;
    public event Action OnInventoryChanged;
    
    // Singleton pattern (optional)
    public static PlayerInventory Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton setup (optional)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Optional: make this persist between scenes
        // DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        // Connect to the interaction system
        InteractionSystem interactionSystem = FindObjectOfType<InteractionSystem>();
        if (interactionSystem != null)
        {
            Debug.Log("PlayerInventory: Connected to InteractionSystem");
        }
        else
        {
            Debug.LogWarning("PlayerInventory: InteractionSystem not found!");
        }
    }
    
    public bool AddItem(string itemName, string description = "", Sprite icon = null)
    {
        // Create a new inventory item
        InventoryItem newItem = new InventoryItem(itemName, description, icon);
        return AddItem(newItem);
    }
    
    public bool AddItem(InventoryItem item)
    {
        // Check if inventory is full
        if (items.Count >= inventorySlots && !CanStack(item))
        {
            Debug.Log($"Cannot add {item.itemName}: Inventory full!");
            return false;
        }
        
        // Try to stack the item if it's stackable
        if (item.isStackable)
        {
            foreach (InventoryItem existingItem in items)
            {
                // If same item type and not at max stack
                if (existingItem.itemName == item.itemName && existingItem.quantity < existingItem.maxStackSize)
                {
                    // Calculate how many can be added to this stack
                    int spaceInStack = existingItem.maxStackSize - existingItem.quantity;
                    int amountToAdd = Mathf.Min(item.quantity, spaceInStack);
                    
                    // Add to stack
                    existingItem.quantity += amountToAdd;
                    
                    // Reduce quantity of item being added
                    item.quantity -= amountToAdd;
                    
                    // If fully stacked, return success
                    if (item.quantity <= 0)
                    {
                        // Fire events
                        if (enableEvents)
                        {
                            OnInventoryChanged?.Invoke();
                        }
                        
                        Debug.Log($"Added {amountToAdd}x {item.itemName} to existing stack");
                        return true;
                    }
                }
            }
        }
        
        // If we get here, either the item isn't stackable or we still have quantity left
        // Add as a new item if we have space
        if (items.Count < inventorySlots)
        {
            items.Add(item);
            
            // Fire events
            if (enableEvents)
            {
                OnItemAdded?.Invoke(item);
                OnInventoryChanged?.Invoke();
            }
            
            Debug.Log($"Added {item.quantity}x {item.itemName} to inventory");
            return true;
        }
        
        Debug.Log($"Could not add all of {item.itemName} to inventory");
        return false;
    }
    
    public bool RemoveItem(string itemName, int quantity = 1)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            InventoryItem item = items[i];
            
            if (item.itemName == itemName)
            {
                if (item.quantity > quantity)
                {
                    // Just reduce the quantity
                    item.quantity -= quantity;
                    
                    // Fire events
                    if (enableEvents)
                    {
                        OnInventoryChanged?.Invoke();
                    }
                    
                    Debug.Log($"Removed {quantity}x {itemName} from inventory");
                    return true;
                }
                else
                {
                    // Remove the entire stack
                    quantity -= item.quantity;
                    
                    // Store reference for event
                    InventoryItem removedItem = item;
                    
                    // Remove the item
                    items.RemoveAt(i);
                    
                    // Fire events
                    if (enableEvents)
                    {
                        OnItemRemoved?.Invoke(removedItem);
                        OnInventoryChanged?.Invoke();
                    }
                    
                    // If we've removed the requested quantity, return success
                    if (quantity <= 0)
                    {
                        Debug.Log($"Removed {itemName} from inventory");
                        return true;
                    }
                }
            }
        }
        
        // If we get here, we couldn't remove the requested quantity
        Debug.Log($"Could not remove {quantity}x {itemName} from inventory");
        return false;
    }
    
    public bool HasItem(string itemName, int quantity = 1)
    {
        int count = 0;
        
        foreach (InventoryItem item in items)
        {
            if (item.itemName == itemName)
            {
                count += item.quantity;
                if (count >= quantity)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public int GetItemCount(string itemName)
    {
        int count = 0;
        
        foreach (InventoryItem item in items)
        {
            if (item.itemName == itemName)
            {
                count += item.quantity;
            }
        }
        
        return count;
    }
    
    public List<InventoryItem> GetAllItems()
    {
        return new List<InventoryItem>(items);
    }
    
    public void ClearInventory()
    {
        items.Clear();
        
        // Fire events
        if (enableEvents)
        {
            OnInventoryChanged?.Invoke();
        }
        
        Debug.Log("Inventory cleared");
    }
    
    // Helper method to check if an item can be stacked
    private bool CanStack(InventoryItem item)
    {
        if (!item.isStackable)
            return false;
            
        foreach (InventoryItem existingItem in items)
        {
            if (existingItem.itemName == item.itemName && existingItem.quantity < existingItem.maxStackSize)
            {
                return true;
            }
        }
        
        return false;
    }
    
    // Called when InteractableItem.Interact() is executed
    // Add this to connect with your existing interaction system
    public void OnItemInteraction(InteractableItem interactableItem)
    {
        if (interactableItem != null)
        {
            AddItem(interactableItem.itemName);
        }
    }
}