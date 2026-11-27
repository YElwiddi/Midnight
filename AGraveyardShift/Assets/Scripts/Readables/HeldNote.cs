using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reading a note by holding it: the sheet comes off the wall into the player's hands, is read by their own torch,
/// turned over for the next page, and goes back. The held sheet is drawn by its own camera after the picture's tape
/// effects, so the writing stays sharp while the scene's lights still fall on it. Cabin notes are aged paper in ink,
/// crypt notes are torn ledger scraps in pencil, and a note with its own art (the bloody page) keeps it.
/// Driven by <see cref="ReadableUI"/> (OpenHeld / Close), so everything that checks ReadableUI.IsOpen keeps working.
/// </summary>
[DefaultExecutionOrder(-40)] // poses the camera and the torch hand before SimpleFlashlight aims (LateUpdate)
public class HeldNote : MonoBehaviour
{
    public enum Style { Cabin, Crypt, OwnPaper }

    // ------------------------------------------------------------------ which notes, and how

    /// <summary>Whether a readable is a flat note on a wall that can be held, and in which style.</summary>
    public static bool TryStyle(ReadableTrigger readable, Sprite background, out Style style, out Renderer wall, out Texture paper)
    {
        style = Style.Cabin; wall = null; paper = null;
        var settings = HeldNoteSettings.Load();
        if (settings == null || readable == null) return false;
        wall = readable.GetComponent<Renderer>();
        if (wall == null || wall.GetComponent<MeshFilter>() == null) return false;
        var size = wall.bounds.size;
        float thin = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        if (thin > 0.03f || size.y < 0.1f) return false; // not a flat sheet

        if (background != null && background != settings.standardBackground && background.texture != null)
        {
            style = Style.OwnPaper; paper = background.texture; return true;
        }
        for (var t = readable.transform; t != null; t = t.parent)
            if (Array.IndexOf(settings.cryptRoots, t.name) >= 0) { style = Style.Crypt; paper = settings.cryptPaper; return true; }
        style = Style.Cabin; paper = settings.cabinPaper;
        return true;
    }

    /// <summary>Starts holding a note. Pages are final (translated, red pen applied). <paramref name="whileHeld"/> runs every
    /// frame the note is in the hands (the reader's keys); <paramref name="onTakenOver"/> when something else takes the camera.</summary>
    public static HeldNote Begin(Renderer wall, Style style, Texture paper, string[] pages, ReadableUI.TextOverrides overrides, bool lowSanity, Action whileHeld, Action onTakenOver)
    {
        var settings = HeldNoteSettings.Load();
        var movement = FindFirstObjectByType<Movement>();
        var cam = movement != null && movement.playerCamera != null ? movement.playerCamera : Camera.main;
        if (settings == null || cam == null) return null;
        var go = new GameObject("Held note");
        var held = go.AddComponent<HeldNote>();
        held.Init(settings, cam, wall, style, paper, pages, overrides, lowSanity, whileHeld, onTakenOver);
        return held;
    }

    // ------------------------------------------------------------------ state

    private HeldNoteSettings settings;
    private Camera cam;
    private Renderer wall;
    private Style style;
    private Texture paper;
    private string[] pages;
    private ReadableUI.TextOverrides overrides;
    private bool lowSanity;
    private Action whileHeld, onTakenOver;

    private GameObject sheet, front, back;
    private Material frontMat, backMat;
    private RenderTexture frontRT, backRT;
    private Vector2 sheetSize; private float holdDist; private Vector2 pageRef; private int rtW, rtH;
    private Camera pageCam; private Canvas pageCanvas; private RawImage pagePaper; private TextMeshProUGUI pageText;
    private Camera sharpCam; private int restMask;
    private SimpleFlashlight torch; private Vector3 restHand; private float restAimMin;
    private Quaternion restLocalRot, liftLocalRot, lastRot;
    private Vector3 wallPos; private Quaternion wallRot; private Vector3 wallScale;
    private AudioSource sfx;
    private Canvas vignetteCanvas; private RawImage vignetteImage;

    private enum Phase { Lift, Hold, Back, Done }
    private Phase phase = Phase.Lift;
    private float phaseTime, life;
    private float head, backHead = 1f;                      // how far the head has come up (0..1); where it was when put down
    private float flip, flipFrom, flipTo, flipTime = -1f;   // 0 = front facing the reader, 1 = back
    private int shownPage;
    private Action onPutBack;
    private bool shaking;
    private float shakeIntensity, shakeSpeed;

    private void Init(HeldNoteSettings s, Camera c, Renderer w, Style st, Texture p, string[] pg, ReadableUI.TextOverrides ov, bool low, Action keys, Action takenOver)
    {
        settings = s; cam = c; wall = w; style = st; paper = p; pages = pg; overrides = ov; lowSanity = low; whileHeld = keys; onTakenOver = takenOver;
        bool crypt = style == Style.Crypt;
        sheetSize = crypt ? settings.cryptSheet : settings.cabinSheet;
        holdDist = crypt ? settings.cryptHold : settings.cabinHold;
        pageRef = crypt ? new Vector2(300f, 381f) : new Vector2(375f, 540f);
        rtW = 1024; rtH = Mathf.RoundToInt(1024f * pageRef.y / pageRef.x);
        shaking = lowSanity && ov.shakeIntensity.HasValue && ov.shakeIntensity.Value > 0f;
        shakeIntensity = ov.shakeIntensity ?? 0f; shakeSpeed = ov.shakeSpeed ?? 25f;

        // where the note hangs
        var b = wall.bounds;
        wallPos = b.center;
        Vector3 n = WallNormal();
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
        if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, n).normalized;
        wallRot = Quaternion.LookRotation(-n, up);
        var right = Vector3.Cross(up, -n);
        float wallW = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z))));
        float wallH = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))));
        wallScale = new Vector3(Mathf.Clamp(wallW / sheetSize.x, 0.4f, 2.5f), Mathf.Clamp(wallH / sheetSize.y, 0.4f, 2.5f), 1f);

        BuildPageRenderer();
        frontRT = NewRT(); backRT = NewRT();
        RenderPage(0, frontRT);
        if (pages.Length > 1) RenderPage(1, backRT); else RenderPage(-1, backRT);
        BuildSheet();
        BuildSharpCamera();
        BuildVignette();

        var movement = FindFirstObjectByType<Movement>();
        torch = movement != null ? movement.GetComponentInChildren<SimpleFlashlight>(true) : null;
        if (torch == null) torch = FindFirstObjectByType<SimpleFlashlight>();
        if (torch != null) { restHand = torch.handOffset; restAimMin = torch.aimMinDistance; }

        restLocalRot = cam.transform.localRotation;
        float pitch = restLocalRot.eulerAngles.x; if (pitch > 180f) pitch -= 360f;
        liftLocalRot = Quaternion.Euler(Mathf.Min(pitch, settings.maxLookDown), restLocalRot.eulerAngles.y, restLocalRot.eulerAngles.z);
        lastRot = cam.transform.rotation;

        sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f;
        Play(settings.pickUpSound);
        wall.enabled = false;
        LateUpdate();
    }

    // Which way the note faces: its thinnest side, towards the reader (the note meshes are not readable at runtime).
    private Vector3 WallNormal()
    {
        Vector3 toCam = cam.transform.position - wall.bounds.center;
        var s = wall.bounds.size;
        Vector3 axis = s.x <= s.y && s.x <= s.z ? Vector3.right : (s.z <= s.y ? Vector3.forward : Vector3.up);
        return Vector3.Dot(axis, toCam) >= 0f ? axis : -axis;
    }

    // ------------------------------------------------------------------ the page itself (a small canvas rendered into textures)

    private RenderTexture NewRT()
    {
        var rt = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32) { name = "Held note page", antiAliasing = 1, useMipMap = true, autoGenerateMips = true, anisoLevel = 8 };
        rt.Create();
        return rt;
    }

    private void BuildPageRenderer()
    {
        var camGo = new GameObject("Held note page camera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = new Vector3(0f, -5000f, 0f);
        pageCam = camGo.AddComponent<Camera>();
        pageCam.enabled = false; pageCam.clearFlags = CameraClearFlags.SolidColor; pageCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        pageCam.cullingMask = 1 << 5; pageCam.nearClipPlane = 0.05f; pageCam.farClipPlane = 10f; pageCam.allowHDR = false; pageCam.allowMSAA = false;

        var canvasGo = new GameObject("Held note page", typeof(RectTransform));
        canvasGo.layer = 5;
        canvasGo.transform.SetParent(transform, false);
        pageCanvas = canvasGo.AddComponent<Canvas>();
        pageCanvas.renderMode = RenderMode.ScreenSpaceCamera; pageCanvas.worldCamera = pageCam; pageCanvas.planeDistance = 1f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = pageRef; scaler.matchWidthOrHeight = 0.5f;

        var paperGo = new GameObject("Paper", typeof(RectTransform)); paperGo.layer = 5;
        paperGo.transform.SetParent(canvasGo.transform, false);
        pagePaper = paperGo.AddComponent<RawImage>(); pagePaper.texture = paper; pagePaper.raycastTarget = false;
        var pr = pagePaper.rectTransform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

        var textGo = new GameObject("Text", typeof(RectTransform)); textGo.layer = 5;
        textGo.transform.SetParent(paperGo.transform, false);
        pageText = textGo.AddComponent<TextMeshProUGUI>();
        pageText.raycastTarget = false; pageText.richText = true; pageText.textWrappingMode = TextWrappingModes.Normal; pageText.overflowMode = TextOverflowModes.Overflow;
        Vector4 m = style == Style.Crypt ? new Vector4(40f, 50f, 22f, 30f) : new Vector4(22f, 26f, 20f, 22f);   // left, top, right, bottom
        var tr = pageText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(m.x, m.w); tr.offsetMax = new Vector2(-m.z, -m.y);

        // writing: the note's own font and size (scaled up for the page), ink on cabin paper, pencil on the scraps;
        // a low-sanity page keeps its own font, colour and shake
        bool crypt = style == Style.Crypt;
        TMP_FontAsset font = (!lowSanity && crypt && settings.cryptFont != null) ? settings.cryptFont : overrides.font;
        if (font == null) font = settings.cryptFont;
        float size = (overrides.fontSize ?? 18f) * (crypt ? settings.cryptTextScale : settings.textScale);
        Loc.ApplyFont(pageText, font, size);
        pageText.enableAutoSizing = true; pageText.fontSizeMax = pageText.fontSize; pageText.fontSizeMin = pageText.fontSize * 0.7f;
        Color ink = crypt ? settings.pencilColor : settings.inkColor;
        if (lowSanity && overrides.fontColor.HasValue) ink = overrides.fontColor.Value;
        else if (!crypt && overrides.fontColor.HasValue && overrides.fontColor.Value.grayscale < 0.4f) ink = overrides.fontColor.Value;
        pageText.color = ink;
        pageText.alignment = overrides.alignment ?? TextAlignmentOptions.Center;
        if (overrides.lineSpacing.HasValue) pageText.lineSpacing = overrides.lineSpacing.Value;
        canvasGo.SetActive(false);
    }

    private void RenderPage(int index, RenderTexture target)
    {
        pageCam.targetTexture = target;          // before the canvas wakes, so it sizes itself to the page
        pageCanvas.gameObject.SetActive(true);
        pageText.text = index >= 0 && index < pages.Length ? pages[index] : string.Empty;
        Canvas.ForceUpdateCanvases();
        pageText.ForceMeshUpdate(true, true);
        if (shaking && index == shownPage) Shake();
        pageCam.Render();
        pageCam.targetTexture = null;
        pageCanvas.gameObject.SetActive(false);
    }

    // The low-sanity shake, as the reading overlay does it, redrawn into the page each frame.
    private void Shake()
    {
        var info = pageText.textInfo;
        float time = Time.time * Mathf.Max(1f, shakeSpeed);
        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
            var off = new Vector3(Mathf.PerlinNoise(time + i * 0.3f, 0f) * 2f - 1f, Mathf.PerlinNoise(0f, time + i * 0.3f) * 2f - 1f, 0f) * shakeIntensity;
            for (int v = 0; v < 4; v++) verts[ch.vertexIndex + v] += off;
        }
        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
            pageText.UpdateGeometry(info.meshInfo[i].mesh, i);
        }
    }

    // ------------------------------------------------------------------ the sheet, its camera, the vignette

    private void BuildSheet()
    {
        sheet = new GameObject("Sheet");
        sheet.transform.SetParent(transform, false);
        sheet.layer = settings.pageLayer;
        Color glow = style == Style.Crypt ? settings.cryptGlow : settings.cabinGlow;
        float bow = style == Style.Crypt ? 0.008f : 0.012f, corner = style == Style.Crypt ? 0.03f : 0.025f;
        front = Face("Front", SheetMesh(sheetSize.x, sheetSize.y, bow, corner, false), frontRT, glow, out frontMat);
        back = Face("Back", SheetMesh(sheetSize.x, sheetSize.y, bow, corner, true), backRT, glow, out backMat);
    }

    private GameObject Face(string name, Mesh mesh, Texture tex, Color glow, out Material mat)
    {
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        g.layer = settings.pageLayer;
        g.transform.SetParent(sheet.transform, false);
        g.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.GetComponent<MeshRenderer>();
        mat = settings.sheetMaterial != null ? new Material(settings.sheetMaterial) : new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;
        if (mat.HasProperty("_EmissionMap")) { mat.SetTexture("_EmissionMap", tex); mat.SetColor("_EmissionColor", glow); mat.EnableKeyword("_EMISSION"); }
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    // A sheet (w x h metres) facing -Z with a gentle bow and a lifted corner; the back has mirrored UVs and reversed faces.
    private static Mesh SheetMesh(float w, float h, float bow, float corner, bool backFace)
    {
        const int nx = 18, ny = 26;
        var m = new Mesh { name = "Held note sheet" };
        var v = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[v.Length];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = (float)i / nx, vv = (float)j / ny, xn = (u - 0.5f) * 2f, yn = (vv - 0.5f) * 2f;
                float z = -bow * xn * xn - corner * Mathf.Pow(Mathf.Max(0f, xn), 2f) * Mathf.Pow(Mathf.Max(0f, -yn), 2f) - 0.003f * Mathf.Sin(yn * 2.7f + 0.6f);
                v[j * (nx + 1) + i] = new Vector3((u - 0.5f) * w, (vv - 0.5f) * h, z);
                uv[j * (nx + 1) + i] = new Vector2(backFace ? 1f - u : u, vv);
            }
        var tri = new int[nx * ny * 6]; int k = 0;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                if (!backFace) { tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = b; tri[k++] = c; tri[k++] = d; }
                else { tri[k++] = a; tri[k++] = b; tri[k++] = c; tri[k++] = b; tri[k++] = d; tri[k++] = c; }
            }
        m.vertices = v; m.uv = uv; m.triangles = tri;
        m.RecalculateNormals(); m.RecalculateTangents();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return m;
    }

    // Draws only the held sheet, after the main camera's tape and PSX effects, so the writing stays sharp.
    private void BuildSharpCamera()
    {
        var g = new GameObject("Held note camera");
        g.transform.SetParent(cam.transform, false);
        sharpCam = g.AddComponent<Camera>();
        sharpCam.CopyFrom(cam);
        sharpCam.clearFlags = CameraClearFlags.Depth;
        sharpCam.cullingMask = 1 << settings.pageLayer;
        sharpCam.depth = cam.depth + 1f;
        sharpCam.targetTexture = cam.targetTexture;
        sharpCam.allowHDR = false; sharpCam.allowMSAA = false;
        sharpCam.nearClipPlane = 0.05f;
        g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
        restMask = cam.cullingMask;
        cam.cullingMask = restMask & ~(1 << settings.pageLayer);
    }

    private void BuildVignette()
    {
        if (settings.vignette <= 0f) return;
        var g = new GameObject("Held note vignette", typeof(RectTransform));
        g.transform.SetParent(transform, false);
        vignetteCanvas = g.AddComponent<Canvas>();
        vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        vignetteCanvas.sortingOrder = -100;   // under every other piece of UI
        var ig = new GameObject("Vignette", typeof(RectTransform));
        ig.transform.SetParent(g.transform, false);
        vignetteImage = ig.AddComponent<RawImage>();
        vignetteImage.texture = VignetteTexture(); vignetteImage.raycastTarget = false; vignetteImage.color = new Color(1f, 1f, 1f, 0f);
        var r = vignetteImage.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private static Texture2D vignetteTex;
    private static Texture2D VignetteTexture()
    {
        if (vignetteTex != null) return vignetteTex;
        const int w = 256, h = 144;
        vignetteTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Held note vignette", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w - 0.5f) * 2f * (16f / 9f) * 0.62f, v = ((y + 0.5f) / h - 0.5f) * 2f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(0.18f + 0.75f * Mathf.Pow(Mathf.Clamp01((r - 0.25f) / 0.95f), 1.4f));
                px[y * w + x] = new Color32(0, 0, 0, (byte)(255 * Mathf.Min(a, 0.95f)));
            }
        vignetteTex.SetPixels32(px);
        vignetteTex.Apply();
        return vignetteTex;
    }

    // ------------------------------------------------------------------ driving it

    /// <summary>Turns the sheet so that page <paramref name="index"/> faces the reader.</summary>
    public void ShowPage(int index)
    {
        if (phase != Phase.Hold && phase != Phase.Lift) return;
        if (flipTime >= 0f) { flip = flipTo; flipTime = -1f; }      // a second click finishes the turn at once
        // the side that will come round gets the new page
        bool toBack = flip < 0.5f;
        RenderPage(index, toBack ? backRT : frontRT);
        shownPage = index;
        flipFrom = flip; flipTo = toBack ? 1f : 0f; flipTime = 0f;
        Play(settings.turnSound);
    }

    /// <summary>Puts the note back on the wall, then calls <paramref name="done"/>.</summary>
    public void PutBack(Action done)
    {
        if (phase == Phase.Back || phase == Phase.Done) return;
        onPutBack = done;
        // put down while still coming up: it goes back from where it got to (the put-back curve mirrors the lift's)
        float up = phase == Phase.Lift ? Mathf.Clamp01(phaseTime / settings.liftSeconds) : 1f;
        backHead = head;
        phase = Phase.Back; phaseTime = (1f - up) * settings.putBackSeconds;
        Play(settings.putBackSound);
    }

    /// <summary>Gone at once: the note is back on the wall, the torch in the hand. The camera is left to whoever took it.</summary>
    public void Abort()
    {
        if (phase == Phase.Done) return;
        phase = Phase.Done;
        Cleanup();
    }

    // The reader's keys (put it back, turn it over), every frame the note is in the hands.
    private void Update()
    {
        if (phase == Phase.Lift || phase == Phase.Hold) whileHeld?.Invoke();
    }

    private void LateUpdate()
    {
        if (phase == Phase.Done || cam == null) return;
        var ct = cam.transform;
        // a kill or a cinematic took the camera: let it have it
        if (life > 0f && Quaternion.Angle(ct.rotation, lastRot) > 2f)
        {
            phase = Phase.Done;
            Cleanup();
            onTakenOver?.Invoke();
            return;
        }
        float dt = Time.deltaTime;
        life += dt; phaseTime += dt;

        float lift; // 0 on the wall .. 1 in the hands
        if (phase == Phase.Lift) { lift = Smooth(phaseTime / settings.liftSeconds); if (phaseTime >= settings.liftSeconds) { phase = Phase.Hold; phaseTime = 0f; } }
        else if (phase == Phase.Hold) lift = 1f;
        else lift = 1f - Smooth(phaseTime / settings.putBackSeconds);

        // the head comes up a little while the page is held, and goes back with it
        // (put down early, it starts down from where it got to)
        head = phase == Phase.Lift ? Smooth(phaseTime / (settings.liftSeconds + 0.1f)) : phase == Phase.Hold ? 1f
             : Mathf.Min(backHead, Smooth(1f - phaseTime / settings.putBackSeconds));
        ct.localRotation = Quaternion.Slerp(restLocalRot, liftLocalRot, head);
        lastRot = ct.rotation;

        // turning the page
        if (flipTime >= 0f)
        {
            flipTime += dt;
            float k = Smooth(flipTime / settings.turnSeconds);
            flip = Mathf.Lerp(flipFrom, flipTo, k);
            if (flipTime >= settings.turnSeconds) { flip = flipTo; flipTime = -1f; }
        }

        // held in front of the eyes, swaying a little
        float sway = Mathf.Clamp01(life / 1.0f);
        var hp = ct.position + ct.rotation * new Vector3(0f, -0.005f, holdDist) + ct.up * (0.003f * sway * Mathf.Sin(life * 1.7f));
        var hr = ct.rotation * Quaternion.Euler(7f + 0.5f * sway * Mathf.Sin(life * 1.9f), 0.7f * sway * Mathf.Sin(life * 1.4f + 1f), 1.2f + 0.4f * sway * Mathf.Sin(life * 2.6f + 2f));
        float turning = flipTime >= 0f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(flipTime / settings.turnSeconds)) : 0f;
        hr = hr * Quaternion.Euler(0f, 180f * flip, 0f);
        hp += ct.forward * (-0.05f * turning);

        // off the wall: peel away, dip, come up into the hands (and the same way back)
        var wallN = wallRot * Vector3.back;
        var peelPos = wallPos + wallN * 0.06f;
        var faceUp = flip > 0.5f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;   // back on the wall the way it is held
        var peelRot = wallRot * faceUp * Quaternion.Euler(-12f, 0f, 0f);
        var ctrl = Vector3.Lerp(peelPos, hp, 0.5f) + Vector3.down * 0.22f;
        float e = Smooth(Mathf.InverseLerp(0.08f, 1f, lift));
        float peel = Smooth(Mathf.Clamp01(lift / 0.2f));
        var start = Vector3.Lerp(wallPos, peelPos, peel);
        var pos = Vector3.Lerp(Vector3.Lerp(start, ctrl, e), Vector3.Lerp(ctrl, hp, e), e);
        var rot = Quaternion.Slerp(Quaternion.Slerp(wallRot * faceUp, peelRot, peel), hr, e);
        sheet.transform.SetPositionAndRotation(pos, rot);
        sheet.transform.localScale = Vector3.Lerp(wallScale, Vector3.one, Smooth(Mathf.Clamp01(lift * 1.4f)));

        if (vignetteImage != null) vignetteImage.color = new Color(1f, 1f, 1f, settings.vignette * lift);
        if (torch != null)
        {
            torch.handOffset = Vector3.Lerp(restHand, settings.readingHand, lift);
            torch.aimMinDistance = Mathf.Lerp(restAimMin, settings.readingAimMin, lift);
        }
        if (sharpCam != null) { sharpCam.fieldOfView = cam.fieldOfView; sharpCam.nearClipPlane = 0.05f; }
        if (shaking && phase != Phase.Back) RenderPage(shownPage, flip > 0.5f ? backRT : frontRT);

        if (phase == Phase.Back && phaseTime >= settings.putBackSeconds)
        {
            ct.localRotation = restLocalRot;
            phase = Phase.Done;
            Cleanup();
            var done = onPutBack; onPutBack = null;
            done?.Invoke();
        }
    }

    private static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

    private void Play(AudioClip clip) { if (clip != null && sfx != null) sfx.PlayOneShot(clip, settings.soundVolume); }

    private void Cleanup()
    {
        if (torch != null) { torch.handOffset = restHand; torch.aimMinDistance = restAimMin; }
        if (wall != null) wall.enabled = true;
        if (cam != null && sharpCam != null) cam.cullingMask = restMask;
        if (sharpCam != null) Destroy(sharpCam.gameObject);
        if (frontRT != null) frontRT.Release();
        if (backRT != null) backRT.Release();
        Destroy(frontRT); Destroy(backRT);
        if (frontMat != null) Destroy(frontMat);
        if (backMat != null) Destroy(backMat);
        if (front != null) Destroy(front.GetComponent<MeshFilter>().sharedMesh);
        if (back != null) Destroy(back.GetComponent<MeshFilter>().sharedMesh);
        // the sounds of putting it back finish on their own
        if (sheet != null) Destroy(sheet);
        if (vignetteCanvas != null) Destroy(vignetteCanvas.gameObject);
        Destroy(gameObject, 2f);
    }
}
