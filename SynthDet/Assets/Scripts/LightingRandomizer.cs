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
    
    ProjectInitialization m_initParams;
    float m_LightColor;
    float m_LightRotation;

    protected override void OnCreate()
    {
        m_Rand = new Random(1);
    }

    void Initialize()
    {
        var light = GameObject.Find("Directional Light");
        if (light == null)
            return;

        m_LightingInfoDefinition = SimulationManager.RegisterMetricDefinition("lighting info", "Per-frame light color and orientation", id: k_LightingInfoGuid);
        m_Light = light.GetComponent<Light>();
        // To simulate phong shading we turn off shadows
        m_Light.shadows = LightShadows.None;
        m_IsInitialized = true;
        
        
        // Try to find game object here because scene may not be initialized on Create()
        if (m_initParams == null)
        {
            m_initParams = GameObject.Find("Management")?.GetComponentInChildren<ProjectInitialization>();
            if (m_initParams == null)
            {
                Debug.LogWarning("Unable to find Management object. Will not randomize lighting.");
                return;
            }
        }
        
        m_LightColor = m_Rand.NextFloat(0, m_initParams.AppParameters.LightColorMin);
        m_LightRotation = m_Rand.NextFloat(0, m_initParams.AppParameters.LightRotationMax);
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
            m_Rand.NextFloat(m_LightColor, 1f), m_Rand.NextFloat(m_LightColor, 1f), m_Rand.NextFloat(m_LightColor, 1f));
        var xRotation = m_Rand.NextFloat(-m_LightRotation, m_LightRotation);
        var yRotation = m_Rand.NextFloat(-m_LightRotation, m_LightRotation);
        m_Light.transform.rotation = Quaternion.Euler(
            xRotation,
            yRotation,
            0f
        );

        SimulationManager.ReportMetric(m_LightingInfoDefinition, new[]
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

