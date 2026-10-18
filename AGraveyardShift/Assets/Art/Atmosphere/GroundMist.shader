// Ground mist particles (NightAtmosphere). Alpha-blended and fogged, softened where they meet
// geometry (soft particles), and faded out close to the camera so walking through the mist
// never pops a billboard across the screen. _Opacity is driven per renderer (dawn fade).
Shader "Custom/GroundMist"
{
    Properties
    {
        _MainTex ("Puff", 2D) = "white" {}
        _Color ("Tint", Color) = (0.6, 0.4, 0.38, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _CameraFadeStart ("Camera Fade Start (m)", Float) = 1.0
        _CameraFadeEnd ("Camera Fade End (m)", Float) = 6.0
        _InvFade ("Soft Particles Factor", Range(0.01, 3.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Opacity;
            float _CameraFadeStart;
            float _CameraFadeEnd;
            float _InvFade;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 uvDepth : TEXCOORD0; // xy = uv, z = eye depth
                UNITY_FOG_COORDS(1)
                #ifdef SOFTPARTICLES_ON
                float4 projPos : TEXCOORD2;
                #endif
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uvDepth.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvDepth.z = -UnityObjectToViewPos(v.vertex).z;
                #ifdef SOFTPARTICLES_ON
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                #endif
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = _Color * i.color;
                col.a *= tex2D(_MainTex, i.uvDepth.xy).a * _Opacity;
                col.a *= saturate((i.uvDepth.z - _CameraFadeStart) / max(_CameraFadeEnd - _CameraFadeStart, 0.01));
                #ifdef SOFTPARTICLES_ON
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                col.a *= saturate(_InvFade * (sceneZ - i.projPos.z));
                #endif
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
