Shader "Noise/WhiteNoise" {
	Properties{
	    [HideInInspector]_MainTex("Texture", 2D) = "white" {}
	    _Strength("Noise Strength", Range(0, 1.0)) = 0
	}

	SubShader {
		Cull Off
		ZWrite Off 
		ZTest Always

        Pass {
             CGPROGRAM
             #include "UnityCG.cginc"
             #include "ShaderRand.cginc"

             #pragma vertex vert
             #pragma fragment frag

             sampler2D _MainTex;
             float _Strength;

            struct v2f {
                float2 uv : TEXCOORD0;
            };

            v2f vert (
                float4 vertex : POSITION, // vertex position input
                float2 uv : TEXCOORD0, // texture coordinate input
                out float4 outpos : SV_POSITION // clip space position output
                )
            {
                v2f o;
                o.uv = uv;
                outpos = UnityObjectToClipPos(vertex);
                return o;
            }
             
             fixed4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_TARGET{
                 float4 noise = float4(random3(screenPos.xy, _Time.x), 1.0);
                 float4 color = lerp(clamp(tex2D(_MainTex, i.uv), 0.0, 1.0), noise, _Strength);
                 return color;
             }

             ENDCG
        }
	}
}
