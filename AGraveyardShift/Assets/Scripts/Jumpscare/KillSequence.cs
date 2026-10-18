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
}
