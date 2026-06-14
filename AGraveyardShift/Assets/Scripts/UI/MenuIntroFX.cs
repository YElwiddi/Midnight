using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a one-shot fade + rise when the menu becomes visible: fades a CanvasGroup
/// 0 -> 1 and slides the given rects up into place with a staggered ease-out.
/// Uses unscaled time. Re-plays each time the object is enabled.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MenuIntroFX : MonoBehaviour
{
    [Header("Timing")]
    public float fadeDuration = 0.8f;
    public float startDelay = 0.1f;

    [Header("Rise")]
    [Tooltip("Rects slid up into place. Don't include elements driven by their own motion script (e.g. the logo).")]
    public RectTransform[] riseTargets;
    public float riseDistance = 28f;
    [Tooltip("Extra delay added per target for a staggered feel.")]
    public float stagger = 0.08f;

    private CanvasGroup group;
    private Vector2[] basePositions;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        CaptureBases();
    }

    private void CaptureBases()
    {
        if (riseTargets == null) return;
        basePositions = new Vector2[riseTargets.Length];
        for (int i = 0; i < riseTargets.Length; i++)
            if (riseTargets[i] != null) basePositions[i] = riseTargets[i].anchoredPosition;
    }

    void OnEnable()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (basePositions == null) CaptureBases();
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        group.alpha = 0f;
        group.interactable = false;
        SetOffset(1f);

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        float elapsed = 0f;
        int count = riseTargets != null ? riseTargets.Length : 0;
        float total = fadeDuration + count * stagger;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / fadeDuration);

            for (int i = 0; i < count; i++)
            {
                if (riseTargets[i] == null) continue;
                float local = Mathf.Clamp01((elapsed - i * stagger) / fadeDuration);
                float eased = 1f - Mathf.Pow(1f - local, 3f); // ease-out cubic
                riseTargets[i].anchoredPosition = Vector2.Lerp(
                    basePositions[i] - new Vector2(0f, riseDistance),
                    basePositions[i], eased);
            }
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        SetOffset(0f);
    }

    private void SetOffset(float amount)
    {
        if (riseTargets == null || basePositions == null) return;
        for (int i = 0; i < riseTargets.Length; i++)
            if (riseTargets[i] != null)
                riseTargets[i].anchoredPosition = basePositions[i] - new Vector2(0f, riseDistance * amount);
    }
}
