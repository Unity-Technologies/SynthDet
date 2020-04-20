using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SimViz.Scenarios;
using System.Collections;

namespace UnityEditor.SimViz.Scenarios
{
    [CustomEditor(typeof(ScenarioManager))]
    public class ScenarioManagerEditor : Editor
    {
        int m_CurrentScenario = -1;
        Dictionary<string, bool> m_FoldoutStates = null;

        // SerializedProperties from ScenarioManager object
        SerializedProperty m_TerminateOnCompletion;
        SerializedProperty m_MaxScenarioLength;
        SerializedProperty m_ExecutionMultiplier;
        SerializedProperty m_UseScheduler;
        SerializedProperty m_SchedulingSource;
        SerializedProperty m_LocalAppParamsFile;
        SerializedProperty m_NumberOfExecutionNodes;

        private static string[] GetAllSceneNames()
        {
            var scenes = new string[EditorSceneManager.sceneCount];
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                scenes[i] = EditorSceneManager.GetSceneAt(i).name;
            }

            return scenes;
        }


        void OnEnable()
        {
            m_TerminateOnCompletion = serializedObject.FindProperty("terminateOnCompletion");
            m_MaxScenarioLength = serializedObject.FindProperty("maxScenarioLength");
            m_ExecutionMultiplier = serializedObject.FindProperty("executionMultiplier");
            m_UseScheduler = serializedObject.FindProperty("useScheduler");
            m_SchedulingSource = serializedObject.FindProperty("schedulingSource");
            m_LocalAppParamsFile = serializedObject.FindProperty("localAppParamsFile");
            m_NumberOfExecutionNodes = serializedObject.FindProperty("numberOfExecutionNodes");
        }

        public override void OnInspectorGUI()
        {
            ScenarioManager scenarioManager = (ScenarioManager)target;
            serializedObject.Update();

            // Simulation fields
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.m_NumberOfExecutionNodes, new GUIContent("Execution Nodes"));
            if (GUILayout.Button("Create App Params"))
            {
                scenarioManager.GenerateAppParamsFiles();
            }
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

            // Scenario Configuration fields
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scenario Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.m_MaxScenarioLength, new GUIContent("Max Scenario Frames"));
            EditorGUILayout.PropertyField(this.m_UseScheduler, new GUIContent("Use Scheduler"));
            EditorGUILayout.PropertyField(this.m_TerminateOnCompletion, new GUIContent("Terminate On Completion"));
            if (m_UseScheduler.boolValue)
            {
                EditorGUILayout.PropertyField(this.m_SchedulingSource, new GUIContent("Scheduling Source"));
                if (m_SchedulingSource.enumValueIndex == (int)SchedulingSource.SimulationScheduler)
                {
                    EditorGUILayout.PropertyField(this.m_LocalAppParamsFile, new GUIContent("App Params File"));
                }

            }
            EditorGUILayout.PropertyField(this.m_ExecutionMultiplier, new GUIContent("Simulation Speed Multiplier"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("scenarioAssets"), new GUIContent("Scenarios"));

            serializedObject.ApplyModifiedProperties();
        }

        static bool UnloadEditorScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                System.Console.WriteLine("Cancelling scenario change");
                return false;
            }

            // Iterate over all loaded scenes and unload them.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded && !scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<ScenarioManager>()))
                {
                    System.Console.WriteLine($"Unloading {scene.name} from editor");
                    EditorSceneManager.CloseScene(scene, false);
                }
            }

            return true;
        }

        static void LoadEditorScenesForScenario(ScenarioAsset scenario)
        {
            if (!UnloadEditorScenes())
            {
                return;
            }

            // Iterate over all scenes from our saved scenes and reload them in the editor.
            foreach (var sceneName in scenario.scenes)
            {
                System.Console.WriteLine($"Loading {sceneName} in editor");
                EditorSceneManager.OpenScene(EditorSceneManager.GetSceneByName(sceneName).path, OpenSceneMode.Additive);
            }

            // Make the first listed scene in the scenario the active scene.
            EditorSceneManager.SetActiveScene(EditorSceneManager.GetSceneByName(scenario.scenes.First()));
        }
    }
}
