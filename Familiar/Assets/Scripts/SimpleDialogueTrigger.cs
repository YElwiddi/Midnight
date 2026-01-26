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
    private Coroutine displayCoroutine;

    // Single global lock - no new dialogue can start while this is true
    private static bool isDialogueLocked = false;
    private static MonoBehaviour coroutineRunner;

    /// <summary>
    /// Returns true if any dialogue is currently displaying.
    /// </summary>
    public static bool IsAnySimpleDialogueActive => isDialogueLocked;

    /// <summary>
    /// Cancels any currently active simple dialogue immediately.
    /// Call this before starting NPC dialogue to prevent conflicts.
    /// </summary>
    public static void CancelActiveSimpleDialogue()
    {
        isDialogueLocked = false;
        Debug.Log("SimpleDialogueTrigger: Dialogue lock released");
    }

    /// <summary>
    /// Shows a temporary dialogue from any script without needing a SimpleDialogueTrigger component.
    /// </summary>
    /// <param name="text">The dialogue text to display</param>
    /// <param name="speaker">Optional speaker name</param>
    /// <param name="duration">How long to display after typing completes</param>
    /// <param name="typewriterSpeed">Characters per second (0 = instant)</param>
    /// <returns>True if dialogue was started, false if another dialogue is active</returns>
    public static bool ShowDialogue(string text, string speaker = "", float duration = 2f, float typewriterSpeed = 30f)
    {
        if (isDialogueLocked) return false;
        if (string.IsNullOrEmpty(text)) return false;

        DialogueUI ui = FindFirstObjectByType<DialogueUI>();
        if (ui == null)
        {
            Debug.LogWarning("SimpleDialogueTrigger: No DialogueUI found for static dialogue");
            return false;
        }

        // Find a MonoBehaviour to run the coroutine on
        if (coroutineRunner == null)
        {
            coroutineRunner = ui;
        }

        // Lock immediately
        isDialogueLocked = true;

        coroutineRunner.StartCoroutine(ShowStaticDialogueCoroutine(ui, text, speaker, duration, typewriterSpeed));
        return true;
    }

    private static IEnumerator ShowStaticDialogueCoroutine(DialogueUI ui, string text, string speaker, float duration, float typewriterSpeed)
    {
        ui.Show();

        string speakerName = string.IsNullOrEmpty(speaker) ? null : speaker;

        if (typewriterSpeed > 0)
        {
            float delay = 1f / typewriterSpeed;
            for (int i = 1; i <= text.Length; i++)
            {
                ui.SetDialogueText(text.Substring(0, i), speakerName);
                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            ui.SetDialogueText(text, speakerName);
        }

        yield return new WaitForSeconds(duration);

        ui.Hide();
        isDialogueLocked = false;
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
        if (isDialogueLocked) return;
        if (dialogueUI == null)
        {
            Debug.LogWarning("SimpleDialogueTrigger: No DialogueUI found in scene");
            return;
        }

        hasTriggered = true;

        // Lock immediately
        isDialogueLocked = true;

        displayCoroutine = StartCoroutine(ShowDialogueForDuration());
    }

    private IEnumerator ShowDialogueForDuration()
    {
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

        dialogueUI.Hide();
        isDialogueLocked = false;
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
            if (dialogueUI != null)
            {
                dialogueUI.Hide();
            }
            isDialogueLocked = false;
        }
    }
}
