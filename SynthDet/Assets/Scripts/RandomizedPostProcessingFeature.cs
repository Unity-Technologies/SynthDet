// https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Perception.GroundTruth;
using Random = Unity.Mathematics.Random;

public class RandomizedPostProcessingFeature : ScriptableRendererFeature
{
    public struct PostProcessingValues
    {
        // NOTE: We don't use C# code style here because these names will be written to json
        public float blur_kernel_size_uv;
        public float blur_std_dev_uv;
        public float noise_strength;
    }

    [Header("Debug")]
    public bool RenderInEditor;
    public bool RenderMaxStrength;
    public RenderPassEvent RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    
    ProjectInitialization m_InitParams;
    RandomizedPostProcessingPass m_RenderPass;
    Material m_BlurMaterial;
    Material m_NoiseMaterial;
    Random m_Rand = new Random(1);
    bool m_ShadersWereFound;
    
    MetricDefinition m_PostProcessValuesMetric;
    static readonly Guid k_PostProcessValuesMetricId = Guid.Parse("A0B7FD8C-7011-4675-A9BA-D1C5C2A22A2C");
    
    const string k_BlurShader = "Blur/GaussianBlur";
    const string k_NoiseShader = "Noise/WhiteNoise";

    public override void Create()
    {
        m_RenderPass = new RandomizedPostProcessingPass();
        if (Application.isPlaying)
        {
            m_PostProcessValuesMetric = DatasetCapture.RegisterMetricDefinition(
                "random post processing", 
                description: "Some post-processing parameters are randomized each frame. These are the per-frame values used.",
                id: k_PostProcessValuesMetricId);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        #if UNITY_EDITOR
        if (!RenderInEditor && !EditorApplication.isPlaying)
            return;
        #endif

        // Initialize here because scene is not guaranteed to be initialized when Create is called
        if (m_InitParams == null)
        {
            m_InitParams = GameObject.Find("Management")?.GetComponentInChildren<ProjectInitialization>();
            if (m_InitParams == null)
            {
                Debug.LogWarning("Unable to find Management object. Will not render this pass. " +
                    "(You can disable this feature in Assets/RenderFeatures/ForwardRenderer/RandomizedPostProcessing");
                return;
            }

            m_ShadersWereFound = true;
            var blurShader = Shader.Find(k_BlurShader);
            if (blurShader != null)
            {
                m_BlurMaterial = new Material(blurShader);
            }
            else
            {
                m_ShadersWereFound = false;
            }

            var noiseShader = Shader.Find(k_NoiseShader);
            if (noiseShader != null)
            {
                m_NoiseMaterial = new Material(noiseShader);
            }
            else
            {
                m_ShadersWereFound = false;
            }

            if (!m_ShadersWereFound)
            {
                Debug.LogError(
                    $"{nameof(RandomizedPostProcessingFeature)} couldn't find its shaders and will not render.");
            }
        }
        
        if (!m_ShadersWereFound)
        {
            return;
        }

        var p = m_InitParams.AppParameters;
        var kernelSize = RenderMaxStrength ? p.BlurKernelSizeMax : m_Rand.NextFloat(0f, p.BlurKernelSizeMax);
        var stdDev = (RenderMaxStrength ? p.BlurStandardDeviationMax : 
            m_Rand.NextFloat(0f, p.BlurStandardDeviationMax)) * kernelSize;
        var noiseStrength = RenderMaxStrength ? p.NoiseStrengthMax : m_Rand.NextFloat(0f, p.NoiseStrengthMax);

        if (Application.isPlaying)
        {
            var metric = new PostProcessingValues
            {
                blur_kernel_size_uv = kernelSize,
                blur_std_dev_uv = stdDev,
                noise_strength = noiseStrength
            };
            DatasetCapture.ReportMetric(m_PostProcessValuesMetric, new[] { metric });
        }

        m_RenderPass.Update(m_BlurMaterial, m_NoiseMaterial, RenderEvent,
            renderer.cameraColorTarget, kernelSize, stdDev, noiseStrength);
        renderer.EnqueuePass(m_RenderPass);
    }
}
