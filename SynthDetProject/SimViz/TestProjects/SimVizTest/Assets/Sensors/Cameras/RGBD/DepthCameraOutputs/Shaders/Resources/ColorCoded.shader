Shader "Hidden/Syncity/Cameras/ColorCoded" 
{
	Properties 
	{
	    _MainTex ("", any) = "" {} 
	    _StartColor ("Start color", Color) = (1, .0, .0, 1)
	    _EndColor ("End color", Color) = (1, 1, 0, 1)
	}
    SubShader 
    { 
		Pass 
		{
 			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _StartColor, _EndColor;
			
			struct v2f 
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert (appdata_img v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord.xy;
				return o;
			}

            uint _InvertY;
            float4 frag (v2f i) : SV_Target
			{
			    float2 coords = float2(i.texcoord.x, i.texcoord.y);
			    if(_InvertY == 1)
			    {
			        coords.y = 1 - coords.y;
			    }

				float4 depthRGBA = tex2D(_MainTex, coords);
			    float depth = depthRGBA.a;
			    
			    if(depth == 0)
			        return float4(0,0,0,0);
			    return lerp(_StartColor, _EndColor, depth);
			}
			ENDCG 

		}
	}
	Fallback Off 
}