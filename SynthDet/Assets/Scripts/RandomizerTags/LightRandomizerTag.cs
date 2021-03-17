using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.RandomizerTags
{
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("SynthDet/RandomizerTags/MyLightRandomizerTag")]
    public class LightRandomizerTag : RandomizerTag
    {
        public float minIntensity;
        public float maxIntensity;

        public void SetIntensity(float rawIntensity)
        {
            var light = gameObject.GetComponent<Light>();
            var scaledIntensity = rawIntensity * (maxIntensity - minIntensity) + minIntensity;
            light.intensity = scaledIntensity;
        }
    }
}
