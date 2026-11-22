using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the night's two extra scares. It starts itself in any scene that has a GameFlowManager, so nothing in the scene
/// file needs to change. Settings, clips, sounds and materials come from Resources/Condemned/CondemnedConfig.
///  * Cut Down: the first visitor you turned away drops from the tree by the east grave row, once, in the third watch,
///    when you walk by with your sanity below 60% (CondemnedConfig.cutDownBelowSanity) (CutDownSequence). Never on a night
///    Maria hangs (she was let in): one hanging a night.
///  * The window: reflections in the cabin glass, and, once the desk lamp has been off a while, what comes in behind
///    you (CabinWindows).
/// </summary>
public class CondemnedDirector : MonoBehaviour
{
    public static CondemnedDirector Instance { get; private set; }

    CondemnedConfig cfg;
    CCTVMonitor cctv;

    // Cut Down
    GameEvent turnedAway; EventNPC turnedAwayNPC;
    CutDownSequence cutDown; bool cutDownDone;
    int lastPhase = -1; float phaseStart;
    float nextCutDownCheck;

    CabinWindows windows;

    // ================================================================== starting

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { TryCreate(); }

    static void TryCreate()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<GameFlowManager>() == null) return;
        var cfg = Resources.Load<CondemnedConfig>(CondemnedConfig.ResourcePath);
        if (cfg == null) { Debug.LogWarning("CondemnedDirector: no config at Resources/" + CondemnedConfig.ResourcePath); return; }
        var go = new GameObject("CondemnedDirector");
        var d = go.AddComponent<CondemnedDirector>();
        d.cfg = cfg;
    }

    void Awake()
    {
        Instance = this;
        EventNPC.GateDecided += OnGate;
    }

    void Start()
    {
        cctv = FindFirstObjectByType<CCTVMonitor>();
        if (cfg.reflectionsEnabled || cfg.windowScareEnabled) windows = CabinWindows.Create(cfg);
    }

    void OnDestroy()
    {
        EventNPC.GateDecided -= OnGate;
        if (Instance == this) Instance = null;
        if (cutDown != null) CondemnedRig.Kill(cutDown.gameObject);
        if (windows != null) CondemnedRig.Kill(windows.gameObject);
    }

    // ================================================================== the gate

    void OnGate(EventNPC npc, bool wasLetIn)
    {
        if (npc == null) return;
        var ev = EventFor(npc);
        if (ev == null) return;
        if (cfg.cutDownSkip.Contains(ev))
        {
            // she hangs on her own: let her in and tonight already has its hanging (the night can't go on until she does)
            if (wasLetIn) CancelCutDown(ev.name + " was let in");
            return;
        }
        if (wasLetIn) return;                                                  // Cut Down only wants the ones you turn away
        if (!cfg.cutDownEnabled || cutDownDone || cutDown != null) return;
        // built now (hidden), so the moment itself costs nothing; if this one cannot hang (not a person: the officer's car),
        // the next one you turn away will
        cutDown = CutDownSequence.Prepare(cfg, ev.npcPrefab);
        if (cutDown == null) return;
        turnedAway = ev; turnedAwayNPC = npc;
    }

    /// <summary>No Cut Down for the rest of the night. One that is already playing plays out.</summary>
    void CancelCutDown(string why)
    {
        if (cutDownDone) return;
        cutDownDone = true;
        if (cutDown != null && !cutDown.Running) { CondemnedRig.Kill(cutDown.gameObject); cutDown = null; }
        Debug.Log("CondemnedDirector: no Cut Down tonight (" + why + ").");
    }

    // Maria records it once she has been let in and reached her stop; from then on she always hangs before the night goes on
    static bool MariaLetInTonight()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.MariaLetIn >= 1;
    }

    static GameEvent EventFor(EventNPC npc)
    {
        var g = GameFlowManager.Instance; if (g == null) return null;
        var ev = g.GetCurrentEvent();
        if (ev != null && ev.npcPrefab != null && npc.gameObject.name.StartsWith(ev.npcPrefab.name)) return ev;
        return null;
    }

    // ================================================================== every frame

    void Update()
    {
        if (Time.timeScale == 0f) return;
        var g = GameFlowManager.Instance; if (g == null) return;
        int phase = g.CurrentPhase;
        if (phase != lastPhase) { lastPhase = phase; phaseStart = Time.time; }

        UpdateCutDown(phase);
    }

    // ---------------------------------------------------------------- Cut Down
    void UpdateCutDown(int phase)
    {
        if (cutDownDone || cutDown == null) return;
        if (cutDown.Finished) { cutDownDone = true; cutDown = null; return; }
        if (cutDown.Running) return;
        if (phase != cfg.cutDownPhase || Time.time - phaseStart < 20f || Time.time < nextCutDownCheck) return;
        nextCutDownCheck = Time.time + 0.1f;
        if (turnedAwayNPC != null) return;                                     // the real one has to be gone first
        var sm = SanityManager.Instance;
        if (sm == null || sm.SanityPercent >= cfg.cutDownBelowSanity) return;  // sanity below 60% only (checked as you walk by)
        var cam = CondemnedRig.PlayerCamera(); var player = CondemnedRig.Player();
        if (cam == null || player == null) return;
        Vector3 p = player.transform.position;
        if (p.y < 5f) return;                                                  // outside only
        Vector3 b = cutDown.DropPoint;
        float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(b.x, b.z));
        if (d < cfg.triggerMinDistance || d > cfg.triggerMaxDistance) return;
        Vector3 look = b + Vector3.up * 2.6f;
        if (!CondemnedRig.OnScreen(cam, look, 0.15f, 0.1f) || !CondemnedRig.ClearLine(cam.transform.position, look, 0.8f)) return;
        if (Busy()) return;
        foreach (var npc in FindObjectsByType<EventNPC>(FindObjectsSortMode.None))
            if ((npc.transform.position - b).sqrMagnitude < 16f) return;
        if (MariaLetInTonight()) { CancelCutDown("Maria hangs tonight"); return; }   // backstop for the gate hook
        cutDown.Play(p);
    }

    bool Busy()
    {
        var dm = DialogueManager.GetInstance(); if (dm != null && dm.IsDialoguePlaying()) return true;
        var g = GameFlowManager.Instance; if (g != null && g.IsKillerEventActive) return true;
        if (cctv != null && cctv.IsViewing) return true;
        var ru = ReadableUI.Instance; if (ru != null && ru.IsOpen) return true;
        if (windows != null && windows.ScareRunning) return true;
        if (cutDown != null && cutDown.Running) return true;
        return false;
    }

    // ================================================================== for testing

    public CutDownSequence CutDown { get { return cutDown; } }
    /// <summary>The visitor who will hang in the third watch (the first one turned away), or null.</summary>
    public GameEvent TurnedAway { get { return turnedAway; } }
    public CabinWindows Windows { get { return windows; } }
}
