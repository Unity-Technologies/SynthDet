using System;
using System.Collections.Generic;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Reports post processing metrics for object that contain a post-processing volume component and are tagged with <see cref="CameraPostProcessingMetricReporter"/>. 
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/My Camera Post-processing Metric Reporter")]
    public class CameraPostProcessingMetricReporter : Randomizer
    {
        const string k_CameraPostProcessingMetricGuid = "a4b2253c-0eb2-4a90-9b20-6e77f1d13286";
        MetricDefinition m_CameraPostProcessingMetricDefinition;
        Dictionary<GameObject, Volume> m_PostProcessingVolumeCache;

        protected override void OnAwake()
        {
            m_CameraPostProcessingMetricDefinition = DatasetCapture.RegisterMetricDefinition("Per Frame Camera Post Processing Info", $"Reports post-processing effects of cameras (or other objects with a Volume) carrying a {nameof(CameraPostProcessingMetricReporter)} component.", new Guid(k_CameraPostProcessingMetricGuid));
            m_PostProcessingVolumeCache = new Dictionary<GameObject, Volume>();
        }
    
        protected override void OnUpdate()
        {
            var tags = tagManager.Query<CameraPostProcessingMetricReporterTag >();
            ReportMetrics(tags);
        }

        void ReportMetrics(IEnumerable<RandomizerTag> tags)
        {
            var infos = new List<MyVolumeInfo>();
            foreach (var tag in tags)
            {
                var taggedObject = tag.gameObject;
                if (!m_PostProcessingVolumeCache.TryGetValue(taggedObject, out var volume))
                {
                    volume = taggedObject.GetComponentInChildren<Volume>();
                    m_PostProcessingVolumeCache.Add(taggedObject, volume);
                }

                var vignette = (Vignette)volume.profile.components.Find(comp => comp is Vignette);
                var colorAdjustments = (ColorAdjustments)volume.profile.components.Find(comp => comp is ColorAdjustments);
                var depthOfField = (DepthOfField)volume.profile.components.Find(comp => comp is DepthOfField);
                infos.Add(new MyVolumeInfo()
                {
                    vignetteIntensity = vignette.intensity.value,
                    saturation = colorAdjustments.saturation.value,
                    contrast = colorAdjustments.contrast.value,
                    gaussianDofStart = depthOfField.gaussianStart.value,
                    gaussianDofEnd = depthOfField.gaussianEnd.value,
                    gaussianDofRadius = depthOfField.gaussianMaxRadius.value
                });
            }

            DatasetCapture.ReportMetric(m_CameraPostProcessingMetricDefinition, infos.ToArray());
        }
    
        struct MyVolumeInfo
        {
            public float vignetteIntensity;
            public float saturation;
            public float contrast;
            public float gaussianDofStart;
            public float gaussianDofEnd;
            public float gaussianDofRadius;
        }
    }
}