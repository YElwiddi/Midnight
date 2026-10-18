using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover / select feedback for the carved-stone menu labels: the word warms like candlelight,
/// a soft glow comes up behind it, and thin rules (the logo's MCMXCIX rules) draw out on both
/// sides of the text. Works with mouse and keyboard/controller selection, in unscaled time.
/// The rules follow the rendered width of the label, so translated words of any length fit.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EpitaphButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Parts")]
    public TMP_Text label;
    [Tooltip("Rule image left of the word (RULE sprite: fades out away from the word).")]
    public RectTransform ruleLeft;
    [Tooltip("Rule image right of the word (same sprite, mirrored).")]
    public RectTransform ruleRight;
    [Tooltip("Optional soft glow behind the word.")]
    public Graphic glow;

    [Header("Colours")]
    public Color idleColor = new Color(0.87f, 0.84f, 0.78f, 0.86f);
    public Color hoverColor = new Color(1f, 0.89f, 0.70f, 1f);
    public Color disabledColor = new Color(0.56f, 0.53f, 0.50f, 0.42f);

    [Header("Rules")]
    public float ruleGap = 26f;
    public float ruleLength = 118f;
    [Range(0f, 1f)] public float glowAlpha = 0.22f;

    [Header("Feel")]
    public float responsiveness = 11f;
    public float pressScale = 0.97f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip hoverClip;
    [Range(0f, 1f)] public float hoverVolume = 0.5f;

    /// <summary>Raised when the pointer or selection lands on this button.</summary>
    public event Action Hovered;

    private Selectable selectable;
    private RectTransform rt;
    private bool hovered, pressed;
    private float amount;          // 0 idle .. 1 hovered
    private Graphic ruleLeftG, ruleRightG;

    private bool Interactable => selectable == null || selectable.IsInteractable();

    private void Awake()
    {
        rt = (RectTransform)transform;
        selectable = GetComponent<Selectable>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (ruleLeft != null) ruleLeftG = ruleLeft.GetComponent<Graphic>();
        if (ruleRight != null) ruleRightG = ruleRight.GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        hovered = pressed = false;
        amount = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) { Enter(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (Interactable) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { Enter(); }
    public void OnDeselect(BaseEventData e) { hovered = false; pressed = false; }

    private void Enter()
    {
        if (!Interactable) return;
        hovered = true;
        if (audioSource != null && hoverClip != null) audioSource.PlayOneShot(hoverClip, hoverVolume);
        Hovered?.Invoke();
    }

    private void Update()
    {
        float target = hovered && Interactable ? 1f : 0f;
        float t = 1f - Mathf.Exp(-responsiveness * Time.unscaledDeltaTime);
        amount = Mathf.Lerp(amount, target, t);
        float s = Mathf.Lerp(rt.localScale.x, pressed ? pressScale : 1f, t);
        rt.localScale = new Vector3(s, s, 1f);
        Apply();
    }

    private void Apply()
    {
        if (label != null)
            label.color = Interactable ? Color.Lerp(idleColor, hoverColor, amount) : disabledColor;

        float half = 0f, cx = 0f;
        if (label != null && !string.IsNullOrEmpty(label.text))
        {
            Bounds b = label.textBounds;
            if (b.size.x > 0f) { half = b.extents.x; cx = b.center.x; }
            else half = label.preferredWidth * 0.5f;
        }

        PlaceRule(ruleLeft, ruleLeftG, cx - half - ruleGap, -1f);
        PlaceRule(ruleRight, ruleRightG, cx + half + ruleGap, 1f);

        if (glow != null)
        {
            var c = glow.color;
            c.a = glowAlpha * amount;
            glow.color = c;
        }
    }

    // A rule grows outward from the word as the hover comes in. Both rules pivot on their inner
    // (opaque) end; the right one is mirrored so it fades out to the right.
    private void PlaceRule(RectTransform r, Graphic g, float innerEdge, float side)
    {
        if (r == null) return;
        float len = ruleLength * Mathf.SmoothStep(0f, 1f, amount);
        r.sizeDelta = new Vector2(Mathf.Max(0.01f, len), r.sizeDelta.y);
        r.pivot = new Vector2(1f, 0.5f);
        var pos = r.anchoredPosition;
        pos.x = innerEdge;
        r.anchoredPosition = pos;
        r.localScale = new Vector3(side < 0f ? 1f : -1f, 1f, 1f);
        if (g != null)
        {
            var c = hoverColor;
            c.a = amount;
            g.color = c;
        }
    }
}
