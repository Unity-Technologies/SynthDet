using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.SimViz.Scenarios;

namespace UnityEditor.SimViz.Scenarios
{
    [CustomEditor(typeof(ScenarioAsset))]
    public class ScenarioAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            if (GUILayout.Button("To app-params"))
            {
                saveAppParams();
            }

            serializedObject.ApplyModifiedProperties();
        }

        public string generateAppParams()
        {
            return JsonUtility.ToJson((ScenarioAppParams) (target as ScenarioAsset));
        }

        public void saveAppParams()
        {
            StreamWriter sw = null;
            try
            {
                File.WriteAllText($"{Application.dataPath}\\{target.name}.json", generateAppParams());
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
