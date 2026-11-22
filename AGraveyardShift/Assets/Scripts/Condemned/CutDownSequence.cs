using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// "Cut Down": the first visitor you turned away drops out of the tree by the east grave row on a rope, kicks,
/// hangs, the rope parts, and what lands puts itself back together the wrong way round and goes up the tree.
/// A port of the pitch clip r2_drop_cutdown: every frame is a function of the sequence clock, which runs over
/// the clip's own times (1.0 to 9.6 s). The player keeps control throughout; nothing here moves the camera.
/// </summary>
public class CutDownSequence : MonoBehaviour
{
    // the clip's beats
    const float T_START = 1.0f, T_DROP = 1.40f, T_HIT = 1.58f;
    const float T_SNAP = 3.50f, T_FEET = 3.61f, T_BUCK = 3.69f, T_KNEEL = 3.76f, T_SLAM = 3.82f, T_ROPE = 3.93f;
    const float T_RE0 = 4.60f, T_RE1 = 6.40f, T_TRUNK = 7.00f, T_PARK = 7.80f, T_END = 9.6f;
    const float DROP = 1.30f, ROPE_LEN = 11f, FEET_UP = 1.5f;
    const float REF_LEG = 1.333f;          // leg length of the model the poses were built on; others are scaled to it
    static readonly Vector3 TRUNK0 = new Vector3(-96.99f, 0f, 120.86f);
    // the trunk of the tree it climbs: axis (x, z) and radius by height above the ground
    static readonly float[] TH = { 0.1f, 0.4f, 0.8f, 1.2f, 1.8f, 2.5f, 3.2f, 4.0f, 5.0f, 6.0f, 7.0f };
    static readonly float[] TR = { 1.00f, 0.85f, 0.64f, 0.58f, 0.55f, 0.48f, 0.43f, 0.34f, 0.30f, 0.29f, 0.22f };
    static readonly float[] TX = { -96.98f, -96.98f, -97.00f, -97.00f, -96.99f, -96.99f, -97.00f, -97.08f, -97.16f, -97.12f, -97.01f };
    static readonly float[] TZ = { 120.81f, 120.80f, 120.81f, 120.82f, 120.86f, 120.92f, 120.99f, 121.06f, 121.20f, 121.24f, 121.14f };

    CondemnedConfig cfg;
    GameObject body; Animator an;
    readonly Dictionary<HumanBodyBones, Transform> bones = new Dictionary<HumanBodyBones, Transform>();
    readonly Dictionary<HumanBodyBones, Quaternion> off = new Dictionary<HumanBodyBones, Quaternion>();
    float lUA, lLA, lUL, lLL, k = 1f;
    Vector3 O, D, Y, S;                       // the floor it is on: origin, its way, off the floor, its side
    readonly int[] mode = new int[4];         // per limb (hand L, hand R, foot L, foot R): 0 ground, 1 trunk, 2 a free point on the bark
    Vector3 B, B0, Dg, Sg, Dh, Sh, anchor, fwdH;
    float gy;
    GameObject pivot, rope, stub, fall, noose, remains;
    GameObject[] strands, pile, frays;
    Vector3[] pilePts;
    Vector3 neck0, neckSnap;
    Transform neckBone;
    CondemnedRig.Snapshot snap;
    Pose heap, buckle, kneel, bridge, onBark0, onBark1;
    Pose[] re;
    const float RUN_END = 2.42f;

    float clock = -1f;
    bool tookSanity, soundPaused;
    AudioSource sound;
    readonly List<Object> owned = new List<Object>();

    public bool Running { get { return clock >= 0f; } }
    public bool Finished { get; private set; }

    // ================================================================== building

    /// <summary>Builds the hanging body (hidden) well before it is needed, so the moment itself costs nothing.</summary>
    public static CutDownSequence Prepare(CondemnedConfig cfg, GameObject visitorPrefab)
    {
        if (cfg == null || visitorPrefab == null || cfg.hangClip == null) return null;
        var go = new GameObject("CondemnedCutDown");
        var cd = go.AddComponent<CutDownSequence>();
        bool ok = false;
        try { ok = cd.Build(cfg, visitorPrefab); }
        catch (System.Exception e) { Debug.LogWarning("CutDownSequence: could not build: " + e); }
        if (!ok) { cd.Cleanup(false); CondemnedRig.Kill(go); return null; }
        return cd;
    }

    bool Build(CondemnedConfig c, GameObject prefab)
    {
        cfg = c;
        B = new Vector3(c.dropPoint.x, 0f, c.dropPoint.z);
        gy = TerrainY(B.x - 0.8f, B.z + 0.1f, 17.29f);
        B.y = gy;
        body = CondemnedRig.SpawnPuppet(prefab, B, Quaternion.Euler(0f, 318f, 0f), "CutDown body");
        an = body.GetComponentInChildren<Animator>();
        if (an == null || an.avatar == null || !an.avatar.isHuman) return false;
        foreach (HumanBodyBones hb in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (hb == HumanBodyBones.LastBone) continue;
            var t = an.GetBoneTransform(hb); if (t != null) bones[hb] = t;
        }
        foreach (var need in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
                                     HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                                     HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
            if (!bones.ContainsKey(need)) return false;
        Calibrate();

        // the hanging shape: the clip, sampled once, then the bones are ours
        var graph = PlayableGraph.Create("CutDown hang");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "anim", an);
        var play = AnimationClipPlayable.Create(graph, c.hangClip);
        play.SetApplyFootIK(false);
        output.SetSourcePlayable(play);
        play.SetTime(c.hangClipTime);
        graph.Evaluate(0f);
        graph.Destroy();
        an.enabled = false;

        Vector3 up = Vector3.up, bf0 = body.transform.forward, br0 = body.transform.right;
        Arm(true, Vector3.down + bf0 * 0.06f - br0 * 0.12f, Vector3.down + bf0 * 0.06f);
        Arm(false, Vector3.down + bf0 * 0.06f + br0 * 0.12f, Vector3.down + bf0 * 0.06f);
        CondemnedRig.Turn(Bn(HumanBodyBones.Neck), bf0, 26f);
        CondemnedRig.Turn(Bn(HumanBodyBones.Head), bf0, 20f);
        CondemnedRig.Turn(Bn(HumanBodyBones.Head), br0, 28f);
        if (bones.ContainsKey(HumanBodyBones.LeftToes)) CondemnedRig.Aim(Bn(HumanBodyBones.LeftFoot), Bn(HumanBodyBones.LeftToes), Vector3.down * 0.9f + bf0 * 0.45f);
        if (bones.ContainsKey(HumanBodyBones.RightToes)) CondemnedRig.Aim(Bn(HumanBodyBones.RightFoot), Bn(HumanBodyBones.RightToes), Vector3.down * 0.9f + bf0 * 0.3f);
        body.transform.position += up * (gy + FEET_UP - CondemnedRig.FeetY(an));
        CondemnedRig.Drain(body, c.bodyDrain, c.bodyTint, owned);

        neckBone = Bn(HumanBodyBones.Neck);
        anchor = neckBone.position + up * ROPE_LEN;
        pivot = new GameObject("CutDown pivot"); pivot.transform.SetParent(transform, false); pivot.transform.position = anchor;
        rope = Cyl("rope"); rope.transform.SetParent(pivot.transform, false);
        body.transform.SetParent(pivot.transform, true);
        snap = CondemnedRig.Snap(body);

        Vector3 trunk0 = new Vector3(TRUNK0.x, gy, TRUNK0.z);
        Dg = new Vector3(trunk0.x - B.x, 0f, trunk0.z - B.z).normalized; Sg = Vector3.Cross(up, Dg);
        Dh = Quaternion.AngleAxis(12f, up) * Dg; Sh = Vector3.Cross(up, Dh);
        B0 = new Vector3(B.x, gy, B.z);
        fwdH = Dg;
        BuildPoses();

        // where its neck lies in the heap (the noose end stays there)
        GroundFrame(B0, Dh); CondemnedRig.Restore(snap); Apply(heap); neck0 = neckBone.position; CondemnedRig.Restore(snap);

        // the rope: a stub left on the branch with its frayed strands, the length that comes down after it, the noose end, a loose loop on the ground
        // (what is left afterwards, the stub on the branch and the coil on the ground, stays for the rest of the night)
        remains = new GameObject("CutDown remains");
        remains.transform.SetParent(transform, false);
        stub = Cyl("rope stub"); fall = Cyl("rope falling"); noose = Cyl("noose");
        stub.transform.SetParent(remains.transform, true);
        strands = new GameObject[4]; for (int i = 0; i < 4; i++) { strands[i] = Cyl("strand"); strands[i].transform.SetParent(remains.transform, true); }
        frays = new GameObject[4]; for (int i = 0; i < 4; i++) { frays[i] = Cyl("fray"); frays[i].transform.SetParent(remains.transform, true); }
        Vector2[] loop = { new Vector2(-0.10f, 0.30f), new Vector2(0.05f, 0.62f), new Vector2(0.32f, 0.86f), new Vector2(0.62f, 0.84f), new Vector2(0.74f, 0.62f),
                           new Vector2(0.55f, 0.46f), new Vector2(0.30f, 0.56f), new Vector2(0.26f, 0.84f), new Vector2(0.44f, 1.10f), new Vector2(0.78f, 1.16f), new Vector2(1.02f, 1.02f) };
        Vector3 nk = neck0 - B0; float nx = Vector3.Dot(nk, Dh), nz = Vector3.Dot(nk, Sh);
        var ctrl = new Vector2[loop.Length + 1];
        ctrl[0] = new Vector2(nx, nz);
        for (int i = 0; i < loop.Length; i++) ctrl[i + 1] = new Vector2(nx + 0.25f + loop[i].x, nz + loop[i].y);
        const int SUB = 4;
        pilePts = new Vector3[(ctrl.Length - 1) * SUB + 1];
        for (int i = 0; i + 1 < ctrl.Length; i++)
        {
            Vector2 c0 = ctrl[Mathf.Max(0, i - 1)], c1 = ctrl[i], c2 = ctrl[i + 1], c3 = ctrl[Mathf.Min(ctrl.Length - 1, i + 2)];
            for (int s = 0; s < SUB; s++)
            {
                float u = s / (float)SUB;
                Vector2 cc = 0.5f * (2f * c1 + (-c0 + c2) * u + (2f * c0 - 5f * c1 + 4f * c2 - c3) * u * u + (-c0 + 3f * c1 - 3f * c2 + c3) * u * u * u);
                Vector3 q = B0 + Dh * cc.x + Sh * cc.y;
                pilePts[i * SUB + s] = new Vector3(q.x, Ground(q.x, q.z) + 0.045f, q.z);
            }
        }
        { Vector2 cc = ctrl[ctrl.Length - 1]; Vector3 q = B0 + Dh * cc.x + Sh * cc.y; pilePts[pilePts.Length - 1] = new Vector3(q.x, Ground(q.x, q.z) + 0.045f, q.z); }
        pilePts[0] = new Vector3(neck0.x, Ground(neck0.x, neck0.z) + 0.05f, neck0.z);
        pile = new GameObject[pilePts.Length - 1];
        for (int i = 0; i < pile.Length; i++) { pile[i] = Cyl("rope coil"); pile[i].transform.SetParent(remains.transform, true); }

        // the neck where the rope parts (for the falling length)
        CondemnedRig.Restore(snap);
        Hang(FallOff(T_SNAP), SwingAtSnap(), TwistAtSnap(), ROPE_LEN, true);
        neckSnap = neckBone.position;
        CondemnedRig.Restore(snap);

        // the whole soundtrack, from the tree
        if (c.cutDownSound != null)
        {
            sound = CondemnedRig.MakeSource(transform, "CutDown sound", 0.8f, 7f, 45f);
            sound.transform.position = B + Vector3.up * 2.6f;
            sound.clip = c.cutDownSound;
        }
        Hide();
        return true;
    }

    void Hide()
    {
        CondemnedRig.SetRenderers(body, false);
        foreach (var g in new[] { rope, stub, fall, noose }) if (g) g.SetActive(false);
        foreach (var arr in new[] { strands, pile, frays }) if (arr != null) foreach (var g in arr) if (g) g.SetActive(false);
    }

    GameObject Cyl(string n)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = n;
        var col = g.GetComponent<Collider>(); if (col != null) CondemnedRig.Kill(col);
        var r = g.GetComponent<Renderer>();
        if (cfg.ropeMaterial != null) r.sharedMaterial = cfg.ropeMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.SetParent(transform, true);
        g.SetActive(false);
        return g;
    }

    // ================================================================== running

    /// <summary>Starts it. `viewer` is where the player stands (the body swings across their view).</summary>
    public void Play(Vector3 viewer)
    {
        if (Running || Finished) return;
        Vector3 d = new Vector3(B.x - viewer.x, 0f, B.z - viewer.z);
        if (d.sqrMagnitude > 0.01f) fwdH = d.normalized;
        clock = 0f;
        if (sound != null) sound.Play();
        Evaluate(T_START);
    }

    void LateUpdate()
    {
        if (!Running || Finished) return;
        CondemnedRig.FollowPause(sound, ref soundPaused);
        clock += Time.deltaTime;
        float t = T_START + clock;
        if (!tookSanity && t >= T_HIT) { tookSanity = true; CondemnedRig.TakeSanity(cfg.cutDownSanityCost); }
        if (t >= T_END) { Finish(); return; }
        Evaluate(t);
    }

    void Finish()
    {
        Finished = true;
        clock = -1f;
        Cleanup(true);
        Destroy(gameObject, 0.1f);
    }

    void Cleanup(bool keepRemains)
    {
        if (body != null) CondemnedRig.Kill(body);
        if (remains != null)
        {
            if (keepRemains) remains.transform.SetParent(null, true);   // the coil on the ground stays for the rest of the night
            else CondemnedRig.Kill(remains);
        }
        foreach (var o in owned) if (o != null) CondemnedRig.Kill(o);
        owned.Clear();
    }

    void OnDestroy()
    {
        if (!Finished) Cleanup(false);
    }

    // ================================================================== the frame

    void Evaluate(float t)
    {
        float tau = t - T_HIT;
        CondemnedRig.Restore(snap);
        bool visible = t >= T_DROP && t < T_PARK;
        CondemnedRig.SetRenderers(body, visible);

        if (t < T_SNAP)
        {
            float offv = FallOff(t);
            float swing = tau > 0f ? 4.5f * Mathf.Exp(-0.75f * tau) * Mathf.Sin(tau * 4.3f) : 0f;
            swing *= 1f - 0.6f * CondemnedRig.Ease(t, 2.7f, 3.4f);          // it hangs still
            float twist = tau > 0f ? 16f * Mathf.Sin(tau * 0.8f) : 0f;
            Hang(offv, swing, twist, ROPE_LEN, t >= T_DROP);
            Kick(tau, 1.15f);
        }
        else if (t < T_FEET)
        {
            float u = (t - T_SNAP) / (T_FEET - T_SNAP);
            Hang(-DROP * u * u, SwingAtSnap(), TwistAtSnap(), ROPE_LEN, true);   // straight down, still in its hanging shape
        }
        else
        {
            Hang(-DROP, SwingAtSnap(), TwistAtSnap(), ROPE_LEN, false);
            Pose pose; float wA = 0f;
            if (t < T_RE0)
            {
                GroundFrame(B0, Dh);
                if (t < T_BUCK) { pose = buckle; wA = 1f - CondemnedRig.Ease(t, T_FEET, T_BUCK); }
                else if (t < T_KNEEL) pose = Mix(buckle, kneel, CondemnedRig.Seg(t, T_BUCK, T_KNEEL));
                else if (t < T_SLAM) pose = Mix(kneel, heap, Mathf.Pow(CondemnedRig.Seg(t, T_KNEEL, T_SLAM), 1.6f));
                else
                {
                    pose = heap.Copy();                                         // a small rebound, then nothing
                    float bo = Mathf.Sin(Mathf.Clamp01((t - T_SLAM) / 0.12f) * Mathf.PI) * (t < T_SLAM + 0.12f ? 1f : 0f);
                    pose.hip.y += 0.035f * bo; pose.crown.y += 0.12f * bo; pose.hR.y += 0.06f * bo;
                }
            }
            else if (t < T_RE1)
            {
                GroundFrame(B0, Dh);
                int n = Mathf.Min(re.Length - 1, Mathf.FloorToInt((t - T_RE0) * 8f + 1e-3f));
                pose = re[n];
            }
            else if (t < T_TRUNK)
            {
                int n = Mathf.FloorToInt((t - T_RE1) * 12f + 1e-3f);
                float ts = T_RE1 + n / 12f;
                GroundFrame(B0 + Dg * (RUN_END * Mathf.Pow(CondemnedRig.Seg(ts, T_RE1, T_TRUNK - 1f / 12f), 0.85f)), Vector3.Slerp(Dh, Dg, Mathf.Min(1f, n / 3f)));
                pose = Gait(n, false);
            }
            else
            {
                int n = Mathf.FloorToInt((t - T_TRUNK) * 12f + 1e-3f);
                if (n < 2)
                {
                    // feet walk up the bark, hands still in the dirt, the pelvis climbing toward the trunk
                    GroundFrame(B0 + Dg * (n == 0 ? 2.55f : 2.68f), Dg);
                    mode[2] = 2; mode[3] = 2;
                    pose = n == 0 ? onBark0 : onBark1;
                }
                else
                {
                    float h = 1.55f + 0.85f * (n - 2);
                    D = Vector3.up; Y = -Dg; S = Sg; O = Axis(h) - Dg * Rad(h); O.y = gy + h;
                    for (int i = 0; i < 4; i++) mode[i] = 1;
                    pose = Gait(n + 1, true);
                }
            }
            if (wA > 0f)
            {
                // the hanging shape where its toes touched, handed over to the fold
                var T = snap.T; int cnt = T.Length;
                var aR = new Quaternion[cnt]; var aP = new Vector3[cnt];
                for (int i = 0; i < cnt; i++) { aR[i] = T[i].localRotation; aP[i] = T[i].localPosition; }
                Apply(pose);
                for (int i = 0; i < cnt; i++) { T[i].localRotation = Quaternion.Slerp(T[i].localRotation, aR[i], wA); T[i].localPosition = Vector3.Lerp(T[i].localPosition, aP[i], wA); }
            }
            else Apply(pose);
        }

        // ---------------- the rope
        bool snapped = t >= T_SNAP;
        float rs = snapped ? 14f * Mathf.Exp(-2.6f * (t - T_SNAP)) * Mathf.Sin((t - T_SNAP) * 11f) : 0f;
        Vector3 stubEnd = anchor + Quaternion.AngleAxis(rs, fwdH) * (Vector3.down * 0.46f);
        RopeTo(stub, anchor, stubEnd, 0.095f, snapped);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f + 30f;
            Vector3 sd = (Vector3.down * 1f + Quaternion.AngleAxis(ang, Vector3.up) * Vector3.forward * (0.35f + 0.15f * (i & 1))).normalized;
            RopeTo(strands[i], stubEnd, stubEnd + sd * (0.11f + 0.03f * i), 0.018f, snapped);
        }
        float topY = gy + 0.05f;
        if (t >= T_FEET && t < T_ROPE) { float u = (t - T_FEET) / (T_ROPE - T_FEET); topY = Mathf.Lerp(anchor.y - DROP, gy + 0.05f, Mathf.Pow(u, 1.5f)); }
        int shown = t < T_FEET ? 0 : Mathf.Clamp(Mathf.CeilToInt(pile.Length * CondemnedRig.Seg(t, T_SLAM - 0.02f, T_ROPE)), 0, pile.Length);
        bool lying = t >= T_FEET;
        for (int i = 0; i < pile.Length; i++) RopeTo(pile[i], pilePts[i], pilePts[i + 1], 0.08f, lying && i < shown);
        Vector3 nk2 = neckBone.position;
        Vector3 ropeFoot = shown > 0 ? pilePts[shown] : pilePts[0];
        if (lying && t < T_ROPE) { Vector3 foot = t < T_SLAM ? nk2 : ropeFoot; RopeTo(fall, foot, new Vector3(B0.x, topY, B0.z), 0.09f, topY > foot.y + 0.05f); }
        else fall.SetActive(false);
        RopeTo(noose, nk2, pilePts[0], 0.085f, visible && t >= T_SLAM && t < T_RE0 + 0.25f && (nk2 - pilePts[0]).sqrMagnitude > 0.0004f);
        Vector3 fe = pilePts[pilePts.Length - 1];
        Vector3 last = (pilePts[pilePts.Length - 1] - pilePts[pilePts.Length - 2]).normalized;
        for (int i = 0; i < 4; i++)
        {
            Vector3 sd = (last + Quaternion.AngleAxis(i * 50f - 75f, Vector3.up) * last * 0.9f).normalized;
            RopeTo(frays[i], fe, fe + sd * (0.09f + 0.025f * i) + Vector3.up * 0.01f, 0.016f, t >= T_ROPE);
        }
    }

    // ================================================================== the rope and the hanging body

    void Hang(float offv, float swing, float twist, float ropeLength, bool ropeOn)
    {
        pivot.transform.position = anchor + Vector3.up * offv;
        pivot.transform.rotation = Quaternion.AngleAxis(swing, fwdH) * Quaternion.AngleAxis(twist, Vector3.up);
        rope.transform.localPosition = Vector3.down * (ropeLength * 0.5f); rope.transform.localRotation = Quaternion.identity;
        rope.transform.localScale = new Vector3(0.095f, ropeLength * 0.5f, 0.095f);
        rope.SetActive(ropeOn);
    }

    static float FallOff(float t)
    {
        if (t < T_HIT) { float u = Mathf.Clamp01((t - T_DROP) / (T_HIT - T_DROP)); return 7.5f * (1f - u * u); }
        float tau = t - T_HIT; return -0.26f * Mathf.Exp(-8f * tau) * Mathf.Cos(21f * tau);
    }

    static float SwingAtSnap() { return 4.5f * Mathf.Exp(-0.75f * (T_SNAP - T_HIT)) * Mathf.Sin((T_SNAP - T_HIT) * 4.3f) * 0.4f; }
    static float TwistAtSnap() { return 16f * Mathf.Sin((T_SNAP - T_HIT) * 0.8f); }

    // 12 fps kicking that dies away
    void Kick(float tau, float len)
    {
        if (tau <= 0.04f || tau >= len) return;
        float amp = 1f - tau / len; float st = Mathf.Floor(tau * 12f);
        Vector3 br = body.transform.right, bf = body.transform.forward;
        CondemnedRig.Turn(Bn(HumanBodyBones.LeftUpperLeg), br, -(10f + 55f * CondemnedRig.Hash(st + 1.3f)) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.RightUpperLeg), br, -(10f + 55f * CondemnedRig.Hash(st + 7.7f)) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.LeftLowerLeg), br, 70f * CondemnedRig.Hash(st + 3.1f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.RightLowerLeg), br, 70f * CondemnedRig.Hash(st + 9.4f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.LeftUpperArm), bf, 95f * CondemnedRig.Hash(st + 4.2f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.RightUpperArm), bf, -95f * CondemnedRig.Hash(st + 5.9f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.LeftLowerArm), br, -80f * CondemnedRig.Hash(st + 6.6f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.RightLowerArm), br, -80f * CondemnedRig.Hash(st + 2.8f) * amp);
        CondemnedRig.Turn(Bn(HumanBodyBones.Spine), bf, (CondemnedRig.Hash(st + 8.1f) - 0.5f) * 22f * amp);
    }

    static void RopeTo(GameObject g, Vector3 a, Vector3 b, float th, bool on)
    {
        if (g == null) return;
        if (g.activeSelf != on) g.SetActive(on);
        if (!on) return;
        Vector3 d = b - a;
        g.transform.position = (a + b) * 0.5f;
        if (d.sqrMagnitude > 1e-8f) g.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        g.transform.localScale = new Vector3(th, d.magnitude * 0.5f, th);
    }

    // ================================================================== posing by hand

    Transform Bn(HumanBodyBones b) { Transform t; return bones.TryGetValue(b, out t) ? t : null; }
    Vector3 Wp(Vector3 l) { return O + (D * l.x + Y * l.y + S * l.z) * k; }
    Vector3 Wd(Vector3 l) { return D * l.x + Y * l.y + S * l.z; }
    static Vector3 N(float x, float y, float z) { return new Vector3(x, y, z); }

    void Arm(bool left, Vector3 upperDir, Vector3 lowerDir)
    {
        CondemnedRig.Aim(Bn(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm), Bn(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), upperDir);
        CondemnedRig.Aim(Bn(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), Bn(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), lowerDir);
    }

    static float Tab(float[] v, float h)
    {
        if (h <= TH[0]) return v[0];
        for (int i = 0; i + 1 < TH.Length; i++) if (h <= TH[i + 1]) return Mathf.Lerp(v[i], v[i + 1], (h - TH[i]) / (TH[i + 1] - TH[i]));
        return v[v.Length - 1];
    }
    Vector3 Axis(float h) { return new Vector3(Tab(TX, h), gy + h, Tab(TZ, h)); }
    static float Rad(float h) { return Tab(TR, h); }

    // the ground under a point: terrain, floor, road and the grave mounds (not stones or trees)
    float Ground(float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN;
        foreach (var h in hits)
        {
            string n = h.collider.name;
            if (n.StartsWith("Terrain") || n.StartsWith("Floor") || n.StartsWith("Road") || n == "HuangGrave" || n.StartsWith("DirtGrave"))
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return float.IsNaN(best) ? gy : best;
    }

    static float TerrainY(float x, float z, float fallback)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN;
        foreach (var h in hits)
        {
            string n = h.collider.name;
            if (n.StartsWith("Terrain") || n.StartsWith("Floor") || n.StartsWith("Road"))
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return float.IsNaN(best) ? fallback : best;
    }

    // a hand or foot target (local) on whatever it is standing on
    Vector3 Contact(Vector3 l, int m)
    {
        Vector3 p = Wp(new Vector3(l.x, 0f, l.z));
        if (m == 0) { p.y = Ground(p.x, p.z) + l.y * k; return p; }
        if (m == 2) p = Wp(l);
        float h = Mathf.Max(0.05f, p.y - gy); Vector3 c = Axis(h);
        Vector3 v = new Vector3(p.x - c.x, 0f, p.z - c.z); if (v.sqrMagnitude < 1e-6f) v = -Dg;
        v = v.normalized * (Rad(h) + (m == 2 ? 0.07f : l.y));
        return new Vector3(c.x + v.x, p.y, c.z + v.z);
    }

    static Vector3 Orth(Vector3 a, Vector3 d)
    {
        Vector3 o = a - d * Vector3.Dot(a, d);
        return o.sqrMagnitude > 1e-6f ? o.normalized : Vector3.Cross(d, Vector3.right).normalized;
    }

    // rest-pose offsets, measured from the bones' real directions (works for T- and A-poses alike)
    void Calibrate()
    {
        Vector3 f = body.transform.forward, u = body.transform.up;
        Quaternion tr = Quaternion.Inverse(Quaternion.LookRotation(f, u));
        foreach (var hb in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder })
            if (Bn(hb) != null) off[hb] = tr * Bn(hb).rotation;
        foreach (bool left in new[] { true, false })
        {
            var U = Bn(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var L = Bn(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var H = Bn(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Vector3 d1 = (L.position - U.position).normalized, d2 = (H.position - L.position).normalized;
            off[left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm] = Quaternion.Inverse(Quaternion.LookRotation(d1, Orth(-f, d1))) * U.rotation;
            off[left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm] = Quaternion.Inverse(Quaternion.LookRotation(d2, Orth(-f, d2))) * L.rotation;
            off[left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand] = Quaternion.Inverse(Quaternion.LookRotation(d2, Orth(u, d2))) * H.rotation;
            var UL = Bn(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var LL = Bn(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var FT = Bn(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            Vector3 e1 = (LL.position - UL.position).normalized, e2 = (FT.position - LL.position).normalized;
            off[left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg] = Quaternion.Inverse(Quaternion.LookRotation(e1, Orth(f, e1))) * UL.rotation;
            off[left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg] = Quaternion.Inverse(Quaternion.LookRotation(e2, Orth(f, e2))) * LL.rotation;
            off[left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot] = Quaternion.Inverse(Quaternion.LookRotation(f, u)) * FT.rotation;
        }
        lUA = (Bn(HumanBodyBones.LeftLowerArm).position - Bn(HumanBodyBones.LeftUpperArm).position).magnitude;
        lLA = (Bn(HumanBodyBones.LeftHand).position - Bn(HumanBodyBones.LeftLowerArm).position).magnitude;
        lUL = (Bn(HumanBodyBones.LeftLowerLeg).position - Bn(HumanBodyBones.LeftUpperLeg).position).magnitude;
        lLL = (Bn(HumanBodyBones.LeftFoot).position - Bn(HumanBodyBones.LeftLowerLeg).position).magnitude;
        k = Mathf.Clamp((lUL + lLL) / REF_LEG, 0.6f, 1.4f);
    }

    public class Pose
    {
        public Vector3 hip;                                   // pelvis (local)
        public float pP, rP, yP, pC, rC, yC;                  // spine pitch below level toward -x, roll (0 belly up, 180 belly down), yaw: pelvis, chest
        public Vector3 crown = N(-1f, 0f, 0f), face = N(0f, -1f, 0f);
        public Vector3 hL, hR, eL, eR, fL, fR, kL, kR;        // hand / foot targets and elbow / knee hints (local)
        public float wL, wR;                                  // elbow bent the wrong way (> 0.5)
        public Vector3 hfL = N(1f, 0f, 0f), hfR = N(1f, 0f, 0f), huL = N(0f, 1f, 0f), huR = N(0f, 1f, 0f);   // fingers, back of hand
        public Vector3 tL = N(1f, 0f, 0f), tR = N(1f, 0f, 0f), dL = N(0f, 1f, 0f), dR = N(0f, 1f, 0f);       // toes, top of foot
        public Pose Copy() { return (Pose)MemberwiseClone(); }
    }

    static Pose Mix(Pose a, Pose b, float u)
    {
        var p = new Pose();
        p.hip = Vector3.Lerp(a.hip, b.hip, u);
        p.pP = Mathf.Lerp(a.pP, b.pP, u); p.rP = Mathf.Lerp(a.rP, b.rP, u); p.yP = Mathf.Lerp(a.yP, b.yP, u);
        p.pC = Mathf.Lerp(a.pC, b.pC, u); p.rC = Mathf.Lerp(a.rC, b.rC, u); p.yC = Mathf.Lerp(a.yC, b.yC, u);
        p.crown = Vector3.Lerp(a.crown, b.crown, u); p.face = Vector3.Lerp(a.face, b.face, u);
        p.hL = Vector3.Lerp(a.hL, b.hL, u); p.hR = Vector3.Lerp(a.hR, b.hR, u); p.eL = Vector3.Lerp(a.eL, b.eL, u); p.eR = Vector3.Lerp(a.eR, b.eR, u);
        p.fL = Vector3.Lerp(a.fL, b.fL, u); p.fR = Vector3.Lerp(a.fR, b.fR, u); p.kL = Vector3.Lerp(a.kL, b.kL, u); p.kR = Vector3.Lerp(a.kR, b.kR, u);
        p.wL = Mathf.Lerp(a.wL, b.wL, u); p.wR = Mathf.Lerp(a.wR, b.wR, u);
        p.hfL = Vector3.Lerp(a.hfL, b.hfL, u); p.hfR = Vector3.Lerp(a.hfR, b.hfR, u); p.huL = Vector3.Lerp(a.huL, b.huL, u); p.huR = Vector3.Lerp(a.huR, b.huR, u);
        p.tL = Vector3.Lerp(a.tL, b.tL, u); p.tR = Vector3.Lerp(a.tR, b.tR, u); p.dL = Vector3.Lerp(a.dL, b.dL, u); p.dR = Vector3.Lerp(a.dR, b.dR, u);
        return p;
    }

    Quaternion Frame(float pitch, float roll, float yaw)
    {
        float p = pitch * Mathf.Deg2Rad, yw = yaw * Mathf.Deg2Rad;
        Vector3 U = Wd(N(-Mathf.Cos(p) * Mathf.Cos(yw), -Mathf.Sin(p), Mathf.Cos(p) * Mathf.Sin(yw)));
        Vector3 F0 = Wd(N(-Mathf.Sin(p) * Mathf.Cos(yw), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Sin(yw)));
        return Quaternion.LookRotation(Quaternion.AngleAxis(roll, U) * F0, U);
    }

    // two bones to a point; the joint goes toward `hint`; `wrong` turns the joint's own front the other way
    void Limb(bool arm, bool left, Vector3 target, Vector3 hint, bool wrong, Vector3 endFwd, Vector3 endUp)
    {
        HumanBodyBones bu, bl, be;
        if (arm) { bu = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm; bl = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm; be = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand; }
        else { bu = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg; bl = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg; be = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot; }
        float l1 = arm ? lUA : lUL, l2 = arm ? lLA : lLL;
        Transform U = Bn(bu), L = Bn(bl), E = Bn(be);
        Vector3 a = U.position, d = target - a; float dist = d.magnitude; Vector3 n = dist > 1e-4f ? d / dist : Vector3.down;
        float dc = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.03f, l1 + l2 - 0.004f);
        float x = (l1 * l1 - l2 * l2 + dc * dc) / (2f * dc), h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 pd = hint - n * Vector3.Dot(hint, n); if (pd.sqrMagnitude < 1e-6f) pd = Vector3.Cross(n, S); pd.Normalize();
        Vector3 j = a + n * x + pd * h, tip = wrong ? -pd : pd;
        U.rotation = Quaternion.LookRotation(j - a, tip) * off[bu];
        L.rotation = Quaternion.LookRotation(a + n * dc - L.position, tip) * off[bl];
        if (endFwd.sqrMagnitude > 1e-6f && endUp.sqrMagnitude > 1e-6f) E.rotation = Quaternion.LookRotation(endFwd, endUp) * off[be];
    }

    void Apply(Pose p)
    {
        Quaternion q0 = Frame(p.pP, p.rP, p.yP), qc = Frame(p.pC, p.rC, p.yC);
        var hips = Bn(HumanBodyBones.Hips);
        hips.rotation = q0 * off[HumanBodyBones.Hips];
        Vector3 hp = Wp(p.hip); if (mode[2] == 0 && mode[3] == 0) hp.y += Ground(hp.x, hp.z) - O.y;
        hips.position = hp;
        Bn(HumanBodyBones.Spine).rotation = Frame(Mathf.Lerp(p.pP, p.pC, 0.34f), Mathf.Lerp(p.rP, p.rC, 0.34f), Mathf.Lerp(p.yP, p.yC, 0.34f)) * off[HumanBodyBones.Spine];
        Bn(HumanBodyBones.Chest).rotation = Frame(Mathf.Lerp(p.pP, p.pC, 0.67f), Mathf.Lerp(p.rP, p.rC, 0.67f), Mathf.Lerp(p.yP, p.yC, 0.67f)) * off[HumanBodyBones.Chest];
        if (Bn(HumanBodyBones.UpperChest) != null) Bn(HumanBodyBones.UpperChest).rotation = qc * off[HumanBodyBones.UpperChest];
        if (Bn(HumanBodyBones.LeftShoulder) != null) Bn(HumanBodyBones.LeftShoulder).rotation = qc * off[HumanBodyBones.LeftShoulder];
        if (Bn(HumanBodyBones.RightShoulder) != null) Bn(HumanBodyBones.RightShoulder).rotation = qc * off[HumanBodyBones.RightShoulder];
        Vector3 cr = Wd(p.crown).normalized, fc = Wd(p.face); fc = (fc - cr * Vector3.Dot(fc, cr)).normalized;
        Quaternion qh = Quaternion.LookRotation(fc, cr);
        Bn(HumanBodyBones.Neck).rotation = Quaternion.Slerp(qc, qh, 0.45f) * off[HumanBodyBones.Neck];
        Bn(HumanBodyBones.Head).rotation = qh * off[HumanBodyBones.Head];
        Limb(true, true, Contact(p.hL, mode[0]), Wd(p.eL), p.wL > 0.5f, Wd(p.hfL), Wd(p.huL));
        Limb(true, false, Contact(p.hR, mode[1]), Wd(p.eR), p.wR > 0.5f, Wd(p.hfR), Wd(p.huR));
        Limb(false, true, Contact(p.fL, mode[2]), Wd(p.kL), false, Wd(p.tL), Wd(p.dL));
        Limb(false, false, Contact(p.fR, mode[3]), Wd(p.kR), false, Wd(p.tR), Wd(p.dR));
    }

    void GroundFrame(Vector3 origin, Vector3 d) { O = origin; D = d.normalized; Y = Vector3.up; S = Vector3.Cross(Y, D); for (int i = 0; i < 4; i++) mode[i] = 0; }

    // ================================================================== the poses (from the clip)

    void BuildPoses()
    {
        // face down, folded over its knees, head toward you; right arm flung out, left arm under it
        heap = new Pose();
        heap.hip = N(0.60f, 0.42f, 0.08f); heap.pP = 26f; heap.rP = 202f; heap.yP = 10f; heap.pC = 9f; heap.rC = 170f; heap.yC = -8f;
        heap.crown = N(-0.96f, -0.16f, 0.2f); heap.face = N(0.18f, -0.98f, 0.04f);
        heap.hR = N(-0.30f, 0.05f, -0.92f); heap.eR = N(0.1f, 1f, -0.3f); heap.hfR = N(-0.3f, 0f, -1f); heap.huR = N(0f, -1f, 0f);
        heap.hL = N(0.38f, 0.05f, 0.34f); heap.eL = N(0.3f, 0.6f, 1f); heap.hfL = N(1f, 0f, 0.2f); heap.huL = N(0f, -1f, 0f);
        heap.fR = N(0.92f, 0.07f, -0.62f); heap.kR = N(-0.35f, -0.1f, -1f); heap.tR = N(0.7f, -0.2f, -0.7f); heap.dR = N(0f, -1f, 0f);
        heap.fL = N(1.16f, 0.07f, 0.30f); heap.kL = N(-1f, -0.12f, 0.25f); heap.tL = N(1f, -0.25f, 0.2f); heap.dL = N(0f, -1f, 0f);

        // knees going as its feet touch
        buckle = new Pose();
        buckle.hip = N(0.08f, 1.02f, 0.02f); buckle.pP = -78f; buckle.rP = 182f; buckle.yP = 4f; buckle.pC = -66f; buckle.rC = 180f; buckle.yC = 0f;
        buckle.crown = N(-0.55f, 0.75f, 0.3f); buckle.face = N(-0.7f, -0.65f, 0f);
        buckle.hL = N(-0.05f, 0.95f, 0.42f); buckle.eL = N(1f, 0f, 0.3f); buckle.hfL = N(0f, -1f, 0f); buckle.huL = N(-1f, 0f, 0f);
        buckle.hR = N(-0.10f, 0.95f, -0.42f); buckle.eR = N(1f, 0f, -0.3f); buckle.hfR = N(0f, -1f, 0f); buckle.huR = N(-1f, 0f, 0f);
        buckle.fL = N(0.12f, 0.10f, 0.20f); buckle.kL = N(-1f, 0.1f, 0.15f); buckle.tL = N(-0.5f, -0.8f, 0f); buckle.dL = N(-0.8f, 0.5f, 0f);
        buckle.fR = N(0.16f, 0.10f, -0.18f); buckle.kR = N(-1f, 0.1f, -0.15f); buckle.tR = N(-0.5f, -0.8f, 0f); buckle.dR = N(-0.8f, 0.5f, 0f);

        // on its knees, going over
        kneel = new Pose();
        kneel.hip = N(0.42f, 0.62f, 0.04f); kneel.pP = -18f; kneel.rP = 188f; kneel.yP = 5f; kneel.pC = 4f; kneel.rC = 178f; kneel.yC = -3f;
        kneel.crown = N(-0.9f, 0.1f, 0.2f); kneel.face = N(-0.1f, -1f, 0f);
        kneel.hL = N(-0.12f, 0.22f, 0.42f); kneel.eL = N(1f, 0.2f, 0.3f); kneel.hfL = N(0f, -1f, 0f); kneel.huL = N(1f, 0f, 0f);
        kneel.hR = N(-0.18f, 0.20f, -0.46f); kneel.eR = N(1f, 0.2f, -0.3f); kneel.hfR = N(0f, -1f, 0f); kneel.huR = N(1f, 0f, 0f);
        kneel.fL = N(1.0f, 0.08f, 0.36f); kneel.kL = N(-1f, -0.1f, 0.2f); kneel.tL = N(1f, -0.3f, 0f); kneel.dL = N(0f, -1f, 0f);
        kneel.fR = N(1.05f, 0.08f, -0.28f); kneel.kR = N(-1f, -0.1f, -0.15f); kneel.tR = N(1f, -0.3f, 0f); kneel.dR = N(0f, -1f, 0f);

        // the bridge: belly to the sky, head hanging face-down, hands by the head, feet toward the trunk
        bridge = new Pose();
        bridge.hip = N(0.0f, 1.04f, 0.0f); bridge.pP = 12f; bridge.rP = 0f; bridge.yP = 0f; bridge.pC = 58f; bridge.rC = 0f; bridge.yC = 0f;
        bridge.crown = N(-0.88f, -0.42f, 0.24f); bridge.face = N(0.42f, -0.9f, 0f);
        bridge.hL = N(-0.66f, 0.05f, -0.46f); bridge.eL = N(-0.35f, 0.45f, -1f); bridge.wL = 1f; bridge.hfL = N(1f, 0f, -0.2f); bridge.huL = N(0f, 1f, 0f);
        bridge.hR = N(-0.66f, 0.05f, 0.46f); bridge.eR = N(-0.35f, 0.45f, 1f); bridge.wR = 1f; bridge.hfR = N(1f, 0f, 0.2f); bridge.huR = N(0f, 1f, 0f);
        bridge.fL = N(0.80f, 0.10f, -0.54f); bridge.kL = N(0.15f, 0.75f, -1f); bridge.tL = N(1f, 0f, -0.3f); bridge.dL = N(0f, 1f, 0f);
        bridge.fR = N(0.80f, 0.10f, 0.54f); bridge.kR = N(0.15f, 0.75f, 1f); bridge.tR = N(1f, 0f, 0.3f); bridge.dR = N(0f, 1f, 0f);

        // the reassembly, hold by hold (8 fps)
        var r = new List<Pose>();
        r.Add(heap.Copy());
        var p = heap.Copy(); p.hR = N(-0.42f, 0.05f, -0.66f); p.eR = N(0.3f, 1f, -0.2f); p.wR = 1f; r.Add(p);
        p = p.Copy(); p.hR = N(-0.50f, 0.05f, -0.46f); p.eR = N(0.4f, 1f, -0.5f); p.crown = N(-0.95f, -0.12f, 0.32f); r.Add(p);
        p = p.Copy(); p.hip = N(0.60f, 0.80f, 0.05f); p.pP = 48f;
        p.fL = N(1.02f, 0.05f, 0.34f); p.fR = N(1.06f, 0.05f, -0.26f); p.tL = N(0.2f, -1f, 0f); p.tR = N(0.2f, -1f, 0f); p.dL = N(1f, 0f, 0f); p.dR = N(1f, 0f, 0f); r.Add(p);
        p = p.Copy(); p.hip = N(0.52f, 1.18f, 0.02f); p.pP = 66f; p.kL = N(-1f, -0.6f, 0.1f); p.kR = N(-1f, -0.6f, -0.1f); r.Add(p);
        p = p.Copy(); p.hip = N(0.46f, 1.24f, 0.10f); p.rP = 104f; p.yP = -12f; p.pP = 50f;
        p.fR = N(0.85f, 0.42f, 0.02f); p.kR = N(0f, 0.4f, -1f); p.fL = N(1.0f, 0.06f, 0.30f); p.kL = N(0f, -0.3f, 1f); r.Add(p);
        p = p.Copy(); p.hip = N(0.40f, 1.02f, 0.02f); p.rP = 4f; p.yP = 0f; p.pP = 28f;
        p.fR = bridge.fR + N(0.06f, 0f, 0f); p.kR = bridge.kR; p.tR = bridge.tR; p.dR = bridge.dR;
        p.fL = bridge.fL + N(0.04f, 0f, 0f); p.kL = bridge.kL; p.tL = bridge.tL; p.dL = bridge.dL; r.Add(p);
        p = p.Copy(); p.rC = 92f; p.pC = 26f; p.yC = 10f;
        p.hR = N(-0.74f, 0.05f, 0.05f); p.eR = N(0f, 1f, 0.2f); p.hL = N(-0.58f, 0.05f, -0.30f); p.eL = N(0f, 0.6f, -1f); p.wL = 1f; p.hfL = N(1f, 0f, 0f); p.huL = N(0f, 1f, 0f); r.Add(p);
        p = p.Copy(); p.rC = 2f; p.pC = 40f; p.yC = 0f; p.hip = N(0.24f, 0.86f, 0f); p.pP = 22f;
        p.hR = bridge.hR + N(-0.05f, 0f, 0f); p.eR = bridge.eR; p.hfR = bridge.hfR; p.huR = bridge.huR;
        p.hL = bridge.hL + N(-0.05f, 0f, 0f); p.eL = bridge.eL; p.hfL = bridge.hfL; p.huL = bridge.huL;
        p.crown = N(-0.97f, -0.2f, 0.12f); p.face = N(0.2f, -0.97f, 0f); r.Add(p);
        p = Mix(p, bridge, 0.55f); r.Add(p);
        r.Add(bridge.Copy());
        p = bridge.Copy(); p.crown = N(-0.84f, -0.55f, 0.3f); p.face = N(0.55f, -0.84f, 0f); p.hip = N(0f, 1.08f, 0f); r.Add(p);
        r.Add(p.Copy());
        p = p.Copy(); p.hL = p.hL + N(0.05f, 0f, -0.04f); p.crown = N(-0.86f, -0.5f, 0.1f); r.Add(p);
        r.Add(p.Copy());
        re = r.ToArray();

        // at the trunk: feet up the bark, hands still in the dirt, pelvis climbing toward the trunk
        onBark0 = bridge.Copy();
        onBark0.hip = N(0.20f, 1.24f, 0f); onBark0.pP = 34f; onBark0.pC = 62f; onBark0.rP = 5f; onBark0.rC = -4f;
        onBark0.fL = N(0.86f, 0.58f, -0.42f); onBark0.fR = N(0.86f, 0.50f, 0.42f); onBark0.kL = N(0.5f, 1f, -0.7f); onBark0.kR = N(0.5f, 1f, 0.7f);
        onBark0.tL = N(0.1f, 1f, -0.2f); onBark0.tR = N(0.1f, 1f, 0.2f); onBark0.dL = N(-1f, 0.1f, 0f); onBark0.dR = N(-1f, 0.1f, 0f);
        onBark0.hL = N(-0.62f, 0.05f, -0.46f); onBark0.hR = N(-0.74f, 0.17f, 0.46f);
        onBark0.crown = N(-0.8f, -0.6f, 0.3f); onBark0.face = N(0.6f, -0.8f, 0f);
        onBark1 = onBark0.Copy();
        onBark1.hip = N(0.22f, 1.52f, 0f); onBark1.pP = 56f; onBark1.pC = 74f; onBark1.rP = -5f; onBark1.rC = 4f;
        onBark1.fL = N(0.72f, 1.30f, -0.38f); onBark1.fR = N(0.72f, 1.16f, 0.38f); onBark1.kL = N(0.6f, 0.8f, -0.8f); onBark1.kR = N(0.6f, 0.8f, 0.8f);
        onBark1.hL = N(-0.70f, 0.17f, -0.46f); onBark1.hR = N(-0.62f, 0.05f, 0.46f);
        onBark1.crown = N(-0.55f, -0.8f, 0.3f); onBark1.face = N(0.8f, -0.55f, 0f);
    }

    // the bridge on the move: limbs in diagonal pairs at 12 fps (n = hold number)
    Pose Gait(int n, bool climbing)
    {
        var p = bridge.Copy();
        bool a = (n & 1) == 0;            // pair A = left hand + right foot
        float back = -0.16f, fwd = 0.18f, lift = climbing ? 0.10f : 0.14f;
        p.hL = bridge.hL + N(a ? back : fwd, a ? 0f : lift, 0f); p.fR = bridge.fR + N(a ? back : fwd, a ? 0f : lift, 0f);
        p.hR = bridge.hR + N(a ? fwd : back, a ? lift : 0f, 0f); p.fL = bridge.fL + N(a ? fwd : back, a ? lift : 0f, 0f);
        p.hip = bridge.hip + N(0f, a ? 0.05f : -0.02f, 0f);
        if (climbing) p.hip.y -= 0.26f;
        p.rP = a ? 7f : -7f; p.rC = a ? -6f : 6f;
        p.crown = N(-0.84f, -0.55f, a ? 0.3f : 0.02f); p.face = N(0.55f, -0.84f, 0f);
        return p;
    }

    /// <summary>For testing: renders the sequence at a given clip time (1.0 to 9.6) without running the clock.</summary>
    public void PreviewAt(float clipTime, Vector3 viewer)
    {
        Vector3 d = new Vector3(B.x - viewer.x, 0f, B.z - viewer.z);
        if (d.sqrMagnitude > 0.01f) fwdH = d.normalized;
        Evaluate(Mathf.Clamp(clipTime, T_START, T_END));
    }

    public Vector3 DropPoint { get { return B; } }
}
