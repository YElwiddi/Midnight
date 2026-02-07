using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays intro text pages that the player can click through before the game starts.
/// Similar to the ending screen but with player-controlled progression.
/// </summary>
public class IntroTextUI : MonoBehaviour
{
    [Serializable]
    public class IntroPage
    {
        [TextArea(3, 6)]
        public string text;
        public TMP_FontAsset font;
        public float fontSize = 36f;
        public Color textColor = Color.white;

        [Header("Image Page (Optional)")]
        [Tooltip("If set, displays this image instead of text")]
        public Sprite image;

        [Header("Timing")]
        [Tooltip("Minimum seconds before the player can advance (0 = skip freely)")]
        public float minimumDisplayTime = 0f;
    }

    [Header("Intro Pages")]
    [SerializeField] private IntroPage[] pages;

    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private TextMeshProUGUI continuePrompt;

    [Header("Continue Prompt")]
    [SerializeField] private string continueText = "Click to continue...";
    [SerializeField] private TMP_FontAsset continueFont;
    [SerializeField] private float continueFontSize = 20f;
    [SerializeField] private Color continueColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Typewriter Settings")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    // Runtime state
    private int currentPageIndex = 0;
    private bool isTypewriting = false;
    private bool canAdvance = false;
    private Coroutine typewriterCoroutine;
    private Action onComplete;
    private Image pageImage;
    private bool waitingForMinTime = false;

    private void Awake()
    {
        if (canvas == null)
        {
            CreateUI();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        // Check for click/key input
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            HandleInput();
        }
    }

    private void HandleInput()
    {
        // Block all input while waiting for minimum display time
        if (waitingForMinTime)
            return;

        if (isTypewriting)
        {
            // Skip typewriter - show full text immediately
            SkipTypewriter();
        }
        else if (canAdvance)
        {
            // Advance to next page
            AdvanceToNextPage();
        }
    }

    /// <summary>
    /// Shows the intro sequence. Calls onComplete when finished.
    /// </summary>
    public void Show(Action onCompleteCallback)
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("IntroTextUI: No pages configured, skipping intro.");
            onCompleteCallback?.Invoke();
            return;
        }

        onComplete = onCompleteCallback;
        currentPageIndex = 0;
        gameObject.SetActive(true);
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        // Ensure UI is set up
        if (canvas == null)
        {
            CreateUI();
        }

        // Clear text
        if (mainText != null) mainText.text = "";
        if (continuePrompt != null) continuePrompt.text = "";

        // Fade in
        yield return StartCoroutine(FadeIn());

        // Show first page
        ShowCurrentPage();
    }

    private void ShowCurrentPage()
    {
        if (currentPageIndex >= pages.Length)
        {
            // All pages shown, complete
            StartCoroutine(CompleteSequence());
            return;
        }

        IntroPage page = pages[currentPageIndex];

        // Hide continue prompt while typing
        if (continuePrompt != null)
        {
            continuePrompt.text = "";
        }

        canAdvance = false;

        bool isImagePage = page.image != null;

        // Toggle text vs image visibility
        if (mainText != null)
            mainText.gameObject.SetActive(!isImagePage);

        if (pageImage != null)
            pageImage.gameObject.SetActive(isImagePage);

        if (isImagePage)
        {
            // Image page
            if (pageImage != null)
            {
                pageImage.sprite = page.image;
                pageImage.preserveAspect = true;
            }

            // Use minimum display time or show continue prompt immediately
            if (page.minimumDisplayTime > 0f)
            {
                StartCoroutine(WaitMinimumTime(page.minimumDisplayTime));
            }
            else
            {
                OnTypewriterComplete();
            }
        }
        else
        {
            // Text page - apply styling
            if (mainText != null)
            {
                if (page.font != null)
                    mainText.font = page.font;
                mainText.fontSize = page.fontSize;
                mainText.color = page.textColor;
            }

            if (useTypewriter && !string.IsNullOrEmpty(page.text))
            {
                typewriterCoroutine = StartCoroutine(TypewriterEffect(page.text));
            }
            else
            {
                mainText.text = page.text;
                mainText.maxVisibleCharacters = int.MaxValue;

                if (page.minimumDisplayTime > 0f)
                {
                    StartCoroutine(WaitMinimumTime(page.minimumDisplayTime));
                }
                else
                {
                    OnTypewriterComplete();
                }
            }
        }
    }

    private IEnumerator WaitMinimumTime(float seconds)
    {
        waitingForMinTime = true;
        yield return new WaitForSeconds(seconds);
        waitingForMinTime = false;
        OnTypewriterComplete();
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        isTypewriting = true;

        // Set full text immediately so layout is stable, then reveal characters
        mainText.text = fullText;
        mainText.maxVisibleCharacters = 0;

        // Force mesh update to get accurate character count
        mainText.ForceMeshUpdate();
        int totalCharacters = mainText.textInfo.characterCount;

        float charDelay = 1f / typewriterSpeed;

        for (int i = 0; i <= totalCharacters; i++)
        {
            mainText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }

        isTypewriting = false;
        typewriterCoroutine = null;
        OnTypewriterComplete();
    }

    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        // Show full text of current page
        if (currentPageIndex < pages.Length && mainText != null)
        {
            mainText.text = pages[currentPageIndex].text;
            mainText.maxVisibleCharacters = int.MaxValue;
        }

        isTypewriting = false;
        OnTypewriterComplete();
    }

    private void OnTypewriterComplete()
    {
        // Show continue prompt
        if (continuePrompt != null)
        {
            if (continueFont != null)
                continuePrompt.font = continueFont;
            continuePrompt.fontSize = continueFontSize;
            continuePrompt.color = continueColor;
            continuePrompt.text = continueText;
        }

        canAdvance = true;
    }

    private void AdvanceToNextPage()
    {
        currentPageIndex++;
        ShowCurrentPage();
    }

    private IEnumerator CompleteSequence()
    {
        canAdvance = false;

        // Hide continue prompt
        if (continuePrompt != null)
        {
            continuePrompt.text = "";
        }

        // Fade out
        yield return StartCoroutine(FadeOut());

        gameObject.SetActive(false);

        // Invoke callback
        onComplete?.Invoke();
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("IntroTextCanvas");
        canvasObj.transform.SetParent(transform);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1001; // Above fade overlay

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Create black background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        RectTransform bgRect = bgImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Create main text
        GameObject textObj = new GameObject("MainText");
        textObj.transform.SetParent(canvasObj.transform, false);

        mainText = textObj.AddComponent<TextMeshProUGUI>();
        mainText.text = "";
        mainText.fontSize = 36;
        mainText.alignment = TextAlignmentOptions.Top;
        mainText.color = Color.white;

        RectTransform textRect = mainText.rectTransform;
        textRect.anchorMin = new Vector2(0.1f, 0.3f);
        textRect.anchorMax = new Vector2(0.9f, 0.8f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Create page image (for image-only pages)
        GameObject imageObj = new GameObject("PageImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        pageImage = imageObj.AddComponent<Image>();
        pageImage.preserveAspect = true;
        pageImage.color = Color.white;

        RectTransform imageRect = pageImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.05f, 0.15f);
        imageRect.anchorMax = new Vector2(0.95f, 0.9f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        imageObj.SetActive(false);

        // Create continue prompt
        GameObject promptObj = new GameObject("ContinuePrompt");
        promptObj.transform.SetParent(canvasObj.transform, false);

        continuePrompt = promptObj.AddComponent<TextMeshProUGUI>();
        continuePrompt.text = "";
        continuePrompt.fontSize = 20;
        continuePrompt.alignment = TextAlignmentOptions.Center;
        continuePrompt.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        RectTransform promptRect = continuePrompt.rectTransform;
        promptRect.anchorMin = new Vector2(0.1f, 0.1f);
        promptRect.anchorMax = new Vector2(0.9f, 0.2f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = Vector2.zero;

        Debug.Log("IntroTextUI: Created UI elements");
    }
}
