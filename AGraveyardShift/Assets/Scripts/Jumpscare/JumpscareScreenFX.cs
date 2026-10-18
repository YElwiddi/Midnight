using UnityEngine;

/// <summary>
/// Full-screen jumpscare effects for the built-in pipeline: flash, invert, zoom punch, vertical roll,
/// tear, RGB split, CRT power-off, freeze-frame, subliminal image, dirt landing on the lens, blur, eyelids.
/// Add it to the player camera at kill time (AddComponent puts it after the VHS/PSX effects) and drive
/// the public fields every frame. All fields at their defaults = the picture passes through untouched.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class JumpscareScreenFX : MonoBehaviour
{
    [Tooltip("Hidden/JumpscareScreenFX. Found by name when left empty (editor only: builds need a reference).")]
    public Shader shader;

    [Header("Flash / Grade")]
    public Color flashColor = Color.black;
    [Range(0f, 1f)] public float flash;
    [Range(0f, 1f)] public float invert;
    [Range(0f, 1f)] public float desaturate;
    [Range(0f, 1f)] public float darken;
    [Tooltip("1 = eyes shut, 0 = open.")]
    [Range(0f, 1f)] public float lids;
    public float blur;

    [Header("Picture")]
    public float zoom = 1f;
    public Vector2 zoomCenter = new Vector2(0.5f, 0.5f);
    [Tooltip("Vertical hold slip, in screen heights.")]
    public float roll;
    public float tear;
    public float split;
    [Tooltip("CRT power-off: 0 = on, 1 = collapsed to a dot.")]
    [Range(0f, 1f)] public float collapse;
    [Tooltip("Freeze the picture on the frame where this turns on.")]
    public bool hold;
    public int seed;

    [Header("Overlays")]
    public Texture overlay;
    [Range(0f, 1f)] public float overlayAlpha;
    public bool overlayInvert;
    [Tooltip("Where the overlay sits, in screen uv (0..1). Use FitOverlay to letterbox an image.")]
    public Rect overlayRect = new Rect(0f, 0f, 1f, 1f);
    public Texture dirt;
    [Range(0f, 1f)] public float dirtAmount;
    [Tooltip("Multiplies the dirt colour (it sits in the scene's light).")]
    public Color dirtTint = Color.white;
    [Tooltip("Snap overlays to the game's pixel grid (PSX pixelation) so they look like part of the picture. 0 = off.")]
    public Vector2 pixelGrid = new Vector2(320f, 240f);

    private Material material;
    private RenderTexture held;
    private bool wasHolding;

    /// <summary>Resets every effect to pass-through.</summary>
    public void Clear()
    {
        flash = invert = desaturate = darken = lids = blur = 0f;
        flashColor = Color.black;
        zoom = 1f; zoomCenter = new Vector2(0.5f, 0.5f);
        roll = tear = split = collapse = 0f;
        hold = false;
        overlayAlpha = 0f; overlayInvert = false;
        dirtAmount = 0f;
    }

    /// <summary>Fills the screen with the overlay (cropping it), scaled by <paramref name="scale"/> of the screen width,
    /// with the image point <paramref name="focus"/> (0..1, from the bottom-left) at the screen centre.</summary>
    public void CoverOverlay(Texture tex, float screenAspect, Vector2 focus, float scale = 1f)
    {
        overlay = tex;
        if (tex == null) return;
        float w = scale, h = scale * ((float)tex.height / tex.width) * screenAspect;
        overlayRect = new Rect(0.5f - focus.x * w, 0.5f - focus.y * h, w, h);
    }

    /// <summary>Re-grabs the frozen frame on the next render (a tape "skip" while holding).</summary>
    public void Recapture()
    {
        wasHolding = false;
    }

    /// <summary>Centres the overlay at full screen height, keeping the image's aspect (black bars at the sides).</summary>
    public void FitOverlay(Texture tex, float screenAspect)
    {
        overlay = tex;
        if (tex == null) return;
        float w = ((float)tex.width / tex.height) / screenAspect;
        overlayRect = w <= 1f ? new Rect((1f - w) * 0.5f, 0f, w, 1f) : new Rect(0f, (1f - 1f / w) * 0.5f, 1f, 1f / w);
    }

    private void OnDisable()
    {
        if (held != null) { held.Release(); DestroyImmediate(held); held = null; }
        if (material != null) { DestroyImmediate(material); material = null; }
        wasHolding = false;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (shader == null) shader = Shader.Find("Hidden/JumpscareScreenFX");
        if (shader == null || !shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        if (hold && !wasHolding)
        {
            if (held == null || held.width != source.width || held.height != source.height)
            {
                if (held != null) { held.Release(); DestroyImmediate(held); }
                held = new RenderTexture(source.width, source.height, 0, source.format);
            }
            Graphics.Blit(source, held);
        }
        wasHolding = hold;

        material.SetTexture("_HoldTex", held != null ? (Texture)held : source);
        material.SetFloat("_Hold", hold && held != null ? 1f : 0f);
        material.SetTexture("_OverlayTex", overlay);
        material.SetVector("_OverlayRect", new Vector4(overlayRect.x, overlayRect.y, overlayRect.width, overlayRect.height));
        material.SetFloat("_OverlayAlpha", overlay != null ? overlayAlpha : 0f);
        material.SetFloat("_OverlayInvert", overlayInvert ? 1f : 0f);
        material.SetTexture("_DirtTex", dirt);
        material.SetFloat("_Dirt", dirt != null ? dirtAmount : 0f);
        material.SetColor("_DirtTint", dirtTint);
        material.SetVector("_PixelGrid", pixelGrid);
        material.SetVector("_Flash", new Vector4(flashColor.r, flashColor.g, flashColor.b, flash));
        material.SetFloat("_Invert", invert);
        material.SetFloat("_Zoom", zoom);
        material.SetVector("_ZoomCenter", zoomCenter);
        material.SetFloat("_Roll", roll);
        material.SetFloat("_Collapse", collapse);
        material.SetFloat("_Split", split);
        material.SetFloat("_Tear", tear);
        material.SetFloat("_Seed", seed);
        material.SetFloat("_Desat", desaturate);
        material.SetFloat("_Darken", darken);
        material.SetFloat("_Lids", lids);
        material.SetFloat("_Blur", blur);
        Graphics.Blit(source, destination, material);
    }
}
