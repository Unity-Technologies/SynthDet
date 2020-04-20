using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Syncity.Cameras
{
    [CustomEditor(typeof(LidarRenderer))]
    public class LidarRendererInspector : Editor
    {
        LidarRenderer myTarget => (LidarRenderer) target;

        public override void OnInspectorGUI()
        {
            var pointSize = EditorGUILayout.FloatField("Point size", myTarget.pointSize);
            if (myTarget.pointSize != pointSize)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.pointSize = pointSize;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.layer)));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Colors");
                var startColor = EditorGUILayout.ColorField("From", myTarget.startColor);
                if (myTarget.startColor != startColor)
                {
                    if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    myTarget.startColor = startColor;
                }

                var endColor = EditorGUILayout.ColorField("To", myTarget.endColor);
                if (myTarget.endColor != endColor)
                {
                    if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    myTarget.endColor = endColor;
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
