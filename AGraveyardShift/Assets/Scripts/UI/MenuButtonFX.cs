using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Adds hover / press feedback to a menu button: smooth scale, brightness (tint),
/// and a small horizontal slide. Reacts to both mouse pointer and keyboard/controller
/// selection. Attach to a GameObject that has an Image (and usually a Button).
/// Uses unscaled time so it animates while the menu is up (Time.timeScale may be 0).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Scale")]
    public float normalScale = 1f;
    [Tooltip("Keep at 1 so hovering only highlights (no grow).")]
    public float hoverScale = 1f;
    public float pressScale = 0.97f;

    [Header("Tint (multiplies the sprite)")]
    [Tooltip("Resting tint. Dimmed so hovering reads as the chalk 'lighting up'.")]
    public Color normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color hoverColor = Color.white;

    [Header("Slide")]
    [Tooltip("Local-space nudge applied while hovered/selected (px). 0 = highlight only.")]
    public float hoverOffsetX = 0f;
    public float hoverOffsetY = 0f;

    [Header("Feel")]
    [Tooltip("Higher = snappier transitions.")]
    public float responsiveness = 12f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip hoverClip;
    [Range(0f, 1f)] public float hoverVolume = 0.6f;

    private RectTransform rt;
    private Graphic graphic;
    private Button button;
    private Vector2 basePos;
    private bool hovered;
    private bool pressed;
    private bool baseCaptured;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        graphic = GetComponent<Graphic>();
        button = GetComponent<Button>();
        CaptureBase();
        ApplyImmediate();
    }

    void OnEnable()
    {
        CaptureBase();
        hovered = false;
        pressed = false;
        ApplyImmediate();
    }

    private void CaptureBase()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        basePos = rt.anchoredPosition;
        baseCaptured = true;
    }

    private bool Interactable => button == null || button.interactable;

    public void OnPointerEnter(PointerEventData e) { if (Interactable) { hovered = true; PlayHover(); } }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (Interactable) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { if (Interactable) { hovered = true; PlayHover(); } }
    public void OnDeselect(BaseEventData e) { hovered = false; pressed = false; }

    private void PlayHover()
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip, hoverVolume);
    }

    void Update()
    {
        if (!baseCaptured) CaptureBase();

        float targetScale = pressed ? pressScale : (hovered ? hoverScale : normalScale);
        Vector2 targetPos = hovered ? basePos + new Vector2(hoverOffsetX, hoverOffsetY) : basePos;
        Color targetColor = hovered ? hoverColor : normalColor;

        // Frame-rate independent exponential smoothing.
        float t = 1f - Mathf.Exp(-responsiveness * Time.unscaledDeltaTime);

        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * targetScale, t);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, t);
        if (graphic != null)
            graphic.color = Color.Lerp(graphic.color, targetColor, t);
    }

    private void ApplyImmediate()
    {
        if (rt != null)
        {
            rt.localScale = Vector3.one * normalScale;
            rt.anchoredPosition = basePos;
        }
        if (graphic != null) graphic.color = normalColor;
    }
}
