using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.SimViz.Scenarios;
using Object = System.Object;
#if UNITY_EDITOR
using System.IO;
using UnityEditor.SceneManagement;
#endif

namespace RuntimeTests.ScenariosTests
{
    public class ScenarioManagerTests : IPrebuildSetup
    {
        public IEnumerable<ScenarioAsset> ScenariosWithoutPermutations(ScenarioManager scenarioManager) => scenarioManager.scenarioAssets.Where(s => s.parameterSelectors.Count == 0);
#if UNITY_EDITOR
        public List<EditorBuildSettingsScene> editorBuildSettingsScenes = new List<EditorBuildSettingsScene>();
#endif
        public List<SceneReference> testScenes = new List<SceneReference>();
        public string testSceneDirectory = "packages/com.unity.simviz/Tests/Runtime/ScenarioTests/Scenes";

#if UNITY_EDITOR
        string[] FindScenes()
        {
            return AssetDatabase.FindAssets("t:scene", new[] {testSceneDirectory}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }
#endif

        public void Setup()
        {
#if UNITY_EDITOR

            var scenePaths = FindScenes();
            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                EditorSceneManager.CloseScene(scene, false);

                editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
#else
            // TODO:  Get tests working in player.  Currently scenes are not loaded so there is no manager.
#endif
        }


        [UnitySetUp]
        public IEnumerator SetupBeforeTest()
        {
            var asyncLoad = SceneManager.LoadSceneAsync(testSceneDirectory + "/BaseScene", LoadSceneMode.Additive);
            yield return asyncLoad;

            var scenarioManager = GameObject.FindObjectOfType<ScenarioManager>();
            scenarioManager.useScheduler = true;
            scenarioManager.schedulingSource = SchedulingSource.CustomScheduling;
            scenarioManager.terminateOnCompletion = false;

            CarScenarioLogger.Instance.ClearMemoryLog();

#if UNITY_EDITOR
            var scenePaths = FindScenes();
            var scene = new SceneReference();
            foreach (var targetPath in scenePaths)
            {
                scene.ScenePath = targetPath;
                testScenes.Add(scene);
            }
#endif

            CarScenarioLogger.Instance.ClearMemoryLog();

            // Speed up the tests.
            QualitySettings.vSyncCount = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            testScenes.Clear();

            // Ensure all scenes are closed in the editor to start with a clean slate.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.path.StartsWith("Assets/InitTestScene"))
                {
                    // Reload the test base scene that contains scenario manager.
                    var op = SceneManager.UnloadSceneAsync(scene);
                    yield return op;
                    //The current index has been removed, so backstep one
                    i--;
                }
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
#if UNITY_EDITOR
            var allpaths = AssetDatabase.GetAllAssetPaths();
            foreach (var targetPath in allpaths)
            {
                if (targetPath.Contains("Assets") && targetPath.Contains("Scenes") && !targetPath.Contains("Sample"))
                {
                    var finalSceneFileName = targetPath.Remove(0, targetPath.LastIndexOf("/") + 1);
                    var copyPath = "Assets/Scenes/" + finalSceneFileName;

                    foreach (var scene in editorBuildSettingsScenes)
                    {
                        editorBuildSettingsScenes.Remove(scene);
                    }

                    AssetDatabase.DeleteAsset(copyPath);
                }
            }
#endif
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator BasicTest()
        {
            var scenarioManager = GameObject.FindObjectOfType<ScenarioManager>();

            // Schedule scenarios for execution all at once.
            var scenarios = ScenariosWithoutPermutations(scenarioManager);

            foreach (var scenario in scenarios)
                scenarioManager.ScheduleScenario(scenario.name);

            Assert.AreEqual(scenarios.Count(), scenarioManager.GetQueueLength());

            yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);

            // Check log
            var log = CarScenarioLogger.Instance.GetMemoryLog();
            var scenarioData = log.Where(evt => evt.Name == "StartScenario")
                .Join(log.Where(evt => evt.Name == "EndScenario"), startEvent => startEvent.ScenarioId,
                    endEvent => endEvent.ScenarioId,
                    (startEvent, endEvent) => new { startFrame = startEvent.FrameCount, endFrame = endEvent.FrameCount });
            Assert.AreEqual(scenarios.Count(), scenarioData.Count());
            Array.ForEach(scenarioData.ToArray(), scenario => Assert.AreEqual(scenarioManager.maxScenarioLength, scenario.endFrame - scenario.startFrame));
        }

        [UnityTest]
        public IEnumerator ProgressiveScheduling()
        {
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            // Schedule scenarios incrementally.
            var scenarios = ScenariosWithoutPermutations(scenarioManager);
            foreach (var scenario in scenarios)
            {
                scenarioManager.ScheduleScenario(scenario.name);
                yield return new WaitUntil(() => Time.frameCount % 50 == 0);
            }

            yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);

            // This query is a data transformation on the log events.  It joins together the StartScenario/EndScenario pairs by their ScenarioID,
            // and produces an enumerable that contains one entry per scenario with the field metadata from the events for the scenario.
            // With this data model it is easier to do validation.
            var log = CarScenarioLogger.Instance.GetMemoryLog();
            var scenarioData = log.Where(evt => evt.Name == "StartScenario")
                .Join(log.Where(evt => evt.Name == "EndScenario"),
                    startEvent => startEvent.ScenarioId,
                    endEvent => endEvent.ScenarioId,
                    (startEvent, endEvent) =>
                        new
                        {
                            startFrame = startEvent.FrameCount,
                            endFrame = endEvent.FrameCount
                        });
            Assert.AreEqual(scenarios.Count(), scenarioData.Count());
            Array.ForEach(scenarioData.ToArray(), scenario => Assert.AreEqual(scenarioManager.maxScenarioLength, scenario.endFrame - scenario.startFrame));

            // Clear the log before proceeding.
            CarScenarioLogger.Instance.ClearMemoryLog();

            // Schedule scenarios one after the other in sequence.
            foreach (var scenario in scenarios)
            {
                scenarioManager.ScheduleScenario(scenario.name);
                yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);
            }

            // Recompute scenario data model
            scenarioData = log.Where(evt => evt.Name == "StartScenario")
                .Join(log.Where(evt => evt.Name == "EndScenario"),
                    startEvent => startEvent.ScenarioId,
                    endEvent => endEvent.ScenarioId,
                    (startEvent, endEvent) =>
                        new
                        {
                            startFrame = startEvent.FrameCount,
                            endFrame = endEvent.FrameCount
                        });
            Assert.AreEqual(scenarios.Count(), scenarioData.Count());
            Array.ForEach(scenarioData.ToArray(), scenario => Assert.AreEqual(scenarioManager.maxScenarioLength, scenario.endFrame - scenario.startFrame));
        }

        [UnityTest]
        public IEnumerator ScenarioWithNullScene()
        {
            LogAssert.ignoreFailingMessages = true;
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            scenarioManager.scenarioAssets.Clear();
            var scenario = ScriptableObject.CreateInstance<ScenarioAsset>();
            scenario.name = "DummyScenario";
            scenario.scenes = new SceneList() { null };
            scenarioManager.scenarioAssets.Add(scenario);
            Assert.Catch<InvalidOperationException>(() =>
            {
                scenarioManager.ScheduleScenario(scenario.name);
            });
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(ScenarioManagerState.Stopped, scenarioManager.State);
            Assert.AreEqual(0, CarScenarioLogger.Instance.GetMemoryLog().Count);
            Assert.AreEqual(0, scenarioManager.GetQueueLength());
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerable ScheduleByScenarioAsset()
        {
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            scenarioManager.scenarioAssets.Clear();
            var scenario = ScriptableObject.CreateInstance<ScenarioAsset>();
            scenario.scenes.Add(testScenes[0]);
            scenario.scenes.Add(testScenes[1]);
            scenario.name = "DynamicScene";
            scenarioManager.ScheduleScenario(scenario);
            yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);

            // Check log
            var log = CarScenarioLogger.Instance.GetMemoryLog();
            var scenarioData = log.Where(evt => evt.Name == "StartScenario")
                .Join(log.Where(evt => evt.Name == "EndScenario"), startEvent => startEvent.ScenarioId,
                    endEvent => endEvent.ScenarioId,
                    (startEvent, endEvent) => new { startFrame = startEvent.FrameCount, endFrame = endEvent.FrameCount });
            Assert.AreEqual(1, scenarioData.Count());
            Assert.AreEqual(scenarioManager.maxScenarioLength, scenarioData.First().endFrame - scenarioData.First().startFrame);

        }

        [UnityTest]
        public IEnumerator ScenarioWithNoScenes()
        {
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            var originalSceneCount = SceneManager.sceneCount;
            scenarioManager.scenarioAssets.Clear();
            var scenario = ScriptableObject.CreateInstance<ScenarioAsset>();
            scenarioManager.scenarioAssets.Add(scenario);
            scenarioManager.ScheduleScenario(scenario.name);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(ScenarioManagerState.Stopped, scenarioManager.State);
            Assert.AreEqual(originalSceneCount, SceneManager.sceneCount); // Should not have loaded other scenes.
            Assert.AreEqual(0, CarScenarioLogger.Instance.GetMemoryLog().Count);
            Assert.AreEqual(0, scenarioManager.GetQueueLength());
        }

        [UnityTest]
        public IEnumerator ScenarioNotFound()
        {
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            scenarioManager.scenarioAssets.Clear();
            var scenario = ScriptableObject.CreateInstance<ScenarioAsset>();
            scenario.name = "foo";
            scenarioManager.scenarioAssets.Add(scenario);
            Assert.Throws<ArgumentException>(() => scenarioManager.ScheduleScenario("bar"));

            yield return null;
        }

        // Eventually we will need a test like this one but passing on this for now.  The issue is synchronization of getting all scenes loaded and objects
        // activated simultaneously, then having everything running the subsequent frame.  There are a few strategies to deal with this, including
        // moving to a model by which all dynamic objects are activated only when scenario manager is ready, caching all objects and manually activating
        // from scenariomanager, etc., but for now we will just operate with the fact that everything is activated at NEARLY, but not exactly the same time.
        /*[UnityTest]
        public IEnumerator Progression()
        {
            // This test is intended to measure specific activities as seen by the scene objects.

            yield return ReadyTestAsync();
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            scenarioManager.ScheduleScenario(scenarioManager.scenarios[0].Name);
            yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);
            var ball = Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>().First(obj => obj.name == "Ball");
            var counter = ball.GetComponent<Counter>();
            var finalCount = counter.Count;

            Assert.AreEqual(scenarioManager.maxScenarioLength, finalCount);

            yield return null;
        }*/

        [UnityTest]
        public IEnumerator Permutations()
        {
            var scenarioManager = UnityEngine.Object.FindObjectOfType<ScenarioManager>();

            scenarioManager.ScheduleScenario("MyScenario5");

            Assert.AreEqual(24, scenarioManager.GetQueueLength());

            // TODO: Check for IsEmpty instead
            yield return new WaitUntil(() => scenarioManager.GetQueueLength() == 0);

            // Check log
            var log = CarScenarioLogger.Instance.GetMemoryLog();
            var scenarios = log.Where(evt => evt.Name == "StartScenario")
                .Join(log.Where(evt => evt.Name == "EndScenario"), startEvent => startEvent.ScenarioId,
                    endEvent => endEvent.ScenarioId,
                    (startEvent, endEvent) => new { startFrame = startEvent.FrameCount, endFrame = endEvent.FrameCount });
            Assert.AreEqual(24, scenarios.Count());
            Array.ForEach(scenarios.ToArray(), scenario => Assert.AreEqual(scenarioManager.maxScenarioLength, scenario.endFrame - scenario.startFrame));
        }
#endif
    }
}

