using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;

[RequireComponent(typeof(Light))]
[AddComponentMenu("Perception/RandomizerTags/MyLightSwitcherTag")]
public class MyLightSwitcherTag : RandomizerTag
{
    public float enabledProbability;
    public void Act(float rawInput)
    {
        var light = gameObject.GetComponent<Light>();
        light.enabled = rawInput < enabledProbability;
    }
}
