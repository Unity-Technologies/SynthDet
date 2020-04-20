Shader "Hidden/SimViz/Cameras/Panoramic" 
{
	Properties
	{
        _Front("Front", 2D) = "" {}
        _Left("Left", 2D) = "" {}
        _Right("Right", 2D) = "" {}
        _Back("Back", 2D) = "" {}
        _Top("Top", 2D) = "" {}
        _Bottom("Bottom", 2D) = "" {}
        
        _FOV ("FOV", Vector) = (1, 1, 0, 0)
        
        _DepthRange("Depth Range", Vector) = (0, 1, 0, 0)
	}
	
	HLSLINCLUDE

#pragma target 4.5
#pragma multi_compile _ DISABLE_TEXTURE2D_X_ARRAY
#pragma multi_compile _ DEPTH
	
    //#include "UnityCG.cginc"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

    struct v2f
    {
        float4 pos	: SV_POSITION;
        float2 uv	: TEXCOORD0;
    };
    
    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    sampler2D _Front;
    sampler2D _Left;
    sampler2D _Right;
    sampler2D _Back;
    sampler2D _Top;
    sampler2D _Bottom;
    float2 _FOV;
    
#if DEPTH
    float2 _DepthRange;
#endif

    v2f vert(Attributes input)
    {
        v2f output;
        output.pos = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }
    
    float4 frag(v2f input) : Color
    {
        float2 uv = input.uv;

        float theta = 2.0 * uv.x - 1; // to -1, +1 range mirrored
        theta *= _FOV.x;    // adjust to selected horiz FOV
        theta *= PI;
        
        float phi = 2.0 * uv.y - 1.0;
        phi *= _FOV.y;
        phi *= HALF_PI;
        
        float x = cos(phi) * sin(theta);
        float y = sin(phi);
        float z = cos(phi) * cos(theta);
    
        float scale;
        float2 px;            
        float4 src;
        if (abs(x) >= abs(y) && abs(x) >= abs(z)) 
        {
            if (x < 0.0) 
            {   // left
                scale = -1.0 / x;
                px.x = ( z*scale + 1.0) / 2.0;
                px.y = ( y*scale + 1.0) / 2.0;
                src = tex2D(_Left, px);
            }
            else 
            {   // right
                scale = 1.0 / x;
                px.x = (-z*scale + 1.0) / 2.0;
                px.y = ( y*scale + 1.0) / 2.0;
                src = tex2D(_Right, px);
            }
        }
        else if (abs(y) >= abs(z)) 
        {
            if (y < 0.0) 
            {   // bottom
                scale = 1.0 / y;
                px.x = ( x*scale + 1.0) / 2.0;
                px.y = (-z*scale + 1.0) / 2.0;
                src = tex2D(_Bottom, float2(1 - px.x , px.y));
            }
            else
            {   // top
                scale = -1.0 / y;
                px.x = ( x*scale + 1.0) / 2.0;
                px.y = ( z*scale + 1.0) / 2.0;
                src = tex2D(_Top, float2(1 - px.x , px.y));
            }
        }
        else 
        {
            if (z < 0.0) 
            {   // back
                scale = -1.0 / z;
                px.x = (-x*scale + 1.0) / 2.0;
                px.y = ( y*scale + 1.0) / 2.0;
                src = tex2D(_Back, px);
            }
            else 
            {   // front
                scale = 1.0 / z;
                px.x = ( x*scale + 1.0) / 2.0;
                px.y = ( y*scale + 1.0) / 2.0;
                src = tex2D(_Front, px);
            }
        }
        
#if DEPTH
        float depth = (src.x - _DepthRange.x) / (_DepthRange.y - _DepthRange.x);
        src = float4(depth, depth, depth, 1);
#endif
        return src;
    }
    ENDHLSL

    Subshader
    {
		Pass
		{
			ZWrite Off ZTest Always Blend Off Cull Off
			
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    Fallback off
}