using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;

[Serializable]

[AddRandomizerMenu("Perception/My Light Randomizer")]
public class MyLightRandomizer : Randomizer
{
    public FloatParameter lightIntensityParameter;
    public ColorRgbParameter lightColorParameter;
    public FloatParameter auxParameter;
    protected override void OnIterationStart()
    {
        var randomizerTags = tagManager.Query<MyLightRandomizerTag>();
        foreach (var tag in randomizerTags)
        {
            var light = tag.GetComponent<Light>();
            light.color = lightColorParameter.Sample();
            tag.SetIntensity(lightIntensityParameter.Sample());
        }
        
        var switcherTags = tagManager.Query<MyLightSwitcherTag>();
        foreach (var tag in switcherTags)
        {
            tag.Act(auxParameter.Sample());
        }
    }
}