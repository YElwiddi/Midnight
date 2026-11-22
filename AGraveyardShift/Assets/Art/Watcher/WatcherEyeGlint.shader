// One eye of a watcher catching the light (driven by WatcherEyes.cs, which places and sizes the quad for each
// camera and sets _Intensity per renderer). A soft-edged additive dot. It ignores fog on purpose: light sent
// straight back at its source is the last thing the fog takes.
Shader "Custom/WatcherEyeGlint"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.93, 0.74, 1)
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // flat bright core, quick falloff: a small dot must keep its brightness when the picture is coarse
                float a = saturate((1 - length(i.uv)) * 3);
                return fixed4(_Color.rgb * (_Intensity * a), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
