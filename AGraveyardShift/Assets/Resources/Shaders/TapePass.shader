// The tape picture: a late-90s camcorder tape's signal chain, run on the camera image.
//   0 Down    area-filter the frame to the tape's line count
//   1 Bright  what the lens blooms
//   2 Blur    separable blur for the bloom
//   3 Comp    bloom + white clip, then the game's own grade, vignette and black point
//   4 Tape    the recording: luma/chroma bandwidth, noise, time-base errors, head switch, dropouts, wear
//   5 Show    rebuild the picture from its lines at screen size
// Driven by TapeRenderer. Lives in Resources so builds always include it.
// Tuned at 853 x 480 (16:9); every pixel distance below is scaled by the real line count.
Shader "Hidden/TapePass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }

    CGINCLUDE
    #pragma target 3.5
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _BloomTex;
    sampler2D _PrevTex;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // integer hash (lowbias32, public domain): the same noise on every GPU
    uint hu(uint x)
    {
        x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
        return x;
    }
    float h3(float a, float b, float c)
    {
        uint u = hu((uint)(int)a + hu((uint)(int)b + hu((uint)(int)c)));
        return (float)(u >> 8) * (1.0 / 16777216.0);
    }
    float lum(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
    float3 rgb2yiq(float3 c)
    {
        return float3(dot(c, float3(0.299, 0.587, 0.114)), dot(c, float3(0.5959, -0.2746, -0.3213)), dot(c, float3(0.2115, -0.5227, 0.3112)));
    }
    float3 yiq2rgb(float3 c)
    {
        return float3(c.x + 0.956 * c.y + 0.619 * c.z, c.x - 0.272 * c.y - 0.647 * c.z, c.x - 1.106 * c.y + 1.703 * c.z);
    }
    float3 S(sampler2D t, float2 uv) { return tex2Dlod(t, float4(uv, 0.0, 0.0)).rgb; }

    // ---------------------------------------------------------------- 0 Down
    float2 _Foot;   // one tape pixel, in source uv

    half4 fragDown(v2f i) : SV_Target
    {
        float3 acc = 0.0;
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                float2 d = (float2(x, y) + 0.5) * 0.25 - 0.5;
                acc += max(S(_MainTex, i.uv + d * _Foot), 0.0);
            }
        }
        return half4(acc * (1.0 / 16.0), 1.0);
    }

    // ---------------------------------------------------------------- 1 Bright
    float _BloomThresh;

    half4 fragBright(v2f i) : SV_Target
    {
        float3 c = S(_MainTex, i.uv);
        float l = max(c.r, max(c.g, c.b));
        float k = max(l - _BloomThresh, 0.0) / max(l, 1e-4);
        return half4(c * k, 1.0);
    }

    // ---------------------------------------------------------------- 2 Blur
    float2 _Dir;

    half4 fragBlur(v2f i) : SV_Target
    {
        static const float w[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };
        float3 c = S(_MainTex, i.uv) * w[0];
        for (int k = 1; k < 5; k++)
        {
            c += S(_MainTex, i.uv + _Dir * k) * w[k];
            c += S(_MainTex, i.uv - _Dir * k) * w[k];
        }
        return half4(c, 1.0);
    }

    // ---------------------------------------------------------------- 3 Comp
    float _BloomAmt, _Knee;
    float _Contrast, _GradeSat, _Gamma, _Bright, _Vig, _VigSoft, _VigRound, _BlackPoint, _PreOffset, _NightGain;

    half4 fragComp(v2f i) : SV_Target
    {
        float3 c = S(_MainTex, i.uv) + S(_BloomTex, i.uv) * _BloomAmt;
        // a camcorder's white clip: a soft shoulder above the knee
        float3 over = max(c - _Knee, 0.0);
        c = min(c, _Knee) + (1.0 - _Knee) * (1.0 - exp(-over / (1.0 - _Knee)));
        // the grade the game already had, in its original order
        c = saturate((c + _PreOffset - 0.5) * _Contrast + 0.5);
        c = lerp(lum(c), c, _GradeSat);
        c = pow(c, 1.0 / _Gamma);
        c += _Bright;
        float2 cen = i.uv - 0.5;
        float dist = length(cen * float2(_VigRound, 1.0));
        float vig = _Vig > 0.001 ? pow(1.0 - smoothstep(0.0, _Vig * 0.7, dist), _VigSoft) : 1.0;
        c *= vig;
        c = max(c - _BlackPoint, 0.0);
        // night gain: lifts only the darks, the way a camcorder's gain-up does (black stays black)
        float gk = max(1.0 - lum(c) / 0.4, 0.0);
        c *= 1.0 + _NightGain * gk * gk;
        return half4(c, 1.0);
    }

    // ---------------------------------------------------------------- 4 Tape
    float4 _SigSize;                 // w, h, 1/w, 1/h
    float _Frame, _TimeS;
    float _LumaSigma, _Sharp, _ChromaSigma, _ChromaDelay, _ChromaVert;
    float _LumaNoise, _LineNoise, _ChromaNoise, _GrainLen;
    float _Wobble, _Jitter, _HeadLines, _HeadShift;
    float _Dropout, _Tracking, _TrackPos, _Glitch;
    float _ChromaLag, _LumaLag;
    float _Sat, _Hue, _Lift;

    half4 fragTape(v2f i) : SV_Target
    {
        float W = _SigSize.x, H = _SigSize.y;
        float2 px = _SigSize.zw;
        float V = H / 480.0;
        float U = V;                                        // pixel distances follow the line count, so 4:3 and ultrawide get the same softness
        float ln = H - 1.0 - floor(i.uv.y * H);             // 0 = top line
        float fr = _Frame;

        // time-base error: each line lands a little left or right
        float wob = sin(ln / V * 0.021 + _TimeS * 1.3) * 0.6 + sin(ln / V * 0.0063 - _TimeS * 0.71) * 0.4;
        float jit = h3(ln, fr, 11.0) * 2.0 - 1.0;
        float dx = (wob * _Wobble + jit * _Jitter) * U;

        // head switching: the last few lines tear sideways
        float hl = _HeadLines * V;
        float hz = saturate((ln - (H - hl)) / max(hl, 1.0));
        float inHead = step(H - hl, ln) * step(0.5, _HeadLines);
        float hsN = h3(ln, fr, 23.0);
        dx += inHead * _HeadShift * U * (0.3 + 0.7 * hz) * (0.7 + 0.6 * hsN);

        // tracking band (wear): a strip of torn, snowy lines
        float tb = saturate(1.0 - abs(i.uv.y - _TrackPos) / 0.05) * _Tracking;
        float tN = h3(ln, fr, 37.0);
        dx += tb * (tN - 0.35) * 46.0 * U;

        // torn blocks (sanity and kill hooks)
        float blk = floor(ln / (14.0 * V));
        float gOn = step(1.0 - _Glitch * 0.4, h3(blk, floor(_TimeS * 8.0), 53.0)) * step(0.001, _Glitch);
        dx += gOn * (h3(blk, fr, 59.0) - 0.5) * 70.0 * U * _Glitch;

        float2 uv = float2(i.uv.x - dx * px.x, i.uv.y);

        // luma: low-pass with peaking (the bright ghost edge of video sharpening)
        float s0 = max(_LumaSigma * U, 0.3), s1 = s0 * 2.6;
        float y0 = 0.0, y1 = 0.0, w0 = 0.0, w1 = 0.0;
        for (int k = -6; k <= 6; k++)
        {
            float fk = k;
            float l = lum(S(_MainTex, uv + float2(fk * px.x, 0.0)));
            float a = exp(-fk * fk / (2.0 * s0 * s0)), b = exp(-fk * fk / (2.0 * s1 * s1));
            y0 += l * a; w0 += a; y1 += l * b; w1 += b;
        }
        y0 /= w0; y1 /= w1;
        float Y = y0 + (y0 - y1) * _Sharp;

        // chroma: far less bandwidth, arrives late, smears a line down
        float cs = max(_ChromaSigma * U, 0.5);
        float stp = cs * 0.5;
        float2 cuv = uv + float2(-_ChromaDelay * U * px.x, _ChromaVert * px.y);
        float2 iq = 0.0;
        float wc = 0.0;
        for (int m = -6; m <= 6; m++)
        {
            float fm = m;
            float3 c = S(_MainTex, cuv + float2(fm * stp * px.x, 0.0));
            float w = exp(-fm * fm * 0.125);
            iq += rgb2yiq(c).yz * w; wc += w;
        }
        iq /= wc;

        // black pedestal, before the noise rides on it
        Y = Y * (1.0 - _Lift) + _Lift;

        // the previous field leaks into this one
        float3 pv = rgb2yiq(S(_PrevTex, i.uv));
        iq = lerp(iq, pv.yz, _ChromaLag);
        Y = lerp(Y, pv.x, _LumaLag);

        // noise: streaky luma grain, per-line offsets, slow colour blotches; all stronger in the dark
        float dark = 1.0 - saturate(Y * 2.5);
        float gx = uv.x * W / max(_GrainLen * U, 0.5);
        float f = frac(gx); f = f * f * (3.0 - 2.0 * f);
        float grain = lerp(h3(floor(gx), ln, fr), h3(floor(gx) + 1.0, ln, fr), f) - 0.5;
        Y += grain * _LumaNoise * (0.55 + 0.9 * dark) + (h3(ln, fr, 71.0) - 0.5) * _LineNoise;
        float cx = uv.x * W / (22.0 * U);
        float cl = floor(ln / (2.0 * V));
        float fc = frac(cx); fc = fc * fc * (3.0 - 2.0 * fc);
        float2 c0 = float2(h3(floor(cx), cl, fr + 1000.0), h3(floor(cx), cl, fr + 2000.0));
        float2 c1 = float2(h3(floor(cx) + 1.0, cl, fr + 1000.0), h3(floor(cx) + 1.0, cl, fr + 2000.0));
        // colour noise needs some light to ride on: keeps true black neutral
        iq += (lerp(c0, c1, fc) - 0.5) * min(_ChromaNoise * (0.5 + dark), max(Y, 0.0) * 1.2);

        // dropouts: short white comets on single lines
        float dOn = step(1.0 - _Dropout, h3(ln, fr, 83.0)) * step(1e-7, _Dropout);
        float dd = (uv.x - h3(ln, fr, 89.0)) / (0.015 + 0.07 * h3(ln, fr, 97.0));
        float comet = dOn * step(0.0, dd) * step(dd, 1.0) * pow(1.0 - saturate(dd), 1.5);
        Y += comet * 0.85; iq *= 1.0 - comet;

        // snow inside the tracking band, noise in the head-switch lines
        float snow = step(0.7, h3(floor(uv.x * W / (3.0 * U)), ln, fr + 300.0));
        Y = lerp(Y, max(Y, snow * 0.62), tb * step(0.3, tN));
        iq *= 1.0 - 0.8 * tb;
        Y += inHead * (hsN - 0.5) * 0.22; iq *= 1.0 - 0.6 * inHead;

        iq *= _Sat;
        float ch = cos(_Hue), sh = sin(_Hue);
        iq = float2(iq.x * ch - iq.y * sh, iq.x * sh + iq.y * ch);
        return half4(saturate(yiq2rgb(float3(Y, iq))), 1.0);
    }

    // ---------------------------------------------------------------- 5 Show
    float _Beam, _FieldOfs;

    half4 fragShow(v2f i) : SV_Target
    {
        float H = _SigSize.y;
        float y = i.uv.y * H - 0.5 + _FieldOfs;
        float fy = floor(y);
        float3 acc = 0.0;
        for (int k = -1; k <= 2; k++)
        {
            float ly = fy + k;
            float d = y - ly;
            acc += S(_MainTex, float2(i.uv.x, (ly + 0.5) / H)) * exp(-d * d / (2.0 * _Beam * _Beam));
        }
        return half4(saturate(acc / (_Beam * 2.5066283)), 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "DOWN"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG
        }
        Pass
        {
            Name "BRIGHT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBright
            ENDCG
        }
        Pass
        {
            Name "BLUR"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
        Pass
        {
            Name "COMP"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComp
            ENDCG
        }
        Pass
        {
            Name "TAPE"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragTape
            ENDCG
        }
        Pass
        {
            Name "SHOW"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShow
            ENDCG
        }
    }

    Fallback Off
}
