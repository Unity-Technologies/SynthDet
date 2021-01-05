using UnityEngine;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;

[AddComponentMenu("Perception/RandomizerTags/MyLightSwitcherTag")]
public class MyLightSwitcherTag : RandomizerTag
{
    public float enabledProbability;
    public void Act(float rawInput)
    {
        var light = gameObject.GetComponent<Light>();
        if (light)
        {
            light.enabled = rawInput < enabledProbability;
        }
    }
}
