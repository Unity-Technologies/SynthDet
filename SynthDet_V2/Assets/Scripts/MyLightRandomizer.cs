using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Perception.Randomization.Parameters;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;

[Serializable]

[AddRandomizerMenu("Perception/My Light Randomizer")]
public class MyLightRandomizer : Randomizer
{
    public FloatParameter lightIntensityParameter;
    public ColorRgbParameter lightColorParameter;
    public FloatParameter auxParameter;
    protected override void OnIterationStart()
    {
        var taggedObjects = tagManager.Query<MyLightRandomizerTag>();
        foreach (var taggedObject in taggedObjects)
        {
            var light = taggedObject.GetComponent<Light>();
            if (light)
            {                
                light.color = lightColorParameter.Sample();
            }

            var tag = taggedObject.GetComponent<MyLightRandomizerTag>();
            if (tag)
            {
                tag.SetIntensity(lightIntensityParameter.Sample());
            }
        }
        
        taggedObjects = tagManager.Query<MyLightSwitcherTag>();
        foreach (var taggedObject in taggedObjects)
        {
            var tag = taggedObject.GetComponent<MyLightSwitcherTag>();
            if (tag)
            {
                tag.Act(auxParameter.Sample());
            }
        }
    }
}