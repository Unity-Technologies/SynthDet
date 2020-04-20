// https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Perception;
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
    public bool RenderInEditor = false;
    public bool RenderMaxStrength = false;
    public RenderPassEvent RenderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    
    ProjectInitialization m_initParams;
    RandomizedPostProcessingPass m_RenderPass;
    Material m_BlurMaterial;
    Material m_NoiseMaterial;
    Random m_Rand = new Random(1);
    bool m_IsEnabled = true;
    
    MetricDefinition m_PostProcessValuesMetric;
    static readonly Guid k_PostProcessValuesMetricId = Guid.Parse("A0B7FD8C-7011-4675-A9BA-D1C5C2A22A2C");
    
    const string k_BlurShader = "Blur/GaussianBlur";
    const string k_NoiseShader = "Noise/WhiteNoise";

    public override void Create()
    {
        m_IsEnabled = true;
        m_BlurMaterial = new Material(Shader.Find(k_BlurShader));
        m_NoiseMaterial = new Material(Shader.Find(k_NoiseShader));
        if (m_BlurMaterial == null)
        {
            Debug.LogError($"Unable to find blur shader {k_BlurShader}.");
            m_IsEnabled = false;
        }

        if (m_NoiseMaterial == null)
        {
            Debug.LogError($"Unable to find noise shader {k_NoiseShader}");
            m_IsEnabled = false;
        }

        m_RenderPass = new RandomizedPostProcessingPass();
        m_PostProcessValuesMetric = SimulationManager.RegisterMetricDefinition(
            "random post processing", 
            description: "Some post-processing parameters are randomized each frame. These are the per-frame values used.",
            id: k_PostProcessValuesMetricId);

        if (!m_IsEnabled)
        {
            Debug.LogWarning($"{nameof(RandomizedPostProcessingFeature)} is not enabled and will not affect rendering.");
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!m_IsEnabled)
            return;

        #if UNITY_EDITOR
        if (!RenderInEditor && !EditorApplication.isPlaying)
            return;
        #endif

        // Try to find game object here because scene may not be initialized on Create()
        if (m_initParams == null)
        {
            m_initParams = GameObject.Find("Management")?.GetComponentInChildren<ProjectInitialization>();
            if (m_initParams == null)
            {
                Debug.LogWarning("Unable to find Management object. Will not render this pass. " +
                    "(You can disable this feature in Assets/RenderFeatures/ForwardRenderer/RandomizedPostProcessing");
                return;
            }
        }

        var p = m_initParams.PostProcessingParams;
        var kernelSize = RenderMaxStrength ? p.BlurKernelSizeMax : m_Rand.NextFloat(0f, p.BlurKernelSizeMax);
        var stdDev = 
            (RenderMaxStrength ? p.BlurStandardDeviationMax: m_Rand.NextFloat(0f, p.BlurStandardDeviationMax)) * kernelSize;
        var noiseStrength = RenderMaxStrength ? p.NoiseStrengthMax : m_Rand.NextFloat(0f, p.NoiseStrengthMax);

        var metric = new PostProcessingValues
        {
            blur_kernel_size_uv = kernelSize,
            blur_std_dev_uv = stdDev,
            noise_strength = noiseStrength
        };
        SimulationManager.ReportMetric(m_PostProcessValuesMetric, new[] { metric });

        m_RenderPass.Update(m_BlurMaterial, m_NoiseMaterial, RenderEvent,
            renderer.cameraColorTarget, kernelSize, stdDev, noiseStrength);
        renderer.EnqueuePass(m_RenderPass);
    }
}
