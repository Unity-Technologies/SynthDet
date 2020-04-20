using System;
using System.Linq;
using Syncity.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Syncity.Cameras
{
    [CustomEditor(typeof(Radar))]
    public class RadarInspector : Editor
    {
        Radar myTarget => (Radar) target;

        public override void OnInspectorGUI()
        {
            #if !DEBUG_RADAR
            myTarget.linkedCamera.hideFlags |= HideFlags.HideInInspector;
            #else
            myTarget.linkedCamera.hideFlags &= ~HideFlags.HideInInspector;
            EditorGUILayout.ObjectField("Output Texture", myTarget.output, typeof(RenderTexture), true);
            #endif
            myTarget.linkedCamera.clearFlags = CameraClearFlags.Color;
            myTarget.linkedCamera.nearClipPlane = 0.01f;
            myTarget.linkedCamera.backgroundColor = new Color(0,0,0,0);
            
            var cullingMask = EditorGUILayout.MaskField("Culling Mask", myTarget.cullingMask, InternalEditorUtility.layers);
            if (myTarget.cullingMask != cullingMask)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.cullingMask = cullingMask;
            }

            var depth = EditorGUILayout.FloatField("Depth", myTarget.depth);
            if (myTarget.depth != depth)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.depth = depth;
            }

            var range = Mathf.Max(myTarget.linkedCamera.nearClipPlane,
                EditorGUILayout.FloatField("Range", myTarget.range));
            if (myTarget.range != range)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.range = range;
            }

            var verticalFOV = EditorGUILayout.Slider("Vertical FOV", myTarget.verticalfieldOfView, 1, 179);
            if (myTarget.verticalfieldOfView != verticalFOV)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.verticalfieldOfView = verticalFOV;
            }

            var minIntensity = EditorGUILayout.Slider("Min. intensity", myTarget.minIntensity, 0, 1);
            if (myTarget.minIntensity != minIntensity)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.minIntensity = minIntensity;
            }

            var outputResolution = EditorGUILayout.Vector2IntField("Output resolution", myTarget.outputResolution);
            if (myTarget.outputResolution != outputResolution)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.outputResolution = outputResolution;
            }

            EditorGUILayout.BeginVertical("box");
            var startColor = EditorGUILayout.ColorField("Start color", myTarget.startColor);
            if (myTarget.startColor != startColor)
            {
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.startColor = startColor;
            }
            var endColor = EditorGUILayout.ColorField("End color", myTarget.endColor);
            if (myTarget.endColor != endColor)
            {
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.endColor = endColor;
            }
            EditorGUILayout.EndVertical();
            
            var noiseGenerator =
                INoiseGeneratorEditor.Field("Noise generator", myTarget.noiseGenerator);
            if (noiseGenerator != myTarget.noiseGenerator)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.noiseGenerator = noiseGenerator;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onOutput)));

            serializedObject.ApplyModifiedProperties();
        }       
    }
}
