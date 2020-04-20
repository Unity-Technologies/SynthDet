Shader "Hidden/SynCity/Cameras/Radar" 
{
	SubShader 
	{
		ZWrite Off
		Cull Off
		ZTest Always
		Pass 
		{
			CGPROGRAM
	        #include "UnityCG.cginc"
            #include "UnityDeferredLibrary.cginc"
            #include "UnityGBuffer.cginc"
            
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0

            struct v2f 
            {
                float4 pos: SV_POSITION;
                float2 uv: TEXCOORD0;
                float4 ray: TEXCOORD1; 
            };

            v2f vert(appdata_img i) 
            {
                v2f o;
                
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = i.texcoord;
                float far = _ProjectionParams.z;
                o.ray = mul(unity_CameraInvProjection, 2 * i.vertex - 1) * far;
                
                return o;
            }	

            float4 _CameraDepthTexture_TexelSize;

            float getDepth(float2 uv)
            {
                float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);            
                return Linear01Depth(z);                
            } 
            
            sampler2D       _CameraGBufferTexture0;
            float4          _CameraGBufferTexture0_TexelSize;
            sampler2D       _CameraGBufferTexture1;
            float4          _CameraGBufferTexture1_TexelSize;
            sampler2D       _CameraGBufferTexture2;
            float4          _CameraGBufferTexture2_TexelSize;
            float _MinIntensity;
            float getNormal(float2 uv)
            {
                if(uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y == 1)
                {
                    return 0;
                }
                
                half4 gbuffer0 = tex2D (_CameraGBufferTexture0, uv);
                half4 gbuffer1 = tex2D (_CameraGBufferTexture1, uv);
                half4 gbuffer2 = tex2D (_CameraGBufferTexture2, uv);
                //return float4(gbuffer2.rgb, 1);

                UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
                float3 worldNormal = normalize(data.normalWorld);                
                float3 forward = mul((float3x3)unity_CameraToWorld, float3(0,0,-1));
                float normal = saturate(dot(normalize(worldNormal), normalize(forward)));

                if(normal < _MinIntensity)
                {
                    return 0;
                }
                normal = (normal - _MinIntensity) / (1 - _MinIntensity);
                return normal;
            } 
                
            float _Random;
            float random(float2 st)
            {
              return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
            }
        
            RWTexture2D<float4> _Output;
            float _HorizontalFOV;
			float4 _StartColor, _EndColor;
            float3 _Noise;
            float4 frag(v2f i) : SV_TARGET
            {
                float random01 = random(i.uv * _Random * _SinTime.x);
                
                float depth01 = getDepth(i.uv);                

                float intensity = 0;
                if(depth01 > 0 && depth01 < 1)
                {
                    intensity = getNormal(i.uv);
                }
                                
                float4 viewPosition = float4(i.ray.xyz * -depth01, 1);
                viewPosition.x += (random01 - .5) * _Noise.x;
                viewPosition.z += (random01 - .5) * _Noise.z;

                float4 output = float4(0, 0, 0, 0);
                if(intensity > 0)
                {
                    intensity += (random01 - .5) * _Noise.z;
                    
                    uint width, height;
                    _Output.GetDimensions(width, height);
    
                    float near = _ProjectionParams.y;
                    float far = _ProjectionParams.z;    
                    float xScale = 2 * tan(radians(_HorizontalFOV / 2)) * far; 
         
                    float2 outputUV = float2(((-viewPosition.x / xScale) + .5) * width, ((viewPosition.z - near) / (far - near)) * height);
                    
                    //output = EncodeFloatRGBA(intensity);
                    //output = float4(intensity,0,0,1);
                    output = lerp(_StartColor, _EndColor, intensity);
                    _Output[outputUV] = output;
                }
                
                return output;
            }

			ENDCG
		}
	}		

	FallBack "Diffuse"
}