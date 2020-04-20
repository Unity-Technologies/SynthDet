﻿Shader "Renderers/ClassLabeling"
{
    Properties
    {
        [PerObjectData] LabelingId("Labeling Id", int) = 0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    //enable GPU instancing support
    #pragma multi_compile_instancing

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "FirstPass"
            Tags { "LightMode" = "FirstPass" }

            Blend Off
            ZWrite On
            ZTest LEqual

            Cull Back

            HLSLPROGRAM

            // Toggle the alpha test
            #define _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassRenderers.hlsl"

            uint LabelingId;

            // only necessary to define to make ShaderPassForwardUnlit.hlsl happy
            void GetSurfaceAndBuiltinData(FragInputs fragInputs, float3 viewDirection, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {}
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"
            
            float4 SegmentationFrag(PackedVaryingsToPS packedInput) : SV_Target
            {
                return float4(UnpackUIntToFloat((uint)LabelingId, 0, 16), 0, 0, 0);
            }

            #pragma vertex Vert
            #pragma fragment SegmentationFrag

            ENDHLSL
        }
    }
}
