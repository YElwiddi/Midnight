#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Collects every player-facing source string in the game — the "template" a translator works
/// from (the equivalent of gettext's .pot). Sources:
///   1. Inspector strings on components in the build scenes, prefabs and ScriptableObjects
///      (whitelisted per component type in <see cref="Fields"/>).
///   2. Text lines and choices in the compiled Ink dialogue JSON referenced by GameEvent assets.
///   3. Code-defined strings registered in <see cref="CodeStrings"/> (explicit keys).
/// The result is written to Assets/Localization/source_strings.json with a context line per
/// entry, and is what the missing-translation report and the CSV export are built on.
/// </summary>
public static class LocalizationExtractor
{
    public const string SourceStringsPath = "Assets/Localization/source_strings.json";

    /// <summary>One player-facing string and where it comes from.</summary>
    [Serializable]
    public class SourceEntry
    {
        public string key;      // explicit key (code strings) or the normalized source text
        public string text;     // English text
        public string context;  // "Scene/Object (Component.field)" — first occurrence
        public int count;       // how many places use it
        public bool isKey;      // explicit key (true) vs. source-text key (false)
    }

    [Serializable]
    public class SourceStringsFile
    {
        public string generated;
        public List<SourceEntry> entries = new List<SourceEntry>();
    }

    /// <summary>
    /// Player-facing string fields per component type. Extend this when a new script gains
    /// text the player can read. Nested serializable classes are expressed with dotted paths
    /// and "[]" for arrays (e.g. "pages[]", "waypoints[].waitingDialogueText").
    /// </summary>
    public static readonly Dictionary<string, string[]> Fields = new Dictionary<string, string[]>
    {
        { "BackGateController", new[] { "keyPrompt", "gateLockedText", "gateKeyLostText", "gateStuckText", "gateOpenPrompt", "yesText", "noText", "speakerName" } },
        { "CCTVMonitor", new[] { "feedNames[]", "firstTimeHint" } },
        { "CabinExitDialogueCue", new[] { "dialogueText", "speakerName" } },
        { "ChestInteractable", new[] { "lockedText", "unlockPrompt", "yesText", "noText", "speakerName" } },
        { "CinematicEndingData", new[] { "endingTitle", "endingDescription" } },
        { "ConditionalKillerEvent", new[] { "jumpscareDialogueText" } },
        { "CryptKillerConfig", new[] { "flashlightDisabledDialogue", "flashlightDisabledSpeaker" } },
        { "DialogueTeleportInteractable", new[] { "lockedDialogueText", "lockedSpeakerName", "confirmationMessage", "yesOptionText", "noOptionText" } },
        { "DigPenaltyConfig", new[] { "shovelBrokenDialogue", "tryDigWithBrokenShovelDialogue" } },
        { "DirtPileInteractable", new[] { "noShovelMessage", "confirmationMessage", "correctDigMessage", "incorrectDigMessage" } },
        { "EndingsMenuDisplay", new[] { "lockedText" } },
        { "CryptFiendsMenuDisplay", new[] { "lockedText" } },
        { "EventNPC", new[] { "idleNudgeText" } },
        { "GameEvent", new[] { "dialogueText", "speakerName", "waitingDialogueText", "waitingDialogueSpeaker",
                               "waypoints[].waitingDialogueText", "waypoints[].waitingDialogueSpeaker",
                               "exitDialogues[].dialogueText", "exitDialogues[].speakerName",
                               "backgroundNPCs[].waypoints[].waitingDialogueText", "backgroundNPCs[].waypoints[].waitingDialogueSpeaker" } },
        { "GraveRoseInteractable", new[] { "placePrompt", "yesText", "noText", "speakerName", "wiltText", "arrivalLine" } },
        { "GraveyardEndingManager", new[] { "endingDialogue" } },
        { "IntroCinematic", new[] { "dialogueText", "speakerName" } },
        { "IntroTextUI", new[] { "pages[].text", "continueText" } },
        { "LockedTeleportInteractable", new[] { "lockedDialogueText", "lockedSpeakerName" } },
        { "MaintainedLampsLitDialogueCue", new[] { "dialogueText", "speakerName" } },
        { "ReadableTrigger", new[] { "pages[]", "rightColumnPages[]", "lowSanityOverrides.pages[]" } },
        { "RoseInteractable", new[] { "prompt", "yesText", "noText", "speakerName" } },
        { "SanityEffectsController", new[] { "lowSanityDialogue", "lowSanityDialogueSpeaker" } },
        { "SecretKeyInteractable", new[] { "keyPrompt", "yesText", "noText", "speakerName" } },
        { "ShovelPickup", new[] { "pickupDialogue", "dialogueSpeaker" } },
        { "SimpleDialogueTrigger", new[] { "dialogueText", "speakerName" } },
        { "StaircaseDialogueController", new[] { "dialogueEntries[].text", "finalText" } },
        { "TeleportInteractable", new[] { "killerLockedDialogue" } },
    };

    /// <summary>Scene labels whose authored text is the key (LocalizedText in SourceText mode) are picked up automatically.</summary>
    private static readonly string[] BuildScenes = { "Assets/Scenes/Start Menu Scene.unity", "Assets/Scenes/Scene 0.5 Dungeon.unity" };

    /// <summary>Strings defined in code (explicit keys → English). Keep in sync with Loc.Get(...) call sites.</summary>
    public static IEnumerable<KeyValuePair<string, string>> CodeStrings()
    {
        // Endings / crypt fiends
        foreach (Ending e in EndingInfo.InOrder)
        {
            var d = EndingInfo.GetEnglish(e);
            string id = EndingInfo.Id(e);
            yield return new KeyValuePair<string, string>("ending." + id + ".title", d.title);
            yield return new KeyValuePair<string, string>("ending." + id + ".type", d.typeLabel);
            yield return new KeyValuePair<string, string>("ending." + id + ".description", d.description);
        }
        foreach (CryptFiend f in CryptFiendInfo.InOrder)
        {
            var d = CryptFiendInfo.GetEnglish(f);
            string id = CryptFiendInfo.Id(f);
            yield return new KeyValuePair<string, string>("fiend." + id + ".title", d.title);
            yield return new KeyValuePair<string, string>("fiend." + id + ".flavor", d.flavor);
            yield return new KeyValuePair<string, string>("fiend." + id + ".ability", d.ability);
            yield return new KeyValuePair<string, string>("fiend." + id + ".spawn", d.spawnCondition);
        }
        yield return new KeyValuePair<string, string>("fiend.label.ability", "Unique ability:");
        yield return new KeyValuePair<string, string>("fiend.label.spawn", "Spawn condition:");

        // Main menu / options
        yield return new KeyValuePair<string, string>("ui.menu.play", "PLAY");
        yield return new KeyValuePair<string, string>("ui.menu.endings", "ENDINGS");
        yield return new KeyValuePair<string, string>("ui.menu.settings", "SETTINGS");
        yield return new KeyValuePair<string, string>("ui.menu.quit", "QUIT");
        yield return new KeyValuePair<string, string>("ui.menu.cancel", "CANCEL");
        yield return new KeyValuePair<string, string>("ui.menu.accept", "ACCEPT");
        yield return new KeyValuePair<string, string>("ui.menu.back", "BACK");
        yield return new KeyValuePair<string, string>("ui.settings.title", "SETTINGS");
        yield return new KeyValuePair<string, string>("ui.settings.language", "Language");
        yield return new KeyValuePair<string, string>("ui.settings.resolution", "Resolution");
        yield return new KeyValuePair<string, string>("ui.settings.fullscreen", "Fullscreen");
        yield return new KeyValuePair<string, string>("ui.settings.brightness", "Brightness");
        yield return new KeyValuePair<string, string>("ui.settings.volume", "Volume");
        yield return new KeyValuePair<string, string>("ui.settings.brightnessNote", "Default brightness is strongly recommended.");
        yield return new KeyValuePair<string, string>("ui.endings.discovered", "{0} / {1}  DISCOVERED");
        yield return new KeyValuePair<string, string>("ui.endings.fiendsTeaserLocked", "{0} / {1}  ?????");
        yield return new KeyValuePair<string, string>("ui.endings.fiendsTeaser", "{0} / {1}  CRYPT FIENDS FOUND");
        yield return new KeyValuePair<string, string>("ui.fiends.header", "CRYPT FIENDS");
        yield return new KeyValuePair<string, string>("ui.fiends.headerLocked", "?????");
        yield return new KeyValuePair<string, string>("ui.fiends.found", "{0} / {1}  FOUND");
        yield return new KeyValuePair<string, string>("ui.disclaimer.headphones", "It is recommended to use headphones");
        yield return new KeyValuePair<string, string>("ui.pause.resume", "Resume");
        yield return new KeyValuePair<string, string>("ui.pause.exit", "Exit");
        yield return new KeyValuePair<string, string>("ui.readable.page", "Page {0}/{1}");
        yield return new KeyValuePair<string, string>("ui.cctv.camera", "CAMERA {0}");
    }

    [MenuItem("Tools/Graveyard/Localization/1. Extract Source Strings")]
    public static void ExtractMenu()
    {
        var file = Extract(true);
        EditorUtility.DisplayDialog("Localization", "Extracted " + file.entries.Count + " source strings to\n" + SourceStringsPath, "OK");
    }

    /// <summary>Runs the full extraction. Opens the build scenes (asks to save the current one first when <paramref name="interactive"/>).</summary>
    public static SourceStringsFile Extract(bool interactive)
    {
        var map = new Dictionary<string, SourceEntry>(StringComparer.Ordinal);

        // 1. Code strings (explicit keys).
        foreach (var kv in CodeStrings())
            Add(map, kv.Key, kv.Value, "code", true);

        // 2. Scenes.
        string previous = SceneManager.GetActiveScene().path;
        if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return new SourceStringsFile();
        foreach (string scenePath in BuildScenes)
        {
            if (!File.Exists(scenePath)) continue;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                ScanHierarchy(root, Path.GetFileNameWithoutExtension(scenePath), map);
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        // 3. Prefabs + ScriptableObjects.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Additional Models and Assets/") || path.Contains("/TextMesh Pro/") || path.Contains("/Ink/")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) ScanHierarchy(prefab, Path.GetFileName(path), map);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/TextMesh Pro/") || path.Contains("/Ink/")) continue;
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so != null) ScanObject(so, Path.GetFileName(path), map);
        }

        // 4. Ink dialogue referenced by GameEvent-style assets (any TextAsset ending in .json under Assets/Scripts/Dialogue, Assets/InkDialogue, Assets).
        var inkAssets = new HashSet<TextAsset>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Prefabs", "Assets/KillerConfigs" }))
        {
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (so == null) continue;
            var sp = new SerializedObject(so).GetIterator();
            while (sp.Next(true))
            {
                if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                var ta = sp.objectReferenceValue as TextAsset;
                if (ta != null && AssetDatabase.GetAssetPath(ta).EndsWith(".json", StringComparison.OrdinalIgnoreCase)) inkAssets.Add(ta);
            }
        }
        foreach (var ta in inkAssets)
        {
            string name = ta.name + ".json";
            foreach (var line in InkTextExtractor.Extract(ta.text))
                Add(map, line.text, line.text, name + " (" + (line.isChoice ? "choice" : "line") + (string.IsNullOrEmpty(line.speaker) ? "" : ", " + line.speaker) + ")", false);
        }

        var file = new SourceStringsFile { generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        file.entries = map.Values.OrderBy(e => e.isKey ? 0 : 1).ThenBy(e => e.isKey ? e.key : e.context, StringComparer.Ordinal).ThenBy(e => e.key, StringComparer.Ordinal).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(SourceStringsPath));
        File.WriteAllText(SourceStringsPath, JsonUtility.ToJson(file, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(SourceStringsPath);
        Debug.Log("LocalizationExtractor: " + file.entries.Count + " source strings → " + SourceStringsPath);
        return file;
    }

    public static SourceStringsFile LoadSourceStrings()
    {
        if (!File.Exists(SourceStringsPath)) return null;
        return JsonUtility.FromJson<SourceStringsFile>(File.ReadAllText(SourceStringsPath));
    }

    // ------------------------------------------------------------------ scanning

    private static void ScanHierarchy(GameObject root, string origin, Dictionary<string, SourceEntry> map)
    {
        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            string where = origin + "/" + comp.gameObject.name;

            // Labels that localize by their authored text.
            var lt = comp as LocalizedText;
            if (lt != null && lt.mode == LocalizedText.Mode.SourceText)
            {
                var tmp = lt.GetComponent<TMP_Text>();
                if (tmp != null) Add(map, tmp.text, tmp.text, where + " (LocalizedText)", false);
                continue;
            }
            if (comp is MonoBehaviour) ScanObject(comp, where, map);
        }
    }

    private static void ScanObject(UnityEngine.Object obj, string where, Dictionary<string, SourceEntry> map)
    {
        string type = obj.GetType().Name;
        string[] fields;
        if (!Fields.TryGetValue(type, out fields)) return;

        var so = new SerializedObject(obj);
        foreach (string path in fields)
            CollectPath(so, path, where + " (" + type + "." + path + ")", map);
    }

    /// <summary>Resolves a dotted path with "[]" array markers against a SerializedObject and adds every string found.</summary>
    private static void CollectPath(SerializedObject so, string path, string context, Dictionary<string, SourceEntry> map)
    {
        var results = new List<SerializedProperty>();
        Resolve(so.GetIterator(), so, path.Split('.'), 0, results);
        foreach (var p in results)
        {
            if (p.propertyType != SerializedPropertyType.String) continue;
            Add(map, p.stringValue, p.stringValue, context, false);
        }
    }

    private static void Resolve(SerializedProperty parent, SerializedObject so, string[] parts, int index, List<SerializedProperty> results)
    {
        if (index >= parts.Length) { results.Add(parent.Copy()); return; }

        string part = parts[index];
        bool isArray = part.EndsWith("[]");
        string name = isArray ? part.Substring(0, part.Length - 2) : part;

        SerializedProperty prop = index == 0 ? so.FindProperty(name) : parent.FindPropertyRelative(name);
        if (prop == null) return;

        if (isArray)
        {
            if (!prop.isArray) return;
            for (int i = 0; i < prop.arraySize; i++)
                Resolve(prop.GetArrayElementAtIndex(i), so, parts, index + 1, results);
        }
        else
        {
            Resolve(prop, so, parts, index + 1, results);
        }
    }

    private static void Add(Dictionary<string, SourceEntry> map, string key, string text, string context, bool isKey)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!isKey)
        {
            key = Loc.NormalizeKey(key);
            if (IsPlaceholder(key)) return;
        }
        SourceEntry e;
        if (map.TryGetValue(key, out e)) { e.count++; return; }
        map[key] = new SourceEntry { key = key, text = isKey ? text : key, context = context, count = 1, isKey = isKey };
    }

    /// <summary>Designer notes and editor placeholders that are never shown to the player.</summary>
    private static bool IsPlaceholder(string s)
    {
        // Purely numeric "text" (e.g. a number typed into a speaker field) is not translatable.
        double n;
        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out n)) return true;
        switch (s)
        {
            case "New Text":
            case "Button":
            case "Test":
            case "NPC":
            case "NPCCenterPoint":
            case "Enter your dialogue here...":
                return true;
        }
        return false;
    }
}

/// <summary>Pulls player-facing text (lines, choices) out of compiled Ink JSON.</summary>
public static class InkTextExtractor
{
    public struct Line { public string text; public bool isChoice; public string speaker; }

    public static List<Line> Extract(string json)
    {
        var result = new List<Line>();
        object root;
        try { root = MiniJson.Deserialize(json); } catch { return result; }
        var dict = root as Dictionary<string, object>;
        if (dict == null || !dict.ContainsKey("root")) return result;

        var state = new State();
        Walk(dict["root"], state, result);
        state.Flush(result);
        return result;
    }

    private class State
    {
        public StringBuilder line = new StringBuilder();
        public bool inTag, inStr;
        public StringBuilder str = new StringBuilder();
        public string speaker;   // last "#speaker:" tag seen; Ink authors put one at the top of each knot

        public void Flush(List<Line> result)
        {
            string t = line.ToString().Trim();
            if (t.Length > 0) result.Add(new Line { text = t, isChoice = false, speaker = speaker });
            line.Length = 0;
        }
    }

    private static void Walk(object node, State s, List<Line> result)
    {
        var list = node as List<object>;
        if (list != null)
        {
            foreach (var item in list)
            {
                var str = item as string;
                if (str != null) { Visit(str, s, result); continue; }
                var d = item as Dictionary<string, object>;
                if (d != null)
                {
                    if (d.ContainsKey("*")) // choice point; text was collected in the ev/str block just before
                    {
                        string ct = s.str.ToString().Trim();
                        if (ct.Length > 0) result.Add(new Line { text = ct, isChoice = true, speaker = s.speaker });
                        s.str.Length = 0;
                        continue;
                    }
                    // named sub-containers live in the trailing dictionary
                    foreach (var kv in d)
                    {
                        if (kv.Key == "#f" || kv.Key == "#n") continue;
                        if (kv.Value is List<object>) Walk(kv.Value, s, result);
                    }
                    continue;
                }
                if (item is List<object>) Walk(item, s, result);
            }
        }
    }

    private static void Visit(string v, State s, List<Line> result)
    {
        if (v == "#") { s.inTag = true; return; }
        if (v == "/#") { s.inTag = false; return; }
        if (s.inTag)
        {
            if (v.StartsWith("^speaker:")) s.speaker = v.Substring(9).Trim();
            return;
        }
        if (v == "str") { s.inStr = true; s.str.Length = 0; return; }
        if (v == "/str") { s.inStr = false; return; }
        if (s.inStr) { if (v.StartsWith("^")) s.str.Append(v.Substring(1)); return; }
        if (v.StartsWith("^")) { s.line.Append(v.Substring(1)); return; }
        if (v == "\n") { s.Flush(result); return; }
    }
}
#endif
