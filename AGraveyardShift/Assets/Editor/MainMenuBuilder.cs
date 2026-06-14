#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor tool that (re)builds the polished main menu UI for the Start Menu Scene.
/// Idempotent: it removes the previously built "MainMenuUI" (and legacy menu objects)
/// and rebuilds from the imported NewUI sprites, then wires up GameMainMenu and saves.
/// Run via Tools > Graveyard > Build Main Menu, or MainMenuBuilder.Build() from code.
/// </summary>
public static class MainMenuBuilder
{
    const string UIDir = "Assets/UI/MainMenu/";
    const string HelpMeFontPath = "Assets/Additional Models and Assets/HelpMe SDF.asset";

    // ---- Layout knobs (1920x1080 design space) ----
    const float LOGO_WIDTH = 660f;
    const float LOGO_RIGHT = 54f;
    const float LOGO_TOP = 46f;

    const float BTN_WIDTH = 360f;
    const float BTN_LEFT = 120f;
    const float BTN_BOTTOM = 90f;
    const float BTN_STEP = 138f;

    [MenuItem("Tools/Graveyard/Build Main Menu")]
    public static string Build()
    {
        AssetDatabase.Refresh();

        // 1. Import sprites with UI-friendly settings.
        Sprite logo = LoadSprite("LOGO_WHITE.png");
        Sprite vignette = LoadSprite("VIGNETTE.png");
        Sprite play = LoadSprite("PLAY.png");
        Sprite endings = LoadSprite("ENDINGS.png");
        Sprite settings = LoadSprite("SETTINGS.png");
        Sprite quit = LoadSprite("QUIT.png");
        Sprite cancel = LoadSprite("CANCEL.png");
        Sprite accept = LoadSprite("ACCEPT.png");
        Sprite modal = LoadSprite("MODAL.png");
        LoadSprite("LOGO.png"); // keep original dark logo imported/available

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HelpMeFontPath);

        // 2. Find scene references.
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("MainMenuBuilder: No 'Canvas' found."); return "ERROR: no Canvas"; }
        var canvasTf = canvas.transform;

        var menu = Object.FindFirstObjectByType<GameMainMenu>();
        if (menu == null) { Debug.LogError("MainMenuBuilder: No GameMainMenu found."); return "ERROR: no GameMainMenu"; }

        var optionsPanel = FindChild(canvasTf, "OptionsPanel");
        var fadeOverlay = FindChild(canvasTf, "FadeOverlay");
        var disclaimer = FindChild(canvasTf, "DisclaimerPanel");

        // Canvas scaler -> 1920x1080 reference.
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 3. Clean up old / previously built objects (direct children of Canvas only).
        var toKill = new List<GameObject>();
        foreach (Transform c in canvasTf)
        {
            if (c.name == "MainMenuUI" || c.name == "Panel") { toKill.Add(c.gameObject); continue; }
            var tmp = c.GetComponent<TextMeshProUGUI>();
            if (tmp != null && tmp.text != null && tmp.text.ToLower().Contains("graveyard")) { toKill.Add(c.gameObject); continue; }
            var img = c.GetComponent<Image>();
            if (img != null && img.sprite != null && img.sprite.name.ToLower().Contains("graveyard-shift")) { toKill.Add(c.gameObject); continue; }
        }
        foreach (var go in toKill) Object.DestroyImmediate(go);

        // 4. Build the new menu root.
        var root = NewRect("MainMenuUI", canvasTf);
        Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var intro = root.gameObject.AddComponent<MenuIntroFX>();

        // Vignette (mood + contrast), behind everything.
        var vig = NewImage("Vignette", root, vignette, false);
        Stretch(vig.rectTransform);
        vig.color = Color.white;

        // Logo, top-right, static (no motion).
        var logoImg = NewImage("Logo", root, logo, false);
        var logoRt = logoImg.rectTransform;
        logoRt.anchorMin = logoRt.anchorMax = new Vector2(1f, 1f);
        logoRt.pivot = new Vector2(1f, 1f);
        SizeKeepAspect(logoRt, logo, LOGO_WIDTH);
        logoRt.anchoredPosition = new Vector2(-LOGO_RIGHT, -LOGO_TOP);
        logoImg.preserveAspect = true;

        // Button column, lower-left.
        var buttons = NewRect("Buttons", root);
        buttons.anchorMin = buttons.anchorMax = new Vector2(0f, 0f);
        buttons.pivot = new Vector2(0f, 0f);
        buttons.sizeDelta = new Vector2(BTN_WIDTH, 700f);
        buttons.anchoredPosition = new Vector2(BTN_LEFT, BTN_BOTTOM);

        var playBtn = MakeButton("PlayButton", buttons, play, BTN_WIDTH, new Vector2(0f, BTN_STEP * 3f));
        var endingsBtn = MakeButton("EndingsButton", buttons, endings, BTN_WIDTH, new Vector2(0f, BTN_STEP * 2f));
        var settingsBtn = MakeButton("SettingsButton", buttons, settings, BTN_WIDTH, new Vector2(0f, BTN_STEP * 1f));
        var quitBtn = MakeButton("QuitButton", buttons, quit, BTN_WIDTH, new Vector2(0f, 0f));

        // Endings modal (hidden by default).
        var endingsPanel = BuildEndingsModal(root, modal, endings, cancel, font, menu);

        // 5. Order siblings: menu (bottom) -> options -> fade -> disclaimer (top).
        root.SetAsFirstSibling();
        if (optionsPanel != null) optionsPanel.transform.SetSiblingIndex(1);
        if (fadeOverlay != null) fadeOverlay.transform.SetAsLastSibling();
        if (disclaimer != null) disclaimer.transform.SetAsLastSibling();

        // Intro rise target: the button column (logo has its own motion).
        intro.riseTargets = new RectTransform[] { buttons };

        // Light reskin of the existing options panel so Settings matches the new style.
        ReskinOptions(optionsPanel, modal, accept, cancel, menu);

        // 6. Wire GameMainMenu references.
        menu.startButton = playBtn;
        menu.optionsButton = settingsBtn;
        menu.endingsButton = endingsBtn;
        menu.exitButton = quitBtn;
        if (optionsPanel != null) menu.optionsPanel = optionsPanel.gameObject;
        menu.endingsPanel = endingsPanel.gameObject;
        if (fadeOverlay != null) menu.fadeOverlay = fadeOverlay.GetComponent<Image>();
        EditorUtility.SetDirty(menu);

        // Safety: the headphones disclaimer must always ship enabled.
        if (disclaimer != null) disclaimer.gameObject.SetActive(true);

        // 7. Save the scene.
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("MainMenuBuilder: Main menu built and scene saved.");
        return "OK: built main menu (logo, 4 buttons, endings modal, options reskin) and saved scene.";
    }

    // ---------- Endings modal ----------
    private static RectTransform BuildEndingsModal(Transform parent, Sprite modal, Sprite header, Sprite cancel, TMP_FontAsset font, GameMainMenu menu)
    {
        var panel = NewRect("EndingsPanel", parent);
        Stretch(panel);

        // Dim backdrop blocks clicks behind the modal.
        var dim = NewImage("Dim", panel, null, true);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        // Modal frame.
        var modalImg = NewImage("Modal", panel, modal, true);
        var modalRt = modalImg.rectTransform;
        modalRt.anchorMin = modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(1416f, 864f);
        modalRt.anchoredPosition = Vector2.zero;
        var modalGroup = modalImg.gameObject.AddComponent<CanvasGroup>();
        var modalIntro = modalImg.gameObject.AddComponent<MenuIntroFX>();
        modalIntro.fadeDuration = 0.22f;
        modalIntro.startDelay = 0f;
        modalIntro.riseDistance = 26f;
        modalIntro.riseTargets = new RectTransform[] { modalRt };

        // Header (ENDINGS art).
        var head = NewImage("EndingsHeader", modalRt, header, false);
        var headRt = head.rectTransform;
        headRt.anchorMin = headRt.anchorMax = new Vector2(0.5f, 1f);
        headRt.pivot = new Vector2(0.5f, 1f);
        SizeKeepAspect(headRt, header, 380f);
        headRt.anchoredPosition = new Vector2(0f, -42f);
        head.preserveAspect = true;

        // "0 / 4 discovered" subtitle.
        var sub = NewText("Subtitle", modalRt, "0 / 4  DISCOVERED", font, 26f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center);
        sub.characterSpacing = 6f;
        var subRt = sub.rectTransform;
        subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.sizeDelta = new Vector2(700f, 40f);
        subRt.anchoredPosition = new Vector2(0f, -196f);

        // 2x2 grid of locked slots.
        var slotSize = new Vector2(486f, 150f);
        MakeSlot(modalRt, "I", new Vector2(-262f, 64f), slotSize, font);
        MakeSlot(modalRt, "II", new Vector2(262f, 64f), slotSize, font);
        MakeSlot(modalRt, "III", new Vector2(-262f, -116f), slotSize, font);
        MakeSlot(modalRt, "IV", new Vector2(262f, -116f), slotSize, font);

        // Cancel button -> close.
        var cancelBtn = MakeButton("CancelButton", modalRt, cancel, 300f, Vector2.zero);
        var cRt = cancelBtn.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0f);
        cRt.pivot = new Vector2(0.5f, 0f);
        SizeKeepAspect(cRt, cancel, 300f);
        cRt.anchoredPosition = new Vector2(0f, 34f);
        UnityEventTools.AddPersistentListener(cancelBtn.onClick, menu.CloseEndings);

        panel.gameObject.SetActive(false);
        return panel;
    }

    private static void MakeSlot(Transform parent, string numeral, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        var slot = NewRect("Slot_" + numeral, parent);
        slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.sizeDelta = size;
        slot.anchoredPosition = pos;

        var bg = slot.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.40f);
        bg.raycastTarget = false;
        var ol = slot.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(1f, 1f, 1f, 0.14f);
        ol.effectDistance = new Vector2(2f, -2f);

        var num = NewText("Num", slot, numeral, font, 30f, new Color(1f, 1f, 1f, 0.40f), TextAlignmentOptions.TopLeft);
        var nRt = num.rectTransform;
        nRt.anchorMin = nRt.anchorMax = new Vector2(0f, 1f);
        nRt.pivot = new Vector2(0f, 1f);
        nRt.sizeDelta = new Vector2(120f, 44f);
        nRt.anchoredPosition = new Vector2(18f, -10f);

        var val = NewText("Value", slot, "??????", font, 60f, new Color(0.82f, 0.82f, 0.82f, 0.92f), TextAlignmentOptions.Center);
        val.characterSpacing = 6f;
        Stretch(val.rectTransform);
    }

    // ---------- Options reskin + resolution/fullscreen controls ----------
    private static void ReskinOptions(Transform optionsPanel, Sprite modal, Sprite accept, Sprite cancel, GameMainMenu menu)
    {
        if (optionsPanel == null) return;

        var op = optionsPanel.GetComponent<OptionsPanel>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HelpMeFontPath);
        var checkOn = LoadSprite("CHECKBOX_CHECKED.png");
        var checkOff = LoadSprite("CHECKBOX_UNCHECKED.png");

        var img = optionsPanel.GetComponent<Image>();
        if (img != null) { img.sprite = modal; img.type = Image.Type.Simple; img.color = Color.white; }
        var prt = optionsPanel.GetComponent<RectTransform>();
        if (prt != null) prt.sizeDelta = new Vector2(1416f, 912f);

        // Remove old header, hide legacy close button.
        DestroyChildIfExists(optionsPanel, "SettingsHeader");
        var oldClose = optionsPanel.Find("CloseButton");
        if (oldClose != null) oldClose.gameObject.SetActive(false);

        // Clear anything we built before so re-runs stay idempotent.
        foreach (string n in new[] { "AcceptButton", "CancelButton", "Title",
            "ResolutionValue", "ResLeft", "ResRight", "FullscreenToggle",
            "Label_Resolution", "Label_Fullscreen" })
            DestroyChildIfExists(optionsPanel, n);

        const float labelX = -440f;
        const float controlX = 160f;
        const float rowRes = 160f, rowFull = 60f, rowBright = -45f, rowVol = -150f;
        Color labelCol = new Color(0.92f, 0.92f, 0.92f, 1f);

        // Text title (the removed element was the big logo image; a small title is fine).
        var title = NewText("Title", optionsPanel, "SETTINGS", font, 56f, new Color(0.96f, 0.96f, 0.96f, 1f), TextAlignmentOptions.Center);
        var tRt = title.rectTransform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(600f, 80f);
        tRt.anchoredPosition = new Vector2(0f, -26f);

        // Row labels.
        MakeRowLabel(optionsPanel, "Label_Resolution", "Resolution", font, labelX, rowRes, labelCol);
        MakeRowLabel(optionsPanel, "Label_Fullscreen", "Fullscreen", font, labelX, rowFull, labelCol);

        // Resolution cycler:  <   1920 x 1080   >
        var resVal = NewText("ResolutionValue", optionsPanel, "1920 x 1080", font, 36f, new Color(0.85f, 0.85f, 0.85f, 1f), TextAlignmentOptions.Center);
        var rvRt = resVal.rectTransform;
        rvRt.anchorMin = rvRt.anchorMax = new Vector2(0.5f, 0.5f);
        rvRt.pivot = new Vector2(0.5f, 0.5f);
        rvRt.sizeDelta = new Vector2(280f, 60f);
        rvRt.anchoredPosition = new Vector2(controlX, rowRes);
        var resLeft = MakeArrowButton("ResLeft", optionsPanel, "<", font, new Vector2(controlX - 185f, rowRes));
        var resRight = MakeArrowButton("ResRight", optionsPanel, ">", font, new Vector2(controlX + 185f, rowRes));

        // Fullscreen checkbox (Unity Toggle using the two checkbox sprites).
        var toggleRt = NewRect("FullscreenToggle", optionsPanel);
        toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRt.pivot = new Vector2(0.5f, 0.5f);
        toggleRt.sizeDelta = new Vector2(74f, 74f);
        toggleRt.anchoredPosition = new Vector2(controlX, rowFull);
        var boxImg = toggleRt.gameObject.AddComponent<Image>();
        boxImg.sprite = checkOff;
        boxImg.preserveAspect = true;
        var toggle = toggleRt.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = boxImg;
        var checkRt = NewRect("Checkmark", toggleRt);
        Stretch(checkRt);
        var checkImg = checkRt.gameObject.AddComponent<Image>();
        checkImg.sprite = checkOn;
        checkImg.preserveAspect = true;
        checkImg.raycastTarget = false;
        toggle.graphic = checkImg;

        // ACCEPT (saves + closes) right, CANCEL (closes) left.
        var acceptBtn = MakeButton("AcceptButton", optionsPanel, accept, 260f, Vector2.zero);
        var aRt = acceptBtn.GetComponent<RectTransform>();
        aRt.anchorMin = aRt.anchorMax = new Vector2(0.5f, 0f);
        aRt.pivot = new Vector2(0.5f, 0f);
        SizeKeepAspect(aRt, accept, 260f);
        aRt.anchoredPosition = new Vector2(180f, 36f);
        if (op != null) UnityEventTools.AddPersistentListener(acceptBtn.onClick, op.SaveAndClose);

        var cancelBtn = MakeButton("CancelButton", optionsPanel, cancel, 260f, Vector2.zero);
        var cRt = cancelBtn.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0f);
        cRt.pivot = new Vector2(0.5f, 0f);
        SizeKeepAspect(cRt, cancel, 260f);
        cRt.anchoredPosition = new Vector2(-180f, 36f);
        UnityEventTools.AddPersistentListener(cancelBtn.onClick, menu.CloseOptions);

        // Reflow + restyle the existing Volume / Brightness sliders into the row layout.
        StyleSliderRow(optionsPanel, "Slider", "Text (TMP)", "Volume", font, labelX, controlX, rowVol, labelCol);
        StyleSliderRow(optionsPanel, "Brightness Slider", "Text (TMP) (1)", "Brightness", font, labelX, controlX, rowBright, labelCol);

        // Wire OptionsPanel references.
        if (op != null)
        {
            op.fullscreenToggle = toggle;
            op.resolutionLabel = resVal;
            op.resolutionLeftButton = resLeft;
            op.resolutionRightButton = resRight;
            var vs = optionsPanel.Find("Slider");
            if (vs != null) op.masterVolumeSlider = vs.GetComponent<Slider>();
            var bs = optionsPanel.Find("Brightness Slider");
            if (bs != null) op.brightnessSlider = bs.GetComponent<Slider>();
            EditorUtility.SetDirty(op);
        }
    }

    private static void DestroyChildIfExists(Transform parent, string name)
    {
        var c = parent.Find(name);
        if (c != null) Object.DestroyImmediate(c.gameObject);
    }

    private static TextMeshProUGUI MakeRowLabel(Transform parent, string name, string text, TMP_FontAsset font, float x, float y, Color col)
    {
        var t = NewText(name, parent, text, font, 38f, col, TextAlignmentOptions.MidlineLeft);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 60f);
        rt.anchoredPosition = new Vector2(x, y);
        return t;
    }

    private static Button MakeArrowButton(string name, Transform parent, string glyph, TMP_FontAsset font, Vector2 pos)
    {
        var t = NewText(name, parent, glyph, font, 52f, new Color(0.78f, 0.78f, 0.78f, 1f), TextAlignmentOptions.Center);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(72f, 72f);
        rt.anchoredPosition = pos;
        t.raycastTarget = true;
        var btn = t.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = t;
        t.gameObject.AddComponent<MenuButtonFX>();
        return btn;
    }

    private static void StyleSliderRow(Transform parent, string sliderName, string labelName, string labelText, TMP_FontAsset font, float labelX, float controlX, float y, Color col)
    {
        var slider = parent.Find(sliderName);
        if (slider != null)
        {
            var rt = slider.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(340f, 26f);
            rt.anchoredPosition = new Vector2(controlX, y);
        }
        var label = parent.Find(labelName);
        if (label != null)
        {
            var lt = label.GetComponent<TextMeshProUGUI>();
            if (lt != null)
            {
                if (font != null) lt.font = font;
                lt.text = labelText;
                lt.fontSize = 38f;
                lt.color = col;
                lt.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 60f);
            rt.anchoredPosition = new Vector2(labelX, y);
        }
    }

    // ---------- Helpers ----------
    private static Sprite LoadSprite(string file)
    {
        string path = UIDir + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            if (s.spriteMeshType != SpriteMeshType.FullRect) { s.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(s); changed = true; }
            if (changed) { EditorUtility.SetDirty(importer); importer.SaveAndReimport(); }
        }
        else
        {
            Debug.LogWarning("MainMenuBuilder: no importer found for " + path);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
            if (c.name == name) return c;
        return null;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SizeKeepAspect(RectTransform rt, Sprite sp, float width)
    {
        float h = (sp != null && sp.rect.width > 0f) ? width * sp.rect.height / sp.rect.width : width;
        rt.sizeDelta = new Vector2(width, h);
    }

    private static Image NewImage(string name, Transform parent, Sprite sp, bool raycast)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static Button MakeButton(string name, Transform parent, Sprite sp, float width, Vector2 pos)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp;
        img.preserveAspect = true;
        img.raycastTarget = true;
        SizeKeepAspect(rt, sp, width);
        rt.anchoredPosition = pos;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; // MenuButtonFX owns the visuals
        btn.targetGraphic = img;

        rt.gameObject.AddComponent<MenuButtonFX>();
        return btn;
    }
}
#endif
