using System.Collections;
using UnityEngine;

/// <summary>
/// Applies fog + ambient lighting changes ONLY while the player is in the forest, then restores
/// the previous atmosphere on exit. This keeps a shared-scene world from being permanently altered:
/// the forest's mood is local and temporary.
///
/// Mirrors <see cref="StaircaseFogZone"/> (save fog -> lerp -> restore), but also reacts to teleport
/// entry/exit. PlayerZoneTracker (on the Player) sends "OnZoneEnterByTeleport"/"OnZoneExitByTeleport"
/// when the player is teleported between zones, because OnTriggerEnter/Exit don't fire on teleport.
/// Since the player is *sent* to the forest, teleport entry must be handled too.
///
/// You can also drive it directly from a decision/event via EnterForest() / ExitForest().
///
/// Setup: put this on a GameObject with a trigger Collider covering the forest area.
/// Requires a LightingController in the scene; the player must be tagged "Player".
/// </summary>
[RequireComponent(typeof(Collider))]
public class ForestAtmosphereZone : MonoBehaviour
{
    [Header("Fog (inside the forest)")]
    [Tooltip("Target fog density inside the forest")]
    [SerializeField] private float fogDensity = 0.02f;

    [Tooltip("Fog color inside the forest (cold, moonlit)")]
    [SerializeField] private Color fogColor = new Color(0.05f, 0.07f, 0.11f);

    [Tooltip("Seconds to lerp fog in/out")]
    [SerializeField] private float fogTransitionDuration = 2.5f;

    [Header("Lighting")]
    [Tooltip("Lighting preset applied when entering the forest")]
    [SerializeField] private LightingPreset lightingPreset = LightingPreset.Forest;

    [Tooltip("Lighting preset restored when leaving the forest")]
    [SerializeField] private LightingPreset restorePreset = LightingPreset.Outdoor;

    [Header("Forest content / extras")]
    [Tooltip("GameObjects active ONLY while in the forest (the ProceduralTrees group itself, fog particles, ambient SFX, a moon light...). Disabled at startup and on exit, enabled on enter — so the forest costs nothing while the player is elsewhere in the shared scene.")]
    [SerializeField] private GameObject[] enableWhileInForest;

    // Saved fog state to restore on exit
    private bool previousFogEnabled;
    private float previousFogDensity;
    private Color previousFogColor;
    private FogMode previousFogMode;

    private Coroutine fogTransition;
    private bool playerInside;

    private void Start()
    {
        // The forest's heavy content begins disabled so it costs nothing while the player is
        // elsewhere in the shared scene; entering the zone switches it on.
        if (!playerInside) SetOptionalObjects(false);
    }

    // ---- Entry / exit paths ----

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) EnterForest();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) ExitForest();
    }

    // Sent by PlayerZoneTracker when the player teleports into/out of this zone.
    private void OnZoneEnterByTeleport() => EnterForest();
    private void OnZoneExitByTeleport() => ExitForest();

    /// <summary>Activate the forest atmosphere. Safe to call from a decision/event.</summary>
    public void EnterForest()
    {
        if (playerInside) return;
        playerInside = true;

        // Save the current (world) fog state before overriding it.
        previousFogEnabled = RenderSettings.fog;
        previousFogDensity = RenderSettings.fogDensity;
        previousFogColor = RenderSettings.fogColor;
        previousFogMode = RenderSettings.fogMode;

        if (lightingPreset != LightingPreset.None && LightingController.Instance != null)
            LightingController.Instance.ApplyPreset(lightingPreset);

        SetOptionalObjects(true);

        if (fogTransition != null) StopCoroutine(fogTransition);
        fogTransition = StartCoroutine(TransitionFog(fogDensity, fogColor, true));
    }

    /// <summary>Restore the previous (world) atmosphere. Safe to call from a decision/event.</summary>
    public void ExitForest()
    {
        if (!playerInside) return;
        playerInside = false;

        if (restorePreset != LightingPreset.None && LightingController.Instance != null)
            LightingController.Instance.ApplyPreset(restorePreset);

        SetOptionalObjects(false);

        if (fogTransition != null) StopCoroutine(fogTransition);
        fogTransition = StartCoroutine(TransitionFog(previousFogDensity, previousFogColor, previousFogEnabled));
    }

    private void SetOptionalObjects(bool on)
    {
        if (enableWhileInForest == null) return;
        foreach (var go in enableWhileInForest)
            if (go != null) go.SetActive(on);
    }

    private IEnumerator TransitionFog(float targetDensity, Color targetColor, bool enableFog)
    {
        // Enable fog immediately so the lerp is visible.
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

        // Snap to final values.
        RenderSettings.fogDensity = targetDensity;
        RenderSettings.fogColor = targetColor;
        RenderSettings.fog = enableFog;

        // If disabling fog, also restore the original fog mode.
        if (!enableFog) RenderSettings.fogMode = previousFogMode;

        fogTransition = null;
    }
}
