#include "UnityCG.cginc"

Texture2D<float4> _Front; 
Texture2D<float4> _Left; 
Texture2D<float4> _Right; 
Texture2D<float4> _Back; 
Texture2D<float4> _Top; 
Texture2D<float4> _Bottom; 
SamplerState  sampler_Front; 
SamplerState  sampler_Left; 
SamplerState  sampler_Right; 
SamplerState  sampler_Back; 
SamplerState  sampler_Top; 
SamplerState  sampler_Bottom; 

float2 _FOV;

StructuredBuffer<float> _Beams;
uint _NbOfBeams;
uint _PointsPerBeam;
float _AzOffset;
float2 _ClipPlanes;

static float PI = 3.14159265358979323846264;
static float PI_2 = 1.57079632679489661923;

float calculateDepth (float2 uv)
{
    float theta = 2.0 * uv.x - 1; // to -1, +1 range mirrored
    theta *= _FOV.x / 360.0;    // adjust to selected horiz FOV
    theta *= PI;
    
    float phi = 2.0 * uv.y - 1.0;
    phi *= _FOV.y / 180.0;
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
            src = _Left.SampleLevel(sampler_Left, px, 0);
        }
        else 
        {   // right
            scale = 1.0 / x;
            px.x = (-z*scale + 1.0) / 2.0;
            px.y = ( y*scale + 1.0) / 2.0;
            src = _Right.SampleLevel(sampler_Right, px, 0);
        }
    }
    else if (abs(y) >= abs(z)) 
    {
        if (y < 0.0) 
        {   // bottom
            scale = 1.0 / y;
            px.x = ( x*scale + 1.0) / 2.0;
            px.y = (-z*scale + 1.0) / 2.0;
            src = _Bottom.SampleLevel(sampler_Bottom, float2(1 - px.x , px.y), 0);
        }
        else
        {   // top
            scale = -1.0 / y;
            px.x = ( x*scale + 1.0) / 2.0;
            px.y = ( z*scale + 1.0) / 2.0;
            src = _Top.SampleLevel(sampler_Top, float2(1 - px.x , px.y), 0);
        }
    }
    else 
    {
        if (z < 0.0) 
        {   // back
            scale = -1.0 / z;
            px.x = (-x*scale + 1.0) / 2.0;
            px.y = ( y*scale + 1.0) / 2.0;
            src = _Back.SampleLevel(sampler_Back, px, 0);
        }
        else 
        {   // front
            scale = 1.0 / z;
            px.x = ( x*scale + 1.0) / 2.0;
            px.y = ( y*scale + 1.0) / 2.0;
            src = _Front.SampleLevel(sampler_Front, px, 0);
        }
    }

    float depth01 = DecodeFloatRGBA(src);   
    if(depth01 == 0)
        return 0;
    float depth = _ClipPlanes.x + depth01 * (_ClipPlanes.y - _ClipPlanes.x);
    
    return depth;
}
float getLaserId (uint laserId, uint _NbOfBeams)
{
    float ret = 0;
    if(_NbOfBeams > 1)
    {
        ret = laserId / (float)(_NbOfBeams - 1);
    }
    return ret;
}
uint getIndex(uint3 id)
{
    return id.x * _NbOfBeams + id.y;
}
float4 calculatePolar(uint3 id)
{
    float2 uv = float2(((float)id.x / (float)_PointsPerBeam), _Beams[id.y]);
    
    uv.x += _AzOffset;
    uv.x %= 1;

    float depth = calculateDepth(uv);

    float azimuth = (uv.x - 0.5) * _FOV.x;
    float elevation = (uv.y - 0.5) * _FOV.y;
    
    return float4(azimuth, elevation, depth, getLaserId(id.y, _NbOfBeams));
}
