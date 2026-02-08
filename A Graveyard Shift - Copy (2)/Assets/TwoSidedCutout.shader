  Shader "Custom/TwoSidedCutout"
  {
      Properties
      {
          _MainTex ("Texture", 2D) = "white" {}
          _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
      }
      SubShader
      {
          Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
          LOD 200
          Cull Off

          CGPROGRAM
          #pragma surface surf Standard alphatest:_Cutoff fullforwardshadows addshadow
          #pragma target 3.0

          sampler2D _MainTex;

          struct Input
          {
              float2 uv_MainTex;
          };

          void surf (Input IN, inout SurfaceOutputStandard o)
          {
              fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
              o.Albedo = c.rgb;
              o.Alpha = c.a;
          }
          ENDCG
      }
      FallBack "Transparent/Cutout/Diffuse"
  }