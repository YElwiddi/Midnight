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
}