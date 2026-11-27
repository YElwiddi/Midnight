using UnityEngine;

/// <summary>
/// A decorative gate lantern that follows one of the graveyard lamps: lit while that lamp is lit, out while it is out,
/// flickering and tinted with it (the kill sequences turn every lamp red through LanternInteractable.LockAllLanterns).
/// The source is found by name in this lantern's own scene, so the copies the main-menu backdrop makes follow the
/// menu's lamp too (MenuBackdropBuilder keeps this component).
/// </summary>
[RequireComponent(typeof(Light))]
public class GateLanternLink : MonoBehaviour
{
    [Tooltip("Name of the lamp object whose light this lantern follows")]
    public string sourceName = "VicLamp (1)";

    [Tooltip("This lantern's intensity while the source shines at its base intensity")]
    public float baseIntensity = 2.6f;

    [Tooltip("Used when the source has no LanternInteractable (the menu copy): the source's normal intensity")]
    public float fallbackSourceIntensity = 5f;

    [Tooltip("The lantern body whose glass glows with the light (optional)")]
    public Renderer glassRenderer;

    [Tooltip("Material slot of the glass on that renderer")]
    public int glassMaterialIndex = 1;

    [Tooltip("Glow of the glass at full brightness, multiplied by the source's colour")]
    public float glassEmission = 1.6f;

    private Light lanternLight;
    private Light source;
    private float sourceBase;
    private MaterialPropertyBlock block;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        lanternLight = GetComponent<Light>();
        block = new MaterialPropertyBlock();
    }

    private void Start()
    {
        FindSource();
    }

    private void FindSource()
    {
        var scene = gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindChild(root.transform, sourceName);
            if (t == null) continue;
            source = t.GetComponentInChildren<Light>(true);
            var lantern = t.GetComponentInParent<LanternInteractable>();
            sourceBase = lantern != null && lantern.baseIntensity > 0f ? lantern.baseIntensity : fallbackSourceIntensity;
            break;
        }
    }

    private static Transform FindChild(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindChild(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // After LanternInteractable.Update has set this frame's flicker
    private void LateUpdate()
    {
        if (source == null) return;

        bool on = source.isActiveAndEnabled && source.intensity > 0.001f;
        float k = on ? source.intensity / Mathf.Max(0.01f, sourceBase) : 0f;
        lanternLight.enabled = on;
        if (on)
        {
            lanternLight.intensity = baseIntensity * k;
            lanternLight.color = source.color;
        }

        if (glassRenderer != null)
        {
            glassRenderer.GetPropertyBlock(block, glassMaterialIndex);
            block.SetColor(EmissionId, on ? source.color * (glassEmission * Mathf.Clamp(k, 0f, 1.5f)) : Color.black);
            glassRenderer.SetPropertyBlock(block, glassMaterialIndex);
        }
    }
}
