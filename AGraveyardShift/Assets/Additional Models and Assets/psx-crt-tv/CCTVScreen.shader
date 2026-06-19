Shader "CCTV/ScreenScanlines"
{
    // Unlit screen shader for the CCTV monitor (built-in pipeline). Normally it just
    // shows the feed texture with horizontal CRT scanlines. When _GlitchAmount is
    // driven up (the low-sanity / low-protection scare) it heavily corrupts the image
    // with per-row jitter, RGB split, horizontal tearing and animated static.
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ScanlineCount ("Scanline Count", Float) = 130
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.4
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _StaticStrength ("Static Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Lighting Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ScanlineCount;
            float _ScanlineStrength;
            float _GlitchAmount;
            float _StaticStrength;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float g = saturate(_GlitchAmount);
                float t = _Time.y;
                float2 uv = i.uv;

                // Keeps the glitch offsets visually consistent when _MainTex is zoomed
                // (1 normally; < 1 when the spirit is zoomed in on her face).
                float zoomComp = _MainTex_ST.x;

                // Per-row horizontal jitter (quantised to scanline rows, re-rolled fast).
                float row = floor(uv.y * _ScanlineCount);
                float jitter = (hash21(float2(row, floor(t * 22.0))) - 0.5) * 0.05 * g * zoomComp;
                uv.x += jitter;

                // Occasional larger horizontal tear across a band of the screen.
                float tear = hash21(float2(floor(t * 14.0), 7.3));
                float band = step(0.7, frac(uv.y * 2.3 + t * 1.7));
                uv.x += (tear > 0.82) ? band * 0.04 * g * zoomComp : 0.0;

                // RGB split grows with glitch.
                float split = 0.012 * g * zoomComp;
                fixed4 col;
                col.r = tex2D(_MainTex, uv + float2(split, 0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2(split, 0)).b;
                col.a = 1.0;
                col *= _Color;

                // Animated white-noise static mixed in (kept moderate so the figure reads).
                float n = hash21(uv * float2(640.0, 480.0) + t * 91.0);
                col.rgb = lerp(col.rgb, float3(n, n, n), g * 0.30 * _StaticStrength);

                // Scanlines (kept at the baseline strength during the glitch so they
                // don't crush the figure into dark bands).
                float scan = 0.5 + 0.5 * sin(uv.y * _ScanlineCount * 6.2831853);
                col.rgb *= (1.0 - _ScanlineStrength * scan);

                return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
