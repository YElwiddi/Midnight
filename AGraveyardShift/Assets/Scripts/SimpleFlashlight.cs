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
    
    [Header("Beam")]
    [Tooltip("A soft beam: bright core, broad body, dim rim, instead of a hard-edged disc")]
    public bool softBeam = true;
    [Tooltip("Optional cookie for the beam. Empty = the built-in soft beam")]
    public Texture beamCookie;

    [Header("Hand-held")]
    [Tooltip("Hold the torch in the hand instead of at the eyes, so whatever it lights throws a shadow that swings as you move")]
    public bool handHeld = true;
    [Tooltip("Where the hand holds the torch, in metres from the eyes (right, up, forward). The player's scale doesn't stretch it")]
    public Vector3 handOffset = new Vector3(0.35f, -0.8f, 0.2f);
    [Tooltip("The beam aims at what you look at, no nearer than this (metres)")]
    public float aimMinDistance = 3f;
    [Tooltip("...and no further than this (metres)")]
    public float aimMaxDistance = 15f;
    [Tooltip("Where the beam aims when nothing is in front within the furthest distance (metres)")]
    public float aimOpenDistance = 8f;
    [Tooltip("Seconds the beam takes to catch up with a turn")]
    public float aimLag = 0.08f;
    [Tooltip("Hard shadows from the hand-held torch")]
    public bool castShadows = true;

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

    // Hand-held aim
    private bool started;
    private bool aimSet;
    private Quaternion aimRotation;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private static readonly RaycastHit[] aimHits = new RaycastHit[8];
    private static Texture2D softCookie;

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
        started = true;
    }

    void OnEnable()
    {
        // Back from a kill sequence or cinematic that switched this off: the hand-held beam again.
        if (!started) return;
        aimSet = false;
        ConfigureLight();
    }

    void OnDisable()
    {
        // Whatever takes the torch over (kill sequences, jumpscares) gets the eye-level beam it was framed with:
        // at the camera, no cookie, no shadows. They set their own range, angle and intensity.
        if (spotLight == null) return;
        spotLight.cookie = null;
        spotLight.shadows = LightShadows.None;
        if (playerCamera != null)
            spotLight.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);
    }

    void Update()
    {
        // Toggle flashlight with key press (if controls not disabled)
        if (!controlsDisabled && Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }

        // Update killer proximity flicker
        if (enableKillerFlicker && isOn && !isDisabledByKiller)
        {
            UpdateKillerFlicker();
        }
    }

    // After the camera has moved and turned this frame
    void LateUpdate()
    {
        UpdateFlashlightTransform();
    }

    // Update the flashlight to follow the camera view
    private void UpdateFlashlightTransform()
    {
        if (playerCamera == null || spotLight == null) return;

        Transform cam = playerCamera.transform;
        if (!handHeld)
        {
            // Mounted on the camera: the light follows exactly where the camera is looking
            spotLight.transform.SetPositionAndRotation(cam.position, cam.rotation);
            return;
        }

        // Held in the hand, aimed at what the player looks at, catching up with turns a moment late
        Vector3 hand = cam.position + cam.rotation * handOffset;
        Vector3 target = cam.position + cam.forward * AimDistance(cam);
        Quaternion want = Quaternion.LookRotation(target - hand, cam.up);
        // a teleport or a cut (doors, respawns, cinematics) snaps the beam instead of swinging it round
        bool jumped = aimSet && ((cam.position - lastCameraPosition).sqrMagnitude > 9f || Quaternion.Angle(cam.rotation, lastCameraRotation) > 60f);
        lastCameraPosition = cam.position;
        lastCameraRotation = cam.rotation;
        if (!aimSet || jumped || aimLag <= 0f)
        {
            aimRotation = want;
            aimSet = true;
        }
        else
        {
            aimRotation = Quaternion.Slerp(aimRotation, want, 1f - Mathf.Exp(-Time.deltaTime / aimLag));
        }
        spotLight.transform.SetPositionAndRotation(hand, aimRotation);
    }

    // How far ahead the beam converges: the first solid thing in view (not the player), within limits
    private float AimDistance(Transform cam)
    {
        int count = Physics.RaycastNonAlloc(cam.position, cam.forward, aimHits, aimMaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Transform self = transform.root;
        for (int i = 0; i < count; i++)
        {
            if (aimHits[i].collider.transform.IsChildOf(self)) continue;
            nearest = Mathf.Min(nearest, aimHits[i].distance);
        }
        if (nearest == float.MaxValue) return aimOpenDistance;
        return Mathf.Clamp(nearest, aimMinDistance, aimMaxDistance);
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
            spotLight.cookie = softBeam ? (beamCookie != null ? beamCookie : SoftCookie()) : null;

            bool shadows = handHeld && castShadows;
            spotLight.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            if (shadows)
            {
                spotLight.shadowStrength = 1f;
                spotLight.shadowBias = 0.03f;
                spotLight.shadowNormalBias = 0.3f;
                spotLight.shadowNearPlane = 0.1f;
            }
        }
    }

    // The soft beam, drawn once: a bright core, a broad body and a dim spill, black at the edge (spot cookies need that).
    private static Texture2D SoftCookie()
    {
        if (softCookie != null) return softCookie;

        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
        {
            name = "Torch soft beam",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = 0.35f * Mathf.Pow(Fall(0.5f, 0f, r), 1.2f) + 0.5f * Fall(0.8f, 0.42f, r) + 0.15f * Fall(1f, 0.62f, r);
                if (r >= 0.985f || x == 0 || y == 0 || x == n - 1 || y == n - 1) a = 0f;
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                pixels[y * n + x] = new Color32(b, b, b, b);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        softCookie = tex;
        return tex;
    }

    // smoothstep that also runs downhill (edge0 > edge1)
    private static float Fall(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
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