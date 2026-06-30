using UnityEngine;

/// <summary>
/// End-of-phase cleanup for Maria's hanging corpse. Spawned (bare, no model) by GameFlowManager
/// when an end-of-phase Maria killer-event entry's condition (MariaLetIn >= 1) passes.
///
/// The Maria KILLER / jumpscare was removed — seeing the hanging corpse now drains the player's
/// sanity instead (see DeadMariaHang). This director enforces the intended end-of-phase flow:
///   1. wait until the player triggers the removal mechanic — i.e. is NOT looking at the body
///      (bounded by maxWaitForLookAway so a staring player can't stall the game),
///   2. rope-snap SFX, the body drops/vanishes,
///   3. a gracePeriodAfterRemoval beat (default 5s),
///   4. advance the main event queue (StartNextEvent) — so the actual end-of-phase jumpscare (the
///      next killer-event entry) fires, now that the corpse is gone and the grace has elapsed,
///   5. self-destruct.
///
/// For this to read as "corpse retired → grace → killer", the Maria killer-event entry must sit
/// BEFORE the real killer entries at each phase end (phase 3 already does; phase 2 was reordered so
/// Maria#1 precedes the Spirit/GraveRobber entries).
///
/// It ALWAYS advances the queue EXACTLY once. A Maria entry whose condition passes is NON-terminal
/// now (the original killer was a game-over leaf), so failing to advance would STALL the queue and
/// the end-of-phase jumpscare would never fire. If there is no corpse to retire — e.g. she was let
/// in during phase 2 so the phase-2 entry already cleaned it up, and this is the phase-3 entry
/// (MariaLetIn stays 1) — it advances IMMEDIATELY with no grace (nothing was retired).
///
/// (Class name kept: GameFlowManager.StartKillerEventDirectly looks it up by type and the
/// MariaKillerDirector prefab carries this component with its rope-snap clip assigned.)
/// </summary>
public class MariaKillerSequence : MonoBehaviour
{
    [Header("Look detection")]
    [Tooltip("Seconds the player must be looking AWAY from the corpse before it is retired.")]
    [SerializeField] private float notLookingConfirmTime = 0.3f;

    [Tooltip("Safety cap (seconds): retire the corpse and continue even if the player never looks away, so the end-of-phase jumpscare is never blocked.")]
    [SerializeField] private float maxWaitForLookAway = 6f;

    [Header("Grace")]
    [Tooltip("Seconds to wait AFTER the corpse is removed before the end-of-phase killer event is allowed to fire.")]
    [SerializeField] private float gracePeriodAfterRemoval = 5f;

    [Header("Audio")]
    [Tooltip("Rope-break snap SFX when the corpse drops/vanishes.")]
    [SerializeField] private AudioClip ropeSnapSound;
    [SerializeField] private float ropeSnapVolume = 1f;

    private Camera cam;
    private DeadMariaHang corpse;
    private Renderer[] corpseRenderers;
    private bool retiring; // corpse retired; waiting out the grace before handing the queue on
    private bool done;
    private float notLookingTimer;
    private float elapsed;

    /// <summary>Called by GameFlowManager right after this director is instantiated.</summary>
    public void Begin(ConditionalKillerEvent e)
    {
        corpse = FindObjectOfType<DeadMariaHang>();
        if (corpse != null)
        {
            corpseRenderers = corpse.GetComponentsInChildren<Renderer>();
            Debug.Log("MariaKillerSequence: corpse-cleanup director begun (retire corpse → grace → continue the queue).");
        }
        else
        {
            Debug.Log("MariaKillerSequence: no corpse to retire — will advance the queue next frame.");
        }
    }

    private void Update()
    {
        if (done || retiring) return;

        // No corpse (already removed, e.g. the phase-2 cleanup ran) → hand the queue back
        // immediately, with no grace (there was nothing to retire).
        if (corpse == null) { Finish(); return; }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        elapsed += Time.deltaTime;

        bool lookingAtCorpse = IsBoundsVisible(GetCorpseBounds());
        if (!lookingAtCorpse)
        {
            notLookingTimer += Time.deltaTime;
            if (notLookingTimer >= notLookingConfirmTime) { RetireCorpse(); return; }
        }
        else notLookingTimer = 0f;

        // Safety: never let a staring player block the end-of-phase jumpscare forever.
        if (elapsed >= maxWaitForLookAway) RetireCorpse();
    }

    private void RetireCorpse()
    {
        if (retiring) return;
        retiring = true;

        if (ropeSnapSound != null)
        {
            Vector3 at = corpse != null ? corpse.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(ropeSnapSound, at, ropeSnapVolume);
        }
        if (corpse != null) Destroy(corpse.gameObject);
        Debug.Log($"MariaKillerSequence: rope snaps, corpse removed — {gracePeriodAfterRemoval}s grace before the killer.");

        StartCoroutine(GraceThenAdvance());
    }

    private System.Collections.IEnumerator GraceThenAdvance()
    {
        if (gracePeriodAfterRemoval > 0f) yield return new WaitForSeconds(gracePeriodAfterRemoval);
        Finish();
    }

    // Hand control back to the event queue (the corpse is gone and the grace has elapsed) and clean up.
    private void Finish()
    {
        if (done) return;
        done = true;
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.StartNextEvent();
        Destroy(gameObject);
    }

    private Bounds GetCorpseBounds()
    {
        Bounds b = new Bounds(corpse != null ? corpse.transform.position : transform.position, Vector3.one);
        bool has = false;
        if (corpseRenderers != null)
            foreach (var r in corpseRenderers)
            {
                if (r == null || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        return b;
    }

    private bool IsBoundsVisible(Bounds b)
    {
        return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
    }
}
