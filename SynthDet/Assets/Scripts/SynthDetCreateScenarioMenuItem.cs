using SynthDet.Randomizers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers.SampleRandomizers;
using UnityEngine.Perception.Randomization.Scenarios;
using BackgroundObjectPlacementRandomizer = SynthDet.Randomizers.BackgroundObjectPlacementRandomizer;

namespace SynthDet.MenuItems
{
    public static class SynthDetCreateScenarioMenuItem
    {
        [MenuItem("SynthDet/Create Scenario")]
        static void CreateScenario()
        {
            var scenarioObj = new GameObject("Scenario");

            var scenario = scenarioObj.AddComponent<FixedLengthScenario>();
            scenario.constants.totalIterations = 1000;
            
            scenario.AddRandomizer(new BackgroundObjectPlacementRandomizer());
            scenario.AddRandomizer(new ForegroundOccluderPlacementRandomizer());
            scenario.AddRandomizer(new ForegroundOccluderScaleRandomizer());
            scenario.AddRandomizer(new TextureRandomizer());
            scenario.AddRandomizer(new HueOffsetRandomizer());
            scenario.AddRandomizer(new DualLayerForegroundObjectPlacementRandomizer());
            scenario.AddRandomizer(new RotationRandomizer());
            scenario.AddRandomizer(new LightRandomizer());
            scenario.AddRandomizer(new ForegroundScaleRandomizer());
            scenario.AddRandomizer(new CameraRandomizer());
            scenario.AddRandomizer(new UnifiedRotationRandomizer());
            scenario.AddRandomizer(new ForegroundObjectMetricReporter());
            scenario.AddRandomizer(new LightingInfoMetricReporter());
            scenario.AddRandomizer(new CameraPostProcessingMetricReporter());

            Selection.activeGameObject = scenarioObj;
        }
    }
}
