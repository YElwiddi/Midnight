#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// One-shot (idempotent) project setup for localization:
///   • Create Font Assets   — dynamic TMP font assets for the bundled Noto / Rubik / PT Serif / VT323
///                            fonts plus dynamic twins of the game's own Latin fonts, and the
///                            LocalizationSettings asset with the per-locale font rules; registers
///                            the Noto fonts as global TMP fallbacks.
///   • Setup Main Menu      — language dropdown in Settings, LocalizedText on every static label,
///                            plate+label versions of the sprite buttons, localized headphones note.
///   • Setup Gameplay Scene — LocalizedText on the pause menu labels.
/// Safe to re-run. The main menu binds its own keys in MainMenuBuilder (Epitaph rework, 2026-10-18).
/// </summary>
public static class LocalizationSetup
{
    const string FontsDir = "Assets/Localization/Fonts/";
    const string SettingsAssetPath = "Assets/Resources/" + LocalizationSettings.ResourcesPath + ".asset";
    const string MenuScene = "Assets/Scenes/Start Menu Scene.unity";
    const string GameScene = "Assets/Scenes/Scene 0.5 Dungeon.unity";
    const string HelpMeFontPath = "Assets/Additional Models and Assets/HelpMe SDF.asset";
    const string PlatesDir = "Assets/UI/MainMenu/Plates/";

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/Graveyard/Localization/Setup/All (fonts + settings + scenes)")]
    public static void SetupAll()
    {
        CreateFontAssetsAndSettings();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        SetupMainMenu();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        SetupGameplayScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("LocalizationSetup: done.");
    }

    // ------------------------------------------------------------------ fonts + settings

    [MenuItem("Tools/Graveyard/Localization/Setup/Create Font Assets + Settings")]
    public static void CreateFontAssetsAndSettings()
    {
        var notoSans = DynamicFont("NotoSans-Regular.ttf", "NotoSans SDF", 1024);
        var notoSC = DynamicFont("NotoSansSC-Regular.otf", "NotoSansSC SDF", 2048);
        var notoJP = DynamicFont("NotoSansJP-Regular.otf", "NotoSansJP SDF", 2048);
        var notoKR = DynamicFont("NotoSansKR-Regular.otf", "NotoSansKR SDF", 2048);
        var rubik = DynamicFont("RubikDistressed-Regular.ttf", "RubikDistressed SDF", 1024);
        var rubikWet = DynamicFont("RubikWetPaint-Regular.ttf", "RubikWetPaint SDF", 1024);
        var vt323 = DynamicFont("VT323-Regular.ttf", "VT323 SDF", 1024);
        var ptSerif = DynamicFont("PTSerif-Regular.ttf", "PTSerif SDF", 1024);

        // Dynamic twins of the game's own Latin fonts (their shipped atlases are ASCII-only).
        var caslonDyn = DynamicFont("Assets/Additional Models and Assets/CaslonAntique.ttf", FontsDir + "CaslonAntique Dynamic SDF.asset", 1024);
        var dontExpectDyn = DynamicFont("Assets/Additional Models and Assets/DontExpectDemo-G3ArG.ttf", FontsDir + "DontExpectDemo Dynamic SDF.asset", 1024);

        // Authored fonts referenced by scenes/prefabs.
        var helpMe = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HelpMeFontPath);
        var caslon = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/CaslonAntique SDF.asset");
        var caslonReg = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/caslon-antique.regular SDF.asset");
        var dontExpect = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/DontExpectDemo-G3ArG SDF.asset");
        var youMurderer = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/Youmurdererbb-pwoK SDF.asset");
        var perfectDos = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/perfect dos vga 437 win SDF.asset");
        var indieFlower = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Additional Models and Assets/Indie_Flower/IndieFlower-Regular SDF.asset");
        var demonSker = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Fonts/DemonSker-zyzD SDF.asset");

        // Global fallbacks: guarantee every glyph in every font.
        var fallbacks = new[] { notoSans, notoSC, notoJP, notoKR };

        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsAssetPath);
        if (settings == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath));
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        }

        settings.defaultLocale = "en";
        settings.globalFallbackFonts = fallbacks.Where(f => f != null).ToList();
        settings.locales = new List<LocalizationSettings.LocaleDefinition>();

        // Rules shared by the Latin locales whose accents the game fonts (or their dynamic twins) can draw.
        System.Func<bool, List<LocalizationSettings.FontRule>> latinRules = polish =>
        {
            var r = new List<LocalizationSettings.FontRule>
            {
                Rule(helpMe, rubik, 0.82f),
                Rule(youMurderer, rubikWet, 0.9f),
                Rule(demonSker, rubik, 0.85f),
                Rule(perfectDos, vt323, 1.1f),
                Rule(indieFlower, ptSerif, 1f),
                Rule(caslon, polish ? ptSerif : caslonDyn, 1f),
                Rule(caslonReg, polish ? ptSerif : caslonDyn, 1f),
                Rule(dontExpect, polish ? rubik : dontExpectDyn, polish ? 0.85f : 1f),
            };
            return r.Where(x => x.original != null && x.replacement != null).ToList();
        };

        var cyrillicRules = new List<LocalizationSettings.FontRule>
        {
            Rule(helpMe, rubik, 0.82f),
            Rule(youMurderer, rubikWet, 0.9f),
            Rule(demonSker, rubik, 0.85f),
            Rule(perfectDos, notoSans, 0.95f),
            Rule(indieFlower, ptSerif, 1f),
            Rule(caslon, ptSerif, 1f),
            Rule(caslonReg, ptSerif, 1f),
            Rule(dontExpect, rubik, 0.85f),
        }.Where(x => x.original != null && x.replacement != null).ToList();

        settings.locales.Add(Locale("en", "English", "English", new[] { SystemLanguage.English }, null, 1f, null));
        settings.locales.Add(Locale("zh-Hans", "简体中文", "Chinese (Simplified)", new[] { SystemLanguage.ChineseSimplified, SystemLanguage.Chinese }, notoSC, 1f, null));
        settings.locales.Add(Locale("ru", "Русский", "Russian", new[] { SystemLanguage.Russian }, null, 1f, cyrillicRules));
        settings.locales.Add(Locale("es", "Español", "Spanish", new[] { SystemLanguage.Spanish }, null, 1f, latinRules(false)));
        settings.locales.Add(Locale("pt-BR", "Português (Brasil)", "Portuguese (Brazil)", new[] { SystemLanguage.Portuguese }, null, 1f, latinRules(false)));
        settings.locales.Add(Locale("de", "Deutsch", "German", new[] { SystemLanguage.German }, null, 1f, latinRules(false)));
        settings.locales.Add(Locale("ja", "日本語", "Japanese", new[] { SystemLanguage.Japanese }, notoJP, 1f, null));
        settings.locales.Add(Locale("fr", "Français", "French", new[] { SystemLanguage.French }, null, 1f, latinRules(false)));
        settings.locales.Add(Locale("pl", "Polski", "Polish", new[] { SystemLanguage.Polish }, null, 1f, latinRules(true)));
        settings.locales.Add(Locale("ko", "한국어", "Korean", new[] { SystemLanguage.Korean }, notoKR, 1f, null));

        EditorUtility.SetDirty(settings);

        // Persist the fallbacks in TMP Settings too (Loc also adds them at runtime).
        var tmpSettings = TMP_Settings.instance;
        if (tmpSettings != null)
        {
            var list = TMP_Settings.fallbackFontAssets;
            foreach (var f in fallbacks)
                if (f != null && !list.Contains(f)) list.Add(f);
            EditorUtility.SetDirty(tmpSettings);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LocalizationSetup: font assets + LocalizationSettings ready.");
    }

    static LocalizationSettings.FontRule Rule(TMP_FontAsset original, TMP_FontAsset replacement, float scale)
    {
        return new LocalizationSettings.FontRule { original = original, replacement = replacement, sizeScale = scale };
    }

    static LocalizationSettings.LocaleDefinition Locale(string code, string native, string english, SystemLanguage[] langs, TMP_FontAsset defaultFont, float scale, List<LocalizationSettings.FontRule> rules)
    {
        return new LocalizationSettings.LocaleDefinition
        {
            code = code, nativeName = native, englishName = english, systemLanguages = langs,
            defaultFont = defaultFont, defaultSizeScale = scale,
            fontRules = rules ?? new List<LocalizationSettings.FontRule>()
        };
    }

    static TMP_FontAsset DynamicFont(string fileName, string assetName, int atlasSize)
    {
        string fontPath = fileName.StartsWith("Assets/") ? fileName : FontsDir + fileName;
        string assetPath = assetName.StartsWith("Assets/") ? assetName : FontsDir + assetName + ".asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null) { Debug.LogError("LocalizationSetup: font file not found: " + fontPath); return null; }

        var fa = TMP_FontAsset.CreateFontAsset(font, 60, 6, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
        if (fa == null) { Debug.LogError("LocalizationSetup: could not create font asset for " + fontPath); return null; }

        string name = Path.GetFileNameWithoutExtension(assetPath);
        fa.name = name;
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.material.name = name + " Material";
        fa.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        AssetDatabase.SaveAssets();
        Debug.Log("LocalizationSetup: created " + assetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    // ------------------------------------------------------------------ main menu scene

    [MenuItem("Tools/Graveyard/Localization/Setup/Main Menu (current scene)")]
    public static void SetupMainMenuMenu()
    {
        SetupMainMenu();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void SetupMainMenu()
    {
        // The Epitaph menu (MainMenuBuilder) is plain TMP text in Cinzel / Cormorant and binds its own
        // string keys and font components while it builds, so there is nothing left to convert here.
        // (The old brush-plate conversion below would break the new splash and buttons.)
        Debug.Log("LocalizationSetup: the main menu is localized by MainMenuBuilder (Tools > Graveyard > Build Main Menu).");
    }

    static void SetupOptions(Transform options, TMP_FontAsset font, GameMainMenu menu)
    {
        var op = options.GetComponent<OptionsPanel>();

        // Row layout (panel is 1416 x 912, centered): language on top, then the existing rows.
        const float labelX = -440f, controlX = 160f;
        const float rowLang = 220f, rowRes = 120f, rowFull = 20f, rowBright = -80f, rowVol = -180f;

        Move(options, "Label_Resolution", labelX, rowRes);
        Move(options, "ResolutionValue", controlX, rowRes);
        Move(options, "ResLeft", controlX - 185f, rowRes);
        Move(options, "ResRight", controlX + 185f, rowRes);
        Move(options, "Label_Fullscreen", labelX, rowFull);
        Move(options, "FullscreenToggle", controlX, rowFull);
        Move(options, "Text (TMP) (1)", labelX, rowBright);
        Move(options, "Brightness Slider", controlX, rowBright);
        Move(options, "Text (TMP)", labelX, rowVol);
        Move(options, "Slider", controlX, rowVol);

        KeyText(options.Find("Title"), "ui.settings.title");
        KeyText(options.Find("Label_Resolution"), "ui.settings.resolution");
        KeyText(options.Find("Label_Fullscreen"), "ui.settings.fullscreen");
        KeyText(options.Find("Text (TMP) (1)"), "ui.settings.brightness");
        KeyText(options.Find("Text (TMP)"), "ui.settings.volume");
        FontOnly(options.Find("ResolutionValue"));
        FontOnly(options.Find("ResLeft"));
        FontOnly(options.Find("ResRight"));
        MakeSprite(options.Find("AcceptButton"), "ACCEPT", "ui.menu.accept", font, 0.30f);
        MakeSprite(options.Find("CancelButton"), "CANCEL", "ui.menu.cancel", font, 0.30f);

        // Language label
        var langLabel = options.Find("Label_Language");
        if (langLabel == null)
        {
            var t = NewText("Label_Language", options, "Language", font, 38f, new Color(0.92f, 0.92f, 0.92f, 1f), TextAlignmentOptions.MidlineLeft);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 60f);
            langLabel = t.transform;
        }
        Move(options, "Label_Language", labelX, rowLang);
        KeyText(langLabel, "ui.settings.language");

        // Language dropdown
        var ddTf = options.Find("LanguageDropdown");
        TMP_Dropdown dropdown;
        if (ddTf == null)
        {
            var res = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            };
            var go = TMP_DefaultControls.CreateDropdown(res);
            go.name = "LanguageDropdown";
            go.transform.SetParent(options, false);
            ddTf = go.transform;
            StyleDropdown(go, font);
        }
        dropdown = ddTf.GetComponent<TMP_Dropdown>();
        if (ddTf.GetComponent<LanguageDropdown>() == null) ddTf.gameObject.AddComponent<LanguageDropdown>();
        var ddRt = (RectTransform)ddTf;
        ddRt.anchorMin = ddRt.anchorMax = new Vector2(0.5f, 0.5f);
        ddRt.pivot = new Vector2(0.5f, 0.5f);
        ddRt.sizeDelta = new Vector2(340f, 64f);
        ddRt.anchoredPosition = new Vector2(controlX, rowLang);
        // Keep the dropdown above the other rows in draw order.
        ddTf.SetAsLastSibling();

        if (op != null)
        {
            op.languageDropdown = dropdown;
            EditorUtility.SetDirty(op);
        }

        // Translations run 20-40% longer than English (de/ru/pl/pt-BR): let the fixed-width
        // labels shrink instead of spilling over their neighbours.
        AutoSize(options.Find("Label_Language"), 20f, 38f);
        AutoSize(options.Find("Label_Resolution"), 20f, 38f);
        AutoSize(options.Find("Label_Fullscreen"), 20f, 38f);
        AutoSize(options.Find("Text (TMP) (1)"), 20f, 38f);
        AutoSize(options.Find("Text (TMP)"), 20f, 38f);
        AutoSize(options.Find("Title"), 28f, 56f);
        if (dropdown != null && dropdown.captionText != null) AutoSize(dropdown.captionText.transform, 16f, 34f);
    }

    static void AutoSize(Transform t, float min, float max)
    {
        if (t == null) return;
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = min;
        tmp.fontSizeMax = max;
        tmp.enableWordWrapping = false;
        EditorUtility.SetDirty(tmp);
    }

    /// <summary>Restyles TMP's default dropdown to the modal look (dark plate, outline, menu font).</summary>
    static void StyleDropdown(GameObject go, TMP_FontAsset font)
    {
        var notoSans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsDir + "NotoSans SDF.asset");
        var dd = go.GetComponent<TMP_Dropdown>();
        var bg = go.GetComponent<Image>();
        bg.sprite = null;
        bg.color = new Color(0f, 0f, 0f, 0.40f);
        var ol = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        ol.effectColor = new Color(1f, 1f, 1f, 0.14f);
        ol.effectDistance = new Vector2(2f, -2f);
        dd.transition = Selectable.Transition.ColorTint;
        var cb = dd.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.selectedColor = Color.white;
        dd.colors = cb;

        // Caption
        var caption = dd.captionText;
        if (caption != null)
        {
            if (font != null) caption.font = font;
            caption.fontSize = 34f;
            caption.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            var crt = caption.rectTransform;
            crt.offsetMin = new Vector2(20f, 4f);
            crt.offsetMax = new Vector2(-50f, -4f);
            FontOnly(caption.transform);
        }
        var arrow = go.transform.Find("Arrow");
        if (arrow != null)
        {
            var art = (RectTransform)arrow;
            art.sizeDelta = new Vector2(28f, 28f);
            art.anchoredPosition = new Vector2(-24f, 0f);
            arrow.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        // Template / items
        var template = dd.template;
        if (template != null)
        {
            var tImg = template.GetComponent<Image>();
            if (tImg != null) { tImg.sprite = null; tImg.color = new Color(0.02f, 0.02f, 0.02f, 0.97f); }
            var tol = template.GetComponent<Outline>() ?? template.gameObject.AddComponent<Outline>();
            tol.effectColor = new Color(1f, 1f, 1f, 0.14f);
            tol.effectDistance = new Vector2(2f, -2f);

            const float itemH = 50f;
            int count = 10;
            template.sizeDelta = new Vector2(template.sizeDelta.x, itemH * count + 12f);

            var content = template.Find("Viewport/Content") as RectTransform;
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, itemH);
            var item = template.Find("Viewport/Content/Item") as RectTransform;
            if (item != null)
            {
                item.sizeDelta = new Vector2(item.sizeDelta.x, itemH);
                var toggle = item.GetComponent<Toggle>();
                if (toggle != null)
                {
                    var tc = toggle.colors;
                    tc.normalColor = new Color(1f, 1f, 1f, 0f);
                    tc.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
                    tc.pressedColor = new Color(1f, 1f, 1f, 0.2f);
                    tc.selectedColor = new Color(1f, 1f, 1f, 0.08f);
                    toggle.colors = tc;
                }
                var ibg = item.Find("Item Background")?.GetComponent<Image>();
                if (ibg != null) { ibg.sprite = null; ibg.color = Color.white; }
                var chk = item.Find("Item Checkmark")?.GetComponent<Image>();
                if (chk != null)
                {
                    chk.color = new Color(0.9f, 0.85f, 0.7f, 1f);
                    var cr = chk.rectTransform;
                    cr.sizeDelta = new Vector2(24f, 24f);
                    cr.anchoredPosition = new Vector2(18f, 0f);
                }
            }
            var itemLabel = dd.itemText;
            if (itemLabel != null)
            {
                // Universal font for the list: every language name is drawn with its own script.
                if (notoSans != null) itemLabel.font = notoSans;
                itemLabel.fontSize = 28f;
                itemLabel.color = new Color(0.92f, 0.92f, 0.92f, 1f);
                itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
                var lrt = itemLabel.rectTransform;
                lrt.offsetMin = new Vector2(44f, 2f);
                lrt.offsetMax = new Vector2(-10f, -2f);
            }
            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null)
            {
                var sImg = scrollbar.GetComponent<Image>();
                if (sImg != null) sImg.color = new Color(0f, 0f, 0f, 0.5f);
                var handle = scrollbar.Find("Sliding Area/Handle")?.GetComponent<Image>();
                if (handle != null) handle.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            }
        }
    }

    static void SetupDisclaimer(Transform disclaimerPanel)
    {
        if (disclaimerPanel.childCount == 0) return;
        var first = disclaimerPanel.GetChild(0);      // "Headphones Disclaimer": DisclaimerScreen fades this CanvasGroup
        if (first.Find("HeadphonesIcon") != null) return; // already converted

        var group = first.GetComponent<CanvasGroup>() ?? first.gameObject.AddComponent<CanvasGroup>();
        var oldImage = first.GetComponent<Image>();
        var iconSprite = LoadSprite("Assets/UI/MainMenu/HEADPHONES_ICON.png");
        if (oldImage != null) Object.DestroyImmediate(oldImage);
        var oldRenderer = first.GetComponent<CanvasRenderer>();
        if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);

        var frt = (RectTransform)first;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

        // Text above, icon below — matching the original baked layout.
        var sans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsDir + "NotoSans SDF.asset");
        var text = NewText("DisclaimerText", first, "It is recommended to use headphones", sans, 52f, new Color(0.9f, 0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        text.enableWordWrapping = true;
        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0.2f, 0.52f);
        trt.anchorMax = new Vector2(0.8f, 0.72f);
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        KeyText(text.transform, "ui.disclaimer.headphones");

        var iconGo = new GameObject("HeadphonesIcon", typeof(RectTransform));
        iconGo.transform.SetParent(first, false);
        var icon = iconGo.AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = new Color(0.84f, 0.84f, 0.84f, 1f);
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(196f, 167f);
        irt.anchoredPosition = new Vector2(0f, -70f);
    }

    // ------------------------------------------------------------------ gameplay scene

    [MenuItem("Tools/Graveyard/Localization/Setup/Gameplay Scene (current scene)")]
    public static void SetupGameplaySceneMenu()
    {
        SetupGameplayScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void SetupGameplayScene()
    {
        int done = 0;
        var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pause != null)
        {
            if (pause.resumeButton != null) { KeyText(pause.resumeButton.GetComponentInChildren<TMP_Text>(true)?.transform, "ui.pause.resume"); done++; }
            if (pause.quitButton != null) { KeyText(pause.quitButton.GetComponentInChildren<TMP_Text>(true)?.transform, "ui.pause.exit"); done++; }
        }
        Debug.Log("LocalizationSetup: gameplay scene localized (" + done + " labels).");
    }

    // ------------------------------------------------------------------ helpers

    static void Move(Transform parent, string child, float x, float y)
    {
        var c = parent.Find(child) as RectTransform;
        if (c != null) c.anchoredPosition = new Vector2(x, y);
    }

    static LocalizedText KeyText(Transform t, string key)
    {
        if (t == null) return null;
        var lt = t.GetComponent<LocalizedText>() ?? t.gameObject.AddComponent<LocalizedText>();
        lt.mode = LocalizedText.Mode.Key;
        lt.key = key;
        EditorUtility.SetDirty(lt);
        return lt;
    }

    static LocalizedText FontOnly(Transform t)
    {
        if (t == null) return null;
        var lt = t.GetComponent<LocalizedText>() ?? t.gameObject.AddComponent<LocalizedText>();
        lt.mode = LocalizedText.Mode.FontOnly;
        EditorUtility.SetDirty(lt);
        return lt;
    }

    /// <summary>
    /// Turns a baked-word sprite (PLAY.png…) into a LocalizedSprite: keeps the art for English,
    /// adds the matching text-free plate and a centered auto-sized label for other languages.
    /// </summary>
    static void MakeSprite(Transform t, string artName, string key, TMP_FontAsset font, float labelBottom)
    {
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img == null) return;

        var ls = t.GetComponent<LocalizedSprite>() ?? t.gameObject.AddComponent<LocalizedSprite>();
        ls.englishSprite = LoadSprite("Assets/UI/MainMenu/" + artName + ".png");
        ls.plateSprite = LoadSprite(PlatesDir + artName + "_PLATE.png");

        var labelTf = t.Find("Label");
        TextMeshProUGUI label;
        if (labelTf == null)
        {
            label = NewText("Label", t, artName, font, 72f, new Color(0.93f, 0.93f, 0.93f, 1f), TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 72f;
            label.enableWordWrapping = false;
            label.characterSpacing = 2f;
            var lrt = label.rectTransform;
            // The word sits in the upper band of the brush stroke, above the white streak.
            lrt.anchorMin = new Vector2(0.10f, labelBottom);
            lrt.anchorMax = new Vector2(0.90f, 0.88f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
        }
        else label = labelTf.GetComponent<TextMeshProUGUI>();

        label.raycastTarget = false;
        var lt = KeyText(label.transform, key);
        ls.label = label;
        EditorUtility.SetDirty(ls);
        // In the editor the label is hidden (English art); LocalizedSprite shows it at runtime when needed.
        label.gameObject.SetActive(false);
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            if (s.spriteMeshType != SpriteMeshType.FullRect) { s.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(s); changed = true; }
            if (changed) { EditorUtility.SetDirty(importer); importer.SaveAndReimport(); }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
