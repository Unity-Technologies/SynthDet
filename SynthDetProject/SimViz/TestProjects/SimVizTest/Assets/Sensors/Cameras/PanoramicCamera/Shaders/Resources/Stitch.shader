Shader "Hidden/Syncity/Cameras/Stitch" 
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
	}

    Subshader
    {
		Pass
		{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
    
            struct v2f
            {
                float4 pos	: SV_POSITION;
                float2 uv	: TEXCOORD0;
            };

            sampler2D _Front;
            sampler2D _Left;
            sampler2D _Right;
            sampler2D _Back;
            sampler2D _Top;
            sampler2D _Bottom;
            float2 _FOV;
        
            v2f vert(appdata_img v) 
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
        		o.uv = v.texcoord;
                return o;
            }
        
            static float PI = 3.14159265358979323846264;
            static float PI_2 = 1.57079632679489661923;
            static float PI_4 = 0.78539816339744830962;
	        
            float4 frag(v2f input) : Color
            {
                float2 uv = input.uv;

                float theta = 2.0 * uv.x - 1; // to -1, +1 range mirrored
                theta *= _FOV.x;    // adjust to selected horiz FOV
                theta *= PI;
                
                float phi = 2.0 * uv.y - 1.0;
                phi *= _FOV.y;
                phi *= PI_2;
                
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
                return src;
            }
    
            ENDCG
        }
    }
    Fallback off
}