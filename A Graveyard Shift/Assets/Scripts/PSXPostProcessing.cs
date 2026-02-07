using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PSXPostProcessing : MonoBehaviour
{
    [Header("=== Dithering (Grain) ===")]
    public bool enableDithering = true;
    [Tooltip("0=Checkerboard, 1=Scattered noise, 2=Ordered Bayer, 3=Diamond")]
    [Range(0, 3)]
    public int ditherPatternIndex = 1;
    [Tooltip("Lower = more grain coverage")]
    public float ditherThreshold = 1f;
    [Range(0f, 2f)]
    public float ditherStrength = 1f;
    [Tooltip("Higher = chunkier grain")]
    [Range(1f, 8f)]
    public float ditherScale = 2f;

    [Header("=== Pixelation ===")]
    public bool enablePixelation = false;
    [Tooltip("Horizontal resolution to crush down to")]
    public float widthPixelation = 320f;
    [Tooltip("Vertical resolution to crush down to")]
    public float heightPixelation = 240f;
    [Tooltip("Number of color steps per channel (lower = more posterized)")]
    public float colorPrecision = 32f;

    // Shader references — populated automatically from the existing PSX shaders
    private Material _ditherMat;
    private Material _pixelMat;

    private static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
    private static readonly int DitherThreshold = Shader.PropertyToID("_DitherThreshold");
    private static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
    private static readonly int DitherScale = Shader.PropertyToID("_DitherScale");
    private static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
    private static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
    private static readonly int ColorPrecision = Shader.PropertyToID("_ColorPrecision");

    void OnEnable()
    {
        var ditherShader = Shader.Find("PostEffect/Dithering");
        if (ditherShader != null)
            _ditherMat = new Material(ditherShader);
        else
            Debug.LogWarning("PSXPostProcessing: Could not find shader 'PostEffect/Dithering'");

        var pixelShader = Shader.Find("PostEffect/Pixelation");
        if (pixelShader != null)
            _pixelMat = new Material(pixelShader);
        else
            Debug.LogWarning("PSXPostProcessing: Could not find shader 'PostEffect/Pixelation'");
    }

    void OnDisable()
    {
        if (_ditherMat != null) DestroyImmediate(_ditherMat);
        if (_pixelMat != null) DestroyImmediate(_pixelMat);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RenderTexture current = source;

        // Pass 1: Pixelation + Color Precision
        if (enablePixelation && _pixelMat != null)
        {
            _pixelMat.SetFloat(WidthPixelation, widthPixelation);
            _pixelMat.SetFloat(HeightPixelation, heightPixelation);
            _pixelMat.SetFloat(ColorPrecision, colorPrecision);

            var temp = RenderTexture.GetTemporary(source.width, source.height);
            Graphics.Blit(current, temp, _pixelMat);
            if (current != source) RenderTexture.ReleaseTemporary(current);
            current = temp;
        }

        // Pass 2: Dithering (grain)
        if (enableDithering && _ditherMat != null)
        {
            _ditherMat.SetInt(PatternIndex, ditherPatternIndex);
            _ditherMat.SetFloat(DitherThreshold, ditherThreshold);
            _ditherMat.SetFloat(DitherStrength, ditherStrength);
            _ditherMat.SetFloat(DitherScale, ditherScale);

            var temp = RenderTexture.GetTemporary(source.width, source.height);
            Graphics.Blit(current, temp, _ditherMat);
            if (current != source) RenderTexture.ReleaseTemporary(current);
            current = temp;
        }

        // Final output
        Graphics.Blit(current, destination);
        if (current != source) RenderTexture.ReleaseTemporary(current);
    }
}
