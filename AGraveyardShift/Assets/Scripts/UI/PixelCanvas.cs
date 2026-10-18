using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws everything on this canvas on one low-resolution pixel grid so the menu's text and art
/// match the PSX-pixelated backdrop. Each TMP text gets a pixel copy of its font material
/// ("TextMeshPro/Mobile/Distance Field Pixel") and each sprite image the "UI/Epitaph Pixel"
/// material; an image with a material of its own that supports the grid (the splash logo's
/// sweep) just gets the grid size. Copies are made once per source material and put back
/// whenever something swaps a stock material in (a language change assigns another font, the
/// endings panel restyles its tablets). Text whose em would drop below
/// <see cref="minPixelsPerEm"/> virtual pixels moves to a finer grid on its own, so small labels
/// stay readable. Runtime only: the canvas stays crisp in edit mode.
/// </summary>
[DisallowMultipleComponent]
public class PixelCanvas : MonoBehaviour
{
    public const string TextShaderName = "TextMeshPro/Mobile/Distance Field Pixel";
    public const string ImageShaderName = "UI/Epitaph Pixel";

    [Header("Grid")]
    [Tooltip("Virtual vertical resolution: 360 draws 3x3-pixel blocks on a 1080p screen")]
    [SerializeField] private float pixelLines = 360f;

    [Tooltip("Crisp on/off pixels; off keeps one virtual pixel of soft edge")]
    [SerializeField] private bool hardEdges = true;

    [Tooltip("How much of a virtual pixel a letter must cover to light it (crisp edges only). " +
             "Low enough that thin hairlines (Cormorant in Russian, Noto Serif in CJK) don't break up.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float coverageThreshold = 0.25f;

    [Tooltip("Smallest em, in virtual pixels, that text is drawn at; smaller text gets a finer grid")]
    [SerializeField] private float minPixelsPerEm = 12f;

    [Header("Shaders (referenced so builds include them)")]
    [SerializeField] private Shader textShader;
    [SerializeField] private Shader imageShader;

    private static readonly int PixelLinesId = Shader.PropertyToID("_PixelLines");
    private static readonly int PixelHardId = Shader.PropertyToID("_PixelHard");
    private static readonly int PixelThresholdId = Shader.PropertyToID("_PixelThreshold");
    private static readonly int PixelMinEmId = Shader.PropertyToID("_PixelMinEm");
    private static readonly int PixelEmTexelsId = Shader.PropertyToID("_PixelEmTexels");

    private readonly Dictionary<Material, Material> textCopies = new Dictionary<Material, Material>();
    private readonly List<TMP_Text> texts = new List<TMP_Text>();
    private readonly List<Image> images = new List<Image>();
    private Material imageMaterial;
    private float nextScan;

    private void Reset() => FindShaders();

    private void OnValidate()
    {
        FindShaders();
        foreach (var copy in textCopies.Values)
            if (copy != null) Configure(copy);
        if (imageMaterial != null) imageMaterial.SetFloat(PixelLinesId, pixelLines);
    }

    private void FindShaders()
    {
        if (textShader == null) textShader = Shader.Find(TextShaderName);
        if (imageShader == null) imageShader = Shader.Find(ImageShaderName);
    }

    private void OnEnable()
    {
        FindShaders();
        if (textShader == null || imageShader == null)
        {
            Debug.LogWarning("PixelCanvas: pixel shaders missing, the canvas stays crisp.");
            enabled = false;
            return;
        }

        if (imageMaterial == null)
            imageMaterial = new Material(imageShader) { name = "UI Epitaph Pixel (runtime)", hideFlags = HideFlags.DontSave };
        imageMaterial.SetFloat(PixelLinesId, pixelLines);
        Scan();
        Apply();
    }

    private void LateUpdate()
    {
        // Panels and labels come and go; pick up new ones twice a second.
        if (Time.unscaledTime >= nextScan) Scan();
        Apply();
    }

    private void OnDisable() => Restore();

    private void OnDestroy()
    {
        foreach (var copy in textCopies.Values)
            if (copy != null) Destroy(copy);
        textCopies.Clear();
        if (imageMaterial != null) Destroy(imageMaterial);
    }

    private void Scan()
    {
        nextScan = Time.unscaledTime + 0.5f;
        GetComponentsInChildren(true, texts);
        GetComponentsInChildren(true, images);
    }

    // Puts the pixel materials wherever a stock one is (back) in place; cheap when nothing changed.
    private void Apply()
    {
        for (int i = 0; i < texts.Count; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            var material = text.fontSharedMaterial;
            if (material == null || material.shader == textShader) continue;
            text.fontSharedMaterial = TextCopy(material, text.font);
        }

        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            if (image == null || image.sprite == null) continue;
            var material = image.material;
            if (material == imageMaterial) continue;
            if (material == image.defaultMaterial)
                image.material = imageMaterial;
            else if (material.HasProperty(PixelLinesId) && !IsAsset(material) && material.GetFloat(PixelLinesId) != pixelLines)
                material.SetFloat(PixelLinesId, pixelLines);
        }
    }

    private void Restore()
    {
        for (int i = 0; i < texts.Count; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            foreach (var pair in textCopies)
            {
                if (text.fontSharedMaterial != pair.Value) continue;
                text.fontSharedMaterial = pair.Key;
                break;
            }
        }

        for (int i = 0; i < images.Count; i++)
            if (images[i] != null && images[i].material == imageMaterial) images[i].material = null;
    }

    private Material TextCopy(Material source, TMP_FontAsset font)
    {
        if (!textCopies.TryGetValue(source, out var copy) || copy == null)
        {
            copy = new Material(source) { name = source.name + " (Pixel)", hideFlags = HideFlags.DontSave };
            copy.shader = textShader;
            copy.shaderKeywords = source.shaderKeywords;
            // The em in atlas texels, so the shader can tell how big the text is on screen.
            copy.SetFloat(PixelEmTexelsId, font != null ? font.faceInfo.pointSize : 90f);
            Configure(copy);
            textCopies[source] = copy;
        }
        return copy;
    }

    private void Configure(Material copy)
    {
        copy.SetFloat(PixelLinesId, pixelLines);
        copy.SetFloat(PixelHardId, hardEdges ? 1f : 0f);
        copy.SetFloat(PixelThresholdId, coverageThreshold);
        copy.SetFloat(PixelMinEmId, minPixelsPerEm);
    }

    // Grid values set at runtime must never land in a material asset (the editor would save them).
    private static bool IsAsset(Material material)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.IsPersistent(material);
#else
        return false;
#endif
    }
}
