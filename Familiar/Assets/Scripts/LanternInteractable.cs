using UnityEngine;

public class LanternInteractable : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    public Light lanternLight;
    public bool startOn = false;

    [Header("Audio")]
    public AudioClip toggleSound;
    private AudioSource audioSource;

    private bool isOn;

    void Start()
    {
        // Auto-find light component if not assigned
        if (lanternLight == null)
            lanternLight = GetComponentInChildren<Light>();

        // Set up audio source
        if (toggleSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Set initial state
        isOn = startOn;
        if (lanternLight != null)
            lanternLight.enabled = isOn;
    }

    public void Interact()
    {
        isOn = !isOn;

        if (lanternLight != null)
            lanternLight.enabled = isOn;

        // Play toggle sound
        if (audioSource != null && toggleSound != null)
            audioSource.PlayOneShot(toggleSound);
    }

    public string GetInteractionPrompt()
    {
        return isOn ? "Turn off lantern" : "Turn on lantern";
    }
}
