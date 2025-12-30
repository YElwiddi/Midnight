using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

/// <summary>
/// Manages dialogue logic and Ink story integration.
/// Works with DialogueUI component for visual presentation.
/// This component handles the narrative logic while DialogueUI handles display.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("UI Reference")]
    [Tooltip("Reference to the DialogueUI component that handles visual presentation")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Default Ink JSON (Optional)")]
    [Tooltip("Default Ink story to use if none is provided when starting dialogue")]
    [SerializeField] private TextAsset defaultInkJSONAsset;

    [Header("Input Settings")]
    [Tooltip("Key to advance dialogue when no choices are available")]
    [SerializeField] private KeyCode continueKey = KeyCode.Mouse0;

    [Tooltip("Key to close dialogue")]
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    [Tooltip("Allow clicking anywhere to continue dialogue")]
    [SerializeField] private bool clickToContinue = true;
    #endregion

    #region Private Fields
    private Story currentStory;
    private bool dialogueIsPlaying;
    private GameManager gameManager;
    private Movement movementScript;
    private Transform currentNPC;
    private static DialogueManager instance;
    #endregion

    #region Events
    /// <summary>Invoked when dialogue starts</summary>
    public event Action OnDialogueStarted;

    /// <summary>Invoked when dialogue ends</summary>
    public event Action OnDialogueEnded;

    /// <summary>Invoked when dialogue text changes. Parameter is the new text.</summary>
    public event Action<string> OnDialogueTextChanged;

    /// <summary>Invoked when choices are presented. Parameter is the list of choice texts.</summary>
    public event Action<List<string>> OnChoicesPresented;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("Found more than one Dialogue Manager in the scene");
        }
        instance = this;

        gameManager = FindObjectOfType<GameManager>();
        movementScript = FindObjectOfType<Movement>();

        // Auto-find DialogueUI if not assigned
        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<DialogueUI>();
            if (dialogueUI == null)
            {
                Debug.LogWarning("DialogueManager: No DialogueUI found. Please assign one in the Inspector.");
            }
        }
    }

    private void Start()
    {
        dialogueIsPlaying = false;

        // Subscribe to DialogueUI events
        if (dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected += HandleChoiceSelected;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= HandleChoiceSelected;
        }
    }

    private void Update()
    {
        if (!dialogueIsPlaying) return;

        // Handle typewriter skip
        if (dialogueUI != null && dialogueUI.IsTypewriting)
        {
            if (Input.GetKeyDown(continueKey) || (clickToContinue && Input.GetMouseButtonDown(0)))
            {
                dialogueUI.SkipTypewriter();
                return;
            }
        }

        // Handle continue when no choices
        if (currentStory != null && currentStory.currentChoices.Count == 0 && !dialogueUI.HasActiveChoices)
        {
            bool shouldContinue = Input.GetKeyDown(continueKey) ||
                                  (clickToContinue && Input.GetMouseButtonDown(0));

            if (shouldContinue && !dialogueUI.IsTypewriting)
            {
                if (currentStory.canContinue)
                {
                    ContinueStory();
                }
                else
                {
                    ExitDialogueMode();
                }
            }
        }

        // Handle exit key
        if (Input.GetKeyDown(exitKey))
        {
            ExitDialogueMode();
        }
    }
    #endregion

    #region Public Static Methods
    public static DialogueManager GetInstance()
    {
        return instance;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Starts a dialogue session with the specified Ink story.
    /// </summary>
    /// <param name="inkJSON">The compiled Ink JSON asset</param>
    /// <param name="npcTransform">Optional transform of the NPC being talked to (for camera focus)</param>
    public void EnterDialogueMode(TextAsset inkJSON, Transform npcTransform = null)
    {
        if (inkJSON == null)
        {
            Debug.LogError("DialogueManager: Cannot start dialogue - inkJSON is null");
            return;
        }

        if (dialogueUI == null)
        {
            Debug.LogError("DialogueManager: Cannot start dialogue - DialogueUI is not assigned");
            return;
        }

        currentNPC = npcTransform;
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;

        // Show UI
        dialogueUI.Show();

        // Setup player input
        SetupPlayerInput(false);

        // Bind Ink external functions
        BindInkFunctions();

        // Initialize Ink variables from GameManager
        SyncVariablesToInk();

        // Jump to start knot if it exists
        if (currentStory.canContinue && currentStory.KnotContainerWithName("start") != null)
        {
            currentStory.ChoosePathString("start");
        }

        OnDialogueStarted?.Invoke();

        // Begin dialogue
        ContinueStory();
    }

    /// <summary>
    /// Starts dialogue at a specific knot/stitch in the Ink story.
    /// </summary>
    /// <param name="inkJSON">The compiled Ink JSON asset</param>
    /// <param name="knotName">Name of the knot to start from</param>
    /// <param name="npcTransform">Optional NPC transform</param>
    public void EnterDialogueMode(TextAsset inkJSON, string knotName, Transform npcTransform = null)
    {
        if (inkJSON == null)
        {
            Debug.LogError("DialogueManager: Cannot start dialogue - inkJSON is null");
            return;
        }

        if (dialogueUI == null)
        {
            Debug.LogError("DialogueManager: Cannot start dialogue - DialogueUI is not assigned");
            return;
        }

        currentNPC = npcTransform;
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;

        // Show UI
        dialogueUI.Show();

        // Setup player input
        SetupPlayerInput(false);

        // Bind Ink external functions
        BindInkFunctions();

        // Initialize Ink variables
        SyncVariablesToInk();

        // Jump to specified knot
        if (!string.IsNullOrEmpty(knotName) && currentStory.KnotContainerWithName(knotName) != null)
        {
            currentStory.ChoosePathString(knotName);
        }

        OnDialogueStarted?.Invoke();

        // Begin dialogue
        ContinueStory();
    }

    /// <summary>
    /// Exits the current dialogue session.
    /// </summary>
    public void ExitDialogueMode()
    {
        if (!dialogueIsPlaying) return;

        dialogueIsPlaying = false;

        // Sync variables back to GameManager
        SyncVariablesFromInk();

        // Hide UI
        if (dialogueUI != null)
        {
            dialogueUI.Hide();
        }

        // Restore player input
        SetupPlayerInput(true);

        currentNPC = null;
        currentStory = null;

        OnDialogueEnded?.Invoke();
    }

    /// <summary>
    /// Returns true if dialogue is currently playing.
    /// </summary>
    public bool IsDialoguePlaying()
    {
        return dialogueIsPlaying;
    }

    /// <summary>
    /// Gets a variable value from the current Ink story.
    /// </summary>
    public T GetInkVariable<T>(string variableName)
    {
        if (currentStory == null) return default;

        try
        {
            return (T)currentStory.variablesState[variableName];
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Sets a variable value in the current Ink story.
    /// </summary>
    public void SetInkVariable(string variableName, object value)
    {
        if (currentStory == null) return;

        try
        {
            if (currentStory.variablesState.GlobalVariableExistsWithName(variableName))
            {
                currentStory.variablesState[variableName] = value;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DialogueManager: Could not set variable '{variableName}': {e.Message}");
        }
    }
    #endregion

    #region Private Methods
    private void ContinueStory()
    {
        if (currentStory == null || !currentStory.canContinue)
        {
            if (currentStory != null && currentStory.currentChoices.Count == 0)
            {
                ExitDialogueMode();
            }
            return;
        }

        string text = currentStory.Continue();

        // Skip empty text nodes
        if (string.IsNullOrWhiteSpace(text) && currentStory.currentChoices.Count == 0)
        {
            if (currentStory.canContinue)
            {
                ContinueStory();
                return;
            }
            else
            {
                ExitDialogueMode();
                return;
            }
        }

        // Extract speaker name from tags if present
        string speakerName = null;
        foreach (string tag in currentStory.currentTags)
        {
            if (tag.StartsWith("speaker:"))
            {
                speakerName = tag.Substring(8).Trim();
                break;
            }
        }

        // Update UI
        if (dialogueUI != null)
        {
            dialogueUI.SetDialogueText(text.Trim(), speakerName);
        }

        OnDialogueTextChanged?.Invoke(text);

        // Display choices if any
        DisplayChoices();
    }

    private void DisplayChoices()
    {
        List<Choice> inkChoices = currentStory.currentChoices;

        if (inkChoices.Count == 0)
        {
            if (dialogueUI != null)
            {
                dialogueUI.ClearChoices();
            }
            return;
        }

        List<string> choiceTexts = new List<string>();
        foreach (Choice choice in inkChoices)
        {
            choiceTexts.Add(choice.text);
        }

        if (dialogueUI != null)
        {
            dialogueUI.DisplayChoices(choiceTexts);
        }

        OnChoicesPresented?.Invoke(choiceTexts);
    }

    private void HandleChoiceSelected(int choiceIndex)
    {
        if (currentStory == null) return;

        if (choiceIndex >= 0 && choiceIndex < currentStory.currentChoices.Count)
        {
            currentStory.ChooseChoiceIndex(choiceIndex);
            ContinueStory();
        }
    }

    private void SetupPlayerInput(bool enable)
    {
        if (enable)
        {
            // Enable player input
            if (movementScript != null)
            {
                movementScript.ClearCameraTarget();
                movementScript.EnableAllInput();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Disable player input
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (movementScript != null)
            {
                movementScript.DisableAllInput();

                if (currentNPC != null)
                {
                    movementScript.SetCameraTarget(currentNPC);
                }
            }
        }
    }

    private void BindInkFunctions()
    {
        if (currentStory == null) return;

        // Bind ChangeGameVariable function
        currentStory.BindExternalFunction("ChangeGameVariable", (string varName, int value) =>
        {
            if (gameManager != null)
            {
                gameManager.ChangeVariable(varName, value);
            }
        });
    }

    private void SyncVariablesToInk()
    {
        if (currentStory == null || gameManager == null) return;

        try
        {
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_friendly"))
                currentStory.variablesState["player_friendly"] = gameManager.player_friendly;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_scared"))
                currentStory.variablesState["player_scared"] = gameManager.player_scared;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_brave"))
                currentStory.variablesState["player_brave"] = gameManager.player_brave;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_mean"))
                currentStory.variablesState["player_mean"] = gameManager.player_mean;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_smart"))
                currentStory.variablesState["player_smart"] = gameManager.player_smart;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_stupid"))
                currentStory.variablesState["player_stupid"] = gameManager.player_stupid;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DialogueManager: Could not sync variables to Ink: {e.Message}");
        }
    }

    private void SyncVariablesFromInk()
    {
        if (currentStory == null || gameManager == null) return;

        try
        {
            SyncSingleVariableFromInk("player_friendly", v => gameManager.player_friendly = v);
            SyncSingleVariableFromInk("player_scared", v => gameManager.player_scared = v);
            SyncSingleVariableFromInk("player_brave", v => gameManager.player_brave = v);
            SyncSingleVariableFromInk("player_mean", v => gameManager.player_mean = v);
            SyncSingleVariableFromInk("player_smart", v => gameManager.player_smart = v);
            SyncSingleVariableFromInk("player_stupid", v => gameManager.player_stupid = v);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DialogueManager: Error syncing variables from Ink: {e.Message}");
        }
    }

    private void SyncSingleVariableFromInk(string varName, Action<int> setter)
    {
        try
        {
            if (currentStory.variablesState.GlobalVariableExistsWithName(varName))
            {
                object value = currentStory.variablesState[varName];
                if (value != null)
                {
                    setter(Convert.ToInt32(value));
                }
            }
        }
        catch { }
    }
    #endregion
}
