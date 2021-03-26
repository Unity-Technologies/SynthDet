using System.Collections;
using System.Collections.Generic;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;

namespace SynthDet
{
    static class Utilities
    {
        public static T GetOrAddComponent<T>(GameObject gObj) where T : Component
        {
            var component = gObj.GetComponent<T>();
            if (!component)
                component = gObj.AddComponent<T>();

            return component;
        }
    }
}
