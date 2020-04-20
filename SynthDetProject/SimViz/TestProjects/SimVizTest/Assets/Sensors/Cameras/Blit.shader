Shader "Hidden/SynCity/Blit"
{
	Properties
	{
		_MainTex("main tex", 2D) = "defaulttexture" {}
	}

	HLSLINCLUDE

#pragma target 4.5
#pragma multi_compile _ DISABLE_TEXTURE2D_X_ARRAY
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);

	struct Attributes
	{
		uint vertexID : SV_VertexID;
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float2 texcoord   : TEXCOORD0;
	};

	Varyings Vert(Attributes input)
	{
		Varyings output;
		output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
		output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
		return output;
	}

	float4 Frag(Varyings i) : SV_Target
	{
		float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
		return color;
	}

	ENDHLSL

	SubShader
	{

		// 0: Blit
		Pass
		{
			ZWrite Off ZTest Always Blend Off Cull Off

			HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
			ENDHLSL
		}
	}

	Fallback Off
}