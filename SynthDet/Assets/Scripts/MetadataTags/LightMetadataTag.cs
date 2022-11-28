using UnityEngine;
using UnityEngine.Perception.GroundTruth.DataModel;
using UnityEngine.Perception.GroundTruth.MetadataReporter;
using UnityEngine.Rendering.HighDefinition;

namespace SynthDet.MetadataTags
{
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(HDAdditionalLightData))]
    [AddComponentMenu("SynthDet/SynthDet Light Metadata Tag")]
    public class LightMetadataTag : MetadataTag
    {
        HDAdditionalLightData m_LightData;

        void Awake()
        {
            m_LightData = GetComponent<HDAdditionalLightData>();
        }
        
        protected override string key => "LightMetadata";
        
        protected override void GetReportedValues(IMessageBuilder builder)
        {
            var color = m_LightData.color;
            builder.AddFloatArray("color", new [] { color.r, color.g, color.b, color.a });
            builder.AddFloat("intensity", m_LightData.intensity);
        }
    }
}
