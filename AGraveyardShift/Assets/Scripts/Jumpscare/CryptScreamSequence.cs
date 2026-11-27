using UnityEngine;

/// <summary>
/// What the three crypt fiends' kills share. Each is a screamer: on the frame the fiend catches you it is on you (the
/// flash, the impact and the scream land together), it lunges and shakes at the lens for about a second with its head,
/// shoulders and arms in the picture, the picture cuts dead, and a short after-effect follows.
///
/// KillerJumpscare plays these (its Scripted Kill field). A crypt death respawns the player in the same scene, so
/// nothing a sequence does may outlive it: whatever it spawns goes through rig.Keep, whatever it changes on the killer
/// or the torch it puts back in End, and KillerJumpscare restores the camera and clears the screen effects.
/// </summary>
public abstract class CryptScreamSequence : KillSequence
{
    [Header("Staging")]
    [Tooltip("The field of view the kill is framed at.")]
    public float fieldOfView = 60f;
    [Tooltip("Clear space the kill needs in front of you (m). When a wall is nearer, KillerJumpscare turns you toward open space. 0 = none needed.")]
    public float clearAhead = 1.8f;
    [Tooltip("Clear space the kill needs behind you (m). 0 = none needed.")]
    public float clearBehind = 0f;

    [Header("Shaders (referenced here so builds include them)")]
    [Tooltip("Sprites/Default: pictures fixed to the lens.")]
    public Shader overlayShader;
    [Tooltip("Standard: props.")]
    public Shader litShader;
    [Tooltip("Legacy Shaders/Particles/Additive: a torch's lens glare.")]
    public Shader additiveShader;

    /// <summary>One frame at 30 a second: how long a flash or a snap is held.</summary>
    protected const float Snap = 0.034f;

    /// <summary>Smoothstep from e0 to e1.</summary>
    protected static float S(float e0, float e1, float x) { return InOut(Seg(x, e0, e1)); }

    // ------------------------------------------------------------------ the torch

    /// <summary>A kill's own state (kept on the rig: the asset is shared). Sequences derive theirs from it.</summary>
    protected class ScreamState
    {
        public LightRenderMode torchMode;
        public Vector3 headScale = Vector3.one;
    }

    /// <summary>Call first in Begin with the kill's state: the kill is framed at its own field of view, and the torch
    /// is its light.</summary>
    protected void Frame(KillSequenceRig rig, ScreamState state)
    {
        rig.state = state;
        rig.baseFov = fieldOfView;
        if (rig.head != null) state.headScale = rig.head.localScale;
        if (rig.torch == null) return;
        state.torchMode = rig.torch.renderMode;
        // Always a per-pixel light, whatever the quality setting's light count: the whole kill is lit by it.
        rig.torch.renderMode = LightRenderMode.ForcePixel;
    }

    /// <summary>Hands the killer's head and the torch back as the rig found them (SimpleFlashlight sets its own beam
    /// again when it wakes).</summary>
    public override void End(KillSequenceRig rig)
    {
        var state = rig.state as ScreamState;
        if (state == null) return;
        if (rig.head != null) rig.head.localScale = state.headScale;
        rig.fx.hold = false;
        if (rig.torch == null) return;
        rig.torch.cookie = null;
        rig.torch.shadows = LightShadows.None;
        rig.torch.renderMode = state.torchMode;
    }

    /// <summary>The torch as the kill's own light: plain (no beam pattern, no shadows), from a point, at a point.</summary>
    protected static void KillLight(KillSequenceRig rig, float intensity, Vector3 from, Vector3 at, float angle)
    {
        if (rig.torch == null) return;
        rig.torch.cookie = null;
        rig.torch.shadows = LightShadows.None;
        rig.Torch(intensity, from, at, 30f, angle);
    }

    /// <summary>The torch as a torch: its soft beam and hard shadows, held or stood somewhere.</summary>
    protected static void TorchBeam(KillSequenceRig rig, float intensity, Vector3 from, Vector3 at, float angle)
    {
        if (rig.torch == null) return;
        rig.torch.cookie = rig.torchCookie;
        rig.torch.shadows = LightShadows.Hard;
        rig.torch.shadowStrength = 1f;
        rig.torch.shadowBias = 0.03f;
        rig.torch.shadowNormalBias = 0.3f;
        rig.torch.shadowNearPlane = 0.1f;
        rig.Torch(intensity, from, at, 45f, angle);
    }

    protected static void TorchOff(KillSequenceRig rig)
    {
        rig.Torch(0f, rig.Eye(), rig.Eye() + rig.forward);
    }

    // ------------------------------------------------------------------ limbs

    /// <summary>A hand to a point, the elbow toward the pole.</summary>
    protected static void Arm(KillSequenceRig rig, bool left, Vector3 target, Vector3 pole, float weight = 1f)
    {
        rig.Reach(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, target, weight, pole);
    }

    /// <summary>Wrist to fingers along a direction.</summary>
    protected static void Hand(KillSequenceRig rig, bool left, Vector3 dir, float amount = 1f)
    {
        HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        Transform finger = rig.Bone(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        if (finger == null) finger = rig.Bone(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        if (finger != null) rig.AimBone(hand, finger.position, dir, amount);
    }

    /// <summary>Bends the three spine bones by this many degrees each about an axis.</summary>
    protected static void Spine(KillSequenceRig rig, Vector3 axis, float degreesEach)
    {
        rig.Bend(HumanBodyBones.Spine, axis, degreesEach);
        rig.Bend(HumanBodyBones.Chest, axis, degreesEach);
        rig.Bend(HumanBodyBones.UpperChest, axis, degreesEach);
    }

    /// <summary>Turns the head so the face points at a target, the neck taking <paramref name="neckShare"/> of the turn.</summary>
    protected static void Stare(KillSequenceRig rig, Vector3 target, float amount, float neckShare)
    {
        if (amount <= 0f) return;
        Quaternion q = Quaternion.FromToRotation(rig.FaceDirection, (target - rig.head.position).normalized);
        if (rig.neck != null) rig.neck.rotation = Quaternion.Slerp(Quaternion.identity, q, neckShare * amount) * rig.neck.rotation;
        q = Quaternion.FromToRotation(rig.FaceDirection, (target - rig.head.position).normalized);
        rig.head.rotation = Quaternion.Slerp(Quaternion.identity, q, amount) * rig.head.rotation;
    }

    // ------------------------------------------------------------------ the hit

    /// <summary>How your eyes take the hit: shake and roll (degrees), the roll and the pull-back (m) and the zoom
    /// (degrees) of the first frames, and the sway (m).</summary>
    protected struct Shake
    {
        public float turn, roll, rollHit, sway, kick, zoom;
    }

    /// <summary>Your eyes during the scream: at <paramref name="eye"/>, looking along fw, shaken. hit = 1 on the catch frame, falling fast.</summary>
    protected static void ShakenCamera(KillSequenceRig rig, Vector3 eye, Vector3 fw, Vector3 up, float s, float hit, Shake k)
    {
        Vector3 rt = Vector3.Cross(up, fw).normalized;
        Quaternion rot = Quaternion.LookRotation(fw, up) * Quaternion.Euler(k.turn * Jitter(s, 30f, 5f), k.turn * Jitter(s, 30f, 6f), k.roll * Jitter(s, 30f, 7f) + k.rollHit * hit);
        rig.cam.SetPositionAndRotation(eye + rt * (k.sway * Jitter(s, 30f, 8f)) + up * (k.sway * Jitter(s, 30f, 9f)) - fw * (k.kick * hit), rot);
        rig.camera.fieldOfView = rig.baseFov - k.zoom * hit + 1.2f * Jitter(s, 30f, 10f);
    }

    /// <summary>The tape tearing under the scream: everything on the first frames, then in bursts (more of them the lower
    /// <paramref name="burstAbove"/> is), with a white flash on the catch frame.</summary>
    protected static void TapeHit(KillSequenceRig rig, float s, float quiet, float burstAbove, float rgb, float flash)
    {
        rig.SetVhs(s < 0.07f ? 1f : (quiet + (Jitter(s, 10f, 12f) > burstAbove ? 0.5f : 0f)), -1f, rgb);
        if (s < Snap) { rig.fx.flashColor = Color.white; rig.fx.flash = flash; }
    }

    protected static void Black(KillSequenceRig rig)
    {
        rig.fx.flashColor = Color.black;
        rig.fx.flash = 1f;
    }

    // ------------------------------------------------------------------ props

    /// <summary>A picture for the front of the lens, drawn in the scene so the tape treats it as part of the picture.
    /// It keeps the texture's shape and covers the frame at any aspect; "over" is how much bigger than the frame it is
    /// at 16:9. Place it each frame with <see cref="PlaceLens"/>.</summary>
    protected Renderer LensQuad(KillSequenceRig rig, string name, Texture texture, float distance, float over)
    {
        GameObject q = rig.Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
        q.name = name;
        Collider col = q.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        float h = 2f * distance * Mathf.Tan(rig.baseFov * 0.5f * Mathf.Deg2Rad) * over;
        float w = h * Mathf.Max(rig.camera.aspect, 0.1f);
        float shape = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 16f / 9f;
        float qh = Mathf.Max(h, w / shape);
        q.transform.localScale = new Vector3(qh * shape, qh, 1f);
        Shader shader = overlayShader != null ? overlayShader : Shader.Find("Sprites/Default");
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = rig.Keep(new Material(shader) { mainTexture = texture });
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        q.SetActive(false);
        return r;
    }

    /// <summary>Puts a lens picture in front of the camera as it stands now: call after the camera is set this frame.</summary>
    protected static void PlaceLens(KillSequenceRig rig, Renderer q, float distance, float offsetX = 0f, float offsetY = 0f, float roll = 0f)
    {
        q.transform.SetPositionAndRotation(rig.cam.position + rig.cam.rotation * new Vector3(offsetX, offsetY, distance), rig.cam.rotation * Quaternion.Euler(0f, 0f, roll));
    }

    protected static Light PointLight(KillSequenceRig rig, string name, Color color)
    {
        Light l = rig.Keep(new GameObject(name, typeof(Light))).GetComponent<Light>();
        l.type = LightType.Point;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        l.color = color;
        l.enabled = false;
        return l;
    }
}
