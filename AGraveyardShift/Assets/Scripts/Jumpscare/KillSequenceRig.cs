using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// What a KillSequence works with for one kill: the player's camera and torch, the killer, the lanterns, the
/// VHS and screen effects, plus helpers that pose the killer from its own clips and place it by where its
/// FACE lands (placing by the root put a leaning killer's head inside the camera).
/// </summary>
public class KillSequenceRig
{
    public Camera camera;
    public Transform cam;
    public Transform player;
    /// <summary>The killer's root; the sequence moves and turns it.</summary>
    public Transform killer;
    public Animator animator;
    /// <summary>The player's feet when the kill started.</summary>
    public Vector3 ground;
    /// <summary>Horizontal direction the kill is staged along (away from the player).</summary>
    public Vector3 forward;
    public Vector3 right;
    /// <summary>Eye height above <see cref="ground"/>.</summary>
    public float eye = 1.74f;
    /// <summary>A point the camera was looking at when the kill started.</summary>
    public Vector3 startLook;
    public float baseFov = 60f;
    public Light torch;
    public readonly List<Light> lanterns = new List<Light>();
    public VHSRetroFeature vhs;
    public JumpscareScreenFX fx;
    /// <summary>The sequence's own per-kill state (the sequence asset is shared, so it can't hold any).</summary>
    public object state;

    public Transform head, neck, spine, chest, headTop;

    private readonly List<Object> spawned = new List<Object>();
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private readonly Dictionary<string, int> clipIndex = new Dictionary<string, int>();
    private readonly List<AnimationClipPlayable> clipInputs = new List<AnimationClipPlayable>();
    private Vector3 faceLocal;
    private float eyeUp = 0.42f, eyeForward = 0.05f;
    private float baseGlitch, baseRgb, baseScan, baseTrack, baseDrop;

    // ------------------------------------------------------------------ setup

    /// <summary>Builds the rig for a kill in the running game. The killer is staged along <paramref name="stageForward"/>.</summary>
    public static KillSequenceRig Create(Transform killerRoot, Camera playerCamera, Transform player, Vector3 stageForward, Shader screenFxShader)
    {
        var rig = new KillSequenceRig
        {
            camera = playerCamera,
            cam = playerCamera.transform,
            player = player,
            killer = killerRoot,
            animator = killerRoot.GetComponentInChildren<Animator>(),
            baseFov = playerCamera.fieldOfView,
        };
        Vector3 from = player != null ? player.position : rig.cam.position;
        rig.ground = rig.cam.position - Vector3.up * 1.74f;
        if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            rig.ground = hit.point;
        // the camera's real height (about 2.1 m standing); clamped only against a mid-jump or odd ground hit
        rig.eye = Mathf.Clamp(rig.cam.position.y - rig.ground.y, 1.0f, 2.8f);
        rig.SetForward(stageForward);
        rig.startLook = rig.cam.position + rig.cam.forward * 5f;

        // The torch: SimpleFlashlight stops steering the spot while the sequence does.
        SimpleFlashlight flashlight = Object.FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null)
        {
            flashlight.enabled = false;
            rig.torch = flashlight.spotLight;
        }
        foreach (LanternInteractable lantern in Object.FindObjectsOfType<LanternInteractable>())
        {
            if (lantern.lanternLight != null) rig.lanterns.Add(lantern.lanternLight);
        }
        rig.vhs = playerCamera.GetComponent<VHSRetroFeature>();
        rig.fx = playerCamera.GetComponent<JumpscareScreenFX>();
        if (rig.fx == null) rig.fx = playerCamera.gameObject.AddComponent<JumpscareScreenFX>();
        if (screenFxShader != null) rig.fx.shader = screenFxShader;
        rig.Init();
        return rig;
    }

    /// <summary>Builds a rig around a staged killer and a free camera, for rendering a sequence offline.</summary>
    public static KillSequenceRig ForPreview(Transform killerRoot, Camera camera, Light torch, IEnumerable<Light> lanterns,
        Vector3 ground, Vector3 forward, float eye, JumpscareScreenFX fx)
    {
        var rig = new KillSequenceRig
        {
            camera = camera,
            cam = camera.transform,
            killer = killerRoot,
            animator = killerRoot.GetComponentInChildren<Animator>(),
            ground = ground,
            eye = eye,
            baseFov = camera.fieldOfView,
            torch = torch,
            vhs = camera.GetComponent<VHSRetroFeature>(),
            fx = fx,
        };
        rig.lanterns.AddRange(lanterns);
        rig.SetForward(forward);
        rig.startLook = rig.cam.position + rig.cam.forward * 5f;
        rig.Init();
        return rig;
    }

    private void Init()
    {
        camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, 0.05f);
        // NPCAntiPush pins the killer back to its navmesh anchor every LateUpdate while the agent is on.
        NavMeshAgent agent = killer.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        NPCAntiPush antiPush = killer.GetComponent<NPCAntiPush>();
        if (antiPush != null) antiPush.enabled = false;
        if (vhs != null)
        {
            baseGlitch = vhs.glitchIntensity; baseRgb = vhs.rgbShiftAmount; baseScan = vhs.scanlineIntensity;
            baseTrack = vhs.trackingNoise; baseDrop = vhs.dropoutIntensity;
        }
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        headTop = FindChild(killer, "mixamorig:HeadTop_End");
        faceLocal = RestFaceLocal(animator, head);
    }

    private void SetForward(Vector3 f)
    {
        f.y = 0f;
        forward = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        right = Vector3.Cross(Vector3.up, forward);
    }

    // The head's face direction in its own space, from the avatar's rest pose, so it doesn't matter what pose
    // the killer is caught in when the kill starts.
    private static Vector3 RestFaceLocal(Animator animator, Transform head)
    {
        var rest = new Dictionary<string, Quaternion>();
        if (animator.avatar != null)
        {
            foreach (SkeletonBone bone in animator.avatar.humanDescription.skeleton) rest[bone.name] = bone.rotation;
        }
        if (rest.Count == 0) return Quaternion.Inverse(head.rotation) * animator.transform.forward;
        Quaternion rotation = Quaternion.identity;
        for (Transform t = head; t != null && t != animator.transform; t = t.parent)
            rotation = (rest.TryGetValue(t.name, out Quaternion r) ? r : t.localRotation) * rotation;
        return Quaternion.Inverse(rotation) * Vector3.forward;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    // ------------------------------------------------------------------ animation

    /// <summary>Takes the killer's animation over with these clips, blended by <see cref="Pose"/>.</summary>
    public void UseClips(float faceEyeUp, float faceEyeForward, params (string key, AnimationClip clip)[] clips)
    {
        eyeUp = faceEyeUp;
        eyeForward = faceEyeForward;
        graph = PlayableGraph.Create("KillSequence");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Kill", animator);
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            var playable = AnimationClipPlayable.Create(graph, clips[i].clip);
            playable.SetApplyFootIK(false);
            graph.Connect(playable, 0, mixer, i);
            clipInputs.Add(playable);
            clipIndex[clips[i].key] = i;
        }
        output.SetSourcePlayable(mixer);
        graph.Play();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
    }

    /// <summary>Poses the killer from a blend of its clips: (clip key, seconds into the clip, weight).</summary>
    public void Pose(params (string clip, float time, float weight)[] layers)
    {
        for (int i = 0; i < clipInputs.Count; i++) mixer.SetInputWeight(i, 0f);
        float sum = 0f;
        foreach (var layer in layers) sum += Mathf.Max(0f, layer.weight);
        foreach (var layer in layers)
        {
            int i = clipIndex[layer.clip];
            AnimationClip clip = clipInputs[i].GetAnimationClip();
            clipInputs[i].SetTime(clip.isLooping ? Mathf.Repeat(layer.time, clip.length) : Mathf.Clamp(layer.time, 0f, clip.length));
            mixer.SetInputWeight(i, sum > 0f ? Mathf.Max(0f, layer.weight) / sum : 0f);
        }
        graph.Evaluate(0f);
    }

    // ------------------------------------------------------------------ staging

    public Vector3 At(float distance, float side = 0f) => ground + forward * distance + right * side;
    public Vector3 Eye() => ground + Vector3.up * eye;

    /// <summary>The ground under <paramref name="p"/> (ignores the killer, the player and invisible barriers).</summary>
    public Vector3 Ground(Vector3 p)
    {
        var origin = new Vector3(p.x, ground.y + 2.5f, p.z);
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(killer)) continue;
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            string n = h.collider.name.ToLowerInvariant();
            if (n.Contains("donotpass") || n.Contains("barrier") || n.Contains("invisible")) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? new Vector3(p.x, ground.y, p.z) : new Vector3(p.x, best, p.z);
    }

    public void Place(Vector3 position, Vector3 faceDirection)
    {
        faceDirection.y = 0f;
        if (faceDirection.sqrMagnitude < 1e-6f) faceDirection = -forward;
        killer.SetPositionAndRotation(position, Quaternion.LookRotation(faceDirection.normalized));
    }

    public Vector3 FaceDirection => head.rotation * faceLocal;

    /// <summary>Between the killer's eyes.</summary>
    public Vector3 Face
    {
        get
        {
            Vector3 up = headTop != null ? headTop.position - head.position : Vector3.up * 0.3f;
            return head.position + up * eyeUp + FaceDirection * eyeForward;
        }
    }

    /// <summary>Slides the killer along <see cref="forward"/> so its face ends up <paramref name="distance"/> m in front of <paramref name="camPos"/>.</summary>
    public void PlaceFace(Vector3 camPos, float distance)
    {
        Vector3 f = Face - camPos;
        f.y = 0f;
        killer.position += forward * (distance - Vector3.Dot(f, forward));
    }

    /// <summary>Turns the head (and some neck) so the face points at <paramref name="target"/>.</summary>
    public void Stare(Vector3 target, float amount)
    {
        if (amount <= 0f) return;
        Quaternion q = Quaternion.FromToRotation(FaceDirection, (target - head.position).normalized);
        if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.35f * amount) * neck.rotation;
        q = Quaternion.FromToRotation(FaceDirection, (target - head.position).normalized);
        head.rotation = Quaternion.Slerp(Quaternion.identity, q, amount) * head.rotation;
    }

    /// <summary>Turns the head in world space: roll about the line of sight, yaw about up, pitch about the cross (degrees).</summary>
    public void HeadTurn(Vector3 camPos, float roll, float yaw, float pitch, float neckShare = 0.4f)
    {
        Vector3 view = (camPos - head.position).normalized;
        Vector3 across = Vector3.Cross(Vector3.up, view).normalized;
        Quaternion q = Quaternion.AngleAxis(roll, view) * Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, across);
        if (neck != null && neckShare > 0f)
        {
            neck.rotation = Quaternion.Slerp(Quaternion.identity, q, neckShare) * neck.rotation;
            q = Quaternion.Slerp(Quaternion.identity, q, 1f - neckShare);
        }
        head.rotation = q * head.rotation;
    }

    /// <summary>Bends the torso so the head leans toward <paramref name="target"/> (0..1).</summary>
    public void LeanTo(Vector3 target, float amount)
    {
        foreach (Transform bone in new[] { spine, chest })
        {
            if (bone == null) continue;
            Quaternion bend = Quaternion.FromToRotation(head.position - bone.position, target - bone.position);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, bend, amount * 0.5f) * bone.rotation;
        }
    }

    public void Visible(bool on)
    {
        foreach (Renderer r in killer.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    // ------------------------------------------------------------------ camera, light, picture

    /// <summary>Resets the VHS to its pre-kill settings and the screen effects to pass-through (keeping held frames, overlays and dirt).</summary>
    public void BeginFrame()
    {
        if (vhs != null)
        {
            vhs.glitchIntensity = baseGlitch; vhs.rgbShiftAmount = baseRgb; vhs.scanlineIntensity = baseScan;
            vhs.trackingNoise = baseTrack; vhs.dropoutIntensity = baseDrop;
        }
        bool hold = fx.hold;
        Texture overlay = fx.overlay;
        Rect overlayRect = fx.overlayRect;
        fx.Clear();
        fx.hold = hold;
        fx.overlay = overlay;
        fx.overlayRect = overlayRect;
    }

    /// <summary>Camera at <paramref name="pos"/> looking at <paramref name="look"/>, then turned by pitch/yaw/roll (degrees).</summary>
    public void SetCamera(Vector3 pos, Vector3 look, float fov, float pitch = 0f, float yaw = 0f, float roll = 0f)
    {
        Vector3 dir = look - pos;
        if (dir.sqrMagnitude < 1e-6f) dir = forward;
        cam.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch, yaw, roll));
        camera.fieldOfView = fov;
    }

    /// <summary>The torch, held at <paramref name="from"/> and aimed at <paramref name="at"/>. Intensity 0 = off.</summary>
    public void Torch(float intensity, Vector3 from, Vector3 at, float range = 15f, float angle = 60f)
    {
        if (torch == null) return;
        torch.enabled = intensity > 0.001f;
        if (!torch.enabled) return;
        torch.intensity = intensity;
        torch.range = range;
        torch.spotAngle = angle;
        Vector3 dir = at - from;
        torch.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : forward));
    }

    public void Lanterns(bool on)
    {
        foreach (Light l in lanterns) if (l != null) l.enabled = on;
    }

    /// <summary>VHS glitch, tracking noise, RGB split and dropout this frame (negative = keep the pre-kill value).</summary>
    public void SetVhs(float glitch, float tracking, float rgb = -1f, float dropout = -1f)
    {
        if (vhs == null) return;
        if (glitch >= 0f) vhs.glitchIntensity = glitch;
        if (tracking >= 0f) vhs.trackingNoise = tracking;
        if (rgb >= 0f) vhs.rgbShiftAmount = rgb;
        if (dropout >= 0f) vhs.dropoutIntensity = dropout;
    }

    // ------------------------------------------------------------------ props

    /// <summary>Remembers something the sequence spawned, so <see cref="Dispose"/> can remove it.</summary>
    public T Keep<T>(T obj) where T : Object
    {
        spawned.Add(obj);
        return obj;
    }

    /// <summary>Removes the sequence's props and animation graph (an offline render; the game's scene ends instead).</summary>
    public void Dispose()
    {
        if (graph.IsValid()) graph.Destroy();
        foreach (Object o in spawned)
        {
            if (o == null) continue;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
        spawned.Clear();
    }
}
