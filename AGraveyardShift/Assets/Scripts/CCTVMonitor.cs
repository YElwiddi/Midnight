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
    [Tooltip("Scare becomes possible once sanity is at or below this fraction (0.40 = 40%).")]
    [Range(0f, 1f)] public float scareSanityThreshold = 0.40f;
    [Tooltip("Scare becomes possible once protection is at or below this fraction (0.40 = 40%).")]
    [Range(0f, 1f)] public float scareProtectionThreshold = 0.40f;
    [Tooltip("Per-second chance the scare flashes while watching the matching gate with that stat low (0.10 = 10%). One roll per second, not cumulative.")]
    [Range(0f, 1f)] public float scareChancePerSecond = 0.10f;
    [Tooltip("Seconds the distorted scare image stays on screen.")]
    public float scareDuration = 0.5f;
    [Tooltip("Static noise played for the scare's duration (looped, cut at scareDuration).")]
    public AudioClip scareStaticClip;
    [Range(0f, 1f)] public float scareStaticVolume = 0.9f;

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

        if (!isViewing || transitioning) return;
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
        if (scarePlayed || scareActive) return;

        scareRollTimer += Time.unscaledDeltaTime;
        if (scareRollTimer < 1f) return;
        scareRollTimer = 0f; // exactly one roll per second, not cumulative

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

        if (img == null) return; // not on the matching gate for a low stat (or image unassigned)

        if (Random.value > scareChancePerSecond) return; // a single 10% roll this second
        StartScare(img);
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
