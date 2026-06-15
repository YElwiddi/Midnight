using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// Options panel for adjusting game settings like volume.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Graphics Settings")]
    public Toggle fullscreenToggle;
    public Dropdown qualityDropdown;

    [Header("Resolution")]
    public TextMeshProUGUI resolutionLabel;
    public Button resolutionLeftButton;
    public Button resolutionRightButton;

    [Header("Sensitivity")]
    public Slider mouseSensitivitySlider;

    [Header("Brightness")]
    public Slider brightnessSlider;

    // PlayerPrefs keys
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MOUSE_SENSITIVITY_KEY = "MouseSensitivity";
    private const string FULLSCREEN_KEY = "Fullscreen";
    private const string QUALITY_KEY = "QualityLevel";
    private const string RES_WIDTH_KEY = "ResWidth";
    private const string RES_HEIGHT_KEY = "ResHeight";

    private Resolution[] resolutionOptions;
    private int currentResolutionIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettingsOnLaunch()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 0.75f);

        bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
        FullScreenMode mode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (PlayerPrefs.HasKey(RES_WIDTH_KEY) && PlayerPrefs.HasKey(RES_HEIGHT_KEY))
        {
            int w = PlayerPrefs.GetInt(RES_WIDTH_KEY);
            int h = PlayerPrefs.GetInt(RES_HEIGHT_KEY);
            Screen.SetResolution(w, h, mode);
        }
        else
        {
            Screen.fullScreenMode = mode;
        }
    }

    void Start()
    {
        LoadSettings();
        SetupListeners();
        SetupResolutions();
        CreateBrightnessDisclaimer();
    }

    private void SetupListeners()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        if (qualityDropdown != null)
        {
            // Populate quality levels
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);

        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
    }

    private void LoadSettings()
    {
        // Load master volume
        if (masterVolumeSlider != null)
        {
            float masterVol = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 0.75f);
            masterVolumeSlider.value = masterVol;
            SetMasterVolume(masterVol);
        }

        // Load music volume
        if (musicVolumeSlider != null)
        {
            float musicVol = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.75f);
            musicVolumeSlider.value = musicVol;
            SetMusicVolume(musicVol);
        }

        // Load SFX volume
        if (sfxVolumeSlider != null)
        {
            float sfxVol = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.75f);
            sfxVolumeSlider.value = sfxVol;
            SetSFXVolume(sfxVol);
        }

        // Load fullscreen
        if (fullscreenToggle != null)
        {
            bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
            fullscreenToggle.isOn = isFullscreen;
        }

        // Load quality
        if (qualityDropdown != null)
        {
            int qualityLevel = PlayerPrefs.GetInt(QUALITY_KEY, QualitySettings.GetQualityLevel());
            qualityDropdown.value = qualityLevel;
        }

        // Load mouse sensitivity
        if (mouseSensitivitySlider != null)
        {
            float sensitivity = PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, 2f);
            mouseSensitivitySlider.value = sensitivity;
        }

        // Load brightness (drives the gameplay ambient sky; see BrightnessSettings).
        if (brightnessSlider != null)
            brightnessSlider.value = BrightnessSettings.Value;
    }

    public void SetMasterVolume(float value)
    {
        // AudioListener.volume controls ALL audio in the game globally
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, value);
    }

    public void SetMusicVolume(float value)
    {
        if (audioMixer != null)
        {
            float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
            audioMixer.SetFloat("MusicVolume", dB);
        }
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
    }

    public void SetSFXVolume(float value)
    {
        if (audioMixer != null)
        {
            float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
            audioMixer.SetFloat("SFXVolume", dB);
        }
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        FullScreenMode mode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (resolutionOptions != null && resolutionOptions.Length > 0)
        {
            Resolution r = resolutionOptions[currentResolutionIndex];
            Screen.SetResolution(r.width, r.height, mode);
        }
        else
        {
            Screen.fullScreenMode = mode;
        }

        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
    }

    private void SetupResolutions()
    {
        // Build a de-duplicated list (one entry per unique width x height).
        Resolution[] all = Screen.resolutions;
        var list = new System.Collections.Generic.List<Resolution>();
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < all.Length; i++)
        {
            string key = all[i].width + "x" + all[i].height;
            if (seen.Add(key)) list.Add(all[i]);
        }

        // Fallback if the platform reports nothing usable.
        if (list.Count == 0)
        {
            Resolution cur = new Resolution { width = Screen.width, height = Screen.height };
            list.Add(cur);
        }

        resolutionOptions = list.ToArray();

        // Pick the current index from saved prefs, else the closest to the current screen.
        int savedW = PlayerPrefs.GetInt(RES_WIDTH_KEY, Screen.width);
        int savedH = PlayerPrefs.GetInt(RES_HEIGHT_KEY, Screen.height);
        currentResolutionIndex = 0;
        bool found = false;
        for (int i = 0; i < resolutionOptions.Length; i++)
        {
            if (resolutionOptions[i].width == savedW && resolutionOptions[i].height == savedH)
            {
                currentResolutionIndex = i;
                found = true;
                break;
            }
        }
        if (!found)
        {
            int best = int.MaxValue;
            for (int i = 0; i < resolutionOptions.Length; i++)
            {
                int diff = Mathf.Abs(resolutionOptions[i].width - savedW) + Mathf.Abs(resolutionOptions[i].height - savedH);
                if (diff < best) { best = diff; currentResolutionIndex = i; }
            }
        }

        UpdateResolutionLabel();

        if (resolutionLeftButton != null)
            resolutionLeftButton.onClick.AddListener(() => CycleResolution(-1));
        if (resolutionRightButton != null)
            resolutionRightButton.onClick.AddListener(() => CycleResolution(1));
    }

    public void CycleResolution(int direction)
    {
        if (resolutionOptions == null || resolutionOptions.Length == 0) return;
        int n = resolutionOptions.Length;
        currentResolutionIndex = ((currentResolutionIndex + direction) % n + n) % n;
        ApplyResolution();
        UpdateResolutionLabel();
    }

    private void ApplyResolution()
    {
        if (resolutionOptions == null || resolutionOptions.Length == 0) return;
        Resolution r = resolutionOptions[currentResolutionIndex];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt(RES_WIDTH_KEY, r.width);
        PlayerPrefs.SetInt(RES_HEIGHT_KEY, r.height);
    }

    private void UpdateResolutionLabel()
    {
        if (resolutionLabel != null && resolutionOptions != null && resolutionOptions.Length > 0)
        {
            Resolution r = resolutionOptions[currentResolutionIndex];
            resolutionLabel.text = r.width + " x " + r.height;
        }
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt(QUALITY_KEY, qualityIndex);
    }

    public void SetMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, value);

        // Apply to Movement script if player exists
        Movement movement = FindFirstObjectByType<Movement>();
        if (movement != null)
        {
            movement.lookSpeed = value;
        }
    }

    public void SetBrightness(float value)
    {
        // Drives the gameplay environment ambient sky colour. Applied when the gameplay
        // scene's LightingController initialises (this panel lives in the main menu, which
        // keeps its own lighting) — see BrightnessSettings.
        BrightnessSettings.Save(value);
    }

    public void SaveAndClose()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    public void ResetToDefaults()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.value = 0.75f;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 0.75f;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = 0.75f;
        if (fullscreenToggle != null) fullscreenToggle.isOn = true;
        if (qualityDropdown != null) qualityDropdown.value = QualitySettings.names.Length - 1;
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = 2f;
        if (brightnessSlider != null) brightnessSlider.value = BrightnessSettings.Default;

        PlayerPrefs.Save();
    }

    private const string DISCLAIMER_NAME = "BrightnessDisclaimer";

    /// <summary>
    /// Adds a small cautionary note under the brightness slider. Created at runtime so it
    /// survives a menu rebuild and ships in the build without editing the scene by hand.
    /// </summary>
    private void CreateBrightnessDisclaimer()
    {
        if (brightnessSlider == null) return;

        Transform panel = brightnessSlider.transform.parent;
        if (panel == null || panel.Find(DISCLAIMER_NAME) != null) return;

        // Grab the menu's font from an existing label before adding the new one.
        TMP_FontAsset font = null;
        var anyLabel = panel.GetComponentInChildren<TextMeshProUGUI>();
        if (anyLabel != null) font = anyLabel.font;

        var go = new GameObject(DISCLAIMER_NAME, typeof(RectTransform));
        go.transform.SetParent(panel, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 32f);
        rt.anchoredPosition = new Vector2(0f, -95f); // just under the brightness row

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = "Default brightness is strongly recommended.";
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Italic;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.86f, 0.78f, 0.5f, 1f); // soft amber caution
        tmp.raycastTarget = false;
    }
}
