// This code is licensed under CC-BY 4.0: https://creativecommons.org/licenses/by/4.0/legalcode
// Attributed to ShaderTutorials by Ronja Böhringer 
// https://www.ronja-tutorials.com/ -- https://www.patreon.com/RonjaTutorials

// Unity Technologies' fork here: https://github.com/mrpropellers/ShaderTutorials
// Modifications by Unity Technologies denoted by **

// ** Renamed from PostprocessingBlur
Shader "Blur/GaussianBlur"
{

	Properties{
		[HideInInspector]_MainTex ("Texture", 2D) = "white" {}
		_BlurSize("Blur Size", Range(0,0.5)) = 0
		[KeywordEnum(Low, Medium, High)] _Samples ("Sample amount", Float) = 2 
		// ** Removed option to specify whether or not this is a gaussian blur (it's always Gaussian now!)
		[PowerSlider(3)]_StandardDeviation("Standard Deviation", Range(0.00, 0.3)) = 0.02
	}

	SubShader{
		// markers that specify that we don't need culling 
		// or reading/writing to the depth buffer
		Cull Off
		ZWrite Off 
		ZTest Always


		//Vertical Blur
		Pass{
			CGPROGRAM
			//include useful shader functions
			#include "UnityCG.cginc"

			//define vertex and fragment shader
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _SAMPLES_LOW _SAMPLES_MEDIUM _SAMPLES_HIGH

			//texture and transforms of the texture
			sampler2D _MainTex;
			float _BlurSize;
			float _StandardDeviation;

			#define PI 3.14159265359
			#define E 2.71828182846

		#if _SAMPLES_LOW
			#define SAMPLES 10
		#elif _SAMPLES_MEDIUM
			#define SAMPLES 30
		#else
			#define SAMPLES 100
		#endif

			//the object data that's put into the vertex shader
			struct appdata{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			//the data that's used to generate fragments and can be read by the fragment shader
			struct v2f{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			//the vertex shader
			v2f vert(appdata v){
				v2f o;
				//convert the vertex positions from object space to clip space so they can be rendered
				o.position = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			// ** Removed non-gaussian (box) blur computations
			//the fragment shader
			fixed4 frag(v2f i) : SV_TARGET{
				if(_StandardDeviation == 0)
				return tex2D(_MainTex, i.uv);
				//init color variable
				float4 col = 0;
				float sum = 0;
				//iterate over blur samples
				for(float index = 0; index < SAMPLES; index++){
					//get the offset of the sample
					float offset = (index/(SAMPLES-1) - 0.5) * _BlurSize;
					//get uv coordinate of sample
					float2 uv = i.uv + float2(0, offset);
					//calculate the result of the gaussian function
					float stDevSquared = _StandardDeviation*_StandardDeviation;
					float gauss = (1 / sqrt(2*PI*stDevSquared)) * pow(E, -((offset*offset)/(2*stDevSquared)));
					//add result to sum
					sum += gauss;
					//multiply color with influence from gaussian function and add it to sum color
					col += tex2D(_MainTex, uv) * gauss;
				}
				//divide the sum of values by the amount of samples
				col = col / sum;
				return col;
			}

			ENDCG
		}

		//Horizontal Blur
		Pass{
			CGPROGRAM
			//include useful shader functions
			#include "UnityCG.cginc"

			#pragma multi_compile _SAMPLES_LOW _SAMPLES_MEDIUM _SAMPLES_HIGH

			//define vertex and fragment shader
			#pragma vertex vert
			#pragma fragment frag

			//texture and transforms of the texture
			sampler2D _MainTex;
			float _BlurSize;
			float _StandardDeviation;

			#define PI 3.14159265359
			#define E 2.71828182846

		#if _SAMPLES_LOW
			#define SAMPLES 10
		#elif _SAMPLES_MEDIUM
			#define SAMPLES 30
		#else
			#define SAMPLES 100
		#endif

			//the object data that's put into the vertex shader
			struct appdata{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			//the data that's used to generate fragments and can be read by the fragment shader
			struct v2f{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			//the vertex shader
			v2f vert(appdata v){
				v2f o;
				//convert the vertex positions from object space to clip space so they can be rendered
				o.position = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			//the fragment shader
			fixed4 frag(v2f i) : SV_TARGET{
				//failsafe so we can use turn off the blur by setting the deviation to 0
				if(_StandardDeviation == 0)
				    return tex2D(_MainTex, i.uv);
				//calculate aspect ratio
				float invAspect = _ScreenParams.y / _ScreenParams.x;
				//init color variable
				float4 col = 0;
				float sum = 0;
				//iterate over blur samples
				for(float index = 0; index < SAMPLES; index++){
					//get the offset of the sample
					float offset = (index/(SAMPLES-1) - 0.5) * _BlurSize * invAspect;
					//get uv coordinate of sample
					float2 uv = i.uv + float2(offset, 0);
					//calculate the result of the gaussian function
					float stDevSquared = _StandardDeviation*_StandardDeviation;
					float gauss = (1 / sqrt(2*PI*stDevSquared)) * pow(E, -((offset*offset)/(2*stDevSquared)));
					//add result to sum
					sum += gauss;
					//multiply color with influence from gaussian function and add it to sum color
					col += tex2D(_MainTex, uv) * gauss;
				}
				//divide the sum of values by the amount of samples
				col = col / sum;
				return col;
			}

			ENDCG
		}
	}
}