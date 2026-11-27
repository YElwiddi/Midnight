using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// Footsteps that follow the floor. Attaches itself to every Movement at scene load and takes over from
/// Movement's single footstep set: each step reads what is under the player's feet (the terrain layer outdoors,
/// the object's place in the hierarchy or its material indoors) and plays that floor's sounds from
/// Resources/Footsteps. Steps follow the distance actually walked, at Movement's rhythm (walking speed x walking
/// interval, running speed x running interval), so pushing against a wall stays quiet. A drop of
/// FootstepSettings.minAirTime or more lands with a heavier step. Scripted walks (intro, endings) call PlayStepNow.
/// Without Resources/Footsteps/FootstepSettings nothing attaches and Movement keeps its old sounds.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerFootsteps : MonoBehaviour
{
    public enum StepKind { Walk, Run, Land }

    /// <summary>Raised for every step played (for debugging and tests).</summary>
    public static event Action<PlayerFootsteps, FootSurface, AudioClip, StepKind> Stepped;

    const int VoiceCount = 8;            // echo tails run up to ~2 s; 8 voices cover that even when running
    const float TeleportDistance = 2.5f; // more than this in one frame is a teleport, not a stride
    const float GroundSnap = 0.45f;      // feet within this of the floor count as on the ground
    const float MinMoveSpeed = 0.5f;     // slower than this (m/s) is standing still
    const float RestartAfter = 0.15f;    // standing still this long makes the next move start with a step
    const float SpeedSmoothing = 0.1f;   // seconds; walking vs running is judged on the smoothed speed

    static FootstepSettings settings;
    static bool settingsLoaded;
    static readonly Dictionary<string, AudioClip[]> clipCache = new Dictionary<string, AudioClip[]>();

    Movement movement;
    CharacterController controller;
    AudioSource[] voices;
    int nextVoice;
    readonly Dictionary<int, FootSurface> colliderSurface = new Dictionary<int, FootSurface>();
    readonly HashSet<int> terrainColliders = new HashSet<int>();
    readonly Dictionary<string, int> lastClip = new Dictionary<string, int>();
    readonly RaycastHit[] hits = new RaycastHit[16];

    Vector3 lastPosition;
    bool hasLastPosition;
    float distance;
    bool startWithStep = true;
    float stillTime;
    float smoothSpeed;
    float airTime;
    float airPeakY;
    float quietUntil;
    bool onGround;
    bool hasGroundHit;
    RaycastHit groundHit;

    public FootSurface LastSurface { get; private set; }
    public bool Active { get { return isActiveAndEnabled && settings != null; } }
    public static FootstepSettings Settings { get { LoadSettings(); return settings; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { AttachAll(); }

    static void AttachAll()
    {
        if (!LoadSettings()) return;
        foreach (var m in FindObjectsByType<Movement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (m != null && m.GetComponent<PlayerFootsteps>() == null) m.gameObject.AddComponent<PlayerFootsteps>();
    }

    static bool LoadSettings()
    {
        if (!settingsLoaded)
        {
            settings = Resources.Load<FootstepSettings>(FootstepSettings.ResourcePath);
            settingsLoaded = true;
        }
        return settings != null;
    }

    static AudioClip[] Clips(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return new AudioClip[0];
        AudioClip[] clips;
        if (!clipCache.TryGetValue(folder, out clips))
        {
            clips = Resources.LoadAll<AudioClip>(FootstepSettings.ClipRoot + folder);
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            clipCache[folder] = clips;
        }
        return clips;
    }

    void Awake()
    {
        LoadSettings();
        movement = GetComponent<Movement>();
        controller = GetComponent<CharacterController>();
        var holder = new GameObject("Footsteps");
        holder.transform.SetParent(transform, false);
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var a = holder.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = false;
            a.spatialBlend = 1f;                       // same placement as Movement's own footstep source
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 1f;
            a.maxDistance = 500f;
            a.dopplerLevel = 0f;
            voices[i] = a;
        }
        if (movement != null) movement.surfaceFootsteps = this;
    }

    void OnDestroy()
    {
        if (movement != null && movement.surfaceFootsteps == this) movement.surfaceFootsteps = null;
    }

    void OnEnable() { hasLastPosition = false; startWithStep = true; distance = 0f; airTime = 0f; smoothSpeed = 0f; }

    /// <summary>No automatic steps for a while (input was just locked or unlocked); the next move starts fresh.</summary>
    public void Hush(float seconds)
    {
        quietUntil = Mathf.Max(quietUntil, Time.time + seconds);
        startWithStep = true;
        distance = 0f;
    }

    /// <summary>Plays one walking step on whatever is underfoot now (for scripted walks).</summary>
    public void PlayStepNow()
    {
        if (settings == null) return;
        ProbeGround();
        Play(StepKind.Walk);
    }

    void Update()
    {
        if (settings == null) return;
        float dt = Time.deltaTime;
        Vector3 position = transform.position;
        if (!hasLastPosition) { lastPosition = position; hasLastPosition = true; return; }
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        float flat = Mathf.Sqrt(moved.x * moved.x + moved.z * moved.z);
        if (flat > TeleportDistance || dt <= 0f)
        {
            // a teleport (doors, the endless staircase's loop): skip this frame and keep the rhythm going
            airTime = 0f;
            return;
        }

        ProbeGround();
        bool grounded = onGround || (controller != null && controller.enabled && controller.isGrounded);
        bool playerControlled = movement != null && movement.enabled && movement.canMove;

        if (!grounded)
        {
            if (airTime <= 0f) airPeakY = position.y;
            airTime += dt;
            airPeakY = Mathf.Max(airPeakY, position.y);
            return;
        }
        bool landed = airTime >= settings.minAirTime || (airTime > 0f && airPeakY - position.y >= settings.minDropHeight);
        if (landed && playerControlled && Time.time >= quietUntil)
        {
            Play(StepKind.Land);
            distance = 0f;
            startWithStep = false;
        }
        airTime = 0f;

        if (!playerControlled) { distance = 0f; startWithStep = true; return; }

        float speed = flat / dt;
        smoothSpeed += (speed - smoothSpeed) * (1f - Mathf.Exp(-dt / SpeedSmoothing));
        if (speed < MinMoveSpeed)
        {
            stillTime += dt;
            if (stillTime >= RestartAfter) { startWithStep = true; distance = 0f; smoothSpeed = 0f; }
            return;
        }
        stillTime = 0f;

        // walking or running, from the speed over the last tenth of a second (frame times vary)
        bool running = Mathf.Max(smoothSpeed, startWithStep ? speed : 0f) > (movement.walkingSpeed + movement.runningSpeed) * 0.5f;
        float stride = running
            ? movement.runningSpeed * movement.runningFootstepInterval
            : movement.walkingSpeed * movement.walkingFootstepInterval;
        stride = Mathf.Max(0.5f, stride);
        var kind = running ? StepKind.Run : StepKind.Walk;

        if (startWithStep)
        {
            startWithStep = false;
            distance = 0f;
            if (Time.time >= quietUntil) Play(kind);
            return;
        }
        distance += flat;
        if (distance >= stride)
        {
            distance = Mathf.Min(distance - stride, stride * 0.5f);
            if (Time.time >= quietUntil) Play(kind);
        }
    }

    void ProbeGround()
    {
        onGround = false;
        hasGroundHit = false;
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float half = controller != null ? controller.height * 0.5f * scaleY : 1f;
        Vector3 centre = controller != null ? transform.TransformPoint(controller.center) : transform.position + Vector3.up * half;
        int n = Physics.RaycastNonAlloc(centre, Vector3.down, hits, half + GroundSnap + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.3f) continue;
            if (h.distance < best) { best = h.distance; groundHit = h; hasGroundHit = true; }
        }
        if (hasGroundHit) onGround = best <= half + GroundSnap;
    }

    void Play(StepKind kind)
    {
        FootSurface surface = hasGroundHit ? Classify(groundHit) : LastSurface;
        LastSurface = surface;
        var sound = settings.Find(surface) ?? settings.Find(settings.fallback);
        if (sound == null) return;

        string folder = sound.folder;
        if (!string.IsNullOrEmpty(sound.accentFolder) && Random.value < sound.accentChance) folder = sound.accentFolder;
        var clips = Clips(folder);
        if (clips.Length == 0) { folder = sound.folder; clips = Clips(folder); }
        if (clips.Length == 0) return;

        int last;
        if (!lastClip.TryGetValue(folder, out last)) last = -1;
        int pick = Random.Range(0, clips.Length);
        if (clips.Length > 1 && pick == last) pick = (pick + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        lastClip[folder] = pick;

        float gainDb = Random.Range(-settings.volumeJitterDb, settings.volumeJitterDb);
        float pitch = 1f + Random.Range(-settings.pitchJitter, settings.pitchJitter);
        if (kind == StepKind.Run) { gainDb += settings.runGainDb; pitch += settings.runPitch; }
        else if (kind == StepKind.Land) { gainDb += settings.landGainDb; pitch += settings.landPitch; }

        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        voice.Stop();
        voice.clip = clips[pick];
        voice.pitch = pitch;
        voice.volume = Mathf.Clamp01(sound.volume * settings.masterVolume * Mathf.Pow(10f, gainDb / 20f));
        voice.Play();

        var handler = Stepped;
        if (handler != null) handler(this, surface, clips[pick], kind);
    }

    FootSurface Classify(RaycastHit hit)
    {
        var col = hit.collider;
        int id = col.GetInstanceID();
        FootSurface known;
        if (colliderSurface.TryGetValue(id, out known)) return known;
        if (terrainColliders.Contains(id)) return TerrainSurface((TerrainCollider)col, hit.point);

        var tag = col.GetComponentInParent<FootstepSurfaceTag>();
        if (tag != null) { colliderSurface[id] = tag.surface; return tag.surface; }

        string path = PathOf(col.transform);
        foreach (var rule in settings.pathRules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.match)) continue;
            if (path.StartsWith(rule.match, StringComparison.OrdinalIgnoreCase)) { colliderSurface[id] = rule.surface; return rule.surface; }
        }

        var terrain = col as TerrainCollider;
        if (terrain != null) { terrainColliders.Add(id); return TerrainSurface(terrain, hit.point); }

        var surface = ByName(MaterialNames(col));
        colliderSurface[id] = surface;
        return surface;
    }

    FootSurface TerrainSurface(TerrainCollider col, Vector3 point)
    {
        var terrain = col.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null) return settings.fallback;
        var data = terrain.terrainData;
        var layers = data.terrainLayers;
        if (layers == null || layers.Length == 0) return settings.fallback;
        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp((int)(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp((int)(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
        var weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        float bestWeight = -1f;
        for (int i = 0; i < weights.GetLength(2) && i < layers.Length; i++)
            if (weights[0, 0, i] > bestWeight) { bestWeight = weights[0, 0, i]; best = i; }
        var layer = layers[best];
        if (layer == null) return settings.fallback;
        return ByName((layer.diffuseTexture != null ? layer.diffuseTexture.name : "") + " " + layer.name);
    }

    static string MaterialNames(Collider col)
    {
        var r = col.GetComponent<Renderer>();
        if (r == null) r = col.GetComponentInParent<Renderer>();
        if (r == null) return col.name;
        var m = r.sharedMaterial;
        if (m == null) return col.name;
        string tex = m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "";
        return m.name + " " + tex;
    }

    static FootSurface ByName(string text)
    {
        if (!string.IsNullOrEmpty(text))
            foreach (var rule in settings.nameRules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.match)) continue;
                if (text.IndexOf(rule.match, StringComparison.OrdinalIgnoreCase) >= 0) return rule.surface;
            }
        return settings.fallback;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
