using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class DialogueOption
{
    public string optionText;
    public int nextNodeId;
}

[System.Serializable]
public class DialogueNode
{
    public int id;
    public string npcText;
    public List<DialogueOption> options = new List<DialogueOption>();
    public bool isEndNode = false;
}

[System.Serializable]
public class DialogueTree
{
    public string dialogueName;
    public List<DialogueNode> nodes = new List<DialogueNode>();
}

public class DialogueSystem : MonoBehaviour, IInteractable
{
    [Header("NPC Settings")]
    public string npcName = "NPC";
    public Transform npcHead;
    public float rotationSpeed = 5f;
    
    [Header("Dialogue Data")]
    public List<DialogueTree> dialogueTrees = new List<DialogueTree>();
    public int activeDialogueTreeIndex = 0;
    
    [Header("UI Settings")]
    public float dialogueBoxWidth = 600f;
    public float dialogueBoxHeight = 300f;
    public float optionHeight = 50f;
    public float optionPadding = 10f;
    public Color dialogueBackgroundColor = new Color(0, 0, 0, 0.8f);
    public Color optionBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color optionHoverColor = new Color(0.3f, 0.3f, 0.5f, 1f);
    public Color textColor = Color.white;
    public Color nameTextColor = new Color(1f, 0.8f, 0.2f, 1f);
    public int fontSize = 20;
    public int nameFontSize = 24;
    
    // Reference to player
    private GameObject player;
    private Movement playerMovement;
    private Transform playerCamera;
    
    // UI Components
    private Canvas dialogueCanvas;
    private GameObject dialoguePanel;
    private TextMeshProUGUI npcTextComponent;
    private TextMeshProUGUI npcNameComponent;
    private List<GameObject> optionButtons = new List<GameObject>();
    
    // State tracking
    private int currentNodeId = 0;
    private bool isInDialogue = false;
    private Vector3 originalNpcHeadRotation;
    
    void Start()
    {
        // Find player
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<Movement>();
            playerCamera = playerMovement.playerCamera.transform;
        }
        else
        {
            Debug.LogError("DialogueSystem: Player not found! Make sure your player has the 'Player' tag.");
        }
        
        // Store original NPC head rotation if assigned
        if (npcHead != null)
        {
            originalNpcHeadRotation = npcHead.localEulerAngles;
        }
        
        // Create UI elements but keep them inactive until dialogue starts
        CreateDialogueUI();
    }
    
    void Update()
    {
        if (isInDialogue)
        {
            // Make NPC face player if head transform is assigned
            if (npcHead != null && playerCamera != null)
            {
                Vector3 directionToPlayer = playerCamera.position - npcHead.position;
                directionToPlayer.y = 0; // Only rotate horizontally
                
                if (directionToPlayer != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    npcHead.rotation = Quaternion.Slerp(npcHead.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
            
            // Check for escape key to exit dialogue
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndDialogue();
            }
        }
    }
    
    #region IInteractable Implementation
    
    public void Interact()
    {
        if (!isInDialogue)
        {
            StartDialogue();
        }
    }
    
    public string GetInteractionPrompt()
    {
        return "talk to " + npcName;
    }
    
    #endregion
    
    #region Dialogue Control
    
    public void StartDialogue()
    {
        if (isInDialogue || dialogueTrees.Count == 0 || 
            activeDialogueTreeIndex < 0 || activeDialogueTreeIndex >= dialogueTrees.Count)
        {
            Debug.LogWarning("DialogueSystem: Cannot start dialogue. Check dialogue data configuration.");
            return;
        }
        
        // Set state
        isInDialogue = true;
        
        // Modify player controls
        if (playerMovement != null)
        {
            playerMovement.DisableCameraControl();
            playerMovement.canMove = false;
        }
        
        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Activate dialogue UI
        dialogueCanvas.gameObject.SetActive(true);
        
        // Start with the first node (usually id 0)
        currentNodeId = 0;
        DisplayNode(currentNodeId);
    }
    
    public void EndDialogue()
    {
        if (!isInDialogue)
            return;
            
        // Set state
        isInDialogue = false;
        
        // Restore player controls
        if (playerMovement != null)
        {
            playerMovement.EnableAllInput();
        }
        
        // Hide cursor and lock it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Hide dialogue UI
        dialogueCanvas.gameObject.SetActive(false);
        
        // Reset NPC head rotation
        if (npcHead != null)
        {
            npcHead.localEulerAngles = originalNpcHeadRotation;
        }
    }
    
    private void DisplayNode(int nodeId)
    {
        DialogueTree activeTree = dialogueTrees[activeDialogueTreeIndex];
        DialogueNode node = activeTree.nodes.Find(n => n.id == nodeId);
        
        if (node == null)
        {
            Debug.LogError($"DialogueSystem: Node with ID {nodeId} not found!");
            EndDialogue();
            return;
        }
        
        // Display NPC text
        npcTextComponent.text = node.npcText;
        
        // Clear existing option buttons
        foreach (GameObject button in optionButtons)
        {
            Destroy(button);
        }
        optionButtons.Clear();
        
        // If it's an end node, show a "Close" button instead of options
        if (node.isEndNode || node.options.Count == 0)
        {
            CreateOptionButton("Close", -1, optionButtons.Count);
            return;
        }
        
        // Create option buttons
        for (int i = 0; i < node.options.Count; i++)
        {
            CreateOptionButton(node.options[i].optionText, node.options[i].nextNodeId, i);
        }
    }
    
    private void SelectOption(int nextNodeId)
    {
        // Special case: -1 means end dialogue
        if (nextNodeId == -1)
        {
            EndDialogue();
            return;
        }
        
        // Navigate to the next node
        currentNodeId = nextNodeId;
        DisplayNode(currentNodeId);
    }
    
    #endregion
    
    #region UI Creation
    
    private void CreateDialogueUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("DialogueCanvas");
        canvasObj.transform.SetParent(transform);
        dialogueCanvas = canvasObj.AddComponent<Canvas>();
        dialogueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dialogueCanvas.sortingOrder = 100; // Ensure it's rendered on top
        
        // Add required components
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create main panel
        dialoguePanel = CreatePanel(canvasObj.transform, "DialoguePanel", 
            new Vector2(0, 0), new Vector2(dialogueBoxWidth, dialogueBoxHeight), 
            dialogueBackgroundColor);
        
        // Create NPC name text
        GameObject namePanel = CreatePanel(dialoguePanel.transform, "NamePanel",
            new Vector2(-dialogueBoxWidth/2 + 100, dialogueBoxHeight/2 + 20), 
            new Vector2(180, 40), 
            dialogueBackgroundColor);
            
        npcNameComponent = CreateTextComponent(namePanel.transform, "NPCName", 
            npcName, nameFontSize, nameTextColor, TextAlignmentOptions.Center);
        
        // Create main text area
        GameObject textArea = CreatePanel(dialoguePanel.transform, "TextArea",
            new Vector2(0, 50), 
            new Vector2(dialogueBoxWidth - 40, dialogueBoxHeight/2), 
            new Color(0,0,0,0));
            
        npcTextComponent = CreateTextComponent(textArea.transform, "NPCText", 
            "Dialogue text goes here.", fontSize, textColor, TextAlignmentOptions.TopLeft);
        
        // Options area will be created dynamically for each node
        
        // Initially hide the dialogue UI
        dialogueCanvas.gameObject.SetActive(false);
    }
    
    private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        
        Image image = panel.AddComponent<Image>();
        image.color = color;
        
        return panel;
    }
    
    private TextMeshProUGUI CreateTextComponent(Transform parent, string name, string text, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(10, 10);
        rect.offsetMax = new Vector2(-10, -10);
        
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        
        return tmp;
    }
    
    private void CreateOptionButton(string text, int nextNodeId, int index)
    {
        float buttonY = -dialogueBoxHeight/2 + 80 + (optionHeight + optionPadding) * index;
        
        // Create button background
        GameObject button = CreatePanel(dialoguePanel.transform, $"Option_{index}",
            new Vector2(0, buttonY), 
            new Vector2(dialogueBoxWidth - 80, optionHeight), 
            optionBackgroundColor);
            
        // Add text
        TextMeshProUGUI buttonText = CreateTextComponent(button.transform, "OptionText", 
            text, fontSize - 2, textColor, TextAlignmentOptions.Left);
            
        // Add button component
        Button buttonComponent = button.AddComponent<Button>();
        ColorBlock colors = buttonComponent.colors;
        colors.normalColor = optionBackgroundColor;
        colors.highlightedColor = optionHoverColor;
        colors.pressedColor = optionHoverColor;
        buttonComponent.colors = colors;
        
        // Add click event
        int nodeIdCopy = nextNodeId; // Local copy for closure
        buttonComponent.onClick.AddListener(() => SelectOption(nodeIdCopy));
        
        // Track created button
        optionButtons.Add(button);
    }
    
    #endregion
    
    #region Public Methods for External Control
    
    // Change the active dialogue tree at runtime
    public void SetActiveDialogueTree(int index)
    {
        if (index >= 0 && index < dialogueTrees.Count)
        {
            activeDialogueTreeIndex = index;
        }
        else
        {
            Debug.LogError($"DialogueSystem: Invalid dialogue tree index {index}");
        }
    }
    
    // Check if dialogue is currently active
    public bool IsInDialogue()
    {
        return isInDialogue;
    }
    
    #endregion
}