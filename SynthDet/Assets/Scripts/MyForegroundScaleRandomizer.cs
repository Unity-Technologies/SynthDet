using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;

[Serializable]
[AddRandomizerMenu("Perception/My Foreground Scale Randomizer")]
public class MyForegroundScaleRandomizer : Randomizer
{
    public FloatParameter scale;

    protected override void OnIterationStart()
    {
        var tags = tagManager.Query<MyForegroundScaleRandomizerTag>();
        foreach (var tag in tags)
        {
            tag.transform.localScale = Vector3.one * scale.Sample();
        }
    }
}