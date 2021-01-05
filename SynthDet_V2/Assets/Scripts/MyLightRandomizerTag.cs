using UnityEngine;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;

[AddComponentMenu("Perception/RandomizerTags/MyLightRandomizerTag")]
public class MyLightRandomizerTag : RandomizerTag
{
    public float minIntensity;
    public float maxIntensity;

    public void SetIntensity(float rawIntensity)
    {
        var light = gameObject.GetComponent<Light>();
        if (light)
        {
            var scaledIntensity = rawIntensity * (maxIntensity - minIntensity) + minIntensity;
            light.intensity = scaledIntensity;
        }
    }
}
