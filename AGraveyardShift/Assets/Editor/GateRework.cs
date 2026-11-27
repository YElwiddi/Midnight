using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// The "Epitaph" gates (pitched 26 Nov 2026): a carved stone gateway at the front gate (HODIE MIHI · CRAS TIBI over
/// MDCCCXCIX) and its older sibling at the back gate (NEMO INTRET, chained shut), built from the project's own 128 px
/// textures. Apply builds the meshes and materials under Assets/Gate Rework and edits the open Scene 0.5 Dungeon in one
/// undo step: the old pillars are switched off, and the four gate leaves keep their objects (names, scripts, colliders,
/// layers) and get new iron meshes on today's hinge sides. Remove puts the old gates back from the saved backup.
/// </summary>
public static class GateRework
{
    const string RootDir = "Assets/Gate Rework";
    const string GenDir = RootDir + "/Generated";
    const string MatDir = RootDir + "/Materials";
    const string TexDir = RootDir + "/Textures";
    const string BackupPath = RootDir + "/Backup (before Epitaph).asset";
    const string AM = "Assets/Additional Models and Assets/";
    const string PCP = AM + "PSX Cemetery Pack/Models/";
    const string DungeonPath = "Assets/Scenes/Scene 0.5 Dungeon.unity";
    public const string FrontRootName = "Epitaph Gate (front)";
    public const string BackRootName = "Epitaph Gate (back)";

    static readonly string[] LeafNames = { "FrontGate1", "FrontGate2", "BackGate1", "BackGate2" };
    static readonly string[] PillarPaths = { "NewGate/Pilar (2)", "NewGate/Pilar (3)", "NewGate/Pilar (7)", "NewGate/Pilar (8)" };
    static readonly string[] LampNames = { "VicLamp (1)", "VicLamp" };
    const float LampShadowNear = 0.6f;

    // ---- shape, in metres (the graveyard is built at about 1.45x human scale)
    const float ShaftW = 1.9f;
    const float Spring = 5.45f, Rise = 1.0f, Ring = 0.60f, ArchDepth = 1.15f;
    const float FriezeH = 1.45f, FriezeHalfDepth = 0.62f;
    const float LeafH = 4.75f, LeafBow = 0.45f, BackLeafH = 4.6f, BackLeafBow = 0.30f;
    const float LanternIntensity = 2.6f, LanternRange = 11f;
    static readonly Color LampColour = new Color(1f, 0.62f, 0.26f);

    [MenuItem("Tools/Graveyard/Gate Rework/Apply Epitaph")]
    static void ApplyMenu()
    {
        string r = Apply();
        Debug.Log("[Gate Rework] " + r);
        EditorUtility.DisplayDialog("Gate Rework", r, "OK");
    }

    [MenuItem("Tools/Graveyard/Gate Rework/Remove Epitaph")]
    static void RemoveMenu()
    {
        string r = Remove();
        Debug.Log("[Gate Rework] " + r);
        EditorUtility.DisplayDialog("Gate Rework", r, "OK");
    }

    // =====================================================================================================  apply
    public static string Apply()
    {
        var scene = SceneManager.GetSceneByPath(DungeonPath);
        if (!scene.IsValid() || !scene.isLoaded) return "Open Scene 0.5 Dungeon first, then run Apply Epitaph again.";
        var map = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GraveyardMap");
        if (map == null) return "GraveyardMap not found in Scene 0.5 Dungeon.";
        var log = new System.Text.StringBuilder();

        EnsureFolders();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Epitaph gates");

        var M = BuildMaterials();
        var leaves = LeafNames.Select(n => map.transform.Find(n)).ToArray();
        var pillars = PillarPaths.Select(p => map.transform.Find(p)).ToArray();
        if (leaves.Any(l => l == null) || pillars.Any(p => p == null)) return "A gate leaf or pillar is missing; nothing changed.";
        var prop = map.transform.Find("Camera_Front_Gate");
        var feedRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CCTV_Cameras");
        var feed = feedRoot != null ? feedRoot.transform.Find("CCTV_FrontGateCam") : null;
        SaveBackup(leaves, prop, feed);

        // pier centres = today's pillars (the fences end inside them)
        Vector3 pL = pillars[0].position, pR = pillars[1].position, bL = pillars[3].position, bR = pillars[2].position;
        foreach (var p in pillars) { Undo.RecordObject(p.gameObject, "Apply Epitaph"); p.gameObject.SetActive(false); }

        // The graveyard lamps' lights sit inside their lamp heads; with the shadow near plane at 0.2 m the head's frame threw
        // magnified hard shadows (a dark diamond and stripes) across the new stonework. 0.6 m clears the head; posts, leaves
        // and visitors still cast shadows as before.
        foreach (var lampName in LampNames)
        {
            var lamp = map.transform.Find(lampName);
            var ll = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            if (ll == null) continue;
            Undo.RecordObject(ll, "Apply Epitaph");
            ll.shadowNearPlane = Mathf.Max(ll.shadowNearPlane, LampShadowNear);
        }

        // ---------------------------------------------------------------- front
        float fz = (pL.z + pR.z) * 0.5f, xc = (pL.x + pR.x) * 0.5f;
        float gy = GroundY(xc, fz);
        var front = new FrontShape(pL.x, pR.x, fz, gy);
        var frontRoot = Replace(map.transform, FrontRootName, new Vector3(xc, gy, fz));
        MakeMesh(frontRoot, "Piers", front.Gateway(false), "Epitaph Gate Front Piers", new[] { M["stone"], M["trim"], M["frieze"], M["iron"] }, true);
        MakeMesh(frontRoot, "Arch and frieze", front.Gateway(true), "Epitaph Gate Front Arch", new[] { M["stone"], M["trim"], M["frieze"], M["iron"] }, true, receive: false);
        MakeMesh(frontRoot, "Rain streaks", front.Streaks(), "Epitaph Gate Front Streaks", new[] { M["streaks"] }, false, false);
        foreach (var side in new[] { -1, 1 })
            Lantern(frontRoot, side < 0 ? "Lantern (left)" : "Lantern (right)", front.LanternHook(side), M, castShadows: side > 0);
        var leafMesh = SaveMesh(IronLeaf(front.LeafW, LeafH, LeafBow), "Epitaph Leaf Front");
        SetLeaf(leaves[0], new Vector3(front.InL + 0.04f, gy, fz), leafMesh, M["iron"]);
        SetLeaf(leaves[1], new Vector3(front.InR - 0.04f, gy, fz), leafMesh, M["iron"]);
        if (prop != null)
        {
            Undo.RecordObject(prop, "Apply Epitaph");
            prop.position = front.PropPos;
        }
        if (feed != null)
        {
            Undo.RecordObject(feed, "Apply Epitaph");
            feed.position = front.FeedPos;
        }
        log.AppendLine("Front gate: stone gateway, 2 lanterns (following VicLamp (1)), leaves re-meshed (" + front.LeafW.ToString("F2") + " m).");

        // ---------------------------------------------------------------- back
        float bz = (bL.z + bR.z) * 0.5f, bxc = (bL.x + bR.x) * 0.5f;
        float by = GroundY(bxc, bz);
        var back = new BackShape(bL.x, bR.x, bL.z, bR.z, bz, by);
        var backRoot = Replace(map.transform, BackRootName, new Vector3(bxc, by, bz));
        MakeMesh(backRoot, "Piers", back.Gateway(false), "Epitaph Gate Back Piers", new[] { M["moss"], M["trim"], M["lintel"], M["iron"] }, true);
        MakeMesh(backRoot, "Lintel", back.Gateway(true), "Epitaph Gate Back Lintel", new[] { M["moss"], M["trim"], M["lintel"], M["iron"] }, true, receive: false);
        MakeMesh(backRoot, "Rain streaks", back.Streaks(), "Epitaph Gate Back Streaks", new[] { M["streaks"] }, false, false);
        var locked = MakeMesh(backRoot, "Chain (locked)", back.ChainLocked(), "Epitaph Chain Locked", new[] { M["iron"] }, false);
        var dropped = MakeMesh(backRoot, "Chain (dropped)", back.ChainDropped(), "Epitaph Chain Dropped", new[] { M["iron"] }, false);
        var backLeaf = SaveMesh(IronLeaf(back.LeafW, BackLeafH, BackLeafBow), "Epitaph Leaf Back");
        SetLeaf(leaves[2], new Vector3(back.InL + 0.04f, by, bz), backLeaf, M["iron"]);
        SetLeaf(leaves[3], new Vector3(back.InR - 0.04f, by, bz), backLeaf, M["iron"]);
        var chainLock = Undo.AddComponent<GateChainLock>(backRoot.gameObject);
        chainLock.leaves = new[] { leaves[2].GetComponent<SwingingGate>(), leaves[3].GetComponent<SwingingGate>() };
        chainLock.lockedChain = locked; chainLock.droppedChain = dropped;
        dropped.SetActive(false);
        log.AppendLine("Back gate: NEMO INTRET gateway, chained (drops when either leaf opens), leaves re-meshed (" + back.LeafW.ToString("F2") + " m).");

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        log.Append("Done. Save the scene, then rebuild the title backdrop (Tools > Graveyard > Menu > Build Gate Backdrop).");
        return log.ToString();
    }

    // =====================================================================================================  remove
    public static string Remove()
    {
        var scene = SceneManager.GetSceneByPath(DungeonPath);
        if (!scene.IsValid() || !scene.isLoaded) return "Open Scene 0.5 Dungeon first, then run Remove Epitaph again.";
        var map = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GraveyardMap");
        var backup = AssetDatabase.LoadAssetAtPath<GateReworkBackup>(BackupPath);
        if (map == null || backup == null) return "No backup found at " + BackupPath + "; nothing changed.";
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Epitaph gates");
        foreach (var n in new[] { FrontRootName, BackRootName })
        {
            var t = map.transform.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
        foreach (var p in PillarPaths)
        {
            var t = map.transform.Find(p);
            if (t != null) { Undo.RecordObject(t.gameObject, "Remove Epitaph"); t.gameObject.SetActive(true); }
        }
        foreach (var b in backup.leaves)
        {
            var t = map.transform.Find(b.name);
            if (t == null) continue;
            Undo.RecordObject(t, "Remove Epitaph");
            t.position = b.position; t.localRotation = b.localRotation; t.localScale = b.localScale;
            var mf = t.GetComponent<MeshFilter>(); Undo.RecordObject(mf, "Remove Epitaph"); mf.sharedMesh = b.mesh;
            var mr = t.GetComponent<MeshRenderer>(); Undo.RecordObject(mr, "Remove Epitaph"); mr.sharedMaterials = b.materials;
            foreach (var mc in t.GetComponents<MeshCollider>()) { Undo.RecordObject(mc, "Remove Epitaph"); mc.sharedMesh = b.colliderMesh; }
            foreach (var bc in t.GetComponents<BoxCollider>()) { Undo.RecordObject(bc, "Remove Epitaph"); bc.center = b.boxCenter; bc.size = b.boxSize; }
            var ob = t.GetComponent<NavMeshObstacle>();
            if (ob != null) { Undo.RecordObject(ob, "Remove Epitaph"); ob.center = b.obstacleCenter; ob.size = b.obstacleSize; }
        }
        var prop = map.transform.Find("Camera_Front_Gate");
        if (prop != null) { Undo.RecordObject(prop, "Remove Epitaph"); prop.position = backup.frontPropPosition; prop.rotation = backup.frontPropRotation; }
        var feedRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CCTV_Cameras");
        var feed = feedRoot != null ? feedRoot.transform.Find("CCTV_FrontGateCam") : null;
        if (feed != null && backup.hasFeed) { Undo.RecordObject(feed, "Remove Epitaph"); feed.position = backup.frontFeedPosition; }
        for (int i = 0; i < backup.lampNames.Count; i++)
        {
            var lamp = map.transform.Find(backup.lampNames[i]);
            var ll = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            if (ll != null) { Undo.RecordObject(ll, "Remove Epitaph"); ll.shadowNearPlane = backup.lampShadowNear[i]; }
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        return "The old gates are back. Save the scene (and rebuild the title backdrop).";
    }

    // =====================================================================================================  scene helpers
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "Gate Rework");
        foreach (var sub in new[] { "Generated", "Materials", "Textures" })
            if (!AssetDatabase.IsValidFolder(RootDir + "/" + sub)) AssetDatabase.CreateFolder(RootDir, sub);
    }

    static void SaveBackup(Transform[] leaves, Transform prop, Transform feed)
    {
        if (AssetDatabase.LoadAssetAtPath<GateReworkBackup>(BackupPath) != null) return;   // keep the first (original) state
        var b = ScriptableObject.CreateInstance<GateReworkBackup>();
        foreach (var t in leaves)
        {
            var box = t.GetComponent<BoxCollider>(); var mc = t.GetComponent<MeshCollider>(); var ob = t.GetComponent<NavMeshObstacle>();
            b.leaves.Add(new GateReworkBackup.Leaf
            {
                name = t.name, position = t.position, localRotation = t.localRotation, localScale = t.localScale,
                mesh = t.GetComponent<MeshFilter>().sharedMesh, materials = t.GetComponent<MeshRenderer>().sharedMaterials,
                colliderMesh = mc != null ? mc.sharedMesh : null,
                boxCenter = box != null ? box.center : Vector3.zero, boxSize = box != null ? box.size : Vector3.zero,
                obstacleCenter = ob != null ? ob.center : Vector3.zero, obstacleSize = ob != null ? ob.size : Vector3.zero,
            });
        }
        if (prop != null) { b.frontPropPosition = prop.position; b.frontPropRotation = prop.rotation; }
        if (feed != null) { b.hasFeed = true; b.frontFeedPosition = feed.position; }
        var map = leaves[0].parent;
        foreach (var lampName in LampNames)
        {
            var lamp = map != null ? map.Find(lampName) : null;
            var ll = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            if (ll != null) { b.lampNames.Add(lampName); b.lampShadowNear.Add(ll.shadowNearPlane); }
        }
        AssetDatabase.CreateAsset(b, BackupPath);
    }

    static float GroundY(float x, float z)
    {
        Physics.SyncTransforms();
        float best = float.NaN, bestD = float.MaxValue;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = h.collider.name;
            bool ground = h.collider is TerrainCollider || n.StartsWith("Floor") || n.StartsWith("Road") || n.Contains("floor") || n.Contains("Ground");
            if (ground && h.distance < bestD) { bestD = h.distance; best = h.point.y; }
        }
        return float.IsNaN(best) ? 17.29f : best;
    }

    // a fresh child of GraveyardMap at a world position (an earlier build of the same name is removed first)
    static Transform Replace(Transform map, string name, Vector3 worldPos)
    {
        var old = map.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Apply Epitaph");
        go.transform.SetParent(map, false);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;
        return go.transform;
    }

    // a child holding one generated mesh; vertices come in world space and are baked relative to the parent
    static GameObject MakeMesh(Transform parent, string name, MB mb, string asset, Material[] mats, bool collider, bool shadows = true, bool receive = true)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Apply Epitaph");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
        var mesh = SaveMesh(mb.ToMesh(parent.worldToLocalMatrix), asset);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = receive;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    static Mesh SaveMesh(Mesh built, string name)
    {
        string path = GenDir + "/" + name + ".asset";
        built.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(built, path); return built; }
        EditorUtility.CopySerialized(built, existing);
        existing.name = name;
        Object.DestroyImmediate(built);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // re-mesh a gate leaf in place: same object, scripts and layer; hinge at the new pier face, scale 1
    static void SetLeaf(Transform leaf, Vector3 hinge, Mesh mesh, Material iron)
    {
        Undo.RecordObject(leaf, "Apply Epitaph");
        leaf.position = hinge;
        var ps = leaf.parent != null ? leaf.parent.lossyScale : Vector3.one;
        leaf.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        var mf = leaf.GetComponent<MeshFilter>(); Undo.RecordObject(mf, "Apply Epitaph"); mf.sharedMesh = mesh;
        var mr = leaf.GetComponent<MeshRenderer>(); Undo.RecordObject(mr, "Apply Epitaph"); mr.sharedMaterials = new[] { iron };
        var b = mesh.bounds;
        foreach (var mc in leaf.GetComponents<MeshCollider>()) { Undo.RecordObject(mc, "Apply Epitaph"); mc.sharedMesh = mesh; }
        foreach (var bc in leaf.GetComponents<BoxCollider>()) { Undo.RecordObject(bc, "Apply Epitaph"); bc.center = b.center; bc.size = b.size; }
        var ob = leaf.GetComponent<NavMeshObstacle>();
        if (ob != null) { Undo.RecordObject(ob, "Apply Epitaph"); ob.center = b.center; ob.size = b.size; }
    }

    // a hanging lantern: body (iron + glass) on the bracket's hook, a lamp light inside that follows VicLamp (1)
    static void Lantern(Transform parent, string name, Vector3 hook, Dictionary<string, Material> M, bool castShadows)
    {
        var holder = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(holder, "Apply Epitaph");
        holder.transform.SetParent(parent, false);
        holder.transform.position = hook;
        var mb = new MB(2);
        Vector3 lightPos = LanternBody(mb, 0, 1, hook, 0.46f, 0.88f);
        var mesh = SaveMesh(mb.ToMesh(holder.transform.worldToLocalMatrix), "Epitaph Lantern " + (name.Contains("left") ? "L" : "R"));
        holder.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = holder.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { M["iron"], M["glass"] };

        var lgo = new GameObject("Lamp light");
        Undo.RegisterCreatedObjectUndo(lgo, "Apply Epitaph");
        lgo.transform.SetParent(holder.transform, false);
        lgo.transform.position = lightPos;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.color = LampColour; l.intensity = LanternIntensity; l.range = LanternRange;
        l.renderMode = LightRenderMode.ForcePixel;
        l.shadows = castShadows ? LightShadows.Hard : LightShadows.None;
        l.shadowBias = 0.05f; l.shadowNormalBias = 0.4f; l.shadowNearPlane = 0.45f;   // clear of the lantern's own posts
        var src = Object.FindObjectsByType<LampHalo>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(h => h.gameObject.scene == parent.gameObject.scene);
        var halo = lgo.AddComponent<LampHalo>();
        var so = new SerializedObject(halo);
        if (src != null)
        {
            var sSrc = new SerializedObject(src);
            foreach (var f in new[] { "haloMaterial", "outerStrength", "coreStrength", "coreWhiten" })
                so.CopyFromSerializedProperty(sSrc.FindProperty(f));
        }
        so.FindProperty("size").floatValue = 1.6f;
        so.FindProperty("referenceIntensity").floatValue = LanternIntensity;
        so.ApplyModifiedPropertiesWithoutUndo();
        var link = lgo.AddComponent<GateLanternLink>();
        link.sourceName = "VicLamp (1)"; link.baseIntensity = LanternIntensity; link.glassRenderer = mr; link.glassMaterialIndex = 1;
    }

    // =====================================================================================================  materials
    static Dictionary<string, Material> BuildMaterials()
    {
        foreach (var t in new[] { "Epitaph Frieze.png", "Epitaph Lintel.png" })
        {
            var imp = AssetImporter.GetAtPath(TexDir + "/" + t) as TextureImporter;
            if (imp == null) continue;
            imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Bilinear; imp.anisoLevel = 1;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 512;
            imp.SaveAndReimport();
        }
        var M = new Dictionary<string, Material>();
        M["stone"] = Mat("Epitaph Stone", AM + "128x128/Bricks/Bricks_17-128x128.png", new Color(0.78f, 0.764f, 0.733f), 0.05f, 0f);
        M["trim"] = Mat("Epitaph Trim", PCP + "Textures/Graves/Graves_01_A.PNG", new Color(0.86f, 0.84f, 0.80f), 0.05f, 0f);
        M["frieze"] = Mat("Epitaph Frieze", TexDir + "/Epitaph Frieze.png", new Color(0.90f, 0.88f, 0.85f), 0.05f, 0f);
        M["moss"] = Mat("Epitaph Moss Stone", AM + "128x128 1/Brick/Horror_Brick_08-128x128.png", new Color(0.62f, 0.62f, 0.58f), 0.05f, 0f);
        M["lintel"] = Mat("Epitaph Lintel", TexDir + "/Epitaph Lintel.png", new Color(0.72f, 0.72f, 0.68f), 0.05f, 0f);
        M["iron"] = Mat("Epitaph Iron", AM + "128x128 1/Metal/Horror_Metal_03-128x128.png", new Color(0.62f, 0.577f, 0.546f), 0.45f, 0.45f);
        var glass = Mat("Epitaph Lantern Glass", null, new Color(1f, 0.75f, 0.45f), 0.2f, 0f);
        glass.EnableKeyword("_EMISSION"); glass.SetColor("_EmissionColor", LampColour * 1.6f);
        glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        M["glass"] = glass;
        var st = Mat("Epitaph Rain Streaks", AM + "128x128 1/Stains/Horror_Stain_12-128x128.png", new Color(0.55f, 0.52f, 0.50f, 0.85f), 0f, 0f);
        st.SetFloat("_Mode", 2f); st.SetOverrideTag("RenderType", "Transparent");
        st.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); st.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        st.SetInt("_ZWrite", 0); st.DisableKeyword("_ALPHATEST_ON"); st.EnableKeyword("_ALPHABLEND_ON"); st.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        st.renderQueue = 3000;
        M["streaks"] = st;
        foreach (var m in M.Values) EditorUtility.SetDirty(m);
        return M;
    }

    static Material Mat(string name, string tex, Color tint, float gloss, float metal)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Standard");
        m.mainTexture = tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(tex) : null;
        m.color = tint;
        m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        return m;
    }

    // =====================================================================================================  front shape
    class FrontShape
    {
        public readonly float XL, XR, Z, Gy, Xc, InL, InR, Span, R, Half, FriezeB, FriezeT, LeafW;
        public readonly Vector3 ArcC;
        public Vector3 FeedPos => new Vector3(XL + 0.68f, Gy + 6.66f, Z + ShaftW * 0.5f + 0.68f);
        public Vector3 PropPos => FeedPos + new Vector3(-0.10f, -0.25f, -0.25f);

        public FrontShape(float xl, float xr, float z, float gy)
        {
            XL = xl; XR = xr; Z = z; Gy = gy; Xc = (xl + xr) * 0.5f;
            InL = xl + ShaftW * 0.5f; InR = xr - ShaftW * 0.5f; Span = InR - InL;
            R = (Span * Span * 0.25f + Rise * Rise) / (2f * Rise);
            ArcC = new Vector3(Xc, gy + Spring + Rise - R, z);
            Half = Mathf.Asin(Span * 0.5f / R);
            FriezeB = gy + Spring + Rise + Ring - 0.02f; FriezeT = FriezeB + FriezeH;
            LeafW = Span * 0.5f - 0.05f;
        }

        // upper = the stonework above the gate (arch, spandrels, frieze, cornices, caps, finials). It goes in its own
        // renderer that receives no shadows: the gate lamp stands low in the yard and the hand-held torch swings, and both
        // threw magnified shadows of the leaves' tips up onto the arch (a dark diamond, crawling stripes).
        public MB Gateway(bool upper)
        {
            var mb = new MB(4);
            float g = Gy, z = Z, hs = ShaftW * 0.5f;
            foreach (var px in new[] { XL, XR })
            {
                if (!upper)
                {
                    // plinth and its moulding stand proud on three sides; flush with the shaft on the opening side
                    float inSide = px < Xc ? 1f : -1f;
                    AsymBox(mb, 1, px, z, 1.17f, hs, inSide, g - 0.2f, g + 1.0f, 0.5f);
                    AsymBox(mb, 1, px, z, 1.07f, hs, inSide, g + 1.0f, g + 1.2f, 0.5f);
                    mb.BoxAA(0, new Vector3(px - hs, g + 1.2f, z - hs), new Vector3(px + hs, FriezeT, z + hs), 0.55f);
                    mb.BoxAA(1, new Vector3(px - 1.03f, g + Spring - 0.22f, z - 1.03f), new Vector3(px + 1.03f, g + Spring, z + 1.03f), 0.5f);
                    // lantern bracket on the road face: arm, brace, scroll
                    Vector3 wall = new Vector3(px, g + 6.0f, z + hs), arm = wall + new Vector3(0f, 0f, 0.95f);
                    mb.Beam(3, wall, arm, 0.075f, 0.075f, 2f, Vector3.up);
                    mb.Beam(3, wall + new Vector3(0f, -0.65f, 0f), arm + new Vector3(0f, -0.03f, -0.25f), 0.055f, 0.055f, 2f, Vector3.up);
                    mb.Torus(3, wall + new Vector3(0f, -0.12f, 0.42f), Vector3.right, Vector3.forward, 0.14f, 0.024f, 0f, 10, 4);
                    mb.Cyl(3, arm + new Vector3(0f, 0.02f, 0f), arm + new Vector3(0f, -0.06f, 0f), 0.025f, 6, 2f);
                }
                else
                {
                    mb.BoxAA(1, new Vector3(px - 1.18f, FriezeT, z - 1.18f), new Vector3(px + 1.18f, FriezeT + 0.38f, z + 1.18f), 0.5f);
                    mb.BoxAA(1, new Vector3(px - 0.80f, FriezeT + 0.38f, z - 0.80f), new Vector3(px + 0.80f, FriezeT + 0.80f, z + 0.80f), 0.5f);
                    mb.Lathe(1, new Vector3(px, FriezeT + 0.80f, z), new[] { 0.42f, 0.42f, 0.22f, 0.20f, 0.30f, 0.50f, 0.56f, 0.50f, 0.30f, 0.0f },
                        new[] { 0f, 0.12f, 0.20f, 0.34f, 0.40f, 0.55f, 0.80f, 1.05f, 1.22f, 1.30f }, 12, 0.8f);
                }
            }
            if (!upper)
            {
                // CCTV bracket under the camera prop on the left pier
                Vector3 pp = PropPos, face = new Vector3(pp.x, pp.y - 0.22f, z + hs);
                mb.Beam(3, face + new Vector3(0f, 0f, 0.02f), new Vector3(pp.x, pp.y - 0.22f, pp.z), 0.06f, 0.06f, 2f, Vector3.up);
                mb.BoxAA(3, new Vector3(pp.x - 0.12f, pp.y - 0.40f, z + hs), new Vector3(pp.x + 0.12f, pp.y - 0.05f, z + hs + 0.02f), 2f, 32);
                return mb;
            }
            // the arch: voussoirs meet exactly (no overlapping faces), keystone proud
            int nv = 9;
            for (int i = 0; i < nv; i++)
            {
                float a0 = Mathf.PI * 0.5f + Half - 2f * Half * i / nv, a1 = Mathf.PI * 0.5f + Half - 2f * Half * (i + 1) / nv;
                bool key = i == nv / 2;
                mb.Voussoir(1, ArcC, Vector3.right, Vector3.up, Vector3.forward, R - (key ? 0.06f : 0f), R + Ring + (key ? 0.22f : 0f), a0, a1, ArchDepth + (key ? 0.10f : 0f), 0.6f);
            }
            // spandrels between the arch and the frieze (recessed behind the arch faces)
            int ns = 16; float sd = ArchDepth * 0.45f;
            for (int i = 0; i < ns; i++)
            {
                float x0 = InL + Span * i / ns, x1 = InL + Span * (i + 1) / ns;
                float y0 = ArcC.y + Mathf.Sqrt(Mathf.Max(0f, (R + Ring) * (R + Ring) - (x0 - Xc) * (x0 - Xc)));
                float y1 = ArcC.y + Mathf.Sqrt(Mathf.Max(0f, (R + Ring) * (R + Ring) - (x1 - Xc) * (x1 - Xc)));
                float yb = Mathf.Min(y0, y1) - 0.05f;
                if (i == 0) x0 -= 0.03f; if (i == ns - 1) x1 += 0.03f;
                mb.BoxAA(0, new Vector3(x0, yb, z - sd), new Vector3(x1, FriezeB, z + sd), 0.55f, 4 | 8);
            }
            // the frieze runs between the piers (its ends sit inside them): stone body, inscription as its two faces
            float fx0 = InL - 0.05f, fx1 = InR + 0.05f;
            mb.BoxAA(1, new Vector3(fx0, FriezeB, z - FriezeHalfDepth), new Vector3(fx1, FriezeT, z + FriezeHalfDepth), 0.5f, 1 | 2 | 4 | 16 | 32);
            float u0 = (fx0 - InL) / Span, u1 = (fx1 - InL) / Span;
            // road side (+z) reads left to right for someone facing -z: u runs toward -x
            mb.Quad(2, new Vector3(fx1, FriezeB, z + FriezeHalfDepth), new Vector3(fx1, FriezeT, z + FriezeHalfDepth), new Vector3(fx0, FriezeT, z + FriezeHalfDepth), new Vector3(fx0, FriezeB, z + FriezeHalfDepth),
                new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
            mb.Quad(2, new Vector3(fx0, FriezeB, z - FriezeHalfDepth), new Vector3(fx0, FriezeT, z - FriezeHalfDepth), new Vector3(fx1, FriezeT, z - FriezeHalfDepth), new Vector3(fx1, FriezeB, z - FriezeHalfDepth),
                new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
            // cornice over the frieze, between the piers' cornices (touching, not overlapping)
            mb.BoxAA(1, new Vector3(XL + 1.18f, FriezeT, z - 0.80f), new Vector3(XR - 1.18f, FriezeT + 0.30f, z + 0.80f), 0.5f);
            return mb;
        }

        public Vector3 LanternHook(int side)
        {
            float px = side < 0 ? XL : XR;
            return new Vector3(px, Gy + 6.0f - 0.06f, Z + ShaftW * 0.5f + 0.95f);
        }

        // rain streaks under the pier cornices (3 cm off the stone)
        public MB Streaks()
        {
            var mb = new MB(1);
            foreach (var px in new[] { XL, XR }) StreaksPier(mb, 0, px, Z, ShaftW * 0.5f, FriezeT, 2.6f, px * 0.37f);
            return mb;
        }
    }

    // =====================================================================================================  back shape
    class BackShape
    {
        public readonly float XL, XR, ZL, ZR, Z, Gy, Xc, InL, InR, Span, LintB, LintT, LeafW;
        public BackShape(float xl, float xr, float zl, float zr, float z, float gy)
        {
            XL = xl; XR = xr; ZL = zl; ZR = zr; Z = z; Gy = gy; Xc = (xl + xr) * 0.5f;
            InL = xl + ShaftW * 0.5f; InR = xr - ShaftW * 0.5f; Span = InR - InL;
            LintB = gy + 5.55f; LintT = gy + 6.55f;
            LeafW = Span * 0.5f - 0.05f;
        }

        // upper = lintel, caps and finials (no shadows received, like the front's arch)
        public MB Gateway(bool upper)
        {
            var mb = new MB(4);
            float g = Gy, hs = ShaftW * 0.5f;
            for (int k = 0; k < 2; k++)
            {
                float px = k == 0 ? XL : XR, pz = k == 0 ? ZL : ZR;
                float inSide = k == 0 ? 1f : -1f;
                if (!upper)
                {
                    AsymBox(mb, 1, px, pz, 1.12f, hs, inSide, g - 0.2f, g + 0.9f, 0.5f);
                    mb.BoxAA(0, new Vector3(px - hs, g + 0.9f, pz - hs), new Vector3(px + hs, LintT, pz + hs), 0.55f);
                    continue;
                }
                mb.BoxAA(1, new Vector3(px - 1.12f, LintT, pz - 1.12f), new Vector3(px + 1.12f, LintT + 0.32f, pz + 1.12f), 0.5f);
                if (k == 0)   // the left finial has broken off at the ball
                    mb.Lathe(1, new Vector3(px, LintT + 0.32f, pz), new[] { 0.40f, 0.40f, 0.22f, 0.20f, 0.30f, 0.46f, 0.30f }, new[] { 0f, 0.12f, 0.20f, 0.34f, 0.40f, 0.52f, 0.62f }, 12, 0.8f);
                else
                    mb.Lathe(1, new Vector3(px, LintT + 0.32f, pz), new[] { 0.40f, 0.40f, 0.22f, 0.20f, 0.30f, 0.50f, 0.56f, 0.50f, 0.30f, 0.0f }, new[] { 0f, 0.12f, 0.20f, 0.34f, 0.40f, 0.55f, 0.80f, 1.05f, 1.22f, 1.30f }, 12, 0.8f);
            }
            if (!upper) return mb;
            // the lintel: NEMO INTRET faces the yard (+z), plain stone outside
            float lx0 = InL - 0.05f, lx1 = InR + 0.05f, hd = 0.55f;
            float lt = LintT - 0.01f;
            mb.BoxAA(1, new Vector3(lx0, LintB, Z - hd), new Vector3(lx1, lt, Z + hd), 0.5f, 1 | 2 | 16);
            float u0 = (lx0 - InL) / Span, u1 = (lx1 - InL) / Span;
            mb.Quad(2, new Vector3(lx1, LintB, Z + hd), new Vector3(lx1, lt, Z + hd), new Vector3(lx0, lt, Z + hd), new Vector3(lx0, LintB, Z + hd),
                new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
            return mb;
        }

        public MB Streaks()
        {
            var mb = new MB(1);
            StreaksPier(mb, 0, XL, ZL, ShaftW * 0.5f, LintT, 3.2f, XL * 0.21f);
            StreaksPier(mb, 0, XR, ZR, ShaftW * 0.5f, LintT, 3.2f, XR * 0.21f);
            return mb;
        }

        // wrapped round the two meeting bars at waist height, padlocked
        public MB ChainLocked()
        {
            var mb = new MB(1);
            float cy = Gy + 1.66f; var loop = new List<Vector3>();
            for (int i = 0; i <= 14; i++) { float a = i * Mathf.PI * 2f / 14f; loop.Add(new Vector3(Xc + Mathf.Cos(a) * 0.26f, cy + Mathf.Sin(a * 2f) * 0.03f, Z + Mathf.Sin(a) * 0.13f)); }
            Chain(mb, 0, loop.ToArray(), 0.13f, 0.02f);
            Chain(mb, 0, new[] { new Vector3(Xc + 0.02f, cy - 0.02f, Z + 0.16f), new Vector3(Xc + 0.05f, cy - 0.42f, Z + 0.20f) }, 0.13f, 0.02f);
            Padlock(mb, 0, new Vector3(Xc + 0.05f, cy - 0.62f, Z + 0.21f), Vector3.forward, 0.26f, false);
            return mb;
        }

        // lying on the ground on the yard side, padlock sprung
        public MB ChainDropped()
        {
            var mb = new MB(1);
            var pts = new List<Vector3>();
            Vector3 c = new Vector3(Xc + 0.25f, Gy + 0.035f, Z + 0.55f);
            for (int i = 0; i <= 28; i++) { float t = i / 28f; float a = t * Mathf.PI * 4.2f; float r = 0.08f + 0.26f * t; pts.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.8f)); }
            Chain(mb, 0, pts.ToArray(), 0.13f, 0.02f);
            Padlock(mb, 0, c + new Vector3(0.48f, 0.04f, 0.10f), Vector3.up, 0.26f, true);
            return mb;
        }
    }

    // =====================================================================================================  parts
    // box around a pier centre that stands proud by 'out' on three sides and sits flush ('flush') on the opening side
    static void AsymBox(MB mb, int s, float px, float pz, float out_, float flush, float inSide, float y0, float y1, float rpm)
    {
        float xMin = inSide > 0 ? px - out_ : px - flush, xMax = inSide > 0 ? px + flush : px + out_;
        mb.BoxAA(s, new Vector3(xMin, y0, pz - out_), new Vector3(xMax, y1, pz + out_), rpm);
    }

    static void StreaksPier(MB mb, int s, float px, float pz, float half, float yTop, float h, float u0)
    {
        float yc = yTop - h * 0.5f, o = 0.03f;
        mb.Panel(s, new Vector3(px, yc, pz + half + o), Vector3.forward, Vector3.up, half, h * 0.5f, new Rect(u0, 0f, half * 0.9f, 1f));
        mb.Panel(s, new Vector3(px, yc, pz - half - o), Vector3.back, Vector3.up, half, h * 0.5f, new Rect(u0 + 0.31f, 0f, half * 0.9f, 1f));
        mb.Panel(s, new Vector3(px + half + o, yc, pz), Vector3.right, Vector3.up, half, h * 0.5f, new Rect(u0 + 0.57f, 0f, half * 0.9f, 1f));
        mb.Panel(s, new Vector3(px - half - o, yc, pz), Vector3.left, Vector3.up, half, h * 0.5f, new Rect(u0 + 0.83f, 0f, half * 0.9f, 1f));
    }

    // an iron leaf in local space: hinge at x=0, leaf along +x, ground at y=0. Bars and rings are thick enough not to
    // shimmer at distance; the lock sits low (no plate at a visitor's face height).
    static Mesh IronLeaf(float W, float H, float bow)
    {
        var mb = new MB(1);
        float rpm = 2f;
        System.Func<float, float> top = x => H + bow * Mathf.Sin(Mathf.Clamp01(x / W) * Mathf.PI * 0.5f);
        mb.BoxAA(0, new Vector3(0.0f, 0.06f, -0.05f), new Vector3(0.10f, top(0f) + 0.09f, 0.05f), rpm);
        mb.BoxAA(0, new Vector3(W - 0.10f, 0.06f, -0.05f), new Vector3(W, top(W) + 0.09f, 0.05f), rpm);
        foreach (var ry in new[] { 0.24f, 1.62f, H - 1.08f, H - 0.70f })
            mb.BoxAA(0, new Vector3(0.10f, ry, -0.03f), new Vector3(W - 0.10f, ry + 0.085f, 0.03f), rpm);
        int segs = 10;
        for (int i = 0; i < segs; i++)
        {
            float x0 = W * i / segs, x1 = W * (i + 1) / segs;
            mb.Beam(0, new Vector3(x0, top(x0), 0f), new Vector3(x1, top(x1), 0f), 0.09f, 0.06f, rpm, Vector3.forward);
        }
        int nb = Mathf.Max(2, Mathf.RoundToInt((W - 0.44f) / 0.27f));
        float step = (W - 0.44f) / nb;
        for (int i = 0; i <= nb; i++)
        {
            float x = 0.22f + i * step;
            float y1 = top(x) + 0.30f;
            mb.BoxAA(0, new Vector3(x - 0.035f, 0.06f, -0.035f), new Vector3(x + 0.035f, y1 - 0.06f, 0.035f), rpm, 4);
            mb.BoxAA(0, new Vector3(x - 0.055f, y1 - 0.06f, -0.05f), new Vector3(x + 0.055f, y1, 0.05f), rpm);
            mb.Pyramid(0, new Vector3(x, y1, 0f), Quaternion.identity, 0.07f, 0.04f, 0.22f, rpm);
            if (i < nb)
            {
                float xm = x + step * 0.5f;
                mb.Torus(0, new Vector3(xm, H - 0.87f, 0f), Vector3.forward, Vector3.right, 0.10f, 0.026f, 0f, 10, 4);
                mb.BoxAA(0, new Vector3(xm - 0.024f, 0.06f, -0.024f), new Vector3(xm + 0.024f, 0.64f, 0.024f), rpm);
                mb.Pyramid(0, new Vector3(xm, 0.64f, 0f), Quaternion.identity, 0.045f, 0.026f, 0.12f, rpm);
            }
        }
        mb.BoxAA(0, new Vector3(W - 0.26f, 1.40f, -0.075f), new Vector3(W - 0.02f, 1.88f, 0.075f), rpm);
        return mb.ToMesh(Matrix4x4.identity);
    }

    // lantern body with its bottom at c; returns where the light goes (the middle of the glass)
    static Vector3 LanternBody(MB mb, int sIron, int sGlass, Vector3 hook, float w, float h)
    {
        float rpm = 2f, hw = w * 0.5f, t = 0.05f;
        Vector3 ring = hook + new Vector3(0f, -0.07f, 0f);
        Vector3 c = ring - new Vector3(0f, h * 1.52f + 0.12f, 0f);
        mb.BoxAA(sIron, c + new Vector3(-hw - 0.04f, 0f, -hw - 0.04f), c + new Vector3(hw + 0.04f, 0.07f, hw + 0.04f), rpm);
        mb.BoxAA(sIron, c + new Vector3(-hw - 0.06f, h, -hw - 0.06f), c + new Vector3(hw + 0.06f, h + 0.07f, hw + 0.06f), rpm);
        for (int i = 0; i < 4; i++)
        {
            float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
            mb.BoxAA(sIron, c + new Vector3(sx * hw - t, 0.07f, sz * hw - t), c + new Vector3(sx * hw + t, h, sz * hw + t), rpm);
        }
        mb.Pyramid(sIron, c + new Vector3(0f, h + 0.07f, 0f), Quaternion.identity, hw + 0.10f, hw + 0.10f, h * 0.45f, rpm);
        mb.Torus(sIron, ring, Vector3.right, Vector3.up, 0.07f, 0.02f, 0f, 8, 4);
        float gy = c.y + 0.07f + (h - 0.07f) * 0.5f, gh = (h - 0.07f) * 0.5f - 0.02f;
        mb.Panel(sGlass, new Vector3(c.x, gy, c.z + hw - 0.01f), Vector3.forward, Vector3.up, hw - t, gh, new Rect(0, 0, 1, 1));
        mb.Panel(sGlass, new Vector3(c.x, gy, c.z - hw + 0.01f), Vector3.back, Vector3.up, hw - t, gh, new Rect(0, 0, 1, 1));
        mb.Panel(sGlass, new Vector3(c.x + hw - 0.01f, gy, c.z), Vector3.right, Vector3.up, hw - t, gh, new Rect(0, 0, 1, 1));
        mb.Panel(sGlass, new Vector3(c.x - hw + 0.01f, gy, c.z), Vector3.left, Vector3.up, hw - t, gh, new Rect(0, 0, 1, 1));
        mb.BoxAA(sIron, c + new Vector3(-hw + 0.02f, h * 0.55f, -hw + 0.02f), c + new Vector3(hw - 0.02f, h * 0.55f + 0.03f, hw - 0.02f), rpm);
        return new Vector3(c.x, gy, c.z);
    }

    static void Chain(MB mb, int s, Vector3[] pts, float linkLen, float wire)
    {
        float R = linkLen * 0.30f; int k = 0;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1]; float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / (linkLen * 0.78f)));
            Vector3 dir = (b - a).normalized;
            Vector3 n1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int j = 0; j < n; j++)
            {
                Vector3 c = Vector3.Lerp(a, b, (j + 0.5f) / n);
                Vector3 axis = (k % 2 == 0) ? n1 : Vector3.Cross(dir, n1);
                mb.Torus(s, c, axis, dir, R, wire, linkLen * 0.22f, 8, 4);
                k++;
            }
        }
    }

    static void Padlock(MB mb, int s, Vector3 c, Vector3 facing, float size, bool open)
    {
        Quaternion q = Quaternion.LookRotation(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up);
        mb.Box(s, c, q, new Vector3(size * 0.9f, size, size * 0.38f), 4f);
        Vector3 up = q * Vector3.up, side = q * Vector3.right;
        Vector3 sc = c + up * (size * 0.5f) + (open ? side * size * 0.18f : Vector3.zero);
        int seg = 8; Vector3 prev = Vector3.zero;
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.PI * i / seg;
            Vector3 p = sc + side * (Mathf.Cos(a) * size * 0.30f) + up * (Mathf.Sin(a) * size * 0.42f + size * 0.05f);
            if (i > 0) mb.Cyl(s, prev, p, size * 0.06f, 5, 4f);
            prev = p;
        }
    }

    // =====================================================================================================  mesh builder
    class MB
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> U = new List<Vector2>();
        public readonly List<List<int>> T = new List<List<int>>();
        public MB(int subs) { for (int i = 0; i < subs; i++) T.Add(new List<int>()); }

        // a,b,c,d clockwise seen from the front (normal = Cross(b-a, c-a))
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d); U.Add(ua); U.Add(ub); U.Add(uc); U.Add(ud);
            T[s].Add(i); T[s].Add(i + 1); T[s].Add(i + 2); T[s].Add(i); T[s].Add(i + 2); T[s].Add(i + 3);
        }
        public void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = V.Count; V.Add(a); V.Add(b); V.Add(c); U.Add(ua); U.Add(ub); U.Add(uc);
            T[s].Add(i); T[s].Add(i + 1); T[s].Add(i + 2);
        }
        public void QuadAuto(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, System.Func<Vector3, Vector2> uv, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var t = b; b = d; d = t; }
            Quad(s, a, b, c, d, uv(a), uv(b), uv(c), uv(d));
        }
        // a face with world-projected UVs (metres x rpm), so courses line up across faces and boxes
        public void Face(int s, Vector3 c, Vector3 n, Vector3 up, float hw, float hh, float rpm)
        {
            Vector3 r = Vector3.Cross(n, up).normalized; up = up.normalized;
            Vector3 a = c - r * hw - up * hh, b = c - r * hw + up * hh, cc = c + r * hw + up * hh, d = c + r * hw - up * hh;
            Quad(s, a, b, cc, d, P(a, r, up, rpm), P(b, r, up, rpm), P(cc, r, up, rpm), P(d, r, up, rpm));
        }
        static Vector2 P(Vector3 p, Vector3 r, Vector3 up, float rpm) => new Vector2(Vector3.Dot(p, r) * rpm, Vector3.Dot(p, up) * rpm);
        public void Panel(int s, Vector3 c, Vector3 n, Vector3 up, float hw, float hh, Rect uv)
        {
            Vector3 r = Vector3.Cross(n, up).normalized; up = up.normalized;
            Quad(s, c - r * hw - up * hh, c - r * hw + up * hh, c + r * hw + up * hh, c + r * hw - up * hh,
                new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));
        }
        // skip: 1 +x, 2 -x, 4 +y, 8 -y, 16 +z, 32 -z
        public void Box(int s, Vector3 c, Quaternion q, Vector3 size, float rpm, int skip = 0)
        {
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            if ((skip & 1) == 0) Face(s, c + X * hx, X, Y, hz, hy, rpm);
            if ((skip & 2) == 0) Face(s, c - X * hx, -X, Y, hz, hy, rpm);
            if ((skip & 4) == 0) Face(s, c + Y * hy, Y, Z, hx, hz, rpm);
            if ((skip & 8) == 0) Face(s, c - Y * hy, -Y, -Z, hx, hz, rpm);
            if ((skip & 16) == 0) Face(s, c + Z * hz, Z, Y, hx, hy, rpm);
            if ((skip & 32) == 0) Face(s, c - Z * hz, -Z, Y, hx, hy, rpm);
        }
        public void BoxAA(int s, Vector3 min, Vector3 max, float rpm, int skip = 0) => Box(s, (min + max) * 0.5f, Quaternion.identity, max - min, rpm, skip);
        public void Beam(int s, Vector3 a, Vector3 b, float w, float d, float rpm, Vector3 upHint)
        {
            Vector3 f = b - a; float len = f.magnitude; if (len < 1e-5f) return;
            Vector3 fz = f / len; Vector3 up = Vector3.ProjectOnPlane(upHint, fz);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.right, fz);
            Box(s, (a + b) * 0.5f, Quaternion.LookRotation(fz, up.normalized), new Vector3(w, d, len), rpm);
        }
        public void Pyramid(int s, Vector3 c, Quaternion q, float hx, float hz, float h, float rpm)
        {
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            Vector3 top = c + Y * h;
            Vector3 p0 = c - X * hx - Z * hz, p1 = c - X * hx + Z * hz, p2 = c + X * hx + Z * hz, p3 = c + X * hx - Z * hz;
            Vector3[] ring = { p0, p1, p2, p3 };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                Tri(s, b, top, a, new Vector2(0, 0), new Vector2(0.5f * rpm, h * rpm), new Vector2(rpm, 0));
            }
            Tri(s, p0, p2, p1, Vector2.zero, Vector2.one, Vector2.up); Tri(s, p0, p3, p2, Vector2.zero, Vector2.right, Vector2.one);
        }
        public void Lathe(int s, Vector3 basePos, float[] r, float[] y, int sides, float rpm)
        {
            int start = V.Count; int rows = r.Length; float vacc = 0f;
            for (int j = 0; j < rows; j++)
            {
                if (j > 0) vacc += new Vector2(r[j] - r[j - 1], y[j] - y[j - 1]).magnitude;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    V.Add(basePos + new Vector3(Mathf.Cos(a) * r[j], y[j], Mathf.Sin(a) * r[j]));
                    U.Add(new Vector2(i / (float)sides * Mathf.Max(0.2f, 2f * Mathf.PI * Mathf.Max(r[j], 0.05f)) * rpm, vacc * rpm));
                }
            }
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < sides; i++)
                {
                    int a = start + j * (sides + 1) + i, b = a + 1, c = a + sides + 1, d = c + 1;
                    T[s].Add(a); T[s].Add(c); T[s].Add(d); T[s].Add(a); T[s].Add(d); T[s].Add(b);
                }
        }
        public void Cyl(int s, Vector3 a, Vector3 b, float rad, int sides, float rpm)
        {
            Vector3 f = b - a; float len = f.magnitude; if (len < 1e-5f) return; f /= len;
            Vector3 u = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right).normalized; Vector3 w = Vector3.Cross(f, u);
            int start = V.Count;
            for (int j = 0; j < 2; j++)
                for (int i = 0; i <= sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    V.Add((j == 0 ? a : b) + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * rad);
                    U.Add(new Vector2(i / (float)sides * 2f * Mathf.PI * rad * rpm, j * len * rpm));
                }
            for (int i = 0; i < sides; i++)
            {
                int p = start + i, q = p + 1, r2 = p + sides + 1, t2 = r2 + 1;
                T[s].Add(p); T[s].Add(q); T[s].Add(t2); T[s].Add(p); T[s].Add(t2); T[s].Add(r2);
            }
        }
        public void Torus(int s, Vector3 c, Vector3 axis, Vector3 along, float R, float r, float stretch, int seg, int sides)
        {
            axis = axis.normalized; along = Vector3.ProjectOnPlane(along, axis).normalized; Vector3 side = Vector3.Cross(axis, along);
            int start = V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 dir = along * Mathf.Cos(a) + side * Mathf.Sin(a);
                Vector3 centre = c + along * (Mathf.Cos(a) * (R + stretch)) + side * (Mathf.Sin(a) * R);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    V.Add(centre + (dir * Mathf.Cos(b) + axis * Mathf.Sin(b)) * r);
                    U.Add(new Vector2(i / (float)seg, j / (float)sides));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    int p = start + i * (sides + 1) + j, q = p + 1, r2 = p + sides + 1, t2 = r2 + 1;
                    T[s].Add(p); T[s].Add(t2); T[s].Add(q); T[s].Add(p); T[s].Add(r2); T[s].Add(t2);
                }
        }
        // a block of an arch ring between two radii and two angles, in the plane (xA, yA) round centre c, depth along zA
        public void Voussoir(int s, Vector3 c, Vector3 xA, Vector3 yA, Vector3 zA, float r0, float r1, float a0, float a1, float depth, float rpm)
        {
            Vector3 hz = zA.normalized * depth * 0.5f;
            Vector3 pt(float rr, float aa) => c + xA * (Mathf.Cos(aa) * rr) + yA * (Mathf.Sin(aa) * rr);
            Vector3 i0 = pt(r0, a0), i1 = pt(r0, a1), o0 = pt(r1, a0), o1 = pt(r1, a1);
            Vector2 uvF(Vector3 p) => new Vector2(Vector3.Dot(p - c, xA) * rpm, Vector3.Dot(p - c, yA) * rpm);
            Vector2 uvZ(Vector3 p) => new Vector2(Vector3.Dot(p - c, zA.normalized) * rpm, (Vector3.Dot(p - c, xA) + Vector3.Dot(p - c, yA)) * rpm);
            QuadAuto(s, i0 + hz, o0 + hz, o1 + hz, i1 + hz, uvF, zA);
            QuadAuto(s, i1 - hz, o1 - hz, o0 - hz, i0 - hz, uvF, -zA);
            QuadAuto(s, i0 - hz, i0 + hz, i1 + hz, i1 - hz, uvZ, -((i0 + i1) * 0.5f - c));
            QuadAuto(s, o1 - hz, o1 + hz, o0 + hz, o0 - hz, uvZ, (o0 + o1) * 0.5f - c);
            QuadAuto(s, i0 - hz, o0 - hz, o0 + hz, i0 + hz, uvZ, Vector3.Cross(zA, o0 - i0));
            QuadAuto(s, i1 + hz, o1 + hz, o1 - hz, i1 - hz, uvZ, -Vector3.Cross(zA, o1 - i1));
        }
        // bake into a mesh, vertices transformed by m (world -> the holder's local space)
        public Mesh ToMesh(Matrix4x4 m)
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(V.Select(v => m.MultiplyPoint3x4(v)).ToList());
            mesh.SetUVs(0, U);
            mesh.subMeshCount = T.Count;
            for (int i = 0; i < T.Count; i++) mesh.SetTriangles(T[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
