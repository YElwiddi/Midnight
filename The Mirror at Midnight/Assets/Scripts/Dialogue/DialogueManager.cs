using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private Transform choiceButtonContainer;

    [Header("Choice Button Layout")]
    [SerializeField] private ChoiceLayoutMode layoutMode = ChoiceLayoutMode.Horizontal;
    [SerializeField] private Vector2 containerAnchorMin = new Vector2(0, 0);
    [SerializeField] private Vector2 containerAnchorMax = new Vector2(0, 0);
    [SerializeField] private Vector2 containerAnchoredPosition = new Vector2(300, 60);
    [SerializeField] private Vector2 containerSizeDelta = new Vector2(1000, 100);
    [SerializeField] private float buttonSpacing = 10f;
    [SerializeField] private Vector2 buttonSize = new Vector2(450, 80);
    [SerializeField] private Vector2 buttonPositionOffset = new Vector2(0, 0);
    [SerializeField] private float fontSizeMultiplier = 0.5f;
    [SerializeField] private bool useLayoutGroup = false;

    [Header("Choice Button Text Style")]
    [SerializeField] private TMP_FontAsset choiceButtonFont;
    [SerializeField] private FontStyles choiceButtonFontStyle = FontStyles.Normal;
    [SerializeField] private Color choiceButtonTextColor = Color.white;
    [SerializeField] private float choiceButtonFontSize = 16f;
    [SerializeField] private float choiceButtonTextScale = 1f;

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSONAsset;

    private Story currentStory;
    private bool dialogueIsPlaying;

    // Reference to GameManager for variable changes
    private GameManager gameManager;

    // Reference to Movement script for controlling player input
    private Movement movementScript;

    // Current NPC being interacted with
    private Transform currentNPC;

    private static DialogueManager instance;

    public enum ChoiceLayoutMode
    {
        Horizontal,
        Vertical,
        Grid
    }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("Found more than one Dialogue Manager in the scene");
        }
        instance = this;

        gameManager = FindObjectOfType<GameManager>();
        movementScript = FindObjectOfType<Movement>();
    }

    public static DialogueManager GetInstance()
    {
        return instance;
    }

    private void Start()
    {
        dialogueIsPlaying = false;
        dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        // Return if dialogue isn't playing
        if (!dialogueIsPlaying)
        {
            return;
        }

        // Handle continue with space key if there are no choices
        if (currentStory.currentChoices.Count == 0 && Input.GetMouseButtonDown(0))
        {
            ContinueStory();
        }

        // Allow ESC key to close dialogue
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC pressed - closing dialogue");
            ExitDialogueMode();
        }
    }

    public void EnterDialogueMode(TextAsset inkJSON, Transform npcTransform)
    {
        Debug.Log("=== EnterDialogueMode Called ===");
        Debug.Log($"inkJSON is null: {inkJSON == null}");
        Debug.Log($"npcTransform is null: {npcTransform == null}");
        Debug.Log($"dialoguePanel is null: {dialoguePanel == null}");
        Debug.Log($"dialogueText is null: {dialogueText == null}");
        Debug.Log($"choiceButtonPrefab is null: {choiceButtonPrefab == null}");
        Debug.Log($"choiceButtonContainer is null: {choiceButtonContainer == null}");

        // Set current NPC
        currentNPC = npcTransform;

        dialoguePanel.SetActive(true);
        Debug.Log($"Dialogue Panel active after: {dialoguePanel.activeSelf}");

        // Check panel position and size
        RectTransform rect = dialoguePanel.GetComponent<RectTransform>();
        Debug.Log($"Panel position: {rect.anchoredPosition}");
        Debug.Log($"Panel size: {rect.sizeDelta}");
        Debug.Log($"Panel anchors - Min: {rect.anchorMin}, Max: {rect.anchorMax}");

        // Check if panel is visible
        Canvas canvas = dialoguePanel.GetComponentInParent<Canvas>();

        dialoguePanel.SetActive(true);
        if (canvas == null)
        {
            Debug.LogError("Dialogue Panel is not under a Canvas! Creating one...");

            // Find or create a canvas
            canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("DialogueCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Move panel under canvas
            dialoguePanel.transform.SetParent(canvas.transform, false);
        }

        // FIX: Set proper size and position
        rect = dialoguePanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0.3f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = new Vector2(50, 50);
        rect.offsetMax = new Vector2(-50, 0);
        Debug.Log($"Canvas found: {canvas != null}");
        if (canvas != null)
        {
            Debug.Log($"Canvas render mode: {canvas.renderMode}");
            Debug.Log($"Canvas sort order before: {canvas.sortingOrder}");
            // Ensure dialogue canvas is on top
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);
            Debug.Log($"Canvas sort order after: {canvas.sortingOrder}");
        }

        if (dialoguePanel != null)
        {
            Debug.Log($"Dialogue Panel name: {dialoguePanel.name}");
            Debug.Log($"Dialogue Panel active before: {dialoguePanel.activeSelf}");
        }
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;
        dialoguePanel.SetActive(true);

        // Unlock cursor and disable player input during dialogue
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (movementScript != null)
        {
            movementScript.DisableAllInput();
            // Set camera to face NPC
            movementScript.SetCameraTarget(currentNPC);
        }

        // Bind external functions if needed
        currentStory.BindExternalFunction("ChangeGameVariable", (string varName, int value) => {
            gameManager.ChangeVariable(varName, value);
        });

        // Set initial values from GameManager to Ink variables
        if (gameManager != null)
        {
            currentStory.variablesState["player_karma"] = gameManager.playerKarma;
        }

        // Make sure to start at the beginning
        if (currentStory.canContinue)
        {
            // Try to go to start knot if it exists
            if (currentStory.KnotContainerWithName("start") != null)
            {
                currentStory.ChoosePathString("start");
            }
        }

        // Setup choice container with inspector values
        SetupChoiceContainer();

        // Start the dialogue
        ContinueStory();
    }

    private void SetupChoiceContainer()
    {
        if (choiceButtonContainer == null)
        {
            Debug.LogWarning("Choice button container is null");
            return;
        }

        RectTransform containerRect = choiceButtonContainer.GetComponent<RectTransform>();
        if (containerRect != null)
        {
            containerRect.anchorMin = containerAnchorMin;
            containerRect.anchorMax = containerAnchorMax;
            containerRect.anchoredPosition = containerAnchoredPosition;
            containerRect.sizeDelta = containerSizeDelta;
            Debug.Log($"Set choice container - AnchorMin: {containerAnchorMin}, AnchorMax: {containerAnchorMax}");
            Debug.Log($"Set choice container - Position: {containerAnchoredPosition}, SizeDelta: {containerSizeDelta}");
        }

        // Enable or disable layout groups based on inspector setting
        if (!useLayoutGroup)
        {
            DisableLayoutGroups();
        }
        else
        {
            EnableLayoutGroup();
        }
    }

    private void DisableLayoutGroups()
    {
        HorizontalLayoutGroup horizLayout = choiceButtonContainer.GetComponent<HorizontalLayoutGroup>();
        if (horizLayout != null)
        {
            horizLayout.enabled = false;
            Debug.Log("Disabled HorizontalLayoutGroup on choice container");
        }

        VerticalLayoutGroup vertLayout = choiceButtonContainer.GetComponent<VerticalLayoutGroup>();
        if (vertLayout != null)
        {
            vertLayout.enabled = false;
            Debug.Log("Disabled VerticalLayoutGroup on choice container");
        }

        LayoutGroup layoutGroup = choiceButtonContainer.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
            Debug.Log("Disabled LayoutGroup on choice container");
        }
    }

    private void EnableLayoutGroup()
    {
        // Remove existing layout groups
        HorizontalLayoutGroup horizLayout = choiceButtonContainer.GetComponent<HorizontalLayoutGroup>();
        VerticalLayoutGroup vertLayout = choiceButtonContainer.GetComponent<VerticalLayoutGroup>();
        GridLayoutGroup gridLayout = choiceButtonContainer.GetComponent<GridLayoutGroup>();

        if (horizLayout != null) Destroy(horizLayout);
        if (vertLayout != null) Destroy(vertLayout);
        if (gridLayout != null) Destroy(gridLayout);

        // Add appropriate layout group based on mode
        switch (layoutMode)
        {
            case ChoiceLayoutMode.Horizontal:
                HorizontalLayoutGroup hLayout = choiceButtonContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hLayout.spacing = buttonSpacing;
                hLayout.childAlignment = TextAnchor.MiddleCenter;
                hLayout.childControlWidth = false;
                hLayout.childControlHeight = false;
                hLayout.childForceExpandWidth = false;
                hLayout.childForceExpandHeight = false;
                Debug.Log("Enabled HorizontalLayoutGroup");
                break;

            case ChoiceLayoutMode.Vertical:
                VerticalLayoutGroup vLayout = choiceButtonContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                vLayout.spacing = buttonSpacing;
                vLayout.childAlignment = TextAnchor.MiddleCenter;
                vLayout.childControlWidth = false;
                vLayout.childControlHeight = false;
                vLayout.childForceExpandWidth = false;
                vLayout.childForceExpandHeight = false;
                Debug.Log("Enabled VerticalLayoutGroup");
                break;

            case ChoiceLayoutMode.Grid:
                GridLayoutGroup gLayout = choiceButtonContainer.gameObject.AddComponent<GridLayoutGroup>();
                gLayout.spacing = new Vector2(buttonSpacing, buttonSpacing);
                gLayout.cellSize = buttonSize;
                gLayout.childAlignment = TextAnchor.MiddleCenter;
                Debug.Log("Enabled GridLayoutGroup");
                break;
        }
    }

    private void ExitDialogueMode()
    {
        dialogueIsPlaying = false;
        dialoguePanel.SetActive(false);
        dialogueText.text = "";

        // Apply any variable changes from Ink to GameManager
        ApplyVariableChanges();

        // Clear camera target and re-enable player input
        if (movementScript != null)
        {
            movementScript.ClearCameraTarget();
            movementScript.EnableAllInput();
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ContinueStory()
    {
        Debug.Log("=== ContinueStory Called ===");
        Debug.Log($"Story can continue: {currentStory.canContinue}");

        if (currentStory.canContinue)
        {
            // Set text for current dialogue line
            string storyText = currentStory.Continue();
            Debug.Log($"Story text: {storyText}");
            Debug.Log($"Setting dialogue text to: {storyText}");
            dialogueText.text = storyText;

            // Check if text was actually set
            Debug.Log($"Dialogue text now shows: {dialogueText.text}");
            Debug.Log($"Text object active: {dialogueText.gameObject.activeSelf}");

            // Display choices, if any
            DisplayChoices();
        }
        else
        {
            Debug.Log("Story cannot continue, exiting dialogue");
            ExitDialogueMode();
        }
    }

    private void DisplayChoices()
    {
        List<Choice> currentChoices = currentStory.currentChoices;

        Debug.Log($"=== DisplayChoices Called ===");
        Debug.Log($"Number of choices: {currentChoices.Count}");

        // Clear existing choice buttons
        foreach (Transform child in choiceButtonContainer)
        {
            Destroy(child.gameObject);
        }

        // Create button for each choice
        for (int i = 0; i < currentChoices.Count; i++)
        {
            Choice choice = currentChoices[i];
            Debug.Log($"Creating button for choice: {choice.text}");
            GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceButtonContainer, false);
            
            if (choiceButton != null)
            {
                Debug.Log("Choice button instantiated successfully");

                // Position buttons if not using layout groups
                if (!useLayoutGroup)
                {
                    PositionChoiceButton(choiceButton, i, currentChoices.Count);
                }
                else
                {
                    // Set button size for layout groups
                    RectTransform buttonRect = choiceButton.GetComponent<RectTransform>();
                    if (buttonRect != null)
                    {
                        buttonRect.sizeDelta = buttonSize;
                    }
                }

                Debug.Log($"Button active: {choiceButton.activeSelf}");
                Debug.Log($"Button position: {choiceButton.transform.localPosition}");
                Debug.Log($"Button scale: {choiceButton.transform.localScale}");

                // Ensure the button is active
                if (!choiceButton.activeSelf)
                {
                    choiceButton.SetActive(true);
                    Debug.Log("Activated choice button");
                }

                SetupChoiceButtonText(choiceButton, choice.text);
                SetupChoiceButtonListener(choiceButton, choice.index);
                
                // Force UI layout rebuild to ensure hitboxes align with visuals
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(choiceButton.GetComponent<RectTransform>());
            }
            else
            {
                Debug.LogError("Failed to instantiate choice button");
            }
        }

        Debug.Log($"Choice container position: {choiceButtonContainer.localPosition}");
        Debug.Log($"Choice container active: {choiceButtonContainer.gameObject.activeSelf}");

        if (currentChoices.Count == 0)
        {
            Debug.Log("No choices to display - player can continue with space or wait");
        }
    }

    private void PositionChoiceButton(GameObject choiceButton, int index, int totalChoices)
    {
        RectTransform buttonRect = choiceButton.GetComponent<RectTransform>();
        if (buttonRect == null) return;

        // Reset transform to clean slate
        buttonRect.localScale = Vector3.one;
        buttonRect.localRotation = Quaternion.identity;
        
        // Ensure anchors and pivot are set to center for proper positioning
        // This aligns the clickable hitbox with the visual button
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        // Set button size
        buttonRect.sizeDelta = buttonSize;
        
        // Reset position before calculating new position
        buttonRect.anchoredPosition = Vector2.zero;

        switch (layoutMode)
        {
            case ChoiceLayoutMode.Horizontal:
                PositionHorizontal(buttonRect, index, totalChoices);
                break;

            case ChoiceLayoutMode.Vertical:
                PositionVertical(buttonRect, index, totalChoices);
                break;

            case ChoiceLayoutMode.Grid:
                PositionGrid(buttonRect, index, totalChoices);
                break;
        }

        Debug.Log($"Set button {index} position: {buttonRect.anchoredPosition}");
    }

    private void PositionHorizontal(RectTransform buttonRect, int index, int totalChoices)
    {
        RectTransform containerRect = choiceButtonContainer.GetComponent<RectTransform>();
        float containerWidth = containerRect.rect.width;
        
        float totalButtonWidth = (buttonSize.x * totalChoices) + (buttonSpacing * (totalChoices - 1));
        float startX = -totalButtonWidth / 2 + buttonSize.x / 2;
        float xPos = startX + index * (buttonSize.x + buttonSpacing);
        
        buttonRect.anchoredPosition = new Vector2(xPos + buttonPositionOffset.x, buttonPositionOffset.y);
    }

    private void PositionVertical(RectTransform buttonRect, int index, int totalChoices)
    {
        float totalButtonHeight = (buttonSize.y * totalChoices) + (buttonSpacing * (totalChoices - 1));
        float startY = totalButtonHeight / 2 - buttonSize.y / 2;
        float yPos = startY - index * (buttonSize.y + buttonSpacing);
        
        buttonRect.anchoredPosition = new Vector2(buttonPositionOffset.x, yPos + buttonPositionOffset.y);
    }

    private void PositionGrid(RectTransform buttonRect, int index, int totalChoices)
    {
        // Calculate grid dimensions (2 columns)
        int columns = 2;
        int row = index / columns;
        int col = index % columns;
        
        float xPos = (col - 0.5f) * (buttonSize.x + buttonSpacing);
        float yPos = -row * (buttonSize.y + buttonSpacing);
        
        buttonRect.anchoredPosition = new Vector2(xPos + buttonPositionOffset.x, yPos + buttonPositionOffset.y);
    }

    private void SetupChoiceButtonText(GameObject choiceButton, string choiceText)
    {
        TextMeshProUGUI textComponent = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            // Fix text stretching by setting proper RectTransform anchors
            RectTransform textRect = textComponent.GetComponent<RectTransform>();
            if (textRect != null)
            {
                // Set anchors to stretch to fill the button with padding
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 5); // Left and bottom padding
                textRect.offsetMax = new Vector2(-10, -5); // Right and top padding
                
                // DO NOT scale the text transform - this causes hitbox misalignment!
                // Use font size instead
                textRect.localScale = Vector3.one;
            }
            
            textComponent.text = choiceText;
            
            // Disable auto-sizing to prevent stretching
            textComponent.enableAutoSizing = false;
            
            // Apply custom font if specified
            if (choiceButtonFont != null)
            {
                textComponent.font = choiceButtonFont;
            }
            
            // Apply font style
            textComponent.fontStyle = choiceButtonFontStyle;
            
            // Apply text color
            textComponent.color = choiceButtonTextColor;
            
            // Apply font size (can use either the new fontSize or the multiplier for backward compatibility)
            if (choiceButtonFontSize > 0)
            {
                textComponent.fontSize = choiceButtonFontSize;
            }
            else if (fontSizeMultiplier != 1f)
            {
                // Fallback to multiplier if new fontSize not set
                textComponent.fontSize = textComponent.fontSize * fontSizeMultiplier;
            }
            
            Debug.Log($"Set button text to: {choiceText}");
            Debug.Log($"Set button font size to: {textComponent.fontSize}");
            Debug.Log($"Set button text scale to: {choiceButtonTextScale}");
            Debug.Log($"Text component active: {textComponent.gameObject.activeSelf}");
        }
        else
        {
            Debug.LogError("TextMeshProUGUI component not found on choice button");
        }
    }

    private void SetupChoiceButtonListener(GameObject choiceButton, int choiceIndex)
    {
        Button buttonComponent = choiceButton.GetComponent<Button>();
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(() => {
                MakeChoice(choiceIndex);
            });
            Debug.Log("Button click listener added");
        }
        else
        {
            Debug.LogError("Button component not found on choice button");
        }
    }

    private void MakeChoice(int choiceIndex)
    {
        currentStory.ChooseChoiceIndex(choiceIndex);
        ContinueStory();
    }

    private void ApplyVariableChanges()
    {
        // Get variables from Ink and apply to GameManager
        if (currentStory == null || gameManager == null)
        {
            Debug.LogWarning("Story or GameManager is null, skipping variable sync");
            return;
        }

        try
        {
            // Try to get player_karma
            object karmaValue = null;
            try 
            {
                karmaValue = currentStory.variablesState["player_karma"];
            }
            catch 
            {
                // Variable doesn't exist
            }
            
            if (karmaValue != null)
            {
                gameManager.playerKarma = Convert.ToInt32(karmaValue);
                Debug.Log($"Updated GameManager - Karma: {gameManager.playerKarma}");
            }
            else
            {
                Debug.Log("player_karma variable not found in Ink story");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error applying variable changes: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    public bool IsDialoguePlaying()
    {
        return dialogueIsPlaying;
    }
}
