using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Syncity.Sensors
{
    [CustomEditor(typeof(IMU))]
    public class IMUInspector : Editor
    {
        IMU myTarget => (IMU) target;

        readonly AnimationCurve accelerationCurveX = new AnimationCurve();
        readonly AnimationCurve accelerationCurveY = new AnimationCurve();
        readonly AnimationCurve accelerationCurveZ = new AnimationCurve();
        readonly AnimationCurve gyroCurveX = new AnimationCurve();
        readonly AnimationCurve gyroCurveY = new AnimationCurve();
        readonly AnimationCurve gyroCurveZ = new AnimationCurve();
        const int maxCurveLength = 50;
        public override void OnInspectorGUI()
        {
            if (myTarget.linkedRigidBody == null)
            {
                EditorGUILayout.HelpBox("IMU requires a rigidbody attached to its gameobject or any of its parents", MessageType.Warning, true);
            }
            
            var accelerationNoiseGenerator = INoiseGeneratorEditor.Field("Acceleration noise generator",
                myTarget.accelerationNoiseGenerator);
            if (accelerationNoiseGenerator != myTarget.accelerationNoiseGenerator)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.accelerationNoiseGenerator = accelerationNoiseGenerator;
            }
            
            var gyroNoiseGenerator =
                INoiseGeneratorEditor.Field("Gyro noise generator", myTarget.gyroNoiseGenerator);
            if (gyroNoiseGenerator != myTarget.gyroNoiseGenerator)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.gyroNoiseGenerator = gyroNoiseGenerator;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onAcceleration)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onGyro)));
            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                var acceleration = myTarget.acceleration;
                acceleration = new Vector3(
                    (float)Math.Round(acceleration.x, 2), 
                    (float)Math.Round(acceleration.y, 2), 
                    (float)Math.Round(acceleration.z, 2));
                accelerationCurveX.AddKey(Time.time, acceleration.x);
                accelerationCurveY.AddKey(Time.time, acceleration.y);
                accelerationCurveZ.AddKey(Time.time, acceleration.z);

                var gyro = myTarget.gyro;
                gyro = new Vector3(
                    (float)Math.Round(gyro.x, 2), 
                    (float)Math.Round(gyro.y, 2), 
                    (float)Math.Round(gyro.z, 2));
                gyroCurveX.AddKey(Time.time, gyro.x);
                gyroCurveY.AddKey(Time.time, gyro.y);
                gyroCurveZ.AddKey(Time.time, gyro.z);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUI.BeginDisabledGroup(true);
                    {
                        EditorGUILayout.Vector3Field("Acceleration", acceleration);
                        EditorGUILayout.CurveField("", accelerationCurveX);
                        EditorGUILayout.CurveField("", accelerationCurveY);
                        EditorGUILayout.CurveField("", accelerationCurveZ);
                        EditorGUILayout.Vector3Field("Gyro", gyro);
                        EditorGUILayout.CurveField("", gyroCurveX);
                        EditorGUILayout.CurveField("", gyroCurveY);
                        EditorGUILayout.CurveField("", gyroCurveZ);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndVertical();

                while (accelerationCurveX.length > maxCurveLength)
                {
                    accelerationCurveX.RemoveKey(0);
                    accelerationCurveY.RemoveKey(0);
                    accelerationCurveZ.RemoveKey(0);

                    gyroCurveX.RemoveKey(0);
                    gyroCurveY.RemoveKey(0);
                    gyroCurveZ.RemoveKey(0);
                }
                
                Repaint();
            }
        }
    }
}
