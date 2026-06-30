using UnityEngine;

/// <summary>
/// Orchestrates the Maria killer payoff, built on the hanging corpse left by her visit.
/// Spawned (bare, no model) by GameFlowManager when the end-of-phase MariaLetIn check passes.
///
/// Stages:
///  0. Wait until the player is NOT looking at Maria's hanging corpse →
///       rope-snap SFX (lever) + mute ambience + lock the cabin door (arm the killer event, so
///       TeleportInteractable.lockDuringKillerEvent engages) + the corpse vanishes.
///  1. Wait until the player is inside MariaLookAwayZone AND NOT looking at the tree →
///       spawn the Tomino killer at the tree. It's an ambush killer, so it charges the instant
///       the player looks at it (then jumpscare → the Maria ending).
/// </summary>
public class MariaKillerSequence : MonoBehaviour
{
    [Header("Look detection")]
    [SerializeField] private string lookAwayZoneName = "MariaLookAwayZone";
    [SerializeField] private float notLookingConfirmTime = 0.3f;
    [Tooltip("Half-extents of the box used to test whether the player is 'looking at the tree' (centered above the killer spawn point).")]
    [SerializeField] private Vector3 treeViewHalfExtents = new Vector3(2f, 3f, 2f);

    [Header("Audio")]
    [Tooltip("Rope-break snap SFX when the corpse drops/vanishes (lever — assign later).")]
    [SerializeField] private AudioClip ropeSnapSound;
    [SerializeField] private float ropeSnapVolume = 1f;
    [SerializeField] private string ambienceMuteId = "mariaKiller";

    private ConditionalKillerEvent killerEvent;
    private Camera cam;
    private DeadMariaHang corpse;
    private Renderer[] corpseRenderers;
    private Transform spawnPoint;
    private int stage; // 0 = watching corpse, 1 = watching tree, 2 = done
    private float timer;

    /// <summary>Called by GameFlowManager right after this director is instantiated.</summary>
    public void Begin(ConditionalKillerEvent e)
    {
        // Guard against a double-spawn (e.g. both the phase-2 and phase-3 end checks).
        if (FindObjectsOfType<MariaKillerSequence>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        killerEvent = e;
        corpse = FindObjectOfType<DeadMariaHang>();
        if (corpse != null) corpseRenderers = corpse.GetComponentsInChildren<Renderer>();

        if (e != null && !string.IsNullOrEmpty(e.spawnPointName))
        {
            GameObject sp = GameObject.Find(e.spawnPointName);
            if (sp != null) spawnPoint = sp.transform;
        }
        Debug.Log($"MariaKillerSequence: begun. corpse={(corpse != null)}, spawnPoint={(spawnPoint != null)}");
    }

    private void Update()
    {
        if (killerEvent == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (stage == 0)
        {
            bool lookingAtCorpse = corpse != null && IsBoundsVisible(GetCorpseBounds());
            if (!lookingAtCorpse) { timer += Time.deltaTime; if (timer >= notLookingConfirmTime) DoVanish(); }
            else timer = 0f;
        }
        else if (stage == 1)
        {
            bool inZone = PlayerZoneTracker.IsInZone(lookAwayZoneName);
            bool lookingAtTree = IsBoundsVisible(GetTreeBounds());
            if (inZone && !lookingAtTree) { timer += Time.deltaTime; if (timer >= notLookingConfirmTime) DoSpawnKiller(); }
            else timer = 0f;
        }
    }

    private void DoVanish()
    {
        stage = 1;
        timer = 0f;
        Debug.Log("MariaKillerSequence: player looked away from the corpse — snap, mute, lock, vanish.");

        Vector3 at = spawnPoint != null ? spawnPoint.position : transform.position;
        if (ropeSnapSound != null) AudioSource.PlayClipAtPoint(ropeSnapSound, at, ropeSnapVolume);

        AmbientSoundManager.Instance?.Mute(ambienceMuteId);

        // Arm the killer event → IsKillerEventActive → the cabin door (TeleportInteractable) locks.
        GameFlowManager.Instance?.SetKillerEventArmed(killerEvent);

        if (corpse != null) Destroy(corpse.gameObject);
    }

    private void DoSpawnKiller()
    {
        stage = 2;
        Debug.Log("MariaKillerSequence: player at the zone, not looking at the tree — spawning the killer.");
        GameFlowManager.Instance?.SpawnKillerFromSequence(killerEvent);
        Destroy(gameObject); // the ambush killer takes over from here
    }

    private Bounds GetCorpseBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.one);
        bool has = false;
        if (corpseRenderers != null)
            foreach (var r in corpseRenderers)
            {
                if (r == null || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        return b;
    }

    private Bounds GetTreeBounds()
    {
        Vector3 c = spawnPoint != null ? spawnPoint.position : transform.position;
        return new Bounds(c + Vector3.up * treeViewHalfExtents.y, treeViewHalfExtents * 2f);
    }

    private bool IsBoundsVisible(Bounds b)
    {
        return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
    }
}
