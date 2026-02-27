using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

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
    private const string BRIGHTNESS_KEY = "Brightness";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyMasterVolumeOnLaunch()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 0.75f);
    }

    void Start()
    {
        LoadSettings();
        SetupListeners();
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

        // Load brightness
        if (brightnessSlider != null)
        {
            float bright = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 0.5f);
            brightnessSlider.value = bright;
            SetBrightness(bright);
        }
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
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
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
        // Slider goes 0-1, map to VHS brightness range (-0.2 to 0.2)
        float vhsBrightness = Mathf.Lerp(-0.2f, 0.2f, value);
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, value);

        VHSRetroFeature vhs = FindFirstObjectByType<VHSRetroFeature>();
        if (vhs != null)
        {
            vhs.brightness = vhsBrightness;
        }
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
        if (brightnessSlider != null) brightnessSlider.value = 0.5f;

        PlayerPrefs.Save();
    }
}
