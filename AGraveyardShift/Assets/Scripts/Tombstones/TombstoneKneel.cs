using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Headstones carry their inscriptions, carved into the stone, and reading one means kneeling in front of it:
/// no overlay, the torch does the rest. Added at runtime to every <see cref="ReadableTrigger"/> whose mesh has a
/// face in <see cref="TombstoneSettings"/> (no scene edits); <see cref="ReadableTrigger.Interact"/> hands over to
/// <see cref="Read"/>. The lettering is the readable's first page, in the current language. At low sanity the
/// stone's low-sanity page (red, shaking) takes the lettering over while you kneel, as the overlay did.
/// </summary>
[DefaultExecutionOrder(-50)] // the camera is posed in LateUpdate before the torch (SimpleFlashlight) aims
public class TombstoneKneel : MonoBehaviour
{
    // ------------------------------------------------------------------ setup (self-booting)

    private static readonly List<TombstoneKneel> all = new List<TombstoneKneel>();
    private static TombstoneKneel active;
    private static float rereadAfter;
    private static bool localeHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { AttachAll(); }

    private static void AttachAll()
    {
        var settings = TombstoneSettings.Load();
        if (settings == null || settings.faces == null || settings.faces.Count == 0) return;

        bool any = false;
        foreach (var readable in FindObjectsByType<ReadableTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mf = readable.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var faces = settings.faces.Where(f => f != null && f.mesh == mf.sharedMesh.name).ToList();
            if (faces.Count == 0) continue;

            var kneel = readable.GetComponent<TombstoneKneel>();
            if (kneel == null) kneel = readable.gameObject.AddComponent<TombstoneKneel>();
            kneel.settings = settings;
            kneel.readable = readable;
            kneel.faces = faces;
            any = true;
        }
        if (!any) return;

        if (!localeHooked) { Loc.LocaleChanged += RebuildAll; localeHooked = true; }
        RebuildAll();
    }

    /// <summary>Re-cuts every inscription (language change, or new stones).</summary>
    public static void RebuildAll()
    {
        all.RemoveAll(k => k == null);
        foreach (var group in all.GroupBy(k => k.MeshName))
        {
            // Words every stone of a kind opens with ("Here lies") are cut small above the name.
            string prefix = CommonOpening(group.Select(k => k.FirstLine()).ToList());
            foreach (var k in group) { k.sharedOpening = prefix; k.Build(); }
        }
    }

    private static string CommonOpening(List<string> lines)
    {
        var words = lines.Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(' ')).Where(w => w.Length >= 3).ToList();
        if (words.Count < 2) return null;
        int n = 0;
        while (words.All(w => n < w.Length - 1 && w[n] == words[0][n])) n++;
        return n == 0 ? null : string.Join(" ", words[0].Take(n));
    }

    // ------------------------------------------------------------------ per stone

    private TombstoneSettings settings;
    private ReadableTrigger readable;
    private List<TombstoneSettings.Face> faces;
    private string sharedOpening;
    private readonly List<Cut> cuts = new List<Cut>();   // one per lettered face

    private class Cut
    {
        public TombstoneSettings.Face face;
        public GameObject root;
        public readonly List<TextMeshPro> lines = new List<TextMeshPro>();
        public readonly List<Renderer> marks = new List<Renderer>();
        public Renderer rule;
        public TextMeshPro low;          // the stone's low-sanity words, cut on the same face
        public float lowAlpha = -1f, letteringAlpha = 1f;
    }

    private string MeshName { get { var mf = GetComponent<MeshFilter>(); return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : ""; } }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        if (settings != null && cuts.Count == 0) Build();   // a stone that was switched off while the others were cut
        foreach (var c in cuts) if (c.root != null) c.root.SetActive(true);
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (active == this) Abandon();
        foreach (var c in cuts) if (c.root != null) c.root.SetActive(false);   // the lettering goes with the stone
    }

    private void OnDestroy() { foreach (var c in cuts) if (c.root != null) Kill(c.root); }

    private string FirstLine()
    {
        var lines = Lines(PageText());
        return lines.Count > 0 ? lines[0] : "";
    }

    private string PageText()
    {
        var pages = readable != null ? readable.Pages : null;
        return pages != null && pages.Length > 0 ? Loc.Tr(pages[0]) : "";
    }

    private static List<string> Lines(string text)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        return text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }

    // Faces are stored in the mesh's own space; the stones are imported at different (sometimes uneven) scales.
    private void FaceFrame(TombstoneSettings.Face f, out Vector3 center, out Vector3 normal, out Vector3 up, out Vector2 size)
    {
        Vector3 ln = f.normal.normalized, lu = f.up.normalized, lr = Vector3.Cross(lu, ln);
        center = transform.TransformPoint(f.center);
        normal = transform.TransformDirection(ln).normalized;
        up = Vector3.ProjectOnPlane(transform.TransformDirection(lu), normal).normalized;
        size = new Vector2(transform.TransformVector(lr * f.size.x).magnitude, transform.TransformVector(lu * f.size.y).magnitude);
    }

    // The lettering lives outside the stone, so the stone's import scale never stretches it.
    private static Transform Holder(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == "Inscriptions") return go.transform;
        var holder = new GameObject("Inscriptions");
        SceneManager.MoveGameObjectToScene(holder, scene);
        return holder.transform;
    }

    // A face against a wall (the crypt) is not lettered.
    private bool FaceIsOpen(TombstoneSettings.Face f)
    {
        FaceFrame(f, out var c, out var n, out _, out _);
        var hits = Physics.RaycastAll(c + n * 0.02f, n, 0.55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.collider != null && !h.collider.transform.IsChildOf(transform)) return false;
        return true;
    }

    private AudioSource sfx;

    private void Build()
    {
        if (sfx == null)
        {
            // made ahead of use, so the audio router has put it on the effects bus before it first plays
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
        }
        foreach (var c in cuts) if (c.root != null) Kill(c.root);
        cuts.Clear();
        hasLowWords = false;
        var lines = Lines(PageText());
        if (lines.Count == 0) return; // a blank stone stays blank

        foreach (var f in faces)
        {
            if (!FaceIsOpen(f)) continue;
            var cut = new Cut { face = f };
            cut.root = new GameObject("Inscription (" + name + ")");
            cut.root.transform.SetParent(Holder(gameObject.scene), false);
            FaceFrame(f, out var center, out var normal, out var up, out var size);
            cut.root.transform.SetPositionAndRotation(center + normal * 0.003f, Quaternion.LookRotation(-normal, up));
            cut.root.SetActive(isActiveAndEnabled);
            Letter(cut, lines, size);
            CutLowWords(cut, size);
            cuts.Add(cut);
        }
    }

    // ------------------------------------------------------------------ lettering

    private void Letter(Cut cut, List<string> lines, Vector2 size)
    {
        float W = size.x, maxW = W * settings.maxLineWidth;
        float H = size.y * Mathf.Max(0.1f, cut.face.textScale);   // caps and gaps scale together
        var items = new List<(string text, TMP_FontAsset font, float cap, float gap, float spacing, bool rule)>();

        string name = lines[0];
        if (!string.IsNullOrEmpty(sharedOpening) && name.StartsWith(sharedOpening + " "))
        {
            items.Add((sharedOpening, settings.textFont, settings.prefixCap * H, 0.032f * H, 2f, false));
            name = name.Substring(sharedOpening.Length + 1);
        }
        items.Add((name, settings.nameFont, settings.nameCap * H, 0.039f * H, 1f, false));
        if (lines.Count > 1) items.Add((null, null, 0.00625f * H, 0.039f * H, 0f, true));
        for (int i = 1; i < lines.Count; i++)
        {
            bool dates = IsDates(lines[i]);
            items.Add((dates ? DatesText(lines[i]) : lines[i], dates ? settings.nameFont : settings.textFont,
                       (dates ? settings.datesCap : settings.textCap) * H, 0.045f * H, dates ? 2f : 1.5f, false));
        }

        // cut every line first (long ones wrap onto two rows rather than shrinking), then stack them on the panel
        Mark(cut, PanelTexture(), new Vector2(W * 0.97f, size.y * 0.93f), 0f, settings.panelColor);
        var heights = new List<float>();
        var made = new List<TextMeshPro>();
        foreach (var it in items)
        {
            if (it.rule) { made.Add(null); heights.Add(it.cap); continue; }
            var t = Line(cut.root.transform, it.text, it.font, settings.grooveColor, it.cap, 0f, maxW, it.spacing, false);
            made.Add(t);
            heights.Add(Mathf.Max(it.cap, t.textBounds.size.y));
            cut.lines.Add(t);
        }
        float total = 0f;
        for (int i = 0; i < items.Count; i++) total += heights[i] + (i < items.Count - 1 ? items[i].gap : 0f);
        float y = total * 0.5f;
        for (int i = 0; i < items.Count; i++)
        {
            float mid = y - heights[i] * 0.5f;
            if (items[i].rule) cut.rule = Mark(cut, RuleTexture(), new Vector2(W * 0.275f, items[i].cap * 3.4f), mid, new Color(1f, 1f, 1f, 0.95f));
            else made[i].rectTransform.localPosition += new Vector3(0f, mid, 0f);
            y -= heights[i] + items[i].gap;
        }
    }

    private static bool IsDates(string s)
    {
        int digits = s.Count(char.IsDigit);
        return digits >= 3 && s.All(ch => char.IsDigit(ch) || ch == '-' || ch == '–' || ch == ' ' || ch == '.' || ch == '/');
    }

    private static string DatesText(string s)
    {
        var parts = s.Split(new[] { '-', '–' }, System.StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        return parts.Length == 2 ? parts[0] + " – " + parts[1] : s;
    }

    private TextMeshPro Line(Transform parent, string text, TMP_FontAsset authored, Color color, float cap, float yMid, float maxW, float spacing, bool wrap)
    {
        var font = Loc.ResolveFont(authored, out float sizeScale);
        var go = new GameObject("Line", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.font = font;
        t.fontSharedMaterial = Carved(font, color);
        t.richText = false;
        t.fontStyle = FontStyles.UpperCase;
        t.characterSpacing = spacing;
        t.horizontalAlignment = HorizontalAlignmentOptions.Center;
        t.verticalAlignment = VerticalAlignmentOptions.Middle;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(maxW, cap * 8f);
        t.color = Color.white;
        t.text = text;
        if (font != null) font.TryAddCharacters(text.ToUpperInvariant());
        // size by the font's measured cap height, then keep the line inside the panel
        t.fontSize = cap / CapRatio(font) * sizeScale;
        t.ForceMeshUpdate(true, true);
        if (!wrap)
        {
            float w = t.textBounds.size.x;
            string one = text.Trim();
            int space = -1;
            for (int i = 0; i < one.Length; i++)
                if (one[i] == ' ' && (space < 0 || Mathf.Abs(i - one.Length / 2) < Mathf.Abs(space - one.Length / 2))) space = i;
            if (w > maxW * 1.38f && space > 0)
            {
                // too long for one row at a readable size: two rows, broken at the space nearest the middle
                t.text = one.Substring(0, space) + '\n' + one.Substring(space + 1);
                t.fontSize *= 0.9f;
                t.ForceMeshUpdate(true, true);
                w = t.textBounds.size.x;
            }
            if (w > maxW && w > 0f) { t.fontSize *= maxW / w; t.ForceMeshUpdate(true, true); }
        }
        // centre the visible letters on yMid (Middle centres the line box, which includes room for descenders)
        float off = t.textBounds.center.y;
        t.rectTransform.localPosition = new Vector3(0f, yMid - off, 0f);
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return t;
    }

    // Cap height per unit of font size, measured once per font.
    private static readonly Dictionary<TMP_FontAsset, float> capRatios = new Dictionary<TMP_FontAsset, float>();
    private static float CapRatio(TMP_FontAsset font)
    {
        if (font == null) return 0.07f;
        if (capRatios.TryGetValue(font, out float r)) return r;
        r = font.faceInfo.capLine > 0f && font.faceInfo.pointSize > 0f ? font.faceInfo.capLine / font.faceInfo.pointSize * 0.1f : 0.07f;
        capRatios[font] = r;
        return r;
    }

    // ------------------------------------------------------------------ materials and marks

    private static readonly Dictionary<(TMP_FontAsset, Color), Material> carvedCache = new Dictionary<(TMP_FontAsset, Color), Material>();
    private static readonly string[] carvedFloats = { "_FaceDilate", "_ShaderFlags", "_Bevel", "_BevelOffset", "_BevelWidth", "_BevelClamp", "_BevelRoundness",
                                                      "_BumpFace", "_BumpOutline", "_FaceShininess", "_OutlineShininess", "_OutlineWidth", "_OutlineSoftness", "_CullMode" };
    private static readonly string[] carvedColors = { "_SpecColor", "_OutlineColor" };

    private Material Carved(TMP_FontAsset font, Color face)
    {
        if (font == null) return null;
        if (carvedCache.TryGetValue((font, face), out var m) && m != null) return m;
        m = new Material(font.material) { name = font.name + " (carved)" };
        var tpl = settings.carvedTemplate;
        if (tpl != null)
        {
            m.shader = tpl.shader;
            foreach (var p in carvedFloats) if (tpl.HasProperty(p) && m.HasProperty(p)) m.SetFloat(p, tpl.GetFloat(p));
            foreach (var p in carvedColors) if (tpl.HasProperty(p) && m.HasProperty(p)) m.SetColor(p, tpl.GetColor(p));
        }
        if (m.HasProperty("_FaceColor")) m.SetColor("_FaceColor", face);
        carvedCache[(font, face)] = m;
        return m;
    }

    private Renderer Mark(Cut cut, Texture2D tex, Vector2 size, float yMid, Color tint)
    {
        if (settings.panelMaterial == null) return null;
        var go = new GameObject("Mark", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(cut.root.transform, false);
        go.transform.localPosition = new Vector3(0f, yMid, 0.0012f);
        go.GetComponent<MeshFilter>().sharedMesh = Quad();
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<MeshRenderer>();
        var m = new Material(settings.panelMaterial) { mainTexture = tex, color = tint };
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cut.marks.Add(r);
        return r;
    }

    private static Mesh quad;
    private static Mesh Quad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Inscription quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        quad.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
        quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        return quad;
    }

    private static Texture2D panelTex, ruleTex;
    // The smoother dressed panel the letters are cut into: soft edges, a little grain.
    private static Texture2D PanelTexture()
    {
        if (panelTex != null) return panelTex;
        const int n = 128;
        panelTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Inscription panel", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f, v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;
                float e = Mathf.Clamp01((1f - Mathf.Max(u, v)) / 0.28f);
                float a = e * e * (3f - 2f * e);
                float g = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                px[y * n + x] = new Color32((byte)(153 * g), (byte)(150 * g), (byte)(140 * g), (byte)(255 * a));
            }
        panelTex.SetPixels32(px);
        panelTex.Apply(true);
        return panelTex;
    }

    // A cut rule with diamond ends between the name and the epitaph.
    private static Texture2D RuleTexture()
    {
        if (ruleTex != null) return ruleTex;
        const int w = 256, h = 32;
        ruleTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Inscription rule", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f - h * 0.5f;
                float line = (cx > 30f && cx < w - 30f) ? Mathf.Clamp01(2.6f - Mathf.Abs(cy)) : 0f;
                float d = Mathf.Min(Mathf.Abs(cx - 18f), Mathf.Abs(cx - (w - 18f))) + Mathf.Abs(cy);
                float a = Mathf.Max(line, Mathf.Clamp01(9f - d));
                px[y * w + x] = new Color32(40, 39, 37, (byte)(255 * a));
            }
        ruleTex.SetPixels32(px);
        ruleTex.Apply(true);
        return ruleTex;
    }

    // ------------------------------------------------------------------ kneeling

    private enum Phase { None, Down, Hold, Up, Release }
    private Phase phase;
    private float phaseTime;
    private int startFrame, releaseFrame;
    private Movement movement;
    private Camera cam;
    private Vector3 restLocalPos; private Quaternion restLocalRot; private float restFov;
    private Vector3 fromPos, eyePos, lastPos; private Quaternion fromRot, eyeRot, lastRot;
    private float fromFov, eyeFov;
    private SimpleFlashlight torch; private Vector3 restHand; private float restAimMin;
    private GameObject crosshair; private bool crosshairWasOn;
    private float holdTime;

    /// <summary>True while someone is kneeling at a stone.</summary>
    public static bool IsReading => active != null;

    /// <summary>Kneel in front of this stone (called by ReadableTrigger.Interact).</summary>
    public void Read()
    {
        if (active != null || Time.time < rereadAfter) return;
        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen) return;
        movement = FindFirstObjectByType<Movement>();
        if (movement == null || !movement.canMove || !movement.canControlCamera) return; // someone else holds the player
        cam = movement.playerCamera != null ? movement.playerCamera : Camera.main;
        if (cam == null || faces == null || faces.Count == 0) return;

        var t = cam.transform;
        // the lettered face turned most toward us (or any face of a blank stone)
        TombstoneSettings.Face face = null; float best = float.MinValue;
        var candidates = cuts.Count > 0 ? cuts.Select(c => c.face).ToList() : faces;
        foreach (var f in candidates)
        {
            FaceFrame(f, out var c0, out var n0, out _, out _);
            float d = Vector3.Dot((t.position - c0).normalized, n0);
            if (d > best) { best = d; face = f; }
        }
        FaceFrame(face, out var center, out var normal, out var up, out var size);

        // where the eyes go: in front of the panel, low enough to read it, never through the ground or another stone
        float H = size.y;
        Vector3 flat = Vector3.ProjectOnPlane(normal, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(t.position - center, Vector3.up);
        flat.Normalize();
        float eyeY = center.y + settings.height * H;
        float ground = GroundBelow(center + flat * (settings.distance * H) + Vector3.up * 1.5f, center.y - 3f);
        eyeY = Mathf.Max(eyeY, ground + settings.minEyeHeight);
        float back = Mathf.Max(settings.distance * H, (eyeY - center.y) / Mathf.Tan(settings.maxPitch * Mathf.Deg2Rad));
        Vector3 look = center + up * (0.02f * H);
        Vector3 eye = new Vector3(center.x, eyeY, center.z) + flat * back;
        Vector3 toEye = eye - (center + normal * 0.05f);
        var blockers = Physics.RaycastAll(center + normal * 0.05f, toEye.normalized, toEye.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(blockers, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in blockers)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.root == movement.transform.root) continue;
            eye = center + normal * 0.05f + toEye.normalized * Mathf.Max(0.25f, hit.distance - 0.12f);
            break;
        }
        eyePos = eye;
        eyeRot = Quaternion.LookRotation(look - eye, Vector3.up);
        restFov = cam.fieldOfView;
        float dist = Vector3.Distance(eye, look);
        float fov = 2f * Mathf.Atan(H / settings.framing * 0.5f / dist) * Mathf.Rad2Deg;
        eyeFov = Mathf.Clamp(fov, settings.minFov, restFov);

        restLocalPos = t.localPosition; restLocalRot = t.localRotation;
        fromPos = t.position; fromRot = t.rotation; fromFov = restFov;
        lastPos = fromPos; lastRot = fromRot;

        torch = movement.GetComponentInChildren<SimpleFlashlight>(true);
        if (torch == null) torch = FindFirstObjectByType<SimpleFlashlight>();
        if (torch != null) { restHand = torch.handOffset; restAimMin = torch.aimMinDistance; }

        movement.DisableAllInput();
        crosshair = FindCrosshair();
        crosshairWasOn = crosshair != null && crosshair.activeSelf;
        if (crosshair != null) crosshair.SetActive(false);

        Play(settings.kneelSound);
        active = this;
        phase = Phase.Down; phaseTime = 0f; holdTime = 0f;
        startFrame = Time.frameCount;
    }

    /// <summary>Stand back up (the same as pressing E, Escape or clicking while kneeling).</summary>
    public void StandUp()
    {
        if (active != this || phase == Phase.Up || phase == Phase.Release) return;
        fromPos = cam.transform.position; fromRot = cam.transform.rotation; fromFov = cam.fieldOfView;
        phase = Phase.Up; phaseTime = 0f;
        Play(settings.standSound);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null || sfx == null) return;
        sfx.PlayOneShot(clip, settings.soundVolume);
    }

    private void LateUpdate()
    {
        if (active != this) return;
        if (cam == null || movement == null) { Abandon(); return; }
        var t = cam.transform;

        if (phase == Phase.Release)
        {
            // input comes back a frame after the camera, so the closing key reaches nothing else
            if (Time.frameCount > releaseFrame)
            {
                movement.EnableAllInput();
                active = null;
                rereadAfter = Time.time + settings.rereadDelay;
                phase = Phase.None;
            }
            return;
        }

        // A kill, a cinematic or a teleport moved the camera: let it have it.
        if (Time.frameCount > startFrame && ((t.position - lastPos).sqrMagnitude > 0.0004f || Quaternion.Angle(t.rotation, lastRot) > 2f))
        {
            Abandon();
            return;
        }

        float dt = Time.deltaTime;
        phaseTime += dt;
        Vector3 pos; Quaternion rot; float fov; float handK;

        if (phase == Phase.Down)
        {
            float k = Mathf.Clamp01(phaseTime / settings.kneelSeconds), e = EaseInOut(k);
            pos = Vector3.Lerp(fromPos, eyePos, e) + Vector3.down * (0.06f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f)));
            rot = Quaternion.Slerp(fromRot, eyeRot, e) * Quaternion.Euler(0f, 0f, 1.6f * Mathf.Sin(Mathf.PI * k));
            fov = Mathf.Lerp(fromFov, eyeFov, e);
            handK = e;
            if (k >= 1f) { phase = Phase.Hold; phaseTime = 0f; }
        }
        else if (phase == Phase.Hold)
        {
            holdTime += dt;
            float b = Mathf.Clamp01(holdTime / 0.5f);
            pos = eyePos + Vector3.up * (0.006f * b * Mathf.Sin(holdTime * Mathf.PI * 2f * 0.24f));
            rot = eyeRot * Quaternion.Euler(0.25f * b * Mathf.Sin(holdTime * 1.5f + 0.9f), 0.2f * b * Mathf.Sin(holdTime * 1.3f), 0.3f * b * Mathf.Sin(holdTime * 0.9f));
            fov = eyeFov; handK = 1f;
            if (Time.frameCount > startFrame + 1 && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
                StandUp();
        }
        else // Up
        {
            float k = Mathf.Clamp01(phaseTime / settings.standSeconds), e = EaseInOut(k);
            var parent = t.parent;
            Vector3 restPos = parent != null ? parent.TransformPoint(restLocalPos) : restLocalPos;
            Quaternion restRot = parent != null ? parent.rotation * restLocalRot : restLocalRot;
            pos = Vector3.Lerp(fromPos, restPos, e);
            rot = Quaternion.Slerp(fromRot, restRot, e);
            fov = Mathf.Lerp(fromFov, restFov, e);
            handK = 1f - e;
            if (k >= 1f) { Finish(); return; }
        }

        t.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        lastPos = t.position; lastRot = t.rotation;
        if (torch != null)
        {
            torch.handOffset = Vector3.Lerp(restHand, settings.kneelHand, handK);
            torch.aimMinDistance = Mathf.Lerp(restAimMin, settings.kneelAimMin, handK);
        }
    }

    private void Finish()
    {
        var t = cam.transform;
        t.localPosition = restLocalPos;
        t.localRotation = restLocalRot;
        cam.fieldOfView = restFov;
        movement.SyncRotationFromCamera();
        RestoreTorch();
        if (crosshair != null && crosshairWasOn) crosshair.SetActive(true);
        phase = Phase.Release;
        releaseFrame = Time.frameCount;
    }

    // Something else took the player over: give the torch back and step aside without touching the camera or input.
    private void Abandon()
    {
        RestoreTorch();
        phase = Phase.None;
        if (active == this) active = null;
    }

    private void RestoreTorch()
    {
        if (torch == null) return;
        torch.handOffset = restHand;
        torch.aimMinDistance = restAimMin;
    }

    private static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    private static float EaseInOut(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;

    private float GroundBelow(Vector3 from, float fallback)
    {
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(from, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (movement != null && h.collider.transform.root == movement.transform.root) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best == float.MinValue ? fallback : best;
    }

    private static GameObject FindCrosshair()
    {
        var mgr = FindFirstObjectByType<CrosshairManager>();
        if (mgr != null && mgr.uiCanvas != null)
        {
            var c = mgr.uiCanvas.transform.Find("Crosshair");
            if (c != null) return c.gameObject;
        }
        var canvas = GameObject.Find("CrosshairCanvas");
        if (canvas != null)
        {
            var c = canvas.transform.Find("Crosshair");
            if (c != null) return c.gameObject;
        }
        return null;
    }

    // ------------------------------------------------------------------ low sanity: the stone's words, for as long as it applies

    private bool hasLowWords;   // this stone has low-sanity words to show
    private float lowK;         // 0 = its own lettering, 1 = the low-sanity words
    private bool lowKSet;
    private float shakeIntensity, shakeSpeed;

    /// <summary>0 = the stone shows its own inscription, 1 = its low-sanity words (in between while it changes).</summary>
    public float LowSanityAmount => lowK;

    // Each lettered face also carries the stone's low-sanity words, hidden until the condition applies.
    private void CutLowWords(Cut cut, Vector2 size)
    {
        if (readable == null || !readable.TryGetLowSanity(out string[] pages, out TMP_FontAsset font, out Color color, out shakeIntensity, out shakeSpeed, false)) return;
        string text = pages != null && pages.Length > 0 ? Loc.Tr(pages[0]) : "";
        if (string.IsNullOrWhiteSpace(text)) return;
        // the red keeps its hue but stays readable in torchlight
        float m = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (m < 0.62f && m > 0f) color = new Color(color.r * 0.62f / m, color.g * 0.62f / m, color.b * 0.62f / m, 1f);
        var t = Line(cut.root.transform, text, font != null ? font : settings.nameFont, color, settings.lowSanityCap * size.y, 0f,
                     size.x * settings.maxLineWidth, 1f, true);
        float hgt = t.textBounds.size.y;
        if (hgt > size.y * 0.75f) { t.fontSize *= size.y * 0.75f / hgt; t.ForceMeshUpdate(true, true); }
        t.rectTransform.localPosition = new Vector3(0f, -t.textBounds.center.y, -0.0006f);
        cut.low = t;
        cut.lowAlpha = -1f;
        hasLowWords = true;
    }

    // While the stone's low-sanity condition holds, its own words are gone and the low-sanity words are cut in their place,
    // from any distance and all the way through kneeling. They change over turnSeconds when the condition starts or stops.
    private void Update()
    {
        if (!hasLowWords || settings == null) return;
        bool applies = readable != null && readable.LowSanityApplies;
        float target = applies ? 1f : 0f;
        if (!lowKSet) { lowK = target; lowKSet = true; }
        else lowK = Mathf.MoveTowards(lowK, target, Time.deltaTime / Mathf.Max(0.01f, settings.turnSeconds));
        float k = lowK * lowK * (3f - 2f * lowK);
        var view = Camera.main;
        foreach (var cut in cuts)
        {
            if (cut.low == null) continue;
            SetLettering(cut, 1f - k);
            bool show = k > 0.001f;
            if (cut.low.gameObject.activeSelf != show) { cut.low.gameObject.SetActive(show); cut.lowAlpha = -1f; }
            if (!show) continue;
            bool near = view != null && (view.transform.position - cut.root.transform.position).sqrMagnitude < 18f * 18f;
            if (near) Shake(cut.low, k);
            else if (Mathf.Abs(cut.lowAlpha - k) > 0.01f) { cut.low.alpha = k; cut.lowAlpha = k; }
        }
    }

    private static void SetLettering(Cut cut, float a)
    {
        if (Mathf.Abs(cut.letteringAlpha - a) < 0.005f) return;
        cut.letteringAlpha = a;
        foreach (var l in cut.lines) if (l != null) l.alpha = a;
        if (cut.rule != null) { var c = cut.rule.sharedMaterial.color; c.a = 0.95f * a; cut.rule.sharedMaterial.color = c; }
    }

    // As the overlay's text shake: every letter wanders on its own, harder while the stone is changing.
    private void Shake(TextMeshPro t, float k)
    {
        if (t.alpha < 0.999f) t.alpha = 1f;          // the fade goes into the vertex colours, so TMP is not rebuilt every frame
        t.ForceMeshUpdate();
        var info = t.textInfo;
        float letter = t.fontSize * CapRatio(t.font);
        float amp = letter * settings.shakeScale * Mathf.Max(0.5f, shakeIntensity) * (1f + 2.5f * (1f - k));
        float time = Time.time * Mathf.Max(1f, shakeSpeed);
        byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(k));
        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            int mi = ch.materialReferenceIndex;
            var verts = info.meshInfo[mi].vertices; var cols = info.meshInfo[mi].colors32;
            var off = new Vector3(Mathf.PerlinNoise(time + i * 0.3f, 0f) * 2f - 1f, Mathf.PerlinNoise(0f, time + i * 0.3f) * 2f - 1f, 0f) * amp;
            for (int v = 0; v < 4; v++) { verts[ch.vertexIndex + v] += off; var c = cols[ch.vertexIndex + v]; c.a = alpha; cols[ch.vertexIndex + v] = c; }
        }
        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
            info.meshInfo[i].mesh.colors32 = info.meshInfo[i].colors32;
            t.UpdateGeometry(info.meshInfo[i].mesh, i);
        }
        var cutOf = cuts.FirstOrDefault(c => c.low == t);
        if (cutOf != null) cutOf.lowAlpha = k;
    }
}
