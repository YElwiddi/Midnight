// The Spirit's painted mouth (PaintedMaw.cs): a small texture repainted every frame, laid over her mask.
// Lit like her face is (diffuse only, every light that reaches her), blended on by the paint's own alpha so its
// edge is as soft as her painted lips, and able to light itself for teeth in the dark. Drawn from either side:
// the sheet carries its own normals. A shader of its own, with no keywords, so a build cannot strip the variant
// it needs (a Standard material made at run time could lose its Fade variant).
Shader "Graveyard/Painted Maw"
{
    Properties
    {
        _MainTex ("Paint", 2D) = "black" {}
        // Her face is Standard, non-metallic, which in this project's gamma colour space keeps 0.78 of its albedo as
        // diffuse. The same here, so the paint answers the light the way her skin does.
        _Diffuse ("Diffuse", Range(0, 1.5)) = 0.78
        _Emission ("Self-lit", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noshadow nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 2.0

        sampler2D _MainTex;
        half _Diffuse;
        half _Emission;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb * _Diffuse;
            o.Alpha = c.a;
            o.Emission = c.rgb * _Emission;
        }
        ENDCG
    }
    FallBack Off
}
