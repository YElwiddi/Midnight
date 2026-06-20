using UnityEngine;

public class SimpleFlashlight : MonoBehaviour
{
    [Header("Flashlight Components")]
    public Light spotLight;
    public AudioSource audioSource;
    
    [Header("Flashlight Settings")]
    public bool startsEnabled = false;
    public KeyCode toggleKey = KeyCode.F;
    
    [Header("Light Settings")]
    public float intensity = 2.5f;
    public float range = 20f;
    public float spotAngle = 55f;
    public Color lightColor = Color.white;
    
    [Header("Audio")]
    public AudioClip toggleOnSound;
    public AudioClip toggleOffSound;

    [Header("Killer Proximity Flicker")]
    [Tooltip("Enable flickering when a killer is nearby")]
    public bool enableKillerFlicker = true;
    [Tooltip("Distance at which flickering starts")]
    public float flickerStartDistance = 15f;
    [Tooltip("Distance at which flickering is most intense")]
    public float flickerMaxDistance = 5f;
    [Tooltip("How fast the light flickers (higher = faster)")]
    public float flickerSpeed = 20f;
    [Tooltip("Minimum intensity multiplier during flicker (0 = fully off)")]
    [Range(0f, 1f)]
    public float flickerMinIntensity = 0.1f;

    // Private variables
    private bool isOn = false;
    private Camera playerCamera;

    // Flicker state
    private float flickerTimer = 0f;
    private CryptKiller[] cryptKillers;
    private KillerNPC[] killerNPCs;
    private FatherKillerSequence[] fatherKillers;
    private float lastKillerCheckTime = 0f;
    private const float KILLER_CHECK_INTERVAL = 0.5f; // Check for new killers every 0.5s

    // Killer disable state
    private bool isDisabledByKiller = false;

    // Controls disabled state (for cinematics/events)
    private bool controlsDisabled = false;
    private string disabledDialogueText;
    private string disabledDialogueSpeaker;
    private float disabledDialogueDuration;
    private float disabledDialogueTypewriterSpeed;
    private float disabledDialogueCooldown;
    private float lastDialogueTime = -999f;
    
    void Start()
    {
        // Make sure we have a spotlight assigned
        if (spotLight == null)
        {
            // Try to find it if not assigned
            spotLight = GetComponentInChildren<Light>();
            
            // If still not found, create a new spot light
            if (spotLight == null)
            {
                // Create a new light game object as a child of this object
                GameObject lightObj = new GameObject("SpotLight");
                lightObj.transform.SetParent(transform, false);
                
                // Don't worry about initial position and rotation
                // as we'll update it every frame to match the camera
                
                // Add the light component
                spotLight = lightObj.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                Debug.Log("SimpleFlashlight: Created a new spot light component");
            }
        }
        
        // Make sure it's a spotlight
        if (spotLight.type != LightType.Spot)
        {
            spotLight.type = LightType.Spot;
        }
        
        // Apply initial light settings
        ConfigureLight();
        
        // Setup audio source if needed
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && (toggleOnSound != null || toggleOffSound != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f; // 3D sound
                audioSource.volume = 0.8f;
            }
        }
        
        // Find the player camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            // Try to find another camera in case Camera.main is not set
            playerCamera = FindObjectOfType<Camera>();
        }
        
        if (playerCamera == null)
        {
            Debug.LogError("SimpleFlashlight: No camera found in the scene!");
        }
        
        // Set initial state (without playing sound)
        isOn = startsEnabled;
        SetFlashlightState(isOn, playSound: false);
    }
    
    void Update()
    {
        // Toggle flashlight with key press (if controls not disabled)
        if (!controlsDisabled && Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }

        // Update flashlight position and rotation to follow camera
        UpdateFlashlightTransform();

        // Update killer proximity flicker
        if (enableKillerFlicker && isOn && !isDisabledByKiller)
        {
            UpdateKillerFlicker();
        }
    }
    
    // Update the flashlight to follow the camera view
    private void UpdateFlashlightTransform()
    {
        if (playerCamera != null && spotLight != null)
        {
            // Option 1: Mount the spotlight directly on the camera
            // This makes the light follow exactly where the camera is looking
            spotLight.transform.position = playerCamera.transform.position;
            spotLight.transform.rotation = playerCamera.transform.rotation;

            // Optional offset to make the light come from a slightly different position
            // Uncomment and adjust these values if you want the light slightly offset from camera
            // Vector3 offset = playerCamera.transform.right * 0.2f; // Slight offset to the right
            // offset += playerCamera.transform.up * -0.1f; // Slight offset downward
            // spotLight.transform.position += offset;
        }
    }

    // Update flicker effect based on killer proximity
    private void UpdateKillerFlicker()
    {
        if (spotLight == null) return;

        float closestDistance = GetClosestKillerDistance();

        // Check if any killer is within flicker range
        if (closestDistance <= flickerStartDistance)
        {
            // Calculate flicker intensity based on distance (closer = more intense)
            // At flickerStartDistance: flickerAmount = 0 (no flicker)
            // At flickerMaxDistance or closer: flickerAmount = 1 (max flicker)
            float flickerAmount = Mathf.InverseLerp(flickerStartDistance, flickerMaxDistance, closestDistance);

            // Update flicker timer
            flickerTimer += Time.deltaTime * flickerSpeed;

            // Generate flicker value using multiple sine waves for irregular pattern
            float flicker1 = Mathf.Sin(flickerTimer * 1.0f);
            float flicker2 = Mathf.Sin(flickerTimer * 2.3f) * 0.5f;
            float flicker3 = Mathf.Sin(flickerTimer * 5.7f) * 0.3f;
            float combinedFlicker = (flicker1 + flicker2 + flicker3) / 1.8f; // Normalize to roughly -1 to 1

            // Convert to 0-1 range and apply intensity
            float flickerValue = (combinedFlicker + 1f) * 0.5f; // Now 0 to 1

            // Lerp between min intensity and full intensity based on flicker
            float minIntensityThisFrame = Mathf.Lerp(1f, flickerMinIntensity, flickerAmount);
            float currentIntensityMult = Mathf.Lerp(minIntensityThisFrame, 1f, flickerValue);

            // Apply to light
            spotLight.intensity = intensity * currentIntensityMult;
        }
        else
        {
            // No killer nearby - ensure normal intensity
            spotLight.intensity = intensity;
            flickerTimer = 0f;
        }
    }

    // Find the distance to the closest killer
    private float GetClosestKillerDistance()
    {
        // Periodically refresh killer references (in case new ones spawn)
        if (Time.time - lastKillerCheckTime > KILLER_CHECK_INTERVAL)
        {
            cryptKillers = FindObjectsOfType<CryptKiller>();
            killerNPCs = FindObjectsOfType<KillerNPC>();
            fatherKillers = FindObjectsOfType<FatherKillerSequence>();
            lastKillerCheckTime = Time.time;
        }

        float closestDistance = float.MaxValue;
        Vector3 playerPos = transform.position;

        // Check CryptKillers
        if (cryptKillers != null)
        {
            foreach (var killer in cryptKillers)
            {
                if (killer == null) continue;
                float dist = Vector3.Distance(playerPos, killer.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                }
            }
        }

        // Check KillerNPCs
        if (killerNPCs != null)
        {
            foreach (var killer in killerNPCs)
            {
                if (killer == null) continue;
                if (!killer.CausesFlashlightFlicker) continue; // this killer is excluded from flashlight flicker
                float dist = Vector3.Distance(playerPos, killer.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                }
            }
        }

        // Check FatherKiller (final secret sequence) — flickers like the Wraith once threatening
        if (fatherKillers != null)
        {
            foreach (var killer in fatherKillers)
            {
                if (killer == null) continue;
                if (!killer.CausesFlashlightFlicker) continue;
                float dist = Vector3.Distance(playerPos, killer.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                }
            }
        }

        return closestDistance;
    }
    
    // Configure the light component with our settings
    private void ConfigureLight()
    {
        if (spotLight != null)
        {
            spotLight.color = lightColor;
            spotLight.range = range;
            spotLight.spotAngle = spotAngle;
            spotLight.intensity = intensity;
        }
    }
    
    // Toggle the flashlight on/off
    public void ToggleFlashlight()
    {
        // If disabled by killer, show dialogue instead of toggling
        if (isDisabledByKiller)
        {
            ShowDisabledDialogue();
            return;
        }

        isOn = !isOn;
        SetFlashlightState(isOn);
    }
    
    // Set the flashlight to a specific state
    public void SetFlashlightState(bool state, bool playSound = true)
    {
        isOn = state;

        if (spotLight != null)
        {
            spotLight.enabled = isOn;

            // Reset intensity when turning on (flicker will adjust if needed)
            if (isOn)
            {
                spotLight.intensity = intensity;
                flickerTimer = 0f;
            }

            // Play appropriate sound
            if (playSound && audioSource != null)
            {
                if (isOn && toggleOnSound != null)
                {
                    audioSource.clip = toggleOnSound;
                    audioSource.Play();
                }
                else if (!isOn && toggleOffSound != null)
                {
                    audioSource.clip = toggleOffSound;
                    audioSource.Play();
                }
            }
        }
    }
    
    // Public method to check if flashlight is on
    public bool IsFlashlightOn()
    {
        return isOn;
    }

    /// <summary>
    /// Permanently disables the flashlight (used by FlashlightDisabler killer).
    /// When player tries to use it, shows the specified dialogue.
    /// </summary>
    public void DisableByKiller(string dialogueText, string speakerName = "", float dialogueDuration = 2f, float typewriterSpeed = 30f, float cooldown = 5f)
    {
        // Turn off the flashlight
        SetFlashlightState(false);

        // Set up the disabled state
        isDisabledByKiller = true;
        disabledDialogueText = dialogueText;
        disabledDialogueSpeaker = speakerName;
        disabledDialogueDuration = dialogueDuration;
        disabledDialogueTypewriterSpeed = typewriterSpeed;
        disabledDialogueCooldown = cooldown;

        Debug.Log("SimpleFlashlight: Disabled by killer - will show dialogue on toggle attempt");
    }

    /// <summary>
    /// Shows dialogue when player tries to use the disabled flashlight.
    /// </summary>
    private void ShowDisabledDialogue()
    {
        if (string.IsNullOrEmpty(disabledDialogueText)) return;

        // Check cooldown
        if (Time.time - lastDialogueTime < disabledDialogueCooldown) return;

        // Use the unified dialogue system
        if (SimpleDialogueTrigger.ShowDialogue(disabledDialogueText, disabledDialogueSpeaker, disabledDialogueDuration, disabledDialogueTypewriterSpeed))
        {
            lastDialogueTime = Time.time;
        }
    }

    /// <summary>
    /// Check if the flashlight has been disabled by a killer.
    /// </summary>
    public bool IsDisabledByKiller()
    {
        return isDisabledByKiller;
    }

    /// <summary>
    /// Enable or disable flashlight controls (for cinematics/events).
    /// When disabled, player cannot toggle the flashlight.
    /// </summary>
    public void SetControlsEnabled(bool enabled)
    {
        controlsDisabled = !enabled;
    }

    /// <summary>
    /// Check if flashlight controls are currently enabled.
    /// </summary>
    public bool AreControlsEnabled()
    {
        return !controlsDisabled;
    }

    /// <summary>
    /// Re-enables the flashlight after it was disabled by a killer.
    /// Used by checkpoint respawn system.
    /// </summary>
    public void ReEnableAfterKillerDisable()
    {
        if (!isDisabledByKiller) return;

        isDisabledByKiller = false;
        disabledDialogueText = null;
        disabledDialogueSpeaker = null;
        lastDialogueTime = -999f;

        // Re-enable controls
        controlsDisabled = false;
        enabled = true;

        Debug.Log("SimpleFlashlight: Re-enabled after killer disable");
    }
}