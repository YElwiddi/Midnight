using UnityEngine;

/// <summary>
/// The Beast's kill: "Lights On". It has kept your torch dead all the while it hunted. On the frame it catches you the
/// torch lights by itself, far too bright and stuttering, and its skull is already at the lens, lifting off its jaw,
/// both hands spread at the edges of the picture. The bulb goes. Then you are looking out through the sockets of its
/// face at what is left of it: the long neck weaving with nothing on the end. It knocks on the bone, twice.
///
/// The skull is the Beast's own, baked out of its mesh (Tools > Graveyard > Crypt Kills > Apply): a copy of the body
/// without the skull's triangles, and the skull as a rigid shell that can leave the neck.
/// The view through the sockets is staged at a fixed place in the crypt (socketsEye), where a lit wall stands behind
/// the Beast: it is near-black, and reads only against something.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Beast Lights On", fileName = "BeastLightsOn")]
public class BeastLightsOnSequence : CryptScreamSequence
{
    [Header("Clips")]
    public AnimationClip idle;

    [Header("Skull (baked from the Beast's mesh)")]
    [Tooltip("The Beast's body without its skull: the same skin, fewer triangles.")]
    public Mesh bodyWithoutSkull;
    [Tooltip("The skull alone, in the head bone's space. Part 0 is the face as painted, part 1 the hollow behind it.")]
    public Mesh skull;
    [Tooltip("The inside of the skull (so it does not read as a second face).")]
    public Color hollowColor = new Color(0.085f, 0.07f, 0.058f);
    [Tooltip("Where the skull hangs from the neck, in the head bone's space: it lifts off the jaw about this point.")]
    public Vector3 jawHinge = new Vector3(0f, 0.42f, 0.90f);

    [Header("Scream")]
    [Tooltip("Seconds it is on you before the bulb goes, and the black after (s).")]
    public float cut = 1.1f;
    public float blackGap = 0.2f;
    [Tooltip("How far the skull is from your eyes (m), and how far above the middle of the picture it sits.")]
    public float skullDistance = 1f;
    public float skullUp = 0.20f;
    [Tooltip("Its hands: metres in front of your eyes, out to each side, and above the middle of the picture.")]
    public Vector3 hands = new Vector3(0.62f, 0.42f, 0f);
    [Tooltip("How bright the torch burns when it lights itself.")]
    public float overload = 1.35f;
    [Tooltip("Tape noise for the whole kill.")]
    public float tapeWear = 0.142f;

    [Header("After: through its sockets")]
    [Tooltip("Its face from the inside: two sockets to see through (SkullSockets.png), and the same cracked.")]
    public Texture sockets;
    public Texture socketsCracked;
    [Tooltip("Where your eyes are for this (world), and the way the Beast stands from there.")]
    public Vector3 socketsEye = new Vector3(-84f, 2.6f, 61.5f);
    public Vector3 socketsAhead = new Vector3(-0.9f, 0f, 2.6f);
    [Tooltip("Height of the floor there (world y).")]
    public float socketsFloor = 0f;
    [Tooltip("Seconds into the after-effect of each knock on the bone. The second cracks it, and the picture goes.")]
    public float firstKnock = 0.78f;
    public float secondKnock = 1.08f;
    [Tooltip("Black after it (s).")]
    public float blackHold = 0.2f;

    private class State : ScreamState
    {
        public SkinnedMeshRenderer body;
        public Mesh bodyFull, bodyNoSkull;
        public Transform skull;
        public Vector3 skullCenter;      // the skull's middle in the head bone's space
        public Vector3 faceLocal;        // the way the skull faces, in the head bone's space
        public Renderer mask;
        public float noise;
    }

    private static readonly Shake Hit = new Shake { turn = 2f, roll = 3.5f, rollHit = 7f, sway = 0.014f, kick = 0.06f, zoom = 6f };
    private const float MaskDistance = 0.3f;

    public BeastLightsOnSequence()
    {
        clearAhead = 2.6f;      // it stands off behind its own long neck
    }

    public override float Duration { get { return cut + blackGap + secondKnock + 0.06f + blackHold; } }

    // ------------------------------------------------------------------ setup

    public override void Begin(KillSequenceRig rig)
    {
        var st = new State();
        Frame(rig, st);
        rig.UseClips(0.42f, 0.05f, ("idle", idle));
        rig.Pose(("idle", 0.5f, 1f));
        st.faceLocal = Quaternion.Inverse(rig.head.rotation) * rig.killer.forward;
        if (rig.vhs != null) st.noise = rig.vhs.noiseIntensity;

        st.body = rig.killer.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (st.body != null && bodyWithoutSkull != null && skull != null)
        {
            st.bodyFull = st.body.sharedMesh;
            st.bodyNoSkull = bodyWithoutSkull;
            st.skullCenter = skull.bounds.center;
            Shader lit = litShader != null ? litShader : Shader.Find("Standard");
            Material hollow = rig.Keep(new Material(lit) { color = hollowColor });
            hollow.SetFloat("_Glossiness", 0.02f);
            GameObject g = rig.Keep(new GameObject("BeastSkull", typeof(MeshFilter), typeof(MeshRenderer)));
            g.GetComponent<MeshFilter>().sharedMesh = skull;
            g.GetComponent<MeshRenderer>().sharedMaterials = new[] { st.body.sharedMaterial, hollow };
            g.SetActive(false);
            st.skull = g.transform;
        }
        if (sockets != null) st.mask = LensQuad(rig, "BeastSockets", sockets, MaskDistance, 1.2f);
    }

    public override void End(KillSequenceRig rig)
    {
        var st = rig.state as State;
        if (st != null)
        {
            SkullOff(st, false);
            if (rig.vhs != null) rig.vhs.noiseIntensity = st.noise;
        }
        base.End(rig);
    }

    // The skull on the neck as always (false), or off it (true): the neck ends in its jaw and nothing else.
    private static void SkullOff(State st, bool off)
    {
        if (st.skull == null || st.body == null) return;
        st.body.sharedMesh = off ? st.bodyNoSkull : st.bodyFull;
    }

    // ------------------------------------------------------------------ the kill

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        var st = (State)rig.state;
        rig.BeginFrame();
        if (rig.vhs != null) rig.vhs.noiseIntensity = tapeWear;
        if (st.mask != null) st.mask.gameObject.SetActive(false);
        if (st.skull != null) st.skull.gameObject.SetActive(false);
        float afterAt = cut + blackGap;

        if (t < cut) Scream(rig, st, t);
        else if (t < afterAt)
        {
            SkullOff(st, false);
            rig.Visible(false);
            TorchOff(rig);
            Black(rig);
        }
        else Sockets(rig, st, t - afterAt);
    }

    // The skull, the long neck behind it, and both arms spread to the edges of the picture.
    private void Scream(KillSequenceRig rig, State st, float s)
    {
        Vector3 C = rig.Eye(), fw = rig.forward, rt = rig.right, U = Vector3.up;
        float q = Steps(s, 30f);
        float thrust = Mathf.Abs(Mathf.Sin(q * 2f * Mathf.PI * 6f));
        float hit = Mathf.Exp(-s * 14f);

        rig.Visible(true);
        rig.killer.SetPositionAndRotation(rig.ground + fw * (skullDistance + 1.25f), Quaternion.LookRotation(-fw));
        rig.Pose(("idle", 0.5f, 1f));
        Spine(rig, rig.killer.right, 5f);
        Spasm(rig, q, 0.35f, 28f);

        // the skull is the picture: turned on you, its middle a little above the middle of the frame
        Transform head = rig.head;
        Vector3 faceAt = C + fw * (skullDistance - 0.16f * thrust - 0.10f * hit) + U * skullUp;
        for (int i = 0; i < 4; i++)
        {
            Vector3 middle = head.TransformPoint(st.skullCenter);
            head.rotation = Quaternion.FromToRotation(head.rotation * st.faceLocal, (C - middle).normalized) * head.rotation;
            rig.killer.position += faceAt - head.TransformPoint(st.skullCenter);
        }
        head.rotation = Quaternion.AngleAxis(16f * Jitter(q, 30f, 1f), (C - head.position).normalized) * Quaternion.AngleAxis(9f * Jitter(q, 30f, 2f), U) * head.rotation;
        rig.killer.position += faceAt - head.TransformPoint(st.skullCenter);

        // the skull lifts off its jaw: the mouth is a pit
        if (st.skull != null)
        {
            SkullOff(st, true);
            float gape = 16f + 26f * thrust + 10f * hit;
            float k = head.lossyScale.x;
            Quaternion rot = head.rotation * Quaternion.AngleAxis(-gape, Vector3.right);
            st.skull.gameObject.SetActive(true);
            st.skull.localScale = Vector3.one * k;
            st.skull.SetPositionAndRotation(head.TransformPoint(jawHinge) - rot * (jawHinge * k), rot);
        }

        // its hands: fingers spread, coming in from both edges (facing you, its left hand is on your right)
        Vector3 hl = C + fw * (hands.x - 0.06f * thrust) + rt * hands.y + U * (hands.z + 0.05f * Jitter(q, 15f, 21f));
        Vector3 hr = C + fw * (hands.x - 0.06f * thrust) - rt * hands.y + U * (hands.z + 0.05f * Jitter(q, 15f, 22f));
        Arm(rig, true, hl, C + rt * 2.2f + Vector3.down);
        Arm(rig, false, hr, C - rt * 2.2f + Vector3.down);
        Hand(rig, true, (-fw * 0.6f + U * 0.7f - rt * 0.35f).normalized);
        Hand(rig, false, (-fw * 0.6f + U * 0.7f + rt * 0.35f).normalized);

        ShakenCamera(rig, C, fw, U, s, hit, Hit);

        // the torch lights by itself, far too bright, and stutters
        float strobe = s < 0.1f ? 3.2f : (Jitter(s, 15f, 3f) > -0.35f ? 2.6f + 0.5f * Jitter(s, 30f, 4f) : 0.3f);
        KillLight(rig, strobe * overload, C - fw * 1.1f + U * 0.1f, C + fw * skullDistance + U * (skullUp - 0.25f), 72f);
        TapeHit(rig, s, 0.4f, 0.5f, -1f, 0.6f);
        if (s > cut - Snap) { rig.fx.flashColor = Color.white; rig.fx.flash = 0.8f; }       // the bulb goes
    }

    // Its face is on you. You look out through the sockets at what is left of it.
    private void Sockets(KillSequenceRig rig, State st, float a)
    {
        Vector3 U = Vector3.up, C = socketsEye;
        Vector3 floor = new Vector3(C.x, socketsFloor, C.z);
        Vector3 fw = new Vector3(socketsAhead.x, 0f, socketsAhead.z).normalized, rt = Vector3.Cross(U, fw);

        // the Beast without its skull, its neck weaving, coming a step nearer
        rig.Visible(true);
        rig.killer.SetPositionAndRotation(floor + fw * (2.25f - 0.25f * S(0.3f, 0.75f, a)), Quaternion.LookRotation(-fw));
        rig.Pose(("idle", 0.5f + a * 0.5f, 1f));
        SkullOff(st, true);
        Transform head = rig.head;
        head.rotation = Quaternion.AngleAxis(18f * Mathf.Sin(a * 5.1f) + 7f * Mathf.Sin(a * 11.3f), U) * head.rotation;
        Spine(rig, rig.killer.right, 6f * S(0.3f, 0.75f, a));

        // it knocks on the bone in front of your eyes
        Vector3 gaze = Quaternion.AngleAxis(-22f, U) * fw, gazeRt = Vector3.Cross(U, gaze);
        float raps = Rap(a, firstKnock) + Rap(a, secondKnock);
        float knock = S(firstKnock - 0.4f, firstKnock - 0.1f, a);
        Vector3 knockAt = C + gaze * (0.80f - 0.30f * raps) + gazeRt * 0.30f + U * (0.02f + 0.17f * raps);
        Arm(rig, true, knockAt, knockAt + rt * 1.5f + U * 0.2f, knock);
        if (knock > 0f) Hand(rig, true, (-fw + U * 0.25f).normalized, knock);

        float nod = Kick(a, firstKnock, 1.2f, 9f, 16f) + Kick(a, secondKnock, 2.2f, 9f, 14f);
        rig.SetCamera(C + U * (0.012f * Mathf.Sin(a * 2.4f)), C + gaze * 2f + U * (0.30f - 0.05f * nod), rig.baseFov, 0f, 0f, 1.2f * nod);

        // your torch, back in your hand and failing
        float buzz = 0.86f + 0.14f * Jitter(a, 18f, 4f) + (Jitter(a, 9f, 9f) > 0.76f ? -0.45f : 0f);
        float torch = a >= secondKnock ? (a < secondKnock + Snap ? 6f : 0f) : 2.2f * buzz;
        Vector3 hand = rig.cam.position + rig.cam.rotation * new Vector3(0.30f, -0.55f, 0.22f);
        TorchBeam(rig, torch, hand, Vector3.Lerp(rig.Bone(HumanBodyBones.UpperChest).position, head.position, 0.6f), 64f);

        // the inside of its face, lit by what comes back off the wall
        bool gone = a >= secondKnock + 0.06f;
        if (st.mask != null && !gone)
        {
            st.mask.gameObject.SetActive(true);
            st.mask.sharedMaterial.mainTexture = a >= secondKnock && socketsCracked != null ? socketsCracked : sockets;
            float lit = Mathf.Clamp01(0.16f + 0.17f * torch);
            st.mask.sharedMaterial.color = new Color(lit, lit, lit, 1f);
            PlaceLens(rig, st.mask, MaskDistance, 0.004f * Mathf.Sin(a * 3.1f) + 0.006f * nod, 0.004f * Mathf.Sin(a * 2.3f + 1f) - 0.012f * nod, 0.8f * Mathf.Sin(a * 1.7f) + 1.5f * nod);
        }
        rig.fx.flashColor = Color.black;
        rig.fx.flash = 1f - S(0f, 0.12f, a);
        if (a >= secondKnock && !gone) rig.SetVhs(1f, -1f);
        if (gone)
        {
            TorchOff(rig);
            Black(rig);
        }
    }

    // A knuckle on the bone: there and back inside two frames.
    private static float Rap(float a, float at) { return Mathf.Exp(-Mathf.Pow((a - at) / 0.035f, 2f)); }
}
