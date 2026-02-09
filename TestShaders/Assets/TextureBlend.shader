 Shader "Custom/TextureBlend"
  {
      Properties
      {
          _TexA ("Texture A (Cobblestone)", 2D) = "white" {}
          _TexB ("Texture B (Grass/Leaves)", 2D) = "white" {}
          _Mask ("Blend Mask", 2D) = "white" {}
          _TilingA ("Tiling A", Vector) = (1,1,0,0)
          _TilingB ("Tiling B", Vector) = (1,1,0,0)
          _EdgeFade ("Edge Fade Width", Range(0, 0.5)) = 0.1
      }
      SubShader
      {
          Tags { "Queue"="Transparent" "RenderType"="Transparent" }
          ZWrite Off
          Blend SrcAlpha OneMinusSrcAlpha

          CGPROGRAM
          #pragma surface surf Lambert alpha:fade

          sampler2D _TexA;
          sampler2D _TexB;
          sampler2D _Mask;
          float4 _TilingA;
          float4 _TilingB;
          float _EdgeFade;

          struct Input
          {
              float2 uv_Mask;
          };

          void surf (Input IN, inout SurfaceOutput o)
          {
              float2 uvA = IN.uv_Mask * _TilingA.xy;
              float2 uvB = IN.uv_Mask * _TilingB.xy;

              fixed4 colA = tex2D(_TexA, uvA);
              fixed4 colB = tex2D(_TexB, uvB);
              fixed  mask = tex2D(_Mask, IN.uv_Mask).r;

              // Fade out at edges of the quad
              float2 d = abs(IN.uv_Mask - 0.5) * 2; // 0 at center, 1 at edge
              float edge = smoothstep(1, 1 - _EdgeFade * 2, max(d.x, d.y));

              o.Albedo = lerp(colB, colA, mask);
              o.Alpha = edge;
          }
          ENDCG
      }
      FallBack "Diffuse"
  }