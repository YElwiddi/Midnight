using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The camera's tape effect. Which picture it draws is the player's Picture setting (<see cref="PictureSettings"/>):
///
///  • Tape / Light — the tape picture (<see cref="TapeRenderer"/>, Hidden/TapePass). The grade fields below
///    (contrast, saturation, brightness, gamma, vignette) are carried into it unchanged. The values that the
///    sanity and kill scripts write here keep working: noiseIntensity (and scanlineIntensity in old-style kills)
///    set the tape's wear (<see cref="TapeProfile.Wear"/>); glitchIntensity, trackingNoise, rgbShiftAmount and
///    dropoutIntensity are the kill beats, applied on the frame they are set (<see cref="TapeProfile.Compose"/>).
///    The pixel grid on the same camera (<see cref="PSXPostProcessing"/>) steps aside.
///  • Classic — the original shader, exactly as it shipped, followed by the pixel grid.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Effects/VHS Retro Pro")]
public class VHSRetroFeature : MonoBehaviour
{
    [Header("SHADER ASSIGNMENT")]
    [SerializeField] private Shader vhsShader;

    [Header("RGB SHIFT SETTINGS")]
    [Range(0, 0.05f)] public float rgbShiftAmount = 0.012f;
    [Range(0, 5f)] public float rgbShiftSpeed = 1.5f;
    [Range(0.1f, 3f)] public float rgbShiftCurve = 1.2f;

    [Header("PIXELATION SETTINGS")]
    [Range(1, 15f)] public float pixelSize = 4.0f;
    [Range(0, 1f)] public float pixelScanlines = 0.6f;

    [Header("NOISE & GRAIN SETTINGS")]
    [Range(0, 0.5f)] public float grainAmount = 0.15f;
    [Range(0.5f, 3f)] public float grainSize = 1.2f;
    [Range(0, 0.3f)] public float noiseIntensity = 0.08f;

    [Header("SCANLINE SETTINGS")]
    [Range(0, 1f)] public float scanlineIntensity = 0.45f;
    [Range(0, 5f)] public float scanlineSpeed = 0.8f;
    [Range(200, 800f)] public float scanlineCount = 480f;
    [Range(0.5f, 2f)] public float scanlineSharpness = 1.2f;

    [Header("VIGNETTE SETTINGS")]
    [Range(0, 2f)] public float vignetteIntensity = 0.6f;
    [Range(0.1f, 2f)] public float vignetteSoftness = 0.8f;
    [Range(0.5f, 3f)] public float vignetteRoundness = 1.5f;

    [Header("GLITCH SETTINGS")]
    [Range(0, 1f)] public float glitchIntensity = 0.25f;
    [Range(0, 10f)] public float glitchSpeed = 3.0f;
    [Range(0.005f, 0.05f)] public float glitchBlockSize = 0.015f;

    [Header("COLOR GRADING SETTINGS")]
    [Range(0.8f, 1.5f)] public float contrast = 1.08f;
    [Range(0.8f, 1.4f)] public float saturation = 1.12f;
    [Range(-0.2f, 0.2f)] public float brightness = 0.02f;
    [Range(0.8f, 1.2f)] public float gamma = 1.05f;

    [Header("TAPE EFFECTS SETTINGS")]
    [Range(0, 0.2f)] public float tapeNoise = 0.06f;
    [Range(0, 5f)] public float tapeSpeed = 0.5f;
    public float tapeWobble = 0f;

    [Header("TRACKING ERROR SETTINGS")]
    [Range(0, 0.1f)] public float trackingNoise = 0.03f;
    [Range(0, 5f)] public float trackingSpeed = 1.2f;

    [Header("SIGNAL DROPOUT SETTINGS")]
    [Range(0, 0.1f)] public float dropoutIntensity = 0.02f;
    [Range(0, 10f)] public float dropoutSpeed = 4.0f;

    [Header("TIME CONTROL")]
    [Range(0, 3f)] public float timeSpeed = 1.0f;

    [Header("EFFECT TOGGLES")]
    public bool enablePixel = true;
    public bool enableGlitch = true;
    public bool enableRGB = true;
    public bool enableGrain = true;
    public bool enableScan = true;
    public bool enableTape = false;
    public bool enableTracking = true;
    public bool enableDropout = true;

    [Header("PERFORMANCE")]
    public bool enableInEditMode = true;

    [Header("TAPE PICTURE (Picture setting: Tape / Light)")]
    [Tooltip("The shared tape look. Empty = the asset at Resources/TapeProfile, or built-in defaults.")]
    public TapeProfile tapeProfile;

    private Material _material;
    private bool _isSupported = true;

    private TapeRenderer _tape;
    private readonly TapeFrame _frame = new TapeFrame();
    private PSXPostProcessing _pixelGrid;
    private bool _pixelGridSearched;
    private float _wearShown;
    private float _wearTime = -1f;

    public Material Material
    {
        get
        {
            if (_material == null && vhsShader != null && _isSupported)
            {
                if (vhsShader.isSupported)
                {
                    _material = new Material(vhsShader);
                    _material.hideFlags = HideFlags.HideAndDontSave;
                }
                else
                {
                    _isSupported = false;
                    Debug.LogWarning($"VHS Shader is not supported on this platform.", this);
                }
            }
            return _material;
        }
    }

    /// <summary>True while this component is the one drawing the tape picture (so the pixel grid should not).</summary>
    public bool RendersTape
    {
        get
        {
            if (!isActiveAndEnabled || !PictureSettings.IsTape || !TapeRenderer.Supported) return false;
#if UNITY_EDITOR
            if (!enableInEditMode && !Application.isPlaying) return false;
#endif
            return true;
        }
    }

    /// <summary>The tape wear on screen right now: 0 fresh, 1 worn (sanity 50), 2 failing (sanity 25), 3 kill.</summary>
    public float Wear => _wearShown;

    void Start()
    {
        CheckSupport();
    }

    void OnEnable()
    {
        CheckSupport();
        PictureSettings.Changed += ResetTape;
    }

    void OnDisable()
    {
        PictureSettings.Changed -= ResetTape;
        CleanupMaterial();
    }

    void OnDestroy() => CleanupMaterial();

    void CleanupMaterial()
    {
        if (_material != null)
        {
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }
        if (_tape != null)
        {
            _tape.Dispose();
            _tape = null;
        }
    }

    void ResetTape()
    {
        if (_tape == null) return;
        if (PictureSettings.IsTape) _tape.Reset();
        else { _tape.Dispose(); _tape = null; }
    }

    void CheckSupport()
    {
        if (vhsShader == null)
        {
            Debug.LogWarning("VHS Shader is not assigned.", this);
            _isSupported = false;
            return;
        }

        if (!SystemInfo.supportsImageEffects)
        {
            Debug.LogWarning("Image effects are not supported.", this);
            _isSupported = false;
            return;
        }

        if (!vhsShader.isSupported)
        {
            Debug.LogWarning("VHS Shader is not supported.", this);
            _isSupported = false;
            return;
        }

        _isSupported = true;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (RendersTape)
        {
            RenderTape(source, destination, true);
            return;
        }
        RenderClassic(source, destination);
    }

    // ------------------------------------------------------------------ tape picture

    /// <summary>
    /// Draws the tape picture without wear. The pixel grid calls this while a script has switched this
    /// component off (a jumpscare that wants a clean face), so the picture stays tape and stays graded.
    /// </summary>
    public void RenderCalmTape(RenderTexture source, RenderTexture destination)
    {
        RenderTape(source, destination, false);
    }

    private void RenderTape(RenderTexture source, RenderTexture destination, bool withWear)
    {
        TapeProfile profile = tapeProfile != null ? tapeProfile : TapeProfile.Shared;
        float time = TapeRenderer.Clock;

        // Wear rises at once and settles over a moment when sanity recovers.
        float target = withWear ? profile.Wear(noiseIntensity, scanlineIntensity) : 0f;
        float dt = _wearTime < 0f ? 0f : Mathf.Clamp(time - _wearTime, 0f, 0.25f);
        _wearTime = time;
        _wearShown = target >= _wearShown ? target : Mathf.MoveTowards(_wearShown, target, dt / Mathf.Max(profile.wearRelease, 0.01f));

        profile.Compose(_frame, PictureSettings.Mode, _wearShown, time,
            withWear ? glitchIntensity : 0f, withWear ? trackingNoise : 0f, withWear ? rgbShiftAmount : 0f, withWear ? dropoutIntensity : 0f);

        // The grade this camera already had, in the classic shader's terms. Its "grain" and "tape noise" were
        // really a constant darkening, and the pixel grid subtracted a black point and lost half a colour step.
        _frame.contrast = contrast;
        _frame.gradeSat = saturation;
        _frame.gamma = gamma;
        _frame.bright = brightness;
        _frame.vig = vignetteIntensity;
        _frame.vigSoft = vignetteSoftness;
        _frame.vigRound = vignetteRoundness;
        _frame.preOffset = (grainAmount > 0.001f ? (grainAmount * 0.4375f - 0.5f) * 0.02f : 0f)
                         + (tapeNoise > 0.001f ? (tapeNoise * 0.5f - 0.5f) * 0.01f : 0f);
        PSXPostProcessing grid = PixelGrid;
        _frame.blackPoint = grid != null && grid.enabled && grid.enablePixelation
            ? grid.blackPoint + 0.5f / Mathf.Max(grid.colorPrecision, 1f)
            : 0f;

        if (_tape == null) _tape = new TapeRenderer();
        _tape.Render(source, destination, _frame, time);
    }

    private PSXPostProcessing PixelGrid
    {
        get
        {
            if (!_pixelGridSearched || (_pixelGrid == null && !Application.isPlaying))
            {
                _pixelGridSearched = true;
                _pixelGrid = GetComponent<PSXPostProcessing>();
            }
            return _pixelGrid;
        }
    }

    // ------------------------------------------------------------------ classic picture (unchanged)

    private void RenderClassic(RenderTexture source, RenderTexture destination)
    {
        if (!_isSupported || Material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        #if UNITY_EDITOR
        if (!enableInEditMode && !Application.isPlaying)
        {
            Graphics.Blit(source, destination);
            return;
        }
        #endif

        Material.SetFloat("_RGBShiftAmount", rgbShiftAmount);
        Material.SetFloat("_RGBShiftSpeed", rgbShiftSpeed);
        Material.SetFloat("_RGBShiftCurve", rgbShiftCurve);
        Material.SetFloat("_PixelSize", pixelSize);
        Material.SetFloat("_PixelScanlines", pixelScanlines);
        Material.SetFloat("_GrainAmount", grainAmount);
        Material.SetFloat("_GrainSize", grainSize);
        Material.SetFloat("_NoiseIntensity", noiseIntensity);
        Material.SetFloat("_ScanlineIntensity", scanlineIntensity);
        Material.SetFloat("_ScanlineSpeed", scanlineSpeed);
        Material.SetFloat("_ScanlineCount", scanlineCount);
        Material.SetFloat("_ScanlineSharpness", scanlineSharpness);
        Material.SetFloat("_Vignette", vignetteIntensity);
        Material.SetFloat("_VignetteSoftness", vignetteSoftness);
        Material.SetFloat("_VignetteRoundness", vignetteRoundness);
        Material.SetFloat("_GlitchIntensity", glitchIntensity);
        Material.SetFloat("_GlitchSpeed", glitchSpeed);
        Material.SetFloat("_GlitchBlockSize", glitchBlockSize);
        Material.SetFloat("_Contrast", contrast);
        Material.SetFloat("_Saturation", saturation);
        Material.SetFloat("_Brightness", brightness);
        Material.SetFloat("_Gamma", gamma);
        Material.SetFloat("_TapeNoise", tapeNoise);
        Material.SetFloat("_TapeSpeed", tapeSpeed);
        Material.SetFloat("_TapeWobble", 0f);
        Material.SetFloat("_TrackingNoise", trackingNoise);
        Material.SetFloat("_TrackingSpeed", trackingSpeed);
        Material.SetFloat("_DropoutIntensity", dropoutIntensity);
        Material.SetFloat("_DropoutSpeed", dropoutSpeed);
        Material.SetFloat("_TimeSpeed", timeSpeed);

        Material.SetFloat("_EnablePixel", enablePixel ? 1 : 0);
        Material.SetFloat("_EnableGlitch", enableGlitch ? 1 : 0);
        Material.SetFloat("_EnableRGB", enableRGB ? 1 : 0);
        Material.SetFloat("_EnableGrain", enableGrain ? 1 : 0);
        Material.SetFloat("_EnableScan", enableScan ? 1 : 0);
        Material.SetFloat("_EnableTape", enableTape ? 1 : 0);
        Material.SetFloat("_EnableTracking", enableTracking ? 1 : 0);
        Material.SetFloat("_EnableDropout", enableDropout ? 1 : 0);

        Graphics.Blit(source, destination, Material);
    }

    [ContextMenu("Apply Cinema Preset")]
    public void ApplyCinemaPreset()
    {
        rgbShiftAmount = 0.008f;
        rgbShiftSpeed = 1.2f;
        pixelSize = 3.0f;
        grainAmount = 0.1f;
        scanlineIntensity = 0.35f;
        glitchIntensity = 0.15f;
        contrast = 1.05f;
        saturation = 1.08f;
        tapeNoise = 0.04f;
        tapeWobble = 0f;
        enableTape = false;
    }

    [ContextMenu("Apply Heavy Preset")]
    public void ApplyHeavyPreset()
    {
        rgbShiftAmount = 0.025f;
        rgbShiftSpeed = 2.5f;
        pixelSize = 6.0f;
        grainAmount = 0.25f;
        scanlineIntensity = 0.65f;
        glitchIntensity = 0.45f;
        contrast = 1.15f;
        saturation = 1.18f;
        tapeNoise = 0.12f;
        tapeWobble = 0f;
        enableTape = false;
    }

    [ContextMenu("Toggle All Effects")]
    public void ToggleAllEffects()
    {
        bool state = !enablePixel;
        enablePixel = enableGlitch = enableRGB = enableGrain = enableScan = enableTape = enableTracking = enableDropout = state;
        if (!enableTape) tapeWobble = 0f;
    }

    public void SetShader(Shader newShader)
    {
        CleanupMaterial();
        vhsShader = newShader;
        CheckSupport();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VHSRetroFeature))]
public class VHSRetroProEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var vhs = target as VHSRetroFeature;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VHS Retro Pro - Perfect Edition", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        string mode = PictureSettings.Mode.ToString();
        EditorGUILayout.HelpBox(
            "Picture setting on this machine: " + mode + ".\n" +
            "Tape / Light: the tape picture. The grade and vignette below carry over; noise (and scanline) set the " +
            "tape's wear, glitch / tracking / RGB shift / dropout are the kill beats (see the Tape Profile). " +
            "Classic: this shader as it shipped, then the pixel grid.", MessageType.Info);
        GUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Preview picture");
        foreach (PictureMode m in System.Enum.GetValues(typeof(PictureMode)))
        {
            if (GUILayout.Toggle(PictureSettings.Mode == m, m.ToString(), EditorStyles.miniButton) && PictureSettings.Mode != m)
            {
                PictureSettings.Mode = m;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("vhsShader"));

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Quick Presets (Classic)", EditorStyles.miniBoldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cinema")) { vhs.ApplyCinemaPreset(); EditorUtility.SetDirty(vhs); }
        if (GUILayout.Button("Heavy")) { vhs.ApplyHeavyPreset(); EditorUtility.SetDirty(vhs); }
        if (GUILayout.Button("Toggle All")) { vhs.ToggleAllEffects(); EditorUtility.SetDirty(vhs); }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        DrawPropertiesExcluding(serializedObject, "vhsShader");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
