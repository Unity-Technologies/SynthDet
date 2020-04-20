using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using Malee;
using System.IO;
using Unity.Simulation;

namespace UnityEngine.SimViz.Scenarios
{
	/// <summary>
	/// State machine enumeration for ScenarioManager.
	/// The state diagram is as follows:
	///     Stopped -> Loading -> Running -> Stopped
	/// Playing from editor begins execution in Loaded state.
	/// </summary>
	public enum ScenarioManagerState
	{
		Stopped, // Not running any scenarios
		Loading, // Scenarios are being loaded
		Running, // Running a scenario
	}

	public enum SchedulingSource
	{
		ScheduleAllOnInitialization,
		SimulationScheduler,
		CustomScheduling,
	}

	// A custom Promise class established from paradigms that exist in many async languages.  Our usage is limited here,
	// if we want to expand (with locking support and such) we should pull in some 3P libraries for this.
	internal class Promise<T>
	{
		public T value;
		public static implicit operator T(Promise<T> promise) => promise.value;
	}


	[Serializable]
	public class ScenariosList : ReorderableArray<ScenarioAsset> {}

    [Serializable]
    public struct ScenarioExecution
    {
        public ScenarioAsset Scenario;
        public List<ParameterSet> ScriptConfigurations;

    }

    [Serializable]
    public class ScenarioExecutionList : ReorderableArray<ScenarioExecution> {}

    /// <summary>
    /// ScenarioManager is a scene management class for simulation.  It can be configured with scenarios which are a combination of
    /// scenes.  At runtime, ScenarioManager will execute through the specified scenario set, running them in serial until completion.
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public bool terminateOnCompletion = true;
		public int maxScenarioLength = 10000;
		public float executionMultiplier = 1.0f;
        public int numberOfExecutionNodes = 1;
		[Reorderable]
		public ScenariosList scenarioAssets = new ScenariosList();
        public ScenarioExecutionList scenarioExecutionList = new ScenarioExecutionList();

		int m_CurrentScenarioStartFrame;
		Queue<ScenarioExecution> m_ExecutionQueue = new Queue<ScenarioExecution>();
		int m_BaseTargetFrameRate;
		float m_BaseTimeScale;

		ScenarioManagerState m_State;
		public ScenarioManagerState State
		{
			get => m_State;
			protected set
			{
				m_State = value;
				Console.WriteLine($"Scenario Manager changing state to {m_State}");
			}
		}

        // For the editor, the general flow is that only the loaded scenes are executed, and there is no scenario scheduling.  Tests
        // will override this and schedule scenarios directly, even if run from play mode in editor.
        // For player, we will use the scheduler for scene management.  This will be overriden in Awake and not read from serialized object.

        public bool useScheduler = false;
        public SchedulingSource schedulingSource = SchedulingSource.CustomScheduling;
		public TextAsset localAppParamsFile = null;
		private void DeactivateObjects()
		{
			// Disable all game objects so that execution is halted.
			// TODO: Look into destroying the objects and unloading the scenes immediately instead of just disabling them.
			var allObjects = FindObjectsOfType<GameObject>();
			foreach (var obj in allObjects)
			{
				if (obj.GetInstanceID() != gameObject.GetInstanceID() && !obj.scene.name.StartsWith("Init") && !obj.scene.name.Equals("DontDestroyOnLoad") && obj.scene != gameObject.scene)
					obj.SetActive(false);
			}
		}

		private void EndScenario()
		{
			// Log the end scenario event if we had started running it.
			if (State == ScenarioManagerState.Running)
			{
				CarScenarioLogger.Instance.LogEndCarScenario();
			}
			State = ScenarioManagerState.Stopped;

			DeactivateObjects();

			// If we are using the execution queue, now we can pop the scenario off the queue to indicate it is complete.
			if (useScheduler)
			{
				m_ExecutionQueue.Dequeue();
			}
		}

        public void GenerateAppParamsFiles()
        {
            try
            {
                ScenarioExecution scenarioExecution = new ScenarioExecution();
                List<ScenarioExecution> listOfScenarioExecutions = new List<ScenarioExecution>();

                // Serialize app params
                foreach (var scenarios in scenarioAssets)
                {
                    if (scenarios.parameterSelectors.Count > 0)
                    {
                        // Iterate over all permutations provided by the scenario permuters.
                        var parameterSetsEnumerable = scenarios.parameterSelectors.Select(sel => sel.parameterSetList).ToArray();
                        var allPermutations = GetPermutations(parameterSetsEnumerable, scenarios.parameterSelectors.Count - 1);
                        // Having 0 permutations is invalid
                        if (allPermutations.Any())
                        {
                            // Create the execution information for each scenario
                            foreach (var permutation in allPermutations)
                            {
                                scenarioExecution.Scenario = scenarios;
                                scenarioExecution.ScriptConfigurations = permutation.ToList();
                                listOfScenarioExecutions.Add(scenarioExecution);
                            }
                        }
                    }
                    else
                    {
                        scenarioExecution.Scenario = scenarios;
                        scenarioExecution.ScriptConfigurations = null;
                        listOfScenarioExecutions.Add(scenarioExecution);
                    }
                }

                // Distribute permutations ideally among number of nodes and serialize to appParams.json files
                WritePermutationsToFiles(listOfScenarioExecutions);

            }
            catch (Exception e)
            {
                Console.WriteLine("Encountered exception while creating App Params file: " + e);
                throw;
            }
        }

        public void WritePermutationsToFiles(List<ScenarioExecution> listOfScenarioExecution)
        {
            /*
                 * Distributing perms in each file, we get some files (y) with one extra in each of them
                 * x = number of equal permutations in all files; y = one extra permutation in y number of files;
                 * So,
                 * x = totalPermutations / numOfNodes;
                 * y = totalPermutations % numOfNodes;
                 * (numOfNodes - y) files will have x perms each
                 * y files will have x+1 perms each
                 * Here, extraPermutationsPerFile is y and permutationsPerFile is x
             */
            int i = 0, j = 0, appParamCount=0;
            int totalPermutations = listOfScenarioExecution.Count;
            int overflowPermutationFiles = totalPermutations % numberOfExecutionNodes;
            int permutationsPerFile = totalPermutations / numberOfExecutionNodes;
            listOfScenarioExecution.ToArray();

            // Writing x+1 permutations to y files
            while (i < listOfScenarioExecution.Count && appParamCount < overflowPermutationFiles)
            {
                scenarioExecutionList.Clear();
                j = i;
                while (j < i + permutationsPerFile + 1 && j < listOfScenarioExecution.Count)
                {
                    scenarioExecutionList.Add(listOfScenarioExecution[j]);
                    j++;
                }
                string jsonString = JsonUtility.ToJson(scenarioExecutionList);
                System.IO.File.WriteAllText(Application.dataPath + "/app-params" + appParamCount + ".json", jsonString);
                appParamCount++;
                i += permutationsPerFile + 1;
            }
            // Writing x permutations to (N-y) files
            while (i < listOfScenarioExecution.Count && appParamCount >= overflowPermutationFiles && appParamCount < numberOfExecutionNodes)
            {
                scenarioExecutionList.Clear();
                j = i;
                while (j < i + permutationsPerFile && j < listOfScenarioExecution.Count)
                {
                    scenarioExecutionList.Add(listOfScenarioExecution[j]);
                    j++;
                }
                string jsonString = JsonUtility.ToJson(scenarioExecutionList);
                System.IO.File.WriteAllText(Application.dataPath + "/app-params" + appParamCount + ".json", jsonString);
                appParamCount++;
                i += permutationsPerFile;
            }
        }

		public void ScheduleAllScenarios()
        {
            foreach (var scenario in scenarioAssets)
			{
                if (scenario == null)
				{
					Console.WriteLine("Found null scenario in scenario list, skipping");
				}
                ScheduleScenario(scenario.name);
			}
        }

		void ScheduleFromAppParams()
		{
			// Check whether we are in the editor or a cloud simulation run.
			// Read the local app_params.json from the Assets directory if we are in the editor.
			if (!Configuration.Instance.IsSimulationRunningInCloud())
			{
                Configuration.Instance.SimulationConfig.app_param_uri = "file://" + Application.dataPath + "/" + localAppParamsFile.name + ".json";
			}
			// Retrieve the app params using the DataCapture SDK.
			var appParams = Configuration.Instance.GetAppParams<ScenarioAppParams>();

            // Deserialize app params
            StreamReader r = new StreamReader( Application.dataPath + "/" + localAppParamsFile.name + ".json");
            string json = r.ReadToEnd();
            r.Close();

            // For this example we want to make sure app params were actually read.
            if (appParams == null)
			{
				Console.WriteLine("Unable to load appParams file");
			}
			else
            {
                scenarioExecutionList = JsonUtility.FromJson<ScenarioExecutionList>(json);
                foreach (var permutation in scenarioExecutionList)
                {
                    m_ExecutionQueue.Enqueue(permutation);
                }
            }
		}

        void Start()
		{
#if !UNITY_EDITOR
			// Always use the scheduler for the player.
			useScheduler = true;
#endif

			if (useScheduler)
			{
#if UNITY_EDITOR
                // In the editor, if using the scheduler then we need to immediately unload scenes to halt execution.
                UnloadRuntimeScenes();
#endif

				if (schedulingSource == SchedulingSource.ScheduleAllOnInitialization)
				{
					ScheduleAllScenarios();
				}
				else if (schedulingSource == SchedulingSource.SimulationScheduler)
				{
					// In this case we expect an app params file from which we will schedule
					ScheduleFromAppParams();
				}
			}
			else
			{
				// Set up logger
				CarScenarioLogger.Instance.LogStartCarScenario();

				// If not using the scheduler, then we begin in running State immediately.
				State = ScenarioManagerState.Running;
			}
		}


		private void OnEnable()
		{
			// Save current time scale and target frame rate
			m_BaseTimeScale = Time.timeScale;

			// Allow control over frame capture timing.
			QualitySettings.vSyncCount = 0;
		}

		private void OnDisable()
		{
			// Restore framerates
			// Save current time scale and target frame rate
			Time.timeScale = m_BaseTimeScale;

			// Allow control over frame capture timing.
			QualitySettings.vSyncCount = 1;
		}

        void FixedUpdate()
        {

        }

        private void LateUpdate()
		{
			// Check execution speed.
			//Application.targetFrameRate = (int) Math.Round((float) m_BaseTargetFrameRate * executionMultiplier);
			Time.timeScale = m_BaseTimeScale * executionMultiplier;

			// State machine updates
			switch (State)
			{
				case ScenarioManagerState.Stopped:
					if (useScheduler && m_ExecutionQueue.Count != 0)
					{
						// Run next scenario in queue
						var scenarioExecution = m_ExecutionQueue.Peek();
						StartCoroutine(StartScenario(scenarioExecution));
					}
					else
					{
						EndExecution();
					}

					break;

				case ScenarioManagerState.Running:

					// Check if the specified number of frames have executed and end if needed.
					var scenarioFrameCount = Time.frameCount - m_CurrentScenarioStartFrame;
					if (scenarioFrameCount >= maxScenarioLength)
					{
						EndScenario();
					}

					break;

				case ScenarioManagerState.Loading:
				// Do nothing in loading state, we just need to wait until we are done.

				default:
					break;
			}
		}

		IEnumerator UnloadRuntimeScenes()
		{
			// Collect loaded scenes into a list, except for scene containing this object.
			var sceneNames = new List<string>();
			for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
			{
				var scene = SceneManager.GetSceneAt(sceneIndex);
				if (scene.isLoaded && !scene.name.StartsWith("InitTestScene") && gameObject.scene.name != scene.name)
				{
					sceneNames.Add(scene.name);
				}
			}

			// Unload all collected scenes.
			foreach (var sceneName in sceneNames)
			{
				System.Console.WriteLine($"Unloading {sceneName}");
				var op = SceneManager.UnloadSceneAsync(sceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
				while (!op.isDone) yield return null;
				System.Console.WriteLine($"Unloaded {sceneName}");
			}
		}

		IEnumerator LoadRuntimeScenesForScenario(ScenarioExecution scenarioExecution, Promise<bool> success)
		{
			// Load scenes for scenario
			var asyncOperations = new AsyncOperation[scenarioExecution.Scenario.scenes.Count];
			for (int i = 0; i < scenarioExecution.Scenario.scenes.Count; i++)
			{
				var sceneName = scenarioExecution.Scenario.scenes[i];
				System.Console.WriteLine($"Loading {sceneName}");
				asyncOperations[i] = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
				if (asyncOperations[i] == null)
				{
					System.Console.WriteLine($"Unable to load scene {sceneName} from scenario {scenarioExecution.Scenario.name}.  Cancelling scenario execution.");
					success.value = false;
					yield break;
				}
				else
				{
					asyncOperations[i].allowSceneActivation = false;
					while (asyncOperations[i].progress < .9f) ;  // In the future this does not need to occur in serial, but that optimization is not necessary for now.
				}
				System.Console.WriteLine($"Loaded {sceneName}");
			}

			// TODO:  From testing I observed a case where the scene with the manager was forcibly unloaded at this point which leaves things in a bad state.

			// Once all are loaded, we can transition to running state and allow all the objects to activate
			foreach (var op in asyncOperations)
			{
				op.allowSceneActivation = true;
			}
			while (asyncOperations.Any(o => !o.isDone)) { yield return null; }

			success.value = true;
		}

		IEnumerator StartScenario(ScenarioExecution scenarioExecution)
		{
			State = ScenarioManagerState.Loading;

            // Check that the scenario has scenes in it.
            if (scenarioExecution.Scenario.scenes.Count == 0)
			{
				System.Console.WriteLine($"Scenario {scenarioExecution.Scenario.name} has no scenes.  Cancelling scenario execution.");
				EndScenario();
				yield break;
			}

			// First unload any loaded scenes.
			yield return StartCoroutine(UnloadRuntimeScenes());

			// Next load the necessary scenes for this scenario
			var success = new Promise<bool>();
			yield return StartCoroutine(LoadRuntimeScenesForScenario(scenarioExecution, success));
			if (!success)
			{
				// If loading failed, terminate scenario and return from co-routine.
				EndScenario();
				yield break;
			}

			try
			{
				if (scenarioExecution.ScriptConfigurations != null)
				{
					foreach (var scriptConfiguration in scenarioExecution.ScriptConfigurations)
					{

						scriptConfiguration.ApplyParameters();
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Encountered exception while applying parameters for scenario: " + e);
				EndScenario();
				throw;
			}

			// Set up logger
			m_CurrentScenarioStartFrame = Time.frameCount;
			CarScenarioLogger.Instance.LogStartCarScenario();
			State = ScenarioManagerState.Running;
		}

		private void EndExecution()
		{
			if (terminateOnCompletion)
			{
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
				Application.Quit();
#endif
			}
		}

		static IEnumerable<IEnumerable<ParameterSet>> GetPermutations(IList<IEnumerable<ParameterSet>> parameterSets, int index)
		{
			// TODO:  This functionality can be optimized by creating a mapping function (permutation_counts, current_index) => list<int> permutation_indexes
			// This will work only in the case that all permutations are indexed.  It won't work as well for enumerable-only sets.
			// For now the selectmany + concat approach works fine and is fast enough at low cardinality.
			var newPermutations = parameterSets[index];

			if (index == 0) return newPermutations.Select(p => new ParameterSet[] { p });

			return GetPermutations(parameterSets, index - 1)
				.SelectMany(permutations => newPermutations,
					(currentPermutations, parameterSetSelector) => currentPermutations.Concat(new[] { parameterSetSelector }));
		}

		public void ScheduleScenario(string scenarioName)
        {
            // See if the specified scenario is in the known list of scenarios.
            var scenario = scenarioAssets?.FirstOrDefault(s => s?.name == scenarioName);
            if (scenario == null)
            {
                throw new ArgumentException($"{scenarioName} was not found in the list of scenarios");
            }

            ScheduleScenario(scenario);
        }

        // This version of the ScheduleScenario API allows you to schedule a scenario without having added it to the scenarios list.
        public void ScheduleScenario(ScenarioAsset scenario)
        {
			if (!useScheduler)
			{
				throw new InvalidOperationException("Cannot schedule a scenario for execution when the scheduler is not enabled.  Use ScenarioManager.SetUseScheduler(true).");
			}

			if (scenario == null)
			{
                throw new ArgumentException("scenario parameter cannot be null");
			}

			// Validate that scene references are valid.
			if (scenario.scenes.Any(scene => scene == null))
			{
                throw new InvalidOperationException($"Scenario ${scenario.name} contains an empty scene reference.  Scenario will not be scheduled.");
			}


			if (scenario.parameterSelectors.Count > 0)
			{
				// Iterate over all permutations provided by the scenario permuters.  Currently this is a cross product
				// of all parameter sets.
				var parameterSetsEnumerable = scenario.parameterSelectors.Select(sel => sel.parameterSetList.ToArray()).ToArray();
				var allPermutations = GetPermutations(parameterSetsEnumerable, scenario.parameterSelectors.Count - 1);
                // Having 0 permutations is invalid - mark an error since the scenario won't be scheduled.
                if (!allPermutations.Any())
				{
					Console.WriteLine($"Scenario {scenario.name} has a parameter set list with no permutations.  Scenario will not be scheduled.");
				}
				else
				{
                    // Create the execution information for each scenario
                    foreach (var permutation in allPermutations)
					{
                        ScheduleScenarioConfiguration(new ScenarioExecution() { Scenario = scenario, ScriptConfigurations = permutation.ToList()});
                    }
                }
			}
			else
			{
				ScheduleScenarioConfiguration(new ScenarioExecution() { Scenario = scenario, ScriptConfigurations = null });
			}
		}

		private void ScheduleScenarioConfiguration(ScenarioExecution scenarioExecution)
		{
			Console.Write($"Scheduling scenario {scenarioExecution.Scenario.name} with parameter sets:  ");
			if (scenarioExecution.ScriptConfigurations == null)
			{
				Console.WriteLine("(none)");
			}
			else
			{
				foreach (var permutation in scenarioExecution.ScriptConfigurations)
				{
					Console.Write($"{permutation.name},  ");
                }
                Console.WriteLine();
			}

            m_ExecutionQueue.Enqueue(scenarioExecution);
		}

		public int GetQueueLength()
		{
			return m_ExecutionQueue.Count;
		}
    }
}
