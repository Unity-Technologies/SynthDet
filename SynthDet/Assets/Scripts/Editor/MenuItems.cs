using System;
using SynthDet.Randomizers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers.SampleRandomizers;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.SceneManagement;
using BackgroundObjectPlacementRandomizer = SynthDet.Randomizers.BackgroundObjectPlacementRandomizer;
using ForegroundObjectPlacementRandomizer = SynthDet.Randomizers.ForegroundObjectPlacementRandomizer;

namespace SynthDet
{
    public static class MenuItems
    {
        [MenuItem("SynthDet/Open SynthDet Scene")]
        static void OpenSynthDetScene()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SynthDet.unity");
            }
            catch (Exception e)
            {
                Debug.LogError("Could not open the SynthDet Scene. Make sure the file Assets/Scenes/SynthDet.unity exists and is a valid Scene.");
                Debug.LogException(e);
            }
        }
        
        [MenuItem("SynthDet/Recreate Default SynthDet Scenario")]
        static void CreateDefaultScenario()
        {
            var scenarioObj = new GameObject("Scenario");

            var scenario = scenarioObj.AddComponent<FixedLengthScenario>();
            scenario.constants.totalIterations = 1000;

            scenario.AddRandomizer(new BackgroundObjectPlacementRandomizer());
            scenario.AddRandomizer(new ForegroundObjectPlacementRandomizer());
            scenario.AddRandomizer(new ForegroundOccluderPlacementRandomizer());
            scenario.AddRandomizer(new ForegroundOccluderScaleRandomizer());
            scenario.AddRandomizer(new ForegroundScaleRandomizer());
            scenario.AddRandomizer(new TextureRandomizer());
            scenario.AddRandomizer(new HueOffsetRandomizer());
            scenario.AddRandomizer(new RotationRandomizer());
            scenario.AddRandomizer(new UnifiedRotationRandomizer());
            scenario.AddRandomizer(new LightRandomizer());
            scenario.AddRandomizer(new CameraRandomizer());
            scenario.AddRandomizer(new ForegroundObjectMetricReporter());
            scenario.AddRandomizer(new LightingInfoMetricReporter());
            scenario.AddRandomizer(new CameraPostProcessingMetricReporter());

            Selection.activeGameObject = scenarioObj;
        }
    }
}
