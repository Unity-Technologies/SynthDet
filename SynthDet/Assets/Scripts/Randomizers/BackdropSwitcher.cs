using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("SynthDet/Backdrop Switcher")]
    public class BackdropSwitcher : Randomizer
    {
        public FloatParameter auxParameter = new FloatParameter { value = new UniformSampler(0f, 1f) };
        
        protected override void OnIterationStart()
        {
            var switcherTags = tagManager.Query<BackdropSwitcherTag>();
            foreach (var tag in switcherTags)
            {
                tag.Act(auxParameter.Sample());
            }
        }
    }
}