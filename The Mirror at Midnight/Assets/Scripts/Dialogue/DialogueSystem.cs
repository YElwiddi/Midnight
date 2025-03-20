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
    // Add a custom event for StrangerDialogue to hook into
    public event Action onCustomInteract;
    
    [Header("NPC Settings")]
    public string npcName = "NPC";
    public Transform npcHead;
    public float rotationSpeed = 5f;
    
    [Header("Dialogue Data")]
    public List<DialogueTree> dialogueTrees = new List<DialogueTree>();
    public int activeDialogueTreeIndex = 0;
    
    [Header("UI Settings")]
    public float dialogueBoxWidth = 500f;
    public float dialogueBoxHeight = 250f;
    public float optionHeight = 40f;
    public float optionPadding = 5f;
    
    // Background is now fully transparent by default
    public Color dialogueBackgroundColor = new Color(0, 0, 0, 0);
    public Color nameBackgroundColor = new Color(0, 0, 0, 0.5f); // Semi-transparent for name panel
    public Color optionBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color optionHoverColor = new Color(0.3f, 0.3f, 0.5f, 1f);
    
    // Text colors
    public Color textColor = Color.white;
    public Color nameTextColor = new Color(1f, 0.8f, 0.2f, 1f);
    
    // Font settings
    public int fontSize = 16;
    public int nameFontSize = 20;
    
    [Header("Horror Font Settings")]
    public TMP_FontAsset horrorFont; // Reference to a horror-style font asset
    
    [Header("Text Typing Settings")]
    public float typingSpeed = 0.05f; // Seconds per character
    public AudioClip typingSoundEffect; // Optional sound effect for typing
    [Range(0.0f, 1.0f)]
    public float typingSoundVolume = 0.5f;
    public float typingSoundFrequency = 0.15f; // How often to play the sound (in seconds)
    
    // Reference to player
    private GameObject player;
    private Movement playerMovement;
    private Transform playerCamera;
    private PlayerInventory playerInventory;
    
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
    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private AudioSource audioSource;
    private float lastTypingSoundTime = 0f;
    
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
        
        // Setup audio source for typing sounds
        if (typingSoundEffect != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = typingSoundEffect;
            audioSource.volume = typingSoundVolume;
            audioSource.loop = false;
            audioSource.playOnAwake = false;
        }

        // Get players inventory
        playerInventory = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerInventory>();

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
            
            // Check for input to skip typing effect
            if (isTyping && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                CompleteTypingEffect();
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
    // Check if an IInteractable component with higher priority exists on this GameObject
    IInteractable[] interactables = GetComponents<IInteractable>();
    if (interactables.Length > 1 && interactables[0] != this)
    {
        // Let the higher priority IInteractable handle the interaction
        return;
    }

    // Default behavior
    Debug.Log("DialogueSystem: Using default interaction");
    //Debug.Log("Checking player inventory: " + playerInventory.GetAllItems()[0]);
    Debug.Log("Checking if we have cellar key: " + playerInventory.HasItem("Cellar Key"));
    if (!isInDialogue)
    {
        StartDialogue();
    }
}

public string GetInteractionPrompt()
{
    // Check if another IInteractable exists on this GameObject
    IInteractable[] interactables = GetComponents<IInteractable>();
    if (interactables.Length > 1 && interactables[0] != this)
    {
        // Let the higher priority IInteractable handle the prompt
        return interactables[0].GetInteractionPrompt();
    }

    return npcName;
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
    
    // Fire custom interact event for StrangerDialogue to hook into
    onCustomInteract?.Invoke();
    
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
            
        // Stop typing coroutine if running
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        // Set state
        isInDialogue = false;
        isTyping = false;
        
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
    
    // Log which dialogue tree is being used and whether player has Cellar Key
    if (nodeId == 0) // First node - print info only once at the start
    {
        string treeName = activeDialogueTreeIndex == 0 ? "Regular Dialogue Tree" : "Cellar Key Dialogue Tree";
        Debug.Log($"DialogueSystem: Displaying dialogue from {treeName}");
        
        // Check if player has the Cellar Key
        if (playerInventory != null)
        {
            bool hasKey = playerInventory.HasItem("Cellar Key");
            Debug.Log($"DialogueSystem: Player has Cellar Key: {hasKey}");
        }
    }
    
    // Clear existing option buttons
    ClearOptionButtons();
    
    // Start the typing effect for NPC text
    typingCoroutine = StartCoroutine(TypeText(node));
}
    
    private void ClearOptionButtons()
    {
        foreach (GameObject button in optionButtons)
        {
            Destroy(button);
        }
        optionButtons.Clear();
    }
    
    private void SelectOption(int nextNodeId)
    {
        // Clear any typing coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
        }
        
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
    
    private void CompleteTypingEffect()
    {
        if (isTyping && typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
            
            // Get current node
            DialogueTree activeTree = dialogueTrees[activeDialogueTreeIndex];
            DialogueNode node = activeTree.nodes.Find(n => n.id == currentNodeId);
            
            if (node != null)
            {
                // Show the full text immediately
                npcTextComponent.text = node.npcText;
                
                // Create option buttons
                CreateOptionsForNode(node);
            }
        }
    }
    
    private IEnumerator TypeText(DialogueNode node)
    {
        isTyping = true;
        
        // Reset typing sound timer
        lastTypingSoundTime = 0f;
        
        // Clear the text first
        npcTextComponent.text = "";
        
        // Type the text character by character
        string fullText = node.npcText;
        for (int i = 0; i < fullText.Length; i++)
        {
            // Add next character
            npcTextComponent.text = fullText.Substring(0, i + 1);
            
            // Play typing sound effect at intervals
            if (audioSource != null && typingSoundEffect != null)
            {
                float timeSinceLastSound = Time.time - lastTypingSoundTime;
                if (timeSinceLastSound >= typingSoundFrequency)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f); // Slight pitch variation
                    audioSource.Play();
                    lastTypingSoundTime = Time.time;
                }
            }
            
            yield return new WaitForSeconds(typingSpeed);
        }
        
        // Typing is done
        isTyping = false;
        typingCoroutine = null;
        
        // Create option buttons only after text is fully displayed
        CreateOptionsForNode(node);
    }
    
    private void CreateOptionsForNode(DialogueNode node)
    {
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
        
        // Create main panel - Now with completely transparent background
        dialoguePanel = CreatePanel(canvasObj.transform, "DialoguePanel", 
            new Vector2(0, -Screen.height/4), new Vector2(dialogueBoxWidth, dialogueBoxHeight * 0.7f), 
            new Color(0, 0, 0, 0));
        
        // Ensure the image component uses a transparent raycast target
        Image panelImage = dialoguePanel.GetComponent<Image>();
        panelImage.raycastTarget = false; // Allows clicks to pass through
        
        // Create NPC name text - Semi-transparent background for visibility
        GameObject namePanel = CreatePanel(dialoguePanel.transform, "NamePanel",
            new Vector2(-dialogueBoxWidth/2 + 80, dialogueBoxHeight * 0.35f - 10), 
            new Vector2(140, 30), 
            nameBackgroundColor);
            
        npcNameComponent = CreateTextComponent(namePanel.transform, "NPCName", 
            npcName, nameFontSize, nameTextColor, TextAlignmentOptions.Center);
        
        // Apply horror font if assigned
        if (horrorFont != null)
        {
            npcNameComponent.font = horrorFont;
        }
        
        // Add some horror-style text effects
        npcNameComponent.enableVertexGradient = true;
        npcNameComponent.colorGradient = new VertexGradient(
            nameTextColor,
            nameTextColor,
            new Color(nameTextColor.r * 0.5f, nameTextColor.g * 0.3f, nameTextColor.b * 0.3f, nameTextColor.a),
            new Color(nameTextColor.r * 0.5f, nameTextColor.g * 0.3f, nameTextColor.b * 0.3f, nameTextColor.a)
        );
        
        // Create main text area - Truly transparent background with no Image component
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(dialoguePanel.transform, false);
        
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
        textAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
        textAreaRect.pivot = new Vector2(0.5f, 0.5f);
        textAreaRect.anchoredPosition = new Vector2(0, 30);
        textAreaRect.sizeDelta = new Vector2(dialogueBoxWidth - 40, dialogueBoxHeight * 0.3f);
            
        npcTextComponent = CreateTextComponent(textArea.transform, "NPCText", 
            "", fontSize, textColor, TextAlignmentOptions.TopLeft);
        
        // Apply horror font to dialogue text
        if (horrorFont != null)
        {
            npcTextComponent.font = horrorFont;
        }
        
        // Add some additional horror text styling
        npcTextComponent.enableVertexGradient = true;
        npcTextComponent.colorGradient = new VertexGradient(
            textColor,
            textColor,
            new Color(textColor.r * 0.8f, textColor.g * 0.8f, textColor.b * 0.8f, textColor.a),
            new Color(textColor.r * 0.7f, textColor.g * 0.7f, textColor.b * 0.7f, textColor.a)
        );
        
        // Optional - Add a slight character spacing for creepy effect
        npcTextComponent.characterSpacing = 2f;
        
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
        
        // For completely transparent panels, we can skip adding an Image component
        if (color.a > 0)
        {
            Image image = panel.AddComponent<Image>();
            image.color = color;
            
            // For semi-transparent panels, use a special sprite
            if (color.a < 1)
            {
                // Use a simple transparent sprite if available
                image.sprite = null; // You can assign a transparent sprite in the inspector
                image.type = Image.Type.Sliced;
            }
        }
        else if (name != "TextArea") // Still add image for non-text areas but make it invisible
        {
            // Add an invisible image component for layout purposes only
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = false; // Disable raycast for truly transparent areas
        }
        
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
        
        // Apply horror font if assigned
        if (horrorFont != null)
        {
            tmp.font = horrorFont;
        }
        
        return tmp;
    }
    
    private void CreateOptionButton(string text, int nextNodeId, int index)
    {
        // Reduced option height and spacing
        float buttonHeight = optionHeight * 0.8f;
        float buttonPadding = optionPadding * 0.5f;
        float buttonY = -dialogueBoxHeight * 0.35f + 60 + (buttonHeight + buttonPadding) * index;
        
        // Create button background - Buttons remain with background for usability
        GameObject button = CreatePanel(dialoguePanel.transform, $"Option_{index}",
            new Vector2(0, buttonY), 
            new Vector2(dialogueBoxWidth - 100, buttonHeight), 
            optionBackgroundColor);
            
        // Add text
        TextMeshProUGUI buttonText = CreateTextComponent(button.transform, "OptionText", 
            text, fontSize - 4, textColor, TextAlignmentOptions.Left);
            
        // Apply horror font to option text
        if (horrorFont != null)
        {
            buttonText.font = horrorFont;
        }
        
        // Add horror-style text effects to options
        buttonText.enableVertexGradient = true;
        buttonText.colorGradient = new VertexGradient(
            textColor,
            textColor,
            new Color(textColor.r * 0.8f, textColor.g * 0.8f, textColor.b * 0.8f, textColor.a),
            new Color(textColor.r * 0.7f, textColor.g * 0.7f, textColor.b * 0.7f, textColor.a)
        );
        
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
            // Update the name display
            if (npcNameComponent != null && dialogueTrees[index].dialogueName != null)
            {
                npcName = dialogueTrees[index].dialogueName;
                npcNameComponent.text = npcName;
            }
            Debug.Log($"DialogueSystem: Set active dialogue tree to index {index}, name: {dialogueTrees[index].dialogueName}");
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