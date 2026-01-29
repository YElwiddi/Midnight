using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryPanel;
    [Tooltip("The Grid Layout Group that will hold item slots")]
    public GridLayoutGroup itemsGrid;
    public GameObject itemSlotPrefab;
    
    [Header("UI Settings")]
    public KeyCode toggleInventoryKey = KeyCode.I;
    public bool hideMouseCursorWhenClosed = true;
    public bool disableCameraMovementWhenOpen = true;
    
    // References
    private PlayerInventory playerInventory;
    private Movement movementScript;
    
    void Start()
    {
        // Get reference to the player's inventory
        playerInventory = GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            playerInventory = FindObjectOfType<PlayerInventory>();
        }
        
        if (playerInventory == null)
        {
            Debug.LogError("InventoryUI: No PlayerInventory script found!");
            enabled = false;
            return;
        }
        
        // Get reference to the movement script
        if (disableCameraMovementWhenOpen)
        {
            movementScript = GetComponent<Movement>();
            if (movementScript == null)
            {
                movementScript = FindObjectOfType<Movement>();
            }
            
            if (movementScript == null)
            {
                Debug.LogWarning("InventoryUI: No Movement script found! Camera control will not be disabled when inventory is open.");
            }
        }
        
        // Check if UI references are set
        if (inventoryPanel == null)
        {
            Debug.LogError("InventoryUI: No inventory panel assigned!");
            enabled = false;
            return;
        }
        
        if (itemsGrid == null)
        {
            Debug.LogWarning("InventoryUI: No items grid assigned, trying to find one...");
            itemsGrid = inventoryPanel.GetComponentInChildren<GridLayoutGroup>();
            
            if (itemsGrid == null)
            {
                Debug.LogError("InventoryUI: No GridLayoutGroup found in the inventory panel hierarchy!");
                enabled = false;
                return;
            }
        }
        
        // Subscribe to inventory events
        playerInventory.OnInventoryChanged += UpdateInventoryUI;
        
        // Hide inventory at start
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
        
        // Create slot prefab if not assigned
        if (itemSlotPrefab == null)
        {
            Debug.LogWarning("InventoryUI: No item slot prefab assigned. Using a basic template.");
            CreateBasicSlotPrefab();
        }
    }
    
    void Update()
    {
        // Toggle inventory when key is pressed
        if (Input.GetKeyDown(toggleInventoryKey))
        {
            ToggleInventory();
        }
    }
    
    public void ToggleInventory()
    {
        if (inventoryPanel != null)
        {
            bool newState = !inventoryPanel.activeSelf;
            
            // Toggle panel visibility
            inventoryPanel.SetActive(newState);
            
            // Update UI if opening
            if (newState)
            {
                UpdateInventoryUI();
                
                // Show cursor
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
                // Disable camera movement
                if (disableCameraMovementWhenOpen && movementScript != null)
                {
                    // Try to use the enhanced methods if available
                    try
                    {
                        movementScript.DisableCameraControl();
                    }
                    catch
                    {
                        // Fall back to the basic flag if enhanced methods aren't available
                        movementScript.canMove = false;
                    }
                }
            }
            else
            {
                // Re-enable camera movement
                if (disableCameraMovementWhenOpen && movementScript != null)
                {
                    // Try to use the enhanced methods if available
                    try
                    {
                        movementScript.EnableCameraControl();
                    }
                    catch
                    {
                        // Fall back to the basic flag if enhanced methods aren't available
                        movementScript.canMove = true;
                    }
                }
                
                // Hide and lock cursor when closing inventory
                if (hideMouseCursorWhenClosed)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
    
    void UpdateInventoryUI()
    {
        if (itemsGrid == null || playerInventory == null)
            return;
            
        // Clear current slots
        foreach (Transform child in itemsGrid.transform)
        {
            Destroy(child.gameObject);
        }
        
        // Get all items
        List<PlayerInventory.InventoryItem> items = playerInventory.GetAllItems();
        
        // Create a slot for each item
        foreach (PlayerInventory.InventoryItem item in items)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemsGrid.transform);
            
            // Set item name
            TextMeshProUGUI itemName = slotObj.GetComponentInChildren<TextMeshProUGUI>();
            if (itemName != null)
            {
                if (item.quantity > 1)
                {
                    itemName.text = $"{item.itemName} ({item.quantity})";
                }
                else
                {
                    itemName.text = item.itemName;
                }
            }
            
            // Set item icon if available
            Image itemIcon = slotObj.GetComponentInChildren<Image>();
            if (itemIcon != null && item.itemIcon != null)
            {
                itemIcon.sprite = item.itemIcon;
                itemIcon.enabled = true;
            }
            
            // Add tooltip functionality or other features here
        }
    }
    
    // Create a basic slot prefab if none is assigned
    void CreateBasicSlotPrefab()
    {
        // Create a basic prefab for item slots
        GameObject prefab = new GameObject("ItemSlot");
        
        // Add RectTransform
        RectTransform rect = prefab.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 100);
        
        // Add image background
        Image background = prefab.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Add image for item icon
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(prefab.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.3f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        
        Image icon = iconObj.AddComponent<Image>();
        icon.enabled = false; // Will be enabled when an icon is assigned
        
        // Add text for item name
        GameObject textObj = new GameObject("ItemName");
        textObj.transform.SetParent(prefab.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 0.3f);
        textRect.offsetMin = new Vector2(5, 2);
        textRect.offsetMax = new Vector2(-5, -2);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        
        // Store prefab
        itemSlotPrefab = prefab;
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= UpdateInventoryUI;
        }
    }
}