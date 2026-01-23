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
    private CrosshairManager crosshairManager;
    private Transform currentNPC;
    private float customCameraHeight = -1f; // -1 means use default height
    private float customCameraZoom = -1f; // -1 means use default zoom (FOV)
    private static DialogueManager instance;

    // Sound override settings
    private AudioClip customSoundClip;
    private float customSoundVolume = -1f;
    private float customSoundBasePitch = -1f;
    private float customSoundPitchVariation = -1f;
    private int customSoundEveryN = -1;
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
        crosshairManager = FindObjectOfType<CrosshairManager>();

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
    /// Sets sound override for the next dialogue session. Call before EnterDialogueMode.
    /// </summary>
    public void SetDialogueSoundOverride(AudioClip clip, float volume = -1f, float basePitch = -1f, float pitchVariation = -1f, int soundEveryN = -1)
    {
        customSoundClip = clip;
        customSoundVolume = volume;
        customSoundBasePitch = basePitch;
        customSoundPitchVariation = pitchVariation;
        customSoundEveryN = soundEveryN;
    }
    /// <summary>
    /// Starts a dialogue session with the specified Ink story.
    /// </summary>
    /// <param name="inkJSON">The compiled Ink JSON asset</param>
    /// <param name="npcTransform">Optional transform of the NPC being talked to (for camera focus)</param>
    /// <param name="cameraHeight">Optional camera look height (-1 to use default)</param>
    /// <param name="typewriterSpeed">Optional typewriter speed override (0 = use default)</param>
    /// <param name="cameraZoom">Optional camera zoom/FOV (-1 to use default)</param>
    public void EnterDialogueMode(TextAsset inkJSON, Transform npcTransform = null, float cameraHeight = -1f, float typewriterSpeed = 0f, float cameraZoom = -1f)
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
        customCameraHeight = cameraHeight;
        customCameraZoom = cameraZoom;
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;

        // Set typewriter speed override
        dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);

        // Set sound override if configured
        if (customSoundClip != null)
        {
            dialogueUI.SetTypewriterSoundOverride(customSoundClip, customSoundVolume, customSoundBasePitch, customSoundPitchVariation, customSoundEveryN);
        }

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
    /// <param name="cameraHeight">Optional camera look height (-1 to use default)</param>
    /// <param name="typewriterSpeed">Optional typewriter speed override (0 = use default)</param>
    /// <param name="cameraZoom">Optional camera zoom/FOV (-1 to use default)</param>
    public void EnterDialogueMode(TextAsset inkJSON, string knotName, Transform npcTransform = null, float cameraHeight = -1f, float typewriterSpeed = 0f, float cameraZoom = -1f)
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
        customCameraHeight = cameraHeight;
        customCameraZoom = cameraZoom;
        currentStory = new Story(inkJSON.text);
        dialogueIsPlaying = true;

        // Set typewriter speed override
        dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);

        // Set sound override if configured
        if (customSoundClip != null)
        {
            dialogueUI.SetTypewriterSoundOverride(customSoundClip, customSoundVolume, customSoundBasePitch, customSoundPitchVariation, customSoundEveryN);
        }

        // Show UI
        dialogueUI.Show();

        // Setup player input
        SetupPlayerInput(false);

        // Bind Ink external functions
        BindInkFunctions();

        // Initialize Ink variables
        SyncVariablesToInk();

        // Jump to specified knot
        if (!string.IsNullOrEmpty(knotName))
        {
            if (currentStory.KnotContainerWithName(knotName) != null)
            {
                currentStory.ChoosePathString(knotName);
            }
            else
            {
                Debug.LogWarning($"DialogueManager: Knot '{knotName}' not found in ink file! Starting from beginning.");
            }
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

        // Clear typewriter speed and sound overrides, then hide UI
        if (dialogueUI != null)
        {
            dialogueUI.ClearTypewriterSpeedOverride();
            dialogueUI.ClearTypewriterSoundOverride();
            dialogueUI.Hide();
        }

        // Restore player input
        SetupPlayerInput(true);

        currentNPC = null;
        currentStory = null;
        customCameraHeight = -1f;
        customCameraZoom = -1f;

        // Clear sound overrides
        customSoundClip = null;
        customSoundVolume = -1f;
        customSoundBasePitch = -1f;
        customSoundPitchVariation = -1f;
        customSoundEveryN = -1;

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
                Debug.Log("DialogueManager: Story cannot continue and has no choices - exiting dialogue");
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

            if (crosshairManager != null)
            {
                crosshairManager.Show();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Disable player input
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (crosshairManager != null)
            {
                crosshairManager.Hide();
            }

            if (movementScript != null)
            {
                movementScript.DisableAllInput();

                if (currentNPC != null)
                {
                    if (customCameraHeight >= 0 || customCameraZoom >= 0)
                    {
                        float height = customCameraHeight >= 0 ? customCameraHeight : 1.6f;
                        movementScript.SetCameraTarget(currentNPC, height, customCameraZoom);
                    }
                    else
                    {
                        movementScript.SetCameraTarget(currentNPC);
                    }
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

        // Bind SetEventVar function for event branching
        // Usage in Ink: ~ SetEventVar("allowed_inside", true)
        currentStory.BindExternalFunction("SetEventVar", (string varName, bool value) =>
        {
            EventVariables.SetVariable(varName, value);
        });

        // Overload for string values
        // Usage in Ink: ~ SetEventVarString("destination", "inside")
        currentStory.BindExternalFunction("SetEventVarString", (string varName, string value) =>
        {
            EventVariables.SetVariable(varName, value);
        });

        // Overload for int values
        // Usage in Ink: ~ SetEventVarInt("choice_index", 2)
        currentStory.BindExternalFunction("SetEventVarInt", (string varName, int value) =>
        {
            EventVariables.SetVariable(varName, value);
        });
    }

    private void SyncVariablesToInk()
    {
        if (currentStory == null || gameManager == null) return;

        try
        {
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_scared"))
                currentStory.variablesState["player_scared"] = gameManager.player_scared;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_mean"))
                currentStory.variablesState["player_mean"] = gameManager.player_mean;
            if (currentStory.variablesState.GlobalVariableExistsWithName("player_stupid"))
                currentStory.variablesState["player_stupid"] = gameManager.player_stupid;
            if (currentStory.variablesState.GlobalVariableExistsWithName("SpiritAngered"))
                currentStory.variablesState["SpiritAngered"] = gameManager.SpiritAngered;
            if (currentStory.variablesState.GlobalVariableExistsWithName("GraveRobberSetup"))
                currentStory.variablesState["GraveRobberSetup"] = gameManager.GraveRobberSetup;
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
            SyncSingleVariableFromInk("player_scared", v => gameManager.player_scared = v);
            SyncSingleVariableFromInk("player_mean", v => gameManager.player_mean = v);
            SyncSingleVariableFromInk("player_stupid", v => gameManager.player_stupid = v);
            SyncSingleVariableFromInk("SpiritAngered", v => gameManager.SpiritAngered = v);
            SyncSingleVariableFromInk("GraveRobberSetup", v => gameManager.GraveRobberSetup = v);
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
