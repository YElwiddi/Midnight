using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Bride's kill (ending IV, "Happily Ever After"). She has waited at the altar since her groom never came.
/// Tonight someone walked up the aisle. <see cref="variant"/> picks how it goes:
/// Aisle: she takes your head in her hands and you are standing at the altar, looking down the aisle. The candles
/// light. She comes up the aisle to the organ, a jump closer on every note, and every candle she passes goes out.
/// She reaches for you and breaks herself backwards, then holds you, and her roses burst.
/// First Dance: she takes your hands and waltzes you round the nave to a music box. Every bar she is more wrong.
/// The box winds down; she dips you and lets go.
/// Threshold (Laid to Rest): her hair falls over your eyes and she carries you to the altar in the dark; you wake on
/// it, her roses fall on you, and at the foot of the altar she comes apart while the petals cover your eyes.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Bride", fileName = "BrideKill")]
public class BrideSequence : KillSequence
{
    public enum Variant
    {
        [Tooltip("At the altar: she comes up the aisle with her bouquet, a jump closer and more broken on every organ note, putting out the candles, then throws herself at you.")]
        Aisle,
        [Tooltip("She waltzes you round the nave to a music box and comes apart, bar by bar.")]
        FirstDance,
        [Tooltip("Laid to Rest: carried to the altar in the dark under her hair; her roses fall on you; she comes apart at the foot of the altar while the petals cover your eyes.")]
        Threshold,
        [Tooltip("The Aisle's procession; then her hair over your eyes, and you wake on the altar under her roses (Laid to Rest's end).")]
        AisleToAltar,
        [Tooltip("You lie on the altar and watch her come up the aisle to you; she leans over you while her petals cover your eyes.")]
        Wake,
    }

    public Variant variant = Variant.Aisle;

    [Header("Animation")]
    [Tooltip("BrideWalking")]
    public AnimationClip walk;
    [Tooltip("IdleBride")]
    public AnimationClip idle;
    [Tooltip("Stumble: bent over, the long arms reaching")]
    public AnimationClip reach;
    [Tooltip("Laying Seizure: lying on her back")]
    public AnimationClip lie;
    [Tooltip("Breathing Idle: standing up straight, her face up")]
    public AnimationClip stand;
    [Tooltip("TWalking: a walk with her arms down (the procession)")]
    public AnimationClip stride;
    [Tooltip("A still moment of the lying clip (s).")]
    public float lieTime = 0.4f;

    [Header("The Church")]
    [Tooltip("The object she waits at, in front of the altar (the event's spawn point).")]
    public string altarMarker = "BrideSpawn";
    [Tooltip("An object at the church's door end; the aisle runs from the altar marker toward it.")]
    public string doorMarker = "ChurchSpawn";
    [Tooltip("Where she is (m) when the kill starts. Keep it equal to the event's killStopDistance.")]
    public float startDistance = 2.6f;
    [Tooltip("Black before the ending screen.")]
    public float blackHold = 0.6f;

    [Header("Aisle (and the two that start like it)")]
    [Tooltip("As she catches you, she lifts you by the head to her face. Off: her charge carries her into you and " +
             "it all goes white (no close-up of her face).")]
    public bool seizeFace = true;
    [Tooltip("Without the close-up: how long the white takes to cover her (s).")]
    public float catchWhite = 0.08f;
    [Tooltip("Her hands are on your head.")]
    public float seizeHands = 0.22f;
    [Tooltip("White, and you are at the altar.")]
    public float altarCut = 0.5f;
    [Tooltip("The candle pairs light, from the door end toward you (first, gap).")]
    public float candlesLight = 0.56f, candleGap = 0.08f;
    [Tooltip("How far down the aisle each candle pair stands (m from the altar).")]
    public float[] candleDistances = { 6f, 11f, 16f, 21f, 26f, 31f };
    [Tooltip("How far either side of the aisle the candles stand (m).")]
    public float candleSide = 2.4f;
    [Tooltip("She is at the door end of the aisle, a bride with her bouquet.")]
    public float aisleAppear = 1.1f;
    public float aisleAppearDistance = 34f;
    [Tooltip("The organ's notes; on each one she is suddenly closer, and more broken.")]
    public float[] aisleNotes = { 1.2f, 1.5f, 1.725f, 1.8f, 2.4f, 2.7f, 2.925f, 3.0f };
    [Tooltip("How far she is after each note (m).")]
    public float[] aisleDistances = { 28f, 23f, 20.5f, 17f, 12f, 8.5f, 6.2f, 4.2f };
    [Tooltip("The note she drops her bouquet on.")]
    public int bouquetDropsAt = 3;
    [Tooltip("The organ stops and she is right in front of you.")]
    public float aisleArrive = 3.55f;
    public float aisleArriveDistance = 1.7f;
    [Tooltip("Her head comes back upright, a crack at a time, to look at you.")]
    public float rightHead = 3.62f, rightHeadEnd = 4.0f;
    [Tooltip("Her head rises high and to one side on her neck, like a snake drawing back (Aisle, Aisle To Altar).")]
    public float aisleRecoil = 4.0f;
    [Tooltip("Aisle: her neck pays out and her face strikes at you; black.")]
    public float aisleLunge = 4.2f, aisleCut = 4.34f;
    [Tooltip("Aisle To Altar: her hair over your eyes; you wake on the altar; black at the end of Laid to Rest's ending.")]
    public float toAltarDark = 4.3f, toAltarWake = 4.75f, toAltarCut = 7.05f;
    [Tooltip("Wake: her neck rises out of her and her face comes over yours; her roses fall on you; black.")]
    public float wakeLean = 4.2f, wakeCut = 5.4f;
    [Tooltip("A light on her face from your side, so it reads from the far end of the aisle (0 = none).")]
    public float faceLight = 2.4f;

    [Header("First Dance")]
    [Tooltip("She has your hands and the waltz starts.")]
    public float danceStart = 0.3f;
    [Tooltip("One bar of the waltz (s); four bars.")]
    public float danceBar = 0.9f;
    [Tooltip("Turns per bar while the box plays at speed.")]
    public float danceTurnsPerBar = 0.75f;
    [Tooltip("The box winds down from here and stops.")]
    public float danceSlow = 3.05f, danceStop = 3.95f;
    [Tooltip("Your distance from the middle of the dance, and her face's distance from you (m).")]
    public float danceRadius = 0.7f, danceFace = 1.0f;
    [Tooltip("She dips you, then lets go; black.")]
    public float danceDip = 4.25f, danceLetGo = 4.55f, danceCut = 5.05f;

    [Header("Laid To Rest")]
    [Tooltip("Her face is at yours and her hair is over your eyes.")]
    public float scoop = 0.35f;
    [Tooltip("Carried in the dark: when your eyes open on the vaults going by, and for how long.")]
    public float[] glimpses = { 0.62f, 0.98f, 1.34f };
    public float glimpseLength = 0.14f;
    [Tooltip("Your eyes open on the altar; the altar candles light.")]
    public float wake = 1.75f, candlesAt = 1.85f;
    [Tooltip("Her roses fall from her hands.")]
    public float petalsFrom = 2.3f, petalsTo = 3.3f;
    [Tooltip("She steps back to the foot of the altar.")]
    public float stepBack = 3.3f, stepBackEnd = 3.55f;
    [Tooltip("Her head turns over; her arms hang longer; the petals cover your eyes: black.")]
    public float turnFrom = 3.6f, turnTo = 4.4f, stretchFrom = 3.75f, thresholdCut = 4.7f;

    [Header("Props")]
    [Tooltip("Unlit/Transparent and Legacy Shaders/Particles/Additive, for her hair over your eyes, her petals and the " +
             "candle flames. Referenced here so builds include them (found by name when empty, which only works in the editor).")]
    public Shader transparentShader, additiveShader, litShader;

    private const float CandleLight = 2.6f;

    private class Candle
    {
        public Transform root, flame;
        public Light light;
        public float along;                 // distance from the altar along the aisle (Aisle)
        public float litAt = -1f, outAt = -1f;
        public float seed;
        public int group;                   // 0 the aisle, 1 the altar, 2 round the dance
    }

    private class Petal
    {
        public Transform t;
        public Vector3 p, v, axis;
        public float spin, born;
    }

    private class State
    {
        public Vector3 altar;          // the floor in front of the altar, where she waited
        public Vector3 aisle;          // along the aisle, from the altar toward the door
        public Vector3 altarTop;       // the middle of the altar's top
        public float altarHalfDepth = 0.8f;
        public readonly List<Candle> candles = new List<Candle>();
        public readonly List<Petal> petals = new List<Petal>();
        public Material petalMat, flameMat, haloMat;
        public Mesh quad;
        public Transform hair;
        public float hairBase;
        public Texture2D lensPetals;
        public Vector3 danceCenter, danceForward;
        public float carryFrom, carryTo;    // along the aisle (m from the altar)
        public float carrySide;
        public float lastPetalSpawn = -1f;
        public int burstDone;
        public Light faceLight;
        public Transform bouquet;
        public bool bouquetDropped;
        public readonly List<KeyValuePair<Transform, Vector3>> restLocal = new List<KeyValuePair<Transform, Vector3>>();   // bones moved, not just turned
        public readonly List<KeyValuePair<SkinnedMeshRenderer, bool>> skins = new List<KeyValuePair<SkinnedMeshRenderer, bool>>();
        public readonly List<Renderer> shown = new List<Renderer>();   // hers that are on (the Head marker sphere is off)
    }

    private float Cut => variant == Variant.Aisle ? aisleCut : variant == Variant.FirstDance ? danceCut : variant == Variant.Threshold ? thresholdCut
        : variant == Variant.AisleToAltar ? toAltarCut : wakeCut;

    public override float Duration => Cut + blackHold;

    // ------------------------------------------------------------------ setup

    public override void Begin(KillSequenceRig rig)
    {
        rig.UseClips(eyeUp, eyeForward, ("walk", walk), ("idle", idle != null ? idle : walk),
            ("reach", reach != null ? reach : walk), ("lie", lie != null ? lie : walk),
            ("stand", stand != null ? stand : idle != null ? idle : walk), ("stride", stride != null ? stride : walk));
        var s = new State();
        rig.state = s;
        foreach (Renderer r in rig.killer.GetComponentsInChildren<Renderer>(true)) if (r.enabled) s.shown.Add(r);
        // her bounds follow her pose, so the small light on her face still finds her when her neck has paid out
        foreach (var smr in rig.killer.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            s.skins.Add(new KeyValuePair<SkinnedMeshRenderer, bool>(smr, smr.updateWhenOffscreen));
            smr.updateWhenOffscreen = true;
        }
        Transform head = rig.Bone(HumanBodyBones.Head), neck = rig.Bone(HumanBodyBones.Neck);
        foreach (Transform b in new[] { neck, head != null ? head.parent : null, head })
            if (b != null) s.restLocal.Add(new KeyValuePair<Transform, Vector3>(b, b.localPosition));

        Transform a = Marker(altarMarker), d = Marker(doorMarker);
        s.altar = a != null ? Floor(rig, a.position + Vector3.up * 1.5f) : rig.ground + rig.forward * 6f;
        Vector3 toDoor = a != null && d != null ? d.position - a.position : -rig.forward;
        toDoor.y = 0f;
        s.aisle = toDoor.sqrMagnitude > 1e-4f ? toDoor.normalized : -rig.forward;
        FindAltar(rig, s);

        Shader add = additiveShader != null ? additiveShader : Shader.Find("Legacy Shaders/Particles/Additive");
        Shader clear = transparentShader != null ? transparentShader : Shader.Find("Unlit/Transparent");
        var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        s.quad = quadGo.GetComponent<MeshFilter>().sharedMesh;
        DestroyNow(quadGo);
        if (add != null) s.flameMat = rig.Keep(new Material(add) { mainTexture = rig.Keep(FlameTexture()) });
        if (s.flameMat != null) s.flameMat.SetColor("_TintColor", new Color(1f, 0.72f, 0.42f, 0.5f));
        if (clear != null) s.petalMat = rig.Keep(new Material(clear) { mainTexture = rig.Keep(PetalTexture()) });
        var fl = rig.Keep(new GameObject("BrideFaceLight", typeof(Light)));
        s.faceLight = fl.GetComponent<Light>();
        s.faceLight.type = LightType.Point;
        s.faceLight.color = new Color(1f, 0.86f, 0.72f);
        s.faceLight.range = 1.3f;
        s.faceLight.shadows = LightShadows.None;
        s.faceLight.renderMode = LightRenderMode.ForcePixel;   // or the ambient red swallows it
        s.faceLight.enabled = false;

        Vector3 across = Vector3.Cross(Vector3.up, s.aisle);
        switch (variant)
        {
            case Variant.Aisle:
            case Variant.AisleToAltar:
            case Variant.Wake:
                foreach (float along in candleDistances)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 p = s.altar + s.aisle * along + across * candleSide * side;
                        var c = MakeCandle(rig, s, Floor(rig, p + Vector3.up * 1.5f), 1.15f, true, 0);
                        c.along = along;
                    }
                MakeBouquet(rig, s);
                if (variant == Variant.AisleToAltar)
                {
                    for (int side = -1; side <= 1; side += 2) MakeCandle(rig, s, s.altarTop + across * 1.1f * side, 0f, true, 1);
                    MakeHair(rig, s, clear);
                }
                break;
            case Variant.FirstDance:
                s.danceForward = rig.forward;
                s.danceCenter = rig.ground + rig.forward * danceRadius;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = s.danceCenter + Quaternion.AngleAxis(30f + i * 60f, Vector3.up) * rig.forward * 3.4f;
                    MakeCandle(rig, s, Floor(rig, p + Vector3.up * 1.5f), 0.95f, true, 2);
                }
                break;
            case Variant.Threshold:
                for (int side = -1; side <= 1; side += 2) MakeCandle(rig, s, s.altarTop + across * 1.1f * side, 0f, true, 1);
                s.carryFrom = Vector3.Dot(rig.ground - s.altar, s.aisle);
                s.carryTo = Vector3.Dot(s.altarTop - s.altar, s.aisle) + s.altarHalfDepth + 0.6f;
                s.carrySide = Vector3.Dot(rig.ground - s.altar, across);
                MakeHair(rig, s, clear);
                break;
        }
        s.lensPetals = rig.Keep(LensPetalTexture());
        foreach (var c in s.candles) SetCandle(c, 0f, 0f);
    }

    public override void End(KillSequenceRig rig)
    {
        if (rig.fx != null) { rig.fx.dirt = null; rig.fx.dirtAmount = 0f; }
        if (rig.state is State s)
        {
            foreach (var kv in s.restLocal) if (kv.Key != null) kv.Key.localPosition = kv.Value;
            foreach (var kv in s.skins) if (kv.Key != null) kv.Key.updateWhenOffscreen = kv.Value;
        }
    }

    // ------------------------------------------------------------------ per frame

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        rig.BeginFrame();
        var s = (State)rig.state;
        foreach (var kv in s.restLocal) kv.Key.localPosition = kv.Value;
        rig.Lanterns(false);
        switch (variant)
        {
            case Variant.FirstDance: Dance(rig, s, t); break;
            case Variant.Threshold: Threshold(rig, s, t); break;
            default: Aisle(rig, s, t); break;
        }
        UpdatePetals(rig, s, t);
        if (t >= Cut) { rig.fx.darken = 1f; Show(s, false); }
    }

    // ------------------------------------------------------------------ Aisle, Aisle To Altar, Wake

    // Where you are while she comes up the aisle: standing at the altar (Aisle, Aisle To Altar), or lying on it with
    // your head raised, looking down the aisle (Wake).
    private Vector3 AisleCam(KillSequenceRig rig, State s) =>
        variant == Variant.Wake ? s.altarTop + Vector3.up * 0.55f : s.altar + Vector3.up * rig.eye;

    // Where she stands when the organ stops: in front of you at the altar, or at the foot of the altar you lie on.
    private Vector3 ArriveAt(KillSequenceRig rig, State s)
    {
        if (variant == Variant.Wake) return Floor(rig, s.altarTop + s.aisle * (s.altarHalfDepth + 0.55f) + Vector3.up * 1.5f);
        return Floor(rig, s.altar + s.aisle * aisleArriveDistance + Vector3.up * 1.5f);
    }

    private void Aisle(KillSequenceRig rig, State s, float t)
    {
        Vector3 F = rig.forward, R = rig.right;
        if (t < altarCut && !seizeFace)
        {
            // Her charge carries her into you and it all goes white.
            Vector3 eye = rig.Eye();
            rig.Place(rig.At(Mathf.Lerp(startDistance, startDistance - 0.9f, Out3(Seg(t, 0f, catchWhite)))), -F);
            rig.Pose(("walk", 1.4f + t * 2.8f, 1f));
            Show(s, t < catchWhite);
            FaceLight(rig, s, eye, 1f);
            Bouquet(rig, s, false, false);
            Vector3 her = rig.Face;
            rig.SetCamera(eye, her - Vector3.up * 0.3f, rig.baseFov, Kick(t, 0f, -6f, 6f, 8f), 0f, Kick(t, 0f, 6f, 5f, 7f));
            rig.Torch(1.4f, eye - Vector3.up * 0.45f + R * 0.3f, her, 12f, 60f);
            rig.SetVhs(0.6f, 0.2f);
            rig.fx.flash = Out3(Seg(t, 0f, catchWhite));
            rig.fx.flashColor = Color.white;
            return;
        }
        if (t < altarCut)
        {
            // She takes your head in her hands and lifts you to her face (she is a head taller than you): her face,
            // under the roses, a hand's width from yours.
            Vector3 eye = rig.Eye();
            float d = Mathf.Lerp(startDistance, 0.62f, Out3(Seg(t, 0f, seizeHands)));
            rig.Place(rig.At(d), -F);
            float stopped = Seg(t, 0f, 0.14f);
            rig.Pose(("stride", 0.4f + t * 2f, 1f - stopped), ("stand", 1f, stopped));
            eye += Vector3.up * Mathf.Max(0f, rig.Face.y - eye.y) * Out3(Seg(t, 0.08f, seizeHands + 0.1f));
            rig.PlaceFace(eye, d);
            rig.LeanTo(eye, 0.15f * Seg(t, 0f, seizeHands));
            rig.Stare(eye, 1f);
            float grip = Out3(Seg(t, 0.04f, seizeHands));
            // her hands round the back of your head, out of sight; her forearms come up at the edges of your eyes
            rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, eye + R * 0.24f - F * 0.1f - Vector3.up * 0.04f, grip, rig.Bone(HumanBodyBones.LeftUpperArm).position + R * 0.5f - Vector3.up * 0.8f);
            rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, eye - R * 0.24f - F * 0.1f - Vector3.up * 0.04f, grip, rig.Bone(HumanBodyBones.RightUpperArm).position - R * 0.5f - Vector3.up * 0.8f);
            Show(s, true);
            FaceLight(rig, s, eye, 1f);
            Bouquet(rig, s, false, false);
            Vector3 face = rig.Face;
            float jolt = Kick(t, seizeHands, -7f, 6f, 8f) + Noise(t * 7f, 3) * 1.5f;
            float tilt = Kick(t, seizeHands, 7f, 5f, 7f);
            rig.SetCamera(eye, face - Vector3.up * 0.08f, rig.baseFov - 6f * Seg(t, seizeHands, altarCut), jolt, 0f, tilt);
            rig.Torch(1.4f, eye - Vector3.up * 0.45f + R * 0.3f, face, 12f, 60f);
            rig.SetVhs(0.25f + 0.6f * Seg(t, seizeHands, altarCut), 0.05f + 0.2f * Seg(t, seizeHands, altarCut));
            rig.fx.flash = In2(Seg(t, altarCut - 0.08f, altarCut));
            rig.fx.flashColor = Color.white;
            return;
        }

        // Aisle To Altar: after her strike, you wake on the altar and Laid to Rest's end plays out
        if (variant == Variant.AisleToAltar && t >= toAltarWake)
        {
            AltarScene(rig, s, t, ToAltarTimes);
            return;
        }

        Vector3 D = s.aisle, across = Vector3.Cross(Vector3.up, D);
        Vector3 camPos = AisleCam(rig, s);
        rig.fx.flash = 1f - Out3(Seg(t, altarCut, altarCut + 0.25f));
        rig.fx.flashColor = Color.white;

        // the candles: lit from the door end toward you, then put out as she passes them
        for (int i = 0; i < s.candles.Count; i++)
        {
            var c = s.candles[i];
            if (c.group != 0) continue;
            int pair = i / 2;
            c.litAt = candlesLight + (candleDistances.Length - 1 - pair) * candleGap;
        }

        // her distance: a jump on every organ note; she arrives when the organ stops
        float dist = -1f;
        int jump = -1;
        if (t >= aisleAppear) dist = aisleAppearDistance;
        for (int i = 0; i < aisleNotes.Length && i < aisleDistances.Length; i++)
            if (t >= aisleNotes[i]) { dist = aisleDistances[i]; jump = i; }
        bool arrived = t >= aisleArrive;
        Vector3 herPos = arrived ? ArriveAt(rig, s) : Floor(rig, s.altar + D * Mathf.Max(dist, 0f) + Vector3.up * 1.5f);
        foreach (var c in s.candles)
            if (c.group == 0 && c.outAt < 0f && dist > 0f && dist < c.along - 0.8f) c.outAt = t;
        foreach (var c in s.candles) if (c.group == 0) LightCandle(c, t, camPos);
        int stage = arrived ? aisleNotes.Length - 1 : Mathf.Max(jump, 0);
        bool dropped = jump >= bouquetDropsAt || arrived;

        float pitch = 0f, yaw = 0f, roll = 0f;
        float breath = 0.35f + 0.9f * Seg(t, aisleArrive, aisleLunge);
        pitch += Noise(t * 3f, 1) * breath;
        yaw += Noise(t * 2.6f, 2) * breath;
        if (jump >= 0 && !arrived) pitch += Kick(t, aisleNotes[jump], 1.6f, 6f, 9f);
        pitch += Kick(t, aisleArrive, -4f, 5f, 7f);
        if (variant == Variant.Wake) { pitch += 2f; roll -= 7f; }
        Vector3 lookAt = camPos + D * 30f + Vector3.up * (variant == Variant.Wake ? -0.6f : 1.0f);

        if (dist <= 0f)
        {
            Show(s, false);
            Bouquet(rig, s, false, false);
        }
        else
        {
            Show(s, true);
            // frozen mid-stride; each jump lands her in another stride, and a little more broken
            float strideAt = jump >= 0 ? 0.13f * jump : 0.5f;
            rig.Place(herPos, -D);
            rig.Pose(arrived ? ("stand", 1.6f + Steps(t, 5f) * 0.3f, 1f) : ("stride", strideAt, 1f));
            if (arrived && variant != Variant.Wake) FaceAlong(rig, camPos, D, aisleArriveDistance);
            Corrupt(rig, t, stage, -D, dropped ? 0f : 1f);
            // her head comes back up to look at you, a crack at a time
            float tilt = HeadTilt(stage) * (1f - Steps(Seg(t, rightHead, rightHeadEnd), 5f));
            if (Mathf.Abs(tilt) > 0.5f) rig.HeadTurn(camPos, tilt, 0f, 0f, 0.3f);
            rig.Stare(camPos, Mathf.Lerp(0.55f, 0.9f, Seg(t, rightHead, rightHeadEnd)));
            if (arrived) Vibrate(rig, camPos, t, 0.18f * (1f - Seg(t, rightHead, rightHeadEnd)));
            Bouquet(rig, s, true, dropped);
            Vector3 face = rig.Face;
            float near = Seg(-Vector3.Distance(camPos, face), -9f, -2.5f);
            lookAt = Vector3.Lerp(Vector3.Lerp(herPos + Vector3.up * 1.6f, face, Seg(-dist, -14f, -4f)), face - Vector3.up * 0.2f, near);
            if (variant == Variant.Wake) lookAt = Vector3.Lerp(lookAt, face, 0.5f);

            if (variant != Variant.Wake && t >= aisleRecoil)
            {
                // her head rises high and off to one side on her neck, like a snake drawing back; then her neck pays
                // out and her face strikes at you, while the rest of her stays where it stopped
                float strikeAt = variant == Variant.Aisle ? aisleLunge : toAltarDark - 0.12f;
                float strikeEnd = variant == Variant.Aisle ? aisleCut : toAltarDark;
                Vector3 toCam = (camPos - face).normalized;
                Vector3 back = face + Vector3.up * 0.6f - across * 0.38f - toCam * 0.1f;
                Vector3 hit = camPos - toCam * 0.32f;
                float recoil = InOut(Seg(t, aisleRecoil, strikeAt));
                float strike = In2(Seg(t, strikeAt, strikeEnd));
                NeckReach(rig, Vector3.Lerp(Vector3.Lerp(face, back, recoil), hit, strike), camPos, t, 0.4f + 0.6f * strike);
                lookAt = Vector3.Lerp(lookAt, rig.Face, 0.5f + 0.5f * strike);
                pitch += Kick(t, strikeAt, -6f, 6f, 8f);
                roll += Kick(t, strikeAt, 7f, 5f, 7f);
            }
            else if (variant == Variant.Wake && t >= wakeLean)
            {
                // her neck rises out of her and her face comes along you to hang in front of yours, looking down at
                // you (in front, not over: her hair stays behind it); her crown sheds on you; her head turns over on it
                float lean = InOut(Seg(t, wakeLean, wakeCut - 0.15f));
                NeckReach(rig, Vector3.Lerp(face, camPos + D * 0.55f + Vector3.up * 0.3f, lean), camPos, t, 0.3f + 0.4f * lean);
                float snap = Seg(t, wakeCut - 0.07f, wakeCut - 0.03f);
                if (snap > 0f) rig.HeadTurn(camPos, 150f * snap, 0f, 0f, 0f);
                if (t - s.lastPetalSpawn > 0.11f)
                {
                    s.lastPetalSpawn = t;
                    Vector2 r = Random2(t) * 0.25f;
                    Spawn(s, rig, rig.Face + Vector3.up * 0.2f + new Vector3(r.x, 0f, r.y), new Vector3(r.x, -0.5f, r.y) + D * 0.3f, t);
                }
                lookAt = Vector3.Lerp(lookAt, rig.Face, 0.75f);
                // her petals settle round the edges of your eyes; her face stays in the middle
                rig.fx.dirt = s.lensPetals;
                rig.fx.dirtTint = Color.white;
                rig.fx.dirtAmount = 0.35f * In2(Seg(t, wakeLean + 0.3f, wakeCut));
            }
        }

        // her hair over your eyes (Aisle To Altar's strike)
        if (s.hair != null)
        {
            bool on = variant == Variant.AisleToAltar && t >= toAltarDark - 0.08f && t < toAltarWake;
            s.hair.gameObject.SetActive(on);
            if (on)
            {
                float z = 0.11f;
                float h = 2f * z * Mathf.Tan(rig.baseFov * 0.5f * Mathf.Deg2Rad);
                float drop = 1f - Out3(Seg(t, toAltarDark - 0.08f, toAltarDark + 0.04f));
                s.hair.localPosition = new Vector3(Mathf.Sin(t * 5.2f) * 0.012f, drop * h * 1.05f, z);
                s.hair.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.1f) * 2.5f);
                s.hair.localScale = new Vector3(h * 1.9f, h * 1.05f, 1f);
            }
        }

        FaceLight(rig, s, camPos, dist > 0f ? 1f : 0f);       // last: after anything that moved her face
        rig.SetCamera(camPos, lookAt, rig.baseFov, pitch, yaw, roll);
        Vector3 torchFrom = variant == Variant.Wake ? camPos - Vector3.up * 0.2f + across * -0.25f : camPos - Vector3.up * 0.5f + across * -0.3f;
        bool torchOn = variant == Variant.Aisle ? t < aisleCut - 0.03f : variant == Variant.AisleToAltar ? t < toAltarDark : t < wakeCut;
        Vector3 torchAt = dist <= 0f ? s.altar + D * 25f : arrived ? rig.Face : Vector3.Lerp(herPos + Vector3.up * 1.2f, rig.Face, Seg(-dist, -8f, -2f));
        rig.Torch(torchOn ? 1.7f : 0f, torchFrom, torchAt, 60f, 36f);
        float glitchUp = jump >= 0 && !arrived ? Mathf.Exp(-(t - aisleNotes[jump]) * 18f) : 0f;
        rig.SetVhs(0.25f + 0.45f * glitchUp + 0.4f * Seg(t, rightHead, rightHeadEnd + 0.3f), 0.04f + 0.12f * glitchUp);
        float darkAt = variant == Variant.Aisle ? aisleCut : variant == Variant.AisleToAltar ? toAltarDark + 0.05f : wakeCut;
        rig.fx.darken = Seg(t, darkAt - 0.01f, darkAt);      // a hard cut: her face is the last thing you see
        if (variant == Variant.AisleToAltar && t >= toAltarDark) rig.fx.darken = 1f;
    }

    // How far her head is tilted over (degrees, rolled about your line of sight) at each stage of the procession.
    private static float HeadTilt(int stage)
    {
        float[] tilt = { 0f, 22f, 34f, 48f, -58f, -64f, 70f, 74f };
        return stage < 0 ? 0f : tilt[Mathf.Min(stage, tilt.Length - 1)];
    }

    // The bride coming apart, a stage per note, each a new broken pose, as if she were thrown into it: her hands on her
    // bouquet; her left arm lets go and folds back on itself; she drops the bouquet; her arms hang, go out, fold, never
    // the same twice and never alike; she leans back and judders. Everything stays out at her sides, where you can
    // see it: her arms never come across her face or her body.
    private static readonly ArmShape[] LeftArms = { ArmShape.Held, ArmShape.Held, ArmShape.Fold, ArmShape.Fold, ArmShape.Hang, ArmShape.Cross, ArmShape.Cross, ArmShape.Fold };
    private static readonly ArmShape[] RightArms = { ArmShape.Held, ArmShape.Held, ArmShape.Held, ArmShape.Hang, ArmShape.Cross, ArmShape.Fold, ArmShape.Hang, ArmShape.Cross };

    private void Corrupt(KillSequenceRig rig, float t, int stage, Vector3 herF, float holdBouquet)
    {
        Vector3 R = Vector3.Cross(Vector3.up, herF);
        Spasm(rig, t, stage >= 6 ? 0.1f : 0f);      // the body judders; the arms, aimed after it, keep their shapes
        if (stage >= 5)
        {
            // her body leans back from the hips while her face stays on you
            rig.Bend(HumanBodyBones.Spine, R, -14f);
            rig.Bend(HumanBodyBones.Chest, R, -8f);
        }
        stage = Mathf.Clamp(stage, 0, LeftArms.Length - 1);
        Transform hips = rig.Bone(HumanBodyBones.Hips);
        if (holdBouquet > 0f && hips != null)
        {
            // her hands on the bouquet, in front of her
            Vector3 bunch = hips.position + herF * 0.32f + Vector3.up * 0.22f;
            if (LeftArms[stage] == ArmShape.Held)
                rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, bunch - R * 0.05f, holdBouquet, rig.Bone(HumanBodyBones.LeftUpperArm).position - R * 0.45f - herF * 0.2f - Vector3.up * 0.3f);
            if (RightArms[stage] == ArmShape.Held)
                rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, bunch + R * 0.05f, holdBouquet, rig.Bone(HumanBodyBones.RightUpperArm).position + R * 0.45f - herF * 0.2f - Vector3.up * 0.3f);
        }
        Arm(rig, -1f, herF, LeftArms[stage], t);
        Arm(rig, 1f, herF, RightArms[stage], t);
    }

    private enum ArmShape { Held, Hang, Fold, Cross }

    // One of her arms (side +1 her right, -1 her left) put into a broken shape, out at her side where it can be seen.
    private static void Arm(KillSequenceRig rig, float side, Vector3 herF, ArmShape shape, float t, float amount = 1f)
    {
        if (amount <= 0f || shape == ArmShape.Held) return;
        Vector3 R = Vector3.Cross(Vector3.up, herF) * side, U = Vector3.up;
        Vector3 upper, lower;
        float wrist;
        switch (shape)
        {
            case ArmShape.Hang:     // hanging straight, the hand bent out at the wrist
                upper = R * 0.2f - U + herF * 0.04f; lower = R * 0.08f - U + herF * 0.12f; wrist = 80f; break;
            case ArmShape.Fold:     // folded back on itself at the elbow: the forearm runs back up the outside of the arm
                upper = R * 0.35f - U + herF * 0.08f; lower = R * 0.4f + U * 0.92f + herF * 0.1f; wrist = 70f; break;
            default:                // out to the side, the forearm hanging from the elbow, the wrist bent back
                upper = R - U * 0.1f + herF * 0.06f; lower = R * 0.22f - U + herF * 0.12f; wrist = 75f; break;
        }
        float jit = Jitter(t, 12f, side > 0f ? 5f : 6f) * 0.05f;
        bool right = side > 0f;
        HumanBodyBones hand = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
        AimLimb(rig, right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm, right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            hand, (upper + U * jit).normalized, lower.normalized, amount);
        rig.Bend(hand, herF, wrist * side * amount);
    }

    // Her neck pays out like a rope and her face goes to faceTo on it, while the rest of her stays where it is; her
    // face stays on you, shaking. Call it last: anything that turns her spine or neck after it would swing her head.
    private static void NeckReach(KillSequenceRig rig, Vector3 faceTo, Vector3 cam, float t, float shake)
    {
        Transform head = rig.Bone(HumanBodyBones.Head), neck = rig.Bone(HumanBodyBones.Neck);
        if (head == null || neck == null) return;
        Vector3 off = faceTo - rig.Face;
        float len = off.magnitude;
        if (len < 1e-3f) return;
        Transform mid = head.parent != null && head.parent != neck ? head.parent : null;
        Vector3 h0 = head.position, n0 = neck.position, m0 = mid != null ? mid.position : h0;
        // it sags in the middle and swims a little, like something hanging off it
        Vector3 sag = (Vector3.down * 0.08f + Vector3.Cross(off / len, Vector3.up) * Mathf.Sin(t * 13f) * 0.04f) * len;
        neck.position = n0 + off * 0.1f;
        if (mid != null) mid.position = m0 + off * 0.5f + sag;
        head.position = h0 + off;
        head.rotation = Quaternion.FromToRotation(rig.FaceDirection, (cam - head.position).normalized) * head.rotation;
        if (shake > 0f) rig.HeadTurn(cam, Jitter(t, 40f, 7) * 10f * shake, Jitter(t, 40f, 8) * 14f * shake, Jitter(t, 40f, 9) * 6f * shake, 0f);
    }

    // A warm light on her face from your side, so her face reads from the far end of the aisle.
    private void FaceLight(KillSequenceRig rig, State s, Vector3 cam, float amount)
    {
        if (s.faceLight == null) return;
        Vector3 face = rig.Face;
        Vector3 toCam = cam - face;
        float dist = toCam.magnitude;
        s.faceLight.enabled = amount > 0.01f && faceLight > 0f;
        s.faceLight.transform.position = face + toCam / Mathf.Max(dist, 0.01f) * Mathf.Min(0.55f, dist * 0.5f) + Vector3.up * 0.08f;
        s.faceLight.intensity = faceLight * amount;
    }

    // Her bouquet: in her right hand until she drops it, then lying in the aisle where it fell.
    private void Bouquet(KillSequenceRig rig, State s, bool visible, bool dropped)
    {
        if (s.bouquet == null) return;
        if (!dropped)
        {
            s.bouquetDropped = false;
            Transform hand = rig.Bone(HumanBodyBones.RightHand);
            s.bouquet.gameObject.SetActive(visible && hand != null);
            if (hand != null) s.bouquet.SetPositionAndRotation(hand.position + Vector3.up * 0.04f, Quaternion.LookRotation(-s.aisle, Vector3.up));
            return;
        }
        if (!s.bouquetDropped)
        {
            s.bouquetDropped = true;
            Vector3 floor = Floor(rig, s.bouquet.position + Vector3.up * 0.5f);
            s.bouquet.SetPositionAndRotation(floor + Vector3.up * 0.06f, Quaternion.LookRotation(Vector3.up, -s.aisle) * Quaternion.Euler(0f, 0f, 35f));
        }
        s.bouquet.gameObject.SetActive(true);
    }

    private void MakeBouquet(KillSequenceRig rig, State s)
    {
        Shader lit = litShader != null ? litShader : Shader.Find("Standard");
        if (lit == null) return;
        var root = rig.Keep(new GameObject("BrideBouquet"));
        var rose = rig.Keep(new Material(lit) { color = new Color(0.55f, 0.02f, 0.05f) });
        var pale = rig.Keep(new Material(lit) { color = new Color(0.8f, 0.78f, 0.72f) });
        var stem = rig.Keep(new Material(lit) { color = new Color(0.08f, 0.14f, 0.05f) });
        for (int i = 0; i < 11; i++)
        {
            float a = i * 2.4f, r = i == 0 ? 0f : 0.05f + 0.03f * (i % 3);
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(b);
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.06f + 0.03f * Mathf.Cos(a * 1.7f), Mathf.Sin(a) * r);
            b.transform.localScale = Vector3.one * (i % 4 == 3 ? 0.05f : 0.075f);
            b.GetComponent<Renderer>().sharedMaterial = i % 4 == 3 ? pale : rose;
        }
        for (int i = 0; i < 5; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(c);
            c.transform.SetParent(root.transform, false);
            c.transform.localPosition = new Vector3((i - 2) * 0.012f, -0.12f, 0f);
            c.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 2) * 4f);
            c.transform.localScale = new Vector3(0.012f, 0.12f, 0.012f);
            c.GetComponent<Renderer>().sharedMaterial = stem;
        }
        for (int i = 0; i < 2; i++)
        {
            var ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(ribbon);
            ribbon.transform.SetParent(root.transform, false);
            ribbon.transform.localPosition = new Vector3(0.02f * (i * 2 - 1), -0.2f, 0.01f);
            ribbon.transform.localRotation = Quaternion.Euler(0f, 0f, 8f * (i * 2 - 1));
            ribbon.transform.localScale = new Vector3(0.025f, 0.28f, 0.004f);
            ribbon.GetComponent<Renderer>().sharedMaterial = pale;
        }
        root.SetActive(false);
        s.bouquet = root.transform;
    }

    // ------------------------------------------------------------------ First Dance

    // How far round the dance has turned (degrees) at t.
    private float DanceAngle(float t)
    {
        float w = 360f * danceTurnsPerBar / danceBar;
        if (t <= danceStart) return 0f;
        if (t < danceSlow) return w * (t - danceStart);
        float a = w * (danceSlow - danceStart);
        float T = danceStop - danceSlow;
        float u = Mathf.Min(t, danceStop) - danceSlow;
        return a + w * (u - u * u / (2f * T));
    }

    private void Dance(KillSequenceRig rig, State s, float t)
    {
        Vector3 C = s.danceCenter, F0 = s.danceForward;
        float bar = (t - danceStart) / danceBar;           // 0..4 while the box plays
        // in the second bar she moves in jerks, a few frames behind the turn
        float herT = bar >= 1f && bar < 2f ? danceStart + danceBar + Steps(t - danceStart - danceBar, 6f) : t;
        Vector3 dirCam = Quaternion.AngleAxis(DanceAngle(t), Vector3.up) * F0;     // from you toward her
        Vector3 dirHer = Quaternion.AngleAxis(DanceAngle(herT), Vector3.up) * F0;
        Vector3 camRight = Vector3.Cross(Vector3.up, dirCam);

        // you: on the circle, opposite her, pulled in on the grab
        Vector3 eye = rig.Eye();
        Vector3 cam = C - dirCam * danceRadius + Vector3.up * rig.eye;
        cam = Vector3.Lerp(eye, cam, Out3(Seg(t, 0f, danceStart)));
        float pitch = 0f, roll = -7f * Seg(t, danceStart, danceStart + 0.4f) * (1f - Seg(t, danceSlow, danceStop));
        float yaw = 0f;

        // her: across the circle, facing you, stepping
        Vector3 herPos = t < danceStart ? rig.At(Mathf.Lerp(startDistance, danceRadius + 0.35f, Out3(Seg(t, 0f, danceStart)))) : C + dirHer * 0.35f;
        rig.Place(Floor(rig, herPos + Vector3.up * 1.5f), -dirHer);
        float step = bar >= 1f && bar < 2f ? Steps(herT, 6f) : herT;
        rig.Pose(("stride", 0.4f + step * 1.15f, 0.7f), ("stand", 1f, 0.3f));
        if (t >= danceStart)
        {
            Vector3 f = rig.Face - cam; f.y = 0f;
            rig.killer.position += dirHer * (danceFace - Vector3.Dot(f, dirHer));
        }

        // bar 4: her body turns round on her legs until her back is to you
        float twist = InOut(Seg(t, danceSlow, danceSlow + 0.5f));
        if (twist > 0f)
        {
            rig.Bend(HumanBodyBones.Spine, Vector3.up, 80f * twist);
            rig.Bend(HumanBodyBones.Chest, Vector3.up, 100f * twist);
            // ...but her head stays turned to you, the neck wrung right round
            rig.Bend(HumanBodyBones.Head, Vector3.up, -170f * twist);
        }
        // she looks down at you; in bar 3 her head spins, once round and once over
        rig.Stare(cam, 0.9f * (1f - twist) + 0.5f * twist);
        float spinYaw = 360f * InOut(Seg(t, danceStart + 2f * danceBar + 0.15f, danceStart + 2f * danceBar + 0.32f));
        float spinRoll = 360f * InOut(Seg(t, danceStart + 2f * danceBar + 0.47f, danceStart + 2f * danceBar + 0.64f));
        if (spinYaw > 0f && spinYaw < 360f || spinRoll > 0f && spinRoll < 360f) rig.HeadTurn(cam, spinRoll % 360f, spinYaw % 360f, 0f, 0.15f);
        if (bar >= 2f) Vibrate(rig, cam, t, 0.35f + 0.4f * Seg(t, danceStop, danceDip));

        // her hands: one holds yours out to the side, one is on your shoulder; they keep hold through the twist
        float hold = Out3(Seg(t, 0.04f, danceStart));
        float letGo = 1f - Seg(t, danceLetGo, danceLetGo + 0.1f);
        Vector3 yourHand = cam - camRight * 0.48f + dirCam * 0.32f - Vector3.up * 0.38f;
        Vector3 yourShoulder = cam + camRight * 0.22f + dirCam * 0.12f - Vector3.up * 0.3f;
        if (t >= danceDip)
        {
            float dip = InOut(Seg(t, danceDip, danceLetGo));
            yourHand -= Vector3.up * 0.5f * dip; yourShoulder -= Vector3.up * 0.6f * dip;
        }
        rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, yourHand, hold * letGo, rig.Bone(HumanBodyBones.RightUpperArm).position - Vector3.up * 0.6f + camRight * -0.3f);
        rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, yourShoulder, hold * letGo, rig.Bone(HumanBodyBones.LeftUpperArm).position - Vector3.up * 0.7f + camRight * 0.3f);

        // the dip, and the fall when she lets go
        Vector3 look = Vector3.Lerp(rig.Face, Torso(rig).position + Vector3.up * 0.4f, twist);
        if (t >= danceDip)
        {
            float dip = InOut(Seg(t, danceDip, danceLetGo));
            float fall = In2(Seg(t, danceLetGo, danceCut));
            cam += -dirCam * (0.35f * dip + 0.5f * fall) - Vector3.up * (0.75f * dip + 1.6f * fall);
            rig.LeanTo(cam, 0.6f * dip);
            pitch = Mathf.Lerp(0f, -55f, dip) - 25f * fall;
            roll = Mathf.Lerp(roll, -14f, dip);
            rig.fx.lids = InOut(Seg(t, danceLetGo + 0.05f, danceCut - 0.05f));
        }
        for (int b = 0; b < 4; b++) pitch += Kick(t, danceStart + b * danceBar, 1.6f, 3f, 6f);
        if (t > danceStart && t < danceStop) cam += Vector3.up * 0.035f * Mathf.Cos(2f * Mathf.PI * 3f * (t - danceStart) / danceBar) * (1f - Seg(t, danceSlow, danceStop));
        pitch += Kick(t, 0.05f, -6f, 5f, 7f) + Noise(t * 5f, 1) * 0.8f;
        yaw += Kick(t, 0.05f, 5f, 4f, 6f);
        pitch += Kick(t, danceStop, 3f, 5f, 8f);
        FaceLight(rig, s, cam, 1f - Seg(t, danceLetGo, danceCut));
        rig.SetCamera(cam, look, rig.baseFov + 4f, pitch, yaw, roll);
        rig.Torch(1.5f * (1f - Seg(t, danceLetGo, danceCut)), cam - Vector3.up * 0.4f + camRight * 0.25f, Torso(rig).position + Vector3.up * 0.2f, 12f, 55f);

        // the candles round you light when she takes you, and go out when the box stops
        foreach (var c in s.candles) { c.litAt = 0.12f; if (t >= danceStop && c.outAt < 0f) c.outAt = danceStop + c.seed * 0.08f; LightCandle(c, t, cam); }
        float hit = Mathf.Exp(-Mathf.Max(0f, t - 0.05f) * 9f);
        rig.SetVhs(0.25f + 0.5f * hit + 0.25f * Seg(t, danceStart + 2f * danceBar, danceStop), 0.04f + 0.15f * hit);
        rig.fx.darken = Seg(t, danceCut - 0.15f, danceCut);
    }

    // ------------------------------------------------------------------ Threshold

    private const float CarryHeight = 1.9f;

    // Lying on your back on the altar, looking up, the door end of the church at the top of your eyes.
    private static Quaternion AltarView(State s) => Quaternion.LookRotation((Vector3.up + s.aisle * 0.5f).normalized, s.aisle);

    // Laid to Rest: the lunge and her hair over your eyes; carried in the dark (glimpses of the vaults through her
    // hair); you wake on the altar; her roses fall on you; she steps back to the foot of the altar and comes apart
    // while the petals close over your eyes.
    private void Threshold(KillSequenceRig rig, State s, float t)
    {
        if (t >= wake)
        {
            AltarScene(rig, s, t, LaidTimes);
            return;
        }
        Vector3 D = s.aisle, across = Vector3.Cross(Vector3.up, D), travel = -D, F = rig.forward;
        Vector3 cam = rig.Eye();
        Quaternion rot = Quaternion.identity;
        float dark = 0f;

        if (t < wake)
        {
            if (t < scoop)
            {
                // she comes at you, face first, and her hair whips over your eyes
                Vector3 eye = rig.Eye();
                float d = Mathf.Lerp(startDistance, 0.55f, Out3(Seg(t, 0f, scoop - 0.08f)));
                rig.Place(rig.At(d), -F);
                float stopped = Seg(t, 0f, 0.12f);
                rig.Pose(("stride", 0.4f + t * 2f, 1f - stopped), ("stand", 1f, stopped));
                rig.PlaceFace(eye, d);
                rig.Stare(eye, 1f);
                cam = eye + Vector3.up * 0.12f * Out3(Seg(t, 0.18f, scoop));
                rot = Quaternion.LookRotation(rig.Face - Vector3.up * 0.05f - cam, Vector3.up) * Quaternion.Euler(Kick(t, 0.2f, -9f, 5f, 7f), 0f, Kick(t, 0.2f, 10f, 4f, 6f));
                Show(s, true);
                dark = In2(Seg(t, 0.26f, scoop)) * 0.9f;
            }
            else
            {
                // carried, face up, in the dark; three times your eyes open on the vaults going by
                float u = InOut(Seg(t, scoop, wake));
                float along = Mathf.Lerp(s.carryFrom, s.carryTo, u);
                float sideOff = Mathf.Lerp(s.carrySide, 0f, u);
                Vector3 g = Floor(rig, s.altar + D * along + across * sideOff + Vector3.up * 1.5f);
                float bob = Mathf.Sin(t * Mathf.PI * 2f / 0.36f) * 0.04f;
                cam = g + Vector3.up * (CarryHeight + bob);
                rot = Quaternion.LookRotation((Vector3.up - travel * 0.15f).normalized, -travel) * Quaternion.Euler(Noise(t * 2f, 3) * 6f, Noise(t * 1.6f, 4) * 6f, 14f + Noise(t * 1.3f, 5) * 8f);
                Show(s, false);
                float open = 0f;
                foreach (float gl in glimpses) open = Mathf.Max(open, Seg(t, gl, gl + 0.03f) * (1f - Seg(t, gl + glimpseLength - 0.03f, gl + glimpseLength)));
                dark = Mathf.Lerp(0.94f, 0.05f, open);
                rig.fx.lids = Mathf.Lerp(0.75f, 0.12f, open);
            }
        }

        // her hair over your eyes while she carries you
        if (s.hair != null)
        {
            bool on = t >= 0.2f && t < wake;
            s.hair.gameObject.SetActive(on);
            if (on)
            {
                float z = 0.11f;
                float h = 2f * z * Mathf.Tan(rig.baseFov * 0.5f * Mathf.Deg2Rad);
                float drop = 1f - Out3(Seg(t, 0.2f, scoop + 0.05f));
                float sway = Mathf.Sin(t * 5.2f) * 0.012f + Kick(t, scoop, 0.04f, 3f, 5f);
                s.hair.localPosition = new Vector3(sway, drop * h * 1.05f, z);
                s.hair.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.1f) * 2.5f);
                s.hair.localScale = new Vector3(h * 1.9f, h * 1.05f, 1f);
            }
        }

        rig.cam.SetPositionAndRotation(cam, rot);
        rig.camera.fieldOfView = rig.baseFov;
        rig.Torch(0f, cam, cam + rig.forward);
        foreach (var c in s.candles) { c.litAt = candlesAt + c.seed * 0.08f; LightCandle(c, t, cam); }

        // the petals settle on your eyes, from the edges in, until there is nothing else
        if (t >= petalsFrom)
        {
            rig.fx.dirt = s.lensPetals;
            rig.fx.dirtTint = Color.white;
            rig.fx.dirtAmount = Mathf.Lerp(0f, 0.22f, Seg(t, petalsFrom + 0.2f, stepBack)) + 0.78f * In2(Seg(t, stepBackEnd, thresholdCut));
        }
        float hit = Mathf.Exp(-Mathf.Max(0f, t - 0.2f) * 8f);
        rig.SetVhs(0.25f + 0.5f * hit + 0.35f * Seg(t, turnFrom, thresholdCut), 0.04f + 0.18f * hit + 0.1f * Seg(t, turnFrom, thresholdCut));
        rig.fx.darken = Mathf.Max(dark, Seg(t, thresholdCut - 0.08f, thresholdCut));
    }

    // ------------------------------------------------------------------ on the altar (Laid to Rest, Aisle To Altar)

    private struct AltarTimes { public float wake, candles, petalsFrom, petalsTo, stepBack, stepBackEnd, turnFrom, turnTo, stretchFrom, cut; }

    private AltarTimes LaidTimes => new AltarTimes
    {
        wake = wake, candles = candlesAt, petalsFrom = petalsFrom, petalsTo = petalsTo, stepBack = stepBack,
        stepBackEnd = stepBackEnd, turnFrom = turnFrom, turnTo = turnTo, stretchFrom = stretchFrom, cut = thresholdCut,
    };

    private AltarTimes ToAltarTimes => new AltarTimes
    {
        wake = toAltarWake, candles = toAltarWake + 0.05f, petalsFrom = toAltarWake + 0.3f, petalsTo = toAltarWake + 1.05f,
        stepBack = toAltarWake + 1.05f, stepBackEnd = toAltarWake + 1.3f, turnFrom = toAltarWake + 1.35f, turnTo = toAltarWake + 2.05f,
        stretchFrom = toAltarWake + 1.4f, cut = toAltarCut,
    };

    // On the altar: your eyes open on the vault, then on her at your feet. She bows over you and her crown sheds its
    // petals on you; she steps back to the foot of the altar and comes apart there, out at her sides where you can see
    // it; the petals gather at the edges of your eyes and close over them at the very end.
    private void AltarScene(KillSequenceRig rig, State s, float t, AltarTimes a)
    {
        Vector3 D = s.aisle, across = Vector3.Cross(Vector3.up, D), travel = -D;
        Vector3 cam = s.altarTop + Vector3.up * 0.32f;
        float waking = Seg(t, a.wake, a.wake + 0.35f);
        float dark = 0.94f * (1f - waking);
        rig.fx.lids = 0.7f * (1f - InOut(waking));
        float back = Steps(InOut(Seg(t, a.stepBack, a.stepBackEnd)), 8f);
        Vector3 stand = Floor(rig, s.altarTop + D * (s.altarHalfDepth + Mathf.Lerp(0.5f, 1.45f, back)) + Vector3.up * 1.5f);
        rig.Place(stand, travel);
        rig.Pose(("stand", 1.2f + Steps(t, 5f) * 0.25f, 1f));
        float bow = InOut(Seg(t, a.wake + 0.2f, a.petalsFrom)) * (1f - Seg(t, a.petalsTo, a.stepBack));
        rig.LeanTo(cam, 0.2f + 0.45f * bow);
        float apart = Seg(t, a.stretchFrom, a.cut);
        if (apart <= 0f)
        {
            // her arms hang, the hands bent out at the wrists
            Arm(rig, 1f, travel, ArmShape.Hang, t);
            Arm(rig, -1f, travel, ArmShape.Hang, t);
        }
        if (apart > 0f)
        {
            // her arms go long and break out to the sides, her body leans back; nothing crosses her face
            float k = Steps(apart, 7f);
            rig.Stretch(1f + 0.2f * k, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            Arm(rig, 1f, travel, ArmShape.Cross, t, Mathf.Clamp01(k * 1.4f));
            Arm(rig, -1f, travel, ArmShape.Cross, t, Mathf.Clamp01(k * 1.4f));
            rig.Bend(HumanBodyBones.Spine, Vector3.Cross(Vector3.up, travel), -14f * k);
        }
        rig.Stare(cam, 0.9f);
        float turn = 180f * Steps(InOut(Seg(t, a.turnFrom, a.turnTo)), 9f);
        if (turn > 0f) rig.HeadTurn(cam, turn, 0f, 0f, 0.25f);
        if (t >= a.turnFrom) Vibrate(rig, cam, t, 0.25f);
        Show(s, true);
        FaceLight(rig, s, cam, 1f);
        if (t >= a.petalsFrom && t < a.petalsTo && t - s.lastPetalSpawn > 0.06f)
        {
            // her crown sheds: petals drift down off it toward you
            s.lastPetalSpawn = t;
            Vector2 r = Random2(t * 1.3f) * 0.18f;
            Spawn(s, rig, rig.Face + Vector3.up * 0.18f + new Vector3(r.x, 0f, r.y), (cam - rig.Face).normalized * 0.35f + new Vector3(r.x, -0.25f, r.y), t);
        }

        // your eyes open on the vault; then on her, at your feet
        Quaternion up = AltarView(s);
        Quaternion atHer = Quaternion.LookRotation(Vector3.Lerp(rig.Face, Torso(rig).position, 0.35f) - cam, Vector3.up);
        Quaternion rot = Quaternion.Slerp(up, atHer, InOut(Seg(t, a.wake + 0.3f, a.wake + 0.7f)));
        rot *= Quaternion.Euler(Noise(t * 0.8f, 2) * 1.2f, Noise(t * 0.7f, 3) * 1.2f, 0f);
        rig.cam.SetPositionAndRotation(cam, rot);
        rig.camera.fieldOfView = rig.baseFov - 12f * InOut(Seg(t, a.stepBack, a.cut));
        rig.Torch(0f, cam, cam + rig.forward);
        foreach (var c in s.candles) if (c.group == 1) { c.litAt = a.candles + c.seed * 0.08f; LightCandle(c, t, cam); }
        if (s.hair != null) s.hair.gameObject.SetActive(false);

        // the petals settle on your eyes, from the edges in, until there is nothing else
        if (t >= a.petalsFrom)
        {
            rig.fx.dirt = s.lensPetals;
            rig.fx.dirtTint = Color.white;
            rig.fx.dirtAmount = Mathf.Lerp(0f, 0.22f, Seg(t, a.petalsFrom + 0.2f, a.stepBack)) + 0.1f * Seg(t, a.stepBackEnd, a.cut - 0.25f)
                              + 0.68f * In2(Seg(t, a.cut - 0.25f, a.cut));
        }
        rig.SetVhs(0.25f + 0.35f * Seg(t, a.turnFrom, a.cut), 0.04f + 0.1f * Seg(t, a.turnFrom, a.cut));
        rig.fx.darken = Mathf.Max(dark, Seg(t, a.cut - 0.08f, a.cut));
    }

    // ------------------------------------------------------------------ props

    private Candle MakeCandle(KillSequenceRig rig, State s, Vector3 floor, float standHeight, bool withLight, int group)
    {
        var c = new Candle { seed = Hash(floor.x * 3.1f + floor.z * 1.7f), group = group };
        var root = rig.Keep(new GameObject("BrideCandle"));
        root.transform.position = floor;
        Shader lit = litShader != null ? litShader : Shader.Find("Standard");
        if (standHeight > 0f)
        {
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(stand);
            stand.transform.SetParent(root.transform, false);
            stand.transform.localPosition = Vector3.up * standHeight * 0.5f;
            stand.transform.localScale = new Vector3(0.07f, standHeight * 0.5f, 0.07f);
            if (lit != null) stand.GetComponent<Renderer>().sharedMaterial = rig.Keep(new Material(lit) { color = new Color(0.06f, 0.05f, 0.05f) });
        }
        var wax = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyCollider(wax);
        wax.transform.SetParent(root.transform, false);
        wax.transform.localPosition = Vector3.up * (standHeight + 0.13f);
        wax.transform.localScale = new Vector3(0.07f, 0.13f, 0.07f);
        if (lit != null) wax.GetComponent<Renderer>().sharedMaterial = rig.Keep(new Material(lit) { color = new Color(0.85f, 0.8f, 0.7f) });
        var flame = new GameObject("Flame", typeof(MeshFilter), typeof(MeshRenderer));
        flame.transform.SetParent(root.transform, false);
        flame.transform.localPosition = Vector3.up * (standHeight + 0.33f);
        flame.GetComponent<MeshFilter>().sharedMesh = s.quad;
        if (s.flameMat != null) flame.GetComponent<MeshRenderer>().sharedMaterial = s.flameMat;
        c.flame = flame.transform;
        c.root = root.transform;
        // a soft glow round the flame, so a candle still reads from the far end of the church
        if (s.flameMat != null)
        {
            if (s.haloMat == null)
            {
                s.haloMat = rig.Keep(new Material(s.flameMat.shader) { mainTexture = rig.Keep(GlowTexture()) });
                s.haloMat.SetColor("_TintColor", new Color(1f, 0.55f, 0.25f, 0.22f));
            }
            var halo = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
            halo.transform.SetParent(flame.transform, false);
            halo.transform.localPosition = new Vector3(0f, -0.1f, 0.01f);
            halo.transform.localScale = new Vector3(9f, 3.6f, 1f);
            halo.GetComponent<MeshFilter>().sharedMesh = s.quad;
            halo.GetComponent<MeshRenderer>().sharedMaterial = s.haloMat;
        }
        if (withLight)
        {
            var lg = new GameObject("CandleLight", typeof(Light));
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = Vector3.up * (standHeight + 0.45f);
            c.light = lg.GetComponent<Light>();
            c.light.type = LightType.Point;
            c.light.color = new Color(1f, 0.62f, 0.32f);
            c.light.range = standHeight > 0f ? 7.5f : 5.5f;
            c.light.shadows = LightShadows.None;
            c.light.renderMode = LightRenderMode.ForcePixel;   // or the ambient red swallows them
            c.light.enabled = false;
        }
        s.candles.Add(c);
        return c;
    }

    private void MakeHair(KillSequenceRig rig, State s, Shader clear)
    {
        if (clear == null) return;
        var hair = new GameObject("BrideHairOverYourEyes", typeof(MeshFilter), typeof(MeshRenderer));
        rig.Keep(hair);
        hair.GetComponent<MeshFilter>().sharedMesh = s.quad;
        hair.GetComponent<MeshRenderer>().sharedMaterial = rig.Keep(new Material(clear) { mainTexture = rig.Keep(HairTexture()) });
        hair.transform.SetParent(rig.cam, false);
        hair.SetActive(false);
        s.hair = hair.transform;
    }

    private void LightCandle(Candle c, float t, Vector3 cam)
    {
        float on = c.litAt >= 0f && t >= c.litAt ? 1f : 0f;
        if (c.outAt >= 0f && t >= c.outAt) on = Mathf.Max(0f, 1f - (t - c.outAt) / 0.07f) * (Spike(t, 40f, 0.5f, c.seed) ? 1f : 0.3f);
        float flare = c.litAt >= 0f ? 1f + 1.4f * Mathf.Exp(-Mathf.Max(0f, t - c.litAt) * 14f) : 1f;
        SetCandle(c, on * flare, t);
        if (c.flame != null && on > 0f)
        {
            Vector3 to = cam - c.flame.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) c.flame.rotation = Quaternion.LookRotation(-to.normalized);
        }
    }

    private static void SetCandle(Candle c, float amount, float t)
    {
        float flick = 0.82f + 0.18f * Mathf.PerlinNoise(t * 11f, c.seed * 9f);
        if (c.light != null)
        {
            c.light.enabled = amount > 0.01f;
            c.light.intensity = CandleLight * amount * flick;
        }
        if (c.flame != null)
        {
            c.flame.gameObject.SetActive(amount > 0.01f);
            float k = Mathf.Clamp(amount, 0f, 2f);
            c.flame.localScale = new Vector3(0.1f, 0.26f * (0.8f + 0.2f * flick), 1f) * Mathf.Lerp(0.6f, 1.1f, Mathf.Clamp01(k));
        }
    }

    private void Burst(KillSequenceRig rig, State s, Vector3 from, Vector3 toward, float t, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Hash(i * 1.37f + 2f) * Mathf.PI * 2f, b = Hash(i * 2.11f + 5f);
            Vector3 dir = (toward * (0.9f + b) + new Vector3(Mathf.Cos(a), Hash(i * 0.73f) * 1.2f - 0.2f, Mathf.Sin(a)) * 1.1f).normalized;
            Vector2 r = Random2(i * 3.3f) * 0.1f;
            Spawn(s, rig, from + new Vector3(r.x, 0f, r.y), dir * (2.2f + 2.5f * Hash(i * 4.4f)), t);
        }
    }

    private static Vector2 Random2(float x) => new Vector2(Hash(x * 1.9f + 0.3f) - 0.5f, Hash(x * 2.7f + 0.9f) - 0.5f) * 2f;

    private void Spawn(State s, KillSequenceRig rig, Vector3 at, Vector3 v, float t)
    {
        if (s.petalMat == null) return;
        var go = new GameObject("BridePetal", typeof(MeshFilter), typeof(MeshRenderer));
        rig.Keep(go);
        go.GetComponent<MeshFilter>().sharedMesh = s.quad;
        go.GetComponent<MeshRenderer>().sharedMaterial = s.petalMat;
        float h = Hash(t * 31f + s.petals.Count * 0.7f);
        go.transform.localScale = Vector3.one * (0.028f + 0.018f * h);
        s.petals.Add(new Petal
        {
            t = go.transform, p = at, v = v, born = t, spin = 200f + 500f * h,
            axis = new Vector3(Hash(h * 9f) - 0.5f, Hash(h * 5f) - 0.5f, Hash(h * 3f) - 0.5f).normalized,
        });
    }

    private void UpdatePetals(KillSequenceRig rig, State s, float t)
    {
        foreach (var p in s.petals)
        {
            float a = Mathf.Max(0f, t - p.born);
            // thrown, then fluttering down: drag on the throw, a slow fall, a side-to-side drift
            Vector3 v0 = p.v;
            float drag = 3.2f;
            Vector3 pos = p.p + v0 * (1f - Mathf.Exp(-drag * a)) / drag;
            pos += Vector3.down * 0.55f * a + new Vector3(Mathf.Sin(a * 4f + p.spin), 0f, Mathf.Cos(a * 3.3f + p.spin)) * 0.06f * a;
            p.t.position = pos;
            p.t.rotation = Quaternion.AngleAxis(p.spin * a, p.axis);
            p.t.gameObject.SetActive((pos - rig.cam.position).sqrMagnitude > 0.35f * 0.35f);
        }
    }

    // ------------------------------------------------------------------ the church

    private static Transform Marker(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        GameObject go = GameObject.Find(name);
        if (go != null) return go.transform;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    // The highest walkable surface under p (searching downward from p), ignoring the killer and the player.
    private static Vector3 Floor(KillSequenceRig rig, Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(rig.killer)) continue;
            if (rig.player != null && h.collider.transform.IsChildOf(rig.player)) continue;
            string n = h.collider.name.ToLowerInvariant();
            if (n.Contains("donotpass") || n.Contains("barrier") || n.Contains("invisible")) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? new Vector3(p.x, rig.ground.y, p.z) : new Vector3(p.x, best, p.z);
    }

    // The altar: the raised block behind where she waited, found by stepping toward the apse.
    private static void FindAltar(KillSequenceRig rig, State s)
    {
        float first = -1f, last = -1f, top = float.NegativeInfinity;
        for (float x = 1f; x <= 10f; x += 0.2f)
        {
            Vector3 p = s.altar - s.aisle * x;
            float h = Floor(rig, p + Vector3.up * 4f).y - s.altar.y;
            if (h > 0.6f && h < 3f) { if (first < 0f) first = x; last = x; top = Mathf.Max(top, h); }
            else if (first >= 0f) break;
        }
        if (first < 0f) { s.altarTop = s.altar - s.aisle * 5f + Vector3.up * 1.1f; return; }
        float mid = (first + last) * 0.5f;
        s.altarHalfDepth = (last - first) * 0.5f + 0.1f;
        s.altarTop = s.altar - s.aisle * mid + Vector3.up * top;
    }

    private static void Show(State s, bool on)
    {
        foreach (Renderer r in s.shown) if (r != null) r.enabled = on;
    }

    // Slides her along dir so her face ends up distance m from cam (rig.PlaceFace works along the kill's own forward).
    private static void FaceAlong(KillSequenceRig rig, Vector3 cam, Vector3 dir, float distance)
    {
        Vector3 f = rig.Face - cam;
        f.y = 0f;
        rig.killer.position += dir * (distance - Vector3.Dot(f, dir));
    }

    private static Transform Torso(KillSequenceRig rig)
    {
        Transform c = rig.Bone(HumanBodyBones.Chest);
        return c != null ? c : rig.Bone(HumanBodyBones.Spine);
    }

    // ------------------------------------------------------------------ textures (made here, so there are no assets to lose)

    private static Texture2D FlameTexture()
    {
        var tex = new Texture2D(32, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                float u = (x - 15.5f) / 15.5f, v = y / 63f;
                float width = Mathf.Sin(Mathf.Clamp01(v * 1.15f) * Mathf.PI) * (1f - v * 0.55f);
                float r = Mathf.Abs(u) / Mathf.Max(0.05f, width);
                float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1.2f - v);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, Mathf.Lerp(0.95f, 0.55f, v), Mathf.Lerp(0.8f, 0.2f, v), a));
            }
        tex.Apply();
        return tex;
    }

    private static Texture2D GlowTexture()
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                float a = Mathf.Clamp01(1f - r);
                tex.SetPixel(x, y, new Color(1f, 0.85f, 0.6f, a * a * a));
            }
        tex.Apply();
        return tex;
    }

    private static Texture2D PetalTexture()
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x - 31.5f) / 31.5f, v = (y - 31.5f) / 31.5f;
                // a petal: round at one end, coming to a point at the other
                float w = Mathf.Lerp(0.95f, 0.15f, (v + 1f) * 0.5f);
                float r = new Vector2(u / Mathf.Max(0.1f, w), v * 0.92f).magnitude;
                float a = Mathf.Clamp01((1f - r) * 6f);
                float vein = 1f - 0.25f * Mathf.Exp(-u * u * 90f);
                float shade = Mathf.Lerp(0.55f, 0.85f, (1f - r)) * vein;
                tex.SetPixel(x, y, new Color(0.75f * shade + 0.25f, 0.08f + 0.1f * shade, 0.16f + 0.08f * shade, a));
            }
        tex.Apply();
        return tex;
    }

    // Lens petals for the screen effect's dirt: rgb = colour, a = when it lands (0 first); the gaps land last.
    private static Texture2D LensPetalTexture()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.09f, 0.0f, 0.01f, 1f);
        int count = 420;
        for (int k = 0; k < count; k++)
        {
            float cx = Hash(k * 1.3f + 0.1f) * N, cy = Hash(k * 2.9f + 0.7f) * N;
            float edge01 = Mathf.Clamp01(new Vector2(cx / N - 0.5f, cy / N - 0.5f).magnitude * 1.6f);
            float order = Mathf.Clamp01((1f - edge01) * 0.8f + Hash(k * 11.3f) * 0.15f) * 0.95f;
            float len = 9f + 13f * Hash(k * 5.1f), wid = len * (0.62f + 0.25f * Hash(k * 7.7f));
            float ang = Hash(k * 3.7f) * Mathf.PI * 2f;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float shade = 0.7f + 0.3f * Hash(k * 9.3f);
            int r = Mathf.CeilToInt(len);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = (int)cx + dx, y = (int)cy + dy;
                    if (x < 0 || y < 0 || x >= N || y >= N) continue;
                    float lx = (dx * ca + dy * sa) / len, ly = (-dx * sa + dy * ca) / wid;
                    float taper = Mathf.Lerp(1f, 0.35f, (lx + 1f) * 0.5f);
                    float rr = new Vector2(lx, ly / taper).magnitude;
                    if (rr > 1f) continue;
                    var c = px[y * N + x];
                    if (c.a <= order) continue;   // an earlier petal is already here
                    float edge = Mathf.Clamp01((1f - rr) * 4f);
                    float vein = 1f - 0.35f * Mathf.Exp(-ly * ly * 40f) * Mathf.Clamp01(1f - lx);
                    float rim = Mathf.Lerp(1.25f, 0.85f, edge);
                    px[y * N + x] = new Color(Mathf.Clamp01((0.38f + 0.42f * edge) * shade * vein * rim), 0.02f * shade, 0.05f * shade, order);
                }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Her hair, hanging over your eyes from above: dark strands with gaps between them.
    private static Texture2D HairTexture()
    {
        const int W = 256, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var a = new float[W * H];
        for (int k = 0; k < 170; k++)
        {
            float x0 = Hash(k * 1.7f + 0.2f) * (W + 40) - 20, len = Mathf.Lerp(0.45f, 1.05f, Hash(k * 2.3f)) * H;
            float amp = 3f + 9f * Hash(k * 3.1f), freq = 0.012f + 0.02f * Hash(k * 4.7f), ph = Hash(k * 5.9f) * 6.28f;
            float drift = (Hash(k * 6.1f) - 0.5f) * 30f, thick = 0.6f + 1.6f * Hash(k * 7.3f);
            float strength = 0.55f + 0.45f * Hash(k * 8.9f);
            for (int yy = 0; yy < len; yy++)
            {
                int y = H - 1 - yy;
                if (y < 0) break;
                float x = x0 + Mathf.Sin(yy * freq + ph) * amp + drift * yy / H;
                float fade = 1f - Mathf.Pow(yy / len, 3f);
                for (int dx = -2; dx <= 2; dx++)
                {
                    int xi = Mathf.RoundToInt(x) + dx;
                    if (xi < 0 || xi >= W) continue;
                    float cov = Mathf.Clamp01(thick - Mathf.Abs(xi - x));
                    a[y * W + xi] = Mathf.Max(a[y * W + xi], cov * fade * strength);
                }
            }
        }
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++)
        {
            int y = i / W;
            float top = Mathf.Clamp01((y - H * 0.86f) / (H * 0.1f));   // the mass of it at the top edge
            px[i] = new Color(0.02f, 0.016f, 0.02f, Mathf.Max(a[i], top * 0.92f));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
    }

    private static void DestroyNow(GameObject go)
    {
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }
}
