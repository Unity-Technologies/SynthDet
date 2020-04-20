Shader "Hidden/Syncity/Cameras/TrueDepth"
{
	Properties
	{
    	_MainTex ("Albedo Texture", 2D) = "white" {}
    	_Color("Albedo Color", Color) = (1,1,1,1)
	}
    SubShader 
    {
        Pass 
        {		
            Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
            ZWrite On Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos	: SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            uniform sampler2D _MainTex;
            uniform float4 _MainTex_ST;
            v2f vert(appdata_base v) 
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul (unity_ObjectToWorld, v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
            
            float _Bands;
            float _Noise;
            float _Random;
            
            float random(float2 st)
            {
              return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
            }
            
            float4 _Color;
            uniform sampler2D _Albedo;
            float4 frag(v2f i) : SV_Target 
            {
                if(_Color.a < .9 && (_Color.r > 0 || _Color.g > 0 || _Color.b > 0))
                    discard;
                fixed4 col = tex2D(_MainTex, i.uv) + tex2D(_Albedo, i.uv);
                if(col.a < .9 && (col.r > 0 || col.g > 0 || col.b > 0))
                    discard;
                
                float depth = distance(_WorldSpaceCameraPos, i.worldPos);

                float random01 = random(i.uv * _Random * _SinTime.x);
                depth += (random01 - .5) * _Noise;
                
                if(depth < _ProjectionParams.y)
                {
                    return float4(0,0,0,0);
                }
                else if (depth > _ProjectionParams.z)
                {
                    return float4(1,1,1,1);
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

                //return float4(depth, depth, depth, 1);
                return EncodeFloatRGBA(depth);
            }
            ENDCG
        }	
    }
		
	FallBack Off
}