// TextMeshPro's Mobile Distance Field shader drawn on a low-resolution pixel grid (EpitaphPixel.cginc):
// each glyph is sampled once per virtual pixel, so menu text reads like a bitmap font at any size.
// _PixelLines is the virtual vertical resolution, _PixelHard picks crisp on/off pixels over one
// virtual pixel of anti-aliasing, and the underlay (drop shadow) can be stepped and dithered.
// Same properties and keywords as the stock shader, so TMP's padding and font swaps keep working.

Shader "TextMeshPro/Mobile/Distance Field Pixel" {

Properties {
	_FaceColor          ("Face Color", Color) = (1,1,1,1)
	_FaceDilate			("Face Dilate", Range(-1,1)) = 0

	_OutlineColor	    ("Outline Color", Color) = (0,0,0,1)
	_OutlineWidth		("Outline Thickness", Range(0,1)) = 0
	_OutlineSoftness	("Outline Softness", Range(0,1)) = 0

	_UnderlayColor	    ("Border Color", Color) = (0,0,0,.5)
	_UnderlayOffsetX 	("Border OffsetX", Range(-1,1)) = 0
	_UnderlayOffsetY 	("Border OffsetY", Range(-1,1)) = 0
	_UnderlayDilate		("Border Dilate", Range(-1,1)) = 0
	_UnderlaySoftness 	("Border Softness", Range(0,1)) = 0

	_WeightNormal		("Weight Normal", float) = 0
	_WeightBold			("Weight Bold", float) = .5

	_ShaderFlags		("Flags", float) = 0
	_ScaleRatioA		("Scale RatioA", float) = 1
	_ScaleRatioB		("Scale RatioB", float) = 1
	_ScaleRatioC		("Scale RatioC", float) = 1

	_MainTex			("Font Atlas", 2D) = "white" {}
	_TextureWidth		("Texture Width", float) = 512
	_TextureHeight		("Texture Height", float) = 512
	_GradientScale		("Gradient Scale", float) = 5
	_ScaleX				("Scale X", float) = 1
	_ScaleY				("Scale Y", float) = 1
	_PerspectiveFilter	("Perspective Correction", Range(0, 1)) = 0.875
	_Sharpness			("Sharpness", Range(-1,1)) = 0

	_VertexOffsetX		("Vertex OffsetX", float) = 0
	_VertexOffsetY		("Vertex OffsetY", float) = 0

	_ClipRect			("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
	_MaskSoftnessX		("Mask SoftnessX", float) = 0
	_MaskSoftnessY		("Mask SoftnessY", float) = 0

	_StencilComp		("Stencil Comparison", Float) = 8
	_Stencil			("Stencil ID", Float) = 0
	_StencilOp			("Stencil Operation", Float) = 0
	_StencilWriteMask	("Stencil Write Mask", Float) = 255
	_StencilReadMask	("Stencil Read Mask", Float) = 255

	_CullMode			("Cull Mode", Float) = 0
	_ColorMask			("Color Mask", Float) = 15

	_PixelLines			("Pixel Lines (virtual vertical resolution, 0 = off)", Float) = 360
	_PixelHard			("Pixel Hard Edges", Range(0,1)) = 1
	_PixelThreshold		("Pixel On Threshold (coverage)", Range(0.05,0.95)) = 0.25
	_PixelMinEm			("Min Virtual Pixels Per Em (0 = off)", Float) = 12
	_PixelEmTexels		("Atlas Sampling Point Size", Float) = 90
	_PixelShadowSteps	("Shadow Steps (0 = smooth)", Float) = 0
	_PixelShadowDither	("Shadow Dither", Range(0,1)) = 0
}

SubShader {
	Tags
	{
		"Queue"="Transparent"
		"IgnoreProjector"="True"
		"RenderType"="Transparent"
	}


	Stencil
	{
		Ref [_Stencil]
		Comp [_StencilComp]
		Pass [_StencilOp]
		ReadMask [_StencilReadMask]
		WriteMask [_StencilWriteMask]
	}

	Cull [_CullMode]
	ZWrite Off
	Lighting Off
	Fog { Mode Off }
	ZTest [unity_GUIZTestMode]
	Blend One OneMinusSrcAlpha
	ColorMask [_ColorMask]

	Pass {
		CGPROGRAM
		#pragma vertex VertShader
		#pragma fragment PixShader
		#pragma target 3.0
		// multi_compile, not shader_feature: this shader's materials are only ever created at runtime
		// (PixelCanvas), so a build would strip every outline/underlay variant no asset uses.
		#pragma multi_compile __ OUTLINE_ON
		#pragma multi_compile __ UNDERLAY_ON UNDERLAY_INNER

		#pragma multi_compile __ UNITY_UI_CLIP_RECT
		#pragma multi_compile __ UNITY_UI_ALPHACLIP

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"
		#include "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc"
		#include "EpitaphPixel.cginc"

		float _PixelLines;
		float _PixelHard;
		float _PixelThreshold;
		float _PixelMinEm;
		float _PixelEmTexels;
		float _PixelShadowSteps;
		float _PixelShadowDither;

		struct vertex_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			float4	vertex			: POSITION;
			float3	normal			: NORMAL;
			fixed4	color			: COLOR;
			float4	texcoord0		: TEXCOORD0;
			float2	texcoord1		: TEXCOORD1;
		};

		struct pixel_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
			float4	vertex			: SV_POSITION;
			fixed4	faceColor		: COLOR;
			fixed4	outlineColor	: COLOR1;
			float4	texcoord0		: TEXCOORD0;			// Texture UV, Mask UV
			half4	param			: TEXCOORD1;			// Scale(x), BiasIn(y), BiasOut(z), Bias(w)
			half4	mask			: TEXCOORD2;			// Position in clip space(xy), Softness(zw)
			#if (UNDERLAY_ON | UNDERLAY_INNER)
			float4	texcoord1		: TEXCOORD3;			// Texture UV, alpha, reserved
			half2	underlayParam	: TEXCOORD4;			// Scale(x), Bias(y)
			#endif
		};

		float _UIMaskSoftnessX;
		float _UIMaskSoftnessY;
		int _UIVertexColorAlwaysGammaSpace;

		pixel_t VertShader(vertex_t input)
		{
			pixel_t output;

			UNITY_INITIALIZE_OUTPUT(pixel_t, output);
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			float bold = step(input.texcoord0.w, 0);

			float4 vert = input.vertex;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;
			float4 vPosition = UnityObjectToClipPos(vert);

			float2 pixelSize = vPosition.w;
			pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float scale = rsqrt(dot(pixelSize, pixelSize));
			scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
			if(UNITY_MATRIX_P[3][3] == 0) scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(UnityObjectToWorldNormal(input.normal.xyz), normalize(WorldSpaceViewDir(vert)))));

			float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
			weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;

			float layerScale = scale;

			scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
			float bias = (0.5 - weight) * scale - 0.5;
			float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;

			if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
			{
				input.color.rgb = UIGammaToLinear(input.color.rgb);
			}
			float opacity = input.color.a;
			#if (UNDERLAY_ON | UNDERLAY_INNER)
			opacity = 1.0;
			#endif

			fixed4 faceColor = fixed4(input.color.rgb, opacity) * _FaceColor;
			faceColor.rgb *= faceColor.a;

			fixed4 outlineColor = _OutlineColor;
			outlineColor.a *= opacity;
			outlineColor.rgb *= outlineColor.a;
			outlineColor = lerp(faceColor, outlineColor, sqrt(min(1.0, (outline * 2))));

			#if (UNDERLAY_ON | UNDERLAY_INNER)
			layerScale /= 1 + ((_UnderlaySoftness * _ScaleRatioC) * layerScale);
			float layerBias = (.5 - weight) * layerScale - .5 - ((_UnderlayDilate * _ScaleRatioC) * .5 * layerScale);

			float x = -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth;
			float y = -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight;
			float2 layerOffset = float2(x, y);
			#endif

			// Generate UV for the Masking Texture
			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			float2 maskUV = (vert.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);

			// Populate structure for pixel shader
			output.vertex = vPosition;
			output.faceColor = faceColor;
			output.outlineColor = outlineColor;
			output.texcoord0 = float4(input.texcoord0.x, input.texcoord0.y, maskUV.x, maskUV.y);
			output.param = half4(scale, bias - outline, bias + outline, bias);

			const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
			output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));
			#if (UNDERLAY_ON || UNDERLAY_INNER)
			output.texcoord1 = float4(input.texcoord0 + layerOffset, input.color.a, 0);
			output.underlayParam = half2(layerScale, layerBias);
			#endif

			return output;
		}


		// PIXEL SHADER
		fixed4 PixShader(pixel_t input) : SV_Target
		{
			UNITY_SETUP_INSTANCE_ID(input);

			// How much of this virtual pixel the glyph covers (4x4 taps across it), like the text was
			// rendered large and shrunk. Soft keeps that coverage as anti-aliasing; hard turns the pixel
			// fully on past _PixelThreshold, which keeps hairlines thinner than a pixel from breaking up.
			float2 du = ddx(input.texcoord0.xy);
			float2 dv = ddy(input.texcoord0.xy);
			// Small text gets a finer grid so it never drops below _PixelMinEm virtual pixels per em
			// (its em on screen = the atlas' sampling size over the atlas texels per screen pixel).
			float cell = EpitaphPixelCell(_PixelLines);
			float emPx = _PixelEmTexels / max(length(du * float2(_TextureWidth, _TextureHeight)), 1e-5);
			if (_PixelMinEm > 0) cell = min(cell, max(1.0, floor(emPx / _PixelMinEm + 0.5)));
			float2 delta = EpitaphPixelDelta(input.vertex.xy, cell);
			float2 center = input.texcoord0.xy + du * delta.x + dv * delta.y;
			float scale = input.param.x;
			float edge = (input.param.w + 0.5) / scale;
			#ifdef OUTLINE_ON
			float ow = (input.param.z - input.param.w) / scale;
			#else
			float ow = 0;
			#endif

			float faceCov = 0;
			float fullCov = 0;
			for (int ty = 0; ty < 4; ty++)
			{
				for (int tx = 0; tx < 4; tx++)
				{
					float2 o = (float2(tx, ty) - 1.5) * (cell / 4.0);
					float sd = tex2Dlod(_MainTex, float4(center + du * o.x + dv * o.y, 0, 0)).a;
					faceCov += saturate((sd - edge - ow) * scale + 0.5);
					fullCov += saturate((sd - edge + ow) * scale + 0.5);
				}
			}
			faceCov = lerp(faceCov / 16.0, step(_PixelThreshold, faceCov / 16.0), _PixelHard);
			fullCov = lerp(fullCov / 16.0, step(_PixelThreshold, fullCov / 16.0), _PixelHard);

			half4 c = input.faceColor * faceCov;
			#ifdef OUTLINE_ON
			c = lerp(input.outlineColor, input.faceColor, faceCov) * fullCov;
			#endif

			#if (UNDERLAY_ON | UNDERLAY_INNER)
			float ud = tex2Dlod(_MainTex, float4(input.texcoord1.xy + du * delta.x + dv * delta.y, 0, 0)).a * input.underlayParam.x;
			#endif

			#if UNDERLAY_ON
			float u = EpitaphPixelSteps(saturate(ud - input.underlayParam.y), _PixelShadowSteps, _PixelShadowDither, input.vertex.xy, cell);
			c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * u * (1 - c.a);
			#endif

			#if UNDERLAY_INNER
			float u = EpitaphPixelSteps(1 - saturate(ud - input.underlayParam.y), _PixelShadowSteps, _PixelShadowDither, input.vertex.xy, cell);
			c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * u * faceCov * (1 - c.a);
			#endif

			// Alternative implementation to UnityGet2DClipping with support for softness.
			#if UNITY_UI_CLIP_RECT
			half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
			c *= m.x * m.y;
			#endif

			#if (UNDERLAY_ON | UNDERLAY_INNER)
			c *= input.texcoord1.z;
			#endif

			#if UNITY_UI_ALPHACLIP
			clip(c.a - 0.001);
			#endif

			return c;
		}
		ENDCG
	}
}

CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
}
