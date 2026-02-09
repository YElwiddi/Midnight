using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interactable dirt pile that can be dug up with a shovel.
/// Incorrect digs apply penalties to the killer (faster speed, direct pursuit).
/// </summary>
public class DirtPileInteractable : MonoBehaviour, IInteractable
{
    public enum DirtPileType
    {
        Correct,    // The right grave to dig
        Incorrect   // Wrong grave - applies penalties
    }

    [Header("Dirt Pile Settings")]
    [Tooltip("Is this the correct grave or an incorrect one?")]
    public DirtPileType pileType = DirtPileType.Incorrect;

    [Tooltip("Interaction prompt shown to player")]
    public string interactionPrompt = "Dig";

    [Header("Dialogue Settings")]
    [Tooltip("Message when player doesn't have shovel")]
    public string noShovelMessage = "I need a shovel for this.";

    [Tooltip("Confirmation message before digging")]
    public string confirmationMessage = "Are you sure you want to exhume this site?";

    [Tooltip("Message shown after digging correct grave")]
    public string correctDigMessage = "I found something...";

    [Tooltip("Message shown after digging incorrect grave")]
    public string incorrectDigMessage = "Nothing here... just bones.";

    [Header("Audio")]
    public AudioClip digSound;
    [Range(0f, 1f)]
    public float digSoundVolume = 1f;

    [Header("Visual")]
    [Tooltip("Optional object to show after digging (e.g., open grave)")]
    public GameObject dugUpVisual;
    [Tooltip("Destroy this object after digging")]
    public bool destroyAfterDig = true;

    [Header("Penalty Config")]
    [Tooltip("Configuration for dig penalties. If not set, uses default hardcoded values.")]
    [SerializeField] private DigPenaltyConfig penaltyConfig;

    // Private state
    private bool hasBeenDug = false;
    private bool isShowingChoice = false;
    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private Coroutine activeChoiceCoroutine;
    private System.Action<int> activeChoiceHandler;

    void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();

        if (dugUpVisual != null)
        {
            dugUpVisual.SetActive(false);
        }
    }

    public string GetInteractionPrompt()
    {
        if (hasBeenDug) return "";
        return interactionPrompt;
    }

    public void Interact()
    {
        if (hasBeenDug || isShowingChoice) return;

        // Don't interact if another dialogue is already showing
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return;

        // Check if player has shovel
        if (GameManager.Instance == null || !GameManager.Instance.ShovelPickedUp)
        {
            // Show "need shovel" message
            SimpleDialogueTrigger.ShowDialogue(noShovelMessage, "", 2f, 30f);
            return;
        }

        // Check if shovel is broken
        if (GameManager.Instance.ShovelBroken)
        {
            string brokenMessage = penaltyConfig != null ? penaltyConfig.tryDigWithBrokenShovelDialogue : "The shovel is broken. I can't dig anymore.";
            SimpleDialogueTrigger.ShowDialogue(brokenMessage, "", 2f, 30f);
            return;
        }

        // Show confirmation choice
        activeChoiceCoroutine = StartCoroutine(ShowDigConfirmation());
    }

    private void OnDisable()
    {
        // Clean up if disabled while showing choice
        if (activeChoiceCoroutine != null)
        {
            StopCoroutine(activeChoiceCoroutine);
            activeChoiceCoroutine = null;
        }

        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }

        isShowingChoice = false;
    }

    private IEnumerator ShowDigConfirmation()
    {
        if (dialogueUI == null)
        {
            Debug.LogWarning("DirtPileInteractable: No DialogueUI found!");
            yield break;
        }

        isShowingChoice = true;

        // Disable player input
        if (playerMovement != null)
        {
            playerMovement.DisableAllInput();
        }
        if (crosshairManager != null)
        {
            crosshairManager.Hide();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Show dialogue with choices
        dialogueUI.Show();
        dialogueUI.SetDialogueText(confirmationMessage, null);

        // Wait a frame for UI to update
        yield return null;

        // Display Yes/No choices
        List<string> choices = new List<string> { "Yes", "No" };
        dialogueUI.DisplayChoices(choices);

        // Subscribe to choice selection
        bool choiceMade = false;
        int selectedChoice = -1;

        activeChoiceHandler = (index) =>
        {
            selectedChoice = index;
            choiceMade = true;
            if (dialogueUI != null && activeChoiceHandler != null)
            {
                dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            }
            activeChoiceHandler = null;
        };

        dialogueUI.OnChoiceSelected += activeChoiceHandler;

        // Wait for choice (or until dialogue is closed externally by jumpscare)
        while (!choiceMade && dialogueUI != null && dialogueUI.IsVisible)
        {
            yield return null;
        }

        // Clean up handler if still subscribed (e.g., dialogue closed by jumpscare)
        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }

        // Only proceed with cleanup if dialogue wasn't closed by jumpscare
        if (dialogueUI != null && dialogueUI.IsVisible)
        {
            // Hide dialogue
            dialogueUI.ClearChoices();
            dialogueUI.Hide();

            // Re-enable player input
            if (playerMovement != null)
            {
                playerMovement.EnableAllInput();
            }
            if (crosshairManager != null)
            {
                crosshairManager.Show();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        isShowingChoice = false;
        activeChoiceCoroutine = null;

        // Handle choice (only if player actually made a choice)
        if (choiceMade && selectedChoice == 0) // Yes
        {
            PerformDig();
        }
        // If No (1) or interrupted, do nothing - player can try again (if still alive)
    }

    private void PerformDig()
    {
        hasBeenDug = true;

        // Play dig sound
        if (digSound != null)
        {
            AudioSource.PlayClipAtPoint(digSound, transform.position, digSoundVolume);
        }

        if (pileType == DirtPileType.Correct)
        {
            HandleCorrectDig();
        }
        else
        {
            HandleIncorrectDig();
        }

        // Show dug up visual
        if (dugUpVisual != null)
        {
            dugUpVisual.SetActive(true);
        }

        // Destroy or disable this object
        if (destroyAfterDig)
        {
            Destroy(gameObject, 0.5f);
        }
        else
        {
            // Just disable the collider so it can't be interacted with again
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    private void HandleCorrectDig()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.CorrectDigCount++;
        int correctCount = GameManager.Instance.CorrectDigCount;

        Debug.Log($"DirtPileInteractable: Correct grave #{correctCount} found!");

        // Show success message
        SimpleDialogueTrigger.ShowDialogue(correctDigMessage, "", 3f, 30f);

        // Notify any listeners (like GraveyardEndingManager)
        OnCorrectGraveExhumed?.Invoke(correctCount);
    }

    // Static event for when a correct grave is exhumed
    public static event System.Action<int> OnCorrectGraveExhumed;

    private void HandleIncorrectDig()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.IncorrectDigCount++;
        int incorrectCount = GameManager.Instance.IncorrectDigCount;

        Debug.Log($"DirtPileInteractable: Incorrect dig #{incorrectCount}");

        // Show failure message
        SimpleDialogueTrigger.ShowDialogue(incorrectDigMessage, "", 2f, 30f);

        // Apply penalties to all active killers
        ApplyKillerPenalties(incorrectCount);
    }

    private void ApplyKillerPenalties(int incorrectCount)
    {
        // Get config values or use defaults
        float firstMult = penaltyConfig != null ? penaltyConfig.firstPenaltyMultiplier : 1.2f;
        float secondMult = penaltyConfig != null ? penaltyConfig.secondPenaltyMultiplier : 1.3f;
        float thirdMult = penaltyConfig != null ? penaltyConfig.thirdPenaltyMultiplier : 1.5f;
        int pursuitThreshold = penaltyConfig != null ? penaltyConfig.directPursuitThreshold : 3;

        // Debug: show if config is being used
        if (penaltyConfig != null)
        {
            Debug.Log($"DirtPileInteractable: Using DigPenaltyConfig - multipliers: {firstMult}, {secondMult}, {thirdMult}");
        }
        else
        {
            Debug.LogWarning("DirtPileInteractable: No DigPenaltyConfig assigned! Using hardcoded defaults (1.2, 1.3, 1.5)");
        }

        // Find all CryptKillers in the scene
        CryptKiller[] killers = FindObjectsOfType<CryptKiller>();

        foreach (CryptKiller killer in killers)
        {
            switch (incorrectCount)
            {
                case 1:
                    killer.ApplySpeedMultiplier(firstMult);
                    Debug.Log($"DirtPileInteractable: Killer is now {(firstMult - 1f) * 100f:F0}% faster!");
                    break;

                case 2:
                    // Apply incremental multiplier to reach target from previous
                    killer.ApplySpeedMultiplier(secondMult / firstMult);
                    Debug.Log($"DirtPileInteractable: Killer is now {(secondMult - 1f) * 100f:F0}% faster!");
                    break;

                case 3:
                    // Apply incremental multiplier to reach target from previous
                    killer.ApplySpeedMultiplier(thirdMult / secondMult);
                    if (incorrectCount >= pursuitThreshold)
                    {
                        killer.EnableDirectPursuit();
                    }
                    Debug.Log($"DirtPileInteractable: Killer is now {(thirdMult - 1f) * 100f:F0}% faster and coming directly for you!");
                    break;

                default:
                    // More than 3 incorrect - keep at max penalty
                    if (incorrectCount > 3)
                    {
                        // Already at max, but ensure direct pursuit is on
                        if (incorrectCount >= pursuitThreshold && !killer.IsDirectPursuitEnabled())
                        {
                            killer.EnableDirectPursuit();
                        }
                    }
                    break;
            }
        }

        // Also check for KillerNPC types
        KillerNPC[] killerNPCs = FindObjectsOfType<KillerNPC>();
        // KillerNPC doesn't have the same speed modifier system, so we'd need to add it
        // For now, just log a warning if there are KillerNPCs
        if (killerNPCs.Length > 0)
        {
            Debug.LogWarning("DirtPileInteractable: KillerNPC speed modification not implemented");
        }

        // Handle shovel breaking at max penalty threshold
        if (penaltyConfig != null && penaltyConfig.breakShovelOnMaxPenalty && incorrectCount >= pursuitThreshold)
        {
            BreakShovel();
        }
    }

    private void BreakShovel()
    {
        if (GameManager.Instance == null || GameManager.Instance.ShovelBroken) return;

        GameManager.Instance.ShovelBroken = true;
        string dialogue = penaltyConfig != null ? penaltyConfig.shovelBrokenDialogue : "My shovel broke...";

        // Use a coroutine to show the broken shovel dialogue after a short delay
        // so it doesn't overlap with the incorrect dig message
        StartCoroutine(ShowBrokenShovelDialogue(dialogue));

        Debug.Log("DirtPileInteractable: Shovel has been broken!");
    }

    private IEnumerator ShowBrokenShovelDialogue(string dialogue)
    {
        // Wait for the incorrect dig message to finish
        yield return new WaitForSeconds(2.5f);
        SimpleDialogueTrigger.ShowDialogue(dialogue, "", 3f, 30f);
    }

    // IInteractable hover methods (optional)
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
}
