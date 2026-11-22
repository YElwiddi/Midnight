using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A watcher's eyes: two small glints that only show when a light sitting at the viewer falls on its face, the
/// way an animal's eyes answer a torch. For the player that light is the flashlight; for a CCTV feed it is the
/// feed's own lamp. Each camera gets its own answer just before it renders (built-in pipeline per-camera
/// callbacks), so the player and a feed can disagree about whether the eyes are lit.
///
/// Up close the glints sit where the eyes are, at their true size. Far away they are sized in picture pixels
/// instead of metres: the game draws a coarse picture (the tape, or the old 320x240 grid), and a true-to-scale
/// eye would be lost in it past a few metres.
/// </summary>
[DisallowMultipleComponent]
public class WatcherEyes : MonoBehaviour
{
    [Header("Glint")]
    [Tooltip("Material using Custom/WatcherEyeGlint. Assigned on the prefab so builds include the shader.")]
    public Material glintMaterial;
    public Color glintColor = new Color(1f, 0.93f, 0.74f, 1f);
    [Range(0f, 3f)] public float brightness = 1.1f;

    [Header("Where the eyes are (metres from the head joint, measured in the rest pose)")]
    [Tooltip("Up from the head joint to eye level.")]
    public float eyeHeight = 0.125f;
    [Tooltip("Forward from the head joint to the surface of the face.")]
    public float eyeDepth = 0.26f;
    [Tooltip("Half the distance between the eyes.")]
    public float eyeHalfSpacing = 0.055f;
    [Tooltip("How far the glints sit off the face, toward whoever is looking, so the head never swallows them.")]
    public float clearance = 0.14f;

    [Header("When they show")]
    [Tooltip("The eyes catch a spot light while they are within this many degrees of its axis.")]
    [Range(1f, 60f)] public float coneAngle = 22f;
    [Tooltip("...and are at full strength within this many degrees.")]
    [Range(0f, 30f)] public float fullAngle = 9f;
    [Tooltip("Past this distance the eyes are not drawn at all.")]
    public float maxDistance = 45f;
    [Tooltip("How squarely the face must be turned to the viewer: x = eyes start to show, y = full strength (1 = looking straight at you).")]
    public Vector2 facingRange = new Vector2(0.15f, 0.6f);

    [Header("Size")]
    [Tooltip("True size of one glint, in metres: x wide, y tall.")]
    public Vector2 trueSize = new Vector2(0.065f, 0.05f);
    [Tooltip("Smallest size on the old 320x240 pixel picture, in its pixels: x wide, y tall, z gap between the two centres.")]
    public Vector3 classicPixels = new Vector3(1.5f, 1.5f, 2.1f);
    [Tooltip("Smallest size on the tape picture, in tape pixels: x wide, y tall, z gap between the two centres.")]
    public Vector3 tapePixels = new Vector3(3f, 2f, 5f);
    [Tooltip("Smallest size on a camera that draws to a texture (a CCTV feed), in that texture's pixels.")]
    public Vector3 feedPixels = new Vector3(3f, 2.5f, 5f);

    /// <summary>0 = shut, 1 = open. The watcher shuts them while it runs and blinks when it looks back.</summary>
    public float Open { get; set; } = 1f;

    /// <summary>Extra brightness on top of <see cref="brightness"/> (1 = none).</summary>
    public float Boost { get; set; } = 1f;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Mesh quad;
    private static SimpleFlashlight torch;
    private static float nextTorchSearch;
    private static readonly Dictionary<int, Light[]> cameraLamps = new Dictionary<int, Light[]>();
    private static readonly Dictionary<int, PSXPostProcessing> cameraGrids = new Dictionary<int, PSXPostProcessing>();

    private Transform head;
    private Vector3 headForwardLocal, headUpLocal, headRightLocal;
    private GameObject holder;
    private MeshRenderer left, right;
    private Transform leftT, rightT;
    private MaterialPropertyBlock block;
    private bool ready;

    private void Awake()
    {
        var animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) head = FindByName(transform, "Head");
        if (head == null)
        {
            Debug.LogWarning("WatcherEyes: no head bone under '" + name + "', so it has no eyes.");
            enabled = false;
            return;
        }

        // Read once, before any animation has moved the head: in the rest pose the face looks down the root's forward.
        Quaternion inv = Quaternion.Inverse(head.rotation);
        headForwardLocal = inv * transform.forward;
        headUpLocal = inv * transform.up;
        headRightLocal = inv * transform.right;

        if (glintMaterial == null)
        {
            var shader = Shader.Find("Custom/WatcherEyeGlint");
            if (shader != null) glintMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (glintMaterial == null)
        {
            Debug.LogWarning("WatcherEyes: no glint material on '" + name + "'.");
            enabled = false;
            return;
        }

        if (quad == null) quad = BuildQuad();

        // Not parented to the watcher: its root is scaled and leans, and the glints are placed in world space anyway.
        holder = new GameObject("WatcherEyes (" + name + ")") { hideFlags = HideFlags.DontSave };
        var body = GetComponentInChildren<Renderer>(true);
        int layer = body != null ? body.gameObject.layer : gameObject.layer;
        left = MakeGlint("Left", layer, out leftT);
        right = MakeGlint("Right", layer, out rightT);
        block = new MaterialPropertyBlock();
        ready = true;
    }

    private MeshRenderer MakeGlint(string glintName, int layer, out Transform t)
    {
        var go = new GameObject(glintName) { hideFlags = HideFlags.DontSave, layer = layer };
        go.transform.SetParent(holder.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = glintMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enabled = false;
        t = go.transform;
        return r;
    }

    private static Mesh BuildQuad()
    {
        var m = new Mesh { name = "WatcherEyeQuad", hideFlags = HideFlags.HideAndDontSave };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    private static Transform FindByName(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
        return null;
    }

    private void OnEnable()
    {
        if (!ready) return;
        if (holder != null) holder.SetActive(true);
        Camera.onPreCull += HandlePreCull;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;
        if (left != null) left.enabled = false;
        if (right != null) right.enabled = false;
        if (holder != null) holder.SetActive(false);
    }

    private void OnDestroy()
    {
        if (holder != null) Destroy(holder);
    }

    /// <summary>The point between the eyes and the way the face is turned, as they are this frame.</summary>
    public bool TryGetFace(out Vector3 betweenEyes, out Vector3 forward)
    {
        if (!ready || head == null) { betweenEyes = transform.position; forward = transform.forward; return false; }
        forward = head.rotation * headForwardLocal;
        betweenEyes = head.position + head.rotation * headUpLocal * eyeHeight + forward * eyeDepth;
        return true;
    }

    // Called for every camera just before it culls: decide whether THIS camera sees the eyes lit, and put the
    // two glints where it would see them.
    private void HandlePreCull(Camera cam)
    {
        if (!ready || cam == null || head == null) return;

        float strength = 0f, dist = 0f;
        Vector3 anchor = default, toCam = default;
        if (Open > 0.001f && TryGetFace(out anchor, out Vector3 faceForward))
        {
            toCam = cam.transform.position - anchor;
            dist = toCam.magnitude;
            if (dist > 0.3f && dist < maxDistance)
            {
                toCam /= dist;
                float facing = Mathf.InverseLerp(facingRange.x, facingRange.y, Vector3.Dot(faceForward, toCam));
                if (facing > 0f)
                    strength = Mathf.Clamp01(Open) * facing * ViewerLight(cam, anchor) * Mathf.Clamp01((maxDistance - dist) / (maxDistance * 0.2f));
            }
        }

        bool show = strength > 0.01f;
        if (left.enabled != show) { left.enabled = show; right.enabled = show; }
        if (!show) return;

        // How big one pixel of this camera's picture is, in metres, where the eyes are.
        PictureGrid(cam, out float cols, out float rows, out Vector3 minPixels);
        float tall = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float pixelW = tall * cam.aspect / Mathf.Max(1f, cols);
        float pixelH = tall / Mathf.Max(1f, rows);

        float w = Mathf.Max(trueSize.x, pixelW * minPixels.x);
        float h = Mathf.Max(trueSize.y, pixelH * minPixels.y);
        float half = Mathf.Max(eyeHalfSpacing, pixelW * minPixels.z * 0.5f);

        // The line through both eyes, as this camera sees it (so a tilted head tilts the pair).
        Vector3 across = head.rotation * headRightLocal;
        across -= toCam * Vector3.Dot(across, toCam);
        if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(Vector3.up, toCam);
        across.Normalize();

        Vector3 centre = anchor + toCam * Mathf.Min(clearance, dist * 0.5f);
        Quaternion facingCamera = Quaternion.LookRotation(-toCam, Vector3.Cross(toCam, across));
        var scale = new Vector3(w, h, 1f);
        leftT.SetPositionAndRotation(centre - across * half, facingCamera);
        rightT.SetPositionAndRotation(centre + across * half, facingCamera);
        leftT.localScale = scale;
        rightT.localScale = scale;

        block.SetFloat(IntensityId, brightness * Mathf.Max(0f, Boost) * strength);
        block.SetColor(ColorId, glintColor);
        left.SetPropertyBlock(block);
        right.SetPropertyBlock(block);
    }

    // 0..1: how strongly a light AT this camera falls on the point. Eyes only shine back at the light's own position.
    private float ViewerLight(Camera cam, Vector3 point)
    {
        Vector3 camPos = cam.transform.position;
        float best = 0f;

        // The player's torch. SimpleFlashlight keeps its spot light on the camera it follows.
        if (torch == null && Time.unscaledTime >= nextTorchSearch)
        {
            nextTorchSearch = Time.unscaledTime + 1f;
            torch = FindFirstObjectByType<SimpleFlashlight>();
        }
        if (torch != null && torch.spotLight != null && torch.IsFlashlightOn() && torch.spotLight.enabled
            && (torch.spotLight.transform.position - camPos).sqrMagnitude < 1f)
        {
            float flicker = Mathf.Clamp01(torch.spotLight.intensity / Mathf.Max(0.01f, torch.intensity));
            best = LampFactor(torch.spotLight, point) * flicker;
        }

        // Any lamp mounted on the camera itself (each CCTV feed has one, lit only while that feed is watched).
        int id = cam.GetInstanceID();
        if (!cameraLamps.TryGetValue(id, out Light[] lamps))
        {
            lamps = cam.GetComponentsInChildren<Light>(true);
            cameraLamps[id] = lamps;
        }
        for (int i = 0; i < lamps.Length; i++)
        {
            Light lamp = lamps[i];
            if (lamp == null || !lamp.isActiveAndEnabled) continue;
            if ((lamp.transform.position - camPos).sqrMagnitude > 1f) continue;
            best = Mathf.Max(best, LampFactor(lamp, point));
        }
        return best;
    }

    private float LampFactor(Light lamp, Vector3 point)
    {
        Vector3 to = point - lamp.transform.position;
        float d = to.magnitude;
        if (d > lamp.range || d < 0.01f) return 0f;
        float reach = Mathf.Clamp01((lamp.range - d) / (lamp.range * 0.2f));
        if (lamp.type != LightType.Spot) return reach;

        float cone = Mathf.Min(coneAngle, lamp.spotAngle * 0.5f + 1f);
        float full = Mathf.Min(fullAngle, cone * 0.6f);
        float angle = Vector3.Angle(lamp.transform.forward, to / d);
        return reach * Mathf.InverseLerp(cone, full, angle);
    }

    // The coarsest grid this camera's picture is drawn on, and the smallest glint that survives it.
    private void PictureGrid(Camera cam, out float cols, out float rows, out Vector3 minPixels)
    {
        RenderTexture target = cam.targetTexture;
        if (target != null)
        {
            cols = target.width;
            rows = target.height;
            minPixels = feedPixels;
            return;
        }

        if (PictureSettings.IsTape && TapeRenderer.Supported)
        {
            TapeProfile profile = TapeProfile.Shared;
            rows = Mathf.Clamp(profile != null ? profile.lines : 480f, 120f, 1080f);
            cols = rows * cam.aspect;
            minPixels = tapePixels;
            return;
        }

        int id = cam.GetInstanceID();
        if (!cameraGrids.TryGetValue(id, out PSXPostProcessing grid))
        {
            grid = cam.GetComponent<PSXPostProcessing>();
            cameraGrids[id] = grid;
        }
        if (grid != null && grid.enabled && grid.enablePixelation)
        {
            cols = grid.widthPixelation;
            rows = grid.heightPixelation;
            minPixels = classicPixels;
            return;
        }

        cols = cam.pixelWidth;
        rows = cam.pixelHeight;
        minPixels = new Vector3(2f, 2f, 4f);
    }
}
