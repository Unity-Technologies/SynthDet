using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Scenes.Editor
{
    /// <summary>
    /// Inspector for scene assets.
    /// </summary>
    [CustomEditor(typeof(SceneAsset))]
    public class SceneInspector : UnityEditor.Editor
    {
        /// <summary>
        /// Draw the inspector GUI.
        /// </summary>
        public override void OnInspectorGUI()
        {
            GUI.enabled = true;
            var path = AssetDatabase.GetAssetPath(target);
            var importerData = SceneImporterData.GetAtPath(path);

            GUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            var liveLinkEnabled = EditorGUILayout.Toggle(new GUIContent("LiveLink Enabled"), !importerData.DisableLiveLink);
            if (EditorGUI.EndChangeCheck())
                SceneImporterData.SetAtPath(path, new SceneImporterData() { DisableLiveLink = !liveLinkEnabled });
        }
    }
}