using UnityEngine;

/// <summary>
/// Runs the tape picture (Hidden/TapePass) for one camera: filters the frame down to the tape's lines, blooms it,
/// applies the camera's grade, records it to "tape" with the previous field leaking in, and rebuilds the picture
/// at screen size. Owns the two field buffers that carry the picture from one frame to the next.
/// </summary>
public sealed class TapeRenderer
{
    public const string ShaderResource = "Shaders/TapePass";

    private static Shader shader;
    private static bool searched;

    private static readonly int Foot = Shader.PropertyToID("_Foot");
    private static readonly int BloomTex = Shader.PropertyToID("_BloomTex");
    private static readonly int PrevTex = Shader.PropertyToID("_PrevTex");
    private static readonly int BloomThresh = Shader.PropertyToID("_BloomThresh");
    private static readonly int Dir = Shader.PropertyToID("_Dir");
    private static readonly int BloomAmt = Shader.PropertyToID("_BloomAmt");
    private static readonly int Knee = Shader.PropertyToID("_Knee");
    private static readonly int Contrast = Shader.PropertyToID("_Contrast");
    private static readonly int GradeSat = Shader.PropertyToID("_GradeSat");
    private static readonly int Gamma = Shader.PropertyToID("_Gamma");
    private static readonly int Bright = Shader.PropertyToID("_Bright");
    private static readonly int Vig = Shader.PropertyToID("_Vig");
    private static readonly int VigSoft = Shader.PropertyToID("_VigSoft");
    private static readonly int VigRound = Shader.PropertyToID("_VigRound");
    private static readonly int BlackPoint = Shader.PropertyToID("_BlackPoint");
    private static readonly int PreOffset = Shader.PropertyToID("_PreOffset");
    private static readonly int NightGain = Shader.PropertyToID("_NightGain");
    private static readonly int SigSize = Shader.PropertyToID("_SigSize");
    private static readonly int Frame = Shader.PropertyToID("_Frame");
    private static readonly int TimeS = Shader.PropertyToID("_TimeS");
    private static readonly int LumaSigma = Shader.PropertyToID("_LumaSigma");
    private static readonly int Sharp = Shader.PropertyToID("_Sharp");
    private static readonly int ChromaSigma = Shader.PropertyToID("_ChromaSigma");
    private static readonly int ChromaDelay = Shader.PropertyToID("_ChromaDelay");
    private static readonly int ChromaVert = Shader.PropertyToID("_ChromaVert");
    private static readonly int LumaNoise = Shader.PropertyToID("_LumaNoise");
    private static readonly int LineNoise = Shader.PropertyToID("_LineNoise");
    private static readonly int ChromaNoise = Shader.PropertyToID("_ChromaNoise");
    private static readonly int GrainLen = Shader.PropertyToID("_GrainLen");
    private static readonly int Wobble = Shader.PropertyToID("_Wobble");
    private static readonly int Jitter = Shader.PropertyToID("_Jitter");
    private static readonly int HeadLines = Shader.PropertyToID("_HeadLines");
    private static readonly int HeadShift = Shader.PropertyToID("_HeadShift");
    private static readonly int Dropout = Shader.PropertyToID("_Dropout");
    private static readonly int Tracking = Shader.PropertyToID("_Tracking");
    private static readonly int TrackPos = Shader.PropertyToID("_TrackPos");
    private static readonly int Glitch = Shader.PropertyToID("_Glitch");
    private static readonly int ChromaLag = Shader.PropertyToID("_ChromaLag");
    private static readonly int LumaLag = Shader.PropertyToID("_LumaLag");
    private static readonly int Sat = Shader.PropertyToID("_Sat");
    private static readonly int Hue = Shader.PropertyToID("_Hue");
    private static readonly int Lift = Shader.PropertyToID("_Lift");
    private static readonly int Beam = Shader.PropertyToID("_Beam");
    private static readonly int FieldOfs = Shader.PropertyToID("_FieldOfs");

    /// <summary>
    /// For offline rendering only (an editor job that draws a clip's frames one after another): set this to the
    /// clip's time in seconds before each camera render, so the tape's noise, tracking bands, wear and lag follow the
    /// clip instead of the wall clock. Set it back to -1 when the job ends. The game never sets it.
    /// </summary>
    public static float ClockOverride = -1f;

    /// <summary>The tape's clock, in seconds. Keeps running while the game is paused.</summary>
    public static float Clock
    {
        get
        {
            if (ClockOverride >= 0f) return ClockOverride;
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }
    }

    private Material material;
    private RenderTexture fieldA, fieldB;
    private bool flip, hasPrev;
    private float lastTime = -1f;

    /// <summary>True when the tape shader is present and this GPU can run it.</summary>
    public static bool Supported
    {
        get
        {
            if (!searched)
            {
                searched = true;
                shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null) shader = Shader.Find("Hidden/TapePass");
                if (shader == null) Debug.LogWarning("TapeRenderer: Resources/" + ShaderResource + " is missing; the picture falls back to Classic.");
            }
            return shader != null && shader.isSupported;
        }
    }

    private static RenderTextureFormat Format =>
        SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;

    private static RenderTexture Temp(int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 0, Format);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        return rt;
    }

    private void EnsureFields(int w, int h)
    {
        if (fieldA != null && fieldA.width == w && fieldA.height == h && fieldA.IsCreated() && fieldB.IsCreated()) return;
        ReleaseFields();
        fieldA = NewField(w, h);
        fieldB = NewField(w, h);
        hasPrev = false;
    }

    private static RenderTexture NewField(int w, int h)
    {
        var rt = new RenderTexture(w, h, 0, Format)
        {
            name = "Tape field",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        rt.Create();
        return rt;
    }

    private void ReleaseFields()
    {
        if (fieldA != null) { fieldA.Release(); Object.DestroyImmediate(fieldA); fieldA = null; }
        if (fieldB != null) { fieldB.Release(); Object.DestroyImmediate(fieldB); fieldB = null; }
        hasPrev = false;
    }

    /// <summary>Forgets the previous field (after a cut, a mode change or a teleport fade nothing should leak).</summary>
    public void Reset()
    {
        hasPrev = false;
    }

    public void Dispose()
    {
        ReleaseFields();
        if (material != null) { Object.DestroyImmediate(material); material = null; }
    }

    /// <summary>Draws <paramref name="source"/> as tape into <paramref name="destination"/>. <paramref name="time"/> is in seconds and should not stop while the game is paused.</summary>
    public void Render(RenderTexture source, RenderTexture destination, TapeFrame p, float time)
    {
        if (!Supported) { Graphics.Blit(source, destination); return; }
        if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        int h = Mathf.Clamp(Mathf.RoundToInt(p.lines), 120, 1080);
        int w = Mathf.Max(16, Mathf.RoundToInt(h * (float)source.width / Mathf.Max(1, source.height)));
        EnsureFields(w, h);

        // the tape's own clock: 60 fields a second, whatever the frame rate
        float field = Mathf.Floor(time * 60f);
        float dt = lastTime < 0f ? 1f / 60f : Mathf.Clamp(time - lastTime, 1f / 240f, 1f / 15f);
        lastTime = time;
        float fields = dt * 60f;

        RenderTexture sig = Temp(w, h), graded = Temp(w, h);
        int bw = Mathf.Max(2, w >> 1), bh = Mathf.Max(2, h >> 1);
        RenderTexture b0 = Temp(bw, bh), b1 = Temp(bw, bh);

        material.SetVector(Foot, new Vector4((1f - p.crunch) / w, (1f - p.crunch) / h, 0f, 0f));
        Graphics.Blit(source, sig, material, 0);

        material.SetFloat(BloomThresh, p.bloomThresh);
        Graphics.Blit(sig, b0, material, 1);
        float spread = p.bloomSpread * (h / 480f);
        Blur(b0, b1, 1.6f * spread / bw, 0f);
        Blur(b1, b0, 0f, 1.2f * spread / bh);
        Blur(b0, b1, 4.2f * spread / bw, 0f);
        Blur(b1, b0, 0f, 2.6f * spread / bh);

        material.SetTexture(BloomTex, b0);
        material.SetFloat(BloomAmt, p.bloomAmt);
        material.SetFloat(Knee, Mathf.Clamp(p.knee, 0.05f, 0.99f));
        material.SetFloat(Contrast, p.contrast);
        material.SetFloat(GradeSat, p.gradeSat);
        material.SetFloat(Gamma, Mathf.Max(p.gamma, 0.01f));
        material.SetFloat(Bright, p.bright);
        material.SetFloat(Vig, p.vig);
        material.SetFloat(VigSoft, p.vigSoft);
        material.SetFloat(VigRound, p.vigRound);
        material.SetFloat(BlackPoint, p.blackPoint);
        material.SetFloat(PreOffset, p.preOffset);
        material.SetFloat(NightGain, p.nightGain);
        Graphics.Blit(sig, graded, material, 3);

        RenderTexture cur = flip ? fieldB : fieldA, prev = flip ? fieldA : fieldB;
        material.SetTexture(PrevTex, prev);
        material.SetVector(SigSize, new Vector4(w, h, 1f / w, 1f / h));
        material.SetFloat(Frame, Mathf.Repeat(field, 4096f));
        material.SetFloat(TimeS, Mathf.Repeat(time, 3600f));
        material.SetFloat(LumaSigma, p.lumaSigma);
        material.SetFloat(Sharp, p.sharp);
        material.SetFloat(ChromaSigma, p.chromaSigma);
        material.SetFloat(ChromaDelay, p.chromaDelay);
        material.SetFloat(ChromaVert, p.chromaVert);
        material.SetFloat(LumaNoise, p.lumaNoise);
        material.SetFloat(LineNoise, p.lineNoise);
        material.SetFloat(ChromaNoise, p.chromaNoise);
        material.SetFloat(GrainLen, p.grainLen);
        material.SetFloat(Wobble, p.wobble);
        material.SetFloat(Jitter, p.jitter);
        material.SetFloat(HeadLines, p.headLines);
        material.SetFloat(HeadShift, p.headShift);
        material.SetFloat(Dropout, p.dropout);
        material.SetFloat(Tracking, p.tracking);
        material.SetFloat(TrackPos, p.trackPos);
        material.SetFloat(Glitch, Mathf.Clamp01(p.glitch));
        // lag is tuned per 1/60 s; keep it the same at any frame rate
        material.SetFloat(ChromaLag, hasPrev ? 1f - Mathf.Pow(1f - Mathf.Clamp(p.chromaLag, 0f, 0.95f), fields) : 0f);
        material.SetFloat(LumaLag, hasPrev ? 1f - Mathf.Pow(1f - Mathf.Clamp(p.lumaLag, 0f, 0.95f), fields) : 0f);
        material.SetFloat(Sat, p.sat);
        material.SetFloat(Hue, p.hue);
        material.SetFloat(Lift, p.lift);
        Graphics.Blit(graded, cur, material, 4);

        material.SetFloat(Beam, Mathf.Max(p.beam, 0.2f));
        material.SetFloat(FieldOfs, (((int)field & 1) == 1 ? 0.25f : -0.25f) * p.interlace);
        Graphics.Blit(cur, destination, material, 5);

        flip = !flip;
        hasPrev = true;
        RenderTexture.ReleaseTemporary(sig);
        RenderTexture.ReleaseTemporary(graded);
        RenderTexture.ReleaseTemporary(b0);
        RenderTexture.ReleaseTemporary(b1);
    }

    private void Blur(RenderTexture from, RenderTexture to, float x, float y)
    {
        material.SetVector(Dir, new Vector4(x, y, 0f, 0f));
        Graphics.Blit(from, to, material, 2);
    }
}
