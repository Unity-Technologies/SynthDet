Shader "Hidden/Syncity/Cameras/RGBD"
{
	Properties
	{
	    _Bands ("Bands", Range(0, 100)) = 0
		_MainTex("Texture", 2D) = "white" {}
	}

    SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
    
            struct v2f 
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float3 worldDirection : TEXCOORD1;
            };
        
            float4x4 _ClipToWorld;
            v2f vert(appdata_base v) 
            {
                v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord.xy;
        
                float4 clip = float4(o.vertex.xy, 0.0, 1.0);
                o.worldDirection = mul(_ClipToWorld, clip) - _WorldSpaceCameraPos;
        
                return o;
            }
        
            sampler2D _MainTex;
            uniform sampler2D _CameraDepthTexture;
            float _Bands;

            float4 frag(v2f i) : SV_Target
            {
			    float2 coords = float2(i.texcoord.x, i.texcoord.y);

                float4 c = tex2D(_MainTex, coords);
                if(c.a == 0)
                {
                    return float4(0, 0, 0, 0);
                }
        
        		float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, coords);
                depth = LinearEyeDepth(depth);
        
                float3 worldspace = i.worldDirection * depth + _WorldSpaceCameraPos;
                depth = distance(_WorldSpaceCameraPos, worldspace.xyz);
        
                if(depth < _ProjectionParams.y)
                {
                    depth = 0;
                }
                else if (depth > _ProjectionParams.z)
                {
                    depth = 1; 
                }                
                else
                {
                    if(_Bands > 0)
                    {
                        depth = floor(depth / _Bands);
                        depth *= _Bands;
                    }
                
                    depth -= _ProjectionParams.y;
                    depth /= _ProjectionParams.z - _ProjectionParams.y;
                }

                if (depth > 1)
                    depth = 0;
        
                return float4(c.r, c.g, c.b, depth);
            }
            ENDCG
        }
	}
    FallBack Off
}