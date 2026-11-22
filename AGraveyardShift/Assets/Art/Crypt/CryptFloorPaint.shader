// Paint on the crypt floor (the red hint arrows): a lit, alpha-tested decal quad lying on the floor.
// Lambert-lit so the flashlight and lanterns catch it, plus a faint self-glow so it still reads in the
// dark (the Beast kills the flashlight). Depth is pulled slightly toward the camera so it never
// z-fights the floor it sits on.
Shader "Custom/CryptFloorPaint"
{
    Properties
    {
        _MainTex ("Paint (RGB colour, A coverage)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Glow ("Glow In The Dark", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Offset -1, -1

        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff exclude_path:deferred exclude_path:prepass nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glow;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Emission = c.rgb * _Glow;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
