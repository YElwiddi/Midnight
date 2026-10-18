#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the main menu's backdrop from the real front gate in Scene 0.5 Dungeon: everything the
/// menu camera can see is copied into the Start Menu Scene (visuals only; scripts, colliders and
/// sounds are stripped), lit like the game's night (oxblood fog, moonlight, ground mist, the gate
/// lamp at its runtime brightness). Adds the rare visitor behind the bars, the camera rig (drift /
/// Play push, PSX + VHS on the 3D only) and the ambience.
/// Replaces the old trees-and-stones diorama. Re-runnable: it rebuilds the "GateBackdrop" root.
/// Run via Tools > Graveyard > Menu > Build Gate Backdrop (then Build Main Menu for the UI).
/// </summary>
public static class MenuBackdropBuilder
{
    public const string DungeonPath = "Assets/Scenes/Scene 0.5 Dungeon.unity";
    public const string MenuPath = "Assets/Scenes/Start Menu Scene.unity";

    // The menu view: the gate a little right of centre, the lamp and the cabin's corner at the right.
    public static readonly Vector3 CamPos = new Vector3(-128.2f, 19.95f, 130.6f);
    public static readonly Vector3 GateCentre = new Vector3(-122.6f, 19.2f, 144.0f);
    public const float CamFov = 47f;
    const float GateScreenU = 0.63f;

    // Play pushes toward the lit cabin wall by the lamp.
    static readonly Vector3 PushPos = new Vector3(-124.2f, 19.75f, 135.2f);
    static readonly Vector3 PushLook = new Vector3(-117.4f, 19.3f, 138.2f);

    const float CopyRadius = 95f;
    const float LampRuntimeIntensity = 5f;     // LanternInteractable.baseIntensity of the VicLamps

    static readonly Vector3 VisitorPos = new Vector3(-120.6f, 17.29f, 144.8f);

    static readonly System.Type[] KeepBehaviours = { typeof(LampHalo) };

    [MenuItem("Tools/Graveyard/Menu/Build Gate Backdrop")]
    public static string Build()
    {
        var log = new System.Text.StringBuilder();
        Scene menu = OpenScene(MenuPath);
        Scene dungeon = OpenScene(DungeonPath);
        SceneManager.SetActiveScene(menu);

        // ---- clear the old diorama / previous build
        foreach (var g in menu.GetRootGameObjects())
            if (g.name == "Background terrain" || g.name == "VicLamp" || g.name == "GateBackdrop")
                Object.DestroyImmediate(g);

        var root = new GameObject("GateBackdrop");
        SceneManager.MoveGameObjectToScene(root, menu);
        var set = new GameObject("Set").transform;
        set.SetParent(root.transform, false);

        // ---- copy what the camera can see
        var probe = new GameObject("__probe").AddComponent<Camera>();
        probe.enabled = false;
        AimCamera(probe.transform, out _);
        probe.fieldOfView = 78f;     // wider than the shot: drift, the push to the cabin, and mist
        probe.aspect = 16f / 9f;
        probe.farClipPlane = 130f;
        var planes = GeometryUtility.CalculateFrustumPlanes(probe);
        Object.DestroyImmediate(probe.gameObject);

        var roots = new HashSet<Transform>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != dungeon || !r.enabled) continue;
            if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
            if ((r.bounds.ClosestPoint(CamPos) - CamPos).magnitude > CopyRadius) continue;
            var t = CopyRoot(r.transform);
            if (t == null || Skip(t)) continue;
            roots.Add(t);
        }
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            if (terrain.gameObject.scene == dungeon && terrain.name == "Terrain") roots.Add(terrain.transform);

        int copied = 0;
        Transform lampLight = null, frontGate2 = null;
        foreach (var src in roots.OrderBy(t => t.name))
        {
            var copy = Object.Instantiate(src.gameObject, src.position, src.rotation);
            copy.name = src.name;
            copy.transform.localScale = src.lossyScale;
            SceneManager.MoveGameObjectToScene(copy, menu);
            copy.transform.SetParent(set, true);
            Strip(copy);
            copied++;
            if (src.name == "FrontGate2") frontGate2 = copy.transform;
            if (src.name == "VicLamp (1)") lampLight = copy.GetComponentsInChildren<Light>(true).Select(l => l.transform).FirstOrDefault();
        }
        log.AppendLine("copied " + copied + " objects");

        // The menu's gate is shut.
        if (frontGate2 != null) frontGate2.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // ---- night: fog, ambient, sky, moon, mist
        var dungeonSky = GetSceneRenderSettings(dungeon);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.11f, 0.037f, 0.032f, 1f);
        RenderSettings.fogDensity = 0.028f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.1255f, 0.1255f, 0.1255f);   // LightingController "Outdoor" at default brightness
        RenderSettings.ambientEquatorColor = new Color(0.08f, 0.08f, 0.10f);
        RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
        if (dungeonSky != null) RenderSettings.skybox = dungeonSky;

        var moonGo = new GameObject("Moonlight");
        moonGo.transform.SetParent(root.transform, false);
        moonGo.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.intensity = 0.22f;
        moon.color = new Color(0.52f, 0.6f, 0.82f);
        moon.shadows = LightShadows.Soft;
        RenderSettings.sun = moon;

        var mist = new GameObject("GroundMist").transform;
        mist.SetParent(root.transform, false);
        var mistRoot = FindInScene(dungeon, "NightAtmosphere/GroundMist");
        int mistCount = 0;
        if (mistRoot != null)
        {
            foreach (Transform tile in mistRoot)
            {
                if ((tile.position - CamPos).magnitude > 70f) continue;
                var srcPrefab = PrefabUtility.GetCorrespondingObjectFromSource(tile.gameObject);
                var copy = srcPrefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(srcPrefab, menu)
                    : Object.Instantiate(tile.gameObject);
                if (copy.scene != menu) SceneManager.MoveGameObjectToScene(copy, menu);
                copy.name = tile.name;
                copy.transform.SetPositionAndRotation(tile.position, tile.rotation);
                copy.transform.localScale = tile.lossyScale;
                copy.transform.SetParent(mist, true);
                mistCount++;
            }
        }
        log.AppendLine("mist tiles " + mistCount);

        // ---- the gate lamp at its in-game brightness; Quit puts it out
        var lampComp = root.AddComponent<MenuLamp>();
        if (lampLight != null)
        {
            var l = lampLight.GetComponent<Light>();
            l.enabled = true;
            l.intensity = LampRuntimeIntensity;
            lampComp.lamp = l;
        }
        var sfx = root.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        lampComp.audioSource = sfx;
        lampComp.offClip = LoadClip("Assets/Music/flashlight-clicking-on-105809.mp3");

        // ---- the visitor who is sometimes behind the bars
        var visitor = BuildVisitor(root.transform, menu, log);
        var visitorComp = root.AddComponent<MenuGateVisitor>();
        visitorComp.visitor = visitor;

        // ---- ambience: wind, the bell, the gate creak (wired to Play by Build Main Menu)
        var wind = new GameObject("Wind").AddComponent<AudioSource>();
        wind.transform.SetParent(root.transform, false);
        wind.clip = LoadClip("Assets/Additional Models and Assets/windstorm-blowing-in-mountain-forest-ivo-vicic-1-01-29.mp3");
        wind.loop = true;
        wind.playOnAwake = true;
        wind.volume = 0.22f;
        wind.spatialBlend = 0f;
        var ambience = root.AddComponent<MenuAmbience>();
        ambience.oneShots = sfx;
        ambience.bellClip = LoadClip("Assets/Additional Models and Assets/freesound_community-old-church-bell-6298.mp3");
        ambience.creakClip = LoadClip("Assets/Additional Models and Assets/open-door-stock-sfx-454246.mp3");

        // ---- camera
        var cam = menu.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
        if (cam != null)
        {
            AimCamera(cam.transform, out _);
            cam.fieldOfView = CamFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 260f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var player = FindInScene(dungeon, "Player/Main Camera");
            if (player != null)
            {
                CopyComponent<VHSRetroFeature>(player.gameObject, cam.gameObject);
                CopyComponent<PSXPostProcessing>(player.gameObject, cam.gameObject);
            }
            var director = cam.GetComponent<MenuCameraDirector>() ?? cam.gameObject.AddComponent<MenuCameraDirector>();
            var push = new GameObject("PlayPush").transform;
            push.SetParent(root.transform, false);
            push.SetPositionAndRotation(PushPos, Quaternion.LookRotation(PushLook - PushPos));
            director.pushTarget = push;

            var menuCtl = Object.FindFirstObjectByType<GameMainMenu>();
            if (menuCtl != null)
            {
                menuCtl.cameraDirector = director;
                menuCtl.lamp = lampComp;
                EditorUtility.SetDirty(menuCtl);
                // Re-running this keeps the gate creak on Play (Build Main Menu sets it the first time).
                if (menuCtl.startButton != null) ambience.creakOnHover = menuCtl.startButton.GetComponent<EpitaphButtonFX>();
            }
        }
        else log.AppendLine("WARNING: no menu camera");

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        log.AppendLine("saved " + MenuPath);
        Debug.Log("MenuBackdropBuilder: " + log);
        return log.ToString();
    }

    // ------------------------------------------------------------------ pieces
    public static void AimCamera(Transform t, out Quaternion rot)
    {
        rot = Quaternion.LookRotation(GateCentre - CamPos);
        float vf = CamFov * 0.5f * Mathf.Deg2Rad;
        float hf = Mathf.Atan(Mathf.Tan(vf) * (16f / 9f));
        float yaw = Mathf.Atan((GateScreenU - 0.5f) * 2f * Mathf.Tan(hf)) * Mathf.Rad2Deg;
        rot = rot * Quaternion.Euler(0f, -yaw, 0f);
        t.SetPositionAndRotation(CamPos, rot);
    }

    static GameObject BuildVisitor(Transform parent, Scene menu, System.Text.StringBuilder log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SpiritKiller.prefab");
        if (prefab == null) { log.AppendLine("WARNING: no SpiritKiller prefab"); return null; }
        var root = new GameObject("Visitor");
        root.transform.SetParent(parent, false);
        var spirit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, menu);
        PrefabUtility.UnpackPrefabInstance(spirit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        spirit.name = "Spirit";
        spirit.transform.SetParent(root.transform, false);
        var face = CamPos - VisitorPos; face.y = 0f;
        spirit.transform.SetPositionAndRotation(VisitorPos, Quaternion.LookRotation(face.normalized));
        Strip(spirit, keepAnimator: true);
        foreach (var a in spirit.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

        // The lamp's light catches her face between the bars.
        var head = spirit.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Head);
        Vector3 headPos = head != null ? head.position : VisitorPos + Vector3.up * 1.7f;
        var beam = new GameObject("FaceLight").AddComponent<Light>();
        beam.transform.SetParent(root.transform, false);
        beam.type = LightType.Spot;
        beam.color = new Color(1f, 0.83f, 0.62f);
        beam.intensity = 2.6f;
        beam.range = 9f;
        beam.spotAngle = 9f;
        beam.shadows = LightShadows.None;
        var target = headPos + Vector3.up * 0.1f;
        var from = target + (CamPos - target).normalized * 3f + Vector3.up * 0.4f;
        beam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target + Vector3.down * 0.25f - from));
        root.SetActive(false);
        return root;
    }

    // ------------------------------------------------------------------ helpers
    static Scene OpenScene(string path)
    {
        var s = SceneManager.GetSceneByPath(path);
        if (s.IsValid() && s.isLoaded) return s;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    static Material GetSceneRenderSettings(Scene scene)
    {
        var active = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        var sky = RenderSettings.skybox;
        SceneManager.SetActiveScene(active);
        return sky;
    }

    // The top-level piece to copy: a direct child of a container (GraveyardMap, MapFBXFile, ...) or a root.
    static Transform CopyRoot(Transform t)
    {
        while (t.parent != null && t.parent.parent != null) t = t.parent;
        return t;
    }

    static bool Skip(Transform t)
    {
        if (t.GetComponentInChildren<Camera>(true) != null) return true;
        if (t.GetComponentInChildren<CharacterController>(true) != null) return true;
        string n = t.name;
        return n.StartsWith("Player") || n.StartsWith("NightAtmosphere") || n.Contains("Trigger") || n.StartsWith("Regions");
    }

    // Visuals only: drop scripts (except the lamp halo), physics, sounds and navigation.
    static void Strip(GameObject go, bool keepAnimator = false)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && System.Array.IndexOf(KeepBehaviours, mb.GetType()) < 0 && !(mb is TMP_Text)) Object.DestroyImmediate(mb);
        foreach (var c in go.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            if (c is Collider || c is Rigidbody || c is AudioSource || c is AudioListener || c is UnityEngine.AI.NavMeshAgent || c is UnityEngine.AI.NavMeshObstacle
                || c is CharacterController || (!keepAnimator && c is Animator))
                Object.DestroyImmediate(c);
        }
    }

    static Transform FindInScene(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name != parts[0]) continue;
            Transform t = r.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t;
        }
        return null;
    }

    static void CopyComponent<T>(GameObject from, GameObject to) where T : Component
    {
        var src = from.GetComponent<T>();
        if (src == null) return;
        var dst = to.GetComponent<T>() ?? to.AddComponent<T>();
        EditorUtility.CopySerialized(src, dst);
    }

    static AudioClip LoadClip(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning("MenuBackdropBuilder: missing clip " + path);
        return c;
    }
}
#endif
