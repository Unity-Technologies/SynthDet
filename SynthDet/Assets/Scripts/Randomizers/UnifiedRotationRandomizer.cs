using System;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Randomizes the rotation of objects tagged with a RotationRandomizerTag
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Unified Rotation Randomizer")]
    public class UnifiedRotationRandomizer : Randomizer
    {
        /// <summary>
        /// Defines the range of random rotations that can be assigned to tagged objects
        /// </summary>
        public Vector3Parameter rotation = new Vector3Parameter
        {
            x = new UniformSampler(0, 360),
            y = new UniformSampler(0, 360),
            z = new UniformSampler(0, 360)
        };

        /// <summary>
        /// Randomizes the rotation of tagged objects at the start of each scenario iteration
        /// </summary>
        protected override void OnIterationStart()
        {
            var tags = tagManager.Query<UnifiedRotationRandomizerTag>();
            var rotationSample = Quaternion.Euler(rotation.Sample());
            foreach (var tag in tags)
                tag.transform.rotation = rotationSample;
        }
    }
}

