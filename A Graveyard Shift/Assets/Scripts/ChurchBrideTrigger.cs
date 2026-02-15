using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger zone in the church that activates when BrideKillerReady is true.
/// When the player enters this zone and the flag is set:
///   - Environment lighting turns red
///   - Fog disappears
///   - A ConditionalKillerEvent (Bride killer) is spawned via GameFlowManager
/// </summary>
public class ChurchBrideTrigger : MonoBehaviour
{
    [Header("Killer Event")]
    [Tooltip("The ConditionalKillerEvent to spawn when triggered")]
    [SerializeField] private ConditionalKillerEvent brideKillerEvent;

    [Header("Red Lighting")]
    [Tooltip("Sky color for red church lighting")]
    [SerializeField] private Color redSkyColor = new Color(0.4f, 0.05f, 0.05f);

    [Tooltip("Equator color for red church lighting")]
    [SerializeField] private Color redEquatorColor = new Color(0.3f, 0.03f, 0.03f);

    [Tooltip("Ground color for red church lighting")]
    [SerializeField] private Color redGroundColor = new Color(0.2f, 0.02f, 0.02f);

    [Header("Fog")]
    [Tooltip("Duration to fade fog out (seconds)")]
    [SerializeField] private float fogFadeDuration = 1.5f;

    [Tooltip("Optional: PSX FogController to disable")]
    [SerializeField] private PSX.FogController psxFogController;

    [Header("Sound")]
    [Tooltip("Sound effect to play when the trigger fires")]
    [SerializeField] private AudioClip triggerSound;

    [Tooltip("Volume of the trigger sound")]
    [Range(0f, 1f)]
    [SerializeField] private float triggerSoundVolume = 1f;

    [Header("Settings")]
    [Tooltip("Only trigger once per game session")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"ChurchBrideTrigger: OnTriggerEnter hit by '{other.name}' (tag: {other.tag})");

        if (!other.CompareTag("Player")) return;

        if (triggerOnce && hasTriggered)
        {
            Debug.Log("ChurchBrideTrigger: Already triggered, skipping");
            return;
        }

        bool gmExists = GameManager.Instance != null;
        bool brideReady = gmExists && GameManager.Instance.BrideKillerReady;
        Debug.Log($"ChurchBrideTrigger: GameManager exists={gmExists}, BrideKillerReady={brideReady}");

        if (!gmExists || !brideReady) return;

        hasTriggered = true;
        Debug.Log("ChurchBrideTrigger: All conditions met, activating!");

        // Play trigger sound
        if (triggerSound != null)
        {
            AudioSource.PlayClipAtPoint(triggerSound, transform.position, triggerSoundVolume);
        }

        // Turn lighting red
        ApplyRedLighting();

        // Fade out fog
        StartCoroutine(FadeOutFog());

        // Spawn the killer event
        SpawnBrideKiller();
    }

    private void ApplyRedLighting()
    {
        if (LightingController.Instance != null)
        {
            LightingSettings redSettings = new LightingSettings
            {
                skyColor = redSkyColor,
                equatorColor = redEquatorColor,
                groundColor = redGroundColor
            };
            LightingController.Instance.ApplySettings(redSettings, LightingPreset.Custom);
            Debug.Log("ChurchBrideTrigger: Red lighting applied");
        }
        else
        {
            // Fallback: apply directly to RenderSettings
            RenderSettings.ambientSkyColor = redSkyColor;
            RenderSettings.ambientEquatorColor = redEquatorColor;
            RenderSettings.ambientGroundColor = redGroundColor;
            Debug.LogWarning("ChurchBrideTrigger: LightingController not found, applied red lighting directly");
        }
    }

    private IEnumerator FadeOutFog()
    {
        // Disable PSX shader fog if assigned
        if (psxFogController != null)
        {
            psxFogController.enabled = false;
            Debug.Log("ChurchBrideTrigger: PSX FogController disabled");
        }

        // Fade out Unity built-in fog
        if (RenderSettings.fog)
        {
            float startDensity = RenderSettings.fogDensity;
            float elapsed = 0f;

            while (elapsed < fogFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fogFadeDuration;
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, 0f, t);
                yield return null;
            }

            RenderSettings.fogDensity = 0f;
            RenderSettings.fog = false;
            Debug.Log("ChurchBrideTrigger: Built-in fog faded out");
        }
    }

    private void SpawnBrideKiller()
    {
        if (brideKillerEvent == null)
        {
            Debug.LogError("ChurchBrideTrigger: No brideKillerEvent assigned!");
            return;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.SpawnKillerEvent(brideKillerEvent);
            Debug.Log($"ChurchBrideTrigger: Spawned killer event '{brideKillerEvent.eventName}'");
        }
        else
        {
            Debug.LogError("ChurchBrideTrigger: GameFlowManager not found, cannot spawn killer event!");
        }
    }
}
