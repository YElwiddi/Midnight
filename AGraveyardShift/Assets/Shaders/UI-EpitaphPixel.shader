// UI image shader drawn on the menu's low-resolution pixel grid (EpitaphPixel.cginc): each virtual
// pixel shows the texture averaged over its whole area (4x4 taps), like the art was downscaled and
// blown back up with nearest-neighbour. Alpha can be stepped and dithered for a cruder look.
Shader "UI/Epitaph Pixel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelLines ("Pixel Lines (virtual vertical resolution, 0 = off)", Float) = 360
        _PixelAlphaSteps ("Alpha Steps (0 = smooth)", Float) = 0
        _PixelAlphaDither ("Alpha Dither", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PixelLines;
            float _PixelAlphaSteps;
            float _PixelAlphaDither;

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
                float cell = EpitaphPixelCell(_PixelLines);
                float2 delta = EpitaphPixelDelta(i.vertex.xy, cell);
                float2 dx = ddx(i.uv);
                float2 dy = ddy(i.uv);
                float2 uv = i.uv + dx * delta.x + dy * delta.y;

                half4 c = (EpitaphPixelArea(_MainTex, uv, dx, dy, cell) + _TextureSampleAdd) * i.color;
                c.a = EpitaphPixelSteps(c.a, _PixelAlphaSteps, _PixelAlphaDither, i.vertex.xy, cell);

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
