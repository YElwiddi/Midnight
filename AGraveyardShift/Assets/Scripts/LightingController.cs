using System.Collections;
using UnityEngine;

/// <summary>
/// Lighting preset types.
/// </summary>
public enum LightingPreset
{
    None,       // Don't change lighting
    Outdoor,    // Default outdoor/graveyard lighting
    Cabin,      // Indoor cabin lighting
    Crypt,      // Brighter crypt lighting
    Custom      // Use custom settings
}

/// <summary>
/// Settings for a lighting preset (Gradient ambient mode).
/// </summary>
[System.Serializable]
public class LightingSettings
{
    [Header("Environment Lighting (Gradient)")]
    [Tooltip("Sky color for gradient ambient lighting")]
    public Color skyColor = new Color(0.2f, 0.2f, 0.25f);

    [Tooltip("Equator color for gradient ambient lighting")]
    public Color equatorColor = new Color(0.15f, 0.15f, 0.2f);

    [Tooltip("Ground color for gradient ambient lighting")]
    public Color groundColor = new Color(0.1f, 0.1f, 0.12f);
}

/// <summary>
/// Singleton controller for managing scene lighting.
/// Changes Environment Lighting gradient colors (Sky, Equator, Ground).
/// Supports presets and smooth transitions.
/// </summary>
public class LightingController : MonoBehaviour
{
    #region Singleton
    public static LightingController Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Presets")]
    [Tooltip("Outdoor/graveyard lighting settings")]
    [SerializeField] private LightingSettings outdoorSettings = new LightingSettings
    {
        skyColor = new Color(0.1f, 0.1f, 0.15f),
        equatorColor = new Color(0.08f, 0.08f, 0.1f),
        groundColor = new Color(0.05f, 0.05f, 0.06f)
    };

    [Tooltip("Cabin indoor lighting settings")]
    [SerializeField] private LightingSettings cabinSettings = new LightingSettings
    {
        skyColor = new Color(0.15f, 0.12f, 0.1f),
        equatorColor = new Color(0.12f, 0.1f, 0.08f),
        groundColor = new Color(0.08f, 0.06f, 0.05f)
    };

    [Tooltip("Crypt lighting settings (brighter for visibility)")]
    [SerializeField] private LightingSettings cryptSettings = new LightingSettings
    {
        skyColor = new Color(0.3f, 0.3f, 0.35f),
        equatorColor = new Color(0.25f, 0.25f, 0.3f),
        groundColor = new Color(0.2f, 0.2f, 0.22f)
    };

    [Header("Transition")]
    [Tooltip("Duration of lighting transition in seconds")]
    [SerializeField] private float transitionDuration = 1f;

    [Tooltip("If true, transitions are smooth. If false, instant.")]
    [SerializeField] private bool smoothTransitions = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugUI = false;
    #endregion

    #region Properties
    public LightingPreset CurrentPreset => currentPreset;
    public bool IsTransitioning => isTransitioning;
    #endregion

    #region Private Fields
    private LightingPreset currentPreset = LightingPreset.Outdoor;
    private LightingSettings currentSettings;
    private bool isTransitioning = false;
    private Coroutine transitionCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Initialize with outdoor settings
        currentSettings = CloneSettings(outdoorSettings);

        // The player's brightness setting controls the environment's ambient SKY colour
        // (only the sky channel; equator/ground keep their scene values).
        RenderSettings.ambientSkyColor = BrightnessSettings.SkyColor;
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float yOffset = 10f;
        if (GameManager.Instance != null && GameManager.Instance.showDebugStats) yOffset += 240f;

        GUI.Box(new Rect(Screen.width - 170, yOffset, 160, 50), "Lighting");
        GUI.Label(new Rect(Screen.width - 160, yOffset + 20, 140, 20), $"Preset: {currentPreset}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Apply a lighting preset.
    /// </summary>
    public void ApplyPreset(LightingPreset preset)
    {
        if (preset == LightingPreset.None) return;

        LightingSettings settings = GetSettingsForPreset(preset);
        if (settings == null) return;

        // Brightness shifts each area's sky by the same ±32 (0-255 units) around its own
        // default: outdoor is centred on 32 (0..64); cabin/crypt keep their authored centre
        // (e.g. crypt 48 -> 16..80). Equator/ground are untouched.
        settings = CloneSettings(settings);
        settings.skyColor = (preset == LightingPreset.Outdoor)
            ? BrightnessSettings.SkyColor
            : BrightnessSettings.Shift(settings.skyColor);

        ApplySettings(settings, preset);
    }

    /// <summary>
    /// Apply custom lighting settings.
    /// </summary>
    public void ApplySettings(LightingSettings settings, LightingPreset presetType = LightingPreset.Custom)
    {
        if (settings == null) return;

        currentPreset = presetType;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        if (smoothTransitions && transitionDuration > 0)
        {
            transitionCoroutine = StartCoroutine(TransitionToSettings(settings));
        }
        else
        {
            ApplySettingsImmediate(settings);
        }

        Debug.Log($"LightingController: Applying preset '{presetType}'");
    }

    /// <summary>
    /// Reset to outdoor lighting.
    /// </summary>
    public void ResetToOutdoor()
    {
        ApplyPreset(LightingPreset.Outdoor);
    }

    /// <summary>
    /// Get the settings for a preset.
    /// </summary>
    public LightingSettings GetSettingsForPreset(LightingPreset preset)
    {
        return preset switch
        {
            LightingPreset.Outdoor => outdoorSettings,
            LightingPreset.Cabin => cabinSettings,
            LightingPreset.Crypt => cryptSettings,
            _ => null
        };
    }

    /// <summary>
    /// Set transition duration.
    /// </summary>
    public void SetTransitionDuration(float duration)
    {
        transitionDuration = Mathf.Max(0f, duration);
    }
    #endregion

    #region Private Methods
    private void ApplySettingsImmediate(LightingSettings settings)
    {
        // Apply gradient ambient colors
        RenderSettings.ambientSkyColor = settings.skyColor;
        RenderSettings.ambientEquatorColor = settings.equatorColor;
        RenderSettings.ambientGroundColor = settings.groundColor;

        currentSettings = CloneSettings(settings);
        isTransitioning = false;
    }

    private IEnumerator TransitionToSettings(LightingSettings targetSettings)
    {
        isTransitioning = true;

        // Cache starting values
        Color startSky = RenderSettings.ambientSkyColor;
        Color startEquator = RenderSettings.ambientEquatorColor;
        Color startGround = RenderSettings.ambientGroundColor;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Lerp gradient colors
            RenderSettings.ambientSkyColor = Color.Lerp(startSky, targetSettings.skyColor, smoothT);
            RenderSettings.ambientEquatorColor = Color.Lerp(startEquator, targetSettings.equatorColor, smoothT);
            RenderSettings.ambientGroundColor = Color.Lerp(startGround, targetSettings.groundColor, smoothT);

            yield return null;
        }

        // Apply final values
        ApplySettingsImmediate(targetSettings);

        isTransitioning = false;
        transitionCoroutine = null;
    }

    private LightingSettings CloneSettings(LightingSettings source)
    {
        return new LightingSettings
        {
            skyColor = source.skyColor,
            equatorColor = source.equatorColor,
            groundColor = source.groundColor
        };
    }
    #endregion
}
