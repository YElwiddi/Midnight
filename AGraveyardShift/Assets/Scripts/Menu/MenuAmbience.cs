using UnityEngine;

/// <summary>
/// Sounds of the menu's front gate on top of the wind and music: a far-off church bell now and
/// then, and the gate creaking when the player hovers Play.
/// </summary>
public class MenuAmbience : MonoBehaviour
{
    public AudioSource oneShots;

    [Header("Bell")]
    public AudioClip bellClip;
    [Tooltip("Seconds between tolls (random in range).")]
    public Vector2 bellInterval = new Vector2(40f, 80f);
    [Range(0f, 1f)] public float bellVolume = 0.32f;

    [Header("Gate creak on hovering Play")]
    public EpitaphButtonFX creakOnHover;
    public AudioClip creakClip;
    [Range(0f, 1f)] public float creakVolume = 0.28f;
    public float creakCooldown = 6f;

    private float nextBell;
    private float lastCreak = -100f;

    private void OnEnable()
    {
        if (creakOnHover != null) creakOnHover.Hovered += Creak;
        nextBell = Time.unscaledTime + Random.Range(14f, 26f);
    }

    private void OnDisable()
    {
        if (creakOnHover != null) creakOnHover.Hovered -= Creak;
    }

    private void Update()
    {
        if (oneShots == null || bellClip == null || Time.unscaledTime < nextBell) return;
        oneShots.PlayOneShot(bellClip, bellVolume);
        nextBell = Time.unscaledTime + Random.Range(bellInterval.x, bellInterval.y);
    }

    private void Creak()
    {
        if (oneShots == null || creakClip == null || Time.unscaledTime - lastCreak < creakCooldown) return;
        lastCreak = Time.unscaledTime;
        oneShots.PlayOneShot(creakClip, creakVolume);
    }
}
