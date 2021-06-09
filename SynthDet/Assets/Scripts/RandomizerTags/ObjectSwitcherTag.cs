using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.RandomizerTags
{
    [AddComponentMenu("SynthDet/RandomizerTags/ObjectSwitcherTag")]
    public class ObjectSwitcherTag : RandomizerTag
    {
        public int num;
        public GameObject[] objects;
    }
}
