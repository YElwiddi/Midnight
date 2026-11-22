using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// The cabin windows reflect the room: for each pane a camera, the eye mirrored through the glass, renders what is behind you
/// into a texture drawn faintly on the glass. (The window over the bed was modelled without glass; it gets a pane here.)
/// An easter egg, once a night: put the desk lamp out and stay in the cabin. After 7 s in the dark, each second you look
/// into a window has a 15 % chance: in the glass only, something black and too tall comes in, a little nearer each time it
/// moves, until it stands at your shoulder. Only the desk window can show the door (the others face away from it or share
/// its wall), and only from the right half of it; there you see the door swing open without a sound first. In the other
/// windows it steps into the glass from the door's side. One board creaks behind you and it costs 15 sanity, safe cabin or
/// not. Its hand comes up to your shoulder, the lantern by the door gutters, and it goes back out the way it came, facing
/// you, and the door shuts. That you hear, from the real door behind you.
/// Turn round, or put the lamp back on, and there is nothing: the door is shut and the room is empty. The door and the
/// figure only exist while the reflection is being drawn, so they can never be seen directly.
/// </summary>
public class CabinWindows : MonoBehaviour
{
    class Pane
    {
        public Renderer glass;
        public Vector3 c, n, r;               // centre, normal into the room, right (as the mirrored camera sees it)
        public float hw, y0, y1;
        public Bounds bounds;
        public RenderTexture rt;
        public MeshRenderer qr;
        public Material qm;
        public Material[] glassMats;          // the pane's own materials, put back when this goes
        public bool ownGlass;                 // the glass itself was made here (the window over the bed)
        public int drawnFrame = -1;           // when its reflection was last drawn (-1: never)
    }

    const string ROOT = "Interior Cabin/instance_0/skpE50E/";
    static readonly string[] PANES = { "ID4117", "ID4127", "ID4097", "ID4082" };
    // the window over the bed (south wall) has its frame and bars but no glass in the model; its pane goes here, between the
    // bars' front and back like the other windows' glass
    const string SOUTH_FRAME = "ID3177";
    static readonly Vector3 SOUTH_GLASS = new Vector3(-91.815f, -27.5007f, 150.235f);
    const float SOUTH_HW = 0.829f, SOUTH_HH = 1.5525f;
    static readonly string[] DOOR = { "ID2062", "ID2072", "ID2052" };
    static readonly Vector3 HINGE = new Vector3(-99.955f, -29.08f, 160.14f);     // on the west edge, so the open door never hides the doorway
    static readonly Vector3 KNOB_SHIFT = new Vector3(1.38f, 0f, 0f);              // the knobs move to the free edge while it is open
    static readonly Vector3 DOOR_CENTER = new Vector3(-99.13f, -29.08f, 160.14f);
    static readonly Vector3 SPILL_AT = new Vector3(-98.75f, -28.3f, 159.25f);
    const float FLOOR = -30.74f;
    static readonly Bounds ROOM = new Bounds(new Vector3(-94.85f, -28.2f, 155.7f), new Vector3(15.6f, 6.6f, 12.4f));
    // inside the log walls: where the figure can stand
    static readonly Bounds INSIDE = new Bounds(new Vector3(-95.035f, -28.2f, 155.3f), new Vector3(12.83f, 6.6f, 9.6f));

    const float CLIP = 0.16f;           // the reflection starts this far into the room: past every window's own frame (0.145 m)
    const float QUAD_OFFSET = 0.004f;   // the reflection is drawn this far in front of the glass
    const float LOOK_RANGE = 5f;        // you look into a window from this far away at most
    // With the desk lamp out the glass still shows the room by a little cold night light (in the glass only), so a black
    // shape stands out in it; the easter egg only ever happens in the dark.
    static readonly Color LAMP_FILL = new Color(1f, 0.78f, 0.55f), LAMP_ROOM = new Color(1f, 0.8f, 0.6f), NIGHT = new Color(0.6f, 0.68f, 0.86f);
    const float DARK_FILL = 0.9f, DARK_ROOM = 1.0f;               // of the lit levels (the cold tint is dimmer than the warm)
    static readonly float[] BACK = { 0.62f, 0.85f, 1.1f };                        // how far behind you it ends up
    static readonly float[] SIDE = { 0.25f, -0.25f, 0.1f, -0.1f, 0.4f, -0.4f };   // over which shoulder (+ right)

    // the scare's beats (seconds from the start)
    const float T_DOOR1 = 1.0f, T_HAND0 = 4.9f, T_HAND1 = 5.9f, T_GONE = 6.85f, T_SHUT1 = 7.05f, T_END = 7.6f;
    static readonly float[] KT = { 1.15f, 2.05f, 2.9f, 3.7f, 4.5f };              // each stop-motion step nearer
    static readonly float[] OT = { 6.25f, 6.4f, 6.55f, 6.7f };                    // back at K3, K2, K1, K0 on the way out
    static readonly float[] FLICK = { 6.0f, 6.12f };
    static readonly float[] PHASE = { 0.10f, 0.55f, 0.20f, 0.70f, 0.38f };        // walk-cycle phase held at each step
    static readonly float[] LOLL = { 16f, -22f, 28f, -12f, 20f };                 // the head hangs to one side, differently each time
    static readonly float[] HUNCH = { 14f, 20f, 10f, 24f, 30f };

    CondemnedConfig cfg;
    readonly List<Pane> panes = new List<Pane>();
    readonly List<Pane> chosen = new List<Pane>();
    readonly Plane[] planes = new Plane[6];
    Camera rc;
    Light spill, fill, roomFill;
    readonly List<Transform> door = new List<Transform>();
    Vector3[] doorPos; Quaternion[] doorRot;
    Light deskLamp; LanternInteractable deskLantern;     // the desk lamp: the only light that counts for the dark
    Light gutter; float gutterSaved, gutterWritten; bool gutterOverridden;   // the lantern by the door, which gutters
    float darkFor;                                        // how long the desk lamp has been off with you in the cabin
    CCTVMonitor cctv;
    readonly List<Transform> nudged = new List<Transform>();
    readonly List<Vector3> nudgedFrom = new List<Vector3>();

    // the scare
    enum Scare { Waiting, Running, Done }
    Scare scare = Scare.Waiting;
    float lookTime, nextRoll, sT, offFor;                 // lookTime: toward the next roll (one a second while you look)
    Pane lookingAt; Vector3 lookFrom;
    Pane sPane;
    readonly Vector3[] keys = new Vector3[5];
    Vector3 sRight;                         // the side of you it ends up on (your right or left, facing the glass)
    bool byDoor;                            // the glass shows the door this time
    bool tookSanity, creaked, shutHeard;
    float latchAt = -1f;
    GameObject sil; Animator silAn; PlayableGraph graph; AnimationClipPlayable walk; float walkLen = 1f;
    readonly Dictionary<HumanBodyBones, Transform> sb = new Dictionary<HumanBodyBones, Transform>();
    Renderer[] silRenderers;
    AudioSource src; bool srcPaused;

    public bool ScareRunning { get { return scare == Scare.Running; } }

    // ================================================================== building

    public static CabinWindows Create(CondemnedConfig cfg)
    {
        if (cfg == null || cfg.reflectionMaterial == null) return null;
        if (GameObject.Find(ROOT + PANES[0]) == null) return null;
        var go = new GameObject("CondemnedCabinWindows");
        var w = go.AddComponent<CabinWindows>();
        w.cfg = cfg;
        bool ok = false;
        try { ok = w.Build(); }
        catch (System.Exception e) { Debug.LogWarning("CabinWindows: could not build: " + e); }
        if (!ok) { CondemnedRig.Kill(go); return null; }
        return w;
    }

    bool Build()
    {
        foreach (var id in PANES)
        {
            var g = GameObject.Find(ROOT + id); if (g == null) continue;
            var rend = g.GetComponent<Renderer>(); if (rend == null) continue;
            AddPane(rend);
        }
        if (panes.Count == 0) return false;
        var south = MakeSouthGlass();
        if (south != null) AddPane(south).ownGlass = true;

        foreach (var p in panes)
        {
            if (cfg.darkGlassMaterial != null && !p.ownGlass)
            {
                p.glassMats = p.glass.sharedMaterials;
                var m = new Material[p.glassMats.Length];
                for (int i = 0; i < m.Length; i++) m[i] = cfg.darkGlassMaterial;
                p.glass.sharedMaterials = m;
            }
            // the reflection: a quad a hair inside the glass, drawn additively
            var go = new GameObject("reflection " + p.glass.name);
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "reflection " + p.glass.name };
            Vector3 o = p.c + p.n * QUAD_OFFSET; float yc = p.c.y;
            mesh.vertices = new[] { o - p.r * p.hw + Vector3.up * (p.y0 - yc), o + p.r * p.hw + Vector3.up * (p.y0 - yc),
                                    o + p.r * p.hw + Vector3.up * (p.y1 - yc), o - p.r * p.hw + Vector3.up * (p.y1 - yc) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            p.bounds = mesh.bounds;
            p.qr = go.AddComponent<MeshRenderer>();
            p.qm = new Material(cfg.reflectionMaterial);
            float s = cfg.reflectionStrength * 0.5f;
            p.qm.SetColor("_TintColor", new Color(s, s, s, 0.5f));
            int h = Mathf.Clamp(cfg.reflectionHeight, 64, 1024);
            int w = Mathf.Max(32, Mathf.RoundToInt(h * (2f * p.hw) / Mathf.Max(0.1f, p.y1 - p.y0)));
            p.rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "reflection " + p.glass.name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            p.rt.Create();
            p.qm.mainTexture = p.rt;
            p.qr.sharedMaterial = p.qm;
            p.qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.qr.receiveShadows = false;
            p.qr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            p.qr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            p.qr.enabled = false;
        }
        FixCrossings();

        var cgo = new GameObject("reflection camera");
        cgo.transform.SetParent(transform, false);
        rc = cgo.AddComponent<Camera>();
        rc.enabled = false;
        rc.clearFlags = CameraClearFlags.SolidColor;
        rc.backgroundColor = Color.black;
        rc.cullingMask = ~(1 << 5);                    // everything but the UI
        rc.allowHDR = false; rc.allowMSAA = false; rc.useOcclusionCulling = false;
        rc.depthTextureMode = DepthTextureMode.None;

        if (cfg.doorSpill > 0f)
        {
            var lgo = new GameObject("reflection door spill");
            lgo.transform.SetParent(transform, false);
            lgo.transform.position = SPILL_AT;
            spill = lgo.AddComponent<Light>();
            spill.type = LightType.Point; spill.color = new Color(1f, 0.74f, 0.5f); spill.intensity = cfg.doorSpill; spill.range = 3f;
            spill.shadows = LightShadows.None; spill.renderMode = LightRenderMode.ForcePixel;
            spill.enabled = false;
        }
        if (cfg.reflectionRoomFill > 0f)
        {
            // and a little light over the whole room, as the glass shows it, so a black shape stands out wherever it is
            var rgo = new GameObject("reflection room fill");
            rgo.transform.SetParent(transform, false);
            rgo.transform.position = new Vector3(ROOM.center.x, -26.4f, ROOM.center.z);
            roomFill = rgo.AddComponent<Light>();
            roomFill.type = LightType.Point; roomFill.color = new Color(1f, 0.8f, 0.6f); roomFill.intensity = cfg.reflectionRoomFill; roomFill.range = cfg.reflectionRoomRange;
            roomFill.shadows = LightShadows.None; roomFill.renderMode = LightRenderMode.ForcePixel;
            roomFill.enabled = false;
        }
        if (cfg.reflectionFill > 0f)
        {
            // the desk lamp's light on the room behind you, as the glass shows it (so a black shape stands out against it)
            var fgo = new GameObject("reflection fill");
            fgo.transform.SetParent(transform, false);
            fill = fgo.AddComponent<Light>();
            fill.type = LightType.Point; fill.color = new Color(1f, 0.78f, 0.55f); fill.intensity = cfg.reflectionFill; fill.range = 4.2f;
            fill.shadows = LightShadows.None; fill.renderMode = LightRenderMode.ForcePixel;
            fill.enabled = false;
        }

        foreach (var n in DOOR) { var g = GameObject.Find(ROOT + n); if (g != null) door.Add(g.transform); }
        var cross = GameObject.Find("PSXCross"); if (cross != null && ROOM.Contains(cross.transform.position)) door.Add(cross.transform);
        doorPos = new Vector3[door.Count]; doorRot = new Quaternion[door.Count];

        var lamp = GameObject.Find("Interior Cabin/Lantern (1)");
        if (lamp != null)
        {
            deskLantern = lamp.GetComponent<LanternInteractable>();
            deskLamp = deskLantern != null && deskLantern.lanternLight != null ? deskLantern.lanternLight : lamp.GetComponentInChildren<Light>(true);
        }
        var other = GameObject.Find("Interior Cabin/Lantern");
        if (other != null)
        {
            var li = other.GetComponent<LanternInteractable>();
            gutter = li != null && li.lanternLight != null ? li.lanternLight : other.GetComponentInChildren<Light>(true);
        }
        cctv = FindFirstObjectByType<CCTVMonitor>();
        src = CondemnedRig.MakeSource(transform, "window sounds", 1f, 1.5f, 18f);
        return true;
    }

    Pane AddPane(Renderer rend)
    {
        var b = rend.bounds; var e = b.extents;
        var p = new Pane { glass = rend };
        if (e.x <= e.z) { p.n = new Vector3(Mathf.Sign(ROOM.center.x - b.center.x), 0f, 0f); p.hw = e.z; }
        else { p.n = new Vector3(0f, 0f, Mathf.Sign(ROOM.center.z - b.center.z)); p.hw = e.x; }
        p.c = b.center; p.y0 = b.min.y; p.y1 = b.max.y;
        p.r = Vector3.Cross(Vector3.up, p.n);
        panes.Add(p);
        return p;
    }

    // the window over the bed gets a pane of the same night glass as the others
    Renderer MakeSouthGlass()
    {
        if (GameObject.Find(ROOT + SOUTH_FRAME) == null) return null;
        Material mat = cfg.darkGlassMaterial;
        if (mat == null) { var own = panes[0].glass.sharedMaterials; if (own.Length > 0) mat = own[0]; }
        if (mat == null) return null;
        var go = new GameObject("window glass (south)");
        go.transform.SetParent(transform, false);
        Vector3 c = SOUTH_GLASS, r = Vector3.right, up = Vector3.up;              // facing the room (+z)
        var mesh = new Mesh { name = "window glass (south)" };
        mesh.vertices = new[] { c - r * SOUTH_HW - up * SOUTH_HH, c + r * SOUTH_HW - up * SOUTH_HH, c + r * SOUTH_HW + up * SOUTH_HH, c - r * SOUTH_HW + up * SOUTH_HH };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    // In every window one cross bar has its front face exactly level with the upright's, so the two flicker where they cross
    // (z-fighting, in the model itself). That bar goes back a few millimetres, level with the window's other cross bars.
    void FixCrossings()
    {
        var holder = GameObject.Find(ROOT.TrimEnd('/'));
        if (holder == null) return;
        var all = holder.GetComponentsInChildren<Renderer>(true);
        var uprights = new List<Renderer>(); var crossbars = new List<Renderer>();
        foreach (var p in panes)
        {
            uprights.Clear(); crossbars.Clear();
            foreach (var r in all)
            {
                if (r == p.glass) continue;
                var b = r.bounds;
                float front = Front(p, b), back = Front(p, b) - 2f * Mathf.Abs(Vector3.Dot(b.extents, p.n));
                if (front < 0.02f || front > 0.1f || back < -0.06f) continue;               // not a bar in this window
                float lat = Vector3.Dot(b.center - p.c, p.r), latSize = 2f * Mathf.Abs(Vector3.Dot(b.extents, p.r));
                if (Mathf.Abs(lat) > p.hw || b.center.y < p.y0 || b.center.y > p.y1) continue;
                if (b.size.y > 1.5f && latSize < 0.15f) uprights.Add(r);
                else if (latSize > 1f && b.size.y < 0.15f) crossbars.Add(r);
            }
            foreach (var h in crossbars)
                foreach (var v in uprights)
                {
                    if (Mathf.Abs(Front(p, h.bounds) - Front(p, v.bounds)) > 0.002f) continue;
                    nudged.Add(h.transform); nudgedFrom.Add(h.transform.position);
                    h.transform.position -= p.n * 0.006f;
                    break;
                }
        }
    }

    // how far a box reaches out of the glass into the room
    static float Front(Pane p, Bounds b) { return Vector3.Dot(b.center - p.c, p.n) + Mathf.Abs(Vector3.Dot(b.extents, p.n)); }

    void BuildSilhouette()
    {
        if (sil != null || cfg.silhouettePrefab == null || cfg.silhouetteWalk == null) return;
        sil = CondemnedRig.SpawnPuppet(cfg.silhouettePrefab, new Vector3(0f, -400f, 0f), Quaternion.identity, "Window silhouette");
        sil.transform.localScale = sil.transform.localScale * cfg.silhouetteScale;
        silAn = sil.GetComponentInChildren<Animator>();
        if (silAn == null || silAn.avatar == null || !silAn.avatar.isHuman) { CondemnedRig.Kill(sil); sil = null; return; }
        silAn.runtimeAnimatorController = null;                                         // only the walk below moves it
        silRenderers = sil.GetComponentsInChildren<Renderer>(true);
        foreach (var r in silRenderers)
        {
            if (cfg.silhouetteMaterial != null)
            {
                var m = r.sharedMaterials;
                for (int i = 0; i < m.Length; i++) m[i] = cfg.silhouetteMaterial;
                r.sharedMaterials = m;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
        }
        foreach (HumanBodyBones hb in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (hb == HumanBodyBones.LastBone) continue;
            var t = silAn.GetBoneTransform(hb); if (t != null) sb[hb] = t;
        }
        graph = PlayableGraph.Create("Window silhouette walk");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "anim", silAn);
        walk = AnimationClipPlayable.Create(graph, cfg.silhouetteWalk);
        walk.SetApplyFootIK(false);
        output.SetSourcePlayable(walk);
        walkLen = Mathf.Max(0.1f, cfg.silhouetteWalk.length);
    }

    void OnDestroy()
    {
        RestoreScene();
        if (graph.IsValid()) graph.Destroy();
        foreach (var p in panes)
        {
            if (p.glass != null && p.glassMats != null) p.glass.sharedMaterials = p.glassMats;
            if (p.rt != null) { p.rt.Release(); CondemnedRig.Kill(p.rt); }
            if (p.qm != null) CondemnedRig.Kill(p.qm);
        }
        for (int i = 0; i < nudged.Count; i++) if (nudged[i] != null) nudged[i].position = nudgedFrom[i];
        if (sil != null) CondemnedRig.Kill(sil);
    }

    // ================================================================== every frame

    void LateUpdate()
    {
        if (gutterOverridden)
        {
            // put the lantern back if nothing else has written to it since
            if (gutter != null && Mathf.Approximately(gutter.intensity, gutterWritten)) gutter.intensity = gutterSaved;
            gutterOverridden = false;
        }
        CondemnedRig.FollowPause(src, ref srcPaused);
        if (latchAt >= 0f && CondemnedRig.Now >= latchAt) { latchAt = -1f; PlayAt(cfg.doorShut, DOOR_CENTER, 0.35f, 1.2f); }

        var cam = CondemnedRig.PlayerCamera();
        bool inRoom = cam != null && ROOM.Contains(cam.transform.position);
        // the dark only counts while you are in here, and only the desk lamp makes it
        darkFor = inRoom && !LampOn() ? darkFor + CondemnedRig.Dt : 0f;
        bool inside = inRoom && !Blocked();
        if (!inside)
        {
            foreach (var p in panes) { p.qr.enabled = false; p.drawnFrame = -1; }
            if (scare == Scare.Running) Abort();
            lookTime = 0f; lookingAt = null;
            return;
        }
        Vector3 eye = cam.transform.position;

        // the panes facing you and in view, nearest first (two at most)
        chosen.Clear();
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        foreach (var p in panes)
        {
            float g = Vector3.Dot(eye - p.c, p.n);
            if (g > 0.05f && (eye - p.c).sqrMagnitude < 144f && GeometryUtility.TestPlanesAABB(planes, p.bounds)) chosen.Add(p);
        }
        chosen.Sort((a, b) => (a.c - eye).sqrMagnitude.CompareTo((b.c - eye).sqrMagnitude));
        if (chosen.Count > 2) chosen.RemoveRange(2, chosen.Count - 2);
        // the window something is walking in is always drawn
        if (scare == Scare.Running && sPane != null && !chosen.Contains(sPane)) { if (chosen.Count == 2) chosen.RemoveAt(1); chosen.Insert(0, sPane); }
        foreach (var p in panes) if (!chosen.Contains(p)) p.drawnFrame = -1;     // an old image is never shown again

        // the fill stands in for the desk lamp, so it is off when the lamp is and gutters when it does
        lampK = deskLamp == null ? 1f : (deskLamp.isActiveAndEnabled && deskLamp.intensity > 0.01f ? 1f : 0f);
        UpdateScare(cam, eye);

        if (!cfg.reflectionsEnabled && scare != Scare.Running) { foreach (var p in panes) p.qr.enabled = false; return; }

        // the reflections (the door and the figure are only there for these renders)
        foreach (var p in panes) p.qr.enabled = false;
        ApplyScene();
        try
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                var p = chosen[i];
                // the farther window only every other frame (always every frame while something is walking in it)
                if (i > 0 && scare != Scare.Running && p.drawnFrame >= 0 && (Time.frameCount & 1) == 1) continue;
                p.drawnFrame = Time.frameCount;
                SetupCamera(p, eye);
                if (fill != null)
                {
                    fill.transform.position = eye - p.n * 0.55f + p.r * 0.25f - Vector3.up * 0.11f;
                    fill.color = Color.Lerp(NIGHT, LAMP_FILL, lampK);
                    fill.intensity = cfg.reflectionFill * Mathf.Lerp(DARK_FILL, 1f, lampK);
                    fill.enabled = true;
                }
                if (roomFill != null)
                {
                    roomFill.color = Color.Lerp(NIGHT, LAMP_ROOM, lampK);
                    roomFill.intensity = cfg.reflectionRoomFill * Mathf.Lerp(DARK_ROOM, 1f, lampK);
                    roomFill.range = cfg.reflectionRoomRange;
                    roomFill.enabled = true;
                }
                rc.targetTexture = p.rt; rc.Render();
                if (fill != null) fill.enabled = false;
                if (roomFill != null) roomFill.enabled = false;
            }
        }
        finally { RestoreScene(); }
        foreach (var p in chosen) p.qr.enabled = true;
    }

    bool Blocked()
    {
        if (cctv != null && cctv.IsViewing) return true;
        var ru = ReadableUI.Instance; if (ru != null && ru.IsOpen) return true;
        return false;
    }

    // the eye mirrored through the pane, looking back into the room through it (from just past the window's own frame)
    void SetupCamera(Pane p, Vector3 eye)
    {
        float g = Vector3.Dot(eye - p.c, p.n);
        Vector3 m = eye - 2f * g * p.n;
        rc.transform.SetPositionAndRotation(m, Quaternion.LookRotation(p.n, Vector3.up));
        float nr = g + CLIP, far = 30f, s = nr / g;
        float er = Vector3.Dot(eye - p.c, p.r);
        rc.nearClipPlane = nr; rc.farClipPlane = far;
        rc.projectionMatrix = Matrix4x4.Frustum((-p.hw - er) * s, (p.hw - er) * s, (p.y0 - eye.y) * s, (p.y1 - eye.y) * s, nr, far);
    }

    bool InReflection(Vector3 w)
    {
        Vector3 v = rc.WorldToViewportPoint(w);
        return v.z > rc.nearClipPlane && v.x > 0.06f && v.x < 0.94f && v.y > 0.02f && v.y < 0.98f;
    }

    // where a point's reflection sits on the glass, and nothing (the desk lamp, a wardrobe) between you and it
    static bool SeenInGlass(Pane p, Vector3 eye, Vector3 target)
    {
        float g = Vector3.Dot(eye - p.c, p.n);
        Vector3 m = eye - 2f * g * p.n, d = target - m;
        float dn = Vector3.Dot(d, p.n);
        if (dn <= 1e-4f) return false;
        Vector3 onGlass = m + d * (g / dn);
        return CondemnedRig.ClearLine(eye, onGlass, 0.15f);
    }

    // across the glass, where a point at eye height shows in it (0 = the middle; the glass ends at +-hw)
    static float Across(Pane p, Vector3 m, float g, Vector3 at, float eyeY)
    {
        Vector3 a = new Vector3(at.x, eyeY, at.z);
        float dn = Vector3.Dot(a - m, p.n);
        if (dn <= 1e-4f) return float.PositiveInfinity;
        return Vector3.Dot(m + (a - m) * (g / dn) - p.c, p.r);
    }

    float doorAngle, lampK = 1f;
    bool silShown;

    void ApplyScene()
    {
        if (doorAngle > 0.01f && door.Count > 0)
        {
            for (int i = 0; i < door.Count; i++)
            {
                doorPos[i] = door[i].position; doorRot[i] = door[i].rotation;
                if (i == 1 || i == 2) door[i].position += KNOB_SHIFT;
                door[i].RotateAround(HINGE, Vector3.up, doorAngle);
            }
            doorMoved = true;
        }
        if (silShown && silRenderers != null) foreach (var r in silRenderers) r.enabled = true;
        if (spill != null) spill.enabled = true;
    }

    bool doorMoved;
    void RestoreScene()
    {
        if (doorMoved)
        {
            for (int i = 0; i < door.Count; i++) if (door[i] != null) { door[i].position = doorPos[i]; door[i].rotation = doorRot[i]; }
            doorMoved = false;
        }
        if (silRenderers != null) foreach (var r in silRenderers) if (r != null) r.enabled = false;
        if (spill != null) spill.enabled = false;
        if (fill != null) fill.enabled = false;
        if (roomFill != null) roomFill.enabled = false;
    }

    // ================================================================== the scare

    void UpdateScare(Camera cam, Vector3 eye)
    {
        doorAngle = 0f; silShown = false;
        var gfm = GameFlowManager.Instance;
        float dt = CondemnedRig.Dt;
        if (scare == Scare.Waiting)
        {
            // an easter egg: only once the desk lamp has been out a while
            if (!cfg.windowScareEnabled || gfm == null || gfm.CurrentPhase < cfg.windowScareFromPhase || dt <= 0f || darkFor < cfg.windowScareDarkSeconds) { lookTime = 0f; return; }
            BuildSilhouette();
            if (sil == null) return;
            var dm = DialogueManager.GetInstance();
            if ((dm != null && dm.IsDialoguePlaying()) || gfm.IsKillerEventActive) { lookTime = 0f; return; }
            // you are standing at a window and looking into it
            Pane looking = null;
            foreach (var p in chosen)
            {
                if ((eye - p.c).sqrMagnitude > LOOK_RANGE * LOOK_RANGE) continue;
                if (AtWindow(p, eye) && LookingAt(p, eye, cam.transform.forward)) { looking = p; break; }
            }
            // another window, or walked off: it starts over
            if (looking != lookingAt || (looking != null && (eye - lookFrom).sqrMagnitude > 0.36f)) { lookingAt = looking; lookFrom = eye; lookTime = 0f; }
            if (looking == null || CondemnedRig.Now < nextRoll) { lookTime = 0f; return; }
            lookTime += dt;
            if (lookTime < 1f) return;
            lookTime -= 1f;                                     // one roll for each second you look into it
            // (no roll while nothing right behind you would show in this glass from here, or something hides it)
            if (Plan(looking, eye) && Random.value < cfg.windowScareChance) Begin(looking);
            return;
        }
        if (scare != Scare.Running) return;

        // turned away from the glass, or the lamp back on? then there is nothing
        Vector3 pv = cam.WorldToViewportPoint(sPane.c);
        bool facing = pv.z > 0f && pv.x > -0.12f && pv.x < 1.12f;
        offFor = facing ? 0f : offFor + dt;
        if ((offFor > 0.12f || LampOn()) && sT < T_GONE) { Abort(); return; }

        sT += dt;
        float t = sT;
        if (t >= T_END) { scare = Scare.Done; return; }

        // the door: open without a sound, then shut hard once it is through
        doorAngle = 78f * CondemnedRig.Ease(t, 0f, T_DOOR1);
        if (t >= T_GONE) { float u = CondemnedRig.Seg(t, T_GONE, T_SHUT1); doorAngle *= 1f - u * u; }
        if (!shutHeard && t >= T_SHUT1) { shutHeard = true; PlayAt(cfg.doorShut, DOOR_CENTER, 1f, 1f); }

        // which step it is on, coming or going
        int key = -1; bool between = false; int kFrom = 0;
        if (t < OT[0]) { for (int i = 0; i < KT.Length; i++) if (t >= KT[i]) key = i; for (int i = 1; i < KT.Length; i++) if (t >= KT[i] - 0.034f && t < KT[i]) { between = true; kFrom = i - 1; key = i; } }
        else { int back = 0; for (int i = 0; i < OT.Length; i++) if (t >= OT[i]) back = i + 1; key = t >= T_GONE ? -1 : 4 - back; }
        if (key >= 0) { PoseFigure(key, between, kFrom, eye, t); silShown = true; }

        if (!creaked && t >= KT[4] + 0.06f)
        {
            creaked = true;
            PlayAt(cfg.boardCreak, keys[4] + Vector3.up * 0.1f, 0.9f, 0.95f);
            Drain();
        }
        // the lantern by the door gutters (the desk lamp is out: that is how it got in)
        if (gutter != null && gutter.isActiveAndEnabled && gutter.intensity > 0.01f)
        {
            float k = 1f;
            if ((t >= FLICK[0] && t < FLICK[0] + 0.067f) || (t >= FLICK[1] && t < FLICK[1] + 0.1f)) k = 0.08f;
            else if (t >= FLICK[0] + 0.067f && t < FLICK[0] + 0.1f) k = 0.6f;
            if (k < 1f) { gutterSaved = gutter.intensity; gutterWritten = gutterSaved * k; gutter.intensity = gutterWritten; gutterOverridden = true; }
        }
    }

    // the desk lamp is the only light that counts (no lamp found: as if lit, so nothing happens)
    bool LampOn()
    {
        if (deskLantern != null) return deskLantern.IsLit;
        if (deskLamp != null) return deskLamp.isActiveAndEnabled && deskLamp.intensity > 0.01f;
        return true;
    }

    // what it costs once seen; the cabin is a sanity safe zone, but this gets through (never the last point)
    void Drain()
    {
        if (tookSanity) return;
        tookSanity = true;
        var sm = SanityManager.Instance;
        if (sm == null) return;
        bool safe = sm.IsInSafeZone;
        if (safe) sm.ExitSafeZone();
        try { CondemnedRig.TakeSanity(cfg.windowScareSanityCost); }
        finally { if (safe) sm.EnterSafeZone(); }
    }

    // standing at the window, roughly in front of it (Plan decides whether what is behind you really shows)
    static bool AtWindow(Pane p, Vector3 eye)
    {
        return Vector3.Dot(eye - p.c, p.n) > 0.3f && Mathf.Abs(Vector3.Dot(eye - p.c, p.r)) < p.hw + 0.6f;
    }

    // your line of sight meets the glass (or just around it)
    static bool LookingAt(Pane p, Vector3 eye, Vector3 fwd)
    {
        float g = Vector3.Dot(eye - p.c, p.n), dn = Vector3.Dot(fwd, -p.n);
        if (g <= 0f || dn < 0.25f) return false;
        Vector3 hit = eye + fwd * (g / dn);
        return Mathf.Abs(Vector3.Dot(hit - p.c, p.r)) < p.hw + 0.25f && hit.y > p.y0 - 0.35f && hit.y < p.y1 + 0.25f;
    }

    // Where it stands at each step: last, right behind you over a shoulder; first, in the doorway if this glass shows the door,
    // else as far back as the room allows at the edge of the glass on the door's side; the steps between, each nearer than the
    // one before, on reflected lines of sight (something in the room only shows in the glass on one).
    bool Plan(Pane p, Vector3 eye)
    {
        float g = Vector3.Dot(eye - p.c, p.n);
        if (g < 0.2f) return false;
        Vector3 m = eye - 2f * g * p.n;                                         // the eye mirrored through the glass
        var player = CondemnedRig.Player();
        Vector3 feet = new Vector3(eye.x, FLOOR, eye.z);
        Vector3 right = Vector3.Cross(Vector3.up, -p.n);                        // your right, facing the glass
        Vector3 behind = Vector3.zero; bool found = false;
        foreach (float back in BACK)
        {
            foreach (float side in SIDE)
            {
                Vector3 P = feet + p.n * back + right * side;
                if (Mathf.Abs(Across(p, m, g, P, eye.y)) > p.hw - 0.06f) continue;                     // has to show in the glass
                if (!INSIDE.Contains(P + Vector3.up) || !Free(P, player)) continue;
                if (!SeenInGlass(p, eye, new Vector3(P.x, eye.y, P.z))) continue;                     // and that bit of glass in view
                behind = P; sRight = side >= 0f ? right : -right; found = true; break;
            }
            if (found) break;
        }
        if (!found) return false;

        Vector3 din = new Vector3(DOOR_CENTER.x, FLOOR, DOOR_CENTER.z - 0.55f);
        SetupCamera(p, eye);
        byDoor = InReflection(DOOR_CENTER) && InReflection(new Vector3(DOOR_CENTER.x, FLOOR + 0.3f, DOOR_CENTER.z - 0.5f)) && SeenInGlass(p, eye, DOOR_CENTER);
        Vector3 first;
        if (byDoor) first = din;
        else if (!EdgeEntry(p, m, g, eye, din, behind, player, out first)) return false;

        Vector3 fEye = new Vector3(first.x, eye.y, first.z), bEye = new Vector3(behind.x, eye.y, behind.z);
        float D0 = Vector3.Dot(fEye - p.c, p.n), D4 = Vector3.Dot(bEye - p.c, p.n);   // how far into the room
        Vector3 I0 = m + (fEye - m) * (g / (D0 + g)), I4 = m + (bEye - m) * (g / (D4 + g));   // and where each shows in the glass
        keys[0] = first; keys[4] = behind;
        float[] f = { 0.27f, 0.52f, 0.77f };
        for (int i = 0; i < 3; i++)
        {
            Vector3 I = Vector3.Lerp(I0, I4, f[i]);
            float D = Mathf.Lerp(D0, D4, Mathf.Pow(f[i], 0.8f));
            Vector3 best = keys[i]; bool ok = false;                            // nowhere free: it just holds where it was
            foreach (float dd in new[] { 0f, 0.3f, -0.3f, 0.6f })
            {
                foreach (float s in new[] { 0f, 0.06f, -0.06f, 0.12f })
                {
                    Vector3 P = m + (I + p.r * s - m) * ((g + D + dd) / g);
                    P.y = FLOOR;
                    if (INSIDE.Contains(P + Vector3.up) && Free(P, player)) { best = P; ok = true; break; }
                }
                if (ok) break;
            }
            keys[i + 1] = best;
        }
        return true;
    }

    // The door is not in this glass: the first step is at the edge of the glass on the door's side, as far back into the
    // room as there is floor for it (so it comes from the door's way, and from a distance).
    bool EdgeEntry(Pane p, Vector3 m, float g, Vector3 eye, Vector3 doorway, Vector3 behind, GameObject player, out Vector3 first)
    {
        first = Vector3.zero;
        float uDoor = Across(p, m, g, doorway, eye.y), uBehind = Across(p, m, g, behind, eye.y);
        float side = uDoor >= uBehind ? 1f : -1f;
        float dBehind = Vector3.Dot(behind - p.c, p.n);
        foreach (float inset in new[] { 0.22f, 0.4f, 0.6f })
        {
            Vector3 I = p.c + p.r * (side * (p.hw - inset)) + Vector3.up * (eye.y - p.c.y);   // on the glass, at eye height
            for (float D = Mathf.Min(dBehind + 6f, 9f); D >= dBehind + 1.4f; D -= 0.35f)
            {
                Vector3 P = m + (I - m) * ((g + D) / g);
                P.y = FLOOR;
                if (!INSIDE.Contains(P + Vector3.up) || !Free(P, player)) continue;
                if (!SeenInGlass(p, eye, new Vector3(P.x, eye.y, P.z))) continue;
                first = P;
                return true;
            }
        }
        return false;
    }

    static bool Free(Vector3 feet, GameObject player)
    {
        var hits = Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.7f, 0.22f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (player != null && h.transform.IsChildOf(player.transform)) continue;
            return false;
        }
        return true;
    }

    void Begin(Pane p)
    {
        scare = Scare.Running;
        sPane = p; sT = 0f; offFor = 0f;
        tookSanity = creaked = shutHeard = false;
    }

    void Abort()
    {
        bool seen = sT >= KT[0];
        if (seen)
        {
            scare = Scare.Done;
            Drain();
            if (!shutHeard) latchAt = CondemnedRig.Now + 0.25f;           // a soft click from the door, as if it had just closed
        }
        else
        {
            scare = Scare.Waiting;                                         // it never got in: it can come another time
            nextRoll = CondemnedRig.Now + 30f;
        }
        lookTime = 0f; lookingAt = null;
        doorAngle = 0f; silShown = false;
    }

    // ================================================================== the figure

    Transform S(HumanBodyBones b) { Transform t; return sb.TryGetValue(b, out t) ? t : null; }

    void PoseFigure(int key, bool between, int kFrom, Vector3 eye, float t)
    {
        Vector3 pos = keys[key];
        if (between) pos = Vector3.Lerp(keys[kFrom], pos, 0.5f);
        Vector3 face = eye - pos; face.y = 0f;
        if (face.sqrMagnitude < 1e-4f) face = -sPane.n;
        sil.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(face.normalized, Vector3.up));
        walk.SetTime(PHASE[key] * walkLen);
        graph.Evaluate(0f);

        Vector3 fw = sil.transform.forward, rt = sil.transform.right;
        float lean = key == 4 && t < OT[0] ? 18f * CondemnedRig.Ease(t, KT[4], KT[4] + 1.2f) : 0f;
        CondemnedRig.Turn(S(HumanBodyBones.Spine), rt, (HUNCH[key] + lean) * 0.5f);
        CondemnedRig.Turn(S(HumanBodyBones.Chest), rt, (HUNCH[key] + lean) * 0.5f);
        // arms hang too long and slack
        Vector3 down = Vector3.down;
        AimArm(true, down + fw * (0.12f + 0.1f * CondemnedRig.Rnd(key * 3.1f)) - rt * 0.12f, down + fw * 0.18f);
        AimArm(false, down + fw * (0.12f + 0.1f * CondemnedRig.Rnd(key * 5.3f)) + rt * 0.12f, down + fw * 0.18f);
        // the head hangs to one side; behind you it turns in toward your ear
        CondemnedRig.Turn(S(HumanBodyBones.Head), fw, LOLL[key]);
        CondemnedRig.Turn(S(HumanBodyBones.Head), rt, 12f + lean * 0.8f);
        if (key == 4)
        {
            var head = S(HumanBodyBones.Head);
            if (head != null)
            {
                Vector3 ear = eye + sRight * 0.12f;
                Vector3 want = ear - head.position; want.y *= 0.4f;
                head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(fw, want.normalized), 0.5f * CondemnedRig.Ease(t, KT[4], KT[4] + 0.8f)) * head.rotation;
            }
            // its hand comes up to your shoulder, in held steps (the hand on the side it stands)
            float u = t < OT[0] ? CondemnedRig.Smooth(CondemnedRig.Seg(Mathf.Floor(t * 12f) / 12f, T_HAND0, T_HAND1)) : 0f;
            if (u > 0f)
            {
                bool rightHand = Vector3.Dot(sRight, Vector3.Cross(Vector3.up, -sPane.n)) >= 0f;
                var ua = S(rightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                var la = S(rightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                var hand = S(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                if (ua != null && la != null && hand != null)
                {
                    Vector3 shoulder = eye - Vector3.up * 0.28f + sRight * 0.2f + sPane.n * 0.1f;
                    Vector3 target = Vector3.Lerp(hand.position, shoulder, u);
                    CondemnedRig.Reach(ua, la, hand, target, Vector3.down + fw * -0.3f + (rightHand ? rt : -rt) * 0.4f);
                }
            }
        }
    }

    void AimArm(bool left, Vector3 upperDir, Vector3 lowerDir)
    {
        CondemnedRig.Aim(S(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm), S(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), upperDir);
        CondemnedRig.Aim(S(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm), S(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), lowerDir);
    }

    void PlayAt(AudioClip c, Vector3 at, float vol, float pitch)
    {
        if (c == null || src == null) return;
        src.transform.position = at;
        src.pitch = pitch;
        src.PlayOneShot(c, vol);
    }

    // ================================================================== for testing

    /// <summary>Starts the scare on the pane nearest the given eye position, if it can (for test renders).</summary>
    public bool ForceStart(Vector3 eye)
    {
        BuildSilhouette();
        if (sil == null) return false;
        Pane best = null; float bd = float.MaxValue;
        foreach (var p in panes) { float d = (p.c - eye).sqrMagnitude; if (Vector3.Dot(eye - p.c, p.n) > 0.05f && d < bd) { bd = d; best = p; } }
        if (best == null || !Plan(best, eye)) return false;
        Begin(best);
        return true;
    }

    /// <summary>Sets the scare clock (for test renders).</summary>
    public void SetClock(float t) { sT = t; }

    /// <summary>For tests: whether the last plan comes in through a door the glass shows, and the steps it planned.</summary>
    public bool PlannedByDoor { get { return byDoor; } }
    /// <summary>For tests: how long the desk lamp has been off with the player in the cabin.</summary>
    public float DarkFor { get { return darkFor; } }
    public Vector3[] PlannedSteps { get { return (Vector3[])keys.Clone(); } }
}
