using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SynthDet.RandomizerTags;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;
using Object = UnityEngine.Object;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Reports metrics for rotation, world position, scale, and label id of objects that are tagged with <see cref="ForegroundObjectMetricReporterTag"/>.
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Foreground Object Metric Reporter")]
    public class ForegroundObjectMetricReporter : Randomizer
    {
        const string k_LayerOneForegroundObjectPlacementInfoMetricGuid = "061E08CC-4428-4926-9933-A6732524B52B";
        MetricDefinition m_ForegroundObjectPlacementMetricDefinition;
        public IdLabelConfig labelConfigForObjectPlacementMetrics;
        Dictionary<GameObject, Labeling> m_LabelingComponentsCache;
        bool initialized;
        
    
        protected override void OnUpdate()
        {
            if (!initialized)
            {
                m_ForegroundObjectPlacementMetricDefinition = DatasetCapture.RegisterMetricDefinition("Per Frame Foreground Object Placement Info", $"Reports the world position, scale, rotation, and label id of objects carrying a {nameof(ForegroundObjectMetricReporterTag)} component.", new Guid(k_LayerOneForegroundObjectPlacementInfoMetricGuid));
                m_LabelingComponentsCache = new Dictionary<GameObject, Labeling>();
                if (labelConfigForObjectPlacementMetrics == null)
                {
                    var perceptionCamera = Object.FindObjectOfType<PerceptionCamera>();
                    if (perceptionCamera && perceptionCamera.labelers.Count > 0 && perceptionCamera.labelers[0] is BoundingBox2DLabeler boundingBox2DLabeler)
                    {
                        labelConfigForObjectPlacementMetrics = boundingBox2DLabeler.idLabelConfig;
                    }
                }

                initialized = true;
            }
            var tags = tagManager.Query<ForegroundObjectMetricReporterTag>();
            ReportMetrics(tags);
        }

        void ReportMetrics(IEnumerable<GameObject> tags)
        {
            var objectStates = new JArray();
            foreach (var tag in tags)
            {
                var taggedObject = tag.gameObject;
                if (!m_LabelingComponentsCache.TryGetValue(taggedObject, out var labeling))
                {
                    labeling = taggedObject.GetComponentInChildren<Labeling>();
                    m_LabelingComponentsCache.Add(taggedObject, labeling);
                }
            
                int labelId = -1;
                if (labelConfigForObjectPlacementMetrics.TryGetMatchingConfigurationEntry(labeling, out IdLabelEntry labelEntry))
                {
                    labelId = labelEntry.id;
                }
            
                var jObject = new JObject();
                jObject["label_id"] = labelId;
                var rotationEulerAngles = (float3)taggedObject.transform.rotation.eulerAngles;
                jObject["rotation"] = new JRaw($"[{rotationEulerAngles.x}, {rotationEulerAngles.y}, {rotationEulerAngles.z}]");
                var position = (float3)taggedObject.transform.position;
                jObject["position"] = new JRaw($"[{position.x}, {position.y}, {position.z}]");
                var scale = (float3)taggedObject.transform.localScale;
                jObject["scale"] = new JRaw($"[{scale.x}, {scale.y}, {scale.z}]");
                objectStates.Add(jObject);
            }
            
            DatasetCapture.ReportMetric(m_ForegroundObjectPlacementMetricDefinition, objectStates.ToString(Formatting.Indented));
        }
    }
}
