using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component that displays the ending screen with title and description,
/// then transitions to the main menu or quits the game.
/// </summary>
public class EndingScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriter = false;
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("Description reveal")]
    [Tooltip("Seconds after the title appears before the description/tip line starts to fade in.")]
    [SerializeField] private float descriptionDelay = 2.5f;
    [Tooltip("Seconds the description/tip line takes to fade in.")]
    [SerializeField] private float descriptionFadeDuration = 1.0f;

    [Header("Text size (normalized across endings)")]
    [Tooltip("Target rendered cap-height (px) for the TITLE. Each ending's font size is derived from this so every font renders the same size (matched to the bride / Ending IV look).")]
    [SerializeField] private float titleCapHeightPx = 67f;
    [Tooltip("Target rendered cap-height (px) for the DESCRIPTION / tip line.")]
    [SerializeField] private float descriptionCapHeightPx = 26f;

    [Header("Transition")]
    [Tooltip("If false, the next scene loads while the screen is still black (smoother — the destination fades itself in). If true, fades out to reveal the scene first.")]
    [SerializeField] private bool fadeOutBeforeLoad = false;

    // Runtime state
    private string menuSceneName;
    private float displayDuration;
    private bool isShowing = false;
    private TMP_FontAsset titleFontOverride;

    private void Awake()
    {
        // Create UI if references not set
        if (canvas == null)
        {
            CreateUI();
        }

        // Initially hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the ending screen with the specified content.
    /// </summary>
    public void Show(string title, string description, float duration, string menuScene, TMP_FontAsset titleFont = null)
    {
        if (isShowing) return;

        menuSceneName = menuScene;
        displayDuration = duration;
        titleFontOverride = titleFont;

        gameObject.SetActive(true);
        StartCoroutine(ShowSequence(title, description));
    }

    private IEnumerator ShowSequence(string title, string description)
    {
        isShowing = true;

        // Ensure canvas is set up
        if (canvas == null)
        {
            CreateUI();
        }

        // Per-ending font for the reveal — the title AND the description/tip line share it.
        if (titleFontOverride != null)
        {
            if (titleText != null) titleText.font = titleFontOverride;
            if (descriptionText != null) descriptionText.font = titleFontOverride;
        }

        // Normalize the rendered text size across endings. Different fonts render very
        // differently at the same point size, so derive each size from a target cap-height
        // instead — every ending then looks the bride's (Ending IV) size.
        if (titleText != null) titleText.fontSize = NormalizedSize(titleText.font, titleCapHeightPx);
        if (descriptionText != null) descriptionText.fontSize = NormalizedSize(descriptionText.font, descriptionCapHeightPx);

        // Collapse the description element when there's none so the title stays centered.
        if (descriptionText != null)
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));

        // The ending appears instantly — no fade-in. Only the exit fades (FadeOut).
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (useTypewriter)
        {
            // Reveal text via typewriter on the already-visible black screen.
            if (titleText != null) titleText.text = "";
            if (descriptionText != null) descriptionText.text = "";

            yield return new WaitForSeconds(0.5f);

            if (titleText != null && !string.IsNullOrEmpty(title))
                yield return StartCoroutine(TypewriterEffect(titleText, title, typewriterSpeed * 0.5f));

            yield return new WaitForSeconds(1f);

            if (descriptionText != null && !string.IsNullOrEmpty(description))
                yield return StartCoroutine(TypewriterEffect(descriptionText, description, typewriterSpeed));

            // Hold on the ending for its full duration.
            if (displayDuration > 0f)
                yield return new WaitForSeconds(displayDuration);
        }
        else
        {
            // The title appears instantly; the description/tip line fades in a few seconds later.
            if (titleText != null) titleText.text = title;

            bool hasDesc = descriptionText != null && !string.IsNullOrEmpty(description);
            float held = 0f;
            if (hasDesc)
            {
                descriptionText.text = description;
                descriptionText.alpha = 0f; // hidden until its delayed fade-in (space is still reserved, so the title doesn't shift)

                float delay = Mathf.Max(0f, descriptionDelay);
                if (delay > 0f) { yield return new WaitForSeconds(delay); held += delay; }

                float fade = Mathf.Max(0.01f, descriptionFadeDuration);
                float t = 0f;
                while (t < fade)
                {
                    t += Time.deltaTime;
                    descriptionText.alpha = Mathf.Clamp01(t / fade);
                    yield return null;
                }
                descriptionText.alpha = 1f;
                held += fade;
            }

            // Hold for the rest of the display duration (keep a short beat so the
            // description stays readable even when the duration is tight).
            float remaining = displayDuration - held;
            yield return new WaitForSeconds(hasDesc ? Mathf.Max(0.75f, remaining) : Mathf.Max(0f, remaining));
        }

        // Go to the menu. Loading WHILE BLACK avoids a brief flash of the game scene
        // (e.g. the jumpscare freeze-frame) — the menu fades itself in from black.
        if (fadeOutBeforeLoad)
        {
            yield return StartCoroutine(FadeOut());
        }

        LoadMenuOrQuit();
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

    private IEnumerator TypewriterEffect(TextMeshProUGUI textComponent, string fullText, float charsPerSecond)
    {
        textComponent.text = "";
        float charDelay = 1f / charsPerSecond;

        for (int i = 0; i < fullText.Length; i++)
        {
            textComponent.text = fullText.Substring(0, i + 1);
            yield return new WaitForSeconds(charDelay);
        }
    }

    private void LoadMenuOrQuit()
    {
        Debug.Log($"EndingScreenUI: Loading menu scene '{menuSceneName}'");

        if (!string.IsNullOrEmpty(menuSceneName))
        {
            // Ensure time scale is normal before loading
            Time.timeScale = 1f;
            // Coming from an ending: don't replay the headphones disclaimer on the menu.
            DisclaimerScreen.MarkAsShown();
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            // Quit the application
            Debug.Log("EndingScreenUI: No menu scene specified, quitting application");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Font size that makes <paramref name="font"/> render at the given cap-height (px),
    /// so different ending fonts all look the same visual size.
    /// </summary>
    private float NormalizedSize(TMP_FontAsset font, float targetCapPx)
    {
        float capPerSize = 0.7f; // sane fallback if metrics are unavailable
        if (font != null)
        {
            var fi = font.faceInfo;
            float cap = fi.capLine - fi.baseline;
            if (cap > 0.001f && fi.pointSize > 0.001f) capPerSize = cap / fi.pointSize;
        }
        return targetCapPx / Mathf.Max(0.05f, capPerSize);
    }

    private void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("EndingScreenCanvas");
        canvasObj.transform.SetParent(transform);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // On top of everything

        canvasObj.AddComponent<CanvasScaler>();
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

        // Create container for text (centered)
        GameObject containerObj = new GameObject("TextContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.05f, 0.1f);   // wider + taller so big titles have room
        containerRect.anchorMax = new Vector2(0.95f, 0.9f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 60f;               // a clear gap so the description sits a fair bit below the title
        layout.childControlWidth = true;
        layout.childControlHeight = true;   // size each row to its content so a big title can never overlap the description
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Create title text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(containerObj.transform, false);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "";
        titleText.fontSize = 72;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        // No fixed LayoutElement height — the layout group sizes this row to the title's
        // own content height (childControlHeight), so it never spills onto the description.

        // Create description text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(containerObj.transform, false);

        descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        descriptionText.text = "";
        descriptionText.fontSize = 28;
        descriptionText.alignment = TextAlignmentOptions.Center;
        descriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        // Content-sized row (see title note above).

        Debug.Log("EndingScreenUI: Created UI elements");
    }
}
