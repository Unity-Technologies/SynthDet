using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

[RequireComponent(typeof(Light))]
[AddComponentMenu("Perception/RandomizerTags/MyLightRandomizerTag")]
public class MyLightRandomizerTag : RandomizerTag
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
