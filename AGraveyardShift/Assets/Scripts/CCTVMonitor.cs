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
            EnableFeeds(false);
            SetScreenOff();
            if (screenLabel != null) screenLabel.gameObject.SetActive(false);
            if (crosshair != null) crosshair.Show();
            if (player != null) player.EnableAllInput();
        }
    }

    void Update()
    {
        if (!isViewing || transitioning) return;
        if (Time.unscaledTime < inputLockUntil) return;

        if (Input.GetKeyDown(exitKey) || (altExitKey != KeyCode.None && Input.GetKeyDown(altExitKey)))
        {
            ExitViewing();
            return;
        }

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
        UpdateLabel(index);
    }

    private void UpdateLabel(int index)
    {
        if (screenLabel == null) return;
        screenLabel.text = (feedNames != null && index >= 0 && index < feedNames.Length && !string.IsNullOrEmpty(feedNames[index]))
            ? feedNames[index]
            : ("CAMERA " + (index + 1));
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
    }
}
