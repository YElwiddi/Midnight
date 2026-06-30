using UnityEngine;

/// <summary>
/// Lives on the DeadMaria prefab (the hanging body + noose). The prefab's ROOT origin is the
/// top of the noose, so it gets pinned to TreeNoosePoint and everything hangs below it.
///
/// Responsibilities:
///  - Force the arms limp/straight-down each frame (the hanging clip poses them up).
///  - Gently sway the whole arrangement like a pendulum from the noose top (the root).
///  - Play a one-shot "dramatic" sound the first time the player actually sees the body.
///  - Optionally loop an ambient rope-creak.
///
/// Hand-off: OnFirstSeen fires on that first look — a future killer event can subscribe to it.
/// </summary>
public class DeadMariaHang : MonoBehaviour
{
    [Header("Hands-down override (limp arms, applied after the Animator each frame)")]
    [SerializeField] private bool handsDown = true;
    [Tooltip("0 = keep the animated arms, 1 = fully limp straight down.")]
    [Range(0f, 1f)] [SerializeField] private float handsDownBlend = 1f;

    [Header("Sway (pendulum from the noose top / root)")]
    [SerializeField] private float swayAngle = 3.5f;
    [SerializeField] private float swaySpeed = 0.7f;

    [Header("First-look dramatic sound (lever — assign later)")]
    [SerializeField] private AudioClip dramaticSound;
    [SerializeField] private float dramaticVolume = 1f;
    [Tooltip("Max distance at which a 'first look' counts.")]
    [SerializeField] private float maxSeeDistance = 45f;
    [Tooltip("Layers that block line of sight for the first-look check (walls, terrain, etc.).")]
    [SerializeField] private LayerMask occlusionMask = ~0;
    [Tooltip("Local-space point on the body used for the line-of-sight check (≈ chest, below the root).")]
    [SerializeField] private Vector3 seePointLocalOffset = new Vector3(0f, -1.5f, 0f);

    [Header("Ambient rope creak (lever — optional loop)")]
    [SerializeField] private AudioClip ropeCreakLoop;
    [SerializeField] private float ropeCreakVolume = 0.5f;

    [Header("Sanity drain (seeing / staring at the corpse)")]
    [Tooltip("If true, seeing and staring at the hanging corpse drains the player's sanity.")]
    [SerializeField] private bool drainsSanity = true;
    [Tooltip("One-time sanity lost the first instant the player sees the body.")]
    [SerializeField] private int firstSightSanityLoss = 40;
    [Tooltip("Sanity lost per second while the player keeps staring at the corpse (after the grace period).")]
    [SerializeField] private float stareSanityDrainPerSecond = 1f;
    [Tooltip("Grace period (seconds) after the first sight before the per-second stare drain begins.")]
    [SerializeField] private float stareGracePeriod = 8f;

    /// <summary>Fires once, the first time the player sees the hanging body. Future killer event can hook this.</summary>
    public event System.Action OnFirstSeen;

    private Camera cam;
    private bool seen;
    private float firstSeenTime = -1f;
    private Quaternion baseLocalRot;
    private float swayPhase;

    // Per side [0]=right, [1]=left
    // Per side [0]=right, [1]=left: the full bone chain upper arm -> forearm -> hand -> finger joints -> tip.
    private readonly Transform[][] armChains = new Transform[2][];
    private Renderer[] renderers;
    private AudioSource creakSource;

    private void Awake()
    {
        baseLocalRot = transform.localRotation;
        renderers = GetComponentsInChildren<Renderer>();

        string[] sides = { "Right", "Left" };
        // Ordered Mixamo arm chain down to the fingertip (this rig represents the fingers with the Index chain).
        string[] order = { "Arm", "ForeArm", "Hand", "HandIndex1", "HandIndex2", "HandIndex3", "HandIndex4" };
        for (int i = 0; i < 2; i++)
        {
            armChains[i] = new Transform[order.Length];
            for (int k = 0; k < order.Length; k++)
                armChains[i][k] = FindBoneEndingWith(sides[i] + order[k]);
        }

        if (ropeCreakLoop != null)
        {
            creakSource = gameObject.AddComponent<AudioSource>();
            creakSource.clip = ropeCreakLoop;
            creakSource.loop = true;
            creakSource.volume = ropeCreakVolume;
            creakSource.spatialBlend = 1f;
            creakSource.playOnAwake = false;
            creakSource.Play();
        }
    }

    private void Update()
    {
        // Pendulum sway from the root (= noose top).
        swayPhase += swaySpeed * Time.deltaTime;
        float x = swayAngle * Mathf.Sin(swayPhase);
        float z = swayAngle * 0.5f * Mathf.Sin(swayPhase * 1.3f + 0.7f);
        transform.localRotation = baseLocalRot * Quaternion.Euler(x, 0f, z);

        if (!seen) CheckFirstLook();
        else UpdateStareDrain();
    }

    // Runs after the Animator has applied the hanging pose, so these override it.
    private void LateUpdate()
    {
        if (!handsDown) return;
        // Straighten each arm: walk the chain shoulder->fingertip and rotate every joint so the next
        // bone points straight world-down. Parent-first order means children are re-aligned after their
        // parent moves, giving a single straight limp line (arm + hand + fingers, no curl).
        for (int i = 0; i < 2; i++)
        {
            Transform[] chain = armChains[i];
            if (chain == null) continue;
            for (int k = 0; k < chain.Length - 1; k++)
            {
                Transform cur = chain[k], next = chain[k + 1];
                if (cur == null || next == null) continue;
                Vector3 d = (next.position - cur.position).normalized;
                if (d.sqrMagnitude > 0.0001f)
                {
                    Quaternion target = Quaternion.FromToRotation(d, Vector3.down) * cur.rotation;
                    cur.rotation = Quaternion.Slerp(cur.rotation, target, handsDownBlend);
                }
            }
        }
    }

    private void CheckFirstLook()
    {
        if (!IsCorpseVisible()) return;

        seen = true;
        firstSeenTime = Time.time;
        Debug.Log("DeadMariaHang: player saw the hanging body for the first time.");

        Vector3 seePoint = transform.TransformPoint(seePointLocalOffset);
        if (dramaticSound != null)
            AudioSource.PlayClipAtPoint(dramaticSound, seePoint, dramaticVolume);

        // Big one-time sanity hit the instant the player lays eyes on the body.
        if (drainsSanity && firstSightSanityLoss > 0 && SanityManager.Instance != null)
            SanityManager.Instance.DrainSanity(firstSightSanityLoss);

        OnFirstSeen?.Invoke();
    }

    // After the first sight, staring at the corpse keeps bleeding sanity once the grace period elapses.
    private void UpdateStareDrain()
    {
        if (!drainsSanity || stareSanityDrainPerSecond <= 0f || SanityManager.Instance == null) return;
        if (Time.time - firstSeenTime < stareGracePeriod) return; // grace window after the first sight
        if (IsCorpseVisible())
            SanityManager.Instance.DrainSanityPerSecond(stareSanityDrainPerSecond);
    }

    /// <summary>True if the player currently has the corpse on-screen, within range, with line of sight.</summary>
    private bool IsCorpseVisible()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || renderers == null || renderers.Length == 0) return false;

        Bounds b = default; bool has = false;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (!has) return false;
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b)) return false;

        Vector3 seePoint = transform.TransformPoint(seePointLocalOffset);
        Vector3 camPos = cam.transform.position;
        float dist = Vector3.Distance(camPos, seePoint);
        if (dist > maxSeeDistance) return false;

        Vector3 dir = seePoint - camPos;
        if (Physics.Raycast(camPos, dir.normalized, out RaycastHit hit, dist - 0.3f, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform)) return false; // occluded
        }
        return true;
    }

    /// <summary>Finds the first child transform whose name ends with the given suffix (handles the "mixamorig:" prefix).</summary>
    private Transform FindBoneEndingWith(string suffix)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }
}
