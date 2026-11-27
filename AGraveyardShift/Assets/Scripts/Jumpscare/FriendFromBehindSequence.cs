using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// The Friend's kill: "From Behind", then his row. Your head is wrenched back: he is bent over you from behind, upside
/// down, both arms coming down past your face. The picture cuts. You are sitting against a wall in the crypt, your
/// torch stood on its end like a candle, and along the wall opposite sit others like him, heads bowed, with him among
/// them. Then every one of them looks up at you at once.
///
/// The row is staged at a fixed place in the crypt (rowSeat), the only scene the Friend lives in.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Friend From Behind", fileName = "FriendFromBehind")]
public class FriendFromBehindSequence : CryptScreamSequence
{
    [Header("Clips")]
    public AnimationClip idle;
    [Tooltip("His arms-up pose (ScaredKill), blended in a little under the scream.")]
    public AnimationClip armsUp;

    [Header("Scream: bent over you from behind")]
    [Tooltip("Seconds he is on you before the picture cuts, and the black after the cut (s).")]
    public float cut = 1f;
    public float blackGap = 0.22f;
    [Tooltip("How far the hollow of his hood is from your eyes (m), and how far above the middle of the picture it sits.")]
    public float faceDistance = 0.96f;
    public float faceUp = 0.13f;
    [Tooltip("How far behind you he stands (m).")]
    public float standsBehind = 0.8f;
    [Tooltip("His hands: metres in front of your eyes, out to each side, and below the middle of the picture.")]
    public Vector3 hands = new Vector3(0.46f, 0.36f, 0.16f);
    [Tooltip("Strength of the kill's own light, and how far behind your eyes it sits (m), so his hands at the lens do not burn out against his face.")]
    public float killLight = 3.4f;
    public float killLightBack = 1.1f;

    [Header("After: the row")]
    [Tooltip("Where your eyes are, sitting against the wall (world).")]
    public Vector3 rowSeat = new Vector3(-99f, 1.32f, 59.52f);
    [Tooltip("The way you face from there: toward the wall the row sits against.")]
    public Vector3 rowAhead = Vector3.forward;
    [Tooltip("Height of the floor there (world y).")]
    public float rowFloor = 0f;
    [Tooltip("How far ahead of you they sit (m).")]
    public float rowDistance = 3.18f;
    [Tooltip("Where each of the others sits along the wall, in metres to your left.")]
    public float[] rowPlaces = { 0.55f, 1.5f, 3.4f, 4.35f, 5.3f };
    [Tooltip("His own place among them (m to your left).")]
    public float ownPlace = 2.45f;
    [Tooltip("The torch stood on its end: metres to your left, and ahead of you.")]
    public Vector2 candle = new Vector2(1.95f, 0.88f);
    [Tooltip("How far your head is turned toward them (degrees; negative = left).")]
    public float gaze = -31f;
    [Tooltip("Seconds into the after-effect that they all look up.")]
    public float lookUpAt = 0.62f;
    [Tooltip("Length of the after-effect (s), and the black after it.")]
    public float afterLength = 1.35f;
    public float blackHold = 0.2f;

    private class Body
    {
        public GameObject go;
        public Animator animator;
        public PlayableGraph graph;
        public AnimationClipPlayable clip;
        public Vector3 at;
        public int seed;
    }

    private class State : ScreamState
    {
        public bool rowBuilt;
        public readonly List<Body> row = new List<Body>();
        public readonly List<GameObject> props = new List<GameObject>();
        public Transform candle, candleLens;
        public Material candleGlare;
        public Light bounce, spill, fill;
        public Vector3 faceLocal;
        public Vector3 ahead, left;
    }

    private static readonly Shake Hit = new Shake { turn = 1.8f, roll = 3f, rollHit = 6f, sway = 0.012f, kick = 0.05f, zoom = 5f };

    public FriendFromBehindSequence()
    {
        clearAhead = 0f;        // you are looking up at him; it is the space behind you he stands in
        clearBehind = 1.5f;
    }

    public override float Duration { get { return cut + blackGap + afterLength + blackHold; } }

    // ------------------------------------------------------------------ setup

    public override void Begin(KillSequenceRig rig)
    {
        var st = new State();
        Frame(rig, st);
        rig.UseClips(0.40f, 0.08f, ("idle", idle), ("armsUp", armsUp));     // "face" = the hollow in his hood
        rig.Pose(("idle", 0.3f, 1f));
        st.faceLocal = Quaternion.Inverse(rig.head.rotation) * rig.FaceDirection;
        st.ahead = new Vector3(rowAhead.x, 0f, rowAhead.z).normalized;
        st.left = Vector3.Cross(st.ahead, Vector3.up);
    }

    public override void End(KillSequenceRig rig)
    {
        var st = rig.state as State;
        if (st != null) foreach (Body b in st.row) if (b.graph.IsValid()) b.graph.Destroy();
        base.End(rig);
    }

    // The row is built while the picture is black, so the frame he catches you on costs nothing.
    private void BuildRow(KillSequenceRig rig, State st)
    {
        st.rowBuilt = true;
        Vector3 seatFloor = new Vector3(rowSeat.x, rowFloor, rowSeat.z);
        Shader lit = litShader != null ? litShader : Shader.Find("Standard");
        Material torchBody = rig.Keep(new Material(lit) { color = new Color(0.05f, 0.05f, 0.055f) });
        torchBody.SetFloat("_Glossiness", 0.2f);

        // the others: copies of his own body, each posed from his idle and sat down
        for (int i = 0; i < rowPlaces.Length; i++)
        {
            var b = new Body { seed = i + 2, at = seatFloor + st.ahead * rowDistance + st.left * rowPlaces[i] };
            b.go = rig.Keep(Instantiate(rig.animator.gameObject));
            b.go.name = "FriendRow" + i;
            b.go.transform.localScale = rig.animator.transform.lossyScale;
            b.animator = b.go.GetComponent<Animator>();
            b.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            b.animator.applyRootMotion = false;
            foreach (Renderer r in b.go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (SkinnedMeshRenderer smr in b.go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 8f);
            }
            b.graph = PlayableGraph.Create("FriendRow" + i);
            b.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(b.graph, "pose", b.animator);
            b.clip = AnimationClipPlayable.Create(b.graph, idle);
            b.clip.SetApplyFootIK(false);
            output.SetSourcePlayable(b.clip);
            b.graph.Play();
            st.row.Add(b);

            // a dead torch lying in front of each of them
            GameObject dead = Cylinder(rig, "FriendRowTorch" + i, torchBody);
            Vector3 lie = (-st.ahead - st.left * (Hash(i) - 0.5f)).normalized;
            Vector3 lensAt = b.at - st.ahead * (1.6f + 0.14f * Hash(i * 2.3f)) - st.left * 0.12f + Vector3.up * 0.07f;
            dead.transform.SetPositionAndRotation(lensAt - lie * 0.13f, Quaternion.LookRotation(lie) * Quaternion.Euler(90f, 0f, 0f));
            st.props.Add(dead);
        }

        // your torch, stood on its end: the barrel, and the glare of its lens
        GameObject barrel = Cylinder(rig, "FriendCandle", torchBody);
        st.candle = barrel.transform;
        st.props.Add(barrel);
        GameObject lens = rig.Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
        lens.name = "FriendCandleGlare";
        DestroyImmediate(lens.GetComponent<Collider>());
        Shader additive = additiveShader != null ? additiveShader : Shader.Find("Legacy Shaders/Particles/Additive");
        st.candleGlare = rig.Keep(new Material(additive) { mainTexture = Glare(rig) });
        var lensRenderer = lens.GetComponent<Renderer>();
        lensRenderer.sharedMaterial = st.candleGlare;
        lensRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lensRenderer.receiveShadows = false;
        st.candleLens = lens.transform;
        st.props.Add(lens);

        st.bounce = PointLight(rig, "FriendRowBounce", new Color(1f, 0.97f, 0.92f));
        st.spill = PointLight(rig, "FriendRowSpill", new Color(1f, 0.95f, 0.85f));
        st.fill = PointLight(rig, "FriendRowFill", new Color(0.9f, 0.92f, 1f));
    }

    // A torch's barrel: 26 cm long, lying along its own up axis.
    private static GameObject Cylinder(KillSequenceRig rig, string name, Material material)
    {
        GameObject g = rig.Keep(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        g.name = name;
        DestroyImmediate(g.GetComponent<Collider>());
        g.transform.localScale = new Vector3(0.07f, 0.13f, 0.07f);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    // A soft round spot: bright in the middle, gone at the edge.
    private static Texture2D Glare(KillSequenceRig rig)
    {
        const int n = 64;
        var tex = rig.Keep(new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp });
        float h = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = Mathf.Clamp01(1f - new Vector2(x - h, y - h).magnitude / h);
                tex.SetPixel(x, y, new Color(1f, 0.96f, 0.86f, a * a * a));
            }
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ the kill

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        var st = (State)rig.state;
        rig.BeginFrame();
        rig.head.localScale = st.headScale;
        float afterAt = cut + blackGap;

        if (t < cut)
        {
            Scream(rig, st, t);
            return;
        }
        if (!st.rowBuilt) BuildRow(rig, st);
        bool after = t >= afterAt;
        foreach (Body b in st.row) if (b.go.activeSelf != after) b.go.SetActive(after);
        foreach (GameObject p in st.props) if (p.activeSelf != after) p.SetActive(after);
        st.bounce.enabled = st.spill.enabled = st.fill.enabled = false;
        if (!after)
        {
            rig.Visible(false);
            TorchOff(rig);
            Black(rig);
            return;
        }
        Row(rig, st, t - afterAt);
    }

    // Your head is wrenched back: you are looking up, and he is over you, upside down.
    private void Scream(KillSequenceRig rig, State st, float s)
    {
        Vector3 C = rig.Eye(), F = rig.forward;
        Vector3 fw = (Vector3.up * 0.97f + F * 0.22f).normalized, up = -F;
        Vector3 rt = Vector3.Cross(up, fw).normalized;
        float q = Steps(s, 24f);
        float thrust = Mathf.Abs(Mathf.Sin(q * 2f * Mathf.PI * 6f));
        float hit = Mathf.Exp(-s * 14f);

        rig.Visible(true);
        rig.killer.SetPositionAndRotation(rig.ground - F * standsBehind, Quaternion.LookRotation(F));
        rig.Pose(("idle", 0.3f, 0.7f), ("armsUp", q * 1.3f, 0.3f));
        Spine(rig, rig.killer.right, 56f / 3f);
        Spasm(rig, q, 0.4f, 30f);
        rig.head.localScale = new Vector3(st.headScale.x, st.headScale.y * (1f + 0.14f * thrust), st.headScale.z);

        Vector3 faceAt = C + fw * (faceDistance - 0.16f * thrust - 0.10f * hit) + up * faceUp + rt * (0.03f * Jitter(q, 24f, 4f));
        for (int i = 0; i < 3; i++) { rig.PutFace(faceAt); Stare(rig, C, 1f, 0.5f); }
        rig.HeadTurn(C, 26f * Jitter(q, 12f, 1f), 9f * Jitter(q, 12f, 2f), 6f * Jitter(q, 12f, 3f), 0.4f);
        rig.PutFace(faceAt);

        // his hands come down either side of your face (from behind he faces the way you do: his left is your left)
        Vector3 hl = C + fw * (hands.x - 0.05f * thrust) - rt * hands.y + up * (-hands.z + 0.05f * Jitter(q, 12f, 21f));
        Vector3 hr = C + fw * (hands.x - 0.05f * thrust) + rt * hands.y + up * (-hands.z - 0.02f + 0.05f * Jitter(q, 12f, 22f));
        Arm(rig, true, hl, C - rt * 1.4f - up * 0.7f);
        Arm(rig, false, hr, C + rt * 1.4f - up * 0.7f);
        Hand(rig, true, (up * 0.8f - fw * 0.6f + rt * 0.25f).normalized);
        Hand(rig, false, (up * 0.8f - fw * 0.6f - rt * 0.25f).normalized);

        ShakenCamera(rig, C, fw, up, s, hit, Hit);
        KillLight(rig, killLight + 0.5f * Jitter(s, 20f, 11f), C - fw * killLightBack + up * 0.1f, faceAt - up * 0.2f, 70f);
        TapeHit(rig, s, 0.42f, 0.5f, 0.012f, 0.55f);
    }

    // They sit with their heads down. Then every one of them looks at you.
    private void Row(KillSequenceRig rig, State st, float a)
    {
        Vector3 U = Vector3.up;
        Vector3 seatFloor = new Vector3(rowSeat.x, rowFloor, rowSeat.z);
        float look = a < lookUpAt ? 0f : (a < lookUpAt + 0.07f ? 0.6f : 1f);

        for (int i = 0; i < st.row.Count; i++)
        {
            Body b = st.row[i];
            float at = b.seed * 1.37f;
            b.clip.SetTime(idle.isLooping ? Mathf.Repeat(at, idle.length) : Mathf.Clamp(at, 0f, idle.length));
            b.graph.Evaluate(0f);
            Sit(b.animator, b.go.transform, st, b.at, 6f + 18f * Hash(i * 5.1f + 1f), (Hash(i * 9.3f) - 0.5f) * 12f, (i % 2 == 0 ? 1f : -1f) * (26f + 22f * Hash(i * 4.4f)), b.seed, look);
        }
        rig.Visible(true);
        rig.Pose(("idle", 9f * 1.37f, 1f));
        Sit(rig.animator, rig.killer, st, seatFloor + st.ahead * rowDistance + st.left * ownPlace, 9f, 0f, -40f, 9, look);

        // your torch stood on its end like a candle: a pool on the ceiling, and what comes back down from it
        float k = (0.94f + 0.06f * Noise(a * 9f, 3f)) * (a > afterLength - 0.12f ? 0f : 1f);
        Vector3 torchAt = seatFloor + st.left * candle.x + st.ahead * candle.y;
        st.candle.SetPositionAndRotation(torchAt + U * 0.13f, Quaternion.identity);
        Vector3 lensAt = torchAt + U * 0.27f;
        float facing = Mathf.Clamp01(Vector3.Dot(U, (rowSeat - lensAt).normalized));
        st.candleLens.gameObject.SetActive(k > 0.001f && facing > 0.02f);
        st.candleLens.SetPositionAndRotation(lensAt, Quaternion.LookRotation(lensAt - rowSeat));
        st.candleLens.localScale = Vector3.one * (0.25f + 1.6f * facing * facing);
        st.candleGlare.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f) * (0.9f * k * (0.25f + 1.75f * facing)));
        TorchBeam(rig, 2.6f * k, torchAt + U * 0.3f, torchAt + U * 5.7f + st.ahead * 0.02f, 64f);
        st.bounce.enabled = st.spill.enabled = k > 0.01f;
        st.bounce.transform.position = torchAt + U * 4.7f + st.ahead * 0.5f; st.bounce.range = 12f; st.bounce.intensity = 1.25f * k;
        st.spill.transform.position = torchAt + U * 0.5f; st.spill.range = 4.2f; st.spill.intensity = 1.1f * k;
        st.fill.enabled = true;
        st.fill.transform.position = rowSeat + U * 0.5f + st.ahead * 0.25f; st.fill.range = 3.4f; st.fill.intensity = 0.5f;

        Vector3 dir = Quaternion.Euler(0f, gaze, 0f) * st.ahead;
        float jolt = Kick(a, lookUpAt, 1f, 8f, 12f);
        rig.SetCamera(rowSeat, rowSeat + dir * 3f + Vector3.down * 0.32f + U * (0.1f * jolt), rig.baseFov - 2f * Mathf.Abs(jolt), 0f, 0f, -5f + 2f * jolt);
        rig.fx.lids = 1f - S(0f, 0.12f, a);
        if (a >= lookUpAt && a < lookUpAt + 0.07f) rig.SetVhs(0.9f, -1f);
        if (a > afterLength - 0.04f) Black(rig);
    }

    // ------------------------------------------------------------------ a body sat against the wall

    private static void Aim(Transform bone, Vector3 childPoint, Vector3 dir)
    {
        if (bone == null) return;
        bone.rotation = Quaternion.FromToRotation(childPoint - bone.position, dir) * bone.rotation;
    }

    private static void Limb(Animator an, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Vector3 d1, Vector3 d2)
    {
        Transform a = an.GetBoneTransform(upper), b = an.GetBoneTransform(lower), c = an.GetBoneTransform(end);
        if (a == null || b == null || c == null) return;
        Aim(a, b.position, d1);
        Aim(b, c.position, d2);
    }

    // Sits a body on the floor at a point, back to the wall, legs out toward you; head bowed (look 0) or raised to you (look 1).
    private void Sit(Animator an, Transform root, State st, Vector3 at, float lean, float side, float headRoll, int seed, float look)
    {
        Vector3 U = Vector3.up, F = -st.ahead, L = Vector3.Cross(F, U);     // L = the body's own left
        root.SetPositionAndRotation(at, Quaternion.LookRotation(F));
        float sp = 0.10f + 0.06f * Hash(seed * 3.1f), bend = Hash(seed * 7.7f) < 0.34f ? 0.5f : 0f;
        Vector3 dl = (F + L * sp + Vector3.down * 0.03f).normalized, dr = (F - L * sp + Vector3.down * 0.03f).normalized;
        Limb(an, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, (dl + U * bend).normalized, (dl - U * bend * 1.2f).normalized);
        Limb(an, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, dr, dr);
        Transform lf = an.GetBoneTransform(HumanBodyBones.LeftFoot), lt = an.GetBoneTransform(HumanBodyBones.LeftToes);
        Transform rf = an.GetBoneTransform(HumanBodyBones.RightFoot), rtoe = an.GetBoneTransform(HumanBodyBones.RightToes);
        if (lf != null && lt != null) Aim(lf, lt.position, (U + F * 0.35f + L * 0.3f).normalized);
        if (rf != null && rtoe != null) Aim(rf, rtoe.position, (U + F * 0.35f - L * 0.4f).normalized);

        float forward = lean + 16f * (1f - look);
        Vector3 across = root.right, along = root.forward;
        foreach (HumanBodyBones hb in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
        {
            Transform sb = an.GetBoneTransform(hb);
            if (sb == null) continue;
            sb.rotation = Quaternion.AngleAxis(forward / 3f, across) * sb.rotation;
            sb.rotation = Quaternion.AngleAxis(side / 3f, along) * sb.rotation;
        }

        // hands in the lap, or fallen at the sides
        int arms = seed % 3;
        Vector3 ul = arms == 1 ? (Vector3.down + L * 0.25f + F * 0.1f).normalized : (Vector3.down + F * 0.35f + L * 0.1f).normalized;
        Vector3 ur = arms == 2 ? (Vector3.down - L * 0.3f + F * 0.05f).normalized : (Vector3.down + F * 0.3f - L * 0.12f).normalized;
        Vector3 ll = arms == 1 ? (Vector3.down * 0.6f + L * 0.5f + F * 0.5f).normalized : (F * 0.8f + Vector3.down * 0.35f - L * 0.25f).normalized;
        Vector3 lr = arms == 2 ? (Vector3.down * 0.7f - L * 0.5f + F * 0.4f).normalized : (F * 0.8f + Vector3.down * 0.35f + L * 0.25f).normalized;
        Limb(an, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, ul, ll);
        Limb(an, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, ur, lr);

        Transform hips = an.GetBoneTransform(HumanBodyBones.Hips);
        root.position += U * (at.y + 0.24f - hips.position.y);

        Transform head = an.GetBoneTransform(HumanBodyBones.Head), neck = an.GetBoneTransform(HumanBodyBones.Neck);
        if (look > 0f)
        {
            Quaternion q = Quaternion.FromToRotation(head.rotation * st.faceLocal, (rowSeat - head.position).normalized);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.45f * look) * neck.rotation;
            q = Quaternion.FromToRotation(head.rotation * st.faceLocal, (rowSeat - head.position).normalized);
            head.rotation = Quaternion.Slerp(Quaternion.identity, q, look) * head.rotation;
            // tipped onto a shoulder, each his own way
            Quaternion roll = Quaternion.AngleAxis(headRoll * look, (rowSeat - head.position).normalized);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, roll, 0.5f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, roll, 0.5f) * head.rotation;
        }
        if (look < 1f) head.rotation = Quaternion.AngleAxis(52f * (1f - look), root.right) * head.rotation;
    }
}
