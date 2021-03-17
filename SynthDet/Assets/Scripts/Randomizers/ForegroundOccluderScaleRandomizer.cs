using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("Perception/My Foreground Occluder Scale Randomizer")]
    public class ForegroundOccluderScaleRandomizer : Randomizer
    {
        public FloatParameter scale = new FloatParameter { value = new UniformSampler(0.5f, 6f) };

        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<ForegroundOccluderScaleRandomizerTag>();
            foreach (var tag in tags)
            {
                tag.transform.localScale = Vector3.one * scale.Sample();
            }
        }
    }
}