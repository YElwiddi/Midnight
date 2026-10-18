using System.Collections;
using UnityEngine;

/// <summary>
/// The graveyard's night pass: a soft moonlight and drifting ground mist, kept to the outdoor
/// graveyard so the below-map sets (cabin interior, crypt, church, secret room, forest) never
/// see them. The dark-oxblood fog itself is the scene's Lighting fog setting, so every zone
/// that saves/restores RenderSettings fog (forest, staircases, endings) keeps working unchanged.
///
/// The moon and mist are switched per camera (Camera.onPreCull): any camera inside
/// <see cref="outdoorBounds"/> (player, CCTV feeds) gets them, every other camera doesn't.
/// When the graveyard ending brings the dawn sky, the moon and mist fade out and the fog
/// eases to <see cref="dawnFogColor"/>.
///
/// Setup: one in the scene; assign the moon Light and the mist ParticleSystems.
/// The glow around the lamps is <see cref="LampHalo"/>.
/// </summary>
[ExecuteAlways]
public class NightAtmosphere : MonoBehaviour
{
    [Header("Where the night applies")]
    [Tooltip("World-space box around the outdoor graveyard and the road. Cameras outside it (interiors, crypt, church, forest) don't get the moon or the mist.")]
    [SerializeField] private Bounds outdoorBounds = new Bounds(new Vector3(-120f, 40f, 130f), new Vector3(170f, 60f, 200f));

    [Header("Moon")]
    [SerializeField] private Light moonLight;

    [Tooltip("Shadow casters further than this (metres) are skipped for the moon; the fog hides them anyway")]
    [SerializeField] private float moonShadowCullDistance = 45f;

    [Header("Ground mist")]
    [SerializeField] private ParticleSystem[] mistSystems;

    [Header("Dawn (graveyard ending)")]
    [Tooltip("Fog colour once the graveyard ending has turned the sky to dawn")]
    [SerializeField] private Color dawnFogColor = new Color(0.33f, 0.31f, 0.3f);

    [Tooltip("Seconds for the moon and mist to fade out once the ending triggers")]
    [SerializeField] private float dawnFadeDuration = 6f;

    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    private ParticleSystemRenderer[] mistRenderers;
    private MaterialPropertyBlock block;
    private float moonIntensity;
    private float nightAmount = 1f;   // 1 = full night, 0 = dawn
    private bool dawnStarted;

    private bool NightVisible => nightAmount > 0f;

    private void OnEnable()
    {
        CacheRenderers();
        if (moonLight != null)
        {
            moonIntensity = moonLight.intensity;
            var cull = new float[32];
            for (int i = 0; i < cull.Length; i++) cull[i] = moonShadowCullDistance;
            moonLight.layerShadowCullDistances = cull;
        }
        Camera.onPreCull += HandlePreCull;
        Camera.onPostRender += HandlePostRender;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;
        Camera.onPostRender -= HandlePostRender;
        SetVisible(true);
    }

    private void OnValidate() => CacheRenderers();

    private void Update()
    {
        if (!Application.isPlaying || dawnStarted) return;

        if (GameManager.Instance != null && GameManager.Instance.GraveyardEndingTriggered)
            StartCoroutine(FadeToDawn());
    }

    private void CacheRenderers()
    {
        if (mistSystems == null) { mistRenderers = new ParticleSystemRenderer[0]; return; }
        mistRenderers = new ParticleSystemRenderer[mistSystems.Length];
        for (int i = 0; i < mistSystems.Length; i++)
            if (mistSystems[i] != null) mistRenderers[i] = mistSystems[i].GetComponent<ParticleSystemRenderer>();
    }

    // ---- Per-camera switching ----

    private void HandlePreCull(Camera cam)
    {
        SetVisible(NightVisible && outdoorBounds.Contains(cam.transform.position));
    }

    // Back to the resting state after every camera, so nothing flipped for one camera
    // (an interior shot, the Scene view underground) is left that way or saved into the scene.
    private void HandlePostRender(Camera cam)
    {
        SetVisible(NightVisible);
    }

    private void SetVisible(bool on)
    {
        if (moonLight != null) moonLight.enabled = on;
        if (mistRenderers == null) return;
        foreach (var r in mistRenderers)
            if (r != null) r.enabled = on;
    }

    // ---- Dawn ----

    private IEnumerator FadeToDawn()
    {
        dawnStarted = true;
        if (block == null) block = new MaterialPropertyBlock();

        Color startFog = RenderSettings.fogColor;
        float elapsed = 0f;

        while (elapsed < dawnFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dawnFadeDuration);
            ApplyNight(1f - t);
            RenderSettings.fogColor = Color.Lerp(startFog, dawnFogColor, t);
            yield return null;
        }

        ApplyNight(0f);
        RenderSettings.fogColor = dawnFogColor;
        SetVisible(false);
    }

    private void ApplyNight(float amount)
    {
        nightAmount = amount;
        if (moonLight != null) moonLight.intensity = moonIntensity * amount;

        block.SetFloat(OpacityId, amount);
        foreach (var r in mistRenderers)
            if (r != null) r.SetPropertyBlock(block);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.65f, 0.9f, 0.6f);
        Gizmos.DrawWireCube(outdoorBounds.center, outdoorBounds.size);
    }
}
