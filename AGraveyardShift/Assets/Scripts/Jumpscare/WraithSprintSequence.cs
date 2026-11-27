using UnityEngine;

/// <summary>
/// The Wraith's kill: "Sprint". On the frame he reaches you his face is in yours, eyes wide, both arms thrown up either
/// side of his head. His head takes a new angle on every frame and his body will not stop moving. Twice a blade swings
/// down across the lens. The picture cuts; the last frame of him stays on the tape, turned inside out, and fades.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Wraith Sprint", fileName = "WraithSprint")]
public class WraithSprintSequence : CryptScreamSequence
{
    [Header("Clips")]
    [Tooltip("A standing pose to shake from (KillerAttack).")]
    public AnimationClip stand;

    [Header("Scream")]
    [Tooltip("Seconds he is on you before the picture cuts.")]
    public float cut = 1f;
    [Tooltip("How far his face is from your eyes (m), and how far above the middle of the picture it sits.")]
    public float faceDistance = 1f;
    public float faceUp = 0.10f;
    [Tooltip("Strength of the kill's own light, and how far behind your eyes it sits (m), so blades at the lens do not burn out against his face.")]
    public float killLight = 3.2f;
    public float killLightBack = 1.1f;

    [Header("Blades")]
    [Tooltip("A white streak across the picture (BladeSlash.png).")]
    public Texture slashTexture;
    [Tooltip("When each blade swings across the lens (s).")]
    public float firstSlash = 0.36f;
    public float secondSlash = 0.62f;
    [Tooltip("Where his hands hold the blades up: metres in front of your eyes, out to each side, above them.")]
    public Vector3 bladesHeld = new Vector3(0.50f, 0.50f, 0.26f);

    [Header("After")]
    [Tooltip("Seconds his last frame takes to fade off the black.")]
    public float afterimage = 0.75f;
    [Tooltip("Black after it (s).")]
    public float blackHold = 0.2f;

    private class State : ScreamState
    {
        public Renderer slashA, slashB;
        public bool held;
    }

    private static readonly Shake Hit = new Shake { turn = 2f, roll = 3.5f, rollHit = 7f, sway = 0.014f, kick = 0.06f, zoom = 6f };

    public override float Duration { get { return cut + afterimage + blackHold; } }

    public override void Begin(KillSequenceRig rig)
    {
        var st = new State();
        Frame(rig, st);
        rig.UseClips(0.50f, 0.10f, ("stand", stand));      // "face" = between his eyes and his mouth
        if (slashTexture != null)
        {
            st.slashA = LensQuad(rig, "BladeSlashA", slashTexture, 0.28f, 1f);
            st.slashB = LensQuad(rig, "BladeSlashB", slashTexture, 0.275f, 1f);
            Vector3 sc = st.slashB.transform.localScale;
            st.slashB.transform.localScale = new Vector3(-sc.x, sc.y, sc.z);      // the other diagonal
        }
    }

    // A blade's swing: across in three frames, back up in seven.
    private static float Swing(float x) { return x < 0f ? 0f : (x < 0.09f ? x / 0.09f : Mathf.Max(0f, 1f - (x - 0.09f) / 0.22f)); }

    // A blade across the lens: a white streak for a frame, a red one fading after it.
    private static void Streak(KillSequenceRig rig, Renderer q, float distance, float age)
    {
        if (q == null) return;
        bool on = age >= 0f && age < 0.3f;
        q.gameObject.SetActive(on);
        if (!on) return;
        PlaceLens(rig, q, distance);
        float k = age < 0.04f ? 1f : Mathf.Clamp01(1f - (age - 0.04f) / 0.26f);
        q.sharedMaterial.color = age < 0.04f ? Color.white : new Color(0.55f, 0.05f, 0.04f, k * 0.9f);
    }

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        var st = (State)rig.state;
        rig.BeginFrame();
        rig.head.localScale = st.headScale;

        // The frame frozen for the afterimage has to be one he is in: the first frame at or after the cut still draws
        // him, and holds.
        if (t < cut || !st.held)
        {
            Scream(rig, st, Mathf.Min(t, cut - 0.001f));
            st.held = t >= cut - Snap;
            rig.fx.hold = st.held;
            return;
        }

        // ---------------- after: black. His last frame stays on the tape, inside out, and goes
        float a = t - cut;
        Streak(rig, st.slashA, 0.28f, -1f);
        Streak(rig, st.slashB, 0.275f, -1f);
        rig.Visible(false);
        TorchOff(rig);
        rig.fx.hold = true;
        rig.fx.invert = 1f;
        rig.fx.desaturate = 1f;
        rig.fx.darken = Mathf.Clamp01(0.62f + 0.5f * a / 0.7f);
        rig.SetVhs(a < 0.07f ? 1f : 0f, -1f);
        if (a > afterimage) Black(rig);
    }

    private void Scream(KillSequenceRig rig, State st, float s)
    {
        Vector3 C = rig.Eye(), fw = rig.forward, up = Vector3.up, rt = rig.right;
        float q = Steps(s, 30f);
        float thrust = Mathf.Abs(Mathf.Sin(q * 2f * Mathf.PI * 7.5f));
        float hit = Mathf.Exp(-s * 16f);
        float sl = Swing(s - (firstSlash - 0.03f)), sr = Swing(s - (secondSlash - 0.03f));

        rig.Visible(true);
        rig.killer.SetPositionAndRotation(C + fw * (faceDistance + 0.4f), Quaternion.LookRotation(-fw, up));
        rig.Pose(("stand", 0.02f, 1f));
        Spasm(rig, q, 0.7f, 40f);                                       // his body cannot stop
        rig.head.localScale = st.headScale * (1.08f + 0.05f * thrust);

        // his eyes are the picture: his face sits a little above the middle of it, on you
        Vector3 faceAt = C + fw * (faceDistance - 0.14f * thrust - 0.09f * hit) + up * faceUp + rt * (0.02f * Jitter(q, 30f, 4f));
        for (int i = 0; i < 3; i++) { rig.PutFace(faceAt); Stare(rig, C, 1f, 0.5f); }
        rig.HeadTurn(C, 20f * Jitter(q, 30f, 1f), 14f * Jitter(q, 30f, 2f), 6f * Jitter(q, 30f, 3f), 0.3f);   // a new angle every frame
        rig.PutFace(faceAt);

        // both blades: held up and out either side of his head, then swung down across the lens (his right arm is on your left)
        Vector3 chest = rig.Bone(HumanBodyBones.UpperChest).position;
        Vector3 heldL = C + fw * bladesHeld.x - rt * bladesHeld.y + up * (bladesHeld.z + 0.04f * Jitter(q, 30f, 21f));
        Vector3 heldR = C + fw * bladesHeld.x + rt * bladesHeld.y + up * (bladesHeld.z + 0.04f * Jitter(q, 30f, 22f));
        Vector3 acrossL = C + fw * 0.30f + rt * 0.34f - up * 0.30f, acrossR = C + fw * 0.30f - rt * 0.34f - up * 0.30f;
        Arm(rig, false, Vector3.Lerp(heldL, acrossL, sl), chest - rt * 1.5f + up * 1.2f);
        Arm(rig, true, Vector3.Lerp(heldR, acrossR, sr), chest + rt * 1.5f + up * 1.2f);
        Hand(rig, false, Vector3.Slerp((-fw * 0.55f + rt * 0.45f - up * 0.55f).normalized, (rt * 0.7f - up * 0.6f - fw * 0.2f).normalized, sl));
        Hand(rig, true, Vector3.Slerp((-fw * 0.55f - rt * 0.45f - up * 0.55f).normalized, (-rt * 0.7f - up * 0.6f - fw * 0.2f).normalized, sr));

        ShakenCamera(rig, C, fw, up, s, hit, Hit);
        KillLight(rig, killLight + 0.5f * Jitter(s, 20f, 11f), C - fw * killLightBack + up * 0.1f, faceAt - up * 0.15f, 70f);
        TapeHit(rig, s, 0.42f, 0.45f, 0.014f, 0.6f);

        Streak(rig, st.slashA, 0.28f, s - firstSlash);
        Streak(rig, st.slashB, 0.275f, s - secondSlash);
        if ((s >= firstSlash && s < firstSlash + 0.04f) || (s >= secondSlash && s < secondSlash + 0.04f))
        {
            rig.SetVhs(1f, -1f, 0.014f);
            rig.fx.flashColor = Color.white;
            rig.fx.flash = 0.3f;
        }
    }
}
