using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("SynthDet/Foreground Scale Randomizer For Non-Normalized Meshes")]
    public class ForegroundScaleRandomizerForNonNormalizedMeshes : Randomizer
    {
        public FloatParameter scale = new FloatParameter { value = new UniformSampler(4f, 8f) };

        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<ForegroundScaleRandomizerTag>();
            foreach (var tag in tags)
            {
                tag.transform.localScale = Vector3.one * scale.Sample();
            }
        }
    }
}