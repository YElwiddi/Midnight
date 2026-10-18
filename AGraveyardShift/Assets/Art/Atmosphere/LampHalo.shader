// Camera-facing additive glow around a lamp head (driven by LampHalo.cs).
// The quad is expanded in view space so it faces every camera (player, CCTV feeds) and is
// pulled slightly toward the viewer so the lamp's own cage doesn't cut into it. Like any
// additive effect it fades out with distance fog.
Shader "Custom/LampHalo"
{
    Properties
    {
        _MainTex ("Glow (R outer, G core)", 2D) = "black" {}
        [HDR] _OuterColor ("Outer Glow", Color) = (2.2, 1.35, 0.57, 1)
        [HDR] _CoreColor ("Core", Color) = (2.0, 1.45, 1.13, 1)
        _Size ("Size (m)", Float) = 3.2
        _PullToCamera ("Pull Toward Camera (m)", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _OuterColor;
            half4 _CoreColor;
            float _Size;
            float _PullToCamera;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                // Object origin in view space, moved toward the camera (never past it).
                float3 center = UnityObjectToViewPos(float3(0, 0, 0));
                float dist = length(center);
                center *= max(dist - _PullToCamera, 0.05) / max(dist, 1e-4);
                float3 viewPos = center + float3(v.vertex.xy * _Size, 0);
                o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1));
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half2 g = tex2D(_MainTex, i.uv).rg;
                half4 col = half4(_OuterColor.rgb * g.r + _CoreColor.rgb * g.g, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
