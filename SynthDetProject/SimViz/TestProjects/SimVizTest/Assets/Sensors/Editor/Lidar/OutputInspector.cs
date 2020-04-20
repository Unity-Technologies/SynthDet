using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Syncity.Cameras.LidarOutputs
{
    public abstract class OutputInspector : Editor
    {
        Output myTarget => (Output) target;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onPointCloud)));

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.generatePointCloudData)));
            if (myTarget.generatePointCloudData)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onPointCloudData)));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}