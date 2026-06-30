using UnityEngine;

/// <summary>
/// Drives Maria's "hang on the tree" beat. Lives on the Maria visitor prefab.
///
/// Flow: Maria delivers her back-gate line at MariaStopSite (a waypoint flagged
/// holdAfterDialogue), so her EventNPC fires OnHoldForSignal and holds her standing there.
/// This component then watches for the player to BOTH (a) be inside MariaLookAwayZone and
/// (b) NOT be looking at Maria. Once both are true for a brief moment, Maria vanishes, a
/// neck-break SFX plays, DeadMaria is spawned with the noose top pinned to TreeNoosePoint,
/// and the event is force-completed so the next event starts.
/// </summary>
[RequireComponent(typeof(EventNPC))]
public class MariaHangTrigger : MonoBehaviour
{
    [Header("Trigger Conditions")]
    [Tooltip("Trigger zone the player must be inside (matches the GameObject name).")]
    [SerializeField] private string lookAwayZoneName = "MariaLookAwayZone";

    [Tooltip("Seconds the player must be in the zone AND not looking at Maria before she hangs.")]
    [SerializeField] private float notLookingConfirmTime = 0.25f;

    [Header("Spawn")]
    [Tooltip("The DeadMaria (hanging body + noose) prefab. Its root origin is the top of the noose.")]
    [SerializeField] private GameObject deadMariaPrefab;

    [Tooltip("Scene anchor the top of the noose is pinned to.")]
    [SerializeField] private string treeNoosePointName = "TreeNoosePoint";

    [Header("Audio")]
    [Tooltip("Neck-break SFX played the instant she hangs.")]
    [SerializeField] private AudioClip neckBreakSound;
    [SerializeField] private float neckBreakVolume = 1f;

    [Header("Timing")]
    [Tooltip("Seconds to wait after the hang before the event completes and the next event starts.")]
    [SerializeField] private float bufferBeforeNextEvent = 10f;

    private EventNPC eventNPC;
    private Camera cam;
    private Renderer[] renderers;
    private bool armed;
    private bool triggered;
    private float notLookingTimer;

    private void Awake()
    {
        eventNPC = GetComponent<EventNPC>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        if (eventNPC != null) eventNPC.OnHoldForSignal += Arm;
    }

    private void OnDisable()
    {
        if (eventNPC != null) eventNPC.OnHoldForSignal -= Arm;
    }

    private void Arm()
    {
        armed = true;
        // She only ever reaches this hold on the "let her in" path — record it durably so an
        // end-of-phase conditional killer event can check it (GetStatValue "MariaLetIn" >= 1).
        if (GameManager.Instance != null) GameManager.Instance.MariaLetIn = 1;
        Debug.Log("MariaHangTrigger: armed — Maria holding at the stop site (MariaLetIn recorded).");
    }

    private void Update()
    {
        if (!armed || triggered) return;
        if (cam == null) cam = Camera.main;

        bool inZone = PlayerZoneTracker.IsInZone(lookAwayZoneName);
        bool looking = IsVisibleToCamera();

        if (inZone && !looking)
        {
            notLookingTimer += Time.deltaTime;
            if (notLookingTimer >= notLookingConfirmTime) DoHang();
        }
        else
        {
            notLookingTimer = 0f;
        }
    }

    /// <summary>True if any of Maria's renderers is inside the camera's view frustum.</summary>
    private bool IsVisibleToCamera()
    {
        if (cam == null || renderers == null || renderers.Length == 0) return false;

        Bounds b = default;
        bool has = false;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        return GeometryUtility.TestPlanesAABB(planes, b);
    }

    private void DoHang()
    {
        triggered = true;
        Debug.Log("MariaHangTrigger: player looked away inside the zone — Maria hangs.");

        Transform noosePoint = FindByName(treeNoosePointName);
        Vector3 spawnPos = noosePoint != null ? noosePoint.position : transform.position;
        Quaternion spawnRot = noosePoint != null ? noosePoint.rotation : Quaternion.identity;

        PlayLoud2D(neckBreakSound, neckBreakVolume);

        if (deadMariaPrefab != null)
            Instantiate(deadMariaPrefab, spawnPos, spawnRot);
        else
            Debug.LogWarning("MariaHangTrigger: deadMariaPrefab is not assigned — nothing spawned.");

        // The standing Maria vanishes instantly (the player is looking away); only the body in the tree remains.
        HideStandingMaria();

        // Buffer before the event completes, so the hang has a beat before the next visitor arrives.
        StartCoroutine(CompleteAfterBuffer());
    }

    // Play a clip as a 2D one-shot (no 3D distance falloff) so the neck-break hits the player at full volume.
    private static void PlayLoud2D(AudioClip clip, float volume)
    {
        if (clip == null) return;
        var go = new GameObject("MariaNeckBreakSFX");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 0f; // 2D — full volume regardless of distance from the player
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    /// <summary>Hides the standing Maria but keeps this GameObject active so the buffer coroutine can run.</summary>
    private void HideStandingMaria()
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    private System.Collections.IEnumerator CompleteAfterBuffer()
    {
        if (bufferBeforeNextEvent > 0f)
            yield return new WaitForSeconds(bufferBeforeNextEvent);
        // Fires OnNPCEventCompleted -> the queue advances; also destroys this (already-hidden) Maria.
        eventNPC.ForceComplete();
    }

    private Transform FindByName(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        GameObject go = GameObject.Find(n);
        return go != null ? go.transform : null;
    }
}
