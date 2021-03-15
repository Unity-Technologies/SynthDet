using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;

[Serializable]
[AddRandomizerMenu("Perception/My Foreground Occluder Scale Randomizer")]
public class MyForegroundOccluderScaleRandomizer : Randomizer
{
    public FloatParameter scale;

    protected override void OnIterationStart()
    {
        var tags = tagManager.Query<MyForegroundOccluderScaleRandomizerTag>();
        foreach (var tag in tags)
        {
            tag.transform.localScale = Vector3.one * scale.Sample();
        }
    }
}