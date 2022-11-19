using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.Perception.GroundTruth.DataModel;
using UnityEngine.Perception.GroundTruth.MetadataReporter;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Runtime
{
    [RequireComponent(typeof(Volume))]
    public class PostProcessingMetadataTag : MetadataTag
    {
        ColorAdjustments m_ColorAdjustments;
        VolumeRandomizer m_VolumeRandomizer;

        protected override string key => "PostProcessingMetadata";
        
        void Awake()
        {
            var volume = GetComponent<Volume>();
            m_ColorAdjustments = (ColorAdjustments)volume.profile.components.Find(comp => comp is ColorAdjustments);
            m_VolumeRandomizer = ScenarioBase.activeScenario.GetRandomizer<VolumeRandomizer>();
        }

        protected override void GetReportedValues(IMessageBuilder builder)
        {
            builder.AddBool("blurWasApplied", m_VolumeRandomizer.blurWasApplied);
            builder.AddFloat("blurIntensity", m_VolumeRandomizer.prevBlurIntensity);
            builder.AddFloat("contrast", m_ColorAdjustments.contrast.value);
            builder.AddFloat("saturation", m_ColorAdjustments.saturation.value);
        }
    }
}
