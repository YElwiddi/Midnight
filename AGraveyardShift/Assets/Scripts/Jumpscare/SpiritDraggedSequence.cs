using UnityEngine;

/// <summary>
/// The Spirit's kill (ending I), "Out of the Light". She drops to all fours and scuttles in under your torch,
/// and is gone; then something takes you by the collar from behind and pulls you off your feet. The torch
/// skids away down the path and lies there pointing at where you were. You are dragged backwards, head first,
/// watching the light get smaller. Crane back and she is above you, upside down, looking down at you.
/// Then one of four endings (<see cref="ending"/>).
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Spirit Out Of The Light", fileName = "SpiritOutOfTheLight")]
public class SpiritDraggedSequence : KillSequence
{
    public enum Ending
    {
        [Tooltip("The torch dies and in the dark her face comes down into yours.")]
        FaceInTheDark,
        [Tooltip("She lets go. Far off, she is standing in your torchlight, twisting into shapes, then she comes for you on all fours and blots out the light.")]
        Eclipse,
        [Tooltip("She lets go and is gone, then she is standing over you, and with every flicker of the torch she has bent further back over you.")]
        Bridge,
        [Tooltip("She takes your head in her hands and turns it until it breaks. You see the torch go out sideways, from the ground.")]
        Neck,
    }

    public enum EclipseFinale
    {
        [Tooltip("She drops onto all fours and scuttles at you, blotting out the light.")]
        Crawl,
        [Tooltip("She stays in the light; one arm stretches along the ground at you and the hand covers your eyes.")]
        LongArm,
        [Tooltip("She grows, jerk by jerk, into a giant with her arms spread, then folds over you and blots out the light.")]
        Unfold,
        [Tooltip("She turns her back, walks to your torch, picks it up, turns it on you until it whites out, and clicks it off.")]
        TakeTheLight,
    }

    [Header("Ending")]
    public Ending ending = Ending.Eclipse;
    [Tooltip("How Eclipse ends, after her shapes.")]
    public EclipseFinale eclipseFinale = EclipseFinale.Unfold;

    [Header("Animation")]
    [Tooltip("Run_final_catch: low, on all fours")]
    public AnimationClip scuttle;
    [Tooltip("Stumble: bent over, the long arms reaching")]
    public AnimationClip reach;
    [Tooltip("SpiritIdle: standing")]
    public AnimationClip idle;
    [Tooltip("SpiritWalk")]
    public AnimationClip walk;

    [Header("The Grab")]
    [Tooltip("Where she is (m) when the kill starts. Keep it equal to the event's killStopDistance.")]
    public float startDistance = 4f;
    [Tooltip("She drops low and comes in under the torch.")]
    public float scuttleAt = 0.06f;
    [Tooltip("The torch stutters and she is gone.")]
    public float vanish = 0.3f;
    [Tooltip("Taken by the collar from behind.")]
    public float yank = 0.36f;
    [Tooltip("Your back hits the ground.")]
    public float impact = 0.56f;
    [Tooltip("Where the torch lands, along the path / to the side (m from your feet).")]
    public Vector2 torchLands = new Vector2(2.1f, 0.35f);
    public float torchFlight = 0.42f;

    [Header("Dragged")]
    [Tooltip("When each pull starts (s). Every ending but Face In The Dark stops after the fourth.")]
    public float[] pulls = { 1.35f, 1.8f, 2.25f, 2.7f, 3.1f };
    [Tooltip("How far each pull drags you (m).")]
    public float pullLength = 1.6f;
    public float pullTime = 0.28f;
    [Tooltip("You crane back to see who has you...")]
    public float craneBack = 2.0f;
    [Tooltip("...and look at the light again.")]
    public float craneReturn = 2.55f;
    [Tooltip("She lets go of your collar (all endings but Face In The Dark).")]
    public float release = 2.98f;

    [Header("Ending: Face In The Dark")]
    public float torchDies = 3.3f;
    [Tooltip("Her face comes down over yours.")]
    public float face = 3.72f;
    [Tooltip("Her face's distance from the camera (m).")]
    public float faceDistance = 0.28f;
    public float cut = 4.45f;

    [Header("Ending: Eclipse")]
    [Tooltip("She is standing in the torchlight, far off.")]
    public float eclipseAppear = 3.35f;
    [Tooltip("How far from you she stands (m).")]
    public float eclipseDistance = 3.1f;
    [Tooltip("Each new shape she twists into lasts this long (s).")]
    public float eclipseStep = 0.13f;
    [Tooltip("She drops to all fours and comes for you.")]
    public float eclipseRush = 4.15f;
    [Tooltip("She blots out the light: black.")]
    public float eclipseCut = 4.6f;
    [Tooltip("The finale starts (Long Arm, Unfold, Over You): her shapes stop.")]
    public float finaleAt = 4.0f;
    [Tooltip("Long Arm: her hand drops to the ground / reaches you / covers your eyes.")]
    public float armDrop = 4.12f, armArrive = 4.62f, armHit = 4.72f;
    [Tooltip("Unfold: she grows (until) / folds over you / black.")]
    public float unfoldGrown = 4.6f, unfoldOver = 4.92f;
    [Tooltip("Take The Light: she reaches the torch / has it / turns it on you / it whites out / it clicks off.")]
    public float takeReach = 4.48f, takeHold = 4.62f, takeAim = 4.82f, takeGlare = 4.95f, takeOff = 5.3f;

    [Header("Ending: Bridge")]
    [Tooltip("You crane back: she is gone.")]
    public float bridgeLookBack = 3.0f;
    [Tooltip("You look down at the light again: she is standing over you.")]
    public float bridgeLookDown = 3.25f;
    [Tooltip("The torch flickers; each time it comes back she is bent further back over you.")]
    public float[] bridgeSnaps = { 3.6f, 3.78f, 3.96f, 4.14f };
    [Tooltip("How far past your head she stands (m): at your feet, facing the light.")]
    public float bridgeStand = 1.8f;
    [Tooltip("How far back she is bent at each flicker (degrees).")]
    public float[] bridgeBends = { 0f, 40f, 75f, 100f, 118f };
    public float bridgeDark = 4.3f;
    public float bridgeCut = 4.45f;

    [Header("Ending: Neck")]
    [Tooltip("You crane back: she is bent over your face.")]
    public float neckLookUp = 3.0f;
    [Tooltip("Her hands are on your head.")]
    public float neckHands = 3.25f;
    [Tooltip("Each turn of your head, and how far it has turned (degrees).")]
    public float[] neckTwists = { 3.35f, 3.5f, 3.65f };
    public float[] neckTwistAngles = { 70f, 145f, 205f };
    [Tooltip("She lets go and your head drops.")]
    public float neckLetGo = 3.98f;
    public float neckTorchOff = 4.5f;
    public float neckCut = 4.75f;

    [Tooltip("Black before the ending screen.")]
    public float blackHold = 0.3f;

    [Header("Props")]
    [Tooltip("Optional: the torch's body when it lies on the ground (a small cylinder is made if empty).")]
    public Mesh torchMesh;
    public Material torchMaterial;
    [Tooltip("Standard, Unlit/Texture and Legacy Shaders/Particles/Additive, for the dropped torch's body, lens and glare. " +
             "Referenced here so builds include them (found by name when empty, which only works in the editor).")]
    public Shader litShader, unlitShader, additiveShader;

    private const float LieHeight = 0.32f, FallBack = 1.45f, LookUp = -9f;

    private class State
    {
        public Transform torchProp, glare;
        public Material glareMat;
        public Light ghost;
        public LightShadows torchShadows;
        public float baseScale;
        public Vector3 elbowRest, handRest;   // Long Arm moves these bones; they are put back every frame
        public float fallBack = FallBack, dragScale = 1f;   // less when there is something behind you
    }

    private float Cut => ending == Ending.Eclipse ? EclipseEnd : ending == Ending.Bridge ? bridgeCut : ending == Ending.Neck ? neckCut : cut;

    private float EclipseEnd => eclipseFinale == EclipseFinale.LongArm ? armHit + 0.06f
        : eclipseFinale == EclipseFinale.Unfold ? unfoldOver + 0.03f
        : eclipseFinale == EclipseFinale.TakeTheLight ? takeOff + 0.04f : eclipseCut;

    public override float Duration => Cut + blackHold;

    public override void Begin(KillSequenceRig rig)
    {
        rig.UseClips(eyeUp, eyeForward, ("scuttle", scuttle), ("reach", reach), ("idle", idle != null ? idle : reach), ("walk", walk != null ? walk : reach));
        var s = new State();
        rig.state = s;
        s.baseScale = rig.killer.localScale.x;
        s.elbowRest = rig.Bone(HumanBodyBones.RightLowerArm).localPosition;
        s.handRest = rig.Bone(HumanBodyBones.RightHand).localPosition;

        var prop = rig.Keep(new GameObject("DroppedTorch"));
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        RemoveCollider(body);
        body.transform.SetParent(prop.transform, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.055f, 0.1f, 0.055f);
        if (torchMesh != null) body.GetComponent<MeshFilter>().sharedMesh = torchMesh;
        Shader lit = litShader != null ? litShader : Shader.Find("Standard");
        Shader unlit = unlitShader != null ? unlitShader : Shader.Find("Unlit/Texture");
        Shader additive = additiveShader != null ? additiveShader : Shader.Find("Legacy Shaders/Particles/Additive");
        Material bodyMat = torchMaterial != null ? torchMaterial
            : lit != null ? rig.Keep(new Material(lit) { color = new Color(0.08f, 0.08f, 0.09f) }) : null;
        if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        RemoveCollider(lens);
        lens.transform.SetParent(prop.transform, false);
        lens.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        lens.transform.localScale = new Vector3(0.05f, 0.05f, 0.012f);
        if (unlit != null) lens.GetComponent<Renderer>().sharedMaterial = rig.Keep(new Material(unlit) { mainTexture = Texture2D.whiteTexture });
        prop.SetActive(false);
        s.torchProp = prop.transform;

        // the glare of the torch's lens when it points your way
        var glare = GameObject.CreatePrimitive(PrimitiveType.Quad);
        RemoveCollider(glare);
        rig.Keep(glare);
        glare.name = "DroppedTorchGlare";
        var tex = rig.Keep(new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp });
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                float a = Mathf.Clamp01(1f - r);
                a = a * a * a + 0.6f * Mathf.Exp(-r * r * 60f);
                tex.SetPixel(x, y, new Color(1f, 0.97f, 0.9f, Mathf.Clamp01(a)));
            }
        tex.Apply();
        if (additive != null)
        {
            s.glareMat = rig.Keep(new Material(additive) { mainTexture = tex });
            glare.GetComponent<Renderer>().sharedMaterial = s.glareMat;
        }
        glare.SetActive(false);
        s.glare = glare.transform;

        var ghostGo = rig.Keep(new GameObject("SpiritGhostLight", typeof(Light)));
        s.ghost = ghostGo.GetComponent<Light>();
        s.ghost.type = LightType.Point;
        s.ghost.color = new Color(0.72f, 0.8f, 1f);
        s.ghost.range = 1.3f;
        s.ghost.shadows = LightShadows.None;
        s.ghost.renderMode = LightRenderMode.ForcePixel;   // or the red lanterns crowd it out
        s.ghost.enabled = false;

        // the torch on the ground throws her shadow at you
        if (rig.torch != null) { s.torchShadows = rig.torch.shadows; rig.torch.shadows = LightShadows.Hard; }

        // Caught with your back to something: fall and get dragged only as far as there is room behind you.
        float drag = DragLength();
        float room = Room(rig, FallBack + drag + 0.6f) - 0.6f;
        if (room < FallBack + drag)
        {
            s.fallBack = Mathf.Clamp(room, 0.5f, FallBack);
            s.dragScale = drag > 0f ? Mathf.Clamp01((room - s.fallBack) / drag) : 0f;
        }
    }

    // How far you are dragged in all, at full length (m).
    private float DragLength()
    {
        float d = 0f;
        for (int i = 0; i < pulls.Length; i++)
        {
            if (ending != Ending.FaceInTheDark && pulls[i] >= release) break;
            d += pullLength;
        }
        return d;
    }

    // Open ground behind the player, up to max (m): the nearest thing a body-sized sphere hits going backwards,
    // low to the ground, that isn't the player or the killer.
    private static float Room(KillSequenceRig rig, float max)
    {
        float nearest = max;
        // high enough that a gentle rise in the ground behind you doesn't count; low enough to catch a headstone
        Vector3 from = rig.ground + Vector3.up * 0.6f;
        foreach (RaycastHit h in Physics.SphereCastAll(from, 0.2f, -rig.forward, max, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(rig.killer) || (rig.player != null && t.IsChildOf(rig.player))) continue;
            if (h.distance > 0f && h.distance < nearest) nearest = h.distance;
        }
        return nearest;
    }

    public override void End(KillSequenceRig rig)
    {
        if (rig.state is State s && rig.torch != null) rig.torch.shadows = s.torchShadows;
    }

    // How far you have been dragged at t (m). Only the first four pulls happen in the endings where she lets go.
    private float Dragged(float t)
    {
        float d = 0f;
        for (int i = 0; i < pulls.Length; i++)
        {
            if (ending != Ending.FaceInTheDark && pulls[i] >= release) break;
            d += pullLength * InOut(Seg(t, pulls[i], pulls[i] + pullTime));
        }
        return d;
    }

    private float PullJolt(float t)
    {
        float j = 0f;
        for (int i = 0; i < pulls.Length; i++)
        {
            if (ending != Ending.FaceInTheDark && pulls[i] >= release) break;
            j += Kick(t, pulls[i], 1.6f * (i % 2 == 0 ? 1f : -0.7f), 5f, 9f);
        }
        return j;
    }

    // Lying on your back, head lifted a little, at this many metres dragged.
    private static Vector3 Lying(KillSequenceRig rig, float dragged)
    {
        float fall = rig.state is State s ? s.fallBack : FallBack;
        return rig.Ground(rig.ground - rig.forward * (fall + dragged)) + Vector3.up * LieHeight;
    }

    // The pitch (degrees, negative = up) that looks from cam at p, along the forward/up plane (past the vertical when p is behind).
    private static float PitchTo(Vector3 cam, Vector3 p, Vector3 F)
    {
        return -Mathf.Atan2(p.y - cam.y, Vector3.Dot(p - cam, F)) * Mathf.Rad2Deg;
    }

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        rig.BeginFrame();
        var s = (State)rig.state;
        Vector3 eye = rig.Eye();
        Vector3 F = rig.forward, R = rig.right;
        rig.Lanterns(true);
        float endCut = Cut;

        // ---------------- camera position + pitch (degrees from level along F; negative looks up)
        Vector3 cam;
        float pitch, roll = 0f, yaw = 0f;
        float dragged = Dragged(t) * s.dragScale;
        if (t < yank)
        {
            cam = eye;
            pitch = Mathf.Lerp(4f, 22f, InOut(Seg(t, scuttleAt, vanish)));
        }
        else if (t < impact)
        {
            float u = Seg(t, yank, impact);
            cam = Vector3.Lerp(eye - F * 0.25f * Out3(Seg(t, yank, yank + 0.06f)), Lying(rig, 0f), In2(u));
            pitch = Mathf.Lerp(22f, -82f, Out3(u));
            roll = 8f * u;
        }
        else
        {
            cam = Lying(rig, dragged);
            float lift = Out3(Seg(t, impact, impact + 0.3f));
            pitch = Mathf.Lerp(-82f, LookUp, lift);
            // craning back over your head to see who has you: pitch up past the vertical, onto her face
            float crane = InOut(Seg(t, craneBack, craneBack + 0.32f)) * (1f - InOut(Seg(t, craneReturn, craneReturn + 0.3f)));
            pitch = Mathf.Lerp(pitch, PitchTo(cam, cam - F * 0.8f + Vector3.up * 1.25f, F), crane);
            roll = Mathf.Lerp(8f, 3f, lift) + PullJolt(t) * 0.8f;
            pitch += PullJolt(t);
        }
        if (t >= release && ending != Ending.FaceInTheDark) pitch += Kick(t, release, 2.5f, 4f, 7f);   // she lets go: your head drops
        float panicEnd = ending == Ending.FaceInTheDark ? face : endCut;
        float panic = t < impact ? Mathf.Lerp(0.5f, 1.4f, Seg(t, 0f, yank)) : 0.6f + 0.6f * Seg(t, release, panicEnd);

        // ---------------- the ending's camera
        bool bridgePosed = false, bridgeGhost = false;
        float bridgeGhostIntensity = 0f;
        if (ending == Ending.Bridge)
        {
            float back = InOut(Seg(t, bridgeLookBack, bridgeLookBack + 0.22f)) * (1f - InOut(Seg(t, bridgeLookDown, bridgeLookDown + 0.22f)));
            pitch = Mathf.Lerp(pitch, -108f, back);
            if (t >= bridgeLookBack + 0.2f)
            {
                // she is standing at your feet: you look up at her, and follow her face as she bends back over you
                Bridge(rig, t, cam, F, R, out bridgeGhost, out bridgeGhostIntensity);
                bridgePosed = true;
                Vector3 lookAt = Vector3.Lerp(rig.Bone(HumanBodyBones.Chest).position, rig.Face, 0.55f);
                pitch = Mathf.Lerp(pitch, PitchTo(cam, lookAt, F) + 6f, InOut(Seg(t, bridgeLookDown, bridgeLookDown + 0.3f)));
            }
        }
        else if (ending == Ending.Neck)
        {
            Vector3 herFace = cam - F * 0.12f + Vector3.up * 0.58f;
            float look = InOut(Seg(t, neckLookUp, neckLookUp + 0.22f));
            float toFace = PitchTo(cam, herFace, F);
            pitch = Mathf.Lerp(pitch, toFace, look);
            float twist = 0f;
            for (int i = 0; i < neckTwists.Length && i < neckTwistAngles.Length; i++)
            {
                float from = i == 0 ? 0f : neckTwistAngles[i - 1];
                if (t >= neckTwists[i]) twist = Mathf.Lerp(from, neckTwistAngles[i], Out3(Seg(t, neckTwists[i], neckTwists[i] + 0.05f)));
            }
            roll += twist;
            for (int i = 0; i < neckTwists.Length; i++) roll += Kick(t, neckTwists[i] + 0.05f, 6f, 6f, 9f);
            // let go: your head drops back and to the side; you are looking along the ground at the light
            float drop = In2(Seg(t, neckLetGo, neckLetGo + 0.2f));
            pitch = Mathf.Lerp(pitch, -3f, drop);
            roll = Mathf.Lerp(roll, 60f, drop);
            cam += Vector3.down * 0.16f * drop;
            pitch += Kick(t, neckLetGo + 0.2f, 4f, 5f, 8f);
            if (t >= neckTwists[neckTwists.Length - 1] + 0.05f) panic = 0f;   // nothing moves you any more
        }

        // breath and panic
        pitch += Noise(t * 5f, 1) * panic + Kick(t, impact, 9f, 4f, 6f);
        yaw += Noise(t * 4f, 2) * panic * 1.3f + Kick(t, yank, -7f, 3f, 5f);
        roll += Kick(t, impact, -5f, 4f, 6f);
        if (ending == Ending.FaceInTheDark) { pitch += Kick(t, face, -6f, 5f, 7f); roll += Kick(t, face, 4f, 4f, 6f); }
        bool eclipsePosed = false;
        if (ending == Ending.Eclipse)
        {
            if (t >= eclipseAppear)
            {
                Eclipse(rig, t, cam, F, R);
                eclipsePosed = true;
            }
            float lookUp = 13f * InOut(Seg(t, eclipseAppear - 0.1f, eclipseAppear + 0.15f));
            switch (eclipseFinale)
            {
                case EclipseFinale.Crawl:
                    pitch -= lookUp * (1f - InOut(Seg(t, eclipseRush, eclipseRush + 0.3f)));
                    pitch += Kick(t, endCut - 0.12f, -7f, 5f, 7f);
                    break;
                case EclipseFinale.LongArm:
                    // keep her in view: the arm sweeps out across the frame, then the hand is on you
                    pitch -= lookUp * (1f - 0.35f * InOut(Seg(t, armDrop, armArrive)));
                    pitch += Kick(t, armArrive, -8f, 5f, 7f) + Kick(t, armHit, 5f, 6f, 8f);
                    yaw += Kick(t, armArrive, -4f, 4f, 7f);
                    break;
                case EclipseFinale.Unfold:
                    // up, and further up, as she grows; then she is over you
                    if (eclipsePosed && t >= finaleAt)
                    {
                        Vector3 lookAt = Vector3.Lerp(rig.Bone(HumanBodyBones.Chest).position, rig.Face, 0.5f);
                        pitch = Mathf.Lerp(pitch - lookUp, PitchTo(cam, lookAt, F) + 8f, InOut(Seg(t, finaleAt, finaleAt + 0.25f)));
                    }
                    else pitch -= lookUp;
                    pitch += Kick(t, unfoldGrown, -6f, 4f, 6f);
                    break;
                default:
                    // she walks away into the light: your eyes follow her down to it
                    pitch -= lookUp * (1f - 0.75f * InOut(Seg(t, finaleAt, takeReach)));
                    pitch += Kick(t, takeGlare, -6f, 5f, 7f);
                    break;
            }
        }
        Quaternion camRot = Quaternion.LookRotation(F, Vector3.up) * Quaternion.Euler(pitch, yaw, roll);
        Vector3 view = camRot * Vector3.forward;

        // ---------------- her
        bool ghostOn = false;
        float ghostIntensity = 0.9f;
        Vector3 ghostAt = rig.Face;
        if (t < vanish)
        {
            float go = InOut(Seg(t, scuttleAt, vanish));
            float d = Mathf.Lerp(startDistance, 1.1f, In2(go));
            rig.Visible(true);
            rig.Place(rig.Ground(rig.At(d)), -F);
            if (go <= 0f) { rig.Pose(("reach", 0.2f, 1f)); rig.Stare(eye, 1f); rig.HeadTurn(eye, -30f, 0f, 0f); }
            else { rig.Pose(("scuttle", (t - scuttleAt) * 1.6f, 1f)); rig.Stare(eye, 0.8f); }
        }
        else if (t < impact + 0.2f)
        {
            rig.Visible(false);
        }
        else if (t < (ending == Ending.FaceInTheDark ? face : release) || (ending == Ending.Neck && t < neckLetGo + 0.25f))
        {
            DragYou(rig, t, cam, dragged, F, R);
            ghostOn = ending == Ending.FaceInTheDark ? t < torchDies : true;
            if (ending == Ending.Neck && t >= release) NeckHold(rig, t, cam, camRot, F, out ghostIntensity);
            ghostAt = Vector3.Lerp(cam, rig.Face, 0.75f);
        }
        else if (ending == Ending.FaceInTheDark)
        {
            FaceDown(rig, t, cam, camRot, view, F);
            ghostOn = true;
            ghostIntensity = (3.2f + 2.4f * Mathf.Exp(-(t - face) * 6f)) * (0.85f + 0.15f * Noise(t * 26f, 12));
            ghostAt = Vector3.Lerp(cam, rig.Face, 0.5f) - camRot * Vector3.up * 0.06f;
        }
        else if (ending == Ending.Eclipse)
        {
            if (!eclipsePosed) rig.Visible(false);
            // a faint glow of her own, so her white limbs show against the light behind her
            ghostOn = t >= eclipseAppear;
            ghostAt = rig.Face + (cam - rig.Face).normalized * 0.7f + Vector3.down * 0.3f;
            ghostIntensity = 1.5f;
            if (t >= finaleAt && eclipseFinale == EclipseFinale.LongArm)
            {
                // the glow goes with her hand
                Vector3 hand = rig.Bone(HumanBodyBones.RightHand).position;
                ghostAt = hand + (cam - hand).normalized * 0.35f + Vector3.up * 0.15f;
                ghostIntensity = 2.0f;
            }
            else if (t >= finaleAt && eclipseFinale == EclipseFinale.Unfold)
            {
                ghostAt = rig.Bone(HumanBodyBones.Chest).position + (cam - rig.Bone(HumanBodyBones.Chest).position).normalized * 1.2f;
                ghostIntensity = 1.6f;
            }
            else if (t >= finaleAt && eclipseFinale == EclipseFinale.TakeTheLight)
            {
                ghostAt = rig.Face + (cam - rig.Face).normalized * 0.9f;
                ghostIntensity = 1.4f;
            }
        }
        else if (ending == Ending.Bridge)
        {
            if (!bridgePosed) rig.Visible(false);
            ghostOn = bridgeGhost;
            ghostIntensity = bridgeGhostIntensity;
            ghostAt = Vector3.Lerp(cam, rig.Face, 0.55f);
        }
        else rig.Visible(false);

        // ---------------- the torch: in your hand, flying, then lying on the path
        Vector3 handStart = eye + Vector3.down * 0.3f + R * 0.2f;
        Vector3 land = rig.Ground(rig.At(torchLands.x, torchLands.y)) + Vector3.up * 0.04f;
        // it lies pointing at where you fell; in Eclipse a little higher, so it throws its light (and her shadow) down the path at you
        Vector3 restAim = rig.Ground(rig.ground - F * s.fallBack) + Vector3.up * (ending == Ending.Eclipse ? 0.55f : 0.15f) + R * 0.25f;
        bool torchLit = TorchLit(t);
        if (t < yank)
        {
            s.torchProp.gameObject.SetActive(false);
            rig.Torch(torchLit ? 2.4f * (0.85f + 0.15f * Noise(t * 30f, 9)) : 0f, cam + camRot * new Vector3(0.2f, -0.3f, 0f), cam + view * 6f);
        }
        else
        {
            float tau = t - (yank + 0.03f);
            Vector3 pos, dir;
            if (tau < torchFlight)
            {
                Vector3 g = Physics.gravity;
                Vector3 v = (land - handStart - 0.5f * g * torchFlight * torchFlight) / torchFlight;
                pos = handStart + v * Mathf.Max(0f, tau) + 0.5f * g * tau * tau;
                dir = Quaternion.AngleAxis(tau * 1100f, R) * Quaternion.AngleAxis(tau * 380f, Vector3.up) * F;
            }
            else
            {
                float after = tau - torchFlight;
                float hop = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Seg(after, 0f, 0.14f))) * 0.12f;
                pos = land + F * 0.35f * Out3(Seg(after, 0f, 0.5f)) + Vector3.up * hop;
                Vector3 spinDir = Quaternion.AngleAxis(after * 600f, Vector3.up) * F;
                dir = Vector3.Slerp(spinDir, (restAim - pos).normalized, Out3(Seg(after, 0.05f, 0.55f)));
            }
            if (ending == Ending.Eclipse && eclipseFinale == EclipseFinale.TakeTheLight && t >= takeHold)
            {
                // in her hand: lifted, swung round across the ground at you, then straight into your eyes
                Vector3 hand = rig.Bone(HumanBodyBones.RightHand).position;
                pos = hand;
                Vector3 down = (rig.Ground(hand + rig.killer.forward * 1.5f) - hand).normalized;
                Vector3 atYou = (cam - hand).normalized;
                dir = Vector3.Slerp(down, atYou, InOut(Seg(t, takeHold + 0.05f, takeAim)));
                if (t >= takeAim) dir = atYou;
            }
            s.torchProp.gameObject.SetActive(true);
            s.torchProp.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            float lit = torchLit ? 3.4f * (0.88f + 0.12f * Noise(t * 30f, 9)) : 0f;
            if (lit > 0f && ending == Ending.Eclipse && eclipseFinale == EclipseFinale.TakeTheLight && t >= takeAim) lit *= 1.6f;
            rig.Torch(lit, pos + dir * 0.12f, pos + dir * 5f, ending == Ending.Eclipse ? 16f : 14f, 68f);
            // the lens glares when it points at you
            Vector3 lensAt = pos + dir * 0.11f;
            Vector3 toCam = cam - lensAt;
            float facing = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(dir, toCam.normalized)), 3f);
            s.glare.gameObject.SetActive(lit > 0f && s.glareMat != null);
            s.glare.SetPositionAndRotation(lensAt, Quaternion.LookRotation(-toCam.normalized, Vector3.up));
            s.glare.localScale = Vector3.one * (0.22f + 0.9f * facing) * Mathf.Clamp(toCam.magnitude / 4f, 0.7f, 2.4f);
            if (ending == Ending.Eclipse && eclipseFinale == EclipseFinale.TakeTheLight && t >= takeAim)
                s.glare.localScale *= 1f + 3.5f * In2(Seg(t, takeAim, takeGlare + 0.08f));   // it fills your eyes
            if (s.glareMat != null) s.glareMat.SetColor("_TintColor", new Color(0.5f, 0.48f, 0.44f, 0.5f) * (0.35f + 0.65f * facing));
        }

        // she gives off a little light of her own
        s.ghost.enabled = ghostOn;
        if (ghostOn)
        {
            s.ghost.transform.position = ghostAt;
            s.ghost.range = ending == Ending.FaceInTheDark && t >= face ? 1.2f : ending == Ending.Bridge ? 2.6f : ending != Ending.Eclipse ? 1.6f
                : t >= finaleAt && eclipseFinale == EclipseFinale.LongArm ? 1.6f : t >= finaleAt && eclipseFinale == EclipseFinale.Unfold ? 5f
                : t >= finaleAt && eclipseFinale == EclipseFinale.Unfold ? 5f : 3f;
            s.ghost.intensity = ghostIntensity;
        }

        rig.cam.SetPositionAndRotation(cam, camRot);
        rig.camera.fieldOfView = ending == Ending.FaceInTheDark && t >= face ? rig.baseFov - 6f : rig.baseFov;
        if (ending == Ending.Eclipse && eclipseFinale == EclipseFinale.Unfold)
            rig.camera.fieldOfView = rig.baseFov + 22f * InOut(Seg(t, finaleAt, unfoldGrown));

        // ---------------- picture
        float dazed = 1f - Seg(t, impact + 0.1f, impact + 1.0f);
        if (t >= impact && t < (ending == Ending.FaceInTheDark ? face : endCut))
        {
            rig.fx.blur = 0.006f * dazed + 0.0008f;
            rig.fx.lids = t < impact + 0.5f ? 0.35f * Mathf.Abs(Mathf.Sin((t - impact) * 9f)) * dazed : 0f;
            rig.fx.darken = 0.15f * dazed;
        }
        if (t >= impact && t < impact + 0.034f) { rig.fx.flashColor = Color.white; rig.fx.flash = 0.6f; }
        if (t < yank) rig.SetVhs(-1f, 0.03f);
        else if (t < impact) rig.SetVhs(0.6f, 0.07f, 0.006f, 0.05f);
        else if (ending == Ending.FaceInTheDark && t >= face) rig.SetVhs(t < face + 0.083f ? 0.9f : (Spike(t, 7f, 0.3f, 2f) ? 0.65f : 0.22f), 0.06f, 0.008f, 0.05f);
        else rig.SetVhs(0.12f, 0.04f);
        rig.fx.split = ending == Ending.FaceInTheDark && t >= face ? 0.009f * Mathf.Exp(-(t - face) * 8f) + 0.001f : (t >= impact && t < impact + 0.4f ? 0.004f : 0f);
        EndingPicture(rig, t);
        if (t >= endCut)
        {
            rig.fx.flashColor = Color.black;
            rig.fx.flash = 1f;
        }
    }

    private bool TorchLit(float t)
    {
        if (t >= vanish && t < vanish + 0.066f) return false;   // the stutter she vanishes in
        switch (ending)
        {
            case Ending.FaceInTheDark:
                if (t >= torchDies) return t >= face && t < face + 0.05f;
                return !(t >= torchDies - 0.35f && Spike(t, 14f, 0.3f + 0.6f * Seg(t, torchDies - 0.35f, torchDies), 7f));
            case Ending.Eclipse:
                if (eclipseFinale == EclipseFinale.TakeTheLight && t >= takeOff) return false;   // she switches it off
                // it stutters as she twists, then holds steady
                return !(t >= eclipseAppear + 0.1f && t < finaleAt && Spike(t, 12f, 0.12f, 5f));
            case Ending.Bridge:
                if (t >= bridgeDark) return false;
                foreach (float snap in bridgeSnaps) if (t >= snap - 0.06f && t < snap) return false;   // she moves in the dark
                return true;
            default:
                if (t >= neckTorchOff) return false;
                return !(t >= neckTorchOff - 0.25f && Spike(t, 14f, 0.45f, 9f));
        }
    }

    // ---------------- her, behind your head, walking backwards, bent over you, both hands in your collar
    private void DragYou(KillSequenceRig rig, float t, Vector3 cam, float dragged, Vector3 F, Vector3 R)
    {
        Vector3 feet = rig.Ground(cam - F * 0.8f);
        rig.Visible(true);
        rig.Place(feet + Vector3.up * 0.05f * Mathf.Abs(Mathf.Sin(dragged * 2.3f)), F);
        rig.Pose(("reach", 0.55f + 0.08f * Mathf.Sin(dragged * 2f), 1f));
        rig.LeanTo(cam, 0.7f);
        rig.Stare(cam, 1f);
        rig.HeadTurn(cam, -28f + 6f * Noise(t * 2f, 4), 0f, 0f);
        Vector3 collar = cam + Vector3.down * 0.2f - F * 0.18f;
        rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, collar - R * 0.13f);
        rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, collar + R * 0.13f);
    }

    // ---------------- Face In The Dark: she is standing over your feet; she folds down and her face drops into yours
    private void FaceDown(KillSequenceRig rig, float t, Vector3 cam, Quaternion camRot, Vector3 view, Vector3 F)
    {
        float hold = Seg(t, face, cut);
        float drop = Out3(Seg(t, face, face + 0.08f));
        rig.Visible(true);
        rig.Place(rig.Ground(cam + F * 0.9f), -F);
        rig.Pose(("reach", 0.7f + 0.1f * hold, 1f));
        Vector3 at = cam + view * (faceDistance - 0.04f * hold) + camRot * Vector3.up * (0.45f * (1f - drop));
        rig.LeanTo(at, 1f);
        rig.PutFace(at);
        rig.Stare(cam, 1f);
        rig.HeadTurn(cam, 16f * Noise(t * 24f, 3) + 7f * Jitter(t, 30f, 4), 7f * Noise(t * 21f, 5), 5f * Noise(t * 26f, 6));
        rig.PutFace(at);
    }

    // ---------------- Eclipse: in your torchlight she jerks from one wrong shape to the next, then comes for you on all fours
    private void Eclipse(KillSequenceRig rig, float t, Vector3 cam, Vector3 F, Vector3 R)
    {
        if (t < eclipseAppear) { rig.Visible(false); return; }
        rig.Visible(true);
        Vector3 spot = rig.Ground(cam + F * eclipseDistance);
        if (eclipseFinale != EclipseFinale.Crawl && t >= finaleAt)
        {
            EclipseFinaleBeat(rig, t, cam, F, R, spot);
            return;
        }
        if (t < eclipseRush)
        {
            int step = Mathf.Min(Mathf.FloorToInt((t - eclipseAppear) / eclipseStep), eclipseFinale == EclipseFinale.Crawl ? 5 : 4);
            float ts = Steps(t, 1f / eclipseStep);   // the body holds each shape; the head shakes in real time
            rig.Place(spot, -F);
            rig.Pose(("idle", 0.6f, 1f));
            Vector3 r = rig.killer.right, f = rig.killer.forward, u = Vector3.up;
            const HumanBodyBones LUA = HumanBodyBones.LeftUpperArm, LLA = HumanBodyBones.LeftLowerArm, LH = HumanBodyBones.LeftHand;
            const HumanBodyBones RUA = HumanBodyBones.RightUpperArm, RLA = HumanBodyBones.RightLowerArm, RH = HumanBodyBones.RightHand;
            switch (step)
            {
                case 0:   // standing still, head hanging
                    rig.HeadTurn(cam, 0f, 0f, 40f);
                    break;
                case 1:   // the head snaps over onto her shoulder; arms flung out, hanging broken from the elbows
                    AimLimb(rig, LUA, LLA, LH, (-r + u * 0.1f).normalized, (-u - f * 0.5f + r * 0.2f).normalized);
                    AimLimb(rig, RUA, RLA, RH, (r + u * 0.1f).normalized, (-u - f * 0.5f - r * 0.2f).normalized);
                    rig.Stare(cam, 1f);
                    rig.HeadTurn(cam, 92f, 0f, 0f, 0.5f);
                    break;
                case 2:   // folded sideways at the waist; one arm up and snapped back, the other dragging on the ground
                case 3:   // ...then the other way
                    float side = step == 2 ? 1f : -1f;
                    rig.Bend(HumanBodyBones.Spine, f, 26f * side);
                    rig.Bend(HumanBodyBones.Chest, f, 30f * side);
                    HumanBodyBones hiU = side > 0 ? LUA : RUA, hiL = side > 0 ? LLA : RLA, hiH = side > 0 ? LH : RH;
                    HumanBodyBones loU = side > 0 ? RUA : LUA, loL = side > 0 ? RLA : LLA, loH = side > 0 ? RH : LH;
                    rig.Stretch(1.75f, loU); rig.Stretch(3.0f, loL);   // as long as the shapes were approved
                    AimLimb(rig, hiU, hiL, hiH, (u + r * 0.25f * side).normalized, (-f * 0.85f + u * 0.1f + r * 0.35f * side).normalized);
                    AimLimb(rig, loU, loL, loH, (-u - r * 0.3f * side).normalized, (-u - r * 0.05f * side + f * 0.1f).normalized);
                    rig.Stare(cam, 1f);
                    rig.HeadTurn(cam, -115f * side, 0f, 0f, 0.5f);
                    Vibrate(rig, cam, t, 0.9f);
                    break;
                case 4:   // arched back, both arms up and broken backwards, a knee the wrong way, upside-down face
                    rig.Bend(HumanBodyBones.Spine, r, -22f);
                    rig.Bend(HumanBodyBones.Chest, r, -26f);
                    StretchLimbs(rig, 1.35f);
                    AimLimb(rig, LUA, LLA, LH, (u * 0.85f - f * 0.35f - r * 0.4f).normalized, (-u * 0.5f - f * 0.85f - r * 0.15f).normalized);
                    AimLimb(rig, RUA, RLA, RH, (u * 0.85f - f * 0.35f + r * 0.4f).normalized, (-u * 0.5f - f * 0.85f + r * 0.15f).normalized);
                    Hyperextend(rig, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 55f);
                    rig.Stare(cam, 1f);
                    rig.HeadTurn(cam, 178f, 0f, 0f, 0.45f);
                    Vibrate(rig, cam, t, 1f);
                    break;
                default:  // dropped onto all fours, every limb too long, looking at you
                    rig.Pose(("scuttle", 0.1f, 1f));
                    StretchLimbs(rig, 1.4f);
                    rig.Stare(cam, 1f);
                    Vibrate(rig, cam, t, 0.6f);
                    break;
            }
            if (step >= 1) Spasm(rig, ts, 0.16f + 0.05f * step);
        }
        else
        {
            // across the ground at you, blotting out the light
            float u = Seg(t, eclipseRush, eclipseCut - 0.02f);
            float d = Mathf.Lerp(eclipseDistance, 0.15f, In2(u));
            rig.Place(rig.Ground(cam + F * d), -F);
            rig.Pose(("scuttle", Steps(t - eclipseRush, 15f) * 2.2f, 1f));
            StretchLimbs(rig, 1.4f);
            Spasm(rig, t, 0.35f);
            rig.Stare(cam, 0.9f);
            Vibrate(rig, cam, t, 0.5f);
        }
    }


    // ---------------- Eclipse's finales (all but the crawl)
    private void EclipseFinaleBeat(KillSequenceRig rig, float t, Vector3 cam, Vector3 F, Vector3 R, Vector3 spot)
    {
        const HumanBodyBones RUA = HumanBodyBones.RightUpperArm, RLA = HumanBodyBones.RightLowerArm, RH = HumanBodyBones.RightHand;
        const HumanBodyBones LUA = HumanBodyBones.LeftUpperArm, LLA = HumanBodyBones.LeftLowerArm, LH = HumanBodyBones.LeftHand;
        switch (eclipseFinale)
        {
            case EclipseFinale.LongArm:
            {
                // she stands still in the light, head on one side; one arm sweeps out across the ground, impossibly
                // long and broken at the elbow, and the hand comes at you from the side
                rig.Place(spot, -F);
                rig.Pose(("idle", 0.6f, 1f));
                Vector3 r = rig.killer.right;
                Transform sh = rig.Bone(RUA), el = rig.Bone(RLA), hd = rig.Bone(RH);
                if (rig.state is State st) { el.localPosition = st.elbowRest; hd.localPosition = st.handRest; }
                float natural = (el.position - sh.position).magnitude + (hd.position - el.position).magnitude;
                Vector3 side = hd.position;
                Vector3 sweep = rig.Ground(spot - R * 2.4f + F * 0.6f) + Vector3.up * 0.06f;    // out to the side, in the torchlight
                Vector3 near = rig.Ground(cam - R * 0.55f + F * 0.75f) + Vector3.up * 0.1f;   // beside you, in view
                Vector3 view = Quaternion.LookRotation(F, Vector3.up) * Quaternion.Euler(LookUp - 13f * 0.65f, 0f, 0f) * Vector3.forward;
                Vector3 lens = cam + view * 0.12f + Vector3.down * 0.1f;   // the wrist low, so the palm covers your eyes
                Vector3 target;
                if (t < armDrop) target = Vector3.Lerp(side, sweep, In2(Seg(t, finaleAt + 0.03f, armDrop)));
                else if (t < armArrive)
                {
                    // across the dirt in stop-motion scrabbles
                    float u = InOut(Seg(Steps(t, 16f), armDrop, armArrive));
                    target = Vector3.Lerp(sweep, near, u) + Vector3.up * 0.05f * Mathf.Abs(Mathf.Sin(Steps(t, 16f) * 40f));
                }
                else target = Vector3.Lerp(near, lens, Out3(Seg(t, armArrive, armHit)));
                // one straight, impossibly long limb from her shoulder to the hand (straight, so the stretch stays
                // along the arm instead of skewing it), with a slight break at the elbow
                Vector3 along = (target - sh.position).normalized;
                Vector3 kinkUp = Vector3.Cross(along, Vector3.Cross(Vector3.up, along)).normalized;
                rig.AimBone(RUA, el.position, (along + kinkUp * 0.08f).normalized);
                rig.AimBone(RLA, hd.position, (along - kinkUp * 0.08f).normalized);
                // pulled apart rather than scaled: the elbow and the hand are moved out along the arm and the skin
                // between stretches after them (scaling would flatten the hand), so the hand stays a hand
                float need = (target - sh.position).magnitude;
                if (need > natural)
                {
                    el.position = sh.position + (el.position - sh.position).normalized * (need * 0.45f);
                    hd.position = target;
                }
                Transform finger = rig.Bone(HumanBodyBones.RightIndexProximal);
                if (finger != null)
                {
                    Vector3 fingersTo = t < armArrive ? ((cam - target).normalized * 0.7f + Vector3.down * 0.3f).normalized : Vector3.up;
                    rig.AimBone(RH, finger.position, fingersTo, 1f);
                }
                rig.Stare(cam, 1f);
                rig.HeadTurn(cam, -38f, 0f, 0f, 0.4f);
                if (t >= armDrop) Vibrate(rig, cam, t, 0.35f);
                break;
            }
            case EclipseFinale.Unfold:
            {
                // she grows, jerk by jerk, arms spreading out and up like something opening; then folds over you
                rig.Place(spot, -F);
                rig.Pose(("idle", 0.6f, 1f));
                float g = InOut(Seg(Steps(t, 10f), finaleAt, unfoldGrown));
                rig.killer.localScale = new Vector3(1f, 1.25f, 1f) * Mathf.Lerp(1f, 2.2f, g) * BaseScale(rig);
                Vector3 r = rig.killer.right, f = rig.killer.forward, u = Vector3.up;
                float over = InOut(Seg(t, unfoldGrown, unfoldOver));
                rig.Bend(HumanBodyBones.Spine, r, -12f * g + 38f * over);
                rig.Bend(HumanBodyBones.Chest, r, -14f * g + 42f * over);
                rig.Stretch(Mathf.Lerp(1f, 2.0f, g), RUA, LUA); rig.Stretch(Mathf.Lerp(1f, 4.0f, g), RLA, LLA);
                // wide and up, broken backwards at the elbows; then reaching down around you
                Vector3 lUp = (-r * 0.8f + u * 0.55f - f * 0.1f).normalized, rUp = (r * 0.8f + u * 0.55f - f * 0.1f).normalized;
                Vector3 lLo = (-u * 0.85f + r * 0.3f - f * 0.2f).normalized, rLo = (-u * 0.85f - r * 0.3f - f * 0.2f).normalized;
                Vector3 lDown = (-r * 0.5f - u * 0.3f + f * 0.8f).normalized, rDown = (r * 0.5f - u * 0.3f + f * 0.8f).normalized;
                AimLimb(rig, LUA, LLA, LH, Vector3.Slerp(lUp, lDown, over), Vector3.Slerp(lLo, -u, over), Mathf.Max(g, over));
                AimLimb(rig, RUA, RLA, RH, Vector3.Slerp(rUp, rDown, over), Vector3.Slerp(rLo, -u, over), Mathf.Max(g, over));
                rig.Stare(cam, 1f);
                rig.HeadTurn(cam, 30f * g, 0f, 0f, 0.4f);
                Vibrate(rig, cam, t, 0.5f + 0.5f * over);
                Spasm(rig, Steps(t, 10f), 0.15f * g);
                break;
            }
            default:
            {
                // she turns her back on you and walks to your torch in jerks, picks it up, and turns it on you
                Vector3 torchAt = rig.Ground(rig.At(torchLands.x, torchLands.y) + F * 0.35f);
                Vector3 stand = torchAt - F * 0.75f + R * 0.25f;
                if (t < takeReach)
                {
                    float u = InOut(Seg(Steps(t, 8f), finaleAt, takeReach));
                    rig.Place(rig.Ground(Vector3.Lerp(spot, stand, u)), F);
                    rig.Pose(("walk", Steps(t, 8f) * 1.4f, 1f));
                    Spasm(rig, Steps(t, 8f), 0.2f);
                    rig.HeadTurn(cam, 0f, 0f, 25f);
                }
                else if (t < takeAim)
                {
                    // bent down to it, then up with it, turning round to you
                    float up = InOut(Seg(t, takeHold, takeAim));
                    bool turned = t >= Mathf.Lerp(takeHold, takeAim, 0.5f);
                    rig.Place(rig.Ground(stand), turned ? -F : F);
                    rig.Pose(("reach", 0.5f, 1f - up), ("idle", 0.6f, up));
                    if (t < takeHold) rig.Reach(RUA, RLA, RH, torchAt + Vector3.up * 0.05f, InOut(Seg(t, takeReach, takeHold)));
                    rig.Stare(cam, turned ? 1f : 0f);
                    if (turned) rig.HeadTurn(cam, 30f, 0f, 0f);
                }
                else
                {
                    // facing you, arm out, the torch held straight at your eyes; her head shaking
                    rig.Place(rig.Ground(stand), -F);
                    rig.Pose(("idle", 0.6f, 1f));
                    Transform sh = rig.Bone(RUA);
                    Vector3 hold = sh.position + (cam - sh.position).normalized * 0.75f;
                    rig.Stretch(1.3f, RUA); rig.Stretch(1.69f, RLA);
                    rig.Reach(RUA, RLA, RH, hold, 1f, sh.position + Vector3.up * 0.4f);
                    rig.Stare(cam, 1f);
                    Vibrate(rig, cam, t, 0.9f);
                }
                break;
            }
        }
    }


    // Every limb longer; the lower segments by the square, as the shapes were first tuned (before Stretch stopped
    // compounding down the chain).
    private static void StretchLimbs(KillSequenceRig rig, float k)
    {
        rig.Stretch(k, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg);
        rig.Stretch(k * k, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg);
    }

    private static float BaseScale(KillSequenceRig rig)
    {
        return rig.state is State st && st.baseScale > 0f ? st.baseScale : 1f;
    }

    // ---------------- Bridge: she stands at your feet facing the light; each time the torch flickers she has bent further
    // back over you, arms broken backwards to the ground beside you, until her upside-down face hangs over yours
    private void Bridge(KillSequenceRig rig, float t, Vector3 cam, Vector3 F, Vector3 R, out bool ghost, out float ghostIntensity)
    {
        ghost = false;
        ghostIntensity = 1.2f;
        rig.Visible(true);
        int stage = 0;
        for (int i = 0; i < bridgeSnaps.Length; i++) if (t >= bridgeSnaps[i]) stage = i + 1;
        stage = Mathf.Min(stage, bridgeBends.Length - 1);
        rig.Place(rig.Ground(cam + F * bridgeStand), F);
        rig.Pose(("idle", 0.6f, 1f));
        Vector3 r = rig.killer.right;
        float b = bridgeBends[stage];
        rig.Bend(HumanBodyBones.Spine, r, -b * 0.35f);
        rig.Bend(HumanBodyBones.Chest, r, -b * 0.4f);
        rig.Bend(HumanBodyBones.Neck, r, -b * 0.3f);
        rig.Bend(HumanBodyBones.Head, r, -b * 0.25f);   // over backwards: her face comes at you upside down
        if (stage >= 1)
        {
            // arms hanging back over you, elbows bent the wrong way; then hands on the ground either side of you
            float plant = stage >= 2 ? 1f : 0.4f;
            Vector3 lHand = rig.Ground(cam + F * 0.75f - R * 0.5f) + Vector3.up * 0.03f;
            Vector3 rHand = rig.Ground(cam + F * 0.75f + R * 0.5f) + Vector3.up * 0.03f;
            Vector3 pole = rig.Bone(HumanBodyBones.Chest).position + Vector3.up * 1.2f + F * 0.6f;
            rig.Stretch(1f + 0.1f * stage, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
            rig.Stretch((1f + 0.1f * stage) * (1f + 0.1f * stage), HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm);
            rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, lHand, plant, pole);
            rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rHand, plant, pole);
            Hyperextend(rig, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 20f + 8f * stage);
            Hyperextend(rig, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 20f + 8f * stage);
            if (stage >= 3) Hyperextend(rig, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 35f);
            if (stage >= 3) rig.Stare(cam, 0.35f);
            ghost = true;
            ghostIntensity = 2.0f + 0.5f * stage;
        }
        if (stage >= 2) Spasm(rig, t, 0.1f * stage);
        if (stage >= 3) Vibrate(rig, cam, t, stage >= 4 ? 1f : 0.55f);
    }

    // ---------------- Neck: bent over your face, her hands on your head
    private void NeckHold(KillSequenceRig rig, float t, Vector3 cam, Quaternion camRot, Vector3 F, out float ghostIntensity)
    {
        ghostIntensity = 1.3f;
        float closer = InOut(Seg(t, release, neckLookUp + 0.2f));
        // her face comes down close over yours
        Vector3 faceAt = cam - F * 0.12f + Vector3.up * Mathf.Lerp(1.1f, 0.58f, closer);
        rig.LeanTo(faceAt, 0.6f * closer);
        rig.PutFace(Vector3.Lerp(rig.Face, faceAt, closer));
        rig.Stare(cam, 1f);
        rig.HeadTurn(cam, -22f * Mathf.Sin((t - release) * 2.5f), 0f, 0f);
        if (t >= neckTwists[0]) Vibrate(rig, cam, t, 0.12f);
        // her hands, on your temples (they turn with your head)
        float hands = InOut(Seg(t, neckLookUp + 0.05f, neckHands));
        Vector3 cr = camRot * Vector3.right, cu = camRot * Vector3.up, cf = camRot * Vector3.forward;
        Vector3 lHand = cam - cr * 0.14f - cu * 0.07f + cf * 0.07f;
        Vector3 rHand = cam + cr * 0.14f - cu * 0.07f + cf * 0.07f;
        Vector3 pole = rig.Face + Vector3.up * 0.4f;
        rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, lHand, hands, pole);
        rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rHand, hands, pole);
        if (t >= neckLetGo) rig.Visible(t < neckLetGo + 0.1f);
    }

    private void EndingPicture(KillSequenceRig rig, float t)
    {
        switch (ending)
        {
            case Ending.Eclipse:
                if (t >= eclipseAppear && t < eclipseRush)
                {
                    int step = Mathf.FloorToInt((t - eclipseAppear) / eclipseStep);
                    bool snapFrame = t - (eclipseAppear + step * eclipseStep) < 0.034f;
                    rig.SetVhs(snapFrame ? 0.55f : 0.16f, 0.05f, snapFrame ? 0.006f : -1f);
                }
                else if (eclipseFinale == EclipseFinale.Crawl && t >= eclipseRush)
                {
                    rig.SetVhs(0.3f + 0.4f * Seg(t, eclipseRush, eclipseCut), 0.07f, 0.006f, 0.05f);
                    rig.fx.split = 0.004f * Seg(t, eclipseRush, eclipseCut);
                }
                else if (eclipseFinale == EclipseFinale.LongArm && t >= finaleAt)
                {
                    bool scrabble = t >= armDrop && t < armArrive && Spike(t, 16f, 0.25f, 3f);
                    rig.SetVhs(t >= armArrive ? 0.6f : (scrabble ? 0.35f : 0.12f), 0.05f, t >= armArrive ? 0.008f : -1f);
                    if (t >= armArrive) rig.fx.split = 0.006f;
                }
                else if (eclipseFinale == EclipseFinale.Unfold && t >= finaleAt)
                {
                    bool jerk = t < unfoldGrown && (t - Steps(t, 10f)) < 0.034f;
                    rig.SetVhs(jerk ? 0.55f : 0.15f, 0.05f, jerk ? 0.006f : -1f);
                    if (t >= unfoldGrown) { rig.SetVhs(0.35f + 0.4f * Seg(t, unfoldGrown, unfoldOver), 0.06f, 0.006f, 0.05f); rig.fx.split = 0.003f; }
                }
                else if (eclipseFinale == EclipseFinale.TakeTheLight && t >= finaleAt)
                {
                    rig.SetVhs(t >= takeAim ? 0.45f : 0.14f, 0.05f);
                    if (t >= takeAim)
                    {
                        // the beam in your eyes whites the picture out, then she clicks it off
                        rig.fx.flashColor = new Color(1f, 0.97f, 0.92f);
                        rig.fx.flash = 0.85f * In2(Seg(t, takeAim + 0.02f, takeGlare + 0.04f)) * (t < takeOff ? 1f : 0f);
                    }
                }
                break;
            case Ending.Bridge:
                foreach (float snap in bridgeSnaps) if (t >= snap && t < snap + 0.034f) rig.SetVhs(0.7f, 0.06f, 0.008f, 0.05f);
                if (t >= bridgeSnaps[bridgeSnaps.Length - 1]) rig.fx.split = 0.005f;
                break;
            case Ending.Neck:
                float dead = Seg(t, neckTwists[neckTwists.Length - 1] + 0.05f, neckTwists[neckTwists.Length - 1] + 0.4f);
                rig.fx.blur = Mathf.Max(rig.fx.blur, 0.003f * dead);
                rig.fx.desaturate = 0.45f * dead;
                rig.fx.darken = Mathf.Max(rig.fx.darken, 0.25f * dead);
                foreach (float tw in neckTwists) if (t >= tw && t < tw + 0.05f) { rig.SetVhs(0.75f, 0.07f, 0.008f, 0.05f); rig.fx.split = 0.006f; }
                break;
        }
    }

    private static void RemoveCollider(GameObject g)
    {
        Collider c = g.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }
}
