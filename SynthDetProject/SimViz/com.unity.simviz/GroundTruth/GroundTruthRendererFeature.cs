using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SimViz;
using UnityEngine.SimViz.Sensors;

public interface IGroundTruthPass
{
    void SetupMaterialProperties(MaterialPropertyBlock mpb, MeshRenderer meshRenderer, Labeling labeling, uint instanceId);
}
class SegmentationUrpPass : ScriptableRenderPass
{
    SegmentationCrossPipelinePass m_SegmentationPass;

    public SegmentationUrpPass(Camera camera, RenderTexture targetTexture)
    {
        m_SegmentationPass = new SegmentationCrossPipelinePass(camera, targetTexture, "Renderers/SegmentationPassURPShader");
        ConfigureTarget(targetTexture, targetTexture.depthBuffer);
        m_SegmentationPass.Setup();
    }
    // This method is called before executing the render pass.
    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in an performance manner.
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
    }

    // Here you can implement the rendering logic.
    // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
    // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
    // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var commandBuffer = CommandBufferPool.Get(nameof(SegmentationUrpPass));
        m_SegmentationPass.Execute(context, commandBuffer, renderingData.cameraData.camera, renderingData.cullResults);
        CommandBufferPool.Release(commandBuffer);
    }

    /// Cleanup any allocated resources that were created during the execution of this render pass.
    public override void FrameCleanup(CommandBuffer cmd)
    {
    }
}
class LabelingUrpPass : ScriptableRenderPass
{
    LabelingCrossPipelinePass m_LabelingCrossPipelinePass;

    public LabelingUrpPass(Camera camera, RenderTexture targetTexture, LabelingConfiguration labelingConfiguration)
    {
        m_LabelingCrossPipelinePass = new LabelingCrossPipelinePass(camera, targetTexture, "Renderers/ClassLabelingURP", labelingConfiguration);
        ConfigureTarget(targetTexture, targetTexture.depthBuffer);
        m_LabelingCrossPipelinePass.Setup();
    }
    // This method is called before executing the render pass.
    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in an performance manner.
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
    }

    // Here you can implement the rendering logic.
    // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
    // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
    // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var commandBuffer = CommandBufferPool.Get(nameof(LabelingUrpPass));
        m_LabelingCrossPipelinePass.Execute(context, commandBuffer, renderingData.cameraData.camera, renderingData.cullResults);
        CommandBufferPool.Release(commandBuffer);
    }

    /// Cleanup any allocated resources that were created during the execution of this render pass.
    public override void FrameCleanup(CommandBuffer cmd)
    {
    }
}

public class GroundTruthRendererFeature : ScriptableRendererFeature
{

    Dictionary<PerceptionCamera, (SegmentationUrpPass segmentationUrpPass, LabelingUrpPass labelingUrpPass)> m_PassMap =
        new Dictionary<PerceptionCamera, (SegmentationUrpPass segmentationUrpPass, LabelingUrpPass labelingUrpPass)>();

    public override void Create()
    {
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraObject = renderingData.cameraData.camera.gameObject;
        var perceptionCamera = cameraObject.GetComponent<PerceptionCamera>();

        if (perceptionCamera == null)
            return;

#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
            return;
#endif

        renderer.EnqueuePass(perceptionCamera.m_SegmentationUrpPass);
        renderer.EnqueuePass(perceptionCamera.m_LabelingUrpPass);
    }
}


