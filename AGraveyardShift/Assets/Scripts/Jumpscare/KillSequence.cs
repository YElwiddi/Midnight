using UnityEngine;

/// <summary>
/// A scripted kill. When a ConditionalKillerEvent names one, KillerNPC hands it the whole jumpscare: the
/// sequence drives the camera, the killer's pose, the torch, the lanterns and the screen effects, and the
/// ending screen follows when it's done. Everything it shows is a function of the seconds since the kill
/// started, so it plays the same at any frame rate. Per-kill state lives on the KillSequenceRig, never on
/// the asset (the asset is shared).
/// </summary>
public abstract class KillSequence : ScriptableObject
{
    [Header("Sound")]
    [Tooltip("Played once (2D) the instant the kill starts. Mixed to this sequence's timeline.")]
    public AudioClip sound;
    [Range(0f, 1f)] public float soundVolume = 1f;

    [Header("Screen Effects")]
    [Tooltip("Hidden/JumpscareScreenFX (referenced here so builds include it).")]
    public Shader screenFxShader;

    [Header("Line")]
    [Tooltip("Seconds into the kill when the killer's event shows its jumpscare line (its text, font, shake and " +
             "flash), if the event has one. Negative = never.")]
    public float lineAt = -1f;

    [Header("Killer Face")]
    [Tooltip("Eye height between the head bone and the top of the head (0..1).")]
    public float eyeUp = 0.42f;
    [Tooltip("How far the eyes sit in front of the head bone (m).")]
    public float eyeForward = 0.05f;

    /// <summary>Seconds from the kill to the ending screen.</summary>
    public abstract float Duration { get; }

    /// <summary>Set up the rig (animation clips, props). Called once, on the kill frame.</summary>
    public abstract void Begin(KillSequenceRig rig);

    /// <summary>Show the kill at <paramref name="t"/> seconds. Called every frame after the animators.</summary>
    public abstract void Evaluate(KillSequenceRig rig, float t);

    /// <summary>Called once when the sequence hands over to the ending screen.</summary>
    public virtual void End(KillSequenceRig rig) { }

    // ------------------------------------------------------------------ timing helpers
    protected static float Sat(float x) => Mathf.Clamp01(x);
    protected static float Seg(float t, float a, float b) => b <= a ? (t >= b ? 1f : 0f) : Sat((t - a) / (b - a));
    protected static float In2(float u) => u * u;
    protected static float In3(float u) => u * u * u;
    protected static float Out3(float u) { u = 1f - u; return 1f - u * u * u; }
    protected static float InOut(float u) => u * u * (3f - 2f * u);

    /// <summary>Damped kick: reaches <paramref name="amp"/> about 1/(4 freq) s after t0, rings and settles.</summary>
    protected static float Kick(float t, float t0, float amp, float freq = 5f, float decay = 7f)
    {
        if (t < t0) return 0f;
        float x = t - t0;
        return amp / Mathf.Exp(-decay / (4f * freq)) * Mathf.Exp(-decay * x) * Mathf.Sin(2f * Mathf.PI * freq * x);
    }

    /// <summary>Smooth noise in -1..1.</summary>
    protected static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed * 7.31f) - 0.5f) * 2f;

    protected static float Hash(float x) { float s = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f; return s - Mathf.Floor(s); }

    /// <summary>Random in -1..1 that holds for 1/<paramref name="rate"/> s at a time.</summary>
    protected static float Jitter(float t, float rate, float seed) => Hash(Mathf.Floor(t * rate) * 1.618f + seed * 17.3f) * 2f - 1f;

    /// <summary>True for a random share (<paramref name="chance"/>) of 1/<paramref name="rate"/> s slots.</summary>
    protected static bool Spike(float t, float rate, float chance, float seed) => Hash(Mathf.Floor(t * rate) * 3.7f + seed) < chance;

    /// <summary>Time held in steps of 1/<paramref name="fps"/> s: stop-motion, for movement that jerks from pose to pose.</summary>
    protected static float Steps(float t, float fps) => Mathf.Floor(t * fps) / fps;

    // ------------------------------------------------------------------ unnatural bodies

    /// <summary>Points a limb's two segments along world directions (each blended by <paramref name="amount"/>).</summary>
    protected static void AimLimb(KillSequenceRig rig, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
        Vector3 upperDir, Vector3 lowerDir, float amount = 1f)
    {
        Transform b = rig.Bone(lower), c = rig.Bone(end);
        if (b == null || c == null || amount <= 0f) return;
        rig.AimBone(upper, b.position, upperDir, amount);
        rig.AimBone(lower, c.position, lowerDir, amount);
    }

    /// <summary>Bends a joint (elbow or knee) the wrong way: straight, then <paramref name="degrees"/> past straight.</summary>
    protected static void Hyperextend(KillSequenceRig rig, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, float degrees, float amount = 1f)
    {
        Transform a = rig.Bone(upper), b = rig.Bone(lower), c = rig.Bone(end);
        if (a == null || b == null || c == null || amount <= 0f) return;
        Vector3 u = b.position - a.position, l = c.position - b.position;
        Vector3 hinge = Vector3.Cross(u, l);
        if (hinge.sqrMagnitude < 1e-8f) hinge = rig.killer.right;
        float bend = Vector3.Angle(u, l);
        b.rotation = Quaternion.AngleAxis(-(bend + degrees) * amount, hinge.normalized) * b.rotation;
    }

    /// <summary>A juddering fit: every joint shakes about its pose, elbows and knees hardest.</summary>
    protected static void Spasm(KillSequenceRig rig, float t, float amount, float rate = 26f)
    {
        if (amount <= 0f) return;
        HumanBodyBones[] bones =
        {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Spine,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        };
        float[] weight = { 1f, 0.6f, 0.25f, 0.2f, 0.45f, 1f, 1f, 0.45f, 1f, 1f, 0.25f, 0.7f, 0.25f, 0.7f };
        float ct = t * rate;
        for (int i = 0; i < bones.Length; i++)
        {
            Transform b = rig.Bone(bones[i]);
            if (b == null) continue;
            float p = i * 1.37f;
            Vector3 e = new Vector3(Mathf.Sin(ct + p) + 0.6f * Mathf.Sin(ct * 2.3f + p * 1.7f),
                                    Mathf.Sin(ct * 1.4f + p * 2.1f) + 0.6f * Mathf.Sin(ct * 3.1f + p),
                                    Mathf.Sin(ct * 1.7f + p * 0.7f) + 0.6f * Mathf.Sin(ct * 2.9f + p * 1.3f)) * 20f;
            e += new Vector3(Jitter(t, 30f, i), Jitter(t, 30f, i + 40), Jitter(t, 30f, i + 80)) * 14f;
            b.localRotation = b.localRotation * Quaternion.Euler(e * weight[i] * amount);
        }
    }

    /// <summary>The head shaking side to side faster than the eye can follow (a new angle every frame).</summary>
    protected static void Vibrate(KillSequenceRig rig, Vector3 cam, float t, float amount)
    {
        if (amount <= 0f) return;
        float yaw = (Mathf.Sin(t * 2f * Mathf.PI * 19f) * 32f + Jitter(t, 60f, 31) * 22f) * amount;
        float roll = (Mathf.Sin(t * 2f * Mathf.PI * 23f + 1f) * 18f + Jitter(t, 60f, 32) * 18f) * amount;
        float pitch = Jitter(t, 60f, 33) * 10f * amount;
        rig.HeadTurn(cam, roll, yaw, pitch, 0.25f);
    }
}
