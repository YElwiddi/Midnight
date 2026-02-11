using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configures how an event affects the ambient sound.
/// </summary>
[System.Serializable]
public class EventAudioSettings
{
    [Tooltip("The event that will affect ambient audio")]
    public GameEvent gameEvent;

    [Tooltip("If true, completely mutes the audio. If false, ducks to the specified volume.")]
    public bool muteCompletely = true;

    [Tooltip("Volume to duck to (only used if muteCompletely is false)")]
    [Range(0f, 1f)]
    public float duckVolume = 0.1f;
}

/// <summary>
/// Configures how a killer event affects the ambient sound.
/// </summary>
[System.Serializable]
public class KillerEventAudioSettings
{
    [Tooltip("The killer event that will affect ambient audio")]
    public ConditionalKillerEvent killerEvent;

    [Tooltip("If true, completely mutes the audio. If false, ducks to the specified volume.")]
    public bool muteCompletely = true;

    [Tooltip("Volume to duck to (only used if muteCompletely is false)")]
    [Range(0f, 1f)]
    public float duckVolume = 0.1f;
}

public class AmbientSoundManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip ambientClip;
    [Range(0f, 1f)] public float outdoorVolume = 1f;
    [Range(0f, 1f)] public float indoorVolume = 0.2f;

    [Header("Transition")]
    public float fadeSpeed = 2f;

    [Header("Events That Affect Audio")]
    [Tooltip("Drag GameEvent assets here to automatically mute/duck audio when they play")]
    public EventAudioSettings[] eventSettings;

    [Tooltip("Drag ConditionalKillerEvent assets here to automatically mute/duck audio when they play")]
    public KillerEventAudioSettings[] killerEventSettings;

    private AudioSource audioSource;
    private float targetVolume;
    private bool isIndoor;
    private AudioClip previousAmbientClip;
    private float? volumeOverride = null;

    // Track what's currently muting/ducking the audio
    private HashSet<string> muteRequests = new HashSet<string>();
    private Dictionary<string, float> duckRequests = new Dictionary<string, float>();

    // Cache event names for quick lookup (both types use the same lookup)
    private Dictionary<string, bool> muteEventLookup = new Dictionary<string, bool>();
    private Dictionary<string, float> duckEventLookup = new Dictionary<string, float>();

    private static AmbientSoundManager instance;
    public static AmbientSoundManager Instance => instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = ambientClip;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D sound - heard everywhere equally
        audioSource.volume = outdoorVolume;
        targetVolume = outdoorVolume;
        audioSource.Play();

        // Build lookup tables for event names
        if (eventSettings != null)
        {
            foreach (var setting in eventSettings)
            {
                if (setting.gameEvent != null)
                {
                    string eventName = setting.gameEvent.eventName;
                    if (setting.muteCompletely)
                    {
                        muteEventLookup[eventName] = true;
                    }
                    else
                    {
                        duckEventLookup[eventName] = setting.duckVolume;
                    }
                }
            }
        }

        // Add killer events to the same lookups
        if (killerEventSettings != null)
        {
            foreach (var setting in killerEventSettings)
            {
                if (setting.killerEvent != null)
                {
                    string eventName = setting.killerEvent.eventName;
                    if (setting.muteCompletely)
                    {
                        muteEventLookup[eventName] = true;
                        Debug.Log($"AmbientSoundManager: Registered killer event '{eventName}' for MUTE");
                    }
                    else
                    {
                        duckEventLookup[eventName] = setting.duckVolume;
                        Debug.Log($"AmbientSoundManager: Registered killer event '{eventName}' for DUCK ({setting.duckVolume})");
                    }
                }
            }
        }

        Debug.Log($"AmbientSoundManager: Total mute events: {muteEventLookup.Count}, duck events: {duckEventLookup.Count}");
    }

    void Start()
    {
        TrySubscribeToEvents();
    }

    private bool hasSubscribed = false;

    private void TrySubscribeToEvents()
    {
        if (hasSubscribed) return;

        if (GameEventsManager.instance != null && GameEventsManager.instance.gameFlowEvents != null)
        {
            GameEventsManager.instance.gameFlowEvents.onEventStarted += OnEventStarted;
            GameEventsManager.instance.gameFlowEvents.onEventCompleted += OnEventCompleted;
            hasSubscribed = true;
            Debug.Log("AmbientSoundManager: Successfully subscribed to GameFlowEvents");
        }
    }

    void LateUpdate()
    {
        // Keep trying to subscribe until successful (handles initialization order issues)
        if (!hasSubscribed)
        {
            TrySubscribeToEvents();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from game events
        if (GameEventsManager.instance != null && GameEventsManager.instance.gameFlowEvents != null)
        {
            GameEventsManager.instance.gameFlowEvents.onEventStarted -= OnEventStarted;
            GameEventsManager.instance.gameFlowEvents.onEventCompleted -= OnEventCompleted;
        }
    }

    void Update()
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, fadeSpeed * Time.deltaTime);
        }
    }

    private void OnEventStarted(string eventName)
    {
        Debug.Log($"AmbientSoundManager: Event started - '{eventName}'");

        // Check if this event should mute
        if (muteEventLookup.ContainsKey(eventName))
        {
            Debug.Log($"AmbientSoundManager: Muting for event '{eventName}'");
            Mute($"event_{eventName}");
        }
        // Check if this event should duck
        else if (duckEventLookup.TryGetValue(eventName, out float duckVolume))
        {
            Debug.Log($"AmbientSoundManager: Ducking to {duckVolume} for event '{eventName}'");
            Duck($"event_{eventName}", duckVolume);
        }
        else
        {
            Debug.Log($"AmbientSoundManager: Event '{eventName}' not in audio settings, ignoring");
        }
    }

    private void OnEventCompleted(string eventName)
    {
        // Remove mute/duck if this event was affecting audio
        if (muteEventLookup.ContainsKey(eventName) || duckEventLookup.ContainsKey(eventName))
        {
            Unmute($"event_{eventName}");
            Unduck($"event_{eventName}");
        }
    }

    /// <summary>
    /// Call when player enters an indoor area
    /// </summary>
    public void EnterIndoor()
    {
        isIndoor = true;
        UpdateTargetVolume();
    }

    /// <summary>
    /// Call when player exits to outdoor area
    /// </summary>
    public void ExitIndoor()
    {
        isIndoor = false;
        UpdateTargetVolume();
    }

    /// <summary>
    /// Mute the ambient sound completely. Use a unique requestId so multiple systems can mute without conflict.
    /// </summary>
    /// <param name="requestId">Unique identifier (e.g., "jumpscare", "cutscene", "dialogue")</param>
    public void Mute(string requestId)
    {
        muteRequests.Add(requestId);
        UpdateTargetVolume();
    }

    /// <summary>
    /// Remove a mute request. Sound only restores when ALL mute requests are removed.
    /// </summary>
    /// <param name="requestId">The same identifier used when calling Mute()</param>
    public void Unmute(string requestId)
    {
        muteRequests.Remove(requestId);
        UpdateTargetVolume();
    }

    /// <summary>
    /// Duck (lower) the ambient sound to a specific volume. Useful for dialogue where you want quiet but not silent.
    /// </summary>
    /// <param name="requestId">Unique identifier (e.g., "dialogue")</param>
    /// <param name="volume">Target volume (0-1)</param>
    public void Duck(string requestId, float volume)
    {
        duckRequests[requestId] = Mathf.Clamp01(volume);
        UpdateTargetVolume();
    }

    /// <summary>
    /// Remove a duck request. Volume only restores when ALL duck requests are removed.
    /// </summary>
    /// <param name="requestId">The same identifier used when calling Duck()</param>
    public void Unduck(string requestId)
    {
        duckRequests.Remove(requestId);
        UpdateTargetVolume();
    }

    /// <summary>
    /// Check if the ambient sound is currently muted
    /// </summary>
    public bool IsMuted => muteRequests.Count > 0;

    /// <summary>
    /// Check if player is currently indoors
    /// </summary>
    public bool IsIndoor => isIndoor;

    private void UpdateTargetVolume()
    {
        // Mute takes priority over everything
        if (muteRequests.Count > 0)
        {
            targetVolume = 0f;
            return;
        }

        // Get base volume - use override if set, otherwise indoor/outdoor
        float baseVolume = volumeOverride ?? (isIndoor ? indoorVolume : outdoorVolume);

        // Apply the lowest duck request if any exist
        if (duckRequests.Count > 0)
        {
            float lowestDuck = float.MaxValue;
            string lowestDuckSource = "";
            foreach (var duck in duckRequests)
            {
                if (duck.Value < lowestDuck)
                {
                    lowestDuck = duck.Value;
                    lowestDuckSource = duck.Key;
                }
            }
            Debug.Log($"AmbientSoundManager: Audio ducked to {lowestDuck} by '{lowestDuckSource}' (active duck requests: {duckRequests.Count})");
            targetVolume = Mathf.Min(baseVolume, lowestDuck);
            return;
        }

        targetVolume = baseVolume;
    }

    /// <summary>
    /// Change the ambient clip at runtime
    /// </summary>
    public void SetAmbientClip(AudioClip newClip, bool restartIfSame = false)
    {
        if (audioSource.clip == newClip && !restartIfSame) return;

        audioSource.clip = newClip;
        if (newClip != null)
        {
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Change the ambient clip and remember the previous one for later restoration
    /// </summary>
    /// <param name="newClip">The new ambient clip to play</param>
    /// <param name="volume">Optional volume override (-1 = use default indoor/outdoor volume)</param>
    public void SetAmbientClipWithMemory(AudioClip newClip, float volume = -1f)
    {
        Debug.Log($"AmbientSoundManager: SetAmbientClipWithMemory called with clip: {(newClip != null ? newClip.name : "null")}, volume: {volume}");

        if (audioSource.clip == newClip && volume < 0)
        {
            Debug.Log("AmbientSoundManager: Clip is already playing, skipping");
            return;
        }

        previousAmbientClip = audioSource.clip;
        Debug.Log($"AmbientSoundManager: Stored previous clip: {(previousAmbientClip != null ? previousAmbientClip.name : "null")}");

        // Set volume override if specified
        if (volume >= 0)
        {
            volumeOverride = volume;
            UpdateTargetVolume();
        }

        SetAmbientClip(newClip);
        Debug.Log($"AmbientSoundManager: Now playing: {(audioSource.clip != null ? audioSource.clip.name : "null")}, Volume: {audioSource.volume}, Target: {targetVolume}, IsIndoor: {isIndoor}");
        if (duckRequests.Count > 0)
        {
            foreach (var duck in duckRequests)
            {
                Debug.Log($"AmbientSoundManager: Active duck request - '{duck.Key}' at volume {duck.Value}");
            }
        }
    }

    /// <summary>
    /// Restore the previously remembered ambient clip and clear volume override
    /// </summary>
    public void RestorePreviousAmbientClip()
    {
        if (previousAmbientClip != null)
        {
            volumeOverride = null;
            UpdateTargetVolume();
            SetAmbientClip(previousAmbientClip);
            previousAmbientClip = null;
        }
    }
}
