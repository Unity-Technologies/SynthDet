Shader "Hidden/Syncity/Cameras/LidarRenderer"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.05
        
        _StartColor ("Start color", Color) = (1, .0, .0, 1)
	    _EndColor ("End color", Color) = (1, 1, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex Vertex
            #pragma geometry Geometry
            #pragma fragment Fragment
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            StructuredBuffer<float4> _PointBuffer;
            float4x4 _Transform;
            half _PointSize;
            
            float4 _StartColor, _EndColor;
                        
            struct v2g
            {
                float4 position : SV_POSITION;
                UNITY_FOG_COORDS(1)
                float4 color : TEXCOORD2;
            };
            struct g2f
            {
                float4 position : SV_POSITION;
                UNITY_FOG_COORDS(1)
                float4 color : TEXCOORD2;
            };
            
            // Vertex phase
            v2g Vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                float4 pt = _PointBuffer[id];

                float4 pos;
                if(pt.x == 0 && pt.y == 0 && pt.z == 0)
                {
                    pos = float4(0,0,0,0);
                }
                else
                {
                    pos = UnityObjectToClipPos(mul(_Transform, float4(pt.xyz, 1)));
                }
                float laserId = pt.w;
            
                // Set vertex output.
                v2g o;
                o.position = pos;
                o.color = _StartColor;
                o.color = lerp(_StartColor, _EndColor, laserId);
                //o.color = depthRGBA;
                UNITY_TRANSFER_FOG(o, o.position);
                return o;
            }
            
            // Geometry phase                        
            void createPoint(float4 p, float4 color, inout TriangleStream<g2f> outStream)
            {
                float4 origin = p;
                float2 extent = abs(UNITY_MATRIX_P._11_22 * _PointSize);
            
                // Copy the basic information.
                g2f o;
                o.position = origin;
                UNITY_TRANSFER_FOG(o, o.position);
            
                // Determine the number of slices based on the radius of the
                // point on the screen.
                float radius = extent.y / origin.w * _ScreenParams.y;
                uint slices = min((radius + 1) / 5, 4) + 2;
            
                // Slightly enlarge quad points to compensate area reduction.
                // Hopefully this line would be complied without branch.
                if (slices == 2) extent *= 1.2;
            
                // Top vertex
                o.position.y = origin.y + extent.y;
                o.position.xzw = origin.xzw;
                o.color = color;
                outStream.Append(o);
            
                UNITY_LOOP for (uint i = 1; i < slices; i++)
                {
                    float sn, cs;
                    sincos(UNITY_PI / slices * i, sn, cs);
            
                    // Right side vertex
                    o.position.xy = origin.xy + extent * float2(sn, cs);
                    outStream.Append(o);
            
                    // Left side vertex
                    o.position.x = origin.x - extent.x * sn;
                    outStream.Append(o);
                }
            
                // Bottom vertex
                o.position.x = origin.x;
                o.position.y = origin.y - extent.y;
                outStream.Append(o);
            
                outStream.RestartStrip();
            }
            
            [maxvertexcount(36)]
            void Geometry(point v2g input[1], inout TriangleStream<g2f> outStream)
            {            
                float4 pt = input[0].position;
                if(!(pt.x == 0 && pt.y == 0 && pt.z == 0 && pt.w == 0))
                {    
                    createPoint(pt, input[0].color, outStream);
                }
            }
            
            float4 Fragment(g2f input) : SV_Target
            {
                float4 c = input.color;
                UNITY_APPLY_FOG(input.fogCoord, c);
                return c;
            }

            ENDCG
        }
    }
}
