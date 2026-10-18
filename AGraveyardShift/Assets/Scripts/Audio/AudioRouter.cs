using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Sends every AudioSource in the game through MainMixer's Music or SFX group so the Settings
/// screen's Music and Effects sliders reach all of it. Most sources are created at runtime
/// (AddComponent / PlayClipAtPoint) without a group, so a light sweep routes any source whose
/// output is still empty: music players (BackgroundMusic, ProximityMusicPlayer, MusicSource)
/// to Music, everything else to SFX. Sources already assigned a group are left alone.
/// Master volume stays on AudioListener.volume (OptionsPanel).
/// </summary>
public class AudioRouter : MonoBehaviour
{
    public const string MixerResource = "Audio/MainMixer";
    public const string MusicParam = "MusicVolume";
    public const string SfxParam = "SFXVolume";
    public const string MusicPref = "MusicVolume";
    public const string SfxPref = "SFXVolume";
    public const float DefaultVolume = 0.75f;

    private const float SweepInterval = 0.15f;

    private static AudioRouter instance;
    private static AudioMixer mixer;
    private static AudioMixerGroup musicGroup;
    private static AudioMixerGroup sfxGroup;

    private float nextSweep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("AudioRouter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioRouter>();
    }

    private void Awake()
    {
        LoadMixer();
        ApplySavedVolumes();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private static void LoadMixer()
    {
        if (mixer != null) return;
        mixer = Resources.Load<AudioMixer>(MixerResource);
        if (mixer == null) { Debug.LogWarning("AudioRouter: no mixer at Resources/" + MixerResource); return; }
        musicGroup = FindGroup("Music");
        sfxGroup = FindGroup("SFX");
    }

    private static AudioMixerGroup FindGroup(string name)
    {
        var groups = mixer.FindMatchingGroups(name);
        foreach (var g in groups) if (g.name == name) return g;
        return null;
    }

    /// <summary>Converts a 0..1 slider value to mixer decibels.</summary>
    public static float ToDecibels(float value)
    {
        return value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
    }

    public static void ApplySavedVolumes()
    {
        SetMusicVolume(PlayerPrefs.GetFloat(MusicPref, DefaultVolume), false);
        SetSfxVolume(PlayerPrefs.GetFloat(SfxPref, DefaultVolume), false);
    }

    public static void SetMusicVolume(float value, bool save = true)
    {
        LoadMixer();
        if (mixer != null) mixer.SetFloat(MusicParam, ToDecibels(value));
        if (save) PlayerPrefs.SetFloat(MusicPref, value);
    }

    public static void SetSfxVolume(float value, bool save = true)
    {
        LoadMixer();
        if (mixer != null) mixer.SetFloat(SfxParam, ToDecibels(value));
        if (save) PlayerPrefs.SetFloat(SfxPref, value);
    }

    /// <summary>Routes a source to the Music group now (for music players created in code).</summary>
    public static void RouteMusic(AudioSource source)
    {
        LoadMixer();
        if (source != null && musicGroup != null) source.outputAudioMixerGroup = musicGroup;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Snapshot values are re-applied by the mixer on load; push the player's levels again.
        ApplySavedVolumes();
        Sweep();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSweep) return;
        nextSweep = Time.unscaledTime + SweepInterval;
        Sweep();
    }

    private static void Sweep()
    {
        if (musicGroup == null || sfxGroup == null) return;
        var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            var s = sources[i];
            if (s.outputAudioMixerGroup != null) continue;
            s.outputAudioMixerGroup = IsMusic(s) ? musicGroup : sfxGroup;
        }
    }

    private static bool IsMusic(AudioSource s)
    {
        return s.GetComponentInParent<MusicSource>(true) != null
            || s.GetComponent<BackgroundMusic>() != null
            || s.GetComponent<ProximityMusicPlayer>() != null;
    }
}
