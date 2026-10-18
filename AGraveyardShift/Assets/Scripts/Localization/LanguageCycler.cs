using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "‹ English ›" language picker for the Settings screen: the arrows step through
/// Loc.Locales and apply the choice immediately (saved as the "Locale" PlayerPref by Loc).
/// The label shows each language's own name.
/// </summary>
public class LanguageCycler : MonoBehaviour
{
    public TMP_Text label;
    public Button previousButton;
    public Button nextButton;

    private readonly List<string> codes = new List<string>();

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    private void OnEnable()
    {
        Populate();
        Loc.LocaleChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= Refresh;
    }

    private void Populate()
    {
        codes.Clear();
        foreach (var def in Loc.Locales) codes.Add(def.code);
    }

    public void Step(int direction)
    {
        if (codes.Count == 0) Populate();
        if (codes.Count == 0) return;
        int i = Mathf.Max(0, codes.IndexOf(Loc.Locale));
        i = ((i + direction) % codes.Count + codes.Count) % codes.Count;
        Loc.SetLocale(codes[i]);
    }

    private void Refresh()
    {
        if (label == null) return;
        var def = Loc.CurrentDefinition;
        label.text = def != null && !string.IsNullOrEmpty(def.nativeName) ? def.nativeName : Loc.Locale;
        // Each language's name is written in its own script, so it always uses that locale's font.
        Loc.EnsureFontComponent(label);
    }
}
