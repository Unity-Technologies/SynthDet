using System;
using System.Collections;
using System.Collections.Generic;
using Syncity.Cameras.DepthCameraOutputs;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Syncity.Cameras
{
    [CustomEditor(typeof(DepthCamera))]
    public class DepthCameraInspector : Editor
    {
        DepthCamera myTarget => (DepthCamera) target;

        public override void OnInspectorGUI()
        {
            var output = (DepthCamera.TOutput)EditorGUILayout.EnumPopup("Output", myTarget.output);
            if (myTarget.output != output)
            {
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.output = output;
            }

            if (myTarget.output == DepthCamera.TOutput.ColorCoded)
            {
                var colorCode = myTarget.GetComponent<ColorCodedOutput>();

                EditorGUILayout.BeginVertical("box");
                var startColor = EditorGUILayout.ColorField("Start color", colorCode.startColor);
                if (colorCode.startColor != startColor)
                {
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    colorCode.startColor = startColor;
                }
                var endColor = EditorGUILayout.ColorField("End color", colorCode.endColor);
                if (colorCode.endColor != endColor)
                {
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    colorCode.endColor = endColor;
                }
                EditorGUILayout.EndVertical();
            }
            else if (myTarget.output == DepthCamera.TOutput.Greyscale)
            {

            }
            else if (myTarget.output == DepthCamera.TOutput.Raw)
            {
            }

            var bands = EditorGUILayout.Slider("Bands", myTarget.bands, 0f, 2f);
            if (bands != myTarget.bands)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.bands = bands;
            }
        }
    }
}
