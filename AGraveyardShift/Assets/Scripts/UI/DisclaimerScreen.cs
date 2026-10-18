using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The launch splash (it replaced the plain headphones card): the Epitaph logo comes up out of
/// the dark while a slow light crosses the carving, then the headphones line appears under it.
/// Any key or click skips it. Shown once per session; <see cref="HasShown"/> also starts the
/// menu music and the menu's own entrance, and EndingScreenUI marks it shown when returning
/// from an ending.
/// </summary>
public class DisclaimerScreen : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("Full-screen black behind the splash (usually this object's Image).")]
    public Image background;
    public CanvasGroup logoGroup;
    [Tooltip("The logo image; its material uses the UI/Epitaph Sweep shader.")]
    public Image logo;
    public CanvasGroup headphonesGroup;

    [Header("Timing (seconds)")]
    public float logoFadeIn = 1.2f;
    public float sweepDelay = 0.5f;
    public float sweepDuration = 2.2f;
    public float headphonesDelay = 2.0f;
    public float headphonesFadeIn = 0.8f;
    public float hold = 1.2f;
    public float fadeOut = 0.7f;
    public float backgroundFadeOut = 0.8f;

    public static bool HasShown { get; private set; }

    /// <summary>
    /// Marks the splash as already shown so it won't replay (e.g. when returning to the menu
    /// from an ending — the player has been playing, no need to show it again).
    /// </summary>
    public static void MarkAsShown()
    {
        HasShown = true;
    }

    private static readonly int SweepPos = Shader.PropertyToID("_SweepPos");

    private Material sweepMaterial;
    private bool skip;
    private float startTime;

    void Awake()
    {
        if (HasShown)
        {
            gameObject.SetActive(false);
            return;
        }

        if (background == null) background = GetComponent<Image>();
        if (background != null) background.color = Color.black;
        if (logoGroup != null) logoGroup.alpha = 0f;
        if (headphonesGroup != null) headphonesGroup.alpha = 0f;

        // Own copy of the sweep material so the animated property never touches the asset.
        if (logo != null && logo.material != null && logo.material.HasProperty(SweepPos))
        {
            sweepMaterial = new Material(logo.material);
            sweepMaterial.SetFloat(SweepPos, -1f);
            logo.material = sweepMaterial;
        }
    }

    void Start()
    {
        startTime = Time.unscaledTime;
        StartCoroutine(Show());
    }

    void Update()
    {
        if (!skip && Time.unscaledTime - startTime > 0.25f && Input.anyKeyDown)
            skip = true;
    }

    void OnDestroy()
    {
        if (sweepMaterial != null) Destroy(sweepMaterial);
    }

    private IEnumerator Show()
    {
        float total = Mathf.Max(logoFadeIn, Mathf.Max(sweepDelay + sweepDuration, headphonesDelay + headphonesFadeIn)) + hold;
        for (float t = 0f; t < total && !skip; t += Time.unscaledDeltaTime)
        {
            if (logoGroup != null) logoGroup.alpha = Smooth(t / logoFadeIn);
            if (sweepMaterial != null) sweepMaterial.SetFloat(SweepPos, Mathf.Lerp(-0.35f, 1.35f, Mathf.Clamp01((t - sweepDelay) / sweepDuration)));
            if (headphonesGroup != null) headphonesGroup.alpha = Smooth((t - headphonesDelay) / headphonesFadeIn);
            yield return null;
        }

        // Content out, then the black lifts on the menu.
        float logoFrom = logoGroup != null ? logoGroup.alpha : 0f;
        float phonesFrom = headphonesGroup != null ? headphonesGroup.alpha : 0f;
        float d = skip ? 0.3f : fadeOut;
        for (float f = 0f; f < 1f; )
        {
            f = Mathf.Min(1f, f + Time.unscaledDeltaTime / d);
            if (logoGroup != null) logoGroup.alpha = Mathf.Lerp(logoFrom, 0f, f);
            if (headphonesGroup != null) headphonesGroup.alpha = Mathf.Lerp(phonesFrom, 0f, f);
            yield return null;
        }

        HasShown = true;

        float bd = skip ? 0.4f : backgroundFadeOut;
        for (float f = 0f; f < 1f; )
        {
            f = Mathf.Min(1f, f + Time.unscaledDeltaTime / bd);
            if (background != null) background.color = new Color(0f, 0f, 0f, 1f - f);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
