using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using FloatParameter = UnityEngine.Perception.Randomization.Parameters.FloatParameter;
using Random = Unity.Mathematics.Random;
using VolumeRandomizerTag = SynthDet.RandomizerTags.VolumeRandomizerTag;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Randomizes the blur, contract, saturation, and grain properties of the scene's volume profile
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Volume Randomizer")]
    public class VolumeRandomizer : Randomizer
    {
        Random m_Rng;

        [Tooltip("The probability a blur effect will be applied. Valid range is [0,1]")]
        public float blurOnProbability = 0.3f;
        bool m_BlurWasApplied;
        
        [Tooltip("The intensity of the blur effect. Valid range is [0,1]")]
        public FloatParameter blurIntensity = new() { value = new UniformSampler(0, 1, true, 0, 1) };
        float m_BlurIntensity;
        
        public FloatParameter contrastParameter = new() { value = new UniformSampler(-10f, 10f) };
        public FloatParameter saturationParameter = new() { value = new UniformSampler(-10f, 10f) };
        
        public float prevBlurIntensity => m_BlurIntensity;
        public bool blurWasApplied => m_BlurWasApplied;
        
        protected override void OnScenarioStart()
        {
            m_Rng = SamplerState.CreateGenerator();
        }

        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<VolumeRandomizerTag>();
            foreach (var tag in tags)
            {
                var volume = tag.gameObject.GetComponent<Volume>();
                if (volume && volume.profile)
                {
                    var dof = (DepthOfField)volume.profile.components.Find(comp => comp is DepthOfField);
                    if (dof)
                    {
                        if (blurOnProbability is < 0 or > 1)
                        {
                            Debug.LogError($"{GetType().Name}: {nameof(blurOnProbability)} value of {blurOnProbability} is invalid. Valid range is [0,1]. Will not randomize blur.");
                        }
                        m_BlurWasApplied = m_Rng.NextFloat(0, 1) < blurOnProbability; 
                        if (m_BlurWasApplied)
                        {
                            m_BlurIntensity = blurIntensity.Sample();
                            var radius = m_BlurIntensity * 6f;
                            dof.active = true;
                            dof.farMaxBlur = radius;
                        }
                        else
                        {
                            dof.active = false;
                        }
                    }

                    var colorAdjust = (ColorAdjustments)volume.profile.components.Find(comp => comp is ColorAdjustments);
                    if (colorAdjust)
                    {
                        var val = contrastParameter.Sample();
                        colorAdjust.contrast.value = val;

                        val = saturationParameter.Sample();
                        colorAdjust.saturation.value = val;
                    }
                }
            }
        }
    }
}
