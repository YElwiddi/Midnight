using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Church Rework (Nov 2026): Father Joseph becomes Characters PSX Male_51 dressed as a priest, the cathedral interior is replaced
// by Ice Ching's Provincial Church (lengthened to 12 window bays so the Bride's aisle staging and spawn distance still fit), the three church doors get
// the outside church's door, and the infinite staircase is walled and roofed with the BlenderVoyage kit it was already built from.
// Tools > Graveyard > Church Rework > Apply. Scene changes are one undo step (Ctrl+Z); save the scene to keep them.

public static class ChurchRework
{
    // ------------------------------------------------------------------ assets
    public const string ROOT = "Assets/Church Rework/";
    public const string PC = ROOT + "Provincial Church/";
    public const string FJ = ROOT + "Father Joseph/";
    public const string GEN = ROOT + "Generated/";
    public const string KIT = "Assets/Additional Models and Assets/ModularDungeonFreeEdition.fbx";
    public const string EXT_DOOR = "Assets/Additional Models and Assets/psx-abandoned-church/textures/Door (2).jpeg";
    public const string NAV_ASSET = "Assets/Scenes/Scene 0.5 Dungeon/NavMesh-Interior PSX.asset";
    public const string PRIEST = "Assets/Prefabs/Priest.prefab";
    public const string HAROLD = "Assets/Prefabs/Harold.prefab";
    const string UNDO = "Church Rework";

    // ------------------------------------------------------------------ church layout
    // The pack's room, in its own units: floor height, entrance + altar wall faces, half width, and the 2-bay section that repeats.
    const float N_FLOOR = 3.747f, N_ENT = 0.90f, N_ALTAR = 14.41f, N_CUT = 11.15f, N_SEC = 4.25f, N_HALF = 4.08f;
    const int REPEATS = 4;                   // + 4 x 2 bays: 12 window bays, about 60 units from door to altar wall, so the
                                             // Bride trigger is ~40 from her (old church: 48) and fog hides her when she spawns
    const float S = 1.47f;                   // pack units -> game units (eaves ~4.2, ridge ~9.8, as wide as the old nave's aisle)
    const float FLOOR_Y = -706.8f;           // 100 units under the old cathedral, whose baked navmesh stays behind
    const float ENTRANCE_X = -32.3f, CENTER_Z = -46.3f;
    static float NAltarLong { get { return N_ALTAR + (N_CUT - N_SEC) * REPEATS; } }
    static Vector3 P0 { get { return new Vector3(ENTRANCE_X + S * N_ENT, FLOOR_Y - S * N_FLOOR, CENTER_Z); } }
    static float AltarWallX { get { return ENTRANCE_X - S * (NAltarLong - N_ENT); } }
    // room point (pack units, +x toward the altar) -> world (the church runs toward -x like the old one)
    static Vector3 W(float x, float y, float z) { return P0 + new Vector3(-S * x, S * y, -S * z); }

    // the Bride's chancel, copied from the old one: altar centre 4.2 behind BrideSpawn, its top 1.75 over the floor there
    const float ALTAR_DEPTH = 3.5f, ALTAR_WIDTH = 4.6f, ALTAR_TOP = 1.75f, ALTAR_GAP = 0.6f, BRIDE_AHEAD = 4.2f;
    const float DOOR_W = 1.9f, DOOR_H = 4.0f, DOOR_D = 0.14f;
    const float SIDE_DOOR_Z = 2.70f * S;     // the pack's back-room doorway sits 2.7 (pack units) right of centre

#if UNITY_EDITOR
    [MenuItem("Tools/Graveyard/Church Rework/Apply")]
    static void ApplyMenu()
    {
        const string scenePath = "Assets/Scenes/Scene 0.5 Dungeon.unity";
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Church Rework", "Stop play mode first, then run this again.", "OK"); return; }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
        {
            if (!EditorUtility.DisplayDialog("Church Rework", "This changes 'Scene 0.5 Dungeon', which isn't the open scene. Open it now?", "Open it", "Cancel")) return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        if (!EditorUtility.DisplayDialog("Church Rework", "Replace Father Joseph's model, the church interior and the staircase walls in 'Scene 0.5 Dungeon'?\n\nScene changes are one undo step. Save the scene afterwards to keep them.", "Apply", "Cancel")) return;
        string log = Apply(true, true, true);
        Debug.Log("[Church Rework]\n" + log);
        bool failed = log.StartsWith("Open ") || log.Contains("missing") || log.Contains("not found") || log.Contains("FAILED");
        EditorUtility.DisplayDialog("Church Rework", (failed ? "Something didn't apply:\n\n" : "Done. Save the scene (Ctrl+S) to keep it, or Ctrl+Z to undo the scene changes.\n\n") + log, "OK");
    }
#endif

    public static string Apply(bool church, bool stairs, bool priest)
    {
        var log = new System.Text.StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Scene 0.5 Dungeon.unity")) return "Open 'Scene 0.5 Dungeon' first (active scene is " + scene.path + ").";
        EnsureFolders();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UNDO);
        int group = Undo.GetCurrentGroup();
        if (priest) log.AppendLine(ReworkPriest());
        if (church) log.AppendLine(BuildChapel());
        if (stairs) log.AppendLine(BuildTunnel());
        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Church Rework")) AssetDatabase.CreateFolder("Assets", "Church Rework");
        if (!AssetDatabase.IsValidFolder("Assets/Church Rework/Generated")) AssetDatabase.CreateFolder("Assets/Church Rework", "Generated");
        // import settings the rework relies on
        var mi = AssetImporter.GetAtPath(FJ + "Character_Male_51.fbx") as ModelImporter;
        if (mi != null && mi.animationType != ModelImporterAnimationType.Human)
        { mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mi.SaveAndReimport(); }
        var ci = AssetImporter.GetAtPath(PC + "abandoned_church1.fbx") as ModelImporter;
        if (ci != null && (ci.animationType != ModelImporterAnimationType.None || ci.importAnimation))
        { ci.animationType = ModelImporterAnimationType.None; ci.importAnimation = false; ci.importCameras = false; ci.importLights = false; ci.SaveAndReimport(); }
    }

    // ------------------------------------------------------------------ materials and meshes
    static Material Mat(string name, Texture tex, Material basedOn, Color color)
    {
        string path = GEN + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = basedOn != null ? new Material(basedOn) : new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        else if (basedOn != null) { m.shader = basedOn.shader; m.CopyPropertiesFromMaterial(basedOn); }
        m.name = name;
        m.mainTexture = tex;
        m.color = color;
        if (basedOn == null)
        {
            // matte, like the other PSX sets in the scene
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_GlossyReflections", 0f); m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D Tex(string file) { return AssetDatabase.LoadAssetAtPath<Texture2D>(PC + "Textures/" + file + ".png"); }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = GEN + name + ".asset";
        m.name = name;
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            // rewrite the existing asset in place (keeps its GUID, so the scene's references stay valid)
            old.Clear();
            old.indexFormat = m.indexFormat;
            old.SetVertices(m.vertices); old.SetNormals(m.normals); old.SetUVs(0, m.uv);
            old.subMeshCount = 1; old.SetTriangles(m.triangles, 0);
            old.RecalculateBounds(); old.name = name;
            EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(m);
            return old;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    struct V { public Vector3 p, n; public Vector2 uv; }
    static V Lerp(V a, V b, float t) { var r = new V(); r.p = Vector3.Lerp(a.p, b.p, t); r.n = Vector3.Lerp(a.n, b.n, t); r.uv = Vector2.Lerp(a.uv, b.uv, t); return r; }

    // keep the part of a convex polygon with x <= c (below) or x >= c
    static System.Collections.Generic.List<V> Clip(System.Collections.Generic.List<V> poly, float c, bool below)
    {
        var o = new System.Collections.Generic.List<V>();
        for (int i = 0; i < poly.Count; i++)
        {
            V a = poly[i], b = poly[(i + 1) % poly.Count];
            bool ina = below ? a.p.x <= c + 1e-5f : a.p.x >= c - 1e-5f;
            bool inb = below ? b.p.x <= c + 1e-5f : b.p.x >= c - 1e-5f;
            if (ina) o.Add(a);
            if (ina != inb) o.Add(Lerp(a, b, (c - a.p.x) / (b.p.x - a.p.x)));
        }
        return o;
    }

    static void Emit(System.Collections.Generic.List<V> poly, float dx, System.Collections.Generic.List<Vector3> P, System.Collections.Generic.List<Vector3> N, System.Collections.Generic.List<Vector2> U, System.Collections.Generic.List<int> T)
    {
        if (poly.Count < 3) return;
        int b0 = P.Count;
        for (int i = 0; i < poly.Count; i++) { P.Add(poly[i].p + new Vector3(dx, 0f, 0f)); N.Add(poly[i].n.normalized); U.Add(poly[i].uv); }
        for (int i = 1; i + 1 < poly.Count; i++) { T.Add(b0); T.Add(b0 + i); T.Add(b0 + i + 1); }
    }

    // Cut the room at x = cut and repeat the section [sec, cut] 'repeats' times: same walls, windows, floor and ceiling, just longer.
    static Mesh Lengthen(Mesh src, Matrix4x4 M, float cut, float sec, int repeats)
    {
        var sv = src.vertices; var sn = src.normals; var su = src.uv; var st = src.triangles;
        var P = new System.Collections.Generic.List<Vector3>(); var N = new System.Collections.Generic.List<Vector3>();
        var U = new System.Collections.Generic.List<Vector2>(); var T = new System.Collections.Generic.List<int>();
        float L = cut - sec;
        for (int i = 0; i < st.Length; i += 3)
        {
            var poly = new System.Collections.Generic.List<V>();
            for (int k = 0; k < 3; k++)
            {
                int j = st[i + k]; var v = new V();
                v.p = M.MultiplyPoint3x4(sv[j]); v.n = M.MultiplyVector(sn[j]).normalized; v.uv = su[j];
                poly.Add(v);
            }
            Emit(Clip(poly, cut, true), 0f, P, N, U, T);
            Emit(Clip(poly, cut, false), L * repeats, P, N, U, T);
            var section = Clip(Clip(poly, cut, true), sec, false);
            for (int r = 0; r < repeats; r++) Emit(section, L * (r + 1), P, N, U, T);
        }
        var m = new Mesh();
        if (P.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(P); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
        m.RecalculateBounds();
        return m;
    }

    // A box with its pivot at the bottom centre of its back face (z = 0); 'front' faces +z. Front/back map the whole texture,
    // the other faces map 'edge' (a thin strip of it).
    static Mesh Box(Vector3 size, Rect front, Rect edge)
    {
        float hx = size.x * 0.5f, h = size.y, d = size.z;
        var P = new System.Collections.Generic.List<Vector3>(); var N = new System.Collections.Generic.List<Vector3>();
        var U = new System.Collections.Generic.List<Vector2>(); var T = new System.Collections.Generic.List<int>();
        System.Action<Vector3, Vector3, Vector3, Vector3, Vector3, Rect> quad = (a, b, c, e, n, r) =>
        {
            int b0 = P.Count;
            P.Add(a); P.Add(b); P.Add(c); P.Add(e);
            for (int i = 0; i < 4; i++) N.Add(n);
            U.Add(new Vector2(r.xMin, r.yMin)); U.Add(new Vector2(r.xMax, r.yMin)); U.Add(new Vector2(r.xMax, r.yMax)); U.Add(new Vector2(r.xMin, r.yMax));
            T.Add(b0); T.Add(b0 + 2); T.Add(b0 + 1); T.Add(b0); T.Add(b0 + 3); T.Add(b0 + 2);
        };
        // front (+z): seen from +z, left is +x
        quad(new Vector3(hx, 0, d), new Vector3(-hx, 0, d), new Vector3(-hx, h, d), new Vector3(hx, h, d), Vector3.forward, front);
        // back (-z)
        quad(new Vector3(-hx, 0, 0), new Vector3(hx, 0, 0), new Vector3(hx, h, 0), new Vector3(-hx, h, 0), Vector3.back, front);
        // sides, top, bottom
        quad(new Vector3(hx, 0, 0), new Vector3(hx, 0, d), new Vector3(hx, h, d), new Vector3(hx, h, 0), Vector3.right, edge);
        quad(new Vector3(-hx, 0, d), new Vector3(-hx, 0, 0), new Vector3(-hx, h, 0), new Vector3(-hx, h, d), Vector3.left, edge);
        quad(new Vector3(-hx, h, 0), new Vector3(hx, h, 0), new Vector3(hx, h, d), new Vector3(-hx, h, d), Vector3.up, edge);
        quad(new Vector3(-hx, 0, d), new Vector3(hx, 0, d), new Vector3(hx, 0, 0), new Vector3(-hx, 0, 0), Vector3.down, edge);
        var m = new Mesh();
        m.SetVertices(P); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ scene helpers
    static Transform FindInScene(string name, Transform under)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() && (under == null || t.IsChildOf(under))) return t;
        return null;
    }

    static GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // A copy of one of the pack's meshes (by FBX node name) as a static child of 'parent'.
    static GameObject PackProp(Transform parent, GameObject pack, string node, string name, System.Collections.Generic.Dictionary<string, Material> mats)
    {
        var src = pack.transform.Find(node);
        var go = NewChild(name, parent);
        go.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials.Select(m => mats[TexFor(m != null ? m.name : "")]).ToArray();
        go.transform.localRotation = src.localRotation;
        go.transform.localScale = src.localScale;
        go.transform.localPosition = src.localPosition;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // world-space scale + an extra spin about world up, then move so its bounds sit on 'point' (bottom centre, or centre when hanging)
    static void Settle(GameObject go, float worldScale, float spinY, Vector3 point, bool hang)
    {
        var t = go.transform;
        float parentScale = t.parent != null ? t.parent.lossyScale.x : 1f;
        t.localScale = t.localScale * (worldScale / parentScale);
        t.rotation = Quaternion.Euler(0f, spinY, 0f) * t.rotation;
        var b = go.GetComponent<Renderer>().bounds;
        var anchor = hang ? b.center : new Vector3(b.center.x, b.min.y, b.center.z);
        t.position += point - anchor;
    }

    static string TexFor(string matName)
    {
        var n = matName.ToLower();
        if (n.StartsWith("church")) return "church1";
        if (n.StartsWith("material.009") || n.StartsWith("book")) return "Book";
        if (n.StartsWith("material")) return "Bench";
        if (n.StartsWith("wood")) return "wood";
        if (n.StartsWith("lectern")) return "Lectern";
        if (n.StartsWith("candle")) return "Candle-Stand";
        if (n.StartsWith("ceilinglamp")) return "Chandelier";
        if (n.StartsWith("character")) return "Crucified-Corpse";
        if (n.StartsWith("box")) return "Cardboard-Box";
        return "church1";
    }

    static void NotWalkable(GameObject go)
    {
        var mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        mod.overrideArea = true; mod.area = 1; mod.applyToChildren = true;
    }

    // ------------------------------------------------------------------ the chapel
    static string BuildChapel()
    {
        var log = new System.Text.StringBuilder();
        var eg = FindInScene("eglise-sketchfab5", null);
        if (eg == null) return "church: eglise-sketchfab5 not found";
        if (eg.Find("Interior PSX") != null) return "church: already reworked (Interior PSX exists)";
        var pack = AssetDatabase.LoadAssetAtPath<GameObject>(PC + "abandoned_church1.fbx");
        if (pack == null) return "church: " + PC + "abandoned_church1.fbx missing";

        // materials, one per pack texture
        var mats = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var tx in new[] { "church1", "Bench", "Book", "wood", "Lectern", "Candle-Stand", "Chandelier", "Crucified-Corpse", "Cardboard-Box" })
            mats[tx] = Mat("Church " + tx, Tex(tx), null, Color.white);
        var cloth = Mat("Church altar cloth", null, null, new Color(0.30f, 0.05f, 0.05f));
        var doorMat = Mat("Church door (outside)", AssetDatabase.LoadAssetAtPath<Texture2D>(EXT_DOOR), null, Color.white);

        // the room: the pack's shell, lengthened
        var shellNode = pack.transform.Find("Cube.014");
        var M = Matrix4x4.TRS(shellNode.localPosition, shellNode.localRotation, shellNode.localScale);
        var shellMesh = SaveMesh(Lengthen(shellNode.GetComponent<MeshFilter>().sharedMesh, M, N_CUT, N_SEC, REPEATS), "Church shell (12 bays)");
        var doorMesh = SaveMesh(Box(new Vector3(DOOR_W, DOOR_H, DOOR_D), new Rect(0, 0, 1, 1), new Rect(0.0f, 0.1f, 0.05f, 0.8f)), "Church door panel");
        var planks = new Rect(0.38f, 0.63f, 0.36f, 0.36f);   // the dark vertical planks in the church atlas
        var altarMesh = SaveMesh(Box(new Vector3(ALTAR_WIDTH, ALTAR_TOP, ALTAR_DEPTH), planks, planks), "Church altar");
        var clothMesh = SaveMesh(Box(new Vector3(ALTAR_WIDTH + 0.3f, 0.05f, ALTAR_DEPTH * 0.55f), new Rect(0, 0, 1, 1), new Rect(0, 0, 1, 1)), "Church altar cloth");

        var root = NewChild("Interior PSX", eg);
        root.transform.position = P0; root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); root.transform.localScale = Vector3.one * S;
        var shell = NewChild("Shell", root.transform);
        shell.AddComponent<MeshFilter>().sharedMesh = shellMesh;
        shell.AddComponent<MeshRenderer>().sharedMaterial = mats["church1"];
        shell.AddComponent<MeshCollider>().sharedMesh = shellMesh;
        GameObjectUtility.SetStaticEditorFlags(shell, StaticEditorFlags.BatchingStatic);

        float altarWall = AltarWallX;                          // world x of the altar wall
        float floor = FLOOR_Y;
        float altarCx = altarWall + ALTAR_GAP + ALTAR_DEPTH * 0.5f;
        float brideX = altarCx + BRIDE_AHEAD;

        // pews: a 5.6-wide centre aisle (the Bride's candles stand 2.4 out from the middle), eight rows a side
        var pews = NewChild("Pews", root.transform); NotWalkable(pews);
        int n = 0;
        for (float x = ENTRANCE_X - 6.5f; x > brideX + 6.0f; x -= 4.05f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                n++;
                bool fallen = (n == 7 || n == 12);
                var p = PackProp(pews.transform, pack, fallen ? "Cube.009" : "Cube.001", "Pew " + n, mats);
                Settle(p, 0.955f, fallen ? 8f * side : 0f, new Vector3(x, floor, CENTER_Z + side * (fallen ? 3.7f : 3.98f)), false);
                var bc = p.AddComponent<BoxCollider>();
            }
        }
        log.AppendLine("church: " + n + " pews");

        // chancel: altar (the Bride's), cloth, lectern, the crucified figure on the end wall
        var chancel = NewChild("Chancel", root.transform);
        var altar = NewChild("Altar", chancel.transform); NotWalkable(altar);
        altar.AddComponent<MeshFilter>().sharedMesh = altarMesh;
        altar.AddComponent<MeshRenderer>().sharedMaterial = mats["church1"];
        altar.AddComponent<BoxCollider>();
        altar.transform.localScale = Vector3.one / S;
        altar.transform.rotation = Quaternion.Euler(0f, 90f, 0f);                       // front faces the nave (+x)
        altar.transform.position = new Vector3(altarCx - ALTAR_DEPTH * 0.5f, floor, CENTER_Z);
        GameObjectUtility.SetStaticEditorFlags(altar, StaticEditorFlags.BatchingStatic);
        var clothGo = NewChild("Altar Cloth", chancel.transform);
        clothGo.AddComponent<MeshFilter>().sharedMesh = clothMesh;
        clothGo.AddComponent<MeshRenderer>().sharedMaterial = cloth;
        clothGo.transform.localScale = Vector3.one / S;
        clothGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        clothGo.transform.position = new Vector3(altarCx - ALTAR_DEPTH * 0.275f, floor + ALTAR_TOP, CENTER_Z);
        var lectern = PackProp(chancel.transform, pack, "Cube.013", "Lectern", mats); NotWalkable(lectern);
        Settle(lectern, 1.15f, 0f, new Vector3(brideX + 1.5f, floor, CENTER_Z - 4.3f), false);
        lectern.AddComponent<BoxCollider>();
        var corpse = PackProp(chancel.transform, pack, "Cylinder.006", "Crucified Figure", mats);
        Settle(corpse, 1.05f, 0f, new Vector3(altarWall + 0.35f, floor + 5.4f, CENTER_Z), true);

        // chandeliers along the ridge, candle stands at the chancel and the door, a cross over the door, clutter
        var lamps = NewChild("Chandeliers", root.transform);
        int li = 0;
        for (float x = ENTRANCE_X - 6.0f; x > altarWall + 4f; x -= 10.1f)
        {
            var c = PackProp(lamps.transform, pack, "Cylinder.003", "Chandelier " + (++li), mats);
            Settle(c, 1.2f, 0f, new Vector3(x, floor + 9.75f - 1.2f * 1.8f * 0.5f, CENTER_Z), true);
        }
        var candles = NewChild("Candle Stands", root.transform); NotWalkable(candles);
        var cpos = new[] { new Vector3(altarWall + 1.2f, 0, -5.4f), new Vector3(altarWall + 1.2f, 0, 5.4f), new Vector3(brideX + 2.5f, 0, 3.4f), new Vector3(ENTRANCE_X - 1.3f, 0, -2.4f), new Vector3(ENTRANCE_X - 1.3f, 0, 2.4f) };
        for (int i = 0; i < cpos.Length; i++)
        {
            var c = PackProp(candles.transform, pack, i % 2 == 0 ? "Cylinder" : "Cylinder.001", "Candle Stand " + (i + 1), mats);
            Settle(c, 1.2f, 0f, new Vector3(cpos[i].x, floor, CENTER_Z + cpos[i].z), false);
            c.AddComponent<BoxCollider>();
        }
        var cross = PackProp(root.transform, pack, "Cube.011", "Cross Over Door", mats);
        Settle(cross, S, 0f, new Vector3(ENTRANCE_X - 0.1f, floor + DOOR_H + 1.1f, CENTER_Z), true);
        var clutter = NewChild("Clutter", root.transform); NotWalkable(clutter);
        var box1 = PackProp(clutter.transform, pack, "Cube", "Box 1", mats); Settle(box1, 1.3f, 20f, new Vector3(ENTRANCE_X - 2.2f, floor, CENTER_Z + 5.0f), false); box1.AddComponent<BoxCollider>();
        var box2 = PackProp(clutter.transform, pack, "Cube.002", "Box 2", mats); Settle(box2, 1.3f, -10f, new Vector3(ENTRANCE_X - 3.1f, floor, CENTER_Z + 5.1f), false); box2.AddComponent<BoxCollider>();
        var lb = lectern.GetComponent<Renderer>().bounds;
        var book = PackProp(chancel.transform, pack, "Cube.010", "Bible", mats); Settle(book, 1.2f, 15f, new Vector3(lb.center.x, lb.max.y + 0.01f, lb.center.z), false);

        // the navmesh for the new room only (the old cathedral's three surfaces stay as baked)
        var surf = root.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
        surf.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
        surf.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surf.layerMask = ~0;
        Undo.RegisterCreatedObjectUndo(root, UNDO);

        // hide the cathedral (the Secret Room, the three doors and the dormant Sun stay)
        int hidden = 0;
        foreach (Transform c in eg)
        {
            if (c.name == "SecretRoom" || c.name.StartsWith("Door (") || c.name == "Interior PSX" || c.name == "Sun") continue;
            if (!c.gameObject.activeSelf) continue;
            Undo.RecordObject(c.gameObject, UNDO); c.gameObject.SetActive(false); hidden++;
        }
        log.AppendLine("church: hid " + hidden + " cathedral parts");

        // doors: the outside church's door, on the new walls
        DoorTo(eg, "Door (3)", new Vector3(ENTRANCE_X - 0.02f, floor, CENTER_Z), -90f, doorMesh, doorMat, log);       // exit, in the entrance doorway
        DoorTo(eg, "Door (2)", new Vector3(altarWall + 0.02f, floor, CENTER_Z + SIDE_DOOR_Z), 90f, doorMesh, doorMat, log);   // staircase: right of the altar
        DoorTo(eg, "Door (4)", new Vector3(altarWall + 0.02f, floor, CENTER_Z - SIDE_DOOR_Z), 90f, doorMesh, doorMat, log);   // secret room: left of the altar

        // gameplay markers, same offsets from the floor as before
        Mark("ChurchSpawn", new Vector3(ENTRANCE_X - 3.5f, floor + 1.51f, CENTER_Z + 0.24f), -90f, log);            // faces the altar
        Mark("BrideSpawn", new Vector3(brideX, floor, CENTER_Z + 0.15f), float.NaN, log);                  // on the floor: NPCAntiPush pins an idle killer at the spawn height
        Mark("ChurchBrideTrigger", new Vector3(ENTRANCE_X - 14.0f, floor + 0.76f, CENTER_Z + 0.1f), float.NaN, log);   // ~10 past the spawn, ~40 short of her
        // the altar end stands on the shell's chancel platform, 0.29 above the nave floor: pivot 1.51 over it (as ChurchSpawn) so the player arrives standing on it
        Mark("InfiniteStaircaseOutsideDoor", new Vector3(altarWall + 1.7f, floor + 1.80f, CENTER_Z + SIDE_DOOR_Z), 90f, log);
        Mark("ChurchSecretRoomSpawn", new Vector3(altarWall + 1.7f, floor + 1.80f, CENTER_Z - SIDE_DOOR_Z), 90f, log);

        // bake
        Physics.SyncTransforms();
        surf.BuildNavMesh();
        var data = surf.navMeshData;
        if (data != null)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NAV_ASSET) != null) AssetDatabase.DeleteAsset(NAV_ASSET);
            AssetDatabase.CreateAsset(data, NAV_ASSET);
            surf.RemoveData(); surf.navMeshData = data; surf.AddData();
            log.AppendLine("church: navmesh baked -> " + NAV_ASSET);
        }
        else log.AppendLine("church: NAVMESH BAKE FAILED");
        log.AppendLine("church: altar wall x " + altarWall.ToString("F2") + ", BrideSpawn x " + brideX.ToString("F2") + ", aisle to door " + (ENTRANCE_X - brideX).ToString("F1"));
        return log.ToString();
    }

    static void DoorTo(Transform eg, string name, Vector3 pos, float yaw, Mesh mesh, Material mat, System.Text.StringBuilder log)
    {
        var d = eg.Find(name);
        if (d == null) { log.AppendLine("church: " + name + " missing"); return; }
        Undo.RecordObject(d, UNDO);
        d.position = pos; d.rotation = Quaternion.Euler(0f, yaw, 0f); d.localScale = Vector3.one;
        if (d.parent != null && d.parent.lossyScale != Vector3.one) d.localScale = new Vector3(1f / d.parent.lossyScale.x, 1f / d.parent.lossyScale.y, 1f / d.parent.lossyScale.z);
        var mf = d.GetComponent<MeshFilter>(); if (mf != null) { Undo.RecordObject(mf, UNDO); mf.sharedMesh = mesh; }
        var mr = d.GetComponent<MeshRenderer>(); if (mr != null) { Undo.RecordObject(mr, UNDO); mr.sharedMaterials = new[] { mat }; }
        var mc = d.GetComponent<MeshCollider>(); if (mc != null) { Undo.RecordObject(mc, UNDO); mc.sharedMesh = mesh; mc.convex = false; }
        log.AppendLine("church: " + name + " -> " + pos.ToString("F2"));
    }

    static void Mark(string name, Vector3 pos, float yaw, System.Text.StringBuilder log)
    {
        var t = FindInScene(name, null);
        if (t == null) { log.AppendLine("church: marker " + name + " missing"); return; }
        Undo.RecordObject(t, UNDO);
        t.position = pos;
        if (!float.IsNaN(yaw)) t.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ------------------------------------------------------------------ the staircase
    static string BuildTunnel()
    {
        var st = FindInScene("InfiniteStaircase", null);
        if (st == null) return "stairs: InfiniteStaircase not found";
        if (st.Find("Kit Tunnel") != null) return "stairs: already reworked (Kit Tunnel exists)";
        var kit = AssetDatabase.LoadAssetAtPath<GameObject>(KIT);
        if (kit == null) return "stairs: kit missing";

        // the walking surface along the centre line, sampled before anything changes (rays start just under the flat ceiling)
        Physics.SyncTransforms();
        const float cx = -162.4f, z0 = 118f, dz = 0.25f; const int nz = 1200;
        var prof = new float[nz + 1]; var has = new bool[nz + 1];
        for (int k = 0; k <= nz; k++)
        {
            RaycastHit h;
            if (Physics.Raycast(new Vector3(cx, -647f, z0 + k * dz), Vector3.down, out h, 300f, ~0, QueryTriggerInteraction.Ignore)) { prof[k] = h.point.y; has[k] = true; }
        }
        System.Func<float, float> raw = (z) =>
        {
            int k = Mathf.Clamp(Mathf.RoundToInt((z - z0) / dz), 0, nz);
            for (int d = 0; d < 60; d++) { if (k - d >= 0 && has[k - d]) return prof[k - d]; if (k + d <= nz && has[k + d]) return prof[k + d]; }
            return -651.92f;
        };
        System.Func<float, float> floorAt = (z) => { float s = 0f; int c = 0; for (float d = -1.2f; d <= 1.21f; d += 0.3f) { s += raw(z + d); c++; } return s / c; };

        // hide the stretched-photo walls and the flat ceiling; their colliders stay as the walls you bump into
        int hidden = 0;
        foreach (Transform seg in st)
        {
            if (seg.name.StartsWith("Segment"))
                foreach (Transform c in seg)
                {
                    if (!c.name.StartsWith("Cube")) continue;
                    var r = c.GetComponent<MeshRenderer>(); if (r != null && r.enabled) { Undo.RecordObject(r, UNDO); r.enabled = false; hidden++; }
                    // the stairs' right-hand curb pokes 0.3 past the old wall plane, so the right wall comes in to x -159.16
                    if (c.position.x > -162.6f && Mathf.Abs(c.position.x - (-157.82f)) < 0.05f) { Undo.RecordObject(c, UNDO); c.position += new Vector3(-0.34f, 0f, 0f); }
                }
            if (seg.name == "Ceiling")
                foreach (Transform c in seg) { var r = c.GetComponent<MeshRenderer>(); if (r != null && r.enabled) { Undo.RecordObject(r, UNDO); r.enabled = false; hidden++; } }
        }

        var tunnel = new GameObject("Kit Tunnel");
        tunnel.transform.SetParent(st, true); tunnel.transform.position = Vector3.zero; tunnel.transform.rotation = Quaternion.identity; tunnel.transform.localScale = Vector3.one;
        var walls = NewChild("Walls", tunnel.transform); var roof = NewChild("Ceiling", tunnel.transform);
        var wallSrc = kit.transform.Find("BaseWall"); var ceilSrc = kit.transform.Find("Half_Floor1");
        System.Func<Transform, Transform, Vector3, Quaternion, GameObject> place = (src, parent, centre, q) =>
        {
            var go = NewChild(src.name, parent);
            go.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
            go.transform.rotation = q * src.localRotation; go.transform.localScale = src.localScale * 2f;
            var b = go.GetComponent<MeshRenderer>().bounds; go.transform.position += centre - b.center;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        };
        const float inR = -159.16f, inL = -166.04f, half = 0.345f, CEIL = 8.5f, zStart = 126.4f, zEnd = 400f;
        int nw = 0, nc = 0;
        for (float a = zStart; a < zEnd; a += 5.9f)
        {
            float lo = Mathf.Min(floorAt(a), floorAt(a + 6.04f)) - 2.0f;
            float hi = Mathf.Max(floorAt(a), floorAt(a + 6.04f)) + CEIL + 1.5f;
            for (float y = lo; y < hi; y += 6f)
            {
                place(wallSrc, walls.transform, new Vector3(inR + half, y + 3f, a + 3.02f), Quaternion.Euler(0f, 90f, 0f)); nw++;
                place(wallSrc, walls.transform, new Vector3(inL - half, y + 3f, a + 3.02f), Quaternion.Euler(0f, -90f, 0f)); nw++;
            }
        }
        for (float a = 125.4f; a < zEnd; a += 3.0f)
        {
            float b = a + 3.0f;
            float ya = (a < 134f ? -651.92f : floorAt(a)) + CEIL, yb = (b < 134f ? -651.92f : floorAt(b)) + CEIL;
            if (a < 134f) ya = Mathf.Min(ya, -646f); if (b < 134f) yb = Mathf.Min(yb, -646f);
            float pitch = Mathf.Atan2(ya - yb, b - a) * Mathf.Rad2Deg;
            place(ceilSrc, roof.transform, new Vector3(-162.43f, (ya + yb) * 0.5f, (a + b) * 0.5f), Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(180f, 0f, 0f)); nc++;
        }
        Undo.RegisterCreatedObjectUndo(tunnel, UNDO);
        return "stairs: hid " + hidden + " cube/ceiling renderers, built " + nw + " wall slabs + " + nc + " ceiling tiles";
    }

    // ------------------------------------------------------------------ Father Joseph
    // head and lowest foot joint above the root, in the idle pose of the controller it runs on (world units)
    static Vector2 HeadFoot(GameObject inst)
    {
        var an = inst.GetComponentInChildren<Animator>();
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate; an.applyRootMotion = false; an.Rebind(); an.Update(0.3f);
        float baseY = inst.transform.position.y, foot = float.PositiveInfinity;
        foreach (var hb in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
        { var t = an.GetBoneTransform(hb); if (t != null) foot = Mathf.Min(foot, t.position.y); }
        var head = an.GetBoneTransform(HumanBodyBones.Head);
        return new Vector2((head != null ? head.position.y : baseY + 2.6f) - baseY, foot - baseY);
    }

    static string ReworkPriest()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FJ + "Character_Male_51.fbx");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FJ + "Male_51_priest.png");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PRIEST);
        if (model == null || tex == null || prefab == null) return "priest: Male_51 model/texture or Priest.prefab missing";
        var oldModel = prefab.transform.Find("Priest");
        var oldAnimA = oldModel != null ? oldModel.GetComponent<Animator>() : null;
        if (oldAnimA == null) return "priest: model child 'Priest' with an Animator not found";
        if (oldAnimA.avatar != null && oldAnimA.avatar.name.StartsWith("Character_Male_51")) return "priest: already Male_51";
        Avatar av = null; foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FJ + "Character_Male_51.fbx")) if (o is Avatar) av = (Avatar)o;
        var controller = oldAnimA.runtimeAnimatorController;

        // measure the old priest and the new man on the same controller, off to the side, then size and lift him to match
        var far = new Vector3(0f, -5000f, 0f);
        var oldInst = (GameObject)UnityEngine.Object.Instantiate(prefab, far, Quaternion.identity); oldInst.hideFlags = HideFlags.HideAndDontSave;
        Vector2 oldHF = HeadFoot(oldInst);
        float rootScale = prefab.transform.localScale.x;
        UnityEngine.Object.DestroyImmediate(oldInst);
        var probeRoot = new GameObject("probe"); probeRoot.hideFlags = HideFlags.HideAndDontSave; probeRoot.transform.position = far; probeRoot.transform.localScale = Vector3.one * rootScale;
        var probe = (GameObject)UnityEngine.Object.Instantiate(model, probeRoot.transform, false); probe.hideFlags = HideFlags.HideAndDontSave;
        var pan = probe.GetComponent<Animator>(); if (pan == null) pan = probe.AddComponent<Animator>();
        pan.avatar = av; pan.runtimeAnimatorController = controller;
        Vector2 newHF = HeadFoot(probeRoot);
        UnityEngine.Object.DestroyImmediate(probeRoot);
        float k = (oldHF.x - oldHF.y) / Mathf.Max(0.01f, newHF.x - newHF.y);
        float lift = oldHF.y - newHF.y * k;                    // world units, so the feet meet the ground where the old ones did

        Material baseMat = null;
        var harold = AssetDatabase.LoadAssetAtPath<GameObject>(HAROLD);
        if (harold != null) { var hs = harold.GetComponentInChildren<SkinnedMeshRenderer>(true); if (hs != null) baseMat = hs.sharedMaterial; }
        var mat = Mat("Father Joseph (Male_51)", tex, baseMat, Color.white);
        mat.mainTexture = tex; EditorUtility.SetDirty(mat);

        var contents = PrefabUtility.LoadPrefabContents(PRIEST);
        try
        {
            var old = contents.transform.Find("Priest");
            var oldAnim = old.GetComponent<Animator>();
            var cull = oldAnim.cullingMode;
            int layer = old.gameObject.layer; var lp = old.localPosition; var lr = old.localRotation; int idx = old.GetSiblingIndex();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, contents.transform);
            inst.name = "Priest";
            inst.transform.SetSiblingIndex(idx);
            inst.transform.localRotation = lr;
            inst.transform.localScale = Vector3.one * k;
            inst.transform.localPosition = lp + new Vector3(0f, lift / rootScale, 0f);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var an = inst.GetComponent<Animator>(); if (an == null) an = inst.AddComponent<Animator>();
            an.avatar = av; an.runtimeAnimatorController = controller; an.applyRootMotion = false; an.cullingMode = cull;
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            smr.sharedMaterials = new[] { mat };
            PrefabUtility.SaveAsPrefabAsset(contents, PRIEST);
            return "priest: Priest.prefab now uses Character_Male_51 (" + (av != null ? av.name : "no avatar") + "); old head/foot " + oldHF.ToString("F2") + ", new " + (newHF * k).ToString("F2") + " + lift " + lift.ToString("F2") + " (scale " + k.ToString("F3") + ")";
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
