using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Project-wide localization configuration: the supported locales, how each one maps
/// from the OS language, and which TMP fonts replace the game's (ASCII-only) display
/// fonts for scripts they cannot render. Lives in Resources so it is available from
/// the very first frame (see <see cref="Loc"/>).
///
/// String tables live next to it as TextAssets: Resources/Localization/Tables/&lt;code&gt;.json
/// </summary>
[CreateAssetMenu(fileName = "LocalizationSettings", menuName = "Game/Localization Settings")]
public class LocalizationSettings : ScriptableObject
{
    public const string ResourcesPath = "Localization/LocalizationSettings";
    public const string TablesResourcesFolder = "Localization/Tables";

    /// <summary>Replaces one specific font asset with another for a locale.</summary>
    [Serializable]
    public class FontRule
    {
        [Tooltip("The font as authored in scenes/prefabs (e.g. HelpMe SDF).")]
        public TMP_FontAsset original;
        [Tooltip("Font to render with instead. Leave empty to keep the original.")]
        public TMP_FontAsset replacement;
        [Tooltip("Multiplies the authored font size when this rule applies (1 = unchanged). Use it when the replacement face runs visibly larger or smaller.")]
        public float sizeScale = 1f;
    }

    [Serializable]
    public class LocaleDefinition
    {
        [Tooltip("BCP-47 style code, also the string table file name (en, zh-Hans, pt-BR, ...).")]
        public string code = "en";
        [Tooltip("Language name written in that language. Shown in the language selector.")]
        public string nativeName = "English";
        [Tooltip("Language name in English (for tooling / logs).")]
        public string englishName = "English";
        [Tooltip("OS languages that pick this locale automatically on first launch.")]
        public SystemLanguage[] systemLanguages = new SystemLanguage[0];

        [Header("Fonts")]
        [Tooltip("Font used for every text in this locale unless a rule below says otherwise. Leave empty to keep the authored fonts (Latin locales).")]
        public TMP_FontAsset defaultFont;
        [Tooltip("Size multiplier applied together with Default Font.")]
        public float defaultSizeScale = 1f;
        [Tooltip("Per-font replacements, checked before Default Font.")]
        public List<FontRule> fontRules = new List<FontRule>();
    }

    [Tooltip("Locale used when the OS language is not supported and nothing was saved yet. Also the fallback for missing keys.")]
    public string defaultLocale = "en";

    public List<LocaleDefinition> locales = new List<LocaleDefinition>();

    [Header("Global TMP fallbacks")]
    [Tooltip("Appended to TMP Settings > Fallback Font Assets at startup so no text in the game can ever show a missing-glyph box, whatever font it uses.")]
    public List<TMP_FontAsset> globalFallbackFonts = new List<TMP_FontAsset>();

    [Header("Diagnostics")]
    [Tooltip("Log each key/source string that has no translation in the active locale (once per string). Editor and development builds only.")]
    public bool logMissingTranslations = true;

    public LocaleDefinition Find(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        for (int i = 0; i < locales.Count; i++)
            if (string.Equals(locales[i].code, code, StringComparison.OrdinalIgnoreCase))
                return locales[i];
        return null;
    }

    public LocaleDefinition FromSystemLanguage(SystemLanguage language)
    {
        for (int i = 0; i < locales.Count; i++)
        {
            var l = locales[i];
            if (l.systemLanguages == null) continue;
            for (int j = 0; j < l.systemLanguages.Length; j++)
                if (l.systemLanguages[j] == language) return l;
        }
        return null;
    }
}
