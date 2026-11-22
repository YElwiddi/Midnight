using System;
using UnityEngine;

/// <summary>
/// One condition of the tape: fresh, light, worn, failing, or the kill beat. The picture blends between
/// neighbouring states as sanity drops and kills hit. Pixel distances are for an 853-pixel-wide picture.
/// </summary>
[Serializable]
public class TapeState
{
    [Header("Picture")]
    [Tooltip("Edge ringing from video sharpening (0 = none)")]
    [Range(0f, 1.5f)] public float sharpness = 0.55f;
    [Tooltip("How far colour blurs sideways, in pixels")]
    [Range(0.5f, 12f)] public float chromaBlur = 6.5f;
    [Tooltip("How late colour arrives, in pixels")]
    [Range(0f, 12f)] public float chromaDelay = 2.2f;
    [Tooltip("How much of the last field's colour hangs on in motion (per 1/60 s)")]
    [Range(0f, 0.8f)] public float chromaLag = 0.35f;
    [Range(0f, 1.5f)] public float saturation = 1.08f;
    [Tooltip("Colour drift, in radians")]
    [Range(-1f, 1f)] public float hue = 0f;

    [Header("Noise")]
    [Range(0f, 0.2f)] public float lumaNoise = 0.035f;
    [Tooltip("Random brightness offset of whole lines")]
    [Range(0f, 0.05f)] public float lineNoise = 0.006f;
    [Range(0f, 0.2f)] public float chromaNoise = 0.032f;
    [Tooltip("Chance per line, per field, of a white dropout streak")]
    [Range(0f, 0.02f)] public float dropout = 0.00004f;

    [Header("Time base")]
    [Tooltip("Slow sideways sway of the lines, in pixels")]
    [Range(0f, 4f)] public float wobble = 0.35f;
    [Tooltip("Random sideways jitter of each line, in pixels")]
    [Range(0f, 3f)] public float jitter = 0.22f;
    [Tooltip("Lines at the bottom that tear sideways (head switching). 0 = none. Only the kill state uses it: a strip that never goes away reads as a broken screen")]
    [Range(0f, 30f)] public float headLines = 0f;
    [Range(0f, 60f)] public float headShift = 14f;

    [Header("Events")]
    [Tooltip("Strength of the tracking band that rolls up the picture (0 = never)")]
    [Range(0f, 1f)] public float bandAmount = 0f;
    [Tooltip("Seconds between tracking bands")]
    public float bandEvery = 9f;
    [Tooltip("Seconds a band takes to cross the picture")]
    public float bandSeconds = 1.7f;
    [Tooltip("Torn blocks (0 = never)")]
    [Range(0f, 1f)] public float glitch = 0f;
    [Tooltip("Seconds between bursts of torn blocks. At or below Glitch Seconds = constant")]
    public float glitchEvery = 2.3f;
    public float glitchSeconds = 0.4f;

    /// <summary>The tracking band at this moment: strength and height in the picture (0 = bottom).</summary>
    public void Band(float time, out float amount, out float position)
    {
        amount = 0f; position = 0.3f;
        if (bandAmount <= 0f || bandEvery <= 0f || bandSeconds <= 0f) return;
        float phase = Mathf.Repeat(time, bandEvery);
        if (phase >= bandSeconds) return;
        float u = phase / bandSeconds;
        amount = bandAmount * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
        position = -0.06f + 1.12f * u;
    }

    public float GlitchNow(float time)
    {
        if (glitch <= 0f) return 0f;
        if (glitchEvery <= glitchSeconds) return glitch;
        return Mathf.Repeat(time, glitchEvery) < glitchSeconds ? glitch : 0f;
    }

    public static TapeState Tape() => new TapeState();

    public static TapeState Light() => new TapeState
    {
        sharpness = 0.4f, chromaBlur = 4.5f, chromaLag = 0.2f,
        lumaNoise = 0.018f, lineNoise = 0.003f, chromaNoise = 0.015f, dropout = 0f,
        wobble = 0.2f, jitter = 0.1f, headLines = 0f,
    };

    // Sanity 50 and 25 are kept moderate so low sanity stays playable: rare, weak tracking bands and tears, and
    // only a touch of colour drift (more turned the warm lamp light magenta).
    public static TapeState Worn() => new TapeState
    {
        chromaDelay = 2.8f, saturation = 1.05f,
        lumaNoise = 0.045f, lineNoise = 0.008f, chromaNoise = 0.045f, dropout = 0.0004f,
        wobble = 0.57f, jitter = 0.34f, headShift = 22f,
        bandAmount = 0.22f, bandEvery = 14f, bandSeconds = 1.5f,
    };

    public static TapeState Failing() => new TapeState
    {
        chromaDelay = 3.9f, saturation = 0.96f, hue = 0.04f,
        lumaNoise = 0.058f, lineNoise = 0.012f, chromaNoise = 0.062f, dropout = 0.0015f,
        wobble = 0.92f, jitter = 0.52f, headShift = 34f,
        bandAmount = 0.45f, bandEvery = 7f, bandSeconds = 1.4f,
        glitch = 0.25f, glitchEvery = 5.5f, glitchSeconds = 0.3f,
    };

    // Old-style kills: harsher than the failing tape, with the tearing constant, the band coming round faster and
    // the head-switch strip. Little colour drift, so the killer stays readable and a red lamp stays red.
    public static TapeState Kill() => new TapeState
    {
        chromaDelay = 6f, saturation = 0.8f, hue = 0.08f,
        lumaNoise = 0.08f, lineNoise = 0.02f, chromaNoise = 0.09f, dropout = 0.004f,
        wobble = 1.6f, jitter = 0.9f, headLines = 14f, headShift = 34f,
        bandAmount = 1f, bandEvery = 1.6f, bandSeconds = 1.1f,
        glitch = 1f, glitchEvery = 0f, glitchSeconds = 0.4f,
    };
}

/// <summary>Everything the tape shader needs for one frame. Filled by <see cref="TapeProfile.Compose"/>.</summary>
public class TapeFrame
{
    public float lines, crunch, lumaSigma, sharp, chromaSigma, chromaDelay, chromaVert;
    public float lumaNoise, lineNoise, chromaNoise, grainLen;
    public float wobble, jitter, headLines, headShift;
    public float dropout, tracking, trackPos, glitch;
    public float chromaLag, lumaLag;
    public float bloomThresh, bloomAmt, bloomSpread, knee;
    public float sat, hue, lift, beam, interlace, nightGain;
    // the camera's own grade, carried over from the classic effect
    public float contrast = 1f, gradeSat = 1f, gamma = 1f, bright, vig, vigSoft = 1f, vigRound = 1f, blackPoint, preOffset;
}

/// <summary>
/// The tape look shared by every camera: the tape format itself, the five states the picture moves between,
/// and how the old VHS values that sanity and kill scripts write are read as wear.
/// Optional asset at Resources/TapeProfile; without it the defaults below are used.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Tape Profile", fileName = "TapeProfile")]
public class TapeProfile : ScriptableObject
{
    public const string ResourcesPath = "TapeProfile";

    [Header("Tape format")]
    [Tooltip("Picture lines. 480 = camcorder tape; 240 is chunkier and softer")]
    [Range(120f, 720f)] public float lines = 480f;
    [Tooltip("0 = each tape pixel averages the frame under it; 1 = one sample each (crunchy, shimmers)")]
    [Range(0f, 1f)] public float crunch = 0f;
    [Tooltip("Sideways softness of brightness, in pixels at 853 wide")]
    [Range(0.3f, 4f)] public float softness = 1.15f;
    [Tooltip("How far colour smears into the next line (0..1 line)")]
    [Range(0f, 1f)] public float chromaSmearDown = 0.5f;
    [Tooltip("Length of a grain streak, in pixels")]
    [Range(0.5f, 8f)] public float grainLength = 2.2f;
    [Tooltip("How much of the last field's brightness hangs on (per 1/60 s)")]
    [Range(0f, 0.5f)] public float lumaLag = 0.06f;
    [Tooltip("Black pedestal the noise rides on")]
    [Range(0f, 0.06f)] public float blackLift = 0.012f;
    [Tooltip("Width of each drawn line. Lower shows scanlines")]
    [Range(0.3f, 0.8f)] public float beam = 0.5f;
    [Tooltip("Half-line bob between fields")]
    [Range(0f, 1f)] public float interlace = 0.25f;

    [Header("Lens")]
    [Range(0f, 2f)] public float bloomThreshold = 0.75f;
    [Range(0f, 2f)] public float bloomAmount = 0.35f;
    [Range(0.2f, 4f)] public float bloomSpread = 1f;
    [Tooltip("Where whites start to clip softly")]
    [Range(0.3f, 0.99f)] public float whiteKnee = 0.8f;
    [Tooltip("Camcorder gain-up: lifts only the deepest shadows and adds noise. 0 = the night as lit")]
    [Range(0f, 1f)] public float nightGain = 0f;

    [Header("States")]
    public TapeState tape = TapeState.Tape();
    public TapeState light = TapeState.Light();
    [Tooltip("Sanity 50")]
    public TapeState worn = TapeState.Worn();
    [Tooltip("Sanity 25")]
    public TapeState failing = TapeState.Failing();
    [Tooltip("Old-style kills (crypt fiends, the Father): the failing tape with the tearing and the band at full")]
    public TapeState kill = TapeState.Kill();
    [Tooltip("How much of the wear and of the kill beats players on Light get")]
    [Range(0f, 1f)] public float lightWearScale = 0.5f;

    [Header("Wear, from the old VHS values the sanity and kill scripts set")]
    [Tooltip("VHSRetroFeature.noiseIntensity -> wear (x = fresh, y = worn, z = failing, w = kill). 0.08 at rest; " +
             "the sanity levels set 0.142 and 0.216; old-style kills 0.25 to 0.481")]
    public Vector4 noiseSteps = new Vector4(0.08f, 0.142f, 0.216f, 0.481f);
    [Tooltip("VHSRetroFeature.scanlineIntensity only rises in old-style kills (0.8 to 0.957): at or above this the tape is in the kill state")]
    public float scanlineKill = 0.8f;
    [Tooltip("Seconds for one step of wear to settle when sanity recovers. Wear always rises at once")]
    public float wearRelease = 0.35f;

    [Header("Kill beats, frame for frame (the scripted kills drive these every frame)")]
    [Tooltip("VHSRetroFeature.glitchIntensity -> torn blocks: x = where tearing starts (sanity never sets more than 0.3), y = full tearing")]
    public Vector2 glitchHit = new Vector2(0.3f, 1f);
    [Tooltip("Line jitter a full hit adds, in pixels")]
    public float hitJitter = 1.2f;
    [Tooltip("Sideways sway a full hit adds, in pixels")]
    public float hitWobble = 1.5f;
    [Tooltip("Grain a full hit adds")]
    public float hitNoise = 0.03f;
    [Tooltip("Colour delay a full hit adds, in pixels")]
    public float hitChromaDelay = 3f;
    [Tooltip("VHSRetroFeature.trackingNoise -> a tracking band that keeps rolling: x = where it starts (sanity stays below 0.1), y = full band")]
    public Vector2 trackingBand = new Vector2(0.1f, 0.25f);
    public float trackingBandEvery = 1.6f;
    public float trackingBandSeconds = 1.1f;
    [Tooltip("Extra colour delay, in pixels, per unit of VHSRetroFeature.rgbShiftAmount")]
    public float rgbShiftToDelay = 150f;
    [Tooltip("VHSRetroFeature.dropoutIntensity at rest; anything above adds dropouts")]
    public float dropoutRest = 0.024f;
    public float dropoutScale = 0.2f;

    private static TapeProfile shared;

    /// <summary>The asset at Resources/TapeProfile, or built-in defaults when there is none.</summary>
    public static TapeProfile Shared
    {
        get
        {
            if (shared == null)
            {
                shared = Resources.Load<TapeProfile>(ResourcesPath);
                if (shared == null)
                {
                    shared = CreateInstance<TapeProfile>();
                    shared.hideFlags = HideFlags.HideAndDontSave;
                    shared.name = "TapeProfile (defaults)";
                }
            }
            return shared;
        }
    }

    private static float Steps(float value, Vector4 s)
    {
        if (value <= s.x) return 0f;
        if (value < s.y) return Mathf.InverseLerp(s.x, s.y, value);
        if (value < s.z) return 1f + Mathf.InverseLerp(s.y, s.z, value);
        if (value < s.w) return 2f + Mathf.InverseLerp(s.z, s.w, value);
        return 3f;
    }

    /// <summary>
    /// Wear (0 = fresh, 1 = worn, 2 = failing, 3 = kill) from the values the sanity levels and the old-style kills
    /// set on the classic VHS component. Noise is the one value every sanity level sets and no scripted kill
    /// touches, so a kill sequence's own glitch and tracking values never push the whole tape into the red.
    /// </summary>
    public float Wear(float noiseIntensity, float scanlineIntensity)
    {
        float w = Steps(noiseIntensity, noiseSteps);
        if (scanlineKill > 0f && scanlineIntensity >= scanlineKill) w = 3f;
        return w;
    }

    /// <summary>
    /// Fills the tape part of <paramref name="f"/> for this moment. The grade fields are the caller's.
    /// <paramref name="wear"/> picks the tape's state; the hook values are VHSRetroFeature's glitchIntensity,
    /// trackingNoise, rgbShiftAmount and dropoutIntensity as the kill scripts set them this frame.
    /// </summary>
    public void Compose(TapeFrame f, PictureMode mode, float wear, float time, float glitchHook, float trackingHook, float rgbShift, float dropoutHook)
    {
        bool isLight = mode == PictureMode.Light;
        float w = Mathf.Clamp(isLight ? wear * lightWearScale : wear, 0f, 3f);
        TapeState a, b;
        float k;
        if (w <= 1f) { a = isLight ? light : tape; b = worn; k = w; }
        else if (w <= 2f) { a = worn; b = failing; k = w - 1f; }
        else { a = failing; b = kill; k = w - 2f; }

        f.sharp = Mathf.Lerp(a.sharpness, b.sharpness, k);
        f.chromaSigma = Mathf.Lerp(a.chromaBlur, b.chromaBlur, k);
        f.chromaDelay = Mathf.Lerp(a.chromaDelay, b.chromaDelay, k) + Mathf.Max(0f, rgbShift) * rgbShiftToDelay;
        f.chromaLag = Mathf.Lerp(a.chromaLag, b.chromaLag, k);
        f.sat = Mathf.Lerp(a.saturation, b.saturation, k);
        f.hue = Mathf.Lerp(a.hue, b.hue, k);
        f.lumaNoise = Mathf.Lerp(a.lumaNoise, b.lumaNoise, k) * (1f + 0.4f * nightGain);
        f.lineNoise = Mathf.Lerp(a.lineNoise, b.lineNoise, k);
        f.chromaNoise = Mathf.Lerp(a.chromaNoise, b.chromaNoise, k);
        f.dropout = Mathf.Lerp(a.dropout, b.dropout, k) + Mathf.Max(0f, dropoutHook - dropoutRest) * dropoutScale;
        f.wobble = Mathf.Lerp(a.wobble, b.wobble, k);
        f.jitter = Mathf.Lerp(a.jitter, b.jitter, k);
        f.headLines = Mathf.Lerp(a.headLines, b.headLines, k);
        f.headShift = Mathf.Lerp(a.headShift, b.headShift, k);

        float bandA, posA, bandB, posB;
        a.Band(time, out bandA, out posA);
        b.Band(time, out bandB, out posB);
        bandA *= 1f - k; bandB *= k;
        f.tracking = bandA + bandB;
        f.trackPos = bandB >= bandA ? posB : posA;
        f.glitch = Mathf.Lerp(a.GlitchNow(time), b.GlitchNow(time), k);

        // A kill sequence's beats: glitch above anything sanity sets tears the picture on that very frame.
        float hit = Mathf.Clamp01(Mathf.InverseLerp(glitchHit.x, glitchHit.y, glitchHook)) * (isLight ? lightWearScale : 1f);
        if (hit > 0f)
        {
            f.glitch = Mathf.Max(f.glitch, hit);
            f.jitter += hit * hitJitter;
            f.wobble += hit * hitWobble;
            f.lumaNoise += hit * hitNoise;
            f.chromaDelay += hit * hitChromaDelay;
        }
        // Tracking far above the sanity range keeps a band rolling for as long as it is held.
        float roll = Mathf.Clamp01(Mathf.InverseLerp(trackingBand.x, trackingBand.y, trackingHook)) * (isLight ? lightWearScale : 1f);
        if (roll > 0f && trackingBandEvery > 0f && trackingBandSeconds > 0f)
        {
            float phase = Mathf.Repeat(time, trackingBandEvery);
            if (phase < trackingBandSeconds)
            {
                float u = phase / trackingBandSeconds;
                float amount = roll * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
                if (amount > f.tracking) { f.tracking = amount; f.trackPos = -0.06f + 1.12f * u; }
            }
        }

        f.lines = lines;
        f.crunch = crunch;
        f.lumaSigma = softness;
        f.chromaVert = chromaSmearDown;
        f.grainLen = grainLength;
        f.lumaLag = lumaLag;
        f.lift = blackLift;
        f.beam = beam;
        f.interlace = interlace;
        f.bloomThresh = bloomThreshold;
        f.bloomAmt = bloomAmount;
        f.bloomSpread = bloomSpread;
        f.knee = whiteKnee;
        f.nightGain = nightGain;
    }
}
