using UnityEngine;

/// <summary>
/// Subtle idle motion for the title logo: a slow "breath" (scale), a gentle bob,
/// and a faint sway. Purely cosmetic. Uses unscaled time so it animates on the menu.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuLogoFX : MonoBehaviour
{
    [Header("Breathing (scale)")]
    [Tooltip("Plus/minus fraction of scale.")]
    public float breatheAmount = 0.015f;
    public float breathePeriod = 4.5f;

    [Header("Bob (vertical)")]
    public float bobAmount = 5f;
    public float bobPeriod = 6f;

    [Header("Sway (rotation, degrees)")]
    public float swayAmount = 0.4f;
    public float swayPeriod = 8f;

    private RectTransform rt;
    private Vector2 basePos;
    private Vector3 baseScale;
    private float seed;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        basePos = rt.anchoredPosition;
        baseScale = rt.localScale;
        seed = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.unscaledTime + seed;
        float breathe = 1f + Mathf.Sin(Tau(t, breathePeriod)) * breatheAmount;
        float bob = Mathf.Sin(Tau(t, bobPeriod)) * bobAmount;
        float sway = Mathf.Sin(Tau(t, swayPeriod)) * swayAmount;

        rt.localScale = baseScale * breathe;
        rt.anchoredPosition = basePos + new Vector2(0f, bob);
        rt.localRotation = Quaternion.Euler(0f, 0f, sway);
    }

    private static float Tau(float time, float period)
    {
        return time * (2f * Mathf.PI / Mathf.Max(0.01f, period));
    }
}
