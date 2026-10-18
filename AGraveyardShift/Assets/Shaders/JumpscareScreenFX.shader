// Full-screen jumpscare effects, applied after the camera's VHS/PSX image effects.
// Driven per frame by JumpscareScreenFX.cs (flash, invert, zoom punch, vertical roll, tear, RGB split,
// CRT power-off, freeze-frame, subliminal image, dirt landing on the lens, blur, eyelids).
Shader "Hidden/JumpscareScreenFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _HoldTex;
            float _Hold;
            sampler2D _OverlayTex;
            float4 _OverlayRect;   // xy = screen uv of the image's lower-left corner, zw = its size in screen uv
            float _OverlayAlpha;
            float _OverlayInvert;
            sampler2D _DirtTex;    // rgb = dirt colour, a = landing order (0 lands first, 1 last)
            float _Dirt;
            float4 _DirtTint;
            float4 _PixelGrid;     // xy = the game's pixel grid (PSX 320x240) for overlays, 0 = off
            float4 _Flash;         // rgb = colour, a = amount
            float _Invert;
            float _Zoom;
            float4 _ZoomCenter;
            float _Roll;
            float _Collapse;
            float _Split;
            float _Tear;
            float _Seed;
            float _Desat;
            float _Darken;
            float _Lids;
            float _Blur;

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float3 src(float2 uv)
            {
                return lerp(tex2D(_MainTex, uv).rgb, tex2D(_HoldTex, uv).rgb, _Hold);
            }

            float3 srcSplit(float2 uv)
            {
                if (_Split <= 0.0001) return src(uv);
                return float3(src(uv + float2(_Split, 0)).r, src(uv).g, src(uv - float2(_Split, 0)).b);
            }

            float3 srcBlur(float2 uv)
            {
                if (_Blur <= 0.0001) return srcSplit(uv);
                float3 c = srcSplit(uv) * 2.0;
                c += srcSplit(uv + float2( _Blur, 0));
                c += srcSplit(uv + float2(-_Blur, 0));
                c += srcSplit(uv + float2(0,  _Blur * 1.7));
                c += srcSplit(uv + float2(0, -_Blur * 1.7));
                c += srcSplit(uv + float2( _Blur,  _Blur) * 0.7);
                c += srcSplit(uv + float2(-_Blur, -_Blur) * 0.7);
                return c / 8.0;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // CRT power-off: the picture squashes to a bright line, then the line shrinks to a dot.
                float boost = 1.0;
                if (_Collapse > 0.0001)
                {
                    float a = saturate(_Collapse / 0.6);
                    float b = saturate((_Collapse - 0.6) / 0.4);
                    float sy = lerp(1.0, 0.006, a * a);
                    float sx = lerp(1.0, 0.004, b * b);
                    float2 c = uv - 0.5;
                    c.y /= sy; c.x /= sx;
                    if (abs(c.x) > 0.5 || abs(c.y) > 0.5) return fixed4(0, 0, 0, 1);
                    uv = c + 0.5;
                    boost = 1.0 + a * 3.0 + b * 3.0;
                }

                uv = (uv - _ZoomCenter.xy) / max(_Zoom, 0.01) + _ZoomCenter.xy;

                // Vertical hold slip: the frame scrolls, with the black blanking bar at the seam.
                float bar = 1.0;
                if (abs(_Roll) > 0.0001)
                {
                    float y = frac(uv.y + _Roll);
                    bar = smoothstep(0.0, 0.03, y) * smoothstep(1.0, 0.97, y);
                    uv.y = y;
                }

                if (_Tear > 0.0001)
                {
                    float row = floor(uv.y * 120.0);
                    float band = step(0.75, hash(float2(floor(uv.y * 7.0), _Seed + 3.1)));
                    uv.x += (hash(float2(row, _Seed)) - 0.5) * _Tear * (0.2 + band * 1.6);
                }

                float3 col = srcBlur(uv) * bar;
                float2 guv = _PixelGrid.x > 0 ? (floor(i.uv * _PixelGrid.xy) + 0.5) / _PixelGrid.xy : i.uv;

                if (_OverlayAlpha > 0.0001)
                {
                    float2 ouv = (guv - _OverlayRect.xy) / _OverlayRect.zw;
                    float3 o = 0;
                    if (ouv.x >= 0 && ouv.x <= 1 && ouv.y >= 0 && ouv.y <= 1)
                    {
                        o = tex2D(_OverlayTex, ouv).rgb;
                        o = lerp(o, 1.0 - o, _OverlayInvert);
                    }
                    col = lerp(col, o, _OverlayAlpha);
                }

                if (_Dirt > 0.0001)
                {
                    float4 d = tex2D(_DirtTex, guv);
                    float cover = saturate((_Dirt * 1.12 - d.a) * 14.0);
                    col = lerp(col, d.rgb * _DirtTint.rgb, cover);
                }

                col = lerp(col, 1.0 - col, _Invert);
                float l = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, l.xxx, _Desat);
                col *= boost;
                col = lerp(col, _Flash.rgb, _Flash.a);

                if (_Lids > 0.0001)
                {
                    float edge = abs(i.uv.y - 0.5) * 2.0;
                    float open = 1.0 - _Lids;
                    col *= 1.0 - smoothstep(open - 0.25, open, edge + _Lids * 0.25);
                }
                col *= 1.0 - _Darken;
                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
