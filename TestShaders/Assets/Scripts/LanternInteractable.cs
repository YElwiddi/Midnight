using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class LanternInteractable : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    public Light lanternLight;
    public bool startOn = false;

    [Header("Flicker Settings")]
    [Tooltip("Enable light flickering effect")]
    public bool enableFlicker = true;
    [Tooltip("Base intensity of the light")]
    public float baseIntensity = 1f;
    [Tooltip("Minimum intensity during flicker")]
    [Range(0f, 1f)]
    public float minIntensityMultiplier = 0.7f;
    [Tooltip("Maximum intensity during flicker")]
    [Range(1f, 2f)]
    public float maxIntensityMultiplier = 1.1f;
    [Tooltip("How often the light flickers (times per second)")]
    [Range(0.1f, 30f)]
    public float flickerFrequency = 8f;
    [Tooltip("How smooth the flicker transitions are (higher = smoother)")]
    [Range(1f, 20f)]
    public float flickerSmoothness = 5f;

    [Header("Audio")]
    public AudioClip toggleOnSound;
    [Range(0f, 1f)]
    public float toggleOnVolume = 1f;
    [Range(0.1f, 3f)]
    public float toggleOnPitch = 1f;
    public AudioClip toggleOffSound;
    [Range(0f, 1f)]
    public float toggleOffVolume = 1f;
    [Range(0.1f, 3f)]
    public float toggleOffPitch = 1f;
    private AudioSource audioSource;

    [Header("Sanity System")]
    [Tooltip("If true, registers with SanityManager for lamp drain tracking")]
    public bool registerWithSanityManager = false;

    [Tooltip("If true, LanternEventController will never automatically turn off this lantern")]
    public bool excludeFromRandomTurnOff = false;

    /// <summary>Fired when lamp state changes. Parameter is new lit state.</summary>
    public event Action<bool> OnLampStateChanged;

    /// <summary>Is the lantern currently lit?</summary>
    public bool IsLit => isOn;

    /// <summary>Should this lantern be excluded from random turn-off events?</summary>
    public bool ExcludeFromRandomTurnOff => excludeFromRandomTurnOff;

    /// <summary>Is the lantern currently locked (cannot be toggled)?</summary>
    public bool IsLocked => isLocked;

    private bool isOn;
    private bool isLocked = false;
    private Color? originalColor = null;
    private float targetIntensity;
    private float currentIntensity;
    private float flickerTimer;
    private bool isRegistered = false;

    void Start()
    {
        // Auto-find light component if not assigned
        if (lanternLight == null)
            lanternLight = GetComponentInChildren<Light>();

        // Set up audio source
        if (toggleOnSound != null || toggleOffSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        // Set initial state
        isOn = startOn;
        if (lanternLight != null)
        {
            lanternLight.enabled = isOn;
            if (baseIntensity <= 0f)
                baseIntensity = lanternLight.intensity;
            currentIntensity = baseIntensity;
            targetIntensity = baseIntensity;
        }

        // Register with SanityManager for lamp drain tracking
        if (registerWithSanityManager && SanityManager.Instance != null)
        {
            SanityManager.Instance.RegisterLamp(this);
            isRegistered = true;
        }
    }

    void OnDestroy()
    {
        // Unregister from SanityManager
        if (isRegistered && SanityManager.Instance != null)
        {
            SanityManager.Instance.UnregisterLamp(this);
        }
    }

    void Update()
    {
        if (!isOn || lanternLight == null || !enableFlicker)
            return;

        // Update flicker timer
        flickerTimer += Time.deltaTime;

        // Check if it's time to pick a new target intensity
        float flickerInterval = 1f / flickerFrequency;
        if (flickerTimer >= flickerInterval)
        {
            flickerTimer = 0f;
            targetIntensity = baseIntensity * Random.Range(minIntensityMultiplier, maxIntensityMultiplier);
        }

        // Smoothly interpolate to target intensity
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * flickerSmoothness);
        lanternLight.intensity = currentIntensity;
    }

    public void Interact()
    {
        if (isLocked) return;

        isOn = !isOn;

        if (lanternLight != null)
        {
            lanternLight.enabled = isOn;
            if (isOn)
            {
                currentIntensity = baseIntensity;
                lanternLight.intensity = baseIntensity;
            }
        }

        // Play toggle sound
        if (audioSource != null)
        {
            AudioClip clip = isOn ? toggleOnSound : toggleOffSound;
            if (clip != null)
            {
                audioSource.pitch = isOn ? toggleOnPitch : toggleOffPitch;
                float volume = isOn ? toggleOnVolume : toggleOffVolume;
                audioSource.PlayOneShot(clip, volume);
            }
        }

        // Fire state changed event
        OnLampStateChanged?.Invoke(isOn);

        // Fire global sanity event
        if (GameEventsManager.instance != null && GameEventsManager.instance.sanityEvents != null)
        {
            GameEventsManager.instance.sanityEvents.LampStateChanged(gameObject.name, isOn);
        }
    }

    /// <summary>
    /// Turn the lantern on programmatically.
    /// </summary>
    public void TurnOn()
    {
        if (isOn) return;
        Interact();
    }

    /// <summary>
    /// Turn the lantern off programmatically.
    /// </summary>
    public void TurnOff()
    {
        if (!isOn) return;
        Interact();
    }

    /// <summary>
    /// Set lantern state directly.
    /// </summary>
    public void SetLit(bool lit)
    {
        if (isOn == lit) return;
        Interact();
    }

    public string GetInteractionPrompt()
    {
        if (isLocked) return "";
        return isOn ? "Turn off lantern" : "Turn on lantern";
    }

    /// <summary>
    /// Lock the lantern so it cannot be toggled by interaction or LanternEventController.
    /// </summary>
    public void Lock()
    {
        isLocked = true;
    }

    /// <summary>
    /// Unlock the lantern so it can be toggled again.
    /// </summary>
    public void Unlock()
    {
        isLocked = false;
    }

    /// <summary>
    /// Override the lantern light color. Saves original color for restoration.
    /// </summary>
    public void SetLightColor(Color color)
    {
        if (lanternLight == null) return;

        if (originalColor == null)
        {
            originalColor = lanternLight.color;
        }
        lanternLight.color = color;
    }

    /// <summary>
    /// Restore the lantern light to its original color.
    /// </summary>
    public void RestoreLightColor()
    {
        if (lanternLight == null || originalColor == null) return;

        lanternLight.color = originalColor.Value;
        originalColor = null;
    }

    /// <summary>
    /// Turn on, lock, and set color on ALL lanterns in the scene.
    /// </summary>
    public static void LockAllLanterns(Color color)
    {
        LanternInteractable[] allLanterns = FindObjectsOfType<LanternInteractable>();
        foreach (var lantern in allLanterns)
        {
            if (!lantern.isOn)
            {
                lantern.isOn = true;
                if (lantern.lanternLight != null)
                {
                    lantern.lanternLight.enabled = true;
                    lantern.currentIntensity = lantern.baseIntensity;
                    lantern.lanternLight.intensity = lantern.baseIntensity;
                }
            }
            lantern.SetLightColor(color);
            lantern.Lock();
        }

        // Also pause the LanternEventController so it doesn't try to turn any off
        if (LanternEventController.Instance != null)
        {
            LanternEventController.Instance.Deactivate();
        }

        Debug.Log($"LanternInteractable: All lanterns locked with color {color}");
    }
}
