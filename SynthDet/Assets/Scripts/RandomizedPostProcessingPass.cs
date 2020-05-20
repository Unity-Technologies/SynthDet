// https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RandomizedPostProcessingPass : ScriptableRenderPass
{
    public FilterMode filterMode;
    
    Material m_BlurMaterial;
    Material m_NoiseMaterial;

    float m_BlurKernelSize;
    float m_BlurStandardDeviation;
    float m_WhiteNoiseStrength;
    
    const string k_BlurSize = "_BlurSize";
    const string k_StandardDeviation = "_StandardDeviation";
    const string k_NoiseStrength = "_Strength";
    const string k_Tag = "RandomizedPostProcessing";
    RenderTargetHandle m_TemporaryColorTexture;
    RenderTargetIdentifier m_Source;

    public RandomizedPostProcessingPass()
    {
        m_TemporaryColorTexture = new RenderTargetHandle();
        m_TemporaryColorTexture.Init("_TemporaryColorTexture");
    }

    public void Update(
        Material gaussianBlur,
        Material whiteNoise,
        RenderPassEvent renderEvent,
        RenderTargetIdentifier source,
        float blurKernelSize,
        float blurStandardDeviation,
        float whiteNoiseStrength)
    {
        m_BlurMaterial = gaussianBlur;
        m_NoiseMaterial = whiteNoise;
        renderPassEvent = renderEvent;
        m_Source = source;

        m_BlurKernelSize = blurKernelSize;
        m_BlurStandardDeviation = blurStandardDeviation;
        m_WhiteNoiseStrength = whiteNoiseStrength;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get(k_Tag);
        
        var opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
        opaqueDesc.depthBufferBits = 0;
        
        m_BlurMaterial.SetFloat(k_BlurSize, m_BlurKernelSize);
        m_BlurMaterial.SetFloat(k_StandardDeviation, m_BlurStandardDeviation);
        m_NoiseMaterial.SetFloat(k_NoiseStrength, m_WhiteNoiseStrength);
        
        cmd.GetTemporaryRT(m_TemporaryColorTexture.id, opaqueDesc, filterMode);
        Blit(cmd, m_Source, m_TemporaryColorTexture.Identifier(), m_BlurMaterial);
        Blit(cmd, m_TemporaryColorTexture.Identifier(), m_Source, m_BlurMaterial, 1);
        Blit(cmd, m_Source, m_TemporaryColorTexture.Identifier(), m_NoiseMaterial);
        Blit(cmd, m_TemporaryColorTexture.Identifier(), m_Source);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    
    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
    }
}
