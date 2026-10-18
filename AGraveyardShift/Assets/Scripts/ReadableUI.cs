using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying readable content (books, notes, tombstones, etc.)
/// Place this on a Canvas in your scene. Configure the UI elements in the Inspector.
/// </summary>
public class ReadableUI : MonoBehaviour
{
    #region Singleton
    private static ReadableUI instance;
    public static ReadableUI Instance => instance;
    #endregion

    #region Text Overrides
    public struct TextOverrides
    {
        public TMP_FontAsset font;
        public TextAlignmentOptions? alignment;
        public Vector4? margins;
        public float? lineSpacing;
        public float? fontSize;
        public Color? fontColor;
        public float? shakeIntensity;
        public float? shakeSpeed;
    }

    private TMP_FontAsset defaultFont;
    private TextAlignmentOptions defaultAlignment;
    private Vector4 defaultMargins;
    private float defaultLineSpacing;
    private float defaultFontSize;
    private Color defaultFontColor;
    private bool defaultAutoSizing;
    private float defaultFontSizeMin;
    private float defaultFontSizeMax;

    // A page that would overflow the paper (long translations, the keeper's red-pen remarks)
    // shrinks to fit, down to this fraction of its authored size.
    private const float MinFitScale = 0.7f;

    // Background panel sizing (so differently-shaped readables, e.g. an open book,
    // aren't squashed into the default note rect). Captured once, restored after each readable.
    private RectTransform backgroundRect;
    private Vector2 defaultBackgroundSize;
    #endregion

    #region UI References
    [Header("UI Panel References")]
    [Tooltip("The root panel that contains the readable UI. Will be shown/hidden.")]
    [SerializeField] private GameObject readablePanel;

    [Tooltip("Background image that displays the readable's appearance (book, scroll, etc.)")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("TextMeshPro component for displaying page content")]
    [SerializeField] private TextMeshProUGUI contentText;

    [Tooltip("Optional second text area for the right page of a two-page spread (e.g. an open book). Stretched over the background; enabled only when a readable supplies right-column text.")]
    [SerializeField] private TextMeshProUGUI contentTextRight;

    [Tooltip("Optional page indicator text (e.g., 'Page 1/3')")]
    [SerializeField] private TextMeshProUGUI pageIndicatorText;

    #endregion

    #region Private Fields
    private string[] currentPages;
    private string[] currentRightPages;   // right-page text per spread (two-page mode)
    private bool twoPageActive;
    private int currentPageIndex;
    private bool isOpen;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private GameObject crosshairObject;

    // Text shake
    private bool shakeActive;
    private float currentShakeIntensity;
    private float currentShakeSpeed;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Find references early like DialogueManager does
        playerMovement = FindFirstObjectByType<Movement>();
        crosshairManager = FindFirstObjectByType<CrosshairManager>();

        // Store default text settings BEFORE disabling the panel,
        // otherwise TMP may return null/zero for uninitialized properties
        if (contentText != null)
        {
            defaultFont = contentText.font;
            defaultAlignment = contentText.alignment;
            defaultMargins = contentText.margin;
            defaultLineSpacing = contentText.lineSpacing;
            defaultFontSize = contentText.fontSize;
            defaultFontColor = contentText.color;
            defaultAutoSizing = contentText.enableAutoSizing;
            defaultFontSizeMin = contentText.fontSizeMin;
            defaultFontSizeMax = contentText.fontSizeMax;
        }

        // The page counter is set by code; this keeps its font in sync with the locale.
        Loc.EnsureFontComponent(pageIndicatorText);

        // Capture the background panel's authored size so per-readable overrides can be reverted
        if (backgroundImage != null)
        {
            backgroundRect = backgroundImage.rectTransform;
            defaultBackgroundSize = backgroundRect.sizeDelta;
        }

        if (readablePanel != null)
        {
            readablePanel.SetActive(false);
        }
    }

    private void Start()
    {
        FindCrosshair();
    }

    private void FindCrosshair()
    {
        if (crosshairObject != null) return;

        // Method 1: Find via CrosshairManager's canvas
        if (crosshairManager != null && crosshairManager.uiCanvas != null)
        {
            Transform crosshair = crosshairManager.uiCanvas.transform.Find("Crosshair");
            if (crosshair != null)
            {
                crosshairObject = crosshair.gameObject;
                return;
            }
        }

        // Method 2: Find by name (fallback)
        GameObject crosshairCanvas = GameObject.Find("CrosshairCanvas");
        if (crosshairCanvas != null)
        {
            Transform crosshair = crosshairCanvas.transform.Find("Crosshair");
            if (crosshair != null)
            {
                crosshairObject = crosshair.gameObject;
            }
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // Allow closing with Escape or E key
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            Close();
            return;
        }

        // Click anywhere to advance/close
        if (Input.GetMouseButtonDown(0))
        {
            AdvanceOrClose();
        }

        // Apply text shake effect
        if (shakeActive && contentText != null)
        {
            ApplyTextShake();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Opens the readable UI with the specified content.
    /// </summary>
    /// <param name="pages">Array of page text content</param>
    /// <param name="background">Optional background sprite (book, scroll, etc.)</param>
    /// <param name="overrides">Optional text formatting overrides</param>
    /// <param name="backgroundSize">Optional background panel size (width, height) in canvas units.
    /// Use for differently-shaped backgrounds (e.g. an open two-page book). Null keeps the default rect.</param>
    /// <param name="rightColumnPages">Optional right-page text for a two-page spread, one entry per left page
    /// (parallel to <paramref name="pages"/>). When supplied, the main content fills the left page and these
    /// fill the right page, and each click flips both together.</param>
    /// <param name="rightColumnMargin">Margin (l,t,r,b) for the right-page text area. The left page uses the
    /// normal margins from <paramref name="overrides"/>.</param>
    /// <param name="keeperRemarks">Optional red-pen lines appended to the bottom of the first page.</param>
    /// <param name="redPenPhrases">Optional English phrases to underline in red wherever they appear (translated first).</param>
    public void Open(string[] pages, Sprite background = null, TextOverrides overrides = default, Vector2? backgroundSize = null, string[] rightColumnPages = null, Vector4? rightColumnMargin = null,
                     string[] keeperRemarks = null, string[] redPenPhrases = null)
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("ReadableUI: No pages to display");
            return;
        }

        // Localize the authored (English) pages; rich-text tags travel with the translation.
        currentPages = Loc.TrAll(pages);
        ApplyRedPen(currentPages, redPenPhrases);
        AppendKeeperRemarks(currentPages, keeperRemarks);
        currentPageIndex = 0;
        isOpen = true;

        // Set background image
        if (backgroundImage != null && background != null)
        {
            backgroundImage.sprite = background;
            backgroundImage.enabled = true;
        }
        else if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
        }

        // Apply per-readable background size; fall back to the authored default rect.
        // contentText is a stretched child of the background, so it follows automatically.
        if (backgroundRect != null)
        {
            backgroundRect.sizeDelta = backgroundSize ?? defaultBackgroundSize;
        }

        // Reset to defaults before applying overrides
        if (contentText != null)
        {
            RestoreDefaultTextSettings();
        }

        // Apply text overrides
        if (contentText != null)
        {
            if (overrides.alignment.HasValue)
                contentText.alignment = overrides.alignment.Value;
            if (overrides.margins.HasValue)
                contentText.margin = overrides.margins.Value;
            if (overrides.lineSpacing.HasValue)
                contentText.lineSpacing = overrides.lineSpacing.Value;
            if (overrides.fontColor.HasValue)
                contentText.color = overrides.fontColor.Value;

            // Font + size go through the locale font map (handwritten/horror faces are ASCII-only).
            Loc.ApplyFont(contentText,
                overrides.font != null ? overrides.font : defaultFont,
                overrides.fontSize.HasValue ? overrides.fontSize.Value : defaultFontSize);

            // Keep the authored size, but let an overflowing page shrink to fit the paper.
            EnableShrinkToFit(contentText, contentText.fontSize);
        }

        // Two-page spread: the main content fills the left page and rightColumnPages[] fills the right page.
        // Each click flips both columns together (parallel arrays). contentText keeps the resolved (left-page)
        // margin; the right area mirrors the formatting and uses its own right-page margin.
        twoPageActive = rightColumnPages != null && rightColumnPages.Length > 0
                        && contentTextRight != null && contentText != null;
        currentRightPages = twoPageActive ? Loc.TrAll(rightColumnPages) : null;
        ApplyRedPen(currentRightPages, redPenPhrases);
        if (twoPageActive)
        {
            contentTextRight.font = contentText.font;
            contentTextRight.alignment = contentText.alignment;
            contentTextRight.lineSpacing = contentText.lineSpacing;
            EnableShrinkToFit(contentTextRight, contentText.fontSizeMax);
            contentTextRight.color = contentText.color;
            contentTextRight.richText = contentText.richText;
            if (rightColumnMargin.HasValue)
                contentTextRight.margin = rightColumnMargin.Value;
            contentTextRight.gameObject.SetActive(true);
        }
        else if (contentTextRight != null)
        {
            contentTextRight.gameObject.SetActive(false);
        }

        // Apply text shake
        if (overrides.shakeIntensity.HasValue && overrides.shakeIntensity.Value > 0f)
        {
            shakeActive = true;
            currentShakeIntensity = overrides.shakeIntensity.Value;
            currentShakeSpeed = overrides.shakeSpeed.HasValue ? overrides.shakeSpeed.Value : 25f;
        }
        else
        {
            shakeActive = false;
        }

        // Show panel
        if (readablePanel != null)
        {
            readablePanel.SetActive(true);
        }

        // Display first page
        DisplayCurrentPage();

        // Pause player (also handles cursor and crosshair)
        PausePlayer();

        Debug.Log($"ReadableUI: Opened with {pages.Length} page(s)");
    }

    /// <summary>
    /// Closes the readable UI.
    /// </summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        currentPages = null;
        currentPageIndex = 0;
        shakeActive = false;

        // Restore default text settings
        if (contentText != null)
        {
            RestoreDefaultTextSettings();
        }

        // Restore the default background size so the next readable isn't affected
        if (backgroundRect != null)
        {
            backgroundRect.sizeDelta = defaultBackgroundSize;
        }

        // Hide the right-page text area (two-page spreads only)
        twoPageActive = false;
        currentRightPages = null;
        if (contentTextRight != null)
        {
            contentTextRight.text = string.Empty;
            contentTextRight.gameObject.SetActive(false);
        }

        // Hide panel
        if (readablePanel != null)
        {
            readablePanel.SetActive(false);
        }

        // Resume player (also handles cursor and crosshair)
        ResumePlayer();

        Debug.Log("ReadableUI: Closed");
    }

    /// <summary>
    /// Returns true if the readable UI is currently open.
    /// </summary>
    public bool IsOpen => isOpen;
    #endregion

    #region Private Methods
    // The previous keeper's red pen: underlines on the lines that matter, and remarks under the first page.
    private const string RedPenUnderlineOpen = "<u color=#B3261E>";
    private const string RedPenRemarkOpen = "<b><color=#8E1B12>";
    private const string RedPenRemarkClose = "</color></b>";

    private void RestoreDefaultTextSettings()
    {
        contentText.font = defaultFont;
        contentText.alignment = defaultAlignment;
        contentText.margin = defaultMargins;
        contentText.lineSpacing = defaultLineSpacing;
        contentText.enableAutoSizing = defaultAutoSizing;
        contentText.fontSizeMin = defaultFontSizeMin;
        contentText.fontSizeMax = defaultFontSizeMax;
        contentText.fontSize = defaultFontSize;
        contentText.color = defaultFontColor;
    }

    /// <summary>Authored size as the ceiling; TMP only shrinks the page when it wouldn't fit.</summary>
    private static void EnableShrinkToFit(TextMeshProUGUI text, float size)
    {
        text.fontSizeMax = size;
        text.fontSizeMin = size * MinFitScale;
        text.fontSize = size;
        text.enableAutoSizing = true;
    }

    /// <summary>Underlines each (translated) phrase in red where it first appears on each page.</summary>
    private static void ApplyRedPen(string[] pages, string[] phrases)
    {
        if (pages == null || phrases == null) return;

        foreach (string phrase in phrases)
        {
            string translated = Loc.Tr(phrase);
            if (string.IsNullOrEmpty(translated)) continue;

            for (int i = 0; i < pages.Length; i++)
            {
                if (string.IsNullOrEmpty(pages[i])) continue;
                int at = pages[i].IndexOf(translated, System.StringComparison.Ordinal);
                if (at < 0) continue;
                pages[i] = pages[i].Substring(0, at) + RedPenUnderlineOpen + translated + "</u>" + pages[i].Substring(at + translated.Length);
            }
        }
    }

    /// <summary>Adds the keeper's red-pen remarks (translated) under the first page's text.</summary>
    private static void AppendKeeperRemarks(string[] pages, string[] remarks)
    {
        if (pages == null || pages.Length == 0 || remarks == null) return;

        var sb = new System.Text.StringBuilder();
        foreach (string remark in remarks)
        {
            if (string.IsNullOrWhiteSpace(remark)) continue;
            sb.Append(sb.Length == 0 ? "\n\n" : "\n");
            sb.Append(RedPenRemarkOpen).Append(Loc.Tr(remark)).Append(RedPenRemarkClose);
        }
        if (sb.Length > 0) pages[0] = pages[0].TrimEnd() + sb;
    }

    private void AdvanceOrClose()
    {
        if (!isOpen) return;

        // If there are more pages, advance
        if (currentPageIndex < currentPages.Length - 1)
        {
            currentPageIndex++;
            DisplayCurrentPage();
            Debug.Log($"ReadableUI: Advanced to page {currentPageIndex + 1}/{currentPages.Length}");
        }
        else
        {
            // Last page, close
            Close();
        }
    }

    private void DisplayCurrentPage()
    {
        if (currentPages == null || currentPageIndex >= currentPages.Length) return;

        // Set content text
        if (contentText != null)
        {
            contentText.text = currentPages[currentPageIndex];
        }

        // Set right-page text (two-page spread)
        if (twoPageActive && contentTextRight != null && currentRightPages != null)
        {
            contentTextRight.text = currentPageIndex < currentRightPages.Length
                ? currentRightPages[currentPageIndex]
                : string.Empty;
        }

        // Update page indicator
        if (pageIndicatorText != null)
        {
            if (currentPages.Length > 1)
            {
                pageIndicatorText.text = Loc.Format("ui.readable.page", "Page {0}/{1}", currentPageIndex + 1, currentPages.Length);
                pageIndicatorText.gameObject.SetActive(true);
            }
            else
            {
                // Hide indicator for single-page readables
                pageIndicatorText.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyTextShake()
    {
        contentText.ForceMeshUpdate();
        TMP_TextInfo textInfo = contentText.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            Vector3[] vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
            int vertexIndex = charInfo.vertexIndex;

            // Per-character offset using unique seed per character
            float offsetX = Mathf.PerlinNoise((Time.time * currentShakeSpeed) + i * 0.3f, 0f) * 2f - 1f;
            float offsetY = Mathf.PerlinNoise(0f, (Time.time * currentShakeSpeed) + i * 0.3f) * 2f - 1f;
            Vector3 offset = new Vector3(offsetX, offsetY, 0f) * currentShakeIntensity;

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        // Push modified vertices back
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            contentText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    private void PausePlayer()
    {
        // Disable player movement and camera
        if (playerMovement != null)
        {
            playerMovement.DisableAllInput();
        }

        // Find crosshair if not found yet (safety net for timing issues)
        if (crosshairObject == null)
        {
            FindCrosshair();
        }

        // Hide crosshair
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(false);
        }

        // Show cursor and unlock it
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumePlayer()
    {
        // Enable player movement and camera
        if (playerMovement != null)
        {
            playerMovement.EnableAllInput();
        }

        // Show crosshair
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(true);
        }

        // Hide cursor and lock it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
}
