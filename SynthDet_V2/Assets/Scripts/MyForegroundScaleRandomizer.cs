using System;
using UnityEngine;
using UnityEngine.Experimental.Perception.Randomization.Parameters;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;

[Serializable]
[AddRandomizerMenu("Perception/My Foreground Scale Randomizer")]
public class MyForegroundScaleRandomizer : Randomizer
{
    public FloatParameter scale;

    protected override void OnIterationStart()
    {
        var taggedObjects = tagManager.Query<MyForegroundScaleRandomizerTag>();
        foreach (var taggedObject in taggedObjects)
        {
            taggedObject.transform.localScale = Vector3.one * scale.Sample();
        }
    }
}