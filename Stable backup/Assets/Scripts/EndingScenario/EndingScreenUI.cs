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
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 30f;

    // Runtime state
    private string menuSceneName;
    private float displayDuration;
    private bool isShowing = false;

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
    public void Show(string title, string description, float duration, string menuScene)
    {
        if (isShowing) return;

        menuSceneName = menuScene;
        displayDuration = duration;

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

        // Set initial text (hidden)
        if (titleText != null)
        {
            titleText.text = "";
        }
        if (descriptionText != null)
        {
            descriptionText.text = "";
        }

        // Fade in the canvas
        yield return StartCoroutine(FadeIn());

        // Small delay before text appears
        yield return new WaitForSeconds(0.5f);

        // Typewriter effect for title
        if (titleText != null && !string.IsNullOrEmpty(title))
        {
            if (useTypewriter)
            {
                yield return StartCoroutine(TypewriterEffect(titleText, title, typewriterSpeed * 0.5f));
            }
            else
            {
                titleText.text = title;
            }
        }

        // Small delay between title and description
        yield return new WaitForSeconds(1f);

        // Typewriter effect for description
        if (descriptionText != null && !string.IsNullOrEmpty(description))
        {
            if (useTypewriter)
            {
                yield return StartCoroutine(TypewriterEffect(descriptionText, description, typewriterSpeed));
            }
            else
            {
                descriptionText.text = description;
            }
        }

        // Wait for display duration (minus the time we've already spent)
        float remainingTime = displayDuration - fadeInDuration - 1.5f;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Fade out
        yield return StartCoroutine(FadeOut());

        // Load menu scene or quit
        LoadMenuOrQuit();
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
        containerRect.anchorMin = new Vector2(0.1f, 0.2f);
        containerRect.anchorMax = new Vector2(0.9f, 0.8f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 40f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
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

        LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 100f;

        // Create description text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(containerObj.transform, false);

        descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        descriptionText.text = "";
        descriptionText.fontSize = 28;
        descriptionText.alignment = TextAlignmentOptions.Center;
        descriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
        descLayout.preferredHeight = 300f;

        Debug.Log("EndingScreenUI: Created UI elements");
    }
}
