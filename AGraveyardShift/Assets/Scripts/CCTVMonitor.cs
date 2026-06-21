using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// A CRT monitor the player can inspect (press E) to watch live feeds from the
/// gate security cameras. While watching, the player's own camera glides to a
/// fixed vantage in front of the TV (so all the existing VHS/PSX post effects
/// are preserved) and Left/Right arrows switch between feeds. Press E again to
/// step back.
///
/// Basic functionality only -- screen static / channel-change glitches / scan
/// lines come later.
/// </summary>
public class CCTVMonitor : MonoBehaviour, IInteractable
{
    [Header("Feeds (in switch order)")]
    [Tooltip("Gate cameras. Each is driven to its own RenderTexture at runtime and only renders while you are watching.")]
    public Camera[] feedCameras;
    [Tooltip("Optional display name per feed, shown on the on-screen label.")]
    public string[] feedNames;
    [Tooltip("Resolution of the feed render textures (4:3, CRT-ish, kept low on purpose).")]
    public int feedWidth = 320;
    public int feedHeight = 240;

    [Header("Screen")]
    [Tooltip("Renderer of the quad overlaying the CRT glass. Its material displays the active feed.")]
    public Renderer screenRenderer;
    [Tooltip("Colour the glass shows when the TV is off (not being watched).")]
    public Color offColor = Color.black;

    [Header("Viewing")]
    [Tooltip("Pose the player camera moves to for the close-up look.")]
    public Transform viewAnchor;
    [Tooltip("Seconds to glide the camera in / out of the viewing pose.")]
    public float transitionTime = 0.5f;
    [Tooltip("Field of view while watching (<= 0 keeps the camera's current FOV).")]
    public float viewFOV = 0f;

    [Header("Controls")]
    public KeyCode exitKey = KeyCode.E;
    [Tooltip("Optional second exit key. Leave as None to avoid clashing with the pause menu.")]
    public KeyCode altExitKey = KeyCode.None;
    public KeyCode prevKey = KeyCode.LeftArrow;
    public KeyCode nextKey = KeyCode.RightArrow;

    [Header("Interaction")]
    public string interactionPrompt = "Watch monitor";

    [Header("On-screen label")]
    [Tooltip("Green CCTV-style label on the screen naming the current feed. Shown only while watching.")]
    public TMP_Text screenLabel;

    [Header("Channel change")]
    [Tooltip("Static-noise sound played briefly when switching feeds.")]
    public AudioClip changeChannelClip;
    [Tooltip("Seconds the change-channel static plays.")]
    public float changeChannelDuration = 0.25f;
    [Range(0f, 1f)] public float changeChannelVolume = 0.8f;

    [Header("First-time hint")]
    [Tooltip("Shown once per playthrough, the first time the player watches the monitor.")]
    [TextArea] public string firstTimeHint = "Use the left and right arrow keys to switch between cameras.";
    public float hintDuration = 4f;

    [Header("Low-sanity / low-protection scare")]
    [Tooltip("Flashed (heavily distorted) over the feed, only while watching the FRONT gate, when SANITY is at/under the threshold -- a spirit on the cameras.")]
    public Texture spiritImage;
    [Tooltip("Flashed (heavily distorted) over the feed, only while watching the BACK gate, when PROTECTION is at/under the threshold -- a grave robber on the cameras.")]
    public Texture graverobberImage;
    [Tooltip("Zoom applied to the SPIRIT image only, centred on her face (1 = no zoom, 1.5 = 1.5x). The grave robber is never zoomed.")]
    [Range(1f, 3f)] public float spiritZoom = 1.5f;
    [Tooltip("UV point on the spirit image to centre the zoom on (her eyes). (0,0) = bottom-left, (1,1) = top-right.")]
    public Vector2 spiritFaceFocus = new Vector2(0.5f, 0.6f);
    [Tooltip("Static intensity for the SPIRIT only (1 = same as the robber, lower = less static).")]
    [Range(0f, 1f)] public float spiritStaticStrength = 0.6f;
    [Tooltip("Overall glitch intensity for the GRAVE ROBBER only (1 = full; lower = less glitchy -- scales jitter, RGB split, tearing and static together).")]
    [Range(0f, 1f)] public float graverobberGlitchAmount = 0.8f;
    [Tooltip("feedCameras index of the FRONT gate cam. The spirit can only appear on this feed.")]
    public int frontGateFeedIndex = 0;
    [Tooltip("feedCameras index of the BACK gate cam. The grave robber can only appear on this feed.")]
    public int backGateFeedIndex = 1;
    [Tooltip("Scare becomes possible once sanity is at or below this fraction (0.60 = 60%).")]
    [Range(0f, 1f)] public float scareSanityThreshold = 0.60f;
    [Tooltip("Scare becomes possible once protection is at or below this fraction (0.60 = 60%).")]
    [Range(0f, 1f)] public float scareProtectionThreshold = 0.60f;
    [Tooltip("Per-second chance the scare flashes while watching the matching gate with that stat low (0.15 = 15%). One roll per second, not cumulative.")]
    [Range(0f, 1f)] public float scareChancePerSecond = 0.15f;
    [Tooltip("Seconds the distorted scare image stays on screen.")]
    public float scareDuration = 0.5f;
    [Tooltip("Static noise played for the scare's duration (looped, cut at scareDuration).")]
    public AudioClip scareStaticClip;
    [Range(0f, 1f)] public float scareStaticVolume = 0.9f;

    [Header("Watcher dart (back gate, phase 2+)")]
    [Tooltip("The figure that sprints across the BACK gate camera's view. A world object near the back gate, toggled active only for the dart. Leave null to disable the dart entirely.")]
    public GameObject watcherDartRunner;
    [Tooltip("World transform the runner starts from (just off one edge of the back-cam view).")]
    public Transform watcherDartStart;
    [Tooltip("World transform the runner ends at (just off the other edge of the back-cam view).")]
    public Transform watcherDartEnd;
    [Tooltip("Seconds the runner takes to cross from start to end.")]
    public float watcherDartDuration = 1.1f;
    [Tooltip("Per-second chance the watcher darts across while watching the BACK gate in an eligible phase (0.10 = 10%). One roll per second, not cumulative. Happens at most once per playthrough.")]
    [Range(0f, 1f)] public float watcherDartChancePerSecond = 0.10f;
    [Tooltip("Earliest game phase the dart can happen. Phase 1 is the first phase, so 2 = 'after the first phase has elapsed'.")]
    public int watcherMinPhase = 2;
    [Tooltip("Animator playback speed for the runner while darting (1 = the clip's authored speed).")]
    public float watcherDartAnimSpeed = 1f;
    [Tooltip("Optional 2D sound played when the watcher darts. Leave null for a silent dart.")]
    public AudioClip watcherDartSound;
    [Range(0f, 1f)] public float watcherDartVolume = 0.7f;

    [Header("Protection restore (while watching)")]
    [Tooltip("Watching the cameras tops up graveyard protection: +protectionRestoreAmount for every this many seconds spent watching.")]
    public float protectionRestoreInterval = 10f;
    [Tooltip("Protection points restored per interval.")]
    public int protectionRestoreAmount = 1;
    [Tooltip("Most protection this mechanic can EVER restore in one playthrough (counts protection actually gained, so watching at full protection doesn't waste it).")]
    public int protectionRestoreLifetimeMax = 10;

    // ---- runtime ----
    private RenderTexture[] feedTextures;
    private Material screenMat;
    private Texture2D offTex;
    private int activeIndex;
    private bool isViewing;
    private bool transitioning;
    private float inputLockUntil;
    private AudioSource audioSource;
    private bool hintShown;
    private CrosshairManager crosshair;

    // scare overlay
    private AudioSource scareAudio;
    private bool scarePlayed;
    private bool scareActive;
    private float scareRollTimer;
    private float scareEndTime;

    // watcher dart overlay
    private Animator watcherDartAnimator;
    private bool watcherDartActive;
    private bool watcherDartPlayed; // one-time, like the image scare
    private float watcherDartStartTime;
    private Vector3 watcherDartFrom;
    private Vector3 watcherDartTo;

    // protection restore
    private float protectionRestoreTimer;
    private int protectionRestoredTotal; // lifetime points restored via the cameras (capped)

    private Movement player;
    private Camera playerCam;
    private Transform camT;
    private Vector3 savedCamPos;
    private Quaternion savedCamRot;
    private float savedFOV;

    void Awake()
    {
        // Give every feed camera its own render texture and keep it dark until watched.
        if (feedCameras != null && feedCameras.Length > 0)
        {
            feedTextures = new RenderTexture[feedCameras.Length];
            for (int i = 0; i < feedCameras.Length; i++)
            {
                var rt = new RenderTexture(Mathf.Max(16, feedWidth), Mathf.Max(16, feedHeight), 16, RenderTextureFormat.Default);
                rt.name = "RT_CCTV_" + i;
                rt.Create();
                feedTextures[i] = rt;

                if (feedCameras[i] != null)
                {
                    feedCameras[i].targetTexture = rt;
                    feedCameras[i].enabled = false;
                }
            }
        }

        if (screenRenderer != null)
            screenMat = screenRenderer.material; // own instance, safe to mutate

        // 1x1 texture for the "off" screen (built-in Unlit/Texture has no colour tint).
        offTex = new Texture2D(1, 1);
        offTex.SetPixel(0, 0, offColor);
        offTex.Apply();

        SetScreenOff();

        // 2D audio source for the channel-change static.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Separate 2D source for the scare static so it never fights the channel blip.
        scareAudio = gameObject.AddComponent<AudioSource>();
        scareAudio.playOnAwake = false;
        scareAudio.spatialBlend = 0f;
        scareAudio.loop = true;

        if (screenLabel != null) screenLabel.gameObject.SetActive(false);

        // The watcher-dart runner stays hidden until it sprints across the back-gate feed.
        if (watcherDartRunner != null)
        {
            watcherDartAnimator = watcherDartRunner.GetComponentInChildren<Animator>(true);
            watcherDartRunner.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (feedTextures != null)
        {
            for (int i = 0; i < feedTextures.Length; i++)
                if (feedTextures[i] != null) feedTextures[i].Release();
        }
        if (offTex != null) Destroy(offTex);
    }

    public void Interact()
    {
        if (isViewing || transitioning) return;
        if (Time.unscaledTime < inputLockUntil) return;
        EnterViewing();
    }

    public string GetInteractionPrompt() { return interactionPrompt; }

    private void EnterViewing()
    {
        if (feedCameras == null || feedCameras.Length == 0 || viewAnchor == null) return;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null) return;

        player = playerGo.GetComponent<Movement>();
        playerCam = (player != null && player.playerCamera != null) ? player.playerCamera : Camera.main;
        if (playerCam == null) return;
        camT = playerCam.transform;

        savedCamPos = camT.position;
        savedCamRot = camT.rotation;
        savedFOV = playerCam.fieldOfView;

        if (player != null) player.DisableAllInput();

        if (crosshair == null) crosshair = FindObjectOfType<CrosshairManager>();
        if (crosshair != null) crosshair.Hide();

        scareRollTimer = 0f; // a beat before the first scare can roll

        EnableFeeds(true);
        activeIndex = Mathf.Clamp(activeIndex, 0, feedCameras.Length - 1);
        if (screenLabel != null) screenLabel.gameObject.SetActive(true);
        ShowFeed(activeIndex);

        StopAllCoroutines();
        StartCoroutine(MoveCamera(viewAnchor.position, viewAnchor.rotation, viewFOV > 0f ? viewFOV : savedFOV, true));

        if (!hintShown)
        {
            hintShown = true;
            SimpleDialogueTrigger.ShowDialogue(firstTimeHint, "", hintDuration, 0f); // 0 = instant, no typewriter
        }
    }

    private void ExitViewing()
    {
        if (!isViewing || transitioning) return;
        StopAllCoroutines();
        StartCoroutine(MoveCamera(savedCamPos, savedCamRot, savedFOV, false));
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot, float targetFov, bool entering)
    {
        transitioning = true;
        isViewing = true; // lock controls during the glide either way

        Vector3 p0 = camT.position;
        Quaternion r0 = camT.rotation;
        float f0 = playerCam.fieldOfView;

        float t = 0f;
        float dur = Mathf.Max(0.01f, transitionTime);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            camT.position = Vector3.Lerp(p0, targetPos, s);
            camT.rotation = Quaternion.Slerp(r0, targetRot, s);
            playerCam.fieldOfView = Mathf.Lerp(f0, targetFov, s);
            yield return null;
        }

        camT.position = targetPos;
        camT.rotation = targetRot;
        playerCam.fieldOfView = targetFov;

        transitioning = false;
        inputLockUntil = Time.unscaledTime + 0.25f;

        if (entering)
        {
            isViewing = true;
        }
        else
        {
            isViewing = false;
            scareActive = false;
            if (scareAudio != null) scareAudio.Stop();
            EndWatcherDart();
            EnableFeeds(false);
            SetScreenOff();
            if (screenLabel != null) screenLabel.gameObject.SetActive(false);
            if (crosshair != null) crosshair.Show();
            if (player != null) player.EnableAllInput();
        }
    }

    void Update()
    {
        if (scareActive && Time.unscaledTime >= scareEndTime)
            EndScare();

        if (watcherDartActive)
            UpdateWatcherDart();

        if (!isViewing || transitioning) return;

        // Monitoring the cameras slowly restores graveyard protection (capped lifetime total).
        TickProtectionRestore();

        if (Time.unscaledTime < inputLockUntil) return;

        if (Input.GetKeyDown(exitKey) || (altExitKey != KeyCode.None && Input.GetKeyDown(altExitKey)))
        {
            ExitViewing();
            return;
        }

        MaybeRollScare();
        if (scareActive) return; // freeze channel switching while the scare is on screen

        if (feedCameras == null || feedCameras.Length <= 1) return;

        if (Input.GetKeyDown(prevKey))
        {
            activeIndex = (activeIndex - 1 + feedCameras.Length) % feedCameras.Length;
            ShowFeed(activeIndex);
            PlayChangeChannel();
        }
        else if (Input.GetKeyDown(nextKey))
        {
            activeIndex = (activeIndex + 1) % feedCameras.Length;
            ShowFeed(activeIndex);
            PlayChangeChannel();
        }
    }

    private void EnableFeeds(bool on)
    {
        if (feedCameras == null) return;
        for (int i = 0; i < feedCameras.Length; i++)
        {
            if (feedCameras[i] == null) continue;
            feedCameras[i].enabled = on;
            // Toggle any light parented under the camera (the per-feed illuminator)
            // so it only exists while that feed is being watched.
            var lights = feedCameras[i].GetComponentsInChildren<Light>(true);
            for (int j = 0; j < lights.Length; j++)
                lights[j].enabled = on;
        }
    }

    private void ShowFeed(int index)
    {
        if (screenMat == null || feedTextures == null || index < 0 || index >= feedTextures.Length) return;
        var tex = feedTextures[index];
        screenMat.mainTexture = tex;
        if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", tex);
        if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", Color.white);
        if (screenMat.HasProperty("_Color")) screenMat.SetColor("_Color", Color.white);
        if (screenMat.HasProperty("_GlitchAmount")) screenMat.SetFloat("_GlitchAmount", 0f);
        screenMat.mainTextureScale = Vector2.one;   // clear any spirit zoom
        screenMat.mainTextureOffset = Vector2.zero;
        if (screenMat.HasProperty("_StaticStrength")) screenMat.SetFloat("_StaticStrength", 1f);
        UpdateLabel(index);
    }

    private void UpdateLabel(int index)
    {
        if (screenLabel == null) return;
        screenLabel.text = (feedNames != null && index >= 0 && index < feedNames.Length && !string.IsNullOrEmpty(feedNames[index]))
            ? feedNames[index]
            : ("CAMERA " + (index + 1));
    }

    // ---- low-sanity / low-protection scare ----

    // Once per watched second, roll for the one-time scare flash. The scare is tied to the
    // camera you're watching: the FRONT gate can show the spirit (when sanity is low), the
    // BACK gate can show the grave robber (when protection is low). Never both -- you only
    // ever see one camera at a time.
    private void MaybeRollScare()
    {
        // While either effect is already on screen, hold the roll timer. This is what guarantees
        // the grave-robber scare and the watcher dart can never play at the same time.
        if (scareActive || watcherDartActive) { scareRollTimer = 0f; return; }

        scareRollTimer += Time.unscaledDeltaTime;
        if (scareRollTimer < 1f) return;
        scareRollTimer = 0f; // exactly one roll per second, not cumulative

        // 1) One-time low-stat image scare: FRONT gate -> spirit (low sanity), BACK gate -> grave robber (low protection).
        if (!scarePlayed)
        {
            Texture img = null;
            if (activeIndex == frontGateFeedIndex)
            {
                var sanity = SanityManager.Instance;
                if (sanity != null && sanity.SanityPercent <= scareSanityThreshold) img = spiritImage;
            }
            else if (activeIndex == backGateFeedIndex)
            {
                var prot = GraveyardProtectionManager.Instance;
                if (prot != null && prot.ProtectionPercent <= scareProtectionThreshold) img = graverobberImage;
            }

            if (img != null && Random.value <= scareChancePerSecond)
            {
                StartScare(img);
                return; // never also start a dart on the same tick as a scare
            }
        }

        // 2) One-time watcher dart across the BACK gate (phase 2+). Mutually exclusive with the scare above.
        if (CanWatcherDart() && Random.value <= watcherDartChancePerSecond)
            StartWatcherDart();
    }

    // ---- watcher dart (a figure sprints across the back-gate camera) ----

    private bool CanWatcherDart()
    {
        if (watcherDartPlayed) return false;                 // can only ever happen once
        if (watcherDartRunner == null || watcherDartStart == null || watcherDartEnd == null) return false;
        if (activeIndex != backGateFeedIndex) return false; // only the back gate cam can see it
        var gfm = GameFlowManager.Instance;
        return gfm != null && gfm.CurrentPhase >= watcherMinPhase;
    }

    private void StartWatcherDart()
    {
        watcherDartActive = true;
        watcherDartPlayed = true; // one-time only
        watcherDartStartTime = Time.unscaledTime;
        watcherDartFrom = watcherDartStart.position;
        watcherDartTo = watcherDartEnd.position;

        watcherDartRunner.transform.position = watcherDartFrom;
        Vector3 dir = watcherDartTo - watcherDartFrom;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            watcherDartRunner.transform.rotation = Quaternion.LookRotation(dir.normalized);
        watcherDartRunner.SetActive(true);

        if (watcherDartAnimator != null)
        {
            watcherDartAnimator.speed = Mathf.Max(0.01f, watcherDartAnimSpeed);
            watcherDartAnimator.Rebind();   // restart the run cycle cleanly each dart
            watcherDartAnimator.Update(0f);
        }

        if (watcherDartSound != null && audioSource != null)
            audioSource.PlayOneShot(watcherDartSound, watcherDartVolume);
    }

    private void UpdateWatcherDart()
    {
        if (watcherDartRunner == null) { watcherDartActive = false; return; }

        float t = (Time.unscaledTime - watcherDartStartTime) / Mathf.Max(0.05f, watcherDartDuration);
        if (t >= 1f) { EndWatcherDart(); return; }

        watcherDartRunner.transform.position = Vector3.Lerp(watcherDartFrom, watcherDartTo, t);
    }

    private void EndWatcherDart()
    {
        watcherDartActive = false;
        if (watcherDartRunner != null) watcherDartRunner.SetActive(false);
    }

    // ---- protection restore (watching the cameras slowly tops up graveyard protection) ----

    private void TickProtectionRestore()
    {
        if (protectionRestoredTotal >= protectionRestoreLifetimeMax) return; // spent the whole lifetime budget
        var prot = GraveyardProtectionManager.Instance;
        if (prot == null) return;

        protectionRestoreTimer += Time.unscaledDeltaTime;
        if (protectionRestoreTimer < protectionRestoreInterval) return;
        protectionRestoreTimer -= protectionRestoreInterval; // keep the remainder so watch time accumulates cleanly

        int budget = protectionRestoreLifetimeMax - protectionRestoredTotal;
        int amount = Mathf.Min(Mathf.Max(1, protectionRestoreAmount), budget);

        int before = prot.CurrentProtection;
        prot.RestoreProtection(amount);
        int gained = prot.CurrentProtection - before; // 0 if already at max — don't spend the budget on nothing
        protectionRestoredTotal += Mathf.Max(0, gained);
    }

    private void StartScare(Texture img)
    {
        scarePlayed = true; // can only ever play once
        scareActive = true;
        scareEndTime = Time.unscaledTime + Mathf.Max(0.05f, scareDuration);

        if (screenMat != null)
        {
            screenMat.mainTexture = img;
            if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", img);
            if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", Color.white);
            if (screenMat.HasProperty("_Color")) screenMat.SetColor("_Color", Color.white);

            // Spirit gets a zoom onto her face + reduced static; the grave robber stays
            // full-frame but at a slightly lower overall glitch intensity.
            if (img == spiritImage)
            {
                float s = 1f / Mathf.Max(0.01f, spiritZoom);
                screenMat.mainTextureScale = new Vector2(s, s);
                screenMat.mainTextureOffset = new Vector2(spiritFaceFocus.x - s * 0.5f, spiritFaceFocus.y - s * 0.5f);
                if (screenMat.HasProperty("_GlitchAmount")) screenMat.SetFloat("_GlitchAmount", 1f);
                if (screenMat.HasProperty("_StaticStrength")) screenMat.SetFloat("_StaticStrength", spiritStaticStrength);
            }
            else
            {
                screenMat.mainTextureScale = Vector2.one;
                screenMat.mainTextureOffset = Vector2.zero;
                if (screenMat.HasProperty("_GlitchAmount")) screenMat.SetFloat("_GlitchAmount", Mathf.Clamp01(graverobberGlitchAmount));
                if (screenMat.HasProperty("_StaticStrength")) screenMat.SetFloat("_StaticStrength", 1f);
            }
        }

        if (screenLabel != null) screenLabel.gameObject.SetActive(false);

        if (scareStaticClip != null && scareAudio != null)
        {
            scareAudio.clip = scareStaticClip;
            scareAudio.volume = scareStaticVolume;
            scareAudio.time = 0f;
            scareAudio.Play();
        }
    }

    private void EndScare()
    {
        scareActive = false;
        if (scareAudio != null) scareAudio.Stop();
        if (screenMat != null && screenMat.HasProperty("_GlitchAmount")) screenMat.SetFloat("_GlitchAmount", 0f);

        if (isViewing && !transitioning)
        {
            if (screenLabel != null) screenLabel.gameObject.SetActive(true);
            ShowFeed(activeIndex); // back to the live feed
        }
        else
        {
            SetScreenOff();
        }
    }

    private void PlayChangeChannel()
    {
        if (changeChannelClip == null || audioSource == null) return;
        CancelInvoke(nameof(StopChannelAudio));
        audioSource.Stop();
        audioSource.clip = changeChannelClip;
        audioSource.volume = changeChannelVolume;
        audioSource.time = 0f;
        audioSource.Play();
        if (changeChannelDuration > 0f && changeChannelDuration < changeChannelClip.length)
            Invoke(nameof(StopChannelAudio), changeChannelDuration);
    }

    private void StopChannelAudio()
    {
        if (audioSource != null) audioSource.Stop();
    }

    private void SetScreenOff()
    {
        if (screenMat == null) return;
        screenMat.mainTexture = offTex;   // built-in Unlit/Texture shows this directly
        if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", offTex);
        if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", Color.white);
        if (screenMat.HasProperty("_Color")) screenMat.SetColor("_Color", Color.white);
        if (screenMat.HasProperty("_GlitchAmount")) screenMat.SetFloat("_GlitchAmount", 0f);
        screenMat.mainTextureScale = Vector2.one;
        screenMat.mainTextureOffset = Vector2.zero;
        if (screenMat.HasProperty("_StaticStrength")) screenMat.SetFloat("_StaticStrength", 1f);
    }
}
