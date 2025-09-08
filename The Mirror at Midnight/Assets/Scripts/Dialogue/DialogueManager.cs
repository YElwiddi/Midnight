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

    private static DialogueManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("Found more than one Dialogue Manager in the scene");
        }
        instance = this;

        gameManager = FindObjectOfType<GameManager>();
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
        if (currentStory.currentChoices.Count == 0 && Input.GetKeyDown(KeyCode.Space))
        {
            ContinueStory();
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
            Debug.Log($"Canvas sort order: {canvas.sortingOrder}");
        }

        if (dialoguePanel != null)
        {
            Debug.Log($"Dialogue Panel name: {dialoguePanel.name}");
            Debug.Log($"Dialogue Panel active before: {dialoguePanel.activeSelf}");
        }
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;
        dialoguePanel.SetActive(true);

        // Bind external functions if needed
        currentStory.BindExternalFunction("ChangeGameVariable", (string varName, int value) => {
            gameManager.ChangeVariable(varName, value);
        });
        currentStory = new Story(inkJSON.text);

        // Make sure to start at the beginning
        if (currentStory.canContinue)
        {
            // Try to go to start knot if it exists
            if (currentStory.KnotContainerWithName("start") != null)
            {
                currentStory.ChoosePathString("start");
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

        // Optional: Resume game if it was paused
        // Time.timeScale = 1f;

        // Optional: Re-enable player movement
        // You can add code here to re-enable your player controller
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

        // Clear existing choice buttons
        foreach (Transform child in choiceButtonContainer)
        {
            Destroy(child.gameObject);
        }

        // Create button for each choice
        foreach (Choice choice in currentChoices)
        {
            GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceButtonContainer);
            choiceButton.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;

            // Capture the index for the button click
            int choiceIndex = choice.index;
            choiceButton.GetComponent<Button>().onClick.AddListener(() => {
                MakeChoice(choiceIndex);
            });
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

    public bool IsDialoguePlaying()
    {
        return dialogueIsPlaying;
    }
}