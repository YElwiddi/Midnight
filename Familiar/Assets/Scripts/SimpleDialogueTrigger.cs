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

        // Hide the dialogue
        dialogueUI.Hide();

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
