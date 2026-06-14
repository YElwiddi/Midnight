using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DisclaimerScreen : MonoBehaviour
{
    [Header("Timing")]
    public float textFadeInDuration = 1f;
    public float holdDuration = 2f;
    public float textFadeOutDuration = 1f;
    public float backgroundFadeOutDuration = 1f;

    public static bool HasShown { get; private set; }

    /// <summary>
    /// Marks the disclaimer as already shown so it won't replay (e.g. when returning
    /// to the menu from an ending — the player has been playing, no need to warn again).
    /// </summary>
    public static void MarkAsShown()
    {
        HasShown = true;
    }

    private Image backgroundImage;
    private CanvasGroup textCanvasGroup;

    void Awake()
    {
        if (HasShown)
        {
            gameObject.SetActive(false);
            return;
        }

        backgroundImage = GetComponent<Image>();

        // Find CanvasGroup on the first child (the disclaimer text)
        if (transform.childCount > 0)
            textCanvasGroup = transform.GetChild(0).GetComponent<CanvasGroup>();

        // Background starts fully visible, text starts invisible
        if (backgroundImage != null)
            backgroundImage.color = Color.black;

        if (textCanvasGroup != null)
            textCanvasGroup.alpha = 0f;
    }

    void Start()
    {
        StartCoroutine(ShowDisclaimer());
    }

    private IEnumerator ShowDisclaimer()
    {
        // Fade in text
        yield return FadeCanvasGroup(textCanvasGroup, 0f, 1f, textFadeInDuration);

        // Hold both solid
        yield return new WaitForSeconds(holdDuration);

        // Fade out text
        yield return FadeCanvasGroup(textCanvasGroup, 1f, 0f, textFadeOutDuration);

        // Fade out black background
        yield return FadeImageAlpha(backgroundImage, 1f, 0f, backgroundFadeOutDuration);

        HasShown = true;
        gameObject.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            image.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        image.color = new Color(0f, 0f, 0f, to);
    }
}
