using System;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Perception;
using Random = Unity.Mathematics.Random;

public class LightingRandomizerSystem : JobComponentSystem
{
    static Guid k_LightingInfoGuid = new Guid("939248EE-668A-4E98-8E79-E7909F034A47");
    private Random m_Rand;
    private Light m_Light;
    private bool m_IsInitialized;
    MetricDefinition m_LightingInfoDefinition;

    protected override void OnCreate()
    {
        m_Rand = new Random(1);
    }

    private void Initialize()
    {
        var light = GameObject.Find("Directional Light");
        if (light == null)
            return;

        m_LightingInfoDefinition = SimulationManager.RegisterMetricDefinition("lighting info", id: k_LightingInfoGuid);
        m_Light = light.GetComponent<Light>();
        // To simulate phong shading we turn off shadows
        m_Light.shadows = LightShadows.None;
        m_IsInitialized = true;
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
            m_Rand.NextFloat(0.1f, 1f), m_Rand.NextFloat(0.1f, 1f), m_Rand.NextFloat(0.1f, 1f));
        var xRotation = m_Rand.NextFloat(-90f, 90f);
        var yRotation = m_Rand.NextFloat(-90f, 90f);
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

