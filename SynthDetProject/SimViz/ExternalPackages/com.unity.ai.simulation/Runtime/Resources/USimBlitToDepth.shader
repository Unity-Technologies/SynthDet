Shader "usim/BlitCopyDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
 
        Pass
        {
 
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
 
            uniform sampler2D _CameraDepthTexture;
 
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 depth : TEXCOORD0;
            };
 
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = mul(UNITY_MATRIX_IT_MV, v.vertex).z;
                return o;
            }
 
            float4 frag(v2f i) : COLOR
            {
                float d = i.pos.z;
                d = Linear01Depth(d);
                return float4(d, d, d, 1);
            }
 
            ENDCG
        }
    }
    FallBack "VertexLit"
}