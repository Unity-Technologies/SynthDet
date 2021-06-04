using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.RandomizerTags
{
    [AddComponentMenu("SynthDet/RandomizerTags/BackdropSwitcherTag")]
    public class BackdropSwitcherTag : RandomizerTag
    {
        public float enabledProbability;
        public void Act(float rawInput)
        {
            var renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.enabled = rawInput < enabledProbability;
        }
    }
}
