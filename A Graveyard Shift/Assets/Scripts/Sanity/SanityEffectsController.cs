using System;
using UnityEngine;

/// <summary>
/// VHS effect settings for a sanity threshold level.
/// </summary>
[System.Serializable]
public class VHSEffectLevel
{
    [Tooltip("Glitch intensity (normal is ~0.25)")]
    [Range(0f, 1f)]
    public float glitchIntensity = 0.3f;

    [Tooltip("RGB shift amount (normal is ~0.012)")]
    [Range(0f, 0.1f)]
    public float rgbShift = 0.02f;

    [Tooltip("Noise intensity (normal is ~0.08)")]
    [Range(0f, 0.5f)]
    public float noiseIntensity = 0.1f;

    [Tooltip("Scanline intensity (normal is ~0.45)")]
    [Range(0f, 1f)]
    public float scanlineIntensity = 0.5f;

    [Tooltip("Tracking noise (normal is ~0.03)")]
    [Range(0f, 0.2f)]
    public float trackingNoise = 0.05f;
}

/// <summary>
/// Controls visual and gameplay effects based on sanity thresholds.
/// Handles VHS intensification, sprint removal, crucifix flipping, and dialogue triggers.
/// </summary>
public class SanityEffectsController : MonoBehaviour
{
    #region Singleton
    public static SanityEffectsController Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("VHS Effect Toggles")]
    [Tooltip("Enable VHS effect changes at sanity 75")]
    [SerializeField] private bool enableLevel1VHS = true;

    [Tooltip("Enable VHS effect changes at sanity 50")]
    [SerializeField] private bool enableLevel2VHS = true;

    [Tooltip("Enable VHS effect changes at sanity 25")]
    [SerializeField] private bool enableLevel3VHS = true;

    [Header("VHS Effect Levels")]
    [Tooltip("Normal VHS settings (full sanity)")]
    [SerializeField] private VHSEffectLevel normalLevel = new VHSEffectLevel
    {
        glitchIntensity = 0.25f,
        rgbShift = 0.012f,
        noiseIntensity = 0.08f,
        scanlineIntensity = 0.45f,
        trackingNoise = 0.03f
    };

    [Tooltip("VHS settings at sanity 75")]
    [SerializeField] private VHSEffectLevel level1 = new VHSEffectLevel
    {
        glitchIntensity = 0.35f,
        rgbShift = 0.02f,
        noiseIntensity = 0.12f,
        scanlineIntensity = 0.55f,
        trackingNoise = 0.05f
    };

    [Tooltip("VHS settings at sanity 50")]
    [SerializeField] private VHSEffectLevel level2 = new VHSEffectLevel
    {
        glitchIntensity = 0.5f,
        rgbShift = 0.03f,
        noiseIntensity = 0.18f,
        scanlineIntensity = 0.65f,
        trackingNoise = 0.08f
    };

    [Tooltip("VHS settings at sanity 25")]
    [SerializeField] private VHSEffectLevel level3 = new VHSEffectLevel
    {
        glitchIntensity = 0.7f,
        rgbShift = 0.045f,
        noiseIntensity = 0.25f,
        scanlineIntensity = 0.8f,
        trackingNoise = 0.12f
    };

    [Header("VHS Effect Reference")]
    [Tooltip("Reference to VHSRetroFeature component on camera (auto-finds if not set)")]
    [SerializeField] private VHSRetroFeature vhsEffect;

    [Header("Sprint Control")]
    [Tooltip("Reference to player Movement component (auto-finds if not set)")]
    [SerializeField] private Movement playerMovement;

    [Header("Dialogue Settings")]
    [Tooltip("Dialogue text to show at sanity 25")]
    [TextArea(2, 4)]
    [SerializeField] private string lowSanityDialogue = "I can't... I can't think straight...";

    [Tooltip("Speaker name for low sanity dialogue")]
    [SerializeField] private string lowSanityDialogueSpeaker = "";

    [Tooltip("Duration to show dialogue")]
    [SerializeField] private float dialogueDuration = 3f;

    [Header("Threshold IDs (must match SanityManager)")]
    [SerializeField] private string threshold75Id = "vhs_level_1";
    [SerializeField] private string threshold50Id = "vhs_level_2_crucifix";
    [SerializeField] private string threshold25Id = "vhs_level_3_sprint";
    #endregion

    #region Properties
    /// <summary>Returns true if sprint is currently disabled by the sanity system.</summary>
    public bool IsSprintDisabledBySanity => sprintDisabled;
    #endregion

    #region Private Fields
    private int currentEffectLevel = 0;
    private bool sprintDisabled = false;
    private bool wasSprintEnabled = true;
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
        // Auto-find VHS effect if not assigned
        if (vhsEffect == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Try to find VHSRetroFeature component directly
                vhsEffect = mainCam.GetComponent<VHSRetroFeature>();
                if (vhsEffect != null)
                {
                    Debug.Log("SanityEffectsController: Found VHSRetroFeature on main camera");
                }
                else
                {
                    // Try in children
                    vhsEffect = mainCam.GetComponentInChildren<VHSRetroFeature>();
                    if (vhsEffect != null)
                    {
                        Debug.Log("SanityEffectsController: Found VHSRetroFeature in camera children");
                    }
                }
            }

            if (vhsEffect == null)
            {
                // Try to find anywhere in scene
                vhsEffect = FindFirstObjectByType<VHSRetroFeature>();
                if (vhsEffect != null)
                {
                    Debug.Log($"SanityEffectsController: Found VHSRetroFeature on '{vhsEffect.gameObject.name}'");
                }
                else
                {
                    Debug.LogWarning("SanityEffectsController: Could not find VHSRetroFeature anywhere in scene!");
                }
            }
        }
        else
        {
            Debug.Log("SanityEffectsController: VHS effect manually assigned");
        }

        // Auto-find player movement if not assigned
        if (playerMovement == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerMovement = player.GetComponent<Movement>();
            }
        }

        // Subscribe to sanity events
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnThresholdCrossedDown += HandleThresholdDown;
            SanityManager.Instance.OnThresholdCrossedUp += HandleThresholdUp;
            Debug.Log("SanityEffectsController: Subscribed to sanity threshold events");
        }
        else
        {
            Debug.LogWarning("SanityEffectsController: SanityManager.Instance is null - cannot subscribe to events!");
        }
    }

    private void OnDestroy()
    {
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnThresholdCrossedDown -= HandleThresholdDown;
            SanityManager.Instance.OnThresholdCrossedUp -= HandleThresholdUp;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Manually set VHS effect level (0 = normal, 1-3 = threshold levels).
    /// </summary>
    public void SetVHSLevel(int level)
    {
        currentEffectLevel = Mathf.Clamp(level, 0, 3);
        ApplyVHSEffects(GetVHSLevelSettings(currentEffectLevel));
    }

    /// <summary>
    /// Disable player sprint.
    /// </summary>
    public void DisableSprint()
    {
        if (playerMovement != null && !sprintDisabled)
        {
            wasSprintEnabled = playerMovement.canRun;
            playerMovement.canRun = false;
            sprintDisabled = true;
            Debug.Log("SanityEffectsController: Sprint disabled");
        }
    }

    /// <summary>
    /// Re-enable player sprint.
    /// </summary>
    public void EnableSprint()
    {
        if (playerMovement != null && sprintDisabled)
        {
            playerMovement.canRun = wasSprintEnabled;
            sprintDisabled = false;
            Debug.Log("SanityEffectsController: Sprint re-enabled");
        }
    }

    /// <summary>
    /// Show the low sanity dialogue.
    /// </summary>
    public void ShowLowSanityDialogue()
    {
        if (!string.IsNullOrEmpty(lowSanityDialogue))
        {
            SimpleDialogueTrigger.ShowDialogue(lowSanityDialogue, lowSanityDialogueSpeaker, dialogueDuration);
        }
    }

    /// <summary>
    /// Reset all effects to normal state.
    /// </summary>
    public void ResetEffects()
    {
        SetVHSLevel(0);
        EnableSprint();
        currentEffectLevel = 0;

        // Reset crucifix if needed
        if (CrucifixController.Instance != null)
        {
            CrucifixController.Instance.SetFlipped(false);
        }
    }
    #endregion

    #region Private Methods
    private void HandleThresholdDown(string thresholdId)
    {
        Debug.Log($"SanityEffectsController: Handling threshold down - {thresholdId}");

        if (thresholdId == threshold75Id)
        {
            // Sanity 75: VHS level 1
            if (enableLevel1VHS)
            {
                SetVHSLevel(1);
            }
        }
        else if (thresholdId == threshold50Id)
        {
            // Sanity 50: VHS level 2 + flip crucifix
            if (enableLevel2VHS)
            {
                SetVHSLevel(2);
            }
            FlipCrucifix(true);
        }
        else if (thresholdId == threshold25Id)
        {
            // Sanity 25: VHS level 3 + disable sprint + dialogue
            if (enableLevel3VHS)
            {
                SetVHSLevel(3);
            }
            DisableSprint();
            ShowLowSanityDialogue();
        }
    }

    private void HandleThresholdUp(string thresholdId)
    {
        Debug.Log($"SanityEffectsController: Handling threshold up (recovery) - {thresholdId}");

        if (thresholdId == threshold75Id)
        {
            // Recovered above 75: back to normal
            SetVHSLevel(0);
        }
        else if (thresholdId == threshold50Id)
        {
            // Recovered above 50: VHS level 1 (if enabled), unflip crucifix
            if (enableLevel1VHS)
            {
                SetVHSLevel(1);
            }
            else
            {
                SetVHSLevel(0);
            }
            FlipCrucifix(false);
        }
        else if (thresholdId == threshold25Id)
        {
            // Recovered above 25: VHS level 2 (if enabled), re-enable sprint
            if (enableLevel2VHS)
            {
                SetVHSLevel(2);
            }
            else if (enableLevel1VHS)
            {
                SetVHSLevel(1);
            }
            else
            {
                SetVHSLevel(0);
            }
            EnableSprint();
        }
    }

    private VHSEffectLevel GetVHSLevelSettings(int level)
    {
        return level switch
        {
            1 => level1,
            2 => level2,
            3 => level3,
            _ => normalLevel
        };
    }

    private void ApplyVHSEffects(VHSEffectLevel settings)
    {
        if (vhsEffect == null)
        {
            Debug.LogWarning("SanityEffectsController: VHS effect component not found!");
            return;
        }

        // Directly set VHS properties
        vhsEffect.glitchIntensity = settings.glitchIntensity;
        vhsEffect.rgbShiftAmount = settings.rgbShift;
        vhsEffect.noiseIntensity = settings.noiseIntensity;
        vhsEffect.scanlineIntensity = settings.scanlineIntensity;
        vhsEffect.trackingNoise = settings.trackingNoise;

        Debug.Log($"SanityEffectsController: Applied VHS level {currentEffectLevel} - glitch:{settings.glitchIntensity}, rgb:{settings.rgbShift}, noise:{settings.noiseIntensity}, scanline:{settings.scanlineIntensity}, tracking:{settings.trackingNoise}");
    }

    private void FlipCrucifix(bool flipped)
    {
        if (CrucifixController.Instance != null)
        {
            CrucifixController.Instance.SetFlipped(flipped);
            Debug.Log($"SanityEffectsController: Crucifix flipped = {flipped}");
        }
    }
    #endregion
}
