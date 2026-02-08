using UnityEngine;
using TMPro;

/// <summary>
/// A complete set of overridable properties for a readable.
/// Each field has an explicit enable-toggle so "not overridden" is distinguishable
/// from "overridden to a default-looking value."
/// </summary>
[System.Serializable]
public class ReadableOverrideSet
{
    [Header("Content")]
    [Tooltip("Override the text pages. If unchecked, falls back to the base pages.")]
    public bool overridePages;
    [TextArea(5, 15)]
    public string[] pages;

    [Header("Appearance")]
    [Tooltip("Override the background image. If unchecked, falls back to the base background.")]
    public bool overrideBackground;
    public Sprite backgroundImage;

    [Header("Text Overrides")]
    [Tooltip("Override the font. Leave empty to fall back.")]
    public TMP_FontAsset fontOverride;

    public bool overrideAlignment;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.TopLeft;

    public bool overrideMargins;
    public Vector4 textMargins = Vector4.zero;

    public bool overrideLineSpacing;
    public float lineSpacing = 0f;

    public bool overrideFontSize;
    public float fontSize = 24f;

    public bool overrideFontColor;
    public Color fontColor = Color.white;

    [Header("Text Shake")]
    [Tooltip("Enable text shake effect")]
    public bool enableShake;
    [Tooltip("How far characters move (in pixels)")]
    public float shakeIntensity = 2f;
    [Tooltip("How fast the shake moves (higher = faster)")]
    public float shakeSpeed = 25f;
}

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

    [Tooltip("Override font color. Check to apply.")]
    [SerializeField] private bool overrideFontColor;
    [SerializeField] private Color fontColor = Color.white;

    [Header("Content")]
    [Tooltip("Pages of text content. Each element is one page.")]
    [TextArea(5, 15)]
    [SerializeField] private string[] pages;

    [Header("Low Sanity Override")]
    [Tooltip("Enable alternate content/appearance when player sanity is low")]
    [SerializeField] private bool enableLowSanityOverride;

    [Tooltip("Sanity threshold (0-1). Below this percentage, the low-sanity version is shown. Default 0.25 = 25%.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowSanityThreshold = 0.25f;

    [Tooltip("Alternate content and formatting to use when sanity is below the threshold. Any field NOT enabled here falls back to the normal settings above.")]
    [SerializeField] private ReadableOverrideSet lowSanityOverrides = new ReadableOverrideSet();

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

        bool useLowSanity = IsLowSanity();

        // Resolve pages: low-sanity pages -> normal pages
        string[] resolvedPages = pages;
        if (useLowSanity && lowSanityOverrides.overridePages
            && lowSanityOverrides.pages != null && lowSanityOverrides.pages.Length > 0)
        {
            resolvedPages = lowSanityOverrides.pages;
        }

        // Resolve background: low-sanity background -> normal background
        Sprite resolvedBackground = backgroundImage;
        if (useLowSanity && lowSanityOverrides.overrideBackground)
        {
            resolvedBackground = lowSanityOverrides.backgroundImage;
        }

        // Resolve text overrides with three-tier fallback
        var overrides = BuildTextOverrides(useLowSanity);

        readableUI.Open(resolvedPages, resolvedBackground, overrides);
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
    #endregion

    #region Private Methods
    private bool IsLowSanity()
    {
        if (!enableLowSanityOverride) return false;
        if (SanityManager.Instance == null) return false;

        return SanityManager.Instance.SanityPercent < lowSanityThreshold;
    }

    private ReadableUI.TextOverrides BuildTextOverrides(bool useLowSanity)
    {
        // Start with the normal overrides (tier 2)
        TMP_FontAsset resolvedFont = fontOverride;
        TextAlignmentOptions? resolvedAlignment = overrideAlignment ? textAlignment : (TextAlignmentOptions?)null;
        Vector4? resolvedMargins = overrideMargins ? textMargins : (Vector4?)null;
        float? resolvedLineSpacing = overrideLineSpacing ? lineSpacing : (float?)null;
        float? resolvedFontSize = overrideFontSize ? fontSize : (float?)null;
        Color? resolvedFontColor = overrideFontColor ? fontColor : (Color?)null;

        // Shake defaults to off
        float? resolvedShakeIntensity = null;
        float? resolvedShakeSpeed = null;

        // Layer on low-sanity overrides (tier 1) if applicable
        if (useLowSanity)
        {
            if (lowSanityOverrides.fontOverride != null)
                resolvedFont = lowSanityOverrides.fontOverride;

            if (lowSanityOverrides.overrideAlignment)
                resolvedAlignment = lowSanityOverrides.textAlignment;

            if (lowSanityOverrides.overrideMargins)
                resolvedMargins = lowSanityOverrides.textMargins;

            if (lowSanityOverrides.overrideLineSpacing)
                resolvedLineSpacing = lowSanityOverrides.lineSpacing;

            if (lowSanityOverrides.overrideFontSize)
                resolvedFontSize = lowSanityOverrides.fontSize;

            if (lowSanityOverrides.overrideFontColor)
                resolvedFontColor = lowSanityOverrides.fontColor;

            if (lowSanityOverrides.enableShake)
            {
                resolvedShakeIntensity = lowSanityOverrides.shakeIntensity;
                resolvedShakeSpeed = lowSanityOverrides.shakeSpeed;
            }
        }

        return new ReadableUI.TextOverrides
        {
            font = resolvedFont,
            alignment = resolvedAlignment,
            margins = resolvedMargins,
            lineSpacing = resolvedLineSpacing,
            fontSize = resolvedFontSize,
            fontColor = resolvedFontColor,
            shakeIntensity = resolvedShakeIntensity,
            shakeSpeed = resolvedShakeSpeed
        };
    }
    #endregion
}
