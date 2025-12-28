using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the dialogue UI using prefab-based GameObjects.
/// Attach this component to your dialogue canvas and assign UI elements in the Inspector.
/// The UI layout is entirely controlled by the prefab structure - no hard-coded positioning.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    #region UI References
    [Header("UI Panel References")]
    [Tooltip("The root dialogue panel that contains all dialogue UI. Will be shown/hidden.")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("Optional background image for the dialogue panel")]
    [SerializeField] private Image dialoguePanelBackground;

    [Tooltip("Optional continue indicator (shown when player can click to continue)")]
    [SerializeField] private GameObject continueIndicator;

    [Header("Dialogue Text")]
    [Tooltip("TextMeshPro component for displaying dialogue text")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("Optional speaker name text (leave empty if not using speaker names)")]
    [SerializeField] private TextMeshProUGUI speakerNameText;

    [Tooltip("Optional speaker name container (shown/hidden based on whether speaker name exists)")]
    [SerializeField] private GameObject speakerNameContainer;

    [Header("Choice System")]
    [Tooltip("Container that holds choice buttons. Should have a LayoutGroup component.")]
    [SerializeField] private Transform choiceContainer;

    [Tooltip("Prefab for choice buttons. Must have Button and TextMeshProUGUI components.")]
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("Optional Settings")]
    [Tooltip("Optional DialogueUISettings asset for styling. If not assigned, uses prefab defaults.")]
    [SerializeField] private DialogueUISettings uiSettings;
    #endregion

    #region Events
    /// <summary>Invoked when a choice button is clicked. Parameter is the choice index.</summary>
    public event Action<int> OnChoiceSelected;

    /// <summary>Invoked when the dialogue panel is shown.</summary>
    public event Action OnDialogueShown;

    /// <summary>Invoked when the dialogue panel is hidden.</summary>
    public event Action OnDialogueHidden;
    #endregion

    #region Private Fields
    private List<GameObject> activeChoiceButtons = new List<GameObject>();
    private CanvasGroup panelCanvasGroup;
    private Coroutine typewriterCoroutine;
    private Coroutine fadeCoroutine;
    private bool isTypewriting;
    private string fullDialogueText;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        ValidateReferences();
        InitializeComponents();
    }

    private void Start()
    {
        // Ensure panel starts hidden
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Shows the dialogue panel with optional fade animation.
    /// </summary>
    public void Show()
    {
        if (dialoguePanel == null) return;

        dialoguePanel.SetActive(true);

        if (uiSettings != null && uiSettings.panelFadeInDuration > 0 && panelCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadePanel(0f, 1f, uiSettings.panelFadeInDuration));
        }
        else if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
        }

        OnDialogueShown?.Invoke();
    }

    /// <summary>
    /// Hides the dialogue panel with optional fade animation.
    /// </summary>
    public void Hide()
    {
        if (dialoguePanel == null) return;

        ClearChoices();
        ClearDialogueText();

        if (uiSettings != null && uiSettings.panelFadeOutDuration > 0 && panelCanvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadePanel(1f, 0f, uiSettings.panelFadeOutDuration, () =>
            {
                dialoguePanel.SetActive(false);
                OnDialogueHidden?.Invoke();
            }));
        }
        else
        {
            dialoguePanel.SetActive(false);
            OnDialogueHidden?.Invoke();
        }
    }

    /// <summary>
    /// Sets the dialogue text. Applies typewriter effect if enabled in settings.
    /// </summary>
    /// <param name="text">The dialogue text to display</param>
    /// <param name="speakerName">Optional speaker name (null to hide speaker name)</param>
    public void SetDialogueText(string text, string speakerName = null)
    {
        if (dialogueText == null) return;

        // Handle speaker name
        UpdateSpeakerName(speakerName);

        // Apply styling from settings if available
        ApplyDialogueTextStyle();

        // Show/hide continue indicator
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false); // Will be shown when text is complete
        }

        // Apply text with or without typewriter effect
        if (uiSettings != null && uiSettings.useTypewriterEffect)
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
        }
        else
        {
            dialogueText.text = text;
            ShowContinueIndicator();
        }

        fullDialogueText = text;
    }

    /// <summary>
    /// Skips the typewriter effect and shows full text immediately.
    /// </summary>
    public void SkipTypewriter()
    {
        if (isTypewriting && typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            isTypewriting = false;
            dialogueText.text = fullDialogueText;
            ShowContinueIndicator();
        }
    }

    /// <summary>
    /// Returns true if typewriter effect is currently playing.
    /// </summary>
    public bool IsTypewriting => isTypewriting;

    /// <summary>
    /// Displays choice buttons for the given choices.
    /// </summary>
    /// <param name="choices">List of choice texts to display</param>
    public void DisplayChoices(List<string> choices)
    {
        ClearChoices();

        if (choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("DialogueUI: Choice container or button prefab not assigned");
            return;
        }

        // Hide continue indicator when choices are shown
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        for (int i = 0; i < choices.Count; i++)
        {
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceContainer);
            activeChoiceButtons.Add(buttonObj);

            // Setup button text
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = choices[i];
                ApplyChoiceButtonTextStyle(buttonText);
            }

            // Setup button colors
            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                ApplyChoiceButtonColors(button);

                // Setup click handler
                int choiceIndex = i; // Capture for closure
                button.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));
            }

            buttonObj.SetActive(true);
        }

        // Force layout rebuild
        if (choiceContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainer.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// Clears all choice buttons from the container.
    /// </summary>
    public void ClearChoices()
    {
        foreach (GameObject button in activeChoiceButtons)
        {
            if (button != null)
            {
                Destroy(button);
            }
        }
        activeChoiceButtons.Clear();
    }

    /// <summary>
    /// Returns true if the dialogue panel is currently visible.
    /// </summary>
    public bool IsVisible => dialoguePanel != null && dialoguePanel.activeSelf;

    /// <summary>
    /// Returns true if choices are currently being displayed.
    /// </summary>
    public bool HasActiveChoices => activeChoiceButtons.Count > 0;

    /// <summary>
    /// Applies settings from a DialogueUISettings asset at runtime.
    /// </summary>
    /// <param name="settings">The settings to apply</param>
    public void ApplySettings(DialogueUISettings settings)
    {
        uiSettings = settings;
        ApplyDialogueTextStyle();
    }
    #endregion

    #region Private Methods
    private void ValidateReferences()
    {
        if (dialoguePanel == null)
            Debug.LogWarning("DialogueUI: Dialogue Panel not assigned");
        if (dialogueText == null)
            Debug.LogWarning("DialogueUI: Dialogue Text not assigned");
        if (choiceContainer == null)
            Debug.LogWarning("DialogueUI: Choice Container not assigned");
        if (choiceButtonPrefab == null)
            Debug.LogWarning("DialogueUI: Choice Button Prefab not assigned");
    }

    private void InitializeComponents()
    {
        // Get or add CanvasGroup for fade effects
        if (dialoguePanel != null)
        {
            panelCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null && uiSettings != null &&
                (uiSettings.panelFadeInDuration > 0 || uiSettings.panelFadeOutDuration > 0))
            {
                panelCanvasGroup = dialoguePanel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void UpdateSpeakerName(string speakerName)
    {
        bool showSpeaker = uiSettings != null && uiSettings.showSpeakerName && !string.IsNullOrEmpty(speakerName);

        if (speakerNameContainer != null)
        {
            speakerNameContainer.SetActive(showSpeaker);
        }

        if (speakerNameText != null && showSpeaker)
        {
            speakerNameText.text = speakerName;

            if (uiSettings != null)
            {
                if (uiSettings.speakerNameFont != null)
                    speakerNameText.font = uiSettings.speakerNameFont;
                speakerNameText.fontSize = uiSettings.speakerNameFontSize;
                speakerNameText.color = uiSettings.speakerNameColor;
            }
        }
    }

    private void ApplyDialogueTextStyle()
    {
        if (dialogueText == null || uiSettings == null) return;

        if (uiSettings.dialogueFont != null)
            dialogueText.font = uiSettings.dialogueFont;

        dialogueText.fontSize = uiSettings.dialogueFontSize;
        dialogueText.color = uiSettings.dialogueTextColor;
        dialogueText.fontStyle = uiSettings.dialogueFontStyle;
    }

    private void ApplyChoiceButtonTextStyle(TextMeshProUGUI buttonText)
    {
        if (buttonText == null || uiSettings == null) return;

        if (uiSettings.choiceButtonFont != null)
            buttonText.font = uiSettings.choiceButtonFont;

        buttonText.fontSize = uiSettings.choiceButtonFontSize;
        buttonText.color = uiSettings.choiceButtonTextColor;
        buttonText.fontStyle = uiSettings.choiceButtonFontStyle;
    }

    private void ApplyChoiceButtonColors(Button button)
    {
        if (button == null || uiSettings == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = uiSettings.buttonNormalColor;
        colors.highlightedColor = uiSettings.buttonHighlightedColor;
        colors.pressedColor = uiSettings.buttonPressedColor;
        colors.disabledColor = uiSettings.buttonDisabledColor;
        button.colors = colors;
    }

    private void OnChoiceButtonClicked(int choiceIndex)
    {
        OnChoiceSelected?.Invoke(choiceIndex);
    }

    private void ClearDialogueText()
    {
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            isTypewriting = false;
        }
    }

    private void ShowContinueIndicator()
    {
        if (continueIndicator != null && activeChoiceButtons.Count == 0)
        {
            continueIndicator.SetActive(true);
        }
    }

    private IEnumerator TypewriterEffect(string text)
    {
        isTypewriting = true;
        dialogueText.text = "";

        float delay = 1f / uiSettings.typewriterSpeed;

        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(delay);
        }

        isTypewriting = false;
        ShowContinueIndicator();
    }

    private IEnumerator FadePanel(float startAlpha, float endAlpha, float duration, Action onComplete = null)
    {
        if (panelCanvasGroup == null) yield break;

        float elapsed = 0f;
        panelCanvasGroup.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        panelCanvasGroup.alpha = endAlpha;
        onComplete?.Invoke();
    }
    #endregion
}
