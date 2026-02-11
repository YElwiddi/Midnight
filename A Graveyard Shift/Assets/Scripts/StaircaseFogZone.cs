using System.Collections;
using UnityEngine;

public class StaircaseFogZone : MonoBehaviour
{
    [Header("Fog Settings")]
    [Tooltip("Target fog density inside the staircase")]
    [SerializeField] private float fogDensity = 0.08f;

    [Tooltip("Fog color (near-black for horror)")]
    [SerializeField] private Color fogColor = new Color(0.02f, 0.02f, 0.03f);

    [Tooltip("Seconds to lerp fog in/out")]
    [SerializeField] private float fogTransitionDuration = 1.5f;

    [Header("Lighting")]
    [Tooltip("Lighting preset for the staircase area")]
    [SerializeField] private LightingPreset lightingPreset = LightingPreset.Crypt;

    [Tooltip("Lighting preset to restore when exiting the staircase")]
    [SerializeField] private LightingPreset restorePreset = LightingPreset.Outdoor;

    // Saved fog state to restore on exit
    private bool previousFogEnabled;
    private float previousFogDensity;
    private Color previousFogColor;
    private FogMode previousFogMode;

    private Coroutine fogTransition;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Save current fog state before overriding
        previousFogEnabled = RenderSettings.fog;
        previousFogDensity = RenderSettings.fogDensity;
        previousFogColor = RenderSettings.fogColor;
        previousFogMode = RenderSettings.fogMode;

        // Apply lighting preset
        if (lightingPreset != LightingPreset.None && LightingController.Instance != null)
        {
            LightingController.Instance.ApplyPreset(lightingPreset);
        }

        // Start fog transition
        if (fogTransition != null) StopCoroutine(fogTransition);
        fogTransition = StartCoroutine(TransitionFog(fogDensity, fogColor, true));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Restore lighting preset
        if (restorePreset != LightingPreset.None && LightingController.Instance != null)
        {
            LightingController.Instance.ApplyPreset(restorePreset);
        }

        // Transition fog back to previous values
        if (fogTransition != null) StopCoroutine(fogTransition);
        fogTransition = StartCoroutine(TransitionFog(previousFogDensity, previousFogColor, previousFogEnabled));
    }

    private IEnumerator TransitionFog(float targetDensity, Color targetColor, bool enableFog)
    {
        // Enable fog immediately so the lerp is visible
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;
        float elapsed = 0f;

        while (elapsed < fogTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fogTransitionDuration;

            RenderSettings.fogDensity = Mathf.Lerp(startDensity, targetDensity, t);
            RenderSettings.fogColor = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        // Snap to final values
        RenderSettings.fogDensity = targetDensity;
        RenderSettings.fogColor = targetColor;
        RenderSettings.fog = enableFog;

        // If disabling fog, also restore the original fog mode
        if (!enableFog)
        {
            RenderSettings.fogMode = previousFogMode;
        }

        fogTransition = null;
    }
}
