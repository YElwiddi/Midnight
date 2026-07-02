using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives the sitting Maria killer's "stare" trigger. She sits idle (KillerNPC ambush, with its own
/// auto-trigger disabled via a huge lookDurationRequired).
///
/// Ambient violin music loops from the moment she spawns and is cut ABRUPTLY the instant the bonecrack
/// plays. The bonecrack itself fires ~headSnapDelay BEFORE her head actually snaps to the player (sound
/// first, then the head whips around). Timeline once the player has stared for stareToCommit seconds:
///   t0  : bonecrack plays + violin cuts out
///   t0 + headSnapDelay : her head snaps to lock onto the player
///   t0 + headSnapDelay + delayToChase : she warps to the navmesh and charges (ForceActivateChase)
/// Once the stare commits, everything proceeds regardless of whether the player keeps looking.
/// </summary>
public class MariaKillerStare : MonoBehaviour
{
    [Header("Trigger timing")]
    [Tooltip("Seconds of continuous looking before she reacts.")]
    [SerializeField] private float stareToCommit = 2f;
    [Tooltip("Delay between the bonecrack sound and her head actually snapping to the player.")]
    [SerializeField] private float headSnapDelay = 0.2f;
    [Tooltip("Seconds after the head-snap before the chase begins.")]
    [SerializeField] private float delayToChase = 1.5f;

    [Header("'Really see her' detection")]
    [Tooltip("Max distance the player can be and still count as seeing her.")]
    [SerializeField] private float lookRange = 8f;
    [Tooltip("View cone (degrees from screen center) that counts as looking at her.")]
    [SerializeField] private float lookAngle = 32f;
    [Tooltip("Require an unobstructed line of sight (no looking through walls).")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask occlusionMask = ~0;

    [Header("Audio")]
    [Tooltip("Bonecrack — plays the instant she reacts, ~headSnapDelay before her head snaps.")]
    [SerializeField] private AudioClip reactSound;
    [SerializeField] private float reactVolume = 1f;
    [Tooltip("Ambient music that loops from spawn and is cut abruptly the instant the bonecrack plays.")]
    [SerializeField] private AudioClip spawnMusicLoop;
    [SerializeField] private float spawnMusicVolume = 0.5f;
    [Tooltip("Jumpscare sound — plays (2D, full volume) the instant the chase kicks off and rings through the catch.")]
    [SerializeField] private AudioClip chaseStartSound;
    [SerializeField] private float chaseStartVolume = 1f;

    private KillerNPC killer;
    private Transform cam;
    private Transform head;
    private Renderer[] renderers;
    private AudioSource musicSource;
    private int stage; // 0 watching, 1 wind-up (sound played, head not yet snapped), 2 locked (head on player), 3 done
    private float timer;

    private void Awake()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
            if (t.name == "Head") { head = t; break; }
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        // Ambient music begins the moment she spawns; it loops until the bonecrack cuts it.
        if (spawnMusicLoop != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = spawnMusicLoop;
            musicSource.loop = true;
            musicSource.volume = spawnMusicVolume;
            musicSource.spatialBlend = 0f; // 2D ambient
            musicSource.playOnAwake = false;
            musicSource.Play();
        }
    }

    private void Update()
    {
        if (killer == null) killer = GetComponent<KillerNPC>(); // added at runtime by GameFlowManager
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam == null) return;

        if (stage == 0)
        {
            if (IsPlayerLooking()) { timer += Time.deltaTime; if (timer >= stareToCommit) React(); }
            else timer = 0f;
        }
        else if (stage == 1)
        {
            // Wind-up: bonecrack has played; wait headSnapDelay, THEN her head snaps (stage 2).
            timer += Time.deltaTime;
            if (timer >= headSnapDelay) { stage = 2; timer = 0f; }
        }
        else if (stage == 2)
        {
            timer += Time.deltaTime;
            if (timer >= delayToChase) BeginChase();
        }
    }

    // Lock the head onto the player only once it has snapped (stage 2), until the chase begins.
    private void LateUpdate()
    {
        if (stage != 2 || head == null || cam == null) return;
        Vector3 toCam = cam.position - head.position;
        if (toCam.sqrMagnitude > 0.0001f)
            head.rotation = Quaternion.FromToRotation(-head.forward, toCam.normalized) * head.rotation;
    }

    // The instant the stare commits: bonecrack + cut the music. Her head doesn't snap yet (that's headSnapDelay later).
    private void React()
    {
        stage = 1;
        timer = 0f;
        if (musicSource != null) musicSource.Stop(); // abrupt cut
        PlayLoud2D(reactSound, reactVolume);
        Debug.Log("MariaKillerStare: bonecrack — music cut; head snaps in " + headSnapDelay + "s.");
    }

    private void BeginChase()
    {
        stage = 3;
        // She may be sitting on the rock (off the navmesh) — warp her to the mesh before charging.
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        Debug.Log("MariaKillerStare: chase begins.");
        PlayJumpscareSound(); // 2D, on a named object so KillerJumpscare can cut it when the jumpscare ends
        if (killer != null) killer.ForceActivateChase();
    }

    private bool IsPlayerLooking()
    {
        Vector3 target = RenderersCenter();
        Vector3 toTarget = target - cam.position;
        float dist = toTarget.magnitude;
        if (dist > lookRange) return false;
        if (Vector3.Angle(cam.forward, toTarget) > lookAngle) return false;
        if (requireLineOfSight &&
            Physics.Raycast(cam.position, toTarget.normalized, out RaycastHit hit, dist - 0.3f, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform)) return false; // occluded
        }
        return true;
    }

    private Vector3 RenderersCenter()
    {
        if (renderers == null || renderers.Length == 0) return transform.position + Vector3.up;
        Bounds b = default; bool has = false;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return has ? b.center : transform.position + Vector3.up;
    }

    // Play a clip as a 2D one-shot (no 3D distance falloff) so the stinger hits the player at full volume.
    private static void PlayLoud2D(AudioClip clip, float volume)
    {
        if (clip == null) return;
        var go = new GameObject("MariaBoneCrackSFX");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 0f; // 2D — full volume regardless of distance from the player
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // Plays the jumpscare sound 2D on a findable object ("MariaJumpscareSound") so KillerJumpscare can cut
    // it the instant the jumpscare ends (otherwise it rings on into the ending reveal).
    private void PlayJumpscareSound()
    {
        if (chaseStartSound == null) return;
        var go = new GameObject("MariaJumpscareSound");
        var src = go.AddComponent<AudioSource>();
        src.clip = chaseStartSound;
        src.volume = chaseStartVolume;
        src.spatialBlend = 0f;
        src.Play();
        Destroy(go, chaseStartSound.length + 0.2f); // fallback cleanup if the catch never happens
    }
}
