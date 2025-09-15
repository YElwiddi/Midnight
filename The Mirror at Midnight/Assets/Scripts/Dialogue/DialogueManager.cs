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

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSONAsset;

    private Story currentStory;
    private bool dialogueIsPlaying;

    // Reference to GameManager for variable changes
    private GameManager gameManager;

    // Reference to Movement script for controlling player input
    private Movement movementScript;

    private static DialogueManager instance;

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

    public void EnterDialogueMode(TextAsset inkJSON)
    {
        Debug.Log("=== EnterDialogueMode Called ===");
        Debug.Log($"inkJSON is null: {inkJSON == null}");
        Debug.Log($"dialoguePanel is null: {dialoguePanel == null}");
        Debug.Log($"dialogueText is null: {dialogueText == null}");
        Debug.Log($"choiceButtonPrefab is null: {choiceButtonPrefab == null}");
        Debug.Log($"choiceButtonContainer is null: {choiceButtonContainer == null}");
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
        }

        // Bind external functions if needed
        currentStory.BindExternalFunction("ChangeGameVariable", (string varName, int value) => {
            gameManager.ChangeVariable(varName, value);
        });

        // Make sure to start at the beginning
        if (currentStory.canContinue)
        {
            // Try to go to start knot if it exists
            if (currentStory.KnotContainerWithName("start") != null)
            {
                currentStory.ChoosePathString("start");
            }
        }
        // Position the choice container at the bottom of the dialogue panel
        if (choiceButtonContainer != null)
        {
            RectTransform containerRect = choiceButtonContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = new Vector2(0, 0);
                containerRect.anchorMax = new Vector2(1, 0);
                containerRect.anchoredPosition = new Vector2(0, 60); // Position from bottom
                containerRect.sizeDelta = new Vector2(-100, 100); // Leave margins
                Debug.Log($"Set choice container position: {containerRect.anchoredPosition}");
            }

            // Disable any layout components that might interfere with manual positioning
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

        // Start the dialogue
        ContinueStory();
    }

    private void ExitDialogueMode()
    {
        dialogueIsPlaying = false;
        dialoguePanel.SetActive(false);
        dialogueText.text = "";

        // Apply any variable changes from Ink to GameManager
        ApplyVariableChanges();

        // Lock cursor and re-enable player input
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (movementScript != null)
        {
            movementScript.EnableAllInput();
        }
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
            GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            if (choiceButton != null)
            {
                Debug.Log("Choice button instantiated successfully");

                // Position buttons horizontally
                RectTransform buttonRect = choiceButton.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    // Calculate horizontal position
                    float containerWidth = 800f; // Approximate container width (screen width - margins)
                    float spacing = containerWidth / (currentChoices.Count + 1);
                    float xPos = (i + 1) * spacing - containerWidth / 2;
                    buttonRect.anchoredPosition = new Vector2(xPos, 0);
                    Debug.Log($"Set button position: {buttonRect.anchoredPosition}");
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

                TextMeshProUGUI textComponent = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = choice.text;
                    textComponent.fontSize = textComponent.fontSize / 2f; // Make font size half
                    Debug.Log($"Set button text to: {choice.text}");
                    Debug.Log($"Set button font size to: {textComponent.fontSize}");
                    Debug.Log($"Text component active: {textComponent.gameObject.activeSelf}");
                }
                else
                {
                    Debug.LogError("TextMeshProUGUI component not found on choice button");
                }

                Button buttonComponent = choiceButton.GetComponent<Button>();
                if (buttonComponent != null)
                {
                    // Capture the index for the button click
                    int choiceIndex = choice.index;
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

    private void MakeChoice(int choiceIndex)
    {
        currentStory.ChooseChoiceIndex(choiceIndex);
        ContinueStory();
    }

    private void ApplyVariableChanges()
    {
        // Get variables from Ink and apply to GameManager
        try
        {
            int karma = (int)currentStory.variablesState["player_karma"];
            bool questAccepted = (bool)currentStory.variablesState["quest_accepted"];

            // Update GameManager variables
            if (gameManager != null)
            {
                gameManager.playerKarma = karma;
                gameManager.questAccepted = questAccepted;

                Debug.Log($"Updated GameManager - Karma: {karma}, Quest Accepted: {questAccepted}");
            }
        }
        catch (Exception e)
        {
            Debug.Log("No attributes found");
        }
        }

    public bool IsDialoguePlaying()
    {
        return dialogueIsPlaying;
    }
}
