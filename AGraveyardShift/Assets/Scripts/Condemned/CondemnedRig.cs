using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helpers shared by the scares in this folder (Cut Down, the window reflection):
/// timing curves, pose snapshots, a two-bone reach, turning a visitor prefab into a script-free puppet,
/// a drained copy of its look, and finding the player.
/// </summary>
public static class CondemnedRig
{
    // ------------------------------------------------------------------ timing
    public static float Hash(float x) { float s = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f; return s - Mathf.Floor(s); }
    public static float Rnd(float x) { return Hash(x) * 2f - 1f; }
    public static float Seg(float t, float a, float b) { return b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a)); }
    public static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
    public static float Ease(float t, float a, float b) { return Smooth(Seg(t, a, b)); }

    // ------------------------------------------------------------------ the clock (and stand-ins for sandbox test renders)
    /// <summary>Test renders outside play mode only: a camera and player to use instead of the game's, and a fixed frame time.</summary>
    public static Camera TestCamera;
    public static GameObject TestPlayer;
    public static float TestDelta = -1f;
    static float testNow;
    public static float Dt { get { return TestDelta >= 0f ? TestDelta : Time.deltaTime; } }
    public static float Now { get { return TestDelta >= 0f ? testNow : Time.time; } }
    public static bool Paused { get { return TestDelta < 0f && Time.timeScale == 0f; } }
    public static void TestTick() { if (TestDelta >= 0f) testNow += TestDelta; }

    // ------------------------------------------------------------------ the player
    public static GameObject Player()
    {
        if (TestPlayer != null) return TestPlayer;
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null && p.activeInHierarchy ? p : null;
    }

    public static Camera PlayerCamera()
    {
        if (TestCamera != null) return TestCamera;
        var c = Camera.main;
        if (c != null) return c;
        var p = Player();
        return p != null ? p.GetComponentInChildren<Camera>() : null;
    }

    /// <summary>Is a world point on screen (inside the given viewport margins) and in front of the camera?</summary>
    public static bool OnScreen(Camera cam, Vector3 p, float marginX = 0.08f, float marginY = 0.06f)
    {
        if (cam == null) return false;
        var v = cam.WorldToViewportPoint(p);
        return v.z > 0.05f && v.x > marginX && v.x < 1f - marginX && v.y > marginY && v.y < 1f - marginY;
    }

    /// <summary>Nothing solid between the camera and a point (triggers ignored; the target's own colliders are not checked).</summary>
    public static bool ClearLine(Vector3 from, Vector3 to, float endSlack = 0.4f)
    {
        Vector3 d = to - from; float len = d.magnitude;
        if (len <= endSlack) return true;
        return !Physics.Raycast(from, d / len, len - endSlack, ~0, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ poses
    public sealed class Snapshot
    {
        public Transform[] T; public Quaternion[] R; public Vector3[] P; public Vector3[] S;
    }

    public static Snapshot Snap(GameObject g)
    {
        var s = new Snapshot { T = g.GetComponentsInChildren<Transform>(true) };
        int n = s.T.Length; s.R = new Quaternion[n]; s.P = new Vector3[n]; s.S = new Vector3[n];
        for (int i = 0; i < n; i++) { s.R[i] = s.T[i].localRotation; s.P[i] = s.T[i].localPosition; s.S[i] = s.T[i].localScale; }
        return s;
    }

    public static void Restore(Snapshot s)
    {
        if (s == null) return;
        for (int i = 0; i < s.T.Length; i++)
        {
            var t = s.T[i]; if (t == null) continue;
            t.localRotation = s.R[i]; t.localPosition = s.P[i]; t.localScale = s.S[i];
        }
    }

    public static void Turn(Transform b, Vector3 axis, float deg) { if (b != null) b.rotation = Quaternion.AngleAxis(deg, axis) * b.rotation; }

    public static void Aim(Transform bone, Transform child, Vector3 dir)
    {
        if (bone == null || child == null || dir.sqrMagnitude < 1e-8f) return;
        Vector3 cur = child.position - bone.position; if (cur.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(cur.normalized, dir.normalized) * bone.rotation;
    }

    /// <summary>Two-bone reach: the end of the chain goes to target, the middle joint bends toward pole.</summary>
    public static void Reach(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        if (upper == null || lower == null || end == null) return;
        Vector3 a = upper.position;
        float l1 = (lower.position - a).magnitude, l2 = (end.position - lower.position).magnitude;
        Vector3 d = target - a; float dist = d.magnitude; if (dist < 1e-4f) return;
        Vector3 n = d / dist;
        float dc = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.001f);
        float x = (l1 * l1 - l2 * l2 + dc * dc) / (2f * dc), h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 pd = pole - n * Vector3.Dot(pole, n);
        if (pd.sqrMagnitude < 1e-6f) pd = Vector3.Cross(n, Vector3.up);
        if (pd.sqrMagnitude < 1e-6f) pd = Vector3.Cross(n, Vector3.right);
        pd.Normalize();
        Vector3 elbow = a + n * x + pd * h;
        Aim(upper, lower, elbow - a);
        Aim(lower, end, a + n * dc - lower.position);
    }

    // ------------------------------------------------------------------ puppets
    /// <summary>
    /// Instantiates a visitor prefab without ever running its scripts (it is built under an inactive parent),
    /// then strips scripts, agent, colliders, rigidbodies and audio so it is only a posable body.
    /// </summary>
    public static GameObject SpawnPuppet(GameObject prefab, Vector3 pos, Quaternion rot, string name)
    {
        var holder = new GameObject("__puppet_holder");
        holder.SetActive(false);
        var g = Object.Instantiate(prefab, pos, rot, holder.transform);
        g.name = name;
        // scripts first, newest first, so components that require another go before it; a few passes settle the order
        for (int pass = 0; pass < 4; pass++)
        {
            var mbs = g.GetComponentsInChildren<MonoBehaviour>(true);
            if (mbs.Length == 0) break;
            for (int i = mbs.Length - 1; i >= 0; i--)
                if (mbs[i] != null && CanRemove(mbs[i])) Object.DestroyImmediate(mbs[i]);
        }
        foreach (var ag in g.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(ag);
        foreach (var ob in g.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) Object.DestroyImmediate(ob);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var a in g.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        g.transform.SetParent(null, true);
        Kill(holder);
        foreach (var s in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            s.updateWhenOffscreen = true;
            s.forceMatrixRecalculationPerRender = true;
        }
        var an = g.GetComponentInChildren<Animator>();
        if (an != null) { an.cullingMode = AnimatorCullingMode.AlwaysAnimate; an.applyRootMotion = false; }
        return g;
    }

    // a component that another component on the same object requires cannot be destroyed until that one is gone
    static bool CanRemove(Component c)
    {
        var t = c.GetType();
        foreach (var other in c.GetComponents<MonoBehaviour>())
        {
            if (other == null || other == c) continue;
            foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) || (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) || (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t)))
                    return false;
        }
        return true;
    }

    /// <summary>Destroy, or DestroyImmediate outside play mode (sandbox test renders).</summary>
    public static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    public static void SetRenderers(GameObject g, bool on)
    {
        if (g == null) return;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    public static void SetLayer(GameObject g, int layer)
    {
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public static float FeetY(Animator an)
    {
        var l = an.GetBoneTransform(HumanBodyBones.LeftFoot); var r = an.GetBoneTransform(HumanBodyBones.RightFoot);
        if (l == null || r == null) return an.transform.position.y;
        return Mathf.Min(l.position.y, r.position.y);
    }

    // ------------------------------------------------------------------ a drained look
    static readonly Dictionary<Texture, Texture2D> drainCache = new Dictionary<Texture, Texture2D>();
    static float drainCacheAmount = -1f;

    /// <summary>
    /// Gives every renderer its own copy of its materials with the colour drained out: amount 0 keeps the visitor's own
    /// colours, 1 is fully grey. Copies are added to `owned` so the caller can destroy them.
    /// </summary>
    public static void Drain(GameObject g, float amount, Color tint, List<Object> owned)
    {
        if (!Mathf.Approximately(amount, drainCacheAmount)) { drainCache.Clear(); drainCacheAmount = amount; }
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials; var nm = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (m == null) { nm[i] = null; continue; }
                var c = new Material(m); owned.Add(c);
                var src = m.HasProperty("_MainTex") ? m.mainTexture : null;
                if (src != null && amount > 0.001f)
                {
                    Texture2D t2;
                    if (!drainCache.TryGetValue(src, out t2) || t2 == null) { t2 = DrainTexture(src, amount); drainCache[src] = t2; owned.Add(t2); }
                    c.mainTexture = t2;
                }
                if (c.HasProperty("_Color")) c.color = Color.Lerp(m.color, tint, amount);
                nm[i] = c;
            }
            r.sharedMaterials = nm;
        }
    }

    static Texture2D DrainTexture(Texture src, float amount)
    {
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t2 = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        t2.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = t2.GetPixels32();
        for (int p = 0; p < px.Length; p++)
        {
            float l = 0.299f * px[p].r + 0.587f * px[p].g + 0.114f * px[p].b;
            px[p] = new Color32((byte)Mathf.Lerp(px[p].r, l, amount), (byte)Mathf.Lerp(px[p].g, l, amount), (byte)Mathf.Lerp(px[p].b, l, amount), px[p].a);
        }
        t2.SetPixels32(px);
        t2.filterMode = src.filterMode; t2.wrapMode = src.wrapMode;
        t2.Apply(false, true);
        t2.name = src.name + " (drained)";
        return t2;
    }

    // ------------------------------------------------------------------ sound
    /// <summary>A sound source made now so the game's audio router puts it on the SFX bus before it is first played.</summary>
    public static AudioSource MakeSource(Transform parent, string name, float spatial, float minDist, float maxDist)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = false;
        s.spatialBlend = spatial; s.minDistance = minDist; s.maxDistance = maxDist;
        s.rolloffMode = AudioRolloffMode.Linear; s.dopplerLevel = 0f;
        s.outputAudioMixerGroup = SfxGroup();
        return s;
    }

    static UnityEngine.Audio.AudioMixerGroup sfx;
    static UnityEngine.Audio.AudioMixerGroup SfxGroup()
    {
        if (sfx != null) return sfx;
        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>(AudioRouter.MixerResource);
        if (mixer == null) return null;
        foreach (var g in mixer.FindMatchingGroups("SFX")) if (g.name == "SFX") { sfx = g; break; }
        return sfx;
    }

    /// <summary>Keeps a sound in step with the game being paused (the pause menu stops time, not audio).</summary>
    public static void FollowPause(AudioSource s, ref bool pausedByUs)
    {
        if (s == null) return;
        if (Time.timeScale == 0f) { if (s.isPlaying) { s.Pause(); pausedByUs = true; } }
        else if (pausedByUs) { s.UnPause(); pausedByUs = false; }
    }

    /// <summary>Takes up to `amount` sanity but never the last point: these scares can frighten, not kill.</summary>
    public static void TakeSanity(int amount)
    {
        var sm = SanityManager.Instance;
        if (sm == null || amount <= 0) return;
        int allowed = Mathf.Min(amount, sm.CurrentSanity - 1);
        if (allowed > 0) sm.DrainSanity(allowed);
    }
}
