using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Fills a TMP_Dropdown with the supported languages (native names) and switches the
/// active locale when the player picks one. The choice is saved immediately by
/// <see cref="Loc.SetLocale"/>, matching how the other settings apply on change.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private readonly List<string> codes = new List<string>();
    private bool suppress;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnEnable()
    {
        Populate();
        Loc.LocaleChanged += SyncSelection;
    }

    private void OnDisable()
    {
        Loc.LocaleChanged -= SyncSelection;
    }

    private void OnDestroy()
    {
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    public void Populate()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
        codes.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var def in Loc.Locales)
        {
            codes.Add(def.code);
            options.Add(new TMP_Dropdown.OptionData(string.IsNullOrEmpty(def.nativeName) ? def.code : def.nativeName));
        }

        suppress = true;
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Max(0, codes.IndexOf(Loc.Locale)));
        dropdown.RefreshShownValue();
        suppress = false;
    }

    private void SyncSelection()
    {
        if (dropdown == null) return;
        int idx = codes.IndexOf(Loc.Locale);
        if (idx >= 0 && dropdown.value != idx)
        {
            dropdown.SetValueWithoutNotify(idx);
            dropdown.RefreshShownValue();
        }
    }

    private void OnValueChanged(int index)
    {
        if (suppress || index < 0 || index >= codes.Count) return;
        Loc.SetLocale(codes[index]);
    }
}
