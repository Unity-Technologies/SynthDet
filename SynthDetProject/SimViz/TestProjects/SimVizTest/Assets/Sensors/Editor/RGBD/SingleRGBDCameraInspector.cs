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
    [CustomEditor(typeof(SingleRGBDCamera))]
    public class SingleRGBDCameraInspector : Editor
    {
        SingleRGBDCamera myTarget => (SingleRGBDCamera) target;

        public override void OnInspectorGUI()
        {
            var bands = EditorGUILayout.Slider("Bands", myTarget.bands, 0f, 100f);
            if (myTarget.bands != bands)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.bands = bands;
            }
        }
    }
}
