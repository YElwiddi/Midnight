using System.Collections;
using UnityEngine;

public class SimpleDialogueTrigger : MonoBehaviour, IInteractable
{
    public enum TriggerType
    {
        Interaction,    // Player must press interact button
        ZoneEnter,      // Trigger when player enters zone (requires trigger collider)
        Collision       // Trigger on any collision with player
    }

    [Header("Trigger Settings")]
    [SerializeField] private TriggerType triggerType = TriggerType.Interaction;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string interactionPrompt = "Examine";

    [Header("Dialogue Settings")]
    [TextArea(3, 10)]
    [SerializeField] private string dialogueText = "Enter your dialogue here...";
    [SerializeField] private string speakerName = "";
    [SerializeField] private float displayDuration = 3f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriterEffect = true;
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("References")]
    [Tooltip("Leave empty to auto-find DialogueUI in scene")]
    [SerializeField] private DialogueUI dialogueUI;

    private bool hasTriggered = false;
    private bool isDisplaying = false;
    private Coroutine displayCoroutine;

    // Static reference to currently active simple dialogue (for interruption)
    private static SimpleDialogueTrigger activeInstance;

    /// <summary>
    /// Returns true if any SimpleDialogueTrigger is currently displaying.
    /// </summary>
    public static bool IsAnySimpleDialogueActive => activeInstance != null && activeInstance.isDisplaying;

    /// <summary>
    /// Cancels any currently active simple dialogue immediately.
    /// Call this before starting NPC dialogue to prevent conflicts.
    /// </summary>
    public static void CancelActiveSimpleDialogue()
    {
        if (activeInstance != null && activeInstance.isDisplaying)
        {
            activeInstance.CancelDialogue();
        }
    }

    /// <summary>
    /// Cancels this dialogue immediately.
    /// </summary>
    public void CancelDialogue()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }
        isDisplaying = false;
        if (activeInstance == this)
        {
            activeInstance = null;
        }
        // Don't hide UI here - the NPC dialogue will take over
        Debug.Log("SimpleDialogueTrigger: Dialogue cancelled for NPC dialogue");
    }

    private void Start()
    {
        if (dialogueUI == null)
        {
            dialogueUI = FindFirstObjectByType<DialogueUI>();
        }
    }

    // IInteractable implementation for interaction trigger type
    public void Interact()
    {
        if (triggerType != TriggerType.Interaction) return;
        TryShowDialogue();
    }

    public string GetInteractionPrompt()
    {
        return triggerType == TriggerType.Interaction ? interactionPrompt : "";
    }

    // Zone/Collision triggers
    private void OnTriggerEnter(Collider other)
    {
        if (triggerType != TriggerType.ZoneEnter) return;
        if (!other.CompareTag("Player")) return;
        TryShowDialogue();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggerType != TriggerType.Collision) return;
        if (!collision.gameObject.CompareTag("Player")) return;
        TryShowDialogue();
    }

    private void TryShowDialogue()
    {
        if (triggerOnce && hasTriggered) return;
        if (isDisplaying) return;
        // Don't show if flashlight dialogue is active
        if (SimpleFlashlight.IsFlashlightDialogueActive) return;
        if (dialogueUI == null)
        {
            Debug.LogWarning("SimpleDialogueTrigger: No DialogueUI found in scene");
            return;
        }

        hasTriggered = true;
        displayCoroutine = StartCoroutine(ShowDialogueForDuration());
    }

    private IEnumerator ShowDialogueForDuration()
    {
        isDisplaying = true;
        activeInstance = this;

        // Show the dialogue
        dialogueUI.Show();

        string speaker = string.IsNullOrEmpty(speakerName) ? null : speakerName;

        if (useTypewriterEffect && typewriterSpeed > 0)
        {
            // Typewriter effect - show characters one by one
            float delay = 1f / typewriterSpeed;
            for (int i = 1; i <= dialogueText.Length; i++)
            {
                dialogueUI.SetDialogueText(dialogueText.Substring(0, i), speaker);
                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            // Show all text immediately
            dialogueUI.SetDialogueText(dialogueText, speaker);
        }

        // Wait for duration after text is fully displayed
        yield return new WaitForSeconds(displayDuration);

        // Only hide if we're still the active dialogue and no other dialogue has taken over
        if (activeInstance == this && !SimpleFlashlight.IsFlashlightDialogueActive)
        {
            dialogueUI.Hide();
        }

        if (activeInstance == this)
        {
            activeInstance = null;
        }

        isDisplaying = false;
    }

    // Public method to reset trigger (useful for re-triggerable dialogues)
    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    // Allow manual trigger from other scripts or events
    public void TriggerDialogue()
    {
        TryShowDialogue();
    }

    private void OnDisable()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            if (dialogueUI != null && isDisplaying)
            {
                dialogueUI.Hide();
            }
            isDisplaying = false;
        }
    }
}
