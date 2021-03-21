using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("Perception/My Light Randomizer")]
    public class LightRandomizer : Randomizer
    {
        public FloatParameter lightIntensityParameter = new FloatParameter { value = new UniformSampler(0f, 1f) };
        public ColorRgbParameter lightColorParameter = new ColorRgbParameter
        {
            red = new UniformSampler(0.4f, 1f),
            green = new UniformSampler(0.4f, 1f),
            blue = new UniformSampler(0.4f, 1f),
            alpha = new ConstantSampler(1f),
        };
        public FloatParameter auxParameter = new FloatParameter { value = new UniformSampler(0f, 1f) };
    
        protected override void OnIterationStart()
        {
            var randomizerTags = tagManager.Query<LightRandomizerTag>();
            foreach (var tag in randomizerTags)
            {
                var light = tag.GetComponent<Light>();
                light.color = lightColorParameter.Sample();
                tag.SetIntensity(lightIntensityParameter.Sample());
            }
        
            var switcherTags = tagManager.Query<LightSwitcherTag>();
            foreach (var tag in switcherTags)
            {
                tag.Act(auxParameter.Sample());
            }
        }
    }
}