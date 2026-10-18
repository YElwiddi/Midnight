#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// One-time (re-runnable) setup of the assets the Epitaph-style menu is built from:
/// TMP font assets for Cinzel / Cormorant Garamond / Noto Serif CJK, their default materials,
/// sprite import settings for Assets/UI/Epitaph, and the per-locale font rules so the serif
/// menu text keeps a serif look in Russian (Cormorant) and Chinese / Japanese / Korean (Noto Serif).
/// Run via Tools > Graveyard > Menu > Create Epitaph Assets.
/// </summary>
public static class EpitaphAssets
{
    public const string SpriteDir = "Assets/UI/Epitaph/";
    public const string FontDir = "Assets/UI/Fonts/";
    public const string LocFontDir = "Assets/Localization/Fonts/";

    public const string CinzelPath = FontDir + "Cinzel SDF.asset";
    public const string CinzelBoldPath = FontDir + "Cinzel Bold SDF.asset";
    public const string CormorantPath = FontDir + "Cormorant SDF.asset";
    public const string CarvedMaterialPath = FontDir + "Cinzel SDF - Carved.mat";
    public const string SweepMaterialPath = "Assets/UI/Epitaph/EpitaphSweep.mat";

    static readonly string[] SlicedSprites = { "SLAB.png" };

    [MenuItem("Tools/Graveyard/Menu/Create Epitaph Assets")]
    public static string CreateAll()
    {
        AssetDatabase.Refresh();
        var log = new System.Text.StringBuilder();

        // ---- sprites
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileName(path);
            ImportSprite(path, System.Array.IndexOf(SlicedSprites, file) >= 0 ? new Vector4(56, 56, 56, 56) : Vector4.zero);
        }
        log.AppendLine("sprites imported");

        // ---- fonts
        var cinzel = MakeFont(FontDir + "Cinzel-SemiBold.ttf", CinzelPath, "Cinzel SDF", 90, 1024);
        var cinzelBold = MakeFont(FontDir + "Cinzel-Bold.ttf", CinzelBoldPath, "Cinzel Bold SDF", 90, 1024);
        var cormorant = MakeFont(FontDir + "CormorantGaramond-Medium.ttf", CormorantPath, "Cormorant SDF", 90, 1024);
        var serifSC = MakeFont(LocFontDir + "NotoSerifSC-Regular.otf", LocFontDir + "NotoSerifSC SDF.asset", "NotoSerifSC SDF", 90, 2048);
        var serifJP = MakeFont(LocFontDir + "NotoSerifJP-Regular.otf", LocFontDir + "NotoSerifJP SDF.asset", "NotoSerifJP SDF", 90, 2048);
        var serifKR = MakeFont(LocFontDir + "NotoSerifKR-Regular.otf", LocFontDir + "NotoSerifKR SDF.asset", "NotoSerifKR SDF", 90, 2048);
        log.AppendLine("fonts: " + Names(cinzel, cinzelBold, cormorant, serifSC, serifJP, serifKR));

        // Cinzel's bold weight is the real Cinzel Bold, not faux bold.
        if (cinzel != null && cinzelBold != null)
        {
            var so = new SerializedObject(cinzel);
            var table = so.FindProperty("m_FontWeightTable");
            if (table != null && table.arraySize > 7)
            {
                table.GetArrayElementAtIndex(7).FindPropertyRelative("regularTypeface").objectReferenceValue = cinzelBold;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Every label on the menu gets a soft dark underlay so it reads over the 3D backdrop.
        foreach (var f in new[] { cinzel, cinzelBold, cormorant })
            if (f != null) SetUnderlay(f.material, new Color(0f, 0f, 0f, 0.62f), 0.6f, 0.12f, new Vector2(0f, -0.55f));

        // Carved letters for the endings tablets: dark face, pale lower lip where the light catches the cut.
        if (cinzel != null)
        {
            var carved = AssetDatabase.LoadAssetAtPath<Material>(CarvedMaterialPath);
            if (carved == null)
            {
                carved = new Material(cinzel.material) { name = "Cinzel SDF - Carved" };
                AssetDatabase.CreateAsset(carved, CarvedMaterialPath);
            }
            carved.CopyPropertiesFromMaterial(cinzel.material);
            SetUnderlay(carved, new Color(0.92f, 0.89f, 0.82f, 0.42f), 0.12f, 0.02f, new Vector2(0.35f, -0.9f));
            carved.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.06f);
            carved.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.04f, 0.04f, 1f));
            carved.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(carved);
        }

        // Splash sweep material.
        var shader = Shader.Find("UI/Epitaph Sweep");
        if (shader != null && AssetDatabase.LoadAssetAtPath<Material>(SweepMaterialPath) == null)
            AssetDatabase.CreateAsset(new Material(shader) { name = "EpitaphSweep" }, SweepMaterialPath);

        // ---- localization font rules
        var settings = Resources.Load<LocalizationSettings>(LocalizationSettings.ResourcesPath);
        if (settings != null)
        {
            SetRules(settings, "ru", new[] { cinzel, cinzelBold }, cormorant, 1.08f);
            SetRules(settings, "zh-Hans", new[] { cinzel, cinzelBold, cormorant }, serifSC, 0.95f);
            SetRules(settings, "ja", new[] { cinzel, cinzelBold, cormorant }, serifJP, 0.95f);
            SetRules(settings, "ko", new[] { cinzel, cinzelBold, cormorant }, serifKR, 0.95f);
            EditorUtility.SetDirty(settings);
            log.AppendLine("font rules set for ru / zh-Hans / ja / ko");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("EpitaphAssets: " + log);
        return log.ToString();
    }

    static string Names(params Object[] objs)
    {
        var parts = new List<string>();
        foreach (var o in objs) parts.Add(o != null ? o.name : "MISSING");
        return string.Join(", ", parts.ToArray());
    }

    static void ImportSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.spriteBorder = border;
        var s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(s);
        importer.SaveAndReimport();
    }

    static TMP_FontAsset MakeFont(string sourcePath, string assetPath, string name, int pointSize, int atlasSize)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (font == null) { Debug.LogError("EpitaphAssets: missing font " + sourcePath); return null; }

        var fa = TMP_FontAsset.CreateFontAsset(font, pointSize, 9, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
        fa.name = name;
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    static void SetUnderlay(Material m, Color color, float softness, float dilate, Vector2 offset)
    {
        if (m == null) return;
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offset.y);
        EditorUtility.SetDirty(m);
    }

    static void SetRules(LocalizationSettings settings, string code, TMP_FontAsset[] originals, TMP_FontAsset replacement, float scale)
    {
        var def = settings.Find(code);
        if (def == null || replacement == null) return;
        if (def.fontRules == null) def.fontRules = new List<LocalizationSettings.FontRule>();
        foreach (var original in originals)
        {
            if (original == null) continue;
            var rule = def.fontRules.Find(r => r.original == original);
            if (rule == null) { rule = new LocalizationSettings.FontRule { original = original }; def.fontRules.Add(rule); }
            rule.replacement = replacement;
            rule.sizeScale = scale;
        }
    }
}
#endif
