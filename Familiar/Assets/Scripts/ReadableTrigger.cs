using UnityEngine;
using TMPro;

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

    [Header("Text Overrides (Optional)")]
    [Tooltip("Override the default font. Leave empty to use default.")]
    [SerializeField] private TMP_FontAsset fontOverride;

    [Tooltip("Override text alignment. Check to apply custom alignment.")]
    [SerializeField] private bool overrideAlignment;
    [SerializeField] private TextAlignmentOptions textAlignment = TextAlignmentOptions.TopLeft;

    [Tooltip("Override text position/margins. Check to apply custom margins.")]
    [SerializeField] private bool overrideMargins;
    [SerializeField] private Vector4 textMargins = Vector4.zero;

    [Tooltip("Override line spacing (space between lines). Check to apply.")]
    [SerializeField] private bool overrideLineSpacing;
    [SerializeField] private float lineSpacing = 0f;

    [Tooltip("Override font size. Check to apply.")]
    [SerializeField] private bool overrideFontSize;
    [SerializeField] private float fontSize = 24f;

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

        var overrides = new ReadableUI.TextOverrides
        {
            font = fontOverride,
            alignment = overrideAlignment ? textAlignment : null,
            margins = overrideMargins ? textMargins : null,
            lineSpacing = overrideLineSpacing ? lineSpacing : null,
            fontSize = overrideFontSize ? fontSize : null
        };
        readableUI.Open(pages, backgroundImage, overrides);
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
    #endregion
}
