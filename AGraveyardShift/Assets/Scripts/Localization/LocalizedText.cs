using TMPro;
using UnityEngine;

/// <summary>
/// Keeps a TextMeshPro label in the active language. Attach next to any TMP_Text.
///
///  • Key        — the text is <see cref="Loc.Get(string)"/> of an explicit key ("ui.menu.play").
///  • SourceText — the label's authored (English) text is the key; translated with <see cref="Loc.Tr"/>.
///                 Handy for labels typed straight into the inspector ("Resume", "Exit").
///  • FontOnly   — the text is set by code (counters, dynamic strings); this only swaps the font.
///
/// In every mode the font is resolved through <see cref="Loc.ResolveFont(TMP_FontAsset, out float)"/>
/// so scripts the authored font cannot draw (Cyrillic, CJK, Polish…) render with the locale font,
/// and the authored font/size come back when switching to English again.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    public enum Mode { Key, SourceText, FontOnly }

    [Tooltip("How the text is localized. See class summary.")]
    public Mode mode = Mode.SourceText;

    [Tooltip("Explicit string-table key (Key mode only).")]
    public string key;

    [Tooltip("Optional: keep this text uppercase after translation (menu headers).")]
    public bool forceUppercase;

    private TMP_Text text;
    private string authoredText;
    private TMP_FontAsset authoredFont;
    private float authoredSize;
    private float authoredSizeMax;
    private bool captured;

    /// <summary>The English text this label was authored with (SourceText mode).</summary>
    public string AuthoredText { get { Capture(); return authoredText; } }

    private void Awake()
    {
        Capture();
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

    private void Capture()
    {
        if (captured) return;
        text = GetComponent<TMP_Text>();
        if (text == null) return;
        captured = true;
        authoredText = text.text;
        authoredFont = text.font;
        authoredSize = text.fontSize;
        authoredSizeMax = text.fontSizeMax;
    }

    /// <summary>
    /// Re-captures the authored English text from the label (call after code changes the
    /// text of a SourceText label with a new English value).
    /// </summary>
    public void SetSourceText(string englishText)
    {
        Capture();
        authoredText = englishText;
        Apply();
    }

    /// <summary>Changes the key at runtime and refreshes.</summary>
    public void SetKey(string newKey)
    {
        Capture();
        mode = Mode.Key;
        key = newKey;
        Apply();
    }

    public void Apply()
    {
        Capture();
        if (text == null) return;
        if (!Application.isPlaying) return;

        // Font first: assigning a font resets the material, do it before the text is measured.
        float scale;
        TMP_FontAsset font = Loc.ResolveFont(authoredFont, out scale);
        if (font != null && text.font != font) text.font = font;
        if (authoredSize > 0f) text.fontSize = authoredSize * scale;
        if (authoredSizeMax > 0f) text.fontSizeMax = authoredSizeMax * scale;

        string value = null;
        switch (mode)
        {
            case Mode.Key:
                value = string.IsNullOrEmpty(key) ? authoredText : Loc.Get(key, authoredText);
                break;
            case Mode.SourceText:
                value = Loc.Tr(authoredText);
                break;
            case Mode.FontOnly:
                return;
        }

        if (value == null) return;
        if (forceUppercase) value = value.ToUpperInvariant();
        text.text = value;
    }
}
