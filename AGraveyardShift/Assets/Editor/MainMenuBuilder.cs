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
/// Editor tool that (re)builds the main menu UI for the Start Menu Scene in the Epitaph style:
/// the carved Epitaph logo over the front gate (MenuBackdropBuilder), a column of plain Cinzel
/// labels that warm and grow thin rules on hover, the endings as five stone tablets, and every
/// pop-up on a dark stone slab with an incised border. Also builds the Settings screen as a prefab
/// (Assets/Prefabs/UI/SettingsPanel.prefab) used here and by the pause menu (PauseMenuBuilder),
/// and the launch splash. At runtime the whole canvas is drawn on a low-res pixel grid (PixelCanvas).
/// Idempotent: rebuilds MainMenuUI / the settings panel / the splash and saves the scene.
/// Run via Tools > Graveyard > Build Main Menu.
/// </summary>
public static class MainMenuBuilder
{
    const string EpitaphDir = "Assets/UI/Epitaph/";
    // The cabin notes' paper + ink, reused so the Field Notes archive looks like the real notes.
    const string NotePaperPath = "Assets/Additional Models and Assets/photo-1603484477859-abe6a73f9366.jpg";
    const string NoteFontPath = "Assets/Additional Models and Assets/CaslonAntique SDF.asset";
    public const string SettingsPrefabPath = "Assets/Prefabs/UI/SettingsPanel.prefab";

    // ---- Look (1920x1080 design space)
    static readonly Color Bone = new Color(0.87f, 0.84f, 0.78f, 1f);
    static readonly Color Muted = new Color(0.87f, 0.84f, 0.78f, 0.58f);
    static readonly Color Warm = new Color(1f, 0.89f, 0.70f, 1f);
    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);

    // Logo + menu column share one centre line, left of the gate.
    const float COLUMN_X = 470f;
    const float LOGO_WIDTH = 700f;
    const float LOGO_TOP = 150f;
    const float MENU_TOP = 548f;
    const float ITEM_HEIGHT = 78f;
    const float ITEM_SIZE = 42f;
    const float RETURN_HEIGHT = 118f;

    static TMP_FontAsset cinzel, cinzelBold, cormorant;
    static Material carvedMaterial, sweepMaterial;
    static Sprite slab, rule, glow, white, diamond, checkBox, checkTick, shade, logo, tablet, tabletWorn;

    [MenuItem("Tools/Graveyard/Build Main Menu")]
    public static string Build()
    {
        AssetDatabase.Refresh();
        if (!LoadAssets()) return "ERROR: Epitaph assets missing (run Tools > Graveyard > Menu > Create Epitaph Assets)";

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("MainMenuBuilder: No 'Canvas' found."); return "ERROR: no Canvas"; }
        var canvasTf = canvas.transform;
        var menu = Object.FindFirstObjectByType<GameMainMenu>();
        if (menu == null) { Debug.LogError("MainMenuBuilder: No GameMainMenu found."); return "ERROR: no GameMainMenu"; }

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Text and art on one low-res pixel grid to match the PSX backdrop (applied at runtime).
        var pixel = canvas.GetComponent<PixelCanvas>();
        if (pixel == null) pixel = canvas.AddComponent<PixelCanvas>();
        var pixelSo = new SerializedObject(pixel);
        pixelSo.FindProperty("textShader").objectReferenceValue = Shader.Find(PixelCanvas.TextShaderName);
        pixelSo.FindProperty("imageShader").objectReferenceValue = Shader.Find(PixelCanvas.ImageShaderName);
        pixelSo.ApplyModifiedPropertiesWithoutUndo();

        // ---- clean up previous builds (direct children of Canvas only)
        foreach (string n in new[] { "MainMenuUI", "OptionsPanel", "SettingsPanel", "Panel" })
        {
            var old = FindChild(canvasTf, n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // ---- settings (prefab, shared with the pause menu)
        var settingsPrefab = BuildSettingsPrefab();
        var settings = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, canvas.scene);
        settings.transform.SetParent(canvasTf, false);
        settings.name = "SettingsPanel";
        settings.SetActive(false);

        // ---- main menu root
        var root = NewRect("MainMenuUI", canvasTf);
        Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var intro = root.gameObject.AddComponent<MenuIntroFX>();

        var shadeImg = NewImage("Shade", root, shade, false);
        Stretch(shadeImg.rectTransform);

        var logoImg = NewImage("Logo", root, logo, false);
        var logoRt = logoImg.rectTransform;
        logoRt.anchorMin = logoRt.anchorMax = new Vector2(0f, 1f);
        logoRt.pivot = new Vector2(0.5f, 1f);
        SizeKeepAspect(logoRt, logo, LOGO_WIDTH);
        logoRt.anchoredPosition = new Vector2(COLUMN_X, -LOGO_TOP);
        logoImg.preserveAspect = true;

        // Menu column: Return (only after a death) / Play / Endings / Settings / Quit.
        var column = NewRect("Buttons", root);
        column.anchorMin = column.anchorMax = new Vector2(0f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        column.sizeDelta = new Vector2(760f, 520f);
        column.anchoredPosition = new Vector2(COLUMN_X, -MENU_TOP);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 0f;

        TextMeshProUGUI returnLabel, returnCause;
        var returnBtn = MakeReturnButton(column, out returnLabel, out returnCause);
        TextMeshProUGUI playLabel, l2, l3, l4;
        var playBtn = MenuItem(column, "PlayButton", "PLAY", "ui.menu.play", out playLabel);
        var endingsBtn = MenuItem(column, "EndingsButton", "ENDINGS", "ui.menu.endings", out l2);
        var settingsBtn = MenuItem(column, "SettingsButton", "SETTINGS", "ui.menu.settings", out l3);
        var quitBtn = MenuItem(column, "QuitButton", "QUIT", "ui.menu.quit", out l4);
        intro.riseTargets = new RectTransform[] { column };

        // ---- pop-ups
        var endingsPanel = BuildEndingsPanel(root, menu);
        var cryptFiendsPanel = BuildCryptFiendsModal(root, menu);
        var fieldNotesPanel = BuildFieldNotesModal(root, menu);
        var phaseSelectPanel = BuildPhaseSelectModal(root, menu);

        // ---- launch splash (replaces the headphones card)
        BuildSplash(canvasTf);

        // ---- order: menu -> settings -> fade -> splash on top
        root.SetAsFirstSibling();
        settings.transform.SetSiblingIndex(1);
        var fade = FindChild(canvasTf, "FadeOverlay");
        var splash = FindChild(canvasTf, "DisclaimerPanel");
        if (fade != null) fade.SetAsLastSibling();
        if (splash != null) { splash.SetAsLastSibling(); splash.gameObject.SetActive(true); }

        // ---- wire GameMainMenu
        menu.startButton = playBtn;
        menu.optionsButton = settingsBtn;
        menu.endingsButton = endingsBtn;
        menu.exitButton = quitBtn;
        menu.returnButton = returnBtn;
        menu.returnLabel = returnLabel;
        menu.returnCauseLabel = returnCause;
        menu.playLabel = playLabel;
        menu.optionsPanel = settings;
        menu.endingsPanel = endingsPanel.gameObject;
        menu.cryptFiendsPanel = cryptFiendsPanel.gameObject;
        menu.fieldNotesPanel = fieldNotesPanel.gameObject;
        menu.phaseSelectPanel = phaseSelectPanel.gameObject;
        menu.menuGroup = group;
        menu.menuIntro = intro;
        if (fade != null) menu.fadeOverlay = fade.GetComponent<Image>();
        EditorUtility.SetDirty(menu);

        // Gate creak when hovering Play (MenuAmbience lives on the backdrop).
        var ambience = Object.FindFirstObjectByType<MenuAmbience>();
        if (ambience != null)
        {
            ambience.creakOnHover = playBtn.GetComponent<EpitaphButtonFX>();
            EditorUtility.SetDirty(ambience);
        }

        // Menu music goes to the Music mixer group (Settings > Music); it shipped wired to Master.
        var music = Object.FindFirstObjectByType<BackgroundMusic>();
        if (music != null)
        {
            if (music.GetComponent<MusicSource>() == null) music.gameObject.AddComponent<MusicSource>();
            var source = music.GetComponent<AudioSource>();
            var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Resources/Audio/MainMixer.mixer");
            if (source != null && mixer != null)
                foreach (var g in mixer.FindMatchingGroups("Music"))
                    if (g.name == "Music") { source.outputAudioMixerGroup = g; EditorUtility.SetDirty(source); }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("MainMenuBuilder: Epitaph main menu built and scene saved.");
        return "OK: built the Epitaph main menu (column, endings tablets, crypt fiends, field notes, phase select, settings prefab, splash) and saved the scene.";
    }

    // ------------------------------------------------------------------ main column
    static Button MenuItem(Transform column, string name, string label, string key, out TextMeshProUGUI text)
    {
        var btn = TextButton(name, column, label, cinzel, ITEM_SIZE, new Vector2(700f, ITEM_HEIGHT), out text);
        Bind(text, key);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = ITEM_HEIGHT;
        return btn;
    }

    static Button MakeReturnButton(Transform column, out TextMeshProUGUI label, out TextMeshProUGUI cause)
    {
        var btn = TextButton("ReturnButton", column, "RETURN TO THE SECOND WATCH", cinzel, 33f, new Vector2(740f, RETURN_HEIGHT), out label);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0.42f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.characterSpacing = 6f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 33f;
        FontOnly(label);
        // Rules sit on the label line, not the middle of the taller button.
        var fx = btn.GetComponent<EpitaphButtonFX>();
        foreach (var r in new[] { fx.ruleLeft, fx.ruleRight }) r.anchoredPosition = new Vector2(0f, RETURN_HEIGHT * 0.21f);
        fx.glow.rectTransform.anchoredPosition = new Vector2(0f, RETURN_HEIGHT * 0.21f);
        fx.ruleLength = 70f;

        cause = NewText("Cause", btn.transform, "Taken by the Grave Robber", cormorant, 27f, new Color(Bone.r, Bone.g, Bone.b, 0.72f), TextAlignmentOptions.Center);
        var crt = cause.rectTransform;
        crt.anchorMin = new Vector2(0f, 0.04f);
        crt.anchorMax = new Vector2(1f, 0.42f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        FontOnly(cause);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RETURN_HEIGHT;
        btn.gameObject.SetActive(false);
        return btn;
    }

    // ------------------------------------------------------------------ endings (tablets)
    static RectTransform BuildEndingsPanel(Transform parent, GameMainMenu menu)
    {
        var panel = NewRect("EndingsPanel", parent);
        Stretch(panel);
        var dim = NewImage("Dim", panel, null, true);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.93f);

        var content = NewRect("Content", panel);
        Stretch(content);
        content.gameObject.AddComponent<CanvasGroup>();
        var cIntro = content.gameObject.AddComponent<MenuIntroFX>();
        cIntro.fadeDuration = 0.25f; cIntro.startDelay = 0f; cIntro.riseDistance = 22f;
        cIntro.riseTargets = new RectTransform[] { content };

        var title = NewText("Title", content, "ENDINGS", cinzelBold, 50f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 14f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 70f));
        Bind(title, "ui.menu.endings");

        var sub = NewText("Subtitle", content, "0 / 5  DISCOVERED", cinzel, 20f, Muted, TextAlignmentOptions.Center);
        sub.characterSpacing = 8f;
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(900f, 34f));
        FontOnly(sub);

        var disp = panel.gameObject.AddComponent<EndingsMenuDisplay>();
        int n = EndingInfo.InOrder.Length;
        disp.tablets = new Image[n];
        disp.numerals = new TextMeshProUGUI[n];
        disp.kinds = new TextMeshProUGUI[n];
        disp.titles = new TextMeshProUGUI[n];
        disp.dates = new TextMeshProUGUI[n];
        const float tw = 300f, th = 488f, gap = 38f;
        float x0 = -((n - 1) * (tw + gap)) * 0.5f;
        var carvedColor = new Color(0.14f, 0.13f, 0.12f, 1f);
        for (int i = 0; i < n; i++)
        {
            var info = EndingInfo.Get(EndingInfo.InOrder[i]);
            var tab = NewImage("Tablet_" + info.roman, content, tabletWorn, true);
            Place(tab.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (tw + gap), 26f), new Vector2(tw, th));
            var hover = tab.gameObject.AddComponent<EndingTabletHover>();
            hover.display = disp;
            hover.index = i;

            var num = Carved("Numeral", tab.transform, info.roman, cinzelBold, 92f, new Vector2(0f, 128f), new Vector2(260f, 120f), carvedColor);
            var kind = Carved("Kind", tab.transform, "", cinzel, 17f, new Vector2(0f, 42f), new Vector2(250f, 30f), carvedColor);
            kind.characterSpacing = 5f;
            var ttl = Carved("Title", tab.transform, "", cinzelBold, 29f, new Vector2(0f, -36f), new Vector2(252f, 104f), carvedColor);
            ttl.enableWordWrapping = true;
            ttl.enableAutoSizing = true; ttl.fontSizeMin = 16f; ttl.fontSizeMax = 29f;
            ttl.lineSpacing = -6f;
            var date = Carved("Date", tab.transform, "", cinzel, 17f, new Vector2(0f, -176f), new Vector2(250f, 30f), carvedColor);
            date.characterSpacing = 3f;
            date.enableAutoSizing = true; date.fontSizeMin = 11f; date.fontSizeMax = 17f;

            disp.tablets[i] = tab;
            disp.numerals[i] = num;
            disp.kinds[i] = kind;
            disp.titles[i] = ttl;
            disp.dates[i] = date;
        }
        disp.foundSprite = tablet;
        disp.wornSprite = tabletWorn;
        disp.carvedFont = cinzel;
        disp.carvedMaterial = carvedMaterial;

        var desc = NewText("Description", content, "", cinzel, 22f, Warm, TextAlignmentOptions.Center);
        desc.characterSpacing = 4f;
        desc.enableAutoSizing = true; desc.fontSizeMin = 14f; desc.fontSizeMax = 22f;
        desc.fontStyle = FontStyles.UpperCase;
        Place(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -258f), new Vector2(1500f, 40f));
        FontOnly(desc);
        disp.description = desc;

        TextMeshProUGUI fiendsLabel, notesLabel;
        var fiendsBtn = TextButton("CryptFiendsTeaser", content, "0 / 3  ?????", cinzel, 26f, new Vector2(620f, 64f), out fiendsLabel);
        Place((RectTransform)fiendsBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(-330f, -328f), new Vector2(620f, 64f));
        fiendsLabel.characterSpacing = 4f;
        fiendsLabel.enableAutoSizing = true; fiendsLabel.fontSizeMin = 14f; fiendsLabel.fontSizeMax = 26f;
        fiendsBtn.GetComponent<EpitaphButtonFX>().ruleLength = 60f;
        FontOnly(fiendsLabel);
        UnityEventTools.AddPersistentListener(fiendsBtn.onClick, menu.OpenCryptFiends);

        var notesBtn = TextButton("FieldNotesTeaser", content, "0 / 6  FIELD NOTES", cinzel, 26f, new Vector2(620f, 64f), out notesLabel);
        Place((RectTransform)notesBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(330f, -328f), new Vector2(620f, 64f));
        notesLabel.characterSpacing = 4f;
        notesLabel.enableAutoSizing = true; notesLabel.fontSizeMin = 14f; notesLabel.fontSizeMax = 26f;
        notesBtn.GetComponent<EpitaphButtonFX>().ruleLength = 60f;
        FontOnly(notesLabel);
        UnityEventTools.AddPersistentListener(notesBtn.onClick, menu.OpenFieldNotes);

        disp.subtitle = sub;
        disp.cryptFiendsTeaser = fiendsLabel;
        disp.fieldNotesTeaser = notesLabel;

        var back = BackButton(content, new Vector2(0f, -432f), menu.CloseEndings, new Vector2(0.5f, 0.5f));
        panel.gameObject.SetActive(false);
        return panel;
    }

    static TextMeshProUGUI Carved(string name, Transform parent, string text, TMP_FontAsset font, float size, Vector2 pos, Vector2 box, Color color)
    {
        var t = NewText(name, parent, text, font, size, color, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.UpperCase;
        if (font == cinzel && carvedMaterial != null) t.fontSharedMaterial = carvedMaterial;
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), pos, box);
        FontOnly(t);
        return t;
    }

    // ------------------------------------------------------------------ crypt fiends
    static RectTransform BuildCryptFiendsModal(Transform parent, GameMainMenu menu)
    {
        RectTransform modal;
        var panel = Modal("CryptFiendsPanel", parent, new Vector2(1416f, 864f), out modal);

        var title = NewText("CryptFiendsHeader", modal, "CRYPT FIENDS", cinzelBold, 50f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 12f;
        title.enableAutoSizing = true; title.fontSizeMin = 26f; title.fontSizeMax = 50f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 70f));
        FontOnly(title);

        var sub = NewText("Subtitle", modal, "0 / 3  FOUND", cinzel, 20f, Muted, TextAlignmentOptions.Center);
        sub.characterSpacing = 8f;
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(800f, 34f));
        FontOnly(sub);

        var names = new TextMeshProUGUI[3];
        var buttons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI t;
            var b = TextButton("FiendSlot", modal, "???", cinzel, 46f, new Vector2(900f, 96f), out t);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f - 130f * i), new Vector2(900f, 96f));
            t.characterSpacing = 8f;
            t.enableAutoSizing = true; t.fontSizeMin = 24f; t.fontSizeMax = 46f;
            FontOnly(t);
            names[i] = t;
            buttons[i] = b;
        }

        BackButton(modal, new Vector2(0f, 62f), menu.CloseCryptFiends, new Vector2(0.5f, 0f));

        // Detail view: shown when an unlocked fiend is clicked.
        var detail = NewImage("FiendDetail", modal, slab, true);
        detail.type = Image.Type.Sliced;
        Place(detail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1240f, 640f));
        var dName = NewText("DetailName", detail.transform, "", cinzelBold, 56f, Bone, TextAlignmentOptions.Top);
        dName.characterSpacing = 10f;
        dName.enableAutoSizing = true; dName.fontSizeMin = 28f; dName.fontSizeMax = 56f;
        Place(dName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1060f, 84f));
        FontOnly(dName);
        var dBody = NewText("DetailBody", detail.transform, "", cormorant, 36f, new Color(Bone.r, Bone.g, Bone.b, 0.92f), TextAlignmentOptions.Top);
        dBody.enableWordWrapping = true;
        dBody.enableAutoSizing = true; dBody.fontSizeMin = 22f; dBody.fontSizeMax = 36f;
        dBody.lineSpacing = 4f;
        Place(dBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1020f, 320f));
        FontOnly(dBody);
        var dBack = BackButton(detail.transform, new Vector2(0f, 54f), null, new Vector2(0.5f, 0f));
        detail.gameObject.SetActive(false);

        var disp = panel.gameObject.AddComponent<CryptFiendsMenuDisplay>();
        disp.slotNames = names;
        disp.slotButtons = buttons;
        disp.subtitle = sub;
        disp.header = title;
        disp.detailPanel = detail.gameObject;
        disp.detailName = dName;
        disp.detailBody = dBody;
        disp.detailBackButton = dBack;
        panel.gameObject.SetActive(false);
        return panel;
    }

    // ------------------------------------------------------------------ field notes
    static RectTransform BuildFieldNotesModal(Transform parent, GameMainMenu menu)
    {
        RectTransform modal;
        var panel = Modal("FieldNotesPanel", parent, new Vector2(1500f, 1010f), out modal);

        var title = NewText("FieldNotesHeader", modal, "FIELD NOTES", cinzelBold, 50f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 12f;
        title.enableAutoSizing = true; title.fontSizeMin = 26f; title.fontSizeMax = 50f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 70f));
        FontOnly(title);

        var sub = NewText("Subtitle", modal, "0 / 6  RECOVERED", cinzel, 20f, Muted, TextAlignmentOptions.Center);
        sub.characterSpacing = 8f;
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(800f, 34f));
        FontOnly(sub);

        var archive = FieldNoteArchive.Load();
        int count = archive != null && archive.entries != null ? archive.entries.Length : 6;
        var names = new TextMeshProUGUI[count];
        var buttons = new Button[count];
        for (int i = 0; i < count; i++)
        {
            TextMeshProUGUI t;
            var b = TextButton("FiendSlot", modal, "???", cinzel, 34f, new Vector2(620f, 96f), out t);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(i % 2 == 0 ? -330f : 330f, 160f - 150f * (i / 2)), new Vector2(620f, 96f));
            t.characterSpacing = 5f;
            t.enableAutoSizing = true; t.fontSizeMin = 18f; t.fontSizeMax = 34f;
            b.GetComponent<EpitaphButtonFX>().ruleLength = 54f;
            FontOnly(t);
            names[i] = t;
            buttons[i] = b;
        }

        BackButton(modal, new Vector2(0f, 62f), menu.CloseFieldNotes, new Vector2(0.5f, 0f));

        // Detail: the note itself on the cabin's paper, paged.
        var detail = NewImage("NoteDetail", modal, slab, true);
        detail.type = Image.Type.Sliced;
        Place(detail.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 930f));
        var paperSprite = LoadSpriteAt(NotePaperPath);
        var paper = NewImage("Paper", detail.transform, paperSprite, false);
        Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(570f, 760f));
        var noteFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NoteFontPath);
        var body = NewText("DetailBody", paper.transform, "", noteFont, 26f, Color.black, TextAlignmentOptions.TopLeft);
        body.enableWordWrapping = true;
        body.enableAutoSizing = true; body.fontSizeMin = 16f; body.fontSizeMax = 26f;
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(44f, 44f); bodyRt.offsetMax = new Vector2(-44f, -44f);
        FontOnly(body);

        var prev = ArrowButton("PrevButton", detail.transform, "‹", new Vector2(-400f, 50f));
        var next = ArrowButton("NextButton", detail.transform, "›", new Vector2(400f, 50f));
        var page = NewText("DetailPage", detail.transform, "Page 1/2", cinzel, 22f, Muted, TextAlignmentOptions.Center);
        page.characterSpacing = 6f;
        Place(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -360f), new Vector2(400f, 40f));
        FontOnly(page);
        var back = BackButton(detail.transform, new Vector2(0f, 26f), null, new Vector2(0.5f, 0f));
        detail.gameObject.SetActive(false);

        var disp = panel.gameObject.AddComponent<FieldNotesMenuDisplay>();
        disp.slotNames = names;
        disp.slotButtons = buttons;
        disp.subtitle = sub;
        disp.header = title;
        disp.detailPanel = detail.gameObject;
        disp.detailBody = body;
        disp.detailPage = page;
        disp.detailPrevButton = prev;
        disp.detailNextButton = next;
        disp.detailBackButton = back;
        panel.gameObject.SetActive(false);
        return panel;
    }

    // ------------------------------------------------------------------ phase select
    static readonly string[] PhaseKeys = { "ui.phase.first", "ui.phase.second", "ui.phase.third" };
    static readonly string[] PhaseNames = { "FIRST WATCH", "SECOND WATCH", "THIRD WATCH" };
    static readonly string[] PhaseDescs = { "The whole night.", "Skip the first three visitors.", "Skip ahead to the last visitors of the night." };

    static RectTransform BuildPhaseSelectModal(Transform parent, GameMainMenu menu)
    {
        RectTransform modal;
        var panel = Modal("PhaseSelectPanel", parent, new Vector2(1416f, 900f), out modal);

        var title = NewText("Title", modal, "BEGIN YOUR SHIFT", cinzelBold, 50f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 12f;
        title.enableAutoSizing = true; title.fontSizeMin = 26f; title.fontSizeMax = 50f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1100f, 70f));
        Bind(title, "ui.phase.title");

        var sub = NewText("Subtitle", modal, "Starting later skips the earlier visitors. Your stats start fresh.", cormorant, 28f, new Color(Bone.r, Bone.g, Bone.b, 0.7f), TextAlignmentOptions.Center);
        sub.enableWordWrapping = false;
        sub.enableAutoSizing = true; sub.fontSizeMin = 16f; sub.fontSizeMax = 28f;
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1120f, 44f));
        Bind(sub, "ui.phase.subtitle");

        var disp = panel.gameObject.AddComponent<PhaseSelectDisplay>();
        disp.slotButtons = new Button[3];
        disp.slotDescs = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var slot = NewRect("PhaseSlot_" + (i + 1), modal);
            Place(slot, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f - 170f * i), new Vector2(980f, 150f));
            TextMeshProUGUI nameText;
            var b = TextButton("Name", slot, PhaseNames[i], cinzel, 46f, new Vector2(980f, 96f), out nameText);
            Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(980f, 96f));
            nameText.characterSpacing = 10f;
            nameText.enableAutoSizing = true; nameText.fontSizeMin = 24f; nameText.fontSizeMax = 46f;
            Bind(nameText, PhaseKeys[i]);
            UnityEventTools.AddIntPersistentListener(b.onClick, menu.StartAtPhase, i + 1);

            var d = NewText("Desc", slot, PhaseDescs[i], cormorant, 27f, new Color(Bone.r, Bone.g, Bone.b, 0.66f), TextAlignmentOptions.Center);
            d.enableAutoSizing = true; d.fontSizeMin = 16f; d.fontSizeMax = 27f;
            Place(d.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(920f, 40f));
            FontOnly(d);   // PhaseSelectDisplay writes it (the locked line differs)
            disp.slotButtons[i] = b;
            disp.slotDescs[i] = d;
        }

        BackButton(modal, new Vector2(0f, 60f), menu.ClosePhaseSelect, new Vector2(0.5f, 0f));
        panel.gameObject.SetActive(false);
        return panel;
    }

    // ------------------------------------------------------------------ settings (prefab)
    static GameObject BuildSettingsPrefab()
    {
        var root = new GameObject("SettingsPanel", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        Stretch(rt);
        var op = root.AddComponent<OptionsPanel>();

        var dim = NewImage("Dim", rt, null, true);
        Stretch(dim.rectTransform);
        dim.color = DimColor;

        var modal = NewImage("Modal", rt, slab, true);
        modal.type = Image.Type.Sliced;
        Place(modal.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 960f));
        modal.gameObject.AddComponent<CanvasGroup>();
        var mIntro = modal.gameObject.AddComponent<MenuIntroFX>();
        mIntro.fadeDuration = 0.22f; mIntro.startDelay = 0f; mIntro.riseDistance = 22f;
        mIntro.riseTargets = new RectTransform[] { modal.rectTransform };
        var m = modal.rectTransform;

        var title = NewText("Title", m, "SETTINGS", cinzelBold, 48f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 12f;
        title.enableAutoSizing = true; title.fontSizeMin = 26f; title.fontSizeMax = 48f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(900f, 66f));
        Bind(title, "ui.settings.title");

        float y = 300f;
        const float step = 66f;
        op.mouseSensitivitySlider = SliderRow(m, "Row_MouseSensitivity", "MOUSE SENSITIVITY", "ui.settings.mouseSensitivity", y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, false, true); y -= step;
        op.invertLookToggle = ToggleRow(m, "Row_InvertLook", "INVERT LOOK", "ui.settings.invertLook", y, true); y -= step;
        op.masterVolumeSlider = SliderRow(m, "Row_MasterVolume", "MASTER VOLUME", "ui.settings.masterVolume", y, 0f, 1f, true, false); y -= step;
        op.musicVolumeSlider = SliderRow(m, "Row_Music", "MUSIC", "ui.settings.music", y, 0f, 1f, true, true); y -= step;
        op.sfxVolumeSlider = SliderRow(m, "Row_Effects", "EFFECTS", "ui.settings.effects", y, 0f, 1f, true, true); y -= step;
        op.brightnessSlider = SliderRow(m, "Row_Brightness", "BRIGHTNESS", "ui.settings.brightness", y, 0f, 1f, true, false);
        // The caution under brightness (OptionsPanel skips creating its own when this exists).
        var note = NewText("BrightnessDisclaimer", op.brightnessSlider.transform.parent, "Default brightness is strongly recommended.", cormorant, 22f, new Color(0.86f, 0.78f, 0.5f, 0.9f), TextAlignmentOptions.Center);
        Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(900f, 28f));
        note.fontStyle = FontStyles.Italic;
        Bind(note, "ui.settings.brightnessNote");
        y -= step + 18f;

        Button resLeft, resRight;
        op.resolutionLabel = CyclerRow(m, "Row_Resolution", "RESOLUTION", "ui.settings.resolution", y, "1920 x 1080", out resLeft, out resRight); y -= step;
        op.resolutionLeftButton = resLeft;
        op.resolutionRightButton = resRight;
        op.fullscreenToggle = ToggleRow(m, "Row_Fullscreen", "FULLSCREEN", "ui.settings.fullscreen", y, false); y -= step;
        Button langLeft, langRight;
        var langLabel = CyclerRow(m, "Row_Language", "LANGUAGE", "ui.settings.language", y, "English", out langLeft, out langRight);
        var cycler = langLabel.transform.parent.gameObject.AddComponent<LanguageCycler>();
        cycler.label = langLabel;
        cycler.previousButton = langLeft;
        cycler.nextButton = langRight;

        TextMeshProUGUI accLabel, canLabel;
        var accept = TextButton("AcceptButton", m, "ACCEPT", cinzel, 34f, new Vector2(300f, 70f), out accLabel);
        Place((RectTransform)accept.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 70f), new Vector2(300f, 70f));
        accept.GetComponent<EpitaphButtonFX>().ruleLength = 56f;
        Bind(accLabel, "ui.menu.accept");
        UnityEventTools.AddPersistentListener(accept.onClick, op.SaveAndClose);
        var cancel = TextButton("CancelButton", m, "CANCEL", cinzel, 34f, new Vector2(300f, 70f), out canLabel);
        Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(170f, 70f), new Vector2(300f, 70f));
        cancel.GetComponent<EpitaphButtonFX>().ruleLength = 56f;
        Bind(canLabel, "ui.menu.cancel");
        UnityEventTools.AddPersistentListener(cancel.onClick, op.Close);

        op.audioMixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Resources/Audio/MainMixer.mixer");

        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    const float LABEL_X = -500f;
    const float CONTROL_X = 160f;
    const float CONTROL_W = 360f;

    static RectTransform Row(Transform parent, string name, string label, string key, float y, bool isNew)
    {
        var row = NewRect(name, parent);
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1100f, 60f));
        var t = NewText("Label", row, label, cinzel, 24f, Bone, TextAlignmentOptions.MidlineLeft);
        t.characterSpacing = 5f;
        t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 24f;
        var lrt = t.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.sizeDelta = new Vector2(480f, 50f);
        lrt.anchoredPosition = new Vector2(LABEL_X, 0f);
        Bind(t, key);
        return row;
    }

    static Slider SliderRow(Transform parent, string name, string label, string key, float y, float min, float max, bool percent, bool isNew)
    {
        var row = Row(parent, name, label, key, y, isNew);
        var sgo = NewRect("Slider", row);
        Place(sgo, new Vector2(0.5f, 0.5f), new Vector2(CONTROL_X, 0f), new Vector2(CONTROL_W, 24f));
        var slider = sgo.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.transition = Selectable.Transition.None;

        var bg = NewImage("Background", sgo, white, true);
        bg.color = new Color(Bone.r, Bone.g, Bone.b, 0.28f);
        var bgrt = bg.rectTransform;
        bgrt.anchorMin = new Vector2(0f, 0.5f); bgrt.anchorMax = new Vector2(1f, 0.5f);
        bgrt.sizeDelta = new Vector2(0f, 2f); bgrt.anchoredPosition = Vector2.zero;
        // a wider invisible hit area for the whole row of the slider
        var hit = NewImage("HitArea", sgo, white, true);
        hit.color = new Color(0f, 0f, 0f, 0f);
        Stretch(hit.rectTransform);

        var fillArea = NewRect("Fill Area", sgo);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 2f); fillArea.anchoredPosition = Vector2.zero;
        var fill = NewImage("Fill", fillArea, white, false);
        fill.color = Warm;
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
        frt.sizeDelta = Vector2.zero;

        var handleArea = NewRect("Handle Slide Area", sgo);
        Stretch(handleArea);
        var handle = NewImage("Handle", handleArea, diamond, true);
        handle.color = Warm;
        var hrt = handle.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);
        hrt.sizeDelta = new Vector2(22f, 0f);

        slider.fillRect = frt;
        slider.handleRect = hrt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;

        var value = NewText("Value", row, "", cinzel, 21f, new Color(Bone.r, Bone.g, Bone.b, 0.8f), TextAlignmentOptions.MidlineLeft);
        Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(CONTROL_X + CONTROL_W * 0.5f + 70f, 0f), new Vector2(110f, 40f));
        var vl = row.gameObject.AddComponent<SliderValueLabel>();
        vl.slider = slider;
        vl.label = value;
        vl.percent = percent;
        return slider;
    }

    static Toggle ToggleRow(Transform parent, string name, string label, string key, float y, bool isNew)
    {
        var row = Row(parent, name, label, key, y, isNew);
        var tgo = NewRect("Toggle", row);
        Place(tgo, new Vector2(0.5f, 0.5f), new Vector2(CONTROL_X - CONTROL_W * 0.5f + 16f, 0f), new Vector2(34f, 34f));
        var box = tgo.gameObject.AddComponent<Image>();
        box.sprite = checkBox;
        box.color = new Color(Bone.r, Bone.g, Bone.b, 0.8f);
        var toggle = tgo.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = box;
        var tick = NewImage("Checkmark", tgo, checkTick, false);
        Stretch(tick.rectTransform);
        tick.color = Warm;
        toggle.graphic = tick;
        toggle.isOn = false;
        return toggle;
    }

    static TextMeshProUGUI CyclerRow(Transform parent, string name, string label, string key, float y, string value, out Button left, out Button right)
    {
        var row = Row(parent, name, label, key, y, false);
        var v = NewText("Value", row, value, cinzel, 23f, Bone, TextAlignmentOptions.Center);
        v.characterSpacing = 4f;
        v.enableAutoSizing = true; v.fontSizeMin = 14f; v.fontSizeMax = 23f;
        Place(v.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(CONTROL_X, 0f), new Vector2(CONTROL_W - 90f, 44f));
        FontOnly(v);
        left = ArrowButton("Prev", row, "‹", new Vector2(CONTROL_X - CONTROL_W * 0.5f + 10f, 0f));
        right = ArrowButton("Next", row, "›", new Vector2(CONTROL_X + CONTROL_W * 0.5f - 10f, 0f));
        return v;
    }

    // ------------------------------------------------------------------ splash
    static void BuildSplash(Transform canvasTf)
    {
        var panel = FindChild(canvasTf, "DisclaimerPanel");
        if (panel == null) return;
        var screen = panel.GetComponent<DisclaimerScreen>() ?? panel.gameObject.AddComponent<DisclaimerScreen>();
        for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);
        var bg = panel.GetComponent<Image>();
        if (bg != null) { bg.color = Color.black; bg.raycastTarget = true; }

        var logoGo = NewRect("Logo", panel);
        Place(logoGo, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1040f, 1040f * logo.rect.height / logo.rect.width));
        var lg = logoGo.gameObject.AddComponent<CanvasGroup>();
        var li = logoGo.gameObject.AddComponent<Image>();
        li.sprite = logo;
        li.preserveAspect = true;
        li.raycastTarget = false;
        li.material = sweepMaterial;

        var phones = NewRect("Headphones", panel);
        Place(phones, new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(1400f, 56f));
        var pg = phones.gameObject.AddComponent<CanvasGroup>();
        var h = phones.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 22f;
        h.childControlWidth = false; h.childControlHeight = false;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var iconSprite = LoadSpriteAt("Assets/UI/MainMenu/HEADPHONES_ICON.png");
        var icon = NewImage("HeadphonesIcon", phones, iconSprite, false);
        icon.preserveAspect = true;
        icon.color = new Color(Bone.r, Bone.g, Bone.b, 0.75f);
        icon.rectTransform.sizeDelta = new Vector2(54f, 46f);
        var text = NewText("DisclaimerText", phones, "It is recommended to use headphones", cinzel, 22f, new Color(Bone.r, Bone.g, Bone.b, 0.7f), TextAlignmentOptions.MidlineLeft);
        text.characterSpacing = 6f;
        text.fontStyle = FontStyles.UpperCase;
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(1100f, 46f);
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        Bind(text, "ui.disclaimer.headphones");

        screen.background = bg;
        screen.logoGroup = lg;
        screen.logo = li;
        screen.headphonesGroup = pg;
        EditorUtility.SetDirty(screen);
    }

    // ------------------------------------------------------------------ building blocks
    static RectTransform Modal(string name, Transform parent, Vector2 size, out RectTransform modal)
    {
        var panel = NewRect(name, parent);
        Stretch(panel);
        var dim = NewImage("Dim", panel, null, true);
        Stretch(dim.rectTransform);
        dim.color = DimColor;
        var img = NewImage("Modal", panel, slab, true);
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        img.gameObject.AddComponent<CanvasGroup>();
        var mi = img.gameObject.AddComponent<MenuIntroFX>();
        mi.fadeDuration = 0.22f; mi.startDelay = 0f; mi.riseDistance = 22f;
        mi.riseTargets = new RectTransform[] { img.rectTransform };
        modal = img.rectTransform;
        return panel;
    }

    // A Cinzel label that is the button: warm hover, glow, thin rules either side (EpitaphButtonFX).
    static Button TextButton(string name, Transform parent, string label, TMP_FontAsset font, float size, Vector2 box, out TextMeshProUGUI text)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = box;

        var g = NewImage("Glow", rt, glow, false);
        g.color = new Color(Warm.r, Warm.g, Warm.b, 0f);
        Place(g.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(box.x * 0.75f, box.y * 1.6f));

        var rl = NewImage("RuleLeft", rt, rule, false);
        Place(rl.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1f, 4f));
        var rr = NewImage("RuleRight", rt, rule, false);
        Place(rr.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1f, 4f));

        text = NewText("Label", rt, label, font, size, Bone, TextAlignmentOptions.Center);
        text.characterSpacing = 10f;
        text.enableWordWrapping = false;
        text.raycastTarget = true;
        Stretch(text.rectTransform);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = text;
        var fx = rt.gameObject.AddComponent<EpitaphButtonFX>();
        fx.label = text;
        fx.ruleLeft = rl.rectTransform;
        fx.ruleRight = rr.rectTransform;
        fx.glow = g;
        fx.ruleGap = Mathf.Round(size * 0.62f);
        return btn;
    }

    static Button BackButton(Transform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick, Vector2 anchor)
    {
        TextMeshProUGUI t;
        var b = TextButton("BackButton", parent, "BACK", cinzel, 34f, new Vector2(300f, 70f), out t);
        Place((RectTransform)b.transform, anchor, pos, new Vector2(300f, 70f));
        b.GetComponent<EpitaphButtonFX>().ruleLength = 56f;
        Bind(t, "ui.menu.back");
        if (onClick != null) UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    static Button ArrowButton(string name, Transform parent, string glyph, Vector2 pos)
    {
        TextMeshProUGUI t;
        var b = TextButton(name, parent, glyph, cinzel, 44f, new Vector2(64f, 64f), out t);
        Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), pos, new Vector2(64f, 64f));
        t.characterSpacing = 0f;
        var fx = b.GetComponent<EpitaphButtonFX>();
        fx.ruleLength = 0f;
        fx.glowAlpha = 0.3f;
        return b;
    }

    static void Bind(TMP_Text t, string key) { if (t != null) Loc.BindKey(t, key); }
    static void FontOnly(TMP_Text t) { if (t != null) Loc.EnsureFontComponent(t); }

    // ------------------------------------------------------------------ assets / helpers
    static bool LoadAssets()
    {
        cinzel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EpitaphAssets.CinzelPath);
        cinzelBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EpitaphAssets.CinzelBoldPath);
        cormorant = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EpitaphAssets.CormorantPath);
        carvedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EpitaphAssets.CarvedMaterialPath);
        sweepMaterial = AssetDatabase.LoadAssetAtPath<Material>(EpitaphAssets.SweepMaterialPath);
        slab = Sprite("SLAB.png");
        rule = Sprite("RULE.png");
        glow = Sprite("GLOW.png");
        white = Sprite("WHITE.png");
        diamond = Sprite("DIAMOND.png");
        checkBox = Sprite("CHECK_BOX.png");
        checkTick = Sprite("CHECK_TICK.png");
        shade = Sprite("MENU_SHADE.png");
        logo = Sprite("LOGO_EPITAPH.png");
        tablet = Sprite("TABLET.png");
        tabletWorn = Sprite("TABLET_WORN.png");
        return cinzel != null && cinzelBold != null && cormorant != null && slab != null && logo != null && tablet != null;
    }

    static Sprite Sprite(string file) { return AssetDatabase.LoadAssetAtPath<Sprite>(EpitaphDir + file); }

    static Sprite LoadSpriteAt(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
            if (c.name == name) return c;
        return null;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SizeKeepAspect(RectTransform rt, Sprite sp, float width)
    {
        float h = (sp != null && sp.rect.width > 0f) ? width * sp.rect.height / sp.rect.width : width;
        rt.sizeDelta = new Vector2(width, h);
    }

    static Image NewImage(string name, Transform parent, Sprite sp, bool raycast)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp;
        img.raycastTarget = raycast;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        // Carved capitals everywhere Cinzel is used, whatever case a translation comes in.
        if (font != null && (font == cinzel || font == cinzelBold)) t.fontStyle |= FontStyles.UpperCase;
        return t;
    }

    // ------------------------------------------------------------------ pause menu (gameplay scene)
    public const string GameScenePath = "Assets/Scenes/Scene 0.5 Dungeon.unity";

    /// <summary>
    /// Restyles the in-game pause menu to match (Resume / Settings / Exit on a stone slab) and gives
    /// it the shared Settings screen. Both live on a canvas of their own, laid out at 1920x1080 like
    /// the main menu, above the HUD.
    /// </summary>
    [MenuItem("Tools/Graveyard/Menu/Build Pause Menu")]
    public static string BuildPauseMenu()
    {
        if (!LoadAssets()) return "ERROR: Epitaph assets missing";
        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
        if (settingsPrefab == null) return "ERROR: build the main menu first (no SettingsPanel prefab)";

        var scene = SceneManager.GetSceneByPath(GameScenePath);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive); opened = true; }

        PauseMenu pm = null;
        foreach (var p in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (p.gameObject.scene == scene) pm = p;
        if (pm == null || pm.pausePanel == null) return "ERROR: no PauseMenu / pausePanel in " + GameScenePath;

        var canvasTf = pm.transform.Find("PauseCanvas");
        if (canvasTf == null)
        {
            var go = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(pm.transform, false);
            canvasTf = go.transform;
        }
        canvasTf.gameObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasTf.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasTf.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // The pause panel moves onto this canvas and is rebuilt.
        var panel = (RectTransform)pm.pausePanel.transform;
        panel.SetParent(canvasTf, false);
        Stretch(panel);
        for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);
        var dim = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        dim.sprite = null;
        dim.color = new Color(0f, 0f, 0f, 0.74f);
        dim.raycastTarget = true;

        var slabImg = NewImage("Slab", panel, slab, true);
        slabImg.type = Image.Type.Sliced;
        Place(slabImg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 560f));
        var title = NewText("Title", slabImg.transform, "PAUSED", cinzelBold, 46f, Bone, TextAlignmentOptions.Center);
        title.characterSpacing = 14f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(560f, 64f));
        Bind(title, "ui.pause.title");

        TextMeshProUGUI rl, sl, ql;
        var resume = TextButton("ResumeButton", slabImg.transform, "Resume", cinzel, 40f, new Vector2(560f, 80f), out rl);
        Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(560f, 80f));
        Bind(rl, "ui.pause.resume");
        var settingsBtn = TextButton("SettingsButton", slabImg.transform, "SETTINGS", cinzel, 40f, new Vector2(560f, 80f), out sl);
        Place((RectTransform)settingsBtn.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(560f, 80f));
        Bind(sl, "ui.menu.settings");
        var quit = TextButton("ExitButton", slabImg.transform, "Exit", cinzel, 40f, new Vector2(560f, 80f), out ql);
        Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -136f), new Vector2(560f, 80f));
        Bind(ql, "ui.pause.exit");

        BuildPauseControls(panel);

        var old = canvasTf.Find("SettingsPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var settings = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, scene);
        settings.transform.SetParent(canvasTf, false);
        settings.name = "SettingsPanel";
        settings.SetActive(false);

        pm.resumeButton = resume;
        pm.settingsButton = settingsBtn;
        pm.quitButton = quit;
        pm.settingsPanel = settings.GetComponent<OptionsPanel>();
        pm.pausePanel.SetActive(false);
        // The pause menu shipped switched off; it is on now that it has Settings (PauseMenu guards
        // against pausing mid-cutscene / mid-kill / on the Escape that closes a note or dialogue).
        pm.enabled = true;
        EditorUtility.SetDirty(pm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("MainMenuBuilder: pause menu rebuilt with Settings.");
        return "OK: pause menu rebuilt (Resume / Settings / Exit + controls + Settings screen) and " + GameScenePath + " saved.";
    }

    // The keys the intro's controls card shows, plus the CCTV: { key, localization key, English }.
    static readonly string[][] PauseControls =
    {
        new[] { "WASD", "ui.controls.move", "Move" },
        new[] { "SHIFT", "ui.controls.sprint", "Sprint" },
        new[] { "SPACE", "ui.controls.jump", "Jump" },
        new[] { "E", "ui.controls.interact", "Interact" },
        new[] { "F", "ui.controls.flashlight", "Flashlight" },
        new[] { "← →", "ui.controls.cameras", "Switch cameras" },
    };

    /// <summary>
    /// The controls in two centred rows along the bottom of the pause screen. Layout groups size each
    /// key + action pair to its text, so a longer translation only widens its row.
    /// </summary>
    static void BuildPauseControls(RectTransform panel)
    {
        var block = NewRect("Controls", panel);
        Place(block, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1500f, 104f));
        var rows = block.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.childAlignment = TextAnchor.MiddleCenter;
        rows.spacing = 18f;
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandWidth = rows.childForceExpandHeight = false;

        for (int r = 0; r < 2; r++)
        {
            var row = NewRect("Row" + (r + 1), block);
            var across = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            across.childAlignment = TextAnchor.MiddleCenter;
            across.spacing = 56f;
            across.childControlWidth = across.childControlHeight = true;
            across.childForceExpandWidth = across.childForceExpandHeight = false;

            for (int i = r * 3; i < r * 3 + 3; i++)
            {
                var pair = NewRect("Control", row);
                var inner = pair.gameObject.AddComponent<HorizontalLayoutGroup>();
                inner.childAlignment = TextAnchor.MiddleCenter;
                inner.spacing = 14f;
                inner.childControlWidth = inner.childControlHeight = true;
                inner.childForceExpandWidth = inner.childForceExpandHeight = false;

                var key = NewText("Key", pair, PauseControls[i][0], cinzelBold, 26f, Bone, TextAlignmentOptions.Center);
                key.enableWordWrapping = false;
                var action = NewText("Action", pair, PauseControls[i][2], cinzel, 26f, Muted, TextAlignmentOptions.Left);
                action.enableWordWrapping = false;
                action.characterSpacing = 4f;
                Bind(action, PauseControls[i][1]);
            }
        }
    }

    // ------------------------------------------------------------------ test helpers
    [MenuItem("Tools/Graveyard/Endings/Unlock All")]
    public static void UnlockAllEndings()
    {
        foreach (var e in EndingInfo.InOrder) EndingsSave.Unlock(e);
        Debug.Log("MainMenuBuilder: all endings unlocked.");
    }

    [MenuItem("Tools/Graveyard/Endings/Reset All")]
    public static void ResetAllEndings()
    {
        EndingsSave.ResetAll();
        Debug.Log("MainMenuBuilder: all endings reset.");
    }

    [MenuItem("Tools/Graveyard/Crypt Fiends/Unlock All")]
    public static void UnlockAllCryptFiends()
    {
        foreach (var f in CryptFiendInfo.InOrder) CryptFiendsSave.Unlock(f);
        Debug.Log("MainMenuBuilder: all crypt fiends unlocked.");
    }

    [MenuItem("Tools/Graveyard/Crypt Fiends/Reset All")]
    public static void ResetAllCryptFiends()
    {
        CryptFiendsSave.ResetAll();
        Debug.Log("MainMenuBuilder: all crypt fiends reset.");
    }

    [MenuItem("Tools/Graveyard/Field Notes/Unlock All")]
    public static void UnlockAllFieldNotes()
    {
        var archive = FieldNoteArchive.Load();
        if (archive != null && archive.entries != null)
            foreach (var entry in archive.entries) FieldNotesSave.Unlock(entry.id);
        Debug.Log("MainMenuBuilder: all field notes unlocked.");
    }

    [MenuItem("Tools/Graveyard/Field Notes/Reset All")]
    public static void ResetAllFieldNotes()
    {
        FieldNotesSave.ResetAll();
        Debug.Log("MainMenuBuilder: all field notes reset.");
    }

    [MenuItem("Tools/Graveyard/Last Death/Simulate Death in Second Watch")]
    public static void SimulateDeath()
    {
        LastDeathSave.RecordDeath(DeathCauses.GraveRobber, 2);
        Debug.Log("MainMenuBuilder: the menu will offer 'Return to the Second Watch'.");
    }

    [MenuItem("Tools/Graveyard/Last Death/Clear")]
    public static void ClearLastDeath()
    {
        LastDeathSave.ClearReturnOffer();
        Debug.Log("MainMenuBuilder: return offer cleared.");
    }
}
#endif
