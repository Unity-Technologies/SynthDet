using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FloatParameter = UnityEngine.Perception.Randomization.Parameters.FloatParameter;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Randomizes the blur, contract, saturation, and grain properties of the scene's volume profile
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Camera Randomizer")]
    public class CameraRandomizer : Randomizer
    {
        public FloatParameter blurParameter = new FloatParameter { value = new UniformSampler(0f, 4f) };
        public FloatParameter contrastParameter = new FloatParameter { value = new UniformSampler(-10f, 10f) };
        public FloatParameter saturationParameter = new FloatParameter { value = new UniformSampler(-10f, 10f) };

        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<CameraRandomizerTag>();
            foreach (var tag in tags)
            {
                var volume = tag.gameObject.GetComponent<Volume>();
                if (volume && volume.profile)
                {
                    var dof = (DepthOfField) volume.profile.components.Find(comp => comp is DepthOfField);
                    if (dof)
                    {
                        var val = blurParameter.Sample();
                        dof.gaussianStart.value = val;
                    }

                    var colorAdjust = (ColorAdjustments) volume.profile.components.Find(comp => comp is ColorAdjustments);
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