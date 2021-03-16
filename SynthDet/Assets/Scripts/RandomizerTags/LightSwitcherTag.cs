using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.RandomizerTags
{
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("SynthDet/RandomizerTags/MyLightSwitcherTag")]
    public class LightSwitcherTag : RandomizerTag
    {
        public float enabledProbability;
        public void Act(float rawInput)
        {
            var light = gameObject.GetComponent<Light>();
            light.enabled = rawInput < enabledProbability;
        }
    }
}
