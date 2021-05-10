using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("SynthDet/Foreground Scale Randomizer for Normalized Object Bounds")]
    public class ForegroundScaleRandomizerForNormalizedMeshes : Randomizer
    {
        public FloatParameter scale = new FloatParameter { value = new UniformSampler(0.15f, 0.60f) };

        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<ForegroundScaleRandomizerTag>();
            foreach (var tag in tags)
            {
                tag.transform.localScale *= scale.Sample();
            }
        }
    }
}