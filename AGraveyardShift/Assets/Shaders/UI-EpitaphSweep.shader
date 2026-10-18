// UI image shader with a moving light band, masked by the image's own alpha.
// Used by the splash: a slow light crosses the carved Epitaph logo (_SweepPos animates 0 -> 1).
// With _PixelLines set (PixelCanvas does it on the menu) the logo and band sit on the menu's pixel grid.
Shader "UI/Epitaph Sweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SweepColor ("Sweep Color", Color) = (1, 0.93, 0.8, 1)
        _SweepPos ("Sweep Position (0-1)", Float) = -1
        _SweepWidth ("Sweep Width", Float) = 0.12
        _SweepSlant ("Sweep Slant", Float) = 0.35
        _SweepStrength ("Sweep Strength", Float) = 0.9
        _PixelLines ("Pixel Lines (virtual vertical resolution, 0 = smooth)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "EpitaphPixel.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _SweepColor;
            float _SweepPos, _SweepWidth, _SweepSlant, _SweepStrength;
            float _PixelLines;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 dx = ddx(i.uv);
                float2 dy = ddy(i.uv);
                fixed4 c;
                if (_PixelLines > 0)
                {
                    float cell = EpitaphPixelCell(_PixelLines);
                    float2 delta = EpitaphPixelDelta(i.vertex.xy, cell);
                    uv += dx * delta.x + dy * delta.y;
                    c = EpitaphPixelArea(_MainTex, uv, dx, dy, cell) * i.color;
                }
                else
                {
                    c = tex2Dgrad(_MainTex, uv, dx, dy) * i.color;
                }
                // diagonal band across the image, soft at both edges
                float x = uv.x + (uv.y - 0.5) * _SweepSlant;
                float d = abs(x - _SweepPos) / max(_SweepWidth, 0.0001);
                float band = saturate(1.0 - d);
                band = band * band * (3.0 - 2.0 * band);
                c.rgb = lerp(c.rgb, _SweepColor.rgb, band * _SweepStrength * _SweepColor.a);
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return c;
            }
            ENDCG
        }
    }
}
