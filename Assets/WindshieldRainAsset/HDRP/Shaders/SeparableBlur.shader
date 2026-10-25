/*
Based on: https://github.com/andydbc/unity-frosted-glass

MIT License

Copyright (c) 2018 Andy Duboc

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// Blur from this blog post: https://blogs.unity3d.com/2015/02/06/extending-unity-5-rendering-pipeline-command-buffers/

Shader "Hidden/SeparableGlassBlurHDRP" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "" {}
	}

		HLSLINCLUDE
#pragma target 4.5
#pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"


		struct MyVaryings
	{
		float4 positionCS : SV_POSITION;
		float2 texcoord : TEXCOORD0;
		float4 uv01 : TEXCOORD1;
		float4 uv23 : TEXCOORD2;
		float4 uv45 : TEXCOORD3;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	float4 _WindshieldRain_Blur_Offsets;

	TEXTURE2D_X(_BlitTexture);
	SAMPLER(sampler_BlitTexture);

	MyVaryings MyVert(Attributes input)
	{
		MyVaryings output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
		output.texcoord = GetNormalizedFullScreenTriangleTexCoord(input.vertexID);
		output.uv01 = output.texcoord.xyxy + _WindshieldRain_Blur_Offsets.xyxy * float4(1, 1, -1, -1);
		output.uv23 = output.texcoord.xyxy + _WindshieldRain_Blur_Offsets.xyxy * float4(1, 1, -1, -1) * 2.0;
		output.uv45 = output.texcoord.xyxy + _WindshieldRain_Blur_Offsets.xyxy * float4(1, 1, -1, -1) * 3.0;
		return output;
	}

	float4 Frag(MyVaryings varyings) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);


		half4 color = float4 (0, 0, 0, 0);

		color += 0.40 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.texcoord);
		color += 0.15 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv01.xy);
		color += 0.15 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv01.zw);
		color += 0.10 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv23.xy);
		color += 0.10 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv23.zw);
		color += 0.05 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv45.xy);
		color += 0.05 * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, varyings.uv45.zw);

		return color;
	}

		ENDHLSL

		Subshader {
		Pass{
			Name "Blur Pass"

			ZWrite Off
			ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off

			 HLSLPROGRAM
			 #pragma vertex MyVert
			 #pragma fragment Frag
			 ENDHLSL
		}
	}

	Fallback off


} // shader