using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Graveyard > Crypt Kills.
///
/// Apply gives the three crypt fiends their screamer kills: it bakes the Beast's skull out of its mesh, creates the
/// three kill assets (or fills in the references of the ones that exist, leaving their tuned numbers alone) and names
/// each on its fiend's KillerJumpscare (Scripted Kill).
/// Remove takes the kills off the fiends again: the jumpscares configured on the prefabs return. The assets stay.
/// </summary>
public static class CryptKillSetup
{
    private const string ArtFolder = "Assets/Art/CryptKills";
    private const string BodyMeshPath = ArtFolder + "/BeastBodyNoSkull.asset";
    private const string SkullMeshPath = ArtFolder + "/BeastSkull.asset";

    private const string FriendPrefab = "Assets/Prefabs/ScaredKiller.prefab";
    private const string WraithPrefab = "Assets/Prefabs/MeanKiller.prefab";
    private const string BeastPrefab = "Assets/Prefabs/StupidKiller.prefab";

    private const string FriendKill = "Assets/Prefabs/FriendFromBehind.asset";
    private const string WraithKill = "Assets/Prefabs/WraithSprint.asset";
    private const string BeastKill = "Assets/Prefabs/BeastLightsOn.asset";

    // A head triangle belongs to the skull when it lies beyond this far along the neck (the head bone's z, in metres
    // of the model), or is bone-white and nearly that far.
    private const float SkullCut = 0.84f;
    private const float SkullWhite = 0.36f;

    [MenuItem("Tools/Graveyard/Crypt Kills/Apply")]
    private static void ApplyMenu() { Debug.Log(Apply()); }

    [MenuItem("Tools/Graveyard/Crypt Kills/Remove")]
    private static void RemoveMenu() { Debug.Log(Remove()); }

    public static string Apply()
    {
        var log = new StringBuilder("Crypt Kills: Apply\n");
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art", "CryptKills");

        Texture slash = Picture(ArtFolder + "/BladeSlash.png", log);
        Texture sockets = Picture(ArtFolder + "/SkullSockets.png", log);
        Texture socketsCracked = Picture(ArtFolder + "/SkullSocketsCracked.png", log);
        BakeBeastSkull(log);

        var friend = Kill<FriendFromBehindSequence>(FriendKill, "Assets/Audio/Jumpscares/FriendFromBehind.wav", log);
        friend.idle = Clip("Assets/Animations/ScaredIdle.fbx", log);
        friend.armsUp = Clip("Assets/Animations/ScaredKill.fbx", log);
        EditorUtility.SetDirty(friend);

        var wraith = Kill<WraithSprintSequence>(WraithKill, "Assets/Audio/Jumpscares/WraithSprint.wav", log);
        wraith.stand = Clip("Assets/Animations/KillerAttack.fbx", log);
        wraith.slashTexture = slash;
        EditorUtility.SetDirty(wraith);

        var beast = Kill<BeastLightsOnSequence>(BeastKill, "Assets/Audio/Jumpscares/BeastLightsOn.wav", log);
        beast.idle = Clip("Assets/Animations/Stupid Idle.fbx", log);
        beast.bodyWithoutSkull = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
        beast.skull = AssetDatabase.LoadAssetAtPath<Mesh>(SkullMeshPath);
        beast.sockets = sockets;
        beast.socketsCracked = socketsCracked;
        EditorUtility.SetDirty(beast);

        AssetDatabase.SaveAssets();
        SetKill(FriendPrefab, friend, log);
        SetKill(WraithPrefab, wraith, log);
        SetKill(BeastPrefab, beast, log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    public static string Remove()
    {
        var log = new StringBuilder("Crypt Kills: Remove\n");
        SetKill(FriendPrefab, null, log);
        SetKill(WraithPrefab, null, log);
        SetKill(BeastPrefab, null, log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ assets

    private static T Kill<T>(string path, string soundPath, StringBuilder log) where T : CryptScreamSequence
    {
        T kill = AssetDatabase.LoadAssetAtPath<T>(path);
        if (kill == null)
        {
            kill = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(kill, path);
            log.AppendLine("  created " + path);
        }
        kill.sound = Sound(soundPath, log);
        kill.screenFxShader = Shader.Find("Hidden/JumpscareScreenFX");
        kill.overlayShader = Shader.Find("Sprites/Default");
        kill.litShader = Shader.Find("Standard");
        kill.additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
        return kill;
    }

    private static AnimationClip Clip(string fbxPath, StringBuilder log)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) log.AppendLine("  MISSING clip " + fbxPath);
        return clip;
    }

    // A kill's sound starts on the catch frame: decompressed and loaded with the scene, not on first play.
    private static AudioClip Sound(string path, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
            log.AppendLine("  MISSING sound " + path);
            return null;
        }
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.DecompressOnLoad || settings.compressionFormat != AudioCompressionFormat.PCM || !settings.preloadAudioData || importer.forceToMono)
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // A picture for the front of the lens: its own pixels, no mip-maps, no compression, clamped.
    private static Texture Picture(string path, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            log.AppendLine("  MISSING picture " + path);
            return null;
        }
        if (importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None || importer.wrapMode != TextureWrapMode.Clamp
            || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture>(path);
    }

    private static void SetKill(string prefabPath, KillSequence kill, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            KillerJumpscare jumpscare = root.GetComponent<KillerJumpscare>();
            if (jumpscare == null)
            {
                log.AppendLine("  NO KillerJumpscare on " + prefabPath);
                return;
            }
            var so = new SerializedObject(jumpscare);
            SerializedProperty property = so.FindProperty("killSequence");
            if (property.objectReferenceValue == kill)
            {
                log.AppendLine("  " + prefabPath + ": already " + (kill != null ? kill.name : "the old jumpscare"));
                return;
            }
            property.objectReferenceValue = kill;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            log.AppendLine("  " + prefabPath + ": " + (kill != null ? kill.name : "the old jumpscare"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ the Beast's skull

    // Its skull is the pale shell at the far end of its neck, skinned to the head bone with the neck itself. This cuts
    // the mesh in two: the body without the skull (the same skin, fewer triangles), and the skull alone as a rigid
    // mesh in the head bone's space, drawn from both sides (part 0 the painted face, part 1 the hollow behind it).
    private static void BakeBeastSkull(StringBuilder log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeastPrefab);
        SkinnedMeshRenderer smr = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
        if (smr == null || animator == null || animator.avatar == null)
        {
            log.AppendLine("  SKULL: no skinned mesh or avatar on " + BeastPrefab);
            return;
        }
        Mesh src = smr.sharedMesh;
        string headName = animator.avatar.humanDescription.human.Where(h => h.humanName == "Head").Select(h => h.boneName).FirstOrDefault();
        int headIndex = System.Array.FindIndex(smr.bones, b => b != null && b.name == headName);
        if (headIndex < 0 || src.subMeshCount != 1)
        {
            log.AppendLine("  SKULL: head bone '" + headName + "' not among the mesh's bones, or the mesh has " + src.subMeshCount + " parts");
            return;
        }

        Vector3[] vertices = src.vertices;
        Vector3[] normals = src.normals;
        Vector2[] uv = src.uv;
        BoneWeight[] weights = src.boneWeights;
        int[] triangles = src.triangles;
        Matrix4x4 toHead = src.bindposes[headIndex];

        // a readable copy of its texture
        Texture texture = smr.sharedMaterial.mainTexture;
        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        System.Func<int, float> headWeight = i =>
            (weights[i].boneIndex0 == headIndex ? weights[i].weight0 : 0f) + (weights[i].boneIndex1 == headIndex ? weights[i].weight1 : 0f) +
            (weights[i].boneIndex2 == headIndex ? weights[i].weight2 : 0f) + (weights[i].boneIndex3 == headIndex ? weights[i].weight3 : 0f);
        System.Func<int, float> brightness = i =>
        {
            Color c = readable.GetPixelBilinear(uv[i].x, uv[i].y);
            return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        };

        var keep = new List<int>();
        var skullTriangles = new List<int>();
        for (int k = 0; k < triangles.Length; k += 3)
        {
            int a = triangles[k], b = triangles[k + 1], c = triangles[k + 2];
            bool bone = false;
            if (headWeight(a) > 0.6f && headWeight(b) > 0.6f && headWeight(c) > 0.6f)
            {
                float near = Mathf.Min(toHead.MultiplyPoint3x4(vertices[a]).z, Mathf.Min(toHead.MultiplyPoint3x4(vertices[b]).z, toHead.MultiplyPoint3x4(vertices[c]).z));
                float white = (brightness(a) + brightness(b) + brightness(c)) / 3f;
                bone = near > SkullCut || (white > SkullWhite && near > SkullCut - 0.25f);
            }
            List<int> into = bone ? skullTriangles : keep;
            into.Add(a); into.Add(b); into.Add(c);
        }
        Object.DestroyImmediate(readable);
        if (skullTriangles.Count == 0)
        {
            log.AppendLine("  SKULL: no skull triangles found");
            return;
        }

        Mesh body = Object.Instantiate(src);
        body.name = "BeastBodyNoSkull";
        body.SetTriangles(keep, 0);

        var map = new Dictionary<int, int>();
        var sv = new List<Vector3>();
        var sn = new List<Vector3>();
        var su = new List<Vector2>();
        var front = new List<int>();
        foreach (int i in skullTriangles)
        {
            if (!map.TryGetValue(i, out int m))
            {
                m = sv.Count;
                map[i] = m;
                sv.Add(toHead.MultiplyPoint3x4(vertices[i]));
                sn.Add(toHead.MultiplyVector(normals[i]).normalized);
                su.Add(uv[i]);
            }
            front.Add(m);
        }
        int n = sv.Count;
        for (int i = 0; i < n; i++)
        {
            sv.Add(sv[i]);
            sn.Add(-sn[i]);
            su.Add(su[i]);
        }
        var back = new List<int>();
        for (int k = 0; k < front.Count; k += 3)
        {
            back.Add(front[k] + n);
            back.Add(front[k + 2] + n);
            back.Add(front[k + 1] + n);
        }
        var skull = new Mesh { name = "BeastSkull" };
        skull.SetVertices(sv);
        skull.SetNormals(sn);
        skull.SetUVs(0, su);
        skull.subMeshCount = 2;
        skull.SetTriangles(front, 0);
        skull.SetTriangles(back, 1);
        skull.RecalculateBounds();

        SaveMesh(body, BodyMeshPath);
        SaveMesh(skull, SkullMeshPath);
        log.AppendLine("  skull baked: " + skullTriangles.Count / 3 + " of " + triangles.Length / 3 + " triangles, " + n + " vertices, middle " + skull.bounds.center.ToString("F3") + ", size " + skull.bounds.size.ToString("F3"));
    }

    // Writes over an existing mesh asset in place, so what refers to it keeps its reference.
    private static void SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return;
        }
        string name = existing.name;
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
    }
}
