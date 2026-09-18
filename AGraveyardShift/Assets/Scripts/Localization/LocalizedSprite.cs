using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An Image whose English art has the word baked into the sprite (the PLAY / ENDINGS / …
/// brush-stroke buttons and the ENDINGS header). In English the original art is shown as-is.
/// In any other locale the image switches to a text-free "plate" version of the same brush
/// stroke and a TMP label (localized through its own <see cref="LocalizedText"/>) is drawn on top.
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class LocalizedSprite : MonoBehaviour
{
    [Tooltip("Sprite with the English word baked in.")]
    public Sprite englishSprite;
    [Tooltip("Same art without the word. Used for every other language.")]
    public Sprite plateSprite;
    [Tooltip("Label drawn over the plate. Its LocalizedText supplies the translated word.")]
    public TMP_Text label;
    [Tooltip("Locale code(s) that use the baked-in art, comma separated.")]
    public string artLocales = "en";

    private Image image;
    private Color labelBaseColor = Color.white;
    private bool labelColorCaptured;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (englishSprite == null && image != null) englishSprite = image.sprite;
        CaptureLabelColor();
    }

    private void CaptureLabelColor()
    {
        if (labelColorCaptured || label == null) return;
        labelBaseColor = label.color;
        labelColorCaptured = true;
    }

    private void LateUpdate()
    {
        // Hover/press feedback (MenuButtonFX) tints the Image; mirror that tint onto the label
        // so the translated word "lights up" exactly like the baked-in English art does.
        if (label == null || image == null || !label.gameObject.activeSelf) return;
        CaptureLabelColor();
        label.color = labelBaseColor * image.color;
    }

    private void OnEnable()
    {
        Loc.LocaleChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= Apply;
    }

    public void Apply()
    {
        if (image == null) image = GetComponent<Image>();
        if (image == null) return;

        bool useArt = UsesArt(Loc.Locale) || plateSprite == null;
        image.sprite = useArt ? (englishSprite != null ? englishSprite : image.sprite) : plateSprite;

        if (label != null)
        {
            label.gameObject.SetActive(!useArt);
            if (!useArt)
            {
                var lt = label.GetComponent<LocalizedText>();
                if (lt != null) lt.Apply();
            }
        }
    }

    private bool UsesArt(string locale)
    {
        if (string.IsNullOrEmpty(artLocales)) return false;
        foreach (string code in artLocales.Split(','))
        {
            if (string.Equals(code.Trim(), locale, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
