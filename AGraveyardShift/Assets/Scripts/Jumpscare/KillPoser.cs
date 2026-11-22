using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poses a killer limb by limb for a KillSequence, and lights her so the shapes read. Limbs are four straight
/// pieces each (KillBreaks' extra joints), given as directions or solved to a point. She gets a cold light of
/// her own that falls on nothing else (her mesh sits on a layer of its own while the kill runs), and the dropped
/// torch gets a glow in the ground mist, so that far off she is a black cut-out against it.
/// One per kill; nothing here outlives it.
/// </summary>
public class KillPoser
{
    /// <summary>TransparentFX: nothing else in the game draws on it, so her light finds only her.</summary>
    public const int GlowLayer = 1;

    public readonly KillSequenceRig rig;
    /// <summary>Her extra joints. Null when her mesh could not be cut: limbs then bend only where they always did.</summary>
    public readonly KillBreaks breaks;
    /// <summary>The stage: away from you, and to your right.</summary>
    public Vector3 F, R;
    /// <summary>Her own right / up / forward, after <see cref="Frame"/>.</summary>
    public Vector3 r, u, f;
    /// <summary>Pose her mirror image: left and right swap (set before <see cref="Frame"/>).</summary>
    public bool mirror;
    /// <summary>Where the dropped torch lies.</summary>
    public Vector3 torchAt;
    /// <summary>Where the glow in the mist is centred: on the torch, unless the sequence moves it.</summary>
    public Vector3 haloAt;
    /// <summary>The dropped torch's lens glare (the sequence's own prop), switched off with the torch.</summary>
    public GameObject torchGlare;

    private Light glow, pool;
    private Transform halo;
    private Material haloMat;
    private readonly SkinnedMeshRenderer skin;
    private readonly int skinLayer;
    private readonly Vector3 baseScale;
    private Vector3 trueRight;
    private Transform[] scaled;
    private Vector3[] scaledRest;
    private Vector3[] soleLocal, footAxisLocal;
    // ZigTo's working space, so posing every frame makes no garbage
    private readonly float[] zigSign = new float[4], zigLength = new float[4];
    private readonly Vector3[] zigDir = new Vector3[4];
    private static readonly HumanBodyBones[] SpineBones = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };
    private static readonly HumanBodyBones[] FootBones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

    public KillPoser(KillSequenceRig rig, KillBreaks breaks, Shader additive)
    {
        this.rig = rig;
        this.breaks = breaks;
        F = rig.forward;
        R = rig.right;
        baseScale = rig.killer.localScale;
        skin = rig.killer.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin != null)
        {
            skinLayer = skin.gameObject.layer;
            skin.gameObject.layer = GlowLayer;
        }

        var list = new List<Transform>();
        foreach (HumanBodyBones hb in new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Head, HumanBodyBones.Neck })
        {
            Transform t = rig.Bone(hb);
            if (t != null) list.Add(t);
        }
        scaled = list.ToArray();
        scaledRest = new Vector3[scaled.Length];
        for (int i = 0; i < scaled.Length; i++) scaledRest[i] = scaled[i].localScale;

        glow = rig.Keep(new GameObject("SpiritGlow", typeof(Light))).GetComponent<Light>();
        glow.type = LightType.Directional;
        glow.color = new Color(0.72f, 0.8f, 1f);
        glow.shadows = LightShadows.None;
        glow.renderMode = LightRenderMode.ForcePixel;
        glow.cullingMask = 1 << GlowLayer;
        glow.enabled = false;
        pool = rig.Keep(new GameObject("SpiritGlowPool", typeof(Light))).GetComponent<Light>();
        pool.type = LightType.Point;
        pool.color = new Color(0.72f, 0.8f, 1f);
        pool.shadows = LightShadows.None;
        pool.renderMode = LightRenderMode.ForcePixel;
        pool.enabled = false;

        var tex = rig.Keep(new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp });
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float rr = new Vector2(x - 63.5f, y - 63.5f).magnitude / 63.5f;
                float a = Mathf.Clamp01(1f - rr);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 0.95f, 0.85f, a));
            }
        tex.Apply();
        GameObject quad = rig.Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
        quad.name = "TorchHalo";
        Collider col = quad.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);     // now, not at the end of the frame: nothing may hit it
        if (additive != null)
        {
            haloMat = rig.Keep(new Material(additive) { mainTexture = tex });
            quad.GetComponent<Renderer>().sharedMaterial = haloMat;
        }
        quad.SetActive(false);
        halo = quad.transform;
    }

    /// <summary>Her lights and the torch's glow off and her mesh back on its own layer: the kill is over.</summary>
    public void Restore()
    {
        LightsOff();
        if (skin != null) skin.gameObject.layer = skinLayer;
    }

    // ------------------------------------------------------------------ lights

    /// <summary>Everything this lights, off. Called at the top of every frame: whatever the frame wants, it asks for again.</summary>
    public void LightsOff()
    {
        if (glow != null) glow.enabled = false;
        if (pool != null) pool.enabled = false;
        if (halo != null) halo.gameObject.SetActive(false);
    }

    /// <summary>Her own cold light, from your side and a little below: every limb reads, nothing else is lit.</summary>
    public void Glow(float intensity, float fromBelow = 0.35f)
    {
        glow.enabled = intensity > 0.001f;
        if (!glow.enabled) return;
        Vector3 chest = rig.Bone(HumanBodyBones.Chest).position;
        Vector3 dir = (chest - rig.cam.position).normalized + Vector3.up * fromBelow;
        glow.transform.rotation = Quaternion.LookRotation(dir.normalized);
        glow.intensity = intensity;
    }

    /// <summary>The spill of her glow on the ground at her feet.</summary>
    public void Pool(float intensity, float range = 2.4f)
    {
        pool.enabled = intensity > 0.001f;
        if (!pool.enabled) return;
        Vector3 hips = rig.Bone(HumanBodyBones.Hips).position;
        Vector3 toCam = rig.cam.position - hips;
        toCam.y = 0f;
        pool.transform.position = new Vector3(hips.x, rig.Ground(hips).y + 0.5f, hips.z) + toCam.normalized * 0.5f;
        pool.intensity = intensity;
        pool.range = range;
    }

    /// <summary>The torch's beam caught in the ground mist: a glowing haze round where it lies.</summary>
    public void Halo(float k, float size = 8f, float up = 0.9f)
    {
        bool on = k > 0.001f && haloMat != null;
        halo.gameObject.SetActive(on);
        if (!on) return;
        Vector3 at = haloAt + Vector3.up * up + F * 0.3f;
        halo.SetPositionAndRotation(at, Quaternion.LookRotation((at - rig.cam.position).normalized, Vector3.up));
        halo.localScale = Vector3.one * size;
        haloMat.SetColor("_TintColor", new Color(0.5f, 0.47f, 0.42f, 0.5f) * (k * 2f));
    }

    /// <summary>The dropped torch dark for this frame (the sequence lights it again on the next).</summary>
    public void TorchOff()
    {
        if (rig.torch != null) rig.torch.enabled = false;
        if (torchGlare != null) torchGlare.SetActive(false);
        halo.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ your eyes

    /// <summary>Turns your eyes toward a point (w 0..1).</summary>
    public void Look(Vector3 target, float w)
    {
        if (w <= 0f) return;
        Quaternion want = Quaternion.LookRotation((target - rig.cam.position).normalized, Vector3.up);
        rig.cam.rotation = Quaternion.Slerp(rig.cam.rotation, want, w);
    }

    public void Turn(float pitch, float yaw, float roll)
    {
        rig.cam.rotation = rig.cam.rotation * Quaternion.Euler(pitch, yaw, roll);
    }

    // ------------------------------------------------------------------ posing

    /// <summary>Before posing each frame: breaks straight, fingers their own size, her own height.</summary>
    public void BeginPose()
    {
        mirror = false;
        if (breaks != null) breaks.Reset();
        for (int i = 0; i < scaled.Length; i++) scaled[i].localScale = scaledRest[i];
        rig.killer.localScale = baseScale;
    }

    /// <summary>Reads her axes. Call after placing her and before giving directions with <see cref="D"/>.</summary>
    public void Frame()
    {
        trueRight = rig.killer.right;
        u = rig.killer.up;
        f = rig.killer.forward;
        r = mirror ? -trueRight : trueRight;
    }

    /// <summary>A direction in her own frame: x to her right, y up, z the way she faces.</summary>
    public Vector3 D(float x, float y, float z)
    {
        return (r * x + u * y + f * z).normalized;
    }

    public void Pose(string clip, float time)
    {
        rig.Pose(new ValueTuple<string, float, float>(clip, time, 1f));
    }

    private static void Bones(bool arm, bool left, out HumanBodyBones up, out HumanBodyBones lo, out HumanBodyBones end)
    {
        if (arm)
        {
            up = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            lo = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            end = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        }
        else
        {
            up = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            lo = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
            end = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
        }
    }

    private void Limb(bool arm, bool left, Vector3 d1, Vector3 d2, Vector3 d3, Vector3 d4, float l1, float l2, float l3, float l4)
    {
        if (mirror) left = !left;
        HumanBodyBones up, lo, end;
        Bones(arm, left, out up, out lo, out end);
        Transform loBone = rig.Bone(lo), endBone = rig.Bone(end);
        if (loBone == null || endBone == null) return;
        if (breaks != null) { breaks.Lengthen(up, l1, l2); breaks.Lengthen(lo, l3, l4); }
        KillBreaks.Joint ju = breaks != null ? breaks.Get(up) : null, jl = breaks != null ? breaks.Get(lo) : null;
        rig.AimBone(up, ju != null ? ju.brk.position : loBone.position, d1);
        if (ju != null) breaks.BendTo(up, d2);
        rig.AimBone(lo, jl != null ? jl.brk.position : endBone.position, d3);
        if (jl != null) breaks.BendTo(lo, d4);
    }

    /// <summary>An arm as four straight pieces from the shoulder: upper arm (near, far half), forearm (near, far half).
    /// Directions are world space (use <see cref="D"/> for her own frame); l1..l4 lengthen each piece.</summary>
    public void Arm(bool left, Vector3 d1, Vector3 d2, Vector3 d3, Vector3 d4, float l1 = 1f, float l2 = 1f, float l3 = 1f, float l4 = 1f)
    {
        Limb(true, left, d1, d2, d3, d4, l1, l2, l3, l4);
    }

    /// <summary>A leg as four straight pieces from the hip: thigh (near, far half), shin (near, far half).</summary>
    public void Leg(bool left, Vector3 d1, Vector3 d2, Vector3 d3, Vector3 d4, float l1 = 1f, float l2 = 1f, float l3 = 1f, float l4 = 1f)
    {
        Limb(false, left, d1, d2, d3, d4, l1, l2, l3, l4);
    }

    /// <summary>
    /// A limb solved to a point: its four pieces lean to either side of the straight line (pattern, one sign per piece:
    /// "+-+-" a zig-zag, "++--" one elbow) by whatever angle uses up the slack. If the kinks would be shallower than
    /// minAngle, or the limb cannot reach, it grows longer until they are not. Needs the extra joints.
    /// </summary>
    public void ZigTo(bool arm, bool left, Vector3 target, Vector3 side, string pattern = "+-+-", float minAngle = 0f, float len = 1f)
    {
        if (mirror) left = !left;
        if (breaks == null) return;
        HumanBodyBones up, lo, end;
        Bones(arm, left, out up, out lo, out end);
        KillBreaks.Joint ju = breaks.Get(up), jl = breaks.Get(lo);
        Transform U = rig.Bone(up), Lo = rig.Bone(lo), E = rig.Bone(end);
        if (ju == null || jl == null || U == null || Lo == null || E == null) return;
        float[] sign = zigSign, L = zigLength;
        Vector3[] d = zigDir;
        for (int i = 0; i < 4; i++) sign[i] = pattern[i] == '+' ? 1f : (pattern[i] == '-' ? -1f : 0f);
        Vector3 S = U.position, to = target - S;
        float dist = Mathf.Max(to.magnitude, 0.01f);
        Vector3 n = to / dist;
        Vector3 perp = side - n * Vector3.Dot(side, n);
        perp = perp.sqrMagnitude < 1e-6f ? Vector3.Cross(n, Vector3.up).normalized : perp.normalized;
        float k = len, theta = 0f;
        float min = minAngle * Mathf.Deg2Rad;
        for (int pass = 0; pass < 3; pass++)
        {
            breaks.Lengthen(up, k, k);
            breaks.Lengthen(lo, k, k);
            L[0] = (ju.brk.position - U.position).magnitude;
            L[1] = (Lo.position - ju.brk.position).magnitude;
            L[2] = (jl.brk.position - Lo.position).magnitude;
            L[3] = (E.position - jl.brk.position).magnitude;
            float atMin = Reach(L, sign, min);
            if (atMin < dist * 0.999f || (min > 0f && pass == 0))
            {
                k *= dist / atMin * 1.0005f;
                theta = min;
                continue;
            }
            float a = min, b = 1.55f;
            for (int it = 0; it < 24; it++)
            {
                float m = (a + b) * 0.5f;
                if (Reach(L, sign, m) > dist) a = m; else b = m;
            }
            theta = (a + b) * 0.5f;
            break;
        }
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            d[i] = sign[i] == 0f ? n : (n * Mathf.Cos(theta) + perp * (sign[i] * Mathf.Sin(theta))).normalized;
            sum += d[i] * L[i];
        }
        Quaternion fix = Quaternion.FromToRotation(sum, to);
        for (int i = 0; i < 4; i++) d[i] = fix * d[i];
        rig.AimBone(up, ju.brk.position, d[0]);
        breaks.BendTo(up, d[1]);
        rig.AimBone(lo, jl.brk.position, d[2]);
        breaks.BendTo(lo, d[3]);
    }

    // How far four pieces reach when each leans by th to its side of the line.
    private static float Reach(float[] L, float[] sign, float th)
    {
        float ax = 0f, pr = 0f;
        for (int i = 0; i < 4; i++)
        {
            ax += L[i] * (sign[i] == 0f ? 1f : Mathf.Cos(th));
            pr += L[i] * sign[i] * Mathf.Sin(th);
        }
        return Mathf.Sqrt(ax * ax + pr * pr);
    }

    /// <summary>Points the hand (wrist to fingers) along a world direction; fingers longer by <paramref name="claw"/> and curled.</summary>
    public void Hand(bool left, Vector3 dir, float claw = 1f, float curl = 0f)
    {
        if (mirror) left = !left;
        Transform p = rig.Bone(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        if (p == null) return;
        rig.AimBone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, p.position, dir);
        Transform m = rig.Bone(left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
        Transform e = rig.Bone(left ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal);
        if (claw != 1f) p.localScale = new Vector3(1f, claw, 1f);
        if (Mathf.Abs(curl) > 0.01f)
        {
            Vector3 hinge = Vector3.Cross(dir, (rig.cam.position - p.position).normalized).normalized;
            Quaternion q = Quaternion.AngleAxis(curl, hinge);
            p.rotation = q * p.rotation;
            if (m != null) m.rotation = q * m.rotation;
            if (e != null) e.rotation = q * e.rotation;
        }
    }

    public void Foot(bool left, Vector3 dir)
    {
        if (mirror) left = !left;
        Transform toe = rig.Bone(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
        if (toe != null) rig.AimBone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, toe.position, dir);
    }

    /// <summary>Learns which way her soles face. Call while she stands upright in a clip pose with her feet flat (once is enough).</summary>
    public void LearnSoles()
    {
        if (soleLocal != null) return;
        var sole = new Vector3[2];
        var axisLocal = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            Transform foot = rig.Bone(i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot), toe = rig.Bone(i == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
            if (foot == null || toe == null) return;
            Vector3 axis = (toe.position - foot.position).normalized, down = -rig.killer.up;
            Vector3 s = (down - axis * Vector3.Dot(down, axis)).normalized;
            sole[i] = Quaternion.Inverse(foot.rotation) * s;
            axisLocal[i] = Quaternion.Inverse(foot.rotation) * axis;
        }
        soleLocal = sole;
        footAxisLocal = axisLocal;
    }

    /// <summary>Turns a foot so its toes point one way and its sole faces another.</summary>
    public void FootFlat(bool left, Vector3 toeDir, Vector3 soleDir)
    {
        if (mirror) left = !left;
        if (soleLocal == null) return;
        int i = left ? 0 : 1;
        Transform foot = rig.Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Quaternion a = Quaternion.LookRotation(footAxisLocal[i], -soleLocal[i]), b = Quaternion.LookRotation(toeDir, -soleDir);
        foot.rotation = b * Quaternion.Inverse(a);
    }

    /// <summary>The spine bent over three bones: forward (the way she faces), to her left, twisted to her right (degrees in all).</summary>
    public void Spine(float forward, float side = 0f, float twist = 0f)
    {
        float m = mirror ? -1f : 1f;
        foreach (HumanBodyBones hb in SpineBones)
        {
            if (Mathf.Abs(forward) > 0.01f) rig.Bend(hb, trueRight, forward / 3f);
            if (Mathf.Abs(side) > 0.01f) rig.Bend(hb, f, m * side / 3f);
            if (Mathf.Abs(twist) > 0.01f) rig.Bend(hb, u, m * twist / 3f);
        }
    }

    /// <summary>The hips (and so all of her above and below): tipped to her left, turned to her right, pitched forward.</summary>
    public void Hips(float side, float twist = 0f, float forward = 0f)
    {
        float m = mirror ? -1f : 1f;
        if (Mathf.Abs(forward) > 0.01f) rig.Bend(HumanBodyBones.Hips, trueRight, forward);
        if (Mathf.Abs(side) > 0.01f) rig.Bend(HumanBodyBones.Hips, f, m * side);
        if (Mathf.Abs(twist) > 0.01f) rig.Bend(HumanBodyBones.Hips, u, m * twist);
    }

    /// <summary>Shoulders hitched up (degrees; negative drops them).</summary>
    public void Shoulders(float left, float right)
    {
        if (mirror) { float s = left; left = right; right = s; }
        rig.Bend(HumanBodyBones.LeftShoulder, f, -left);
        rig.Bend(HumanBodyBones.RightShoulder, f, right);
    }

    /// <summary>Lifts or drops her so her lowest foot stands on the ground.</summary>
    public void Stand(float sole = 0.07f)
    {
        float low = float.PositiveInfinity;
        foreach (HumanBodyBones hb in FootBones)
        {
            Transform t = rig.Bone(hb);
            if (t != null) low = Mathf.Min(low, t.position.y);
        }
        if (float.IsInfinity(low)) return;
        float gy = rig.Ground(rig.killer.position).y;
        rig.killer.position += Vector3.up * (gy + sole - low);
    }

    /// <summary>The head: rolled onto a shoulder / turned / tipped, relative to looking straight at you (degrees).</summary>
    public void Head(float roll, float yaw = 0f, float pitch = 0f, float stare = 1f, float neckShare = 0.5f)
    {
        float m = mirror ? -1f : 1f;
        rig.Stare(rig.cam.position, stare);
        rig.HeadTurn(rig.cam.position, m * roll, m * yaw, pitch, neckShare);
    }
}
