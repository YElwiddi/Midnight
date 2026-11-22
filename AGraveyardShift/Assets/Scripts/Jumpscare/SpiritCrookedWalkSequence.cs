using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// The Spirit's kill (ending I): "Out of the Light", then the Crooked Walk, then Three Frames Down.
/// Everything up to the moment she lets go of your collar is SpiritDraggedSequence, untouched. Then the end of
/// the path glows where your torch lies, and she is standing in the glow, far off, a black shape. She walks to
/// you in eight steps, each a different wrong shape and each sooner than the last, her limbs bending in places
/// limbs have no joints, turning pale as she leaves the light behind her. Over you she comes down in three more
/// held shapes, smiling wider in each; the smile is all teeth; it opens; and it is the last thing you see.
///
/// This class is a layer on top of its base: the base plays with its own ending switched off (see
/// <see cref="Layered"/>) and what is added here is drawn over the frame it leaves. Per-kill state lives in
/// <see cref="Layer"/>, never on the asset.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Spirit Crooked Walk", fileName = "SpiritCrookedWalk")]
public class SpiritCrookedWalkSequence : SpiritDraggedSequence
{
    [Header("The Crooked Walk")]
    [Tooltip("The torch's beam starts to glow in the ground mist.")]
    public float glowAt = 3.05f;
    [Tooltip("She is standing in the glow, far off.")]
    public float walkAppear = 3.5f;
    [Tooltip("Her arms drop into their first wrong shape.")]
    public float firstShape = 3.95f;
    [Tooltip("When each step starts (s). They come sooner each time.")]
    public float[] stepAt = { 4.10f, 4.40f, 4.68f, 4.94f, 5.18f, 5.40f, 5.58f, 5.72f };
    [Tooltip("How far ahead of you each step leaves her (m).")]
    public float[] stepTo = { 7.9f, 7.1f, 6.2f, 5.3f, 4.4f, 3.5f, 2.6f, 1.75f };
    [Tooltip("How far ahead of you she first stands (m).")]
    public float walkStart = 8.7f;

    [Header("Three Frames Down")]
    [Tooltip("When she takes each of the three shapes she comes down to you in (s). The first ends the walk.")]
    public float[] downAt = { 5.84f, 5.96f, 6.08f };
    [Tooltip("Her face's distance from your eyes in each shape (m).")]
    public float[] downDistance = { 1.5f, 0.98f, 0.54f };
    [Tooltip("Her head tipped onto a shoulder in each shape (degrees).")]
    public float[] downRoll = { -26f, 22f, -7f };
    [Tooltip("How wide she is smiling in each shape (0..1).")]
    public float[] downGrin = { 0.45f, 0.75f, 1f };
    [Tooltip("The smile opens.")]
    public float mouthOpens = 6.30f;
    [Tooltip("Her mouth outgrows her face and comes down over you.")]
    public float bite = 6.44f;
    [Tooltip("Black.")]
    public float walkCut = 6.56f;

    [Header("Her Mouth")]
    [Tooltip("Graveyard/Painted Maw (referenced here so builds include it; found by name when empty, which only works in the editor).")]
    public Shader mawShader;
    [Tooltip("Texels per head unit her mouth is painted at. Her face measures about 120; a little finer keeps each tooth readable.")]
    public float mawDensity = 150f;
    [Tooltip("Needles in the upper front row.")]
    public int mawTeeth = 8;

    private const float Never = 999f, SnapFrame = 0.034f;
    // how far her jaw has dropped behind the grin in each of the three shapes, and where her hands are
    // (1 = up either side of her face, 0 = flat on the ground beside your head)
    private static readonly float[] DownJaw = { 0.1f, 0.14f, 0.18f }, DownHands = { 1f, 1f, 0.1f };

    private class Layer
    {
        public KillBreaks breaks;
        public KillPoser poser;
        public PaintedMaw maw;
        /// <summary>The walk pressed into the clear ground ahead of you (1 = all of it, as staged).</summary>
        public float scale = 1f;
        public bool measured;
    }

    // Keyed by the rig, which is made for one kill and dropped after it: nothing here outlives a kill.
    private static readonly ConditionalWeakTable<KillSequenceRig, Layer> layers = new ConditionalWeakTable<KillSequenceRig, Layer>();

    public override float Duration { get { return walkCut + blackHold; } }

    private void OnValidate() { Layered(); }

    // The base plays Eclipse with nothing in it: she lets go, and you lie looking at your torch. (Whatever the asset
    // says, so that nobody can switch the base's own ending back on underneath this one.)
    private void Layered()
    {
        ending = Ending.Eclipse;
        eclipseFinale = EclipseFinale.Crawl;
        eclipseAppear = eclipseRush = eclipseCut = finaleAt = Never;
    }

    public override void Begin(KillSequenceRig rig)
    {
        Layered();
        base.Begin(rig);
        var L = new Layer();
        L.breaks = KillBreaks.Build(rig, 0.5f, true, KillBreaks.AllLimbs);
        L.poser = new KillPoser(rig, L.breaks, additiveShader != null ? additiveShader : Shader.Find("Legacy Shaders/Particles/Additive"));
        L.poser.torchAt = rig.Ground(rig.At(torchLands.x, torchLands.y)) + Vector3.up * 0.04f + rig.forward * 0.35f;
        L.poser.haloAt = L.poser.torchAt;
        Shader paint = mawShader != null ? mawShader : Shader.Find("Graveyard/Painted Maw");
        if (L.breaks != null && L.breaks.jaw != null && paint != null)
        {
            SkinnedMeshRenderer skin = rig.killer.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Texture face = skin != null && skin.sharedMaterial != null ? skin.sharedMaterial.mainTexture : null;
            L.maw = new PaintedMaw(rig, L.breaks, paint, face != null ? face.filterMode : FilterMode.Bilinear, mawDensity, mawTeeth, KillPoser.GlowLayer);
        }
        layers.Remove(rig);
        layers.Add(rig, L);
        Debug.Log("SpiritCrookedWalk: " + (L.breaks != null ? L.breaks.summary : "no extra joints") + (L.maw != null ? ", painted mouth" : ", no mouth"));
    }

    // The glare on the dropped torch's lens is the base's prop and private to it. It is found by the name the base
    // gives it, the first time it is lit, and renamed so that no later kill can mistake it for its own.
    private static void ClaimGlare(KillPoser p)
    {
        if (p.torchGlare != null) return;
        GameObject g = GameObject.Find("DroppedTorchGlare");
        if (g == null) return;
        g.name = "DroppedTorchGlare (crooked walk)";
        p.torchGlare = g;
    }

    public override void End(KillSequenceRig rig)
    {
        Layer L;
        if (layers.TryGetValue(rig, out L))
        {
            if (L.maw != null) L.maw.Hide();
            L.poser.Restore();
            if (L.breaks != null) L.breaks.Dispose();
            layers.Remove(rig);
        }
        base.End(rig);
    }

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        Layer L;
        layers.TryGetValue(rig, out L);     // gone once End has run: the game keeps evaluating until the ending screen is up
        if (L != null) L.poser.BeginPose();
        base.Evaluate(rig, t);
        if (L != null)
        {
            if (t >= yank) ClaimGlare(L.poser);
            L.poser.LightsOff();
            if (L.maw != null) L.maw.Hide();
            Walk(rig, L, t);
        }
        if (t >= walkCut)
        {
            rig.fx.flashColor = Color.black;
            rig.fx.flash = 1f;
        }
    }

    // ------------------------------------------------------------------ timing

    private static int Beat(float t, float[] at)
    {
        int k = -1;
        for (int i = 0; i < at.Length; i++) if (t >= at[i]) k = i;
        return k;
    }

    private static bool Hit(float t, float at) { return t >= at && t - at < SnapFrame; }

    private static float Pick(float[] a, int i, float fallback) { return a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : fallback; }

    private int Steps { get { return Mathf.Min(stepAt.Length, stepTo.Length); } }

    private float WalkEnd { get { return Pick(downAt, 0, 5.84f); } }

    // ------------------------------------------------------------------ the frame

    private void Walk(KillSequenceRig rig, Layer L, float t)
    {
        KillPoser p = L.poser;
        if (t < glowAt) return;
        if (!L.measured) Measure(rig, L);
        if (t >= walkAppear - 0.066f && t < walkAppear) { p.TorchOff(); return; }     // the two dark frames she arrives in
        p.Halo(HaloK(t), 12f);
        if (t < walkAppear) return;
        rig.Visible(true);
        Vector3 cam = rig.cam.position;
        if (t < WalkEnd) Walking(rig, L, cam, t);
        else ComingDown(rig, L, cam, t);
    }

    // ---------------- the ground in front of you
    // She walks the ground you were dragged over, which the base has already found clear behind you. When the drag was
    // cut short (something was behind you), the walk would start beyond it: so it is pressed into the clear ground
    // there is, starting short of anything in the way and with the torch's glow still behind her, as it was staged.
    // Measured once, after she has let go: you lie where you will lie from then on.
    private void Measure(KillSequenceRig rig, Layer L)
    {
        L.measured = true;
        KillPoser p = L.poser;
        int steps = Steps;
        if (steps == 0) return;
        Vector3 cam = rig.cam.position;
        float last = stepTo[steps - 1];
        float room = RoomAhead(rig, cam, p.F, walkStart + 1f);
        float torch = Vector3.Dot(p.torchAt - cam, p.F);
        float start = Mathf.Clamp(Mathf.Min(walkStart, Mathf.Min(room - 0.6f, torch - 1.6f)), Mathf.Min(walkStart, last + 1.75f), walkStart);
        L.scale = walkStart > last ? (start - last) / (walkStart - last) : 1f;
        // if she has to start past the torch, the glow goes back behind her (short of whatever is there)
        float glow = Mathf.Min(start + 1.6f, room - 0.1f);
        p.haloAt = p.torchAt + p.F * Mathf.Max(0f, glow - torch);
        if (L.scale < 0.99f) Debug.Log("SpiritCrookedWalk: " + room.ToString("F1") + " m clear ahead, torch at " + torch.ToString("F1") + " m: she starts at " + start.ToString("F1") + " m");
    }

    // Open ground ahead of where you lie, up to max (m): the nearest thing a body-sized sphere hits going forwards at
    // headstone height that isn't her, you or an invisible barrier.
    private static float RoomAhead(KillSequenceRig rig, Vector3 cam, Vector3 F, float max)
    {
        float nearest = max;
        Vector3 from = rig.Ground(cam) + Vector3.up * 0.6f;
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.2f, F, max, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(rig.killer) || (rig.player != null && t.IsChildOf(rig.player))) continue;
            string n = h.collider.name.ToLowerInvariant();
            if (n.Contains("donotpass") || n.Contains("barrier") || n.Contains("invisible")) continue;
            if (h.distance > 0f && h.distance < nearest) nearest = h.distance;
        }
        return nearest;
    }

    // Where a distance on the walk's own scale puts her, pressed into the room there is.
    private float Placed(Layer L, float d)
    {
        float last = Steps > 0 ? stepTo[Steps - 1] : d;
        return d <= last ? d : last + (d - last) * L.scale;
    }

    // How bright the torch's glow in the mist is.
    private float HaloK(float t)
    {
        return Mathf.Lerp(0.1f, 0.5f, InOut(Seg(t, glowAt, walkAppear - 0.1f))) * (0.93f + 0.07f * Noise(t * 9f, 3));
    }

    // ---------------- the walk: she stands in the glow, her arms drop wrong, and then she comes
    private void Walking(KillSequenceRig rig, Layer L, Vector3 cam, float t)
    {
        KillPoser p = L.poser;
        int steps = Steps;
        int step = Mathf.Min(Beat(t, stepAt), steps - 1);
        bool planted = false, snap;
        if (step < 0) snap = Hit(t, firstShape);
        else
        {
            float next = step + 1 < steps ? stepAt[step + 1] : WalkEnd;
            float plant = stepAt[step] + (next - stepAt[step]) * 0.45f;      // a leg comes up; then it is down, and she is nearer
            planted = t >= plant;
            snap = t - stepAt[step] < SnapFrame || (planted && t - plant < SnapFrame);
        }
        float d;
        if (step < 0) d = walkStart;
        else
        {
            float from = step == 0 ? walkStart : stepTo[step - 1];
            d = planted ? stepTo[step] : Mathf.Lerp(from, stepTo[step], 0.3f);
        }
        rig.Place(rig.Ground(cam + p.F * Placed(L, d)), -p.F);
        p.Frame();
        if (step < 0)
        {
            if (t < firstShape) Still(rig, p);
            else Hang(p);
        }
        else Stride(p, planted ? 1 : 0, step % 2 == 1, step, Mathf.Lerp(1.15f, 1.5f, Mathf.Clamp01(step / (float)Mathf.Max(1, steps - 1))));
        Vibrate(rig, cam, t, 0.55f);
        // a black cut-out against the glow; as she comes out of it her own cold light finds her (on the walk's own
        // scale, so it happens on the same step wherever she started)
        float near = Mathf.InverseLerp(5.8f, 2.6f, d);
        p.Glow(1.2f * near, 0.35f);
        p.Pool(0.5f * near);
        // your eyes on her as she comes, and the small jolt of each step
        Vector3 look = Vector3.Lerp(rig.Bone(HumanBodyBones.Hips).position, rig.Face, 0.45f);
        p.Look(look, 0.72f * InOut(Seg(t, walkAppear - 0.2f, walkAppear + 0.15f)));
        if (step >= 0) p.Turn(Kick(t, stepAt[step], 0.9f + 0.25f * step, 7f, 12f), 0f, 0f);
        rig.SetVhs(snap ? 0.3f : 0.1f, 0.04f);
        if (snap && step >= 5) rig.fx.split = 0.003f;
    }

    // ---------------- three frames down: bent double over you, nearer in each, smiling; then the smile opens
    private void ComingDown(KillSequenceRig rig, Layer L, Vector3 cam, float t)
    {
        KillPoser p = L.poser;
        int k = Mathf.Clamp(Beat(t, downAt), 0, 2);
        bool opened = t >= mouthOpens;
        float eat = In2(Seg(t, bite, walkCut));
        float dist = Pick(downDistance, k, 0.54f) - (k == 2 ? 0.1f * InOut(Seg(t, Pick(downAt, 2, 6.08f), bite)) : 0f);
        Loom(rig, p, cam, dist, opened ? 0f : Pick(downRoll, k, 0f), DownHands[k]);
        if (L.maw != null)
        {
            float open = opened ? 1f + 0.04f * Mathf.Sin((t - mouthOpens) * 55f) : DownJaw[k];     // it judders open
            L.maw.Set(open, Pick(downGrin, k, 1f), opened ? 0f : 1f, Mathf.Lerp(1f, 2.7f, eat));
        }
        Vibrate(rig, cam, t, opened ? 0.5f + 0.5f * eat : 0.14f);
        rig.PutFace(Mark(p, cam, dist));
        if (eat > 0f && L.maw != null)
        {
            // it is her mouth that comes to you now, not her face
            Vector3 m = L.maw.Centre;
            rig.killer.position += Vector3.Lerp(m, Mark(p, cam, 0.2f), eat) - m;
        }
        p.Glow(1.35f, 0.5f);
        p.Pool(0f);
        // your eyes: on her face, then drawn down to her teeth
        Vector3 look = rig.Face + Vector3.down * 0.03f;
        if (L.maw != null) look = Vector3.Lerp(look, L.maw.Centre, k < 2 ? 0.25f : (opened ? 0.62f : 0.5f));
        p.Look(look, 0.93f);
        float d0 = Pick(downAt, 0, 5.84f), d1 = Pick(downAt, 1, 5.96f), d2 = Pick(downAt, 2, 6.08f);
        p.Turn(Kick(t, d0, 5f, 5f, 8f) + Kick(t, d1, 3f, 6f, 9f) + Kick(t, d2, 3f, 6f, 9f) + Kick(t, mouthOpens, -4f, 6f, 9f), 0f,
            Kick(t, d0, -4f, 4f, 7f) + Kick(t, d1, 3f, 5f, 8f) + Kick(t, d2, -3f, 5f, 8f));
        bool snap = Hit(t, d0) || Hit(t, d1) || Hit(t, d2) || Hit(t, mouthOpens);
        rig.SetVhs(snap ? 0.5f : 0.07f, 0.035f, snap ? 0.006f : -1f);
        if (eat > 0f)
        {
            rig.SetVhs(0.6f, 0.07f, 0.008f, 0.05f);
            rig.fx.split = 0.006f * eat;
        }
    }

    // Where her face is held: dist metres from your eyes, up the line she leans down.
    private static Vector3 Mark(KillPoser p, Vector3 cam, float dist)
    {
        return cam + (Vector3.up * 0.93f + p.F * 0.36f).normalized * dist;
    }

    // ------------------------------------------------------------------ her shapes

    private static void Still(KillSequenceRig rig, KillPoser p)
    {
        p.Pose("idle", 0.6f);
        rig.HeadTurn(rig.cam.position, 0f, 0f, 40f);
    }

    // Standing, both arms dropped into zig-zags, her head right over on her shoulder.
    private static void Hang(KillPoser p)
    {
        p.Pose("idle", 0.6f);
        p.Shoulders(24f, -14f);
        p.Arm(true, p.D(-0.9f, -0.45f, 0f), p.D(0.15f, -1f, 0.05f), p.D(-0.85f, -0.5f, 0.15f), p.D(0.2f, -1f, 0.1f), 1.15f, 1.15f, 1.15f, 1.15f);
        p.Arm(false, p.D(0.35f, -0.95f, 0f), p.D(0.95f, -0.3f, 0.05f), p.D(0.2f, -1f, 0.1f), p.D(0.9f, -0.4f, 0.15f), 1.15f, 1.15f, 1.15f, 1.15f);
        p.Hand(true, p.D(-0.2f, -1f, 0.1f), 1.4f);
        p.Hand(false, p.D(0.3f, -1f, 0.1f), 1.4f);
        p.Head(96f);
    }

    // One moment of a step. phase 0: a leg hooked up and out, her body thrown the other way; phase 1: it is down.
    // Four arm shapes take turns, mirrored on every other step; len lengthens every limb as she comes.
    private static void Stride(KillPoser p, int phase, bool mirrored, int variant, float len)
    {
        p.mirror = false;
        p.Frame();
        p.Pose("idle", 0.6f);
        p.LearnSoles();
        p.mirror = mirrored;
        p.Frame();
        float arm = len + 0.1f;
        if (phase == 0)
        {
            p.Hips(-8f, 0f, -6f);
            p.Spine(-14f, -16f, 0f);
            p.Leg(false, p.D(0.15f, -1f, 0.1f), p.D(-0.25f, -1f, 0f), p.D(0.35f, -0.95f, 0f), p.D(-0.1f, -1f, 0.05f), len, len, len, len);
            p.Leg(true, p.D(-0.85f, 0.3f, 0.35f), p.D(-1f, -0.3f, 0.3f), p.D(-0.15f, -1f, 0.2f), p.D(-0.65f, -0.75f, 0.25f), len, len, len, len);
            p.Foot(true, p.D(-0.3f, -0.8f, 0.5f));
            p.Stand();
        }
        else
        {
            p.Spine(12f, 10f, 0f);
            p.Leg(true, p.D(-0.8f, -0.55f, 0.3f), p.D(-0.45f, -0.9f, 0.2f), p.D(0.15f, -1f, 0f), p.D(-0.4f, -0.9f, 0.1f), len, len, len, len);
            p.Leg(false, p.D(0.6f, -0.75f, -0.1f), p.D(0.2f, -1f, -0.1f), p.D(0.5f, -0.85f, -0.2f), p.D(0f, -1f, -0.1f), len, len, len, len);
            p.Stand();
        }
        switch (variant % 4)
        {
            case 0:   // one arm thrown up in a zig-zag, the other hanging broken
                p.Arm(true, p.D(-0.7f, 0.75f, 0f), p.D(-1f, -0.1f, 0.1f), p.D(-0.5f, 0.85f, 0.1f), p.D(-0.9f, 0.1f, 0.2f), arm, arm, arm, arm);
                p.Arm(false, p.D(0.6f, -0.8f, 0f), p.D(-0.05f, -1f, 0.1f), p.D(0.75f, -0.6f, 0.2f), p.D(0.05f, -1f, 0.1f), arm, arm, arm, arm);
                p.Hand(true, p.D(-0.2f, -1f, 0.2f), 1.6f);
                p.Hand(false, p.D(0.2f, -1f, 0.1f), 1.6f);
                p.Head(phase == 0 ? -75f : 42f);
                break;
            case 1:   // both out wide, forearms dropped
                p.Shoulders(14f, 14f);
                p.Arm(true, p.D(-0.9f, 0.45f, 0f), p.D(-1f, -0.15f, 0.1f), p.D(-0.2f, -1f, 0.15f), p.D(-0.75f, -0.65f, 0.2f), arm, arm, arm, arm);
                p.Arm(false, p.D(0.9f, 0.45f, 0f), p.D(1f, -0.15f, 0.1f), p.D(0.2f, -1f, 0.15f), p.D(0.75f, -0.65f, 0.2f), arm, arm, arm, arm);
                p.Hand(true, p.D(-0.2f, -1f, 0.2f), 1.6f);
                p.Hand(false, p.D(0.2f, -1f, 0.2f), 1.6f);
                p.Head(phase == 0 ? 95f : -60f);
                break;
            case 2:   // one arm bent right over her head, the other straight out with the wrist broken
                p.Arm(true, p.D(-0.5f, 0.9f, 0f), p.D(0.35f, 0.95f, 0.05f), p.D(0.9f, 0.35f, 0.15f), p.D(0.55f, -0.8f, 0.2f), arm, arm, arm, arm);
                p.Arm(false, p.D(1f, 0.1f, 0f), p.D(0.9f, -0.35f, 0.1f), p.D(1f, 0.25f, 0.1f), p.D(0.8f, -0.55f, 0.2f), arm, arm, arm, arm);
                p.Hand(true, p.D(0.1f, -1f, 0.2f), 1.6f);
                p.Hand(false, p.D(0f, -1f, 0.2f), 1.6f);
                p.Head(phase == 0 ? 30f : -110f);
                break;
            default:  // both hanging long past her knees, shoulders up round her ears
                p.Shoulders(26f, 22f);
                p.Arm(true, p.D(-0.35f, -1f, 0f), p.D(-0.85f, -0.55f, 0.1f), p.D(0.3f, -1f, 0.15f), p.D(-0.5f, -0.85f, 0.1f), arm * 1.2f, arm * 1.2f, arm * 1.2f, arm * 1.2f);
                p.Arm(false, p.D(0.75f, -0.65f, 0f), p.D(0.1f, -1f, 0.1f), p.D(0.8f, -0.6f, 0.2f), p.D(0.1f, -1f, 0.1f), arm * 1.2f, arm * 1.2f, arm * 1.2f, arm * 1.2f);
                p.Hand(true, p.D(-0.1f, -1f, 0.1f), 1.7f);
                p.Hand(false, p.D(0.1f, -1f, 0.1f), 1.7f);
                p.Head(phase == 0 ? -140f : 150f);
                break;
        }
    }

    // Bent double over you from where she stands: her face brought down to a mark above your eyes (faceDist m from
    // them), her legs planted wide either side of your torch. hands 1 = up either side of her face where you can
    // see them; 0 = on the ground beside your head. roll tips her head.
    private static void Loom(KillSequenceRig rig, KillPoser p, Vector3 cam, float faceDist, float roll, float hands)
    {
        Vector3 F = p.F, R = p.R;
        rig.Place(rig.Ground(cam) + F * 1.15f, -F);
        p.Frame();
        p.Pose("idle", 0.6f);
        p.LearnSoles();
        p.Hips(0f, 0f, 46f);
        p.Spine(70f);
        p.Shoulders(18f, 18f);
        Vector3 mark = Mark(p, cam, faceDist);
        rig.Stare(cam, 1f);
        rig.HeadTurn(cam, roll, 0f, 0f, 0.4f);
        rig.PutFace(mark);
        Vector3 footL = rig.Ground(cam + F * 0.95f + R * 0.62f) + Vector3.up * 0.07f, footR = rig.Ground(cam + F * 0.95f - R * 0.62f) + Vector3.up * 0.07f;
        p.ZigTo(false, true, footL, R * 0.5f + Vector3.up + F * 0.4f, "++--", 0f, 1.5f);
        p.ZigTo(false, false, footR, -R * 0.5f + Vector3.up + F * 0.4f, "++--", 0f, 1.5f);
        p.FootFlat(true, (-F + R * 0.5f).normalized, Vector3.down);
        p.FootFlat(false, (-F - R * 0.5f).normalized, Vector3.down);
        Vector3 groundL = rig.Ground(cam + R * 0.5f + F * 0.1f) + Vector3.up * 0.05f, groundR = rig.Ground(cam - R * 0.5f + F * 0.1f) + Vector3.up * 0.05f;
        Vector3 axis = (mark - cam).normalized;
        Vector3 upL = cam + axis * (faceDist + 0.12f) + R * 0.36f - F * 0.02f, upR = cam + axis * (faceDist + 0.12f) - R * 0.36f - F * 0.02f;
        p.ZigTo(true, true, Vector3.Lerp(groundL, upL, hands), R * 0.3f + Vector3.up, "+-+-", 34f, 1.5f);
        p.ZigTo(true, false, Vector3.Lerp(groundR, upR, hands), -R * 0.3f + Vector3.up, "+-+-", 34f, 1.5f);
        Vector3 claw = (cam - mark).normalized * 0.7f - F * 0.5f;
        p.Hand(true, (claw - R * 0.25f).normalized, 2.0f, 30f);
        p.Hand(false, (claw + R * 0.25f).normalized, 2.0f, 30f);
    }
}
