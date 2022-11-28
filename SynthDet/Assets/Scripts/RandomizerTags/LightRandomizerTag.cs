using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Rendering.HighDefinition;

namespace SynthDet.RandomizerTags
{
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(HDAdditionalLightData))]
    [AddComponentMenu("SynthDet/RandomizerTags/MyLightRandomizerTag")]
    public class LightRandomizerTag : RandomizerTag
    {
        HDAdditionalLightData m_LightData;
        
        public float minIntensity;
        public float maxIntensity;

        void Awake()
        {
            m_LightData = GetComponent<HDAdditionalLightData>();
        }
        
        public void SetIntensity(float rawIntensity)
        {
            var scaledIntensity = rawIntensity * (maxIntensity - minIntensity) + minIntensity;
            m_LightData.intensity = scaledIntensity;
        }
    }
}
