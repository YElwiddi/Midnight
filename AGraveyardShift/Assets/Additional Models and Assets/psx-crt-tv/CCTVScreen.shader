Shader "CCTV/ScreenScanlines"
{
    // Unlit screen shader for the CCTV monitor (built-in pipeline) that shows the
    // feed texture and adds horizontal CRT scanlines on top.
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ScanlineCount ("Scanline Count", Float) = 130
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.4
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

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                // Horizontal scanlines: darken alternating rows across screen height.
                float band = 0.5 + 0.5 * sin(i.uv.y * _ScanlineCount * 6.2831853);
                col.rgb *= (1.0 - _ScanlineStrength * band);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
