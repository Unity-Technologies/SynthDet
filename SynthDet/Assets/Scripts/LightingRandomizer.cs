using System;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using Random = Unity.Mathematics.Random;

public class LightingRandomizerSystem : JobComponentSystem
{
    static Guid k_LightingInfoGuid = new Guid("939248EE-668A-4E98-8E79-E7909F034A47");
    Random m_Rand;
    Light m_Light;
    bool m_IsInitialized;
    MetricDefinition m_LightingInfoDefinition;
    
    ProjectInitialization m_InitParams;

    protected override void OnCreate()
    {
        m_Rand = new Random(1);
    }

    void Initialize()
    {
        var light = GameObject.Find("Directional Light");
        if (light == null)
            return;

        m_LightingInfoDefinition = DatasetCapture.RegisterMetricDefinition("lighting info", "Per-frame light color and orientation", id: k_LightingInfoGuid);
        m_Light = light.GetComponent<Light>();
        // To simulate phong shading we turn off shadows
        m_Light.shadows = LightShadows.None;
        m_IsInitialized = true;
        
        
        // Try to find game object here because scene may not be initialized on Create()
        if (m_InitParams == null)
        {
            m_InitParams = GameObject.Find("Management")?.GetComponentInChildren<ProjectInitialization>();
            if (m_InitParams == null)
            {
                Debug.LogWarning("Unable to find Management object. Will not randomize lighting.");
                return;
            }
        }
    }

    public struct LightInfo
    {
        public Color color;
        // ReSharper disable InconsistentNaming
        public float x_rotation;
        public float y_rotation;
        // ReSharper restore InconsistentNaming
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!m_IsInitialized)
        {
            Initialize();
        }

        if (!m_IsInitialized)
            return inputDeps;

        m_Light.color = new Color(
            m_Rand.NextFloat(m_InitParams.AppParameters.LightColorMin, 1f), m_Rand.NextFloat(m_InitParams.AppParameters.LightColorMin, 1f), m_Rand.NextFloat(m_InitParams.AppParameters.LightColorMin, 1f));
        var xRotation = m_Rand.NextFloat(-m_InitParams.AppParameters.LightRotationMax, m_InitParams.AppParameters.LightRotationMax);
        var yRotation = m_Rand.NextFloat(-m_InitParams.AppParameters.LightRotationMax, m_InitParams.AppParameters.LightRotationMax);
        m_Light.transform.rotation = Quaternion.Euler(
            xRotation,
            yRotation,
            0f
        );

        DatasetCapture.ReportMetric(m_LightingInfoDefinition, new[]
        {
            new LightInfo()
            {
                color = m_Light.color,
                x_rotation = xRotation,
                y_rotation = yRotation
            }
        });
        return inputDeps;
    }

}

