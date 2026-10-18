using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Soft glow around a lamp head, driven by the lamp's Light before every camera renders:
/// hidden while the lamp is off, flickers with LanternInteractable's intensity flicker, and
/// takes the light's colour (so the red jumpscare lock tints it too). Lets a lit lamp read
/// from across the graveyard through the fog.
///
/// Put it on the lamp's Light object. The glow is a camera-facing quad (Custom/LampHalo)
/// created at runtime and never saved into the scene.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class LampHalo : MonoBehaviour
{
    [SerializeField] private Material haloMaterial;

    [Tooltip("Width of the glow in metres")]
    [SerializeField] private float size = 3.2f;

    [Tooltip("Brightness of the wide glow at the lamp's normal intensity")]
    [SerializeField] private float outerStrength = 2.2f;

    [Tooltip("Brightness of the hot core at the lamp's normal intensity")]
    [SerializeField] private float coreStrength = 2.0f;

    [Tooltip("How far the core is pushed from the light colour toward white")]
    [Range(0f, 1f)]
    [SerializeField] private float coreWhiten = 0.45f;

    [Tooltip("Light intensity that counts as fully lit. 0 = the LanternInteractable's base intensity, or the light's starting intensity.")]
    [SerializeField] private float referenceIntensity = 0f;

    private static readonly List<LampHalo> active = new List<LampHalo>();
    private static readonly int OuterId = Shader.PropertyToID("_OuterColor");
    private static readonly int CoreId = Shader.PropertyToID("_CoreColor");
    private static readonly int SizeId = Shader.PropertyToID("_Size");

    private Light lampLight;
    private GameObject haloObject;
    private MeshRenderer haloRenderer;
    private Mesh quad;
    private MaterialPropertyBlock block;
    private float reference;

    private void OnEnable()
    {
        lampLight = GetComponent<Light>();
        block = new MaterialPropertyBlock();

        var lantern = GetComponentInParent<LanternInteractable>();
        reference = referenceIntensity > 0f ? referenceIntensity
                  : lantern != null && lantern.baseIntensity > 0f ? lantern.baseIntensity
                  : Mathf.Max(lampLight.intensity, 0.01f);

        CreateHalo();
        if (active.Count == 0) Camera.onPreCull += SyncAll;
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (active.Count == 0) Camera.onPreCull -= SyncAll;
        DestroyHalo();
    }

    private static void SyncAll(Camera cam)
    {
        foreach (var halo in active) halo.Sync();
    }

    private void Sync()
    {
        if (haloRenderer == null) return;

        bool on = lampLight.isActiveAndEnabled && lampLight.intensity > 0.001f;
        haloRenderer.enabled = on;
        if (!on) return;

        float k = lampLight.intensity / reference;
        Color c = lampLight.color;
        block.SetColor(OuterId, c * (outerStrength * k));
        block.SetColor(CoreId, Color.Lerp(c, Color.white, coreWhiten) * (coreStrength * k));
        block.SetFloat(SizeId, size);
        haloRenderer.SetPropertyBlock(block);
    }

    private void CreateHalo()
    {
        if (haloMaterial == null) return;

        // Unit quad; the shader billboards and scales it, so give it bounds that cover the glow.
        quad = new Mesh { name = "LampHaloQuad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * (size + 1f));

        haloObject = new GameObject("LampHalo (runtime)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        haloObject.transform.SetParent(transform, false);
        haloObject.AddComponent<MeshFilter>().sharedMesh = quad;
        haloRenderer = haloObject.AddComponent<MeshRenderer>();
        haloRenderer.sharedMaterial = haloMaterial;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        haloRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        haloRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        Sync();
    }

    private void DestroyHalo()
    {
        if (Application.isPlaying)
        {
            if (haloObject != null) Destroy(haloObject);
            if (quad != null) Destroy(quad);
        }
        else
        {
            if (haloObject != null) DestroyImmediate(haloObject);
            if (quad != null) DestroyImmediate(quad);
        }
        haloObject = null;
        haloRenderer = null;
        quad = null;
    }
}
