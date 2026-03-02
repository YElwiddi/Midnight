using System;
using UnityEngine;

/// <summary>
/// Controls a lamp that can be lit or unlit.
/// Unlit lamps contribute to sanity drain during specific phases.
/// </summary>
public class LampController : MonoBehaviour
{
    #region Inspector Settings
    [Header("Lamp State")]
    [Tooltip("Is the lamp currently lit?")]
    [SerializeField] private bool isLit = true;

    [Header("Light Components")]
    [Tooltip("Light component for this lamp (auto-finds if not set)")]
    [SerializeField] private Light lampLight;

    [Tooltip("Optional emissive renderer to toggle")]
    [SerializeField] private Renderer emissiveRenderer;

    [Tooltip("Emissive material property name")]
    [SerializeField] private string emissivePropertyName = "_EmissionColor";

    [Header("Light Settings")]
    [Tooltip("Light intensity when lit")]
    [SerializeField] private float litIntensity = 1f;

    [Tooltip("Light color when lit")]
    [SerializeField] private Color litColor = Color.white;

    [Tooltip("Emission color when lit")]
    [SerializeField] private Color litEmissionColor = Color.yellow;

    [Header("Interaction")]
    [Tooltip("If true, player can interact to toggle lamp")]
    [SerializeField] private bool playerCanToggle = true;

    [Tooltip("If true, lamp can be turned off by events/scripts")]
    [SerializeField] private bool canBeTurnedOff = true;

    [Header("Audio")]
    [Tooltip("Sound when lamp turns on")]
    [SerializeField] private AudioClip turnOnSound;

    [Tooltip("Sound when lamp turns off")]
    [SerializeField] private AudioClip turnOffSound;

    [Tooltip("Volume for lamp sounds")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;

    [Header("Registration")]
    [Tooltip("If true, registers with SanityManager for lamp drain tracking")]
    [SerializeField] private bool registerWithSanityManager = true;
    #endregion

    #region Events
    /// <summary>Fired when lamp state changes. Parameter is new lit state.</summary>
    public event Action<bool> OnLampStateChanged;
    #endregion

    #region Properties
    /// <summary>Is the lamp currently lit?</summary>
    public bool IsLit => isLit;

    /// <summary>Can player toggle this lamp?</summary>
    public bool PlayerCanToggle => playerCanToggle;
    #endregion

    #region Private Fields
    private AudioSource audioSource;
    private Material emissiveMaterial;
    private bool isRegistered = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Auto-find light if not assigned
        if (lampLight == null)
        {
            lampLight = GetComponentInChildren<Light>();
        }

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (turnOnSound != null || turnOffSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }

        // Cache emissive material
        if (emissiveRenderer != null)
        {
            emissiveMaterial = emissiveRenderer.material;
        }
    }

    private void Start()
    {
        // NOTE: LampController is deprecated. Use LanternInteractable with registerWithSanityManager instead.
        // Registration removed - SanityManager now only accepts LanternInteractable.

        // Apply initial state
        ApplyLampState(false);
    }

    private void OnDestroy()
    {
        // No cleanup needed - registration was removed
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Turn the lamp on.
    /// </summary>
    public void TurnOn()
    {
        if (isLit) return;

        isLit = true;
        ApplyLampState(true);
        OnLampStateChanged?.Invoke(true);

        Debug.Log($"LampController '{gameObject.name}': Turned ON");
    }

    /// <summary>
    /// Turn the lamp off.
    /// </summary>
    public void TurnOff()
    {
        if (!isLit || !canBeTurnedOff) return;

        isLit = false;
        ApplyLampState(true);
        OnLampStateChanged?.Invoke(false);

        Debug.Log($"LampController '{gameObject.name}': Turned OFF");
    }

    /// <summary>
    /// Toggle lamp state.
    /// </summary>
    public void Toggle()
    {
        if (isLit)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    /// <summary>
    /// Set lamp state directly.
    /// </summary>
    public void SetLit(bool lit, bool playSound = true)
    {
        if (isLit == lit) return;

        isLit = lit;
        ApplyLampState(playSound);
        OnLampStateChanged?.Invoke(isLit);

        Debug.Log($"LampController '{gameObject.name}': Set to {(lit ? "ON" : "OFF")}");
    }

    /// <summary>
    /// Called when player interacts with the lamp.
    /// </summary>
    public void OnPlayerInteract()
    {
        if (playerCanToggle)
        {
            Toggle();
        }
    }
    #endregion

    #region Private Methods
    private void ApplyLampState(bool playSound)
    {
        // Update light component
        if (lampLight != null)
        {
            lampLight.enabled = isLit;
            if (isLit)
            {
                lampLight.intensity = litIntensity;
                lampLight.color = litColor;
            }
        }

        // Update emissive material
        if (emissiveMaterial != null)
        {
            if (isLit)
            {
                emissiveMaterial.EnableKeyword("_EMISSION");
                emissiveMaterial.SetColor(emissivePropertyName, litEmissionColor);
            }
            else
            {
                emissiveMaterial.SetColor(emissivePropertyName, Color.black);
            }
        }

        // Play sound
        if (playSound && audioSource != null)
        {
            AudioClip clip = isLit ? turnOnSound : turnOffSound;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, soundVolume);
            }
        }
    }
    #endregion
}
