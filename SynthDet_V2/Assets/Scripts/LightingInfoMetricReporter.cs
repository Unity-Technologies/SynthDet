using System;
using System.Collections.Generic;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Reports metrics for light intensity, light colour, and rotation for objects that contain a light component and are tagged with <see cref="LightingInfoMetricReporterTag"/>.
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Lighting Info Metric Reporter")]
    public class LightingInfoMetricReporter : Randomizer
    {
        const string k_LightingInfoMetricGuid = "939248EE-668A-4E98-8E79-E7909F034A47";
        MetricDefinition m_LightingInfoMetricDefinition;
        bool initialized;
    
        protected override void OnUpdate()
        {
            if (!initialized)
            {
                m_LightingInfoMetricDefinition = DatasetCapture.RegisterMetricDefinition("Per Frame Lighting Info", $"Reports the enabled state, intensity, colour, and rotation of lights carrying a {nameof(LightingInfoMetricReporterTag)} component.", new Guid(k_LightingInfoMetricGuid));

                initialized = true;
            }
            var tags = tagManager.Query<LightingInfoMetricReporterTag>();
            ReportMetrics(tags);
        }

        void ReportMetrics(IEnumerable<GameObject> tags)
        {
            var infos = new List<MyLightInfo>();
            foreach (var tag in tags)
            {
                var light = tag.gameObject.GetComponent<Light>();
                var rotation = tag.transform.rotation;
                infos.Add(new MyLightInfo()
                {
                    lightName = tag.name,
                    intensity = light.intensity,
                    color = light.color,
                    x_rotation = rotation.eulerAngles.x,
                    y_rotation = rotation.eulerAngles.y,
                    z_rotation = rotation.eulerAngles.z,
                    enabled = light.enabled
                });
            }

            DatasetCapture.ReportMetric(m_LightingInfoMetricDefinition, infos.ToArray());
        }
    
        struct MyLightInfo
        {
            public string lightName;
            public bool enabled;
            public float intensity;
            public Color color;
            // ReSharper disable InconsistentNaming
            public float x_rotation;
            // ReSharper disable InconsistentNaming
            public float y_rotation;
            // ReSharper disable InconsistentNaming
            public float z_rotation;
        }
    }
}
