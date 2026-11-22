using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "‹ Tape ›" picture picker for the Settings screen: the arrows step through Tape, Light and Classic pixels
/// and apply the choice immediately (saved as the "PictureMode" PlayerPref by <see cref="PictureSettings"/>).
/// </summary>
public class PictureCycler : MonoBehaviour
{
    public TMP_Text label;
    public Button previousButton;
    public Button nextButton;

    // In PictureMode order.
    private static readonly string[] Keys = { "ui.settings.picture.tape", "ui.settings.picture.light", "ui.settings.picture.classic" };
    private static readonly string[] English = { "TAPE", "LIGHT", "CLASSIC PIXELS" };

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    private void OnEnable()
    {
        Loc.LocaleChanged += Refresh;
        PictureSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= Refresh;
        PictureSettings.Changed -= Refresh;
    }

    public void Step(int direction)
    {
        int n = Keys.Length;
        int i = (((int)PictureSettings.Mode + direction) % n + n) % n;
        PictureSettings.Mode = (PictureMode)i;
    }

    private void Refresh()
    {
        if (label == null) return;
        int i = Mathf.Clamp((int)PictureSettings.Mode, 0, Keys.Length - 1);
        label.text = Loc.Get(Keys[i], English[i]);
        // The text is set here, so the label only needs the locale's font.
        Loc.EnsureFontComponent(label);
    }
}
