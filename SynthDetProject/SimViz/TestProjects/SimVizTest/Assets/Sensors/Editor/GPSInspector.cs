using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Syncity.Sensors
{
    [CustomEditor(typeof(GPS))]
    public class GPSInspector : Editor
    {
        GPS myTarget => (GPS) target;

        public override void OnInspectorGUI()
        {
            var noiseGenerator =
                INoiseGeneratorEditor.Field("Noise generator", myTarget.noiseGenerator);
            if (noiseGenerator != myTarget.noiseGenerator)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.noiseGenerator = noiseGenerator;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onGPSPosition)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.onNMEA)));
            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUI.BeginDisabledGroup(true);
                {
                    EditorGUILayout.PrefixLabel("GPS position");
                    EditorGUI.indentLevel++;
                    {
                        EditorGUILayout.FloatField("latitude", myTarget.gpsPosition.latitude);
                        EditorGUILayout.FloatField("longitude", myTarget.gpsPosition.longitude);
                        EditorGUILayout.FloatField("height", myTarget.gpsPosition.height);
                    }
                    EditorGUI.indentLevel--;

                    if (GUILayout.Button("Open in Maps"))
                    {
                        var url =
                            "http://www.google.com/maps/place/" +
                            $"{myTarget.gpsPosition.latitude.ToString(CultureInfo.InvariantCulture)}," +
                            $"{myTarget.gpsPosition.longitude.ToString(CultureInfo.InvariantCulture)}";
                        Application.OpenURL(url);
                    }

                    EditorGUILayout.FloatField("Speed in knots", myTarget.speedInKnots);
                    EditorGUILayout.FloatField("Speed in knots km/h", myTarget.speedInKph);
                    EditorGUILayout.FloatField("Speed in knots m/s", myTarget.speedInMps);
                    EditorGUILayout.TextField("GPRMC", myTarget.GPRMC);
                    EditorGUILayout.TextField("GPGGA", myTarget.GPGGA);
                    EditorGUI.EndDisabledGroup();
                }
            }


            Repaint();
        }
    }
}
