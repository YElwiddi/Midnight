using UnityEngine;

/// <summary>
/// The Spirit's kill (ending I), "Lights Out". Her arrival blows out the torch and every lantern. Each time
/// the torch sputters back on she is closer and twisted into a new shape, and the dark gaps get shorter. On
/// the last one the torch and every red lantern snap back on with her at your face, then a smash cut to black.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Spirit Lights Out", fileName = "SpiritLightsOut")]
public class SpiritLightsOutSequence : KillSequence
{
    [System.Serializable]
    public class Flicker
    {
        [Tooltip("When the torch sputters on (seconds after the kill starts).")]
        public float at;
        [Tooltip("How long it stays on (seconds).")]
        public float length = 0.08f;
        [Tooltip("Her face's distance from the camera (m).")]
        public float distance = 2f;
        [Tooltip("The arms-up pose instead of the reaching one.")]
        public bool armsUp;
        [Tooltip("Seconds into the pose's clip.")]
        public float poseTime;
        [Tooltip("Head tilt toward a shoulder (degrees).")]
        public float headRoll;
        [Tooltip("Head pitch (degrees; negative tips it back).")]
        public float headPitch;
        [Tooltip("Lift off the ground (m).")]
        public float rise;
    }

    [Header("Animation")]
    [Tooltip("SpiritWalk")]
    public AnimationClip walk;
    [Tooltip("Stumble: the long arms reaching")]
    public AnimationClip reach;
    [Tooltip("SpiritKill: arms up")]
    public AnimationClip armsUp;

    [Header("Timing")]
    [Tooltip("Where she is (m) when the lights die. Keep it equal to the event's killStopDistance.")]
    public float startDistance = 4f;
    public Flicker[] flickers =
    {
        new Flicker { at = 0.28f, length = 0.10f, distance = 2.7f, poseTime = 0.05f, headRoll = -38f },
        new Flicker { at = 0.62f, length = 0.08f, distance = 1.7f, poseTime = 0.55f, headRoll = 45f, rise = 0.3f },
        new Flicker { at = 0.88f, length = 0.07f, distance = 1.05f, armsUp = true, poseTime = 0.4f, headRoll = 15f, headPitch = -35f, rise = 0.1f },
    };
    [Tooltip("The torch and lanterns come back on with her at your face.")]
    public float lightsBack = 1.06f;
    [Tooltip("Her face's distance from the camera when the lights come back (m).")]
    public float finalDistance = 0.46f;
    [Tooltip("Smash cut to black.")]
    public float cut = 1.95f;
    [Tooltip("Black before the ending screen.")]
    public float blackHold = 0.3f;

    public override float Duration => cut + blackHold;

    public override void Begin(KillSequenceRig rig)
    {
        rig.UseClips(eyeUp, eyeForward, ("walk", walk), ("reach", reach), ("armsUp", armsUp));
    }

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        rig.BeginFrame();
        Vector3 cam = rig.Eye();
        int on = -1;
        for (int i = 0; i < flickers.Length; i++)
        {
            if (t >= flickers[i].at && t < flickers[i].at + flickers[i].length) on = i;
        }
        rig.Lanterns(t >= lightsBack);
        Vector3 dark = rig.At(2.6f) + Vector3.up * 1.9f;
        Vector3 look;
        if (t >= lightsBack)
        {
            float hold = Seg(t, lightsBack, cut);
            cam -= rig.forward * Kick(t, lightsBack, 0.1f, 4f, 8f);
            rig.Visible(true);
            rig.Place(rig.Ground(rig.At(1.2f)) + Vector3.up * 0.1f, -rig.forward);
            rig.Pose(("reach", 0.7f + 0.1f * hold, 1f));
            rig.LeanTo(cam, 0.7f);
            rig.Stare(cam, 1f);
            rig.PlaceFace(cam, finalDistance - 0.06f * hold);
            rig.HeadTurn(cam, 18f * Noise(t * 24f, 3) + 7f * Jitter(t, 30f, 4), 8f * Noise(t * 21f, 5), 5f * Noise(t * 26f, 6));
            look = rig.Face;
        }
        else if (on >= 0)
        {
            Flicker f = flickers[on];
            rig.Visible(true);
            rig.Place(rig.Ground(rig.At(f.distance + 0.3f)) + Vector3.up * f.rise, -rig.forward);
            rig.Pose((f.armsUp ? "armsUp" : "reach", f.poseTime, 1f));
            rig.Stare(cam, 1f);
            rig.PlaceFace(cam, f.distance);
            rig.HeadTurn(cam, f.headRoll, 0f, f.headPitch);
            look = Vector3.Lerp(dark, rig.Face, 0.45f);
        }
        else
        {
            // She moves while you can't see her.
            rig.Visible(false);
            rig.Place(rig.Ground(rig.At(startDistance)), -rig.forward);
            rig.Pose(("walk", 0.2f, 1f));
            look = Vector3.Lerp(rig.startLook, dark, Out3(Seg(t, 0f, 0.25f)));
        }

        float tremble = Mathf.Lerp(0.5f, 1.8f, Seg(t, 0f, lightsBack));
        float pitch = Noise(t * 6f, 1) * tremble + Kick(t, lightsBack, -6f, 5f, 7f);
        float yaw = Noise(t * 5f, 2) * tremble * 1.2f;
        for (int i = 0; i < flickers.Length; i++) yaw += Kick(t, flickers[i].at, i % 2 == 0 ? 1.6f : -1.6f, 6f, 12f);
        float roll = Kick(t, lightsBack, 4f, 4f, 6f);
        rig.SetCamera(cam, look, t >= lightsBack ? rig.baseFov - 6f : rig.baseFov, pitch, yaw, roll);

        Vector3 hand = cam + Vector3.down * 0.3f;
        if (t >= lightsBack) rig.Torch((2.4f + 1.4f * Mathf.Exp(-(t - lightsBack) * 7f)) * (0.85f + 0.15f * Noise(t * 30f, 9)), hand, rig.Face);
        else if (on >= 0) rig.Torch(2.3f + 0.6f * Jitter(t, 60f, on), hand, rig.Face);
        else rig.Torch(0f, hand, dark);

        if (t >= lightsBack) rig.SetVhs(t < lightsBack + 0.083f ? 0.85f : (Spike(t, 6f, 0.3f, 2f) ? 0.7f : 0.2f), 0.06f, 0.008f, 0.05f);
        else if (on >= 0) rig.SetVhs(0.35f, 0.05f);
        rig.fx.split = t >= lightsBack ? 0.009f * Mathf.Exp(-(t - lightsBack) * 9f) : 0f;
        if (t >= cut)
        {
            rig.fx.flashColor = Color.black;
            rig.fx.flash = 1f;
        }
    }
}
