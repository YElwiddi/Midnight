using UnityEngine;

/// <summary>
/// Component for interactable readable objects (books, notes, tombstones, etc.)
/// Place this on the object you want the player to be able to read.
/// Requires a ReadableUI component in the scene.
/// </summary>
public class ReadableTrigger : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [Tooltip("Prompt shown when player can interact (e.g., 'Read', 'Examine')")]
    [SerializeField] private string interactionPrompt = "Read";

    [Header("Appearance")]
    [Tooltip("Background image displayed when reading (book texture, scroll, etc.)")]
    [SerializeField] private Sprite backgroundImage;

    [Header("Content")]
    [Tooltip("Pages of text content. Each element is one page.")]
    [TextArea(5, 15)]
    [SerializeField] private string[] pages;

    #region IInteractable Implementation
    public void Interact()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning($"ReadableTrigger on {gameObject.name}: No pages configured");
            return;
        }

        ReadableUI readableUI = ReadableUI.Instance;
        if (readableUI == null)
        {
            readableUI = FindFirstObjectByType<ReadableUI>();
        }

        if (readableUI == null)
        {
            Debug.LogError("ReadableTrigger: No ReadableUI found in scene!");
            return;
        }

        if (readableUI.IsOpen)
        {
            Debug.Log("ReadableTrigger: ReadableUI is already open");
            return;
        }

        readableUI.Open(pages, backgroundImage);
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
    #endregion
}
