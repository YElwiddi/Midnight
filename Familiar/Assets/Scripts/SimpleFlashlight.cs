using System.Collections;
using System.Collections.Generic;
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
    
    // Private variables
    private bool isOn = false;
    private Camera playerCamera;

    // Killer disable state
    private bool isDisabledByKiller = false;
    private string disabledDialogueText;
    private string disabledDialogueSpeaker;
    private float disabledDialogueDuration;
    private float disabledDialogueTypewriterSpeed;
    private float disabledDialogueCooldown;
    private float lastDialogueTime = -999f;
    private bool isShowingDialogue = false;
    private DialogueUI dialogueUI;
    private Coroutine dialogueCoroutine;

    // Static reference for other systems to check
    private static SimpleFlashlight activeDialogueInstance;

    /// <summary>
    /// Returns true if the flashlight disabled dialogue is currently displaying.
    /// </summary>
    public static bool IsFlashlightDialogueActive => activeDialogueInstance != null && activeDialogueInstance.isShowingDialogue;
    
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
        
        // Set initial state
        isOn = startsEnabled;
        SetFlashlightState(isOn);
    }
    
    void Update()
    {
        // Toggle flashlight with key press
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }
        
        // Update flashlight position and rotation to follow camera
        UpdateFlashlightTransform();
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
    public void SetFlashlightState(bool state)
    {
        isOn = state;
        
        if (spotLight != null)
        {
            spotLight.enabled = isOn;
            
            // Play appropriate sound
            if (audioSource != null)
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

        // Find DialogueUI for showing messages
        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<DialogueUI>();
        }

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

        // Don't show if another dialogue system is active
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return;
        if (isShowingDialogue) return;

        if (dialogueUI == null)
        {
            dialogueUI = FindObjectOfType<DialogueUI>();
        }

        if (dialogueUI != null)
        {
            // Stop any existing dialogue coroutine
            if (dialogueCoroutine != null)
            {
                StopCoroutine(dialogueCoroutine);
            }

            lastDialogueTime = Time.time;
            dialogueCoroutine = StartCoroutine(ShowDisabledDialogueCoroutine());
        }
    }

    private System.Collections.IEnumerator ShowDisabledDialogueCoroutine()
    {
        isShowingDialogue = true;
        activeDialogueInstance = this;

        dialogueUI.Show();

        string speaker = string.IsNullOrEmpty(disabledDialogueSpeaker) ? null : disabledDialogueSpeaker;

        if (disabledDialogueTypewriterSpeed > 0)
        {
            // Typewriter effect - show characters one by one
            float delay = 1f / disabledDialogueTypewriterSpeed;
            for (int i = 1; i <= disabledDialogueText.Length; i++)
            {
                dialogueUI.SetDialogueText(disabledDialogueText.Substring(0, i), speaker);
                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            // Instant display
            dialogueUI.SetDialogueText(disabledDialogueText, speaker);
        }

        // Wait for duration after text is fully displayed
        yield return new WaitForSeconds(disabledDialogueDuration);

        // Only hide if we're still the active dialogue and no other dialogue has taken over
        if (activeDialogueInstance == this && !SimpleDialogueTrigger.IsAnySimpleDialogueActive)
        {
            dialogueUI.Hide();
        }

        if (activeDialogueInstance == this)
        {
            activeDialogueInstance = null;
        }

        isShowingDialogue = false;
        dialogueCoroutine = null;
    }

    /// <summary>
    /// Check if the flashlight has been disabled by a killer.
    /// </summary>
    public bool IsDisabledByKiller()
    {
        return isDisabledByKiller;
    }
}