using System.Collections;
using UnityEngine;

/// <summary>
/// Director for the game's final "secret" sequence — the FatherKiller.
///
/// Kicked off by <see cref="GraveRoseInteractable"/> once the player places the rose on the
/// Father's grave and is teleported to the forest (ForestSpawn). From there this runs a
/// choreographed cat-and-mouse finale that always ends in a jumpscare and unlocks the secret
/// "The Father" ending (via the <see cref="KillerJumpscare"/> on this object).
///
/// Choreography:
///   1. Killer waits at a far spawn, collapsed and twitching (Laying Seizure) for a grace period.
///   2. It rises into a light chase (Sneak Walk) — fast when far, slowing to a creep up close. It
///      carries the same terror-radius heartbeat and flashlight flicker as the Wraith (CryptKiller).
///        - Reaches the player at any point -> jumpscare (FatherJumpscare + FatherJumpscareSound).
///        - Player SEES it from close range -> it sprints off-screen very fast (Run_final_catch).
///   3. After fleeing it vanishes briefly, then returns from the far spawn for a second light chase.
///   4. Execution: it positions directly in front of the player and sprints extremely fast into
///      them (Run_final_catch), and the kill plays the FatherJumpscare scare in the player's face.
///
/// The forest has no baked NavMesh, so movement is direct kinematic steering (MoveTowards + a
/// down-raycast ground clamp), like KillerNPC's direct-movement fallback.
/// </summary>
[RequireComponent(typeof(KillerJumpscare))]
public class FatherKillerSequence : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Where the killer lurks during grace and reappears from after fleeing (a far point from ForestSpawn).")]
    [SerializeField] private Transform killerFarSpawn;
    [Tooltip("Player (auto-found by tag if unset).")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Player camera (auto-found via Camera.main if unset).")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("The animator driving the FatherKiller model (auto-found in children if unset).")]
    [SerializeField] private Animator animator;

    [Header("Timing")]
    [SerializeField] private float graceDuration = 30f;
    [Tooltip("How long the killer sprints away after being seen, before it vanishes.")]
    [SerializeField] private float fleeDuration = 1.5f;
    [Tooltip("Grace/breather between chase phases — he vanishes far away (heartbeat fades) for this long between pursuits.")]
    [SerializeField] private float graceBetweenPhases = 10f;
    [Tooltip("Safety cap on the execution sprint before it forces the kill.")]
    [SerializeField] private float executionTimeout = 5f;

    [Header("Movement Speeds")]
    [Tooltip("Creep speed when right on top of the player (Sneak Walk).")]
    [SerializeField] private float walkSpeed = 2.6f;
    [Tooltip("Faster Sneak Walk speed used when the killer is far from the player.")]
    [SerializeField] private float farWalkSpeed = 6.5f;
    [Tooltip("At/under this distance the killer moves at walkSpeed.")]
    [SerializeField] private float nearWalkDistance = 6f;
    [Tooltip("At/over this distance the killer moves at farWalkSpeed.")]
    [SerializeField] private float farWalkDistance = 26f;
    [Tooltip("Speed when fleeing off-screen (Run).")]
    [SerializeField] private float fleeSpeed = 16f;
    [Tooltip("Extremely fast speed for the final execution charge.")]
    [SerializeField] private float executionSpeed = 24f;
    [Tooltip("Distance at which the killer catches the player and triggers the jumpscare.")]
    [SerializeField] private float killDistance = 1.9f;
    [Tooltip("During the second light chase, flip to the execution once within this distance.")]
    [SerializeField] private float executionRange = 9f;
    [Tooltip("How far in front of the player the killer is placed before the execution charge.")]
    [SerializeField] private float executionStartDistance = 14f;

    [Header("Dart Across Screen")]
    [Tooltip("How far in front of the player the dart happens.")]
    [SerializeField] private float dartDistance = 16f;
    [Tooltip("How wide the dart sweeps, as a fraction of the half-FOV to each side (keeps him on-screen).")]
    [Range(0.3f, 0.95f)]
    [SerializeField] private float dartViewportFraction = 0.72f;
    [Tooltip("How fast he crosses the screen.")]
    [SerializeField] private float dartDuration = 0.7f;
    [Tooltip("Beat where he appears off to the side before darting across.")]
    [SerializeField] private float dartStartHold = 0.3f;

    [Header("\"Player sees me\" detection")]
    [Tooltip("Half-angle (deg) from camera forward within which the killer counts as seen.")]
    [SerializeField] private float seeHalfAngle = 32f;
    [Tooltip("How long the killer must be in view before it counts as seen (debounce).")]
    [SerializeField] private float seeConfirmTime = 0.12f;
    [Tooltip("The player must be within this distance for seeing the killer to make it flee (0 = unlimited).")]
    [SerializeField] private float maxSeeDistance = 20f;
    [Tooltip("Layers that block line of sight (trees/terrain). If Nothing, line-of-sight is not checked.")]
    [SerializeField] private LayerMask viewBlockingLayers;

    [Header("Ground Clamp")]
    [Tooltip("Layers treated as ground for the down-raycast clamp.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Terror Radius (Heartbeat) — matches the Wraith")]
    [SerializeField] private AudioClip heartbeatSound;
    [SerializeField] private float terrorRadius = 20f;
    [SerializeField] private float terrorMaxVolumeDistance = 3f;
    [SerializeField] [Range(0f, 1f)] private float heartbeatMaxVolume = 1f;

    [Header("Animation Parameters")]
    [SerializeField] private string downBool = "IsDown";
    [SerializeField] private string walkBool = "IsWalking";
    [SerializeField] private string runBool = "IsRunning";
    [Tooltip("Trigger that routes the animator to the FatherJumpscare state (the kill).")]
    [SerializeField] private string jumpscareTrigger = "Jumpscare";

    // Runtime
    private KillerJumpscare killerJumpscare;
    private AudioSource heartbeatSource;
    private bool started;
    private bool threatening;       // heartbeat + flashlight flicker active
    private bool ended;             // a jumpscare has fired; stop everything
    private float seeTimer;

    // Outcome flags set by LightChase().
    private bool fled;
    private bool toExecution;

    private void Awake()
    {
        killerJumpscare = GetComponent<KillerJumpscare>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        SetupHeartbeatAudio();
    }

    private void Start()
    {
        EnsureRefs();
    }

    private void Update()
    {
        if (threatening && !ended) UpdateHeartbeat();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>
    /// Starts the finale. Safe to call once; subsequent calls are ignored.
    /// Pass a far-spawn transform when spawned at runtime (the prefab can't hold a scene reference).
    /// </summary>
    public void Begin(Transform farSpawnOverride = null)
    {
        if (started) return;
        if (farSpawnOverride != null) killerFarSpawn = farSpawnOverride;
        started = true;
        EnsureRefs();
        StartCoroutine(RunSequence());
    }

    public bool HasStarted => started;

    /// <summary>
    /// Whether the flashlight should flicker near this killer (mirrors KillerNPC.CausesFlashlightFlicker).
    /// True only once the killer is actively threatening (from the first light chase onward).
    /// </summary>
    public bool CausesFlashlightFlicker => threatening && !ended;

    // ---------------------------------------------------------------- master sequence

    private IEnumerator RunSequence()
    {
        // 1. Stay invisible far away during the grace period (no threat — heartbeat/flicker off).
        if (killerFarSpawn != null)
        {
            transform.position = killerFarSpawn.position;
            transform.rotation = killerFarSpawn.rotation;
        }
        GroundClamp();
        SetVisible(false);
        SetAnim();
        threatening = false;
        yield return new WaitForSeconds(graceDuration);
        if (ended) yield break;

        // 2. Appear and begin the first light chase.
        SetVisible(true);
        FacePlayer(true);
        GroundClamp();
        threatening = true;
        SetAnim(walk: true);
        fled = false; toExecution = false;
        yield return LightChase(fleeOnSeen: true, executeOnSeen: false, execRange: -1f);
        if (ended) yield break;

        if (fled)
        {
            // light chase A exits by sprinting off-screen
            yield return Flee();
            if (ended) yield break;

            // grace
            Hide();
            yield return new WaitForSeconds(graceBetweenPhases);
            if (ended) yield break;

            // dart across the player's view — appears off to the side, then bolts across
            yield return DartAcrossScreen();
            if (ended) yield break;

            // grace
            Hide();
            yield return new WaitForSeconds(graceBetweenPhases);
            if (ended) yield break;

            // light chase B (also runs off to the side when seen, like the first chase)
            Reappear();
            SetAnim(walk: true);
            fled = false; toExecution = false;
            yield return LightChase(fleeOnSeen: true, executeOnSeen: false, execRange: executionRange);
            if (ended) yield break;
            if (fled) { yield return Flee(); if (ended) yield break; }
        }

        // grace, then the execution
        Hide();
        yield return new WaitForSeconds(graceBetweenPhases);
        if (ended) yield break;
        yield return Execution();
    }

    // ---------------------------------------------------------------- phases

    /// <summary>
    /// Creeps toward the player (fast when far, slow when close). Ends by: catching the player
    /// (fires the FatherJumpscare and sets <see cref="ended"/>), being seen from close range
    /// (sets <see cref="fled"/> or <see cref="toExecution"/>), or reaching <paramref name="execRange"/>.
    /// </summary>
    private IEnumerator LightChase(bool fleeOnSeen, bool executeOnSeen, float execRange)
    {
        seeTimer = 0f;
        while (!ended)
        {
            if (playerTransform == null) { EnsureRefs(); yield return null; continue; }

            float dist = HorizontalDistanceToPlayer();
            float speed = Mathf.Lerp(walkSpeed, farWalkSpeed, Mathf.InverseLerp(nearWalkDistance, farWalkDistance, dist));
            dist = StepToward(playerTransform.position, speed, faceInstant: false);

            if (dist <= killDistance)
            {
                Catch();                     // FatherJumpscare
                yield break;
            }

            if (execRange > 0f && dist <= execRange)
            {
                toExecution = true;
                yield break;
            }

            if (IsSeenConfirmed())
            {
                if (fleeOnSeen) { fled = true; yield break; }
                if (executeOnSeen) { toExecution = true; yield break; }
            }

            yield return null;
        }
    }

    /// <summary>Darts off to the side (toward the nearer screen edge), fast, facing where he runs.</summary>
    private IEnumerator Flee()
    {
        SetAnim(run: true);
        Vector3 fleeDir = ComputeFleeDir();   // fixed for the whole flee so he runs a straight line off-screen
        FaceDir(fleeDir, true);
        float t = 0f;
        while (t < fleeDuration && !ended)
        {
            t += Time.deltaTime;
            transform.position += fleeDir * fleeSpeed * Time.deltaTime;
            GroundClamp();
            FaceDir(fleeDir, true);            // face the run direction so the run anim matches (no moonwalk)
            yield return null;
        }
    }

    /// <summary>Mostly-sideways flee direction toward the nearer screen edge, with a slight away bias.</summary>
    private Vector3 ComputeFleeDir()
    {
        Vector3 right = FlatCameraRight();
        Vector3 away = transform.forward;
        if (playerTransform != null)
        {
            away = transform.position - playerTransform.position;
            away.y = 0f;
        }
        away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;

        // Pick the side the killer is already on so he exits the nearest edge of the view.
        float sideSign = 1f;
        if (playerTransform != null && Vector3.Dot(transform.position - playerTransform.position, right) < 0f)
            sideSign = -1f;

        Vector3 dir = right * sideSign + away * 0.35f; // mostly sideways, slight away so he never curves toward the player
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : right * sideSign;
    }

    private void FaceDir(Vector3 dir, bool instant)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir.normalized);
        transform.rotation = instant ? target : Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
    }

    /// <summary>Teleports far from the player and becomes visible again for the final chase.</summary>
    private void Reappear()
    {
        Vector3 pos = killerFarSpawn != null ? killerFarSpawn.position : transform.position;
        // If the far spawn ended up near the player, drop back behind them instead.
        if (playerTransform != null && Vector3.Distance(pos, playerTransform.position) < 22f)
        {
            Vector3 fwd = playerCamera != null ? playerCamera.transform.forward : playerTransform.forward;
            fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
            pos = playerTransform.position - fwd * 30f;
        }
        transform.position = pos;
        GroundClamp();
        SetVisible(true);
        FacePlayer(true);
    }

    /// <summary>Positions in front of the player, then charges straight in at very high speed.</summary>
    private IEnumerator Execution()
    {
        // Place the killer directly in front of the player, in view, and grounded.
        if (playerTransform != null)
        {
            Vector3 fwd = playerCamera != null ? playerCamera.transform.forward : playerTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.position = playerTransform.position + fwd * executionStartDistance;
            FacePlayer(true);
            GroundClamp();
        }

        SetVisible(true);
        SetAnim(run: true);

        float t = 0f;
        while (!ended)
        {
            if (playerTransform == null) { EnsureRefs(); yield return null; continue; }

            t += Time.deltaTime;
            float dist = StepToward(playerTransform.position, executionSpeed, faceInstant: true);

            if (dist <= killDistance || t >= executionTimeout)
            {
                Catch();                     // FatherJumpscare in the player's face
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>Appears off to one side in front of the player, then bolts across their view (always on-screen).</summary>
    private IEnumerator DartAcrossScreen()
    {
        if (playerCamera == null) EnsureRefs();
        if (playerCamera == null) yield break;

        float dir = Random.value < 0.5f ? 1f : -1f;   // sweep direction
        // Keep the sweep within the camera's horizontal FOV so he's always on-screen.
        float vHalf = playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.1f, playerCamera.aspect));
        float maxSide = dartDistance * Mathf.Tan(hHalf) * dartViewportFraction;
        float fromX = -maxSide * dir;
        float toX = maxSide * dir;

        // Appear off to the side, in front, facing across.
        PositionInFrontOfCamera(fromX, dartDistance);
        FaceDart(toX - fromX);
        SetVisible(true);
        SetAnim(run: true);
        yield return new WaitForSeconds(dartStartHold);
        if (ended) yield break;

        float t = 0f;
        while (t < 1f && !ended)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dartDuration);
            PositionInFrontOfCamera(Mathf.Lerp(fromX, toX, t), dartDistance);
            FaceDart(toX - fromX);   // re-evaluate vs the live camera so he stays in view even if the player turns
            yield return null;
        }
        SetVisible(false);
    }

    /// <summary>Places the killer at a side offset, a fixed distance in front of the live camera, grounded.</summary>
    private void PositionInFrontOfCamera(float sideOffset, float dist)
    {
        if (playerCamera == null) return;
        Vector3 fwd = playerCamera.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        transform.position = playerCamera.transform.position + fwd * dist + FlatCameraRight() * sideOffset;
        GroundClamp();
    }

    private void FaceDart(float dirSign)
    {
        Vector3 move = FlatCameraRight() * dirSign;
        if (move.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(move);
    }

    private Vector3 FlatCameraRight()
    {
        Vector3 r = playerCamera != null ? playerCamera.transform.right : Vector3.right;
        r.y = 0f;
        if (r.sqrMagnitude < 0.01f) r = Vector3.right;
        return r.normalized;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Steps the killer toward <paramref name="worldTarget"/> on the XZ plane, clamps to the
    /// ground, and faces the player. Returns the resulting horizontal distance to the player.
    /// </summary>
    private float StepToward(Vector3 worldTarget, float speed, bool faceInstant)
    {
        Vector3 pos = transform.position;
        Vector3 flatTarget = new Vector3(worldTarget.x, pos.y, worldTarget.z);
        Vector3 next = Vector3.MoveTowards(pos, flatTarget, speed * Time.deltaTime);
        transform.position = next;
        GroundClamp();
        FacePlayer(faceInstant);
        return HorizontalDistanceToPlayer();
    }

    private float HorizontalDistanceToPlayer()
    {
        if (playerTransform == null) return Mathf.Infinity;
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude;
    }

    private void FacePlayer(bool instant)
    {
        if (playerTransform == null) return;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = instant
            ? target
            : Quaternion.Slerp(transform.rotation, target, 8f * Time.deltaTime);
    }

    /// <summary>
    /// Snaps the killer's Y onto the ground beneath it (skips self/player colliders and anything
    /// above chest height, so it can't latch onto tree canopies). Picks the highest valid ground.
    /// </summary>
    private void GroundClamp()
    {
        Vector3 pos = transform.position;
        Vector3 origin = pos + Vector3.up * 3f;
        float ceiling = pos.y + 1.2f; // ignore overhead geometry (branches/canopies)
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f, groundMask, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        bool found = false;
        foreach (var h in hits)
        {
            Transform ht = h.collider.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue;
            if (playerTransform != null && (ht == playerTransform || ht.IsChildOf(playerTransform))) continue;
            if (h.point.y > ceiling) continue;
            if (h.point.y > bestY) { bestY = h.point.y; found = true; }
        }
        if (found)
        {
            pos.y = bestY;
            transform.position = pos;
        }
    }

    private bool IsSeenConfirmed()
    {
        if (IsSeenByPlayer())
        {
            seeTimer += Time.deltaTime;
            if (seeTimer >= seeConfirmTime) return true;
        }
        else
        {
            seeTimer = 0f;
        }
        return false;
    }

    /// <summary>True if the killer is within the camera's view cone, close enough, and not occluded.</summary>
    private bool IsSeenByPlayer()
    {
        if (playerCamera == null) return false;

        Vector3 camPos = playerCamera.transform.position;
        Vector3 killerCenter = transform.position + Vector3.up * 1f;
        Vector3 toKiller = killerCenter - camPos;
        float dist = toKiller.magnitude;
        if (dist < 0.001f) return true;

        if (maxSeeDistance > 0f && dist > maxSeeDistance) return false;

        float angle = Vector3.Angle(playerCamera.transform.forward, toKiller.normalized);
        if (angle > seeHalfAngle) return false;

        // Line of sight: if something on the blocking layers is between camera and killer, it's hidden.
        if (viewBlockingLayers.value != 0)
        {
            if (Physics.Raycast(camPos, toKiller.normalized, out RaycastHit hit, dist - 0.5f, viewBlockingLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                    return false;
            }
        }
        return true;
    }

    /// <summary>Fires the shared FatherJumpscare (which unlocks the ending and returns to the menu).</summary>
    private void Catch()
    {
        if (ended) return;
        ended = true;
        threatening = false;
        StopHeartbeat();
        // Clear locomotion bools first, otherwise AnyState->Run (IsRunning still true from the
        // execution charge) immediately pulls the animator back out of FatherJumpscare into the run.
        SetAnim();
        if (killerJumpscare != null) killerJumpscare.TriggerJumpscare(jumpscareTrigger);
    }

    // ---------------------------------------------------------------- animation / visibility

    private void SetAnim(bool down = false, bool walk = false, bool run = false)
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(downBool)) animator.SetBool(downBool, down);
        if (!string.IsNullOrEmpty(walkBool)) animator.SetBool(walkBool, walk);
        if (!string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, run);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    /// <summary>Vanishes for a breather: hides, idles, and retreats to the far spawn so the heartbeat fades.</summary>
    private void Hide()
    {
        SetVisible(false);
        SetAnim();
        if (killerFarSpawn != null)
        {
            transform.position = killerFarSpawn.position;
            GroundClamp();
        }
    }

    // ---------------------------------------------------------------- terror radius (mirrors CryptKiller)

    private void SetupHeartbeatAudio()
    {
        GameObject obj = new GameObject("FatherHeartbeat");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        heartbeatSource = obj.AddComponent<AudioSource>();
        heartbeatSource.clip = heartbeatSound;
        heartbeatSource.loop = true;
        heartbeatSource.playOnAwake = false;
        heartbeatSource.spatialBlend = 0f; // 2D — plays directly to the player
        heartbeatSource.volume = 0f;
        if (heartbeatSound != null) heartbeatSource.Play();
    }

    private void UpdateHeartbeat()
    {
        if (heartbeatSource == null || heartbeatSound == null || playerTransform == null) return;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= terrorRadius)
        {
            float t = Mathf.InverseLerp(terrorRadius, terrorMaxVolumeDistance, dist);
            heartbeatSource.volume = Mathf.Lerp(0f, heartbeatMaxVolume, t);
            if (!heartbeatSource.isPlaying) heartbeatSource.Play();
        }
        else
        {
            heartbeatSource.volume = 0f;
        }
    }

    private void StopHeartbeat()
    {
        if (heartbeatSource != null) heartbeatSource.Stop();
    }

    // ---------------------------------------------------------------- refs

    private void EnsureRefs()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        if (playerCamera == null) playerCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (killerJumpscare == null) killerJumpscare = GetComponent<KillerJumpscare>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, killDistance);
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, terrorRadius);
        if (killerFarSpawn != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(killerFarSpawn.position, 1f);
        }
    }
}
