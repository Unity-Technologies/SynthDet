using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

namespace SynthDet.RandomizerTags
{
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Perception/RandomizerTags/My Camera Randomizer Tag")]
    public class CameraRandomizerTag : RandomizerTag { }
}
