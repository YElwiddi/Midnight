using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime localization service.
///
/// Two kinds of lookups share one string table per locale:
///   • <see cref="Get(string)"/>  — explicit keys for UI chrome and code strings ("ui.menu.play").
///   • <see cref="Tr(string)"/>   — gettext-style: the English source text IS the key. Used for
///     everything authored in the inspector or in Ink (dialogue lines, choices, notes, prompts),
///     so designers keep writing plain English and nothing has to be re-keyed.
///
/// Tables are TextAssets in Resources/Localization/Tables/&lt;code&gt;.json (see <see cref="StringTableData"/>).
/// The active locale is chosen from PlayerPrefs, else the OS language, else the default, and is
/// applied before the first scene loads. Fonts are resolved per locale via <see cref="ResolveFont"/>.
/// </summary>
public static class Loc
{
    public const string PlayerPrefsKey = "Locale";

    /// <summary>Raised after the active locale (and its table) changed. UI re-applies its texts on this.</summary>
    public static event Action LocaleChanged;

    private static LocalizationSettings settings;
    private static LocalizationSettings.LocaleDefinition current;
    private static Dictionary<string, string> table = new Dictionary<string, string>();
    private static Dictionary<string, string> defaultTable;
    private static string locale = "en";
    private static bool initialized;
    private static bool fallbackFontsRegistered;
    private static readonly HashSet<string> reportedMissing = new HashSet<string>();

    /// <summary>Active locale code (e.g. "en", "zh-Hans", "pt-BR").</summary>
    public static string Locale { get { EnsureInitialized(); return locale; } }

    /// <summary>True while the active locale is the source-language (default) locale.</summary>
    public static bool IsDefaultLocale
    {
        get { EnsureInitialized(); return string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase); }
    }

    public static string DefaultLocale => settings != null && !string.IsNullOrEmpty(settings.defaultLocale) ? settings.defaultLocale : "en";

    public static LocalizationSettings Settings { get { EnsureInitialized(); return settings; } }

    public static IReadOnlyList<LocalizationSettings.LocaleDefinition> Locales
    {
        get { EnsureInitialized(); return settings != null ? settings.locales : new List<LocalizationSettings.LocaleDefinition>(); }
    }

    public static LocalizationSettings.LocaleDefinition CurrentDefinition { get { EnsureInitialized(); return current; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        settings = Resources.Load<LocalizationSettings>(LocalizationSettings.ResourcesPath);
        if (settings == null)
        {
            Debug.LogWarning("Loc: no LocalizationSettings asset found at Resources/" + LocalizationSettings.ResourcesPath + ". Running in English only.");
        }

        RegisterFallbackFonts();

        string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        string code = (!string.IsNullOrEmpty(saved) && (settings == null || settings.Find(saved) != null))
            ? saved
            : DetectSystemLocale();

        LoadLocale(code);
    }

    /// <summary>Maps the OS language to a supported locale, else the default locale.</summary>
    public static string DetectSystemLocale()
    {
        if (settings == null) return "en";
        var def = settings.FromSystemLanguage(Application.systemLanguage);
        return def != null ? def.code : DefaultLocale;
    }

    /// <summary>Switches the active language. Persists the choice unless <paramref name="save"/> is false.</summary>
    public static void SetLocale(string code, bool save = true)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(code)) return;
        if (settings != null && settings.Find(code) == null)
        {
            Debug.LogWarning("Loc: unknown locale '" + code + "'");
            return;
        }

        bool changed = !string.Equals(code, locale, StringComparison.OrdinalIgnoreCase);
        LoadLocale(code);

        if (save)
        {
            PlayerPrefs.SetString(PlayerPrefsKey, locale);
            PlayerPrefs.Save();
        }

        if (changed) LocaleChanged?.Invoke();
    }

    // ------------------------------------------------------------------ lookups

    /// <summary>Explicit-key lookup. Falls back to the default locale's table, then to the key itself.</summary>
    public static string Get(string key)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(key)) return string.Empty;

        string value;
        if (table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;

        ReportMissing(key);

        if (defaultTable != null && defaultTable.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
        return key;
    }

    /// <summary>Explicit-key lookup with an inline English fallback (for code-defined strings).</summary>
    public static string Get(string key, string englishFallback)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(key)) return englishFallback;

        string value;
        if (table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;

        ReportMissing(key);

        if (defaultTable != null && defaultTable.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
        return englishFallback;
    }

    /// <summary>String.Format over a localized explicit key.</summary>
    public static string Format(string key, params object[] args)
    {
        string fmt = Get(key);
        try { return string.Format(fmt, args); }
        catch (FormatException) { return fmt; }
    }

    public static string Format(string key, string englishFallback, params object[] args)
    {
        string fmt = Get(key, englishFallback);
        try { return string.Format(fmt, args); }
        catch (FormatException) { return fmt; }
    }

    /// <summary>
    /// Source-text lookup (gettext style): returns the translation of an English string authored
    /// in the inspector or in Ink, or the string itself when there is none / in English.
    /// Whitespace and line-ending differences between the authored text and the table key are tolerated.
    /// </summary>
    public static string Tr(string source)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(source)) return source;
        if (IsDefaultLocale || table.Count == 0) return source;

        string value;
        string normalized = NormalizeKey(source);
        if (table.TryGetValue(normalized, out value))
            return string.IsNullOrEmpty(value) ? source : value;

        if (normalized.Length > 0) ReportMissing(normalized);
        return source;
    }

    /// <summary>Translates each element of an array (null-safe). Returns a new array.</summary>
    public static string[] TrAll(string[] sources)
    {
        if (sources == null) return null;
        var result = new string[sources.Length];
        for (int i = 0; i < sources.Length; i++) result[i] = Tr(sources[i]);
        return result;
    }

    public static bool Has(string key)
    {
        EnsureInitialized();
        return !string.IsNullOrEmpty(key) && table.ContainsKey(key);
    }

    /// <summary>Canonical form of a source-text key: trimmed, \r\n → \n. Applied on both sides of the lookup.</summary>
    public static string NormalizeKey(string s)
    {
        if (s == null) return string.Empty;
        string t = s.Trim();
        if (t.IndexOf('\r') >= 0) t = t.Replace("\r\n", "\n").Replace('\r', '\n');
        return t;
    }

    // ------------------------------------------------------------------ fonts

    /// <summary>
    /// The font to render <paramref name="original"/> with in the active locale. Returns the
    /// original when the locale keeps authored fonts (English / no rule).
    /// </summary>
    public static TMP_FontAsset ResolveFont(TMP_FontAsset original)
    {
        float scale;
        return ResolveFont(original, out scale);
    }

    /// <summary>As <see cref="ResolveFont(TMP_FontAsset)"/>, also returning the size multiplier to apply.</summary>
    public static TMP_FontAsset ResolveFont(TMP_FontAsset original, out float sizeScale)
    {
        EnsureInitialized();
        sizeScale = 1f;
        if (current == null) return original;

        if (original != null && current.fontRules != null)
        {
            for (int i = 0; i < current.fontRules.Count; i++)
            {
                var rule = current.fontRules[i];
                if (rule == null || rule.original != original) continue;
                if (rule.replacement == null) return original;
                sizeScale = rule.sizeScale > 0f ? rule.sizeScale : 1f;
                return rule.replacement;
            }
        }

        if (current.defaultFont != null)
        {
            sizeScale = current.defaultSizeScale > 0f ? current.defaultSizeScale : 1f;
            return current.defaultFont;
        }
        return original;
    }

    /// <summary>
    /// Applies the locale font (and size scale) to a text component created at runtime.
    /// <paramref name="authoredFont"/>/<paramref name="authoredSize"/> are the values the text
    /// would have in English; pass what you would otherwise assign directly.
    /// </summary>
    public static void ApplyFont(TMP_Text text, TMP_FontAsset authoredFont, float authoredSize)
    {
        if (text == null) return;
        float scale;
        TMP_FontAsset font = ResolveFont(authoredFont, out scale);
        if (font != null) text.font = font;
        if (authoredSize > 0f)
        {
            if (text.enableAutoSizing) text.fontSizeMax = authoredSize * scale;
            else text.fontSize = authoredSize * scale;
        }
    }

    /// <summary>Applies the locale font to a text, using its current font/size as the authored values.</summary>
    public static void ApplyFont(TMP_Text text)
    {
        if (text == null) return;
        ApplyFont(text, text.font, text.enableAutoSizing ? text.fontSizeMax : text.fontSize);
    }

    /// <summary>
    /// Makes a code-driven label follow the locale font for the rest of its life: adds a
    /// <see cref="LocalizedText"/> in FontOnly mode (once), which also restores the authored
    /// font when switching back to English.
    /// </summary>
    public static LocalizedText EnsureFontComponent(TMP_Text text)
    {
        if (text == null) return null;
        var lt = text.GetComponent<LocalizedText>();
        if (lt == null)
        {
            lt = text.gameObject.AddComponent<LocalizedText>();
            lt.mode = LocalizedText.Mode.FontOnly;
        }
        return lt;
    }

    /// <summary>
    /// Makes a code-created label localized by key: adds a <see cref="LocalizedText"/> in Key
    /// mode (once) and applies it immediately.
    /// </summary>
    public static LocalizedText BindKey(TMP_Text text, string key)
    {
        if (text == null) return null;
        var lt = text.GetComponent<LocalizedText>();
        if (lt == null) lt = text.gameObject.AddComponent<LocalizedText>();
        lt.SetKey(key);
        return lt;
    }

    // ------------------------------------------------------------------ internals

    private static void LoadLocale(string code)
    {
        locale = string.IsNullOrEmpty(code) ? DefaultLocale : code;
        current = settings != null ? settings.Find(locale) : null;

        table = LoadTable(locale) ?? new Dictionary<string, string>();

        if (defaultTable == null)
            defaultTable = string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase) ? table : (LoadTable(DefaultLocale) ?? new Dictionary<string, string>());

        reportedMissing.Clear();
    }

    private static Dictionary<string, string> LoadTable(string code)
    {
        var asset = Resources.Load<TextAsset>(LocalizationSettings.TablesResourcesFolder + "/" + code);
        if (asset == null)
        {
            if (!string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning("Loc: no string table for locale '" + code + "'");
            return null;
        }
        return StringTableData.Parse(asset.text);
    }

    private static void RegisterFallbackFonts()
    {
        if (fallbackFontsRegistered || settings == null) return;
        fallbackFontsRegistered = true;

        var tmpSettings = TMP_Settings.instance;
        if (tmpSettings == null) return;

        var list = TMP_Settings.fallbackFontAssets;
        if (list == null) return;
        foreach (var f in settings.globalFallbackFonts)
        {
            if (f != null && !list.Contains(f)) list.Add(f);
        }
    }

    private static void ReportMissing(string key)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (settings == null || !settings.logMissingTranslations) return;
        if (IsDefaultLocale) return;
        if (reportedMissing.Add(key))
            Debug.LogWarning("Loc[" + locale + "]: missing translation for \"" + (key.Length > 120 ? key.Substring(0, 120) + "…" : key) + "\"");
#endif
    }
}

/// <summary>
/// Serialized form of a string table (JsonUtility-friendly):
/// { "locale": "fr", "entries": [ { "k": "ui.menu.play", "v": "JOUER" }, ... ] }
/// </summary>
[Serializable]
public class StringTableData
{
    [Serializable]
    public class Entry
    {
        public string k;
        public string v;
    }

    public string locale;
    public List<Entry> entries = new List<Entry>();

    public static Dictionary<string, string> Parse(string json)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(json)) return dict;
        StringTableData data;
        try { data = JsonUtility.FromJson<StringTableData>(json); }
        catch (Exception e)
        {
            Debug.LogError("Loc: failed to parse string table: " + e.Message);
            return dict;
        }
        if (data == null || data.entries == null) return dict;
        foreach (var e in data.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.k)) continue;
            dict[Loc.NormalizeKey(e.k)] = e.v ?? string.Empty;
        }
        return dict;
    }

    public static string Serialize(string locale, IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var data = new StringTableData { locale = locale };
        foreach (var kv in pairs) data.entries.Add(new Entry { k = kv.Key, v = kv.Value });
        return JsonUtility.ToJson(data, true);
    }
}
