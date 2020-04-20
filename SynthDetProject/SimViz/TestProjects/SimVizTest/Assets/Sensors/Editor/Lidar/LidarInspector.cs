using System;
using Syncity.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Syncity.Cameras
{
    [CustomEditor(typeof(Lidar))]
    public class LidarInspector : Editor
    {
        Lidar myTarget => (Lidar) target;

        public override void OnInspectorGUI()
        {
            var cullingMask = EditorGUILayout.MaskField("Culling Mask", myTarget.cullingMask, InternalEditorUtility.layers);
            if (myTarget.cullingMask != cullingMask)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.cullingMask = cullingMask;
            }

            EditorGUILayout.Space();

            var horizontalFieldOfView = EditorGUILayout.Slider("Horizontal FOV",
                myTarget.horizontalFieldOfView,
                0f, 360f);
            if (myTarget.horizontalFieldOfView != horizontalFieldOfView)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.horizontalFieldOfView = horizontalFieldOfView;
            }
            var verticalFieldOfView = EditorGUILayout.Slider("Vertical FOV", myTarget.verticalFieldOfView,
                0f, 180f);
            if (myTarget.verticalFieldOfView != verticalFieldOfView)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.verticalFieldOfView = verticalFieldOfView;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Clipping Planes");
            var nearClipPlane = Mathf.Max(0,
                Mathf.Min(myTarget.farClipPlane, EditorGUILayout.FloatField("Near", myTarget.nearClipPlane)));
            if (myTarget.nearClipPlane != nearClipPlane)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.nearClipPlane = nearClipPlane;
            }
            var farClipPlane = Mathf.Max(myTarget.nearClipPlane,
                EditorGUILayout.FloatField("Far", myTarget.farClipPlane));
            if (myTarget.farClipPlane != farClipPlane)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.farClipPlane = farClipPlane;
            }

            EditorGUILayout.Space();

            var subCameraResolution =
                EditorGUILayout.Vector2Field("Resolution multiplier", myTarget.subCameraResolution);
            if (myTarget.subCameraResolution != subCameraResolution)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.subCameraResolution = subCameraResolution;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Beams", EditorStyles.boldLabel);
                var forceRefresh = false;

                var newDist =
                    (Lidar.TBeamAnglesDistribution) EditorGUILayout.EnumPopup("Distribution", myTarget.beamAnglesDistribution);
                if (newDist != myTarget.beamAnglesDistribution)
                {
                    if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    myTarget.beamAnglesDistribution = newDist;
                    forceRefresh = true;
                }
                
                int nb = Math.Max(1, EditorGUILayout.IntField("Number", myTarget.nbOfBeams));
                if (nb != myTarget.nbOfBeams)
                {
                    if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    myTarget.nbOfBeams = nb;
                    forceRefresh = true;
                }

                switch (myTarget.beamAnglesDistribution)
                {
                    case Lidar.TBeamAnglesDistribution.Uniform:
                        UniformBeamAngles(forceRefresh);
                        break;
                    case Lidar.TBeamAnglesDistribution.Custom:
                        CustomBeamAngles(forceRefresh);
                        break;
                }

                string[] labels = new string[myTarget.nbOfBeams];
                for (int i = 0; i < labels.Length; i++)
                {
                    labels[i] = $"{myTarget.GetVerticlAngleInDegreesForNormalizadValue(myTarget.GetAngle(i))}";
                }
                var label = string.Join(", ", labels, 0, myTarget.nbOfBeams);
                EditorGUILayout.LabelField($"[{label}]");
            }
            EditorGUILayout.EndHorizontal();

            var rpm = EditorGUILayout.FloatField("RPM", myTarget.rpm);
            if (myTarget.rpm != rpm)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.rpm = rpm;
            }

            var pointsPerSecond = EditorGUILayout.IntField("Points per second", myTarget.pointsPerSecond);
            if (myTarget.pointsPerSecond != pointsPerSecond)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.pointsPerSecond = pointsPerSecond;
            }

            var noiseGenerator =
                INoiseGeneratorEditor.Field("Noise generator", myTarget.noiseGenerator);
            if (noiseGenerator != myTarget.noiseGenerator)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.noiseGenerator = noiseGenerator;
            }

#if DEBUG_LIDAR
            myTarget.pointsPerRotationPerBeam =
                EditorGUILayout.IntField("Points per beam", myTarget.pointsPerRotationPerBeam);
            if(GUILayout.Button("Save sub-texture to disk"))
            {
                foreach (var depthCamera in myTarget.subCameras)
                {
                    if (depthCamera.linkedCamera.targetTexture == null) continue;
                    
                    Texture2D texture2D = new Texture2D(
                        depthCamera.linkedCamera.targetTexture.width,
                        depthCamera.linkedCamera.targetTexture.height,
                        TextureFormat.ARGB32, false);

                    RenderTexture.active = depthCamera.linkedCamera.targetTexture;
                    texture2D.ReadPixels(new Rect(0, 0, texture2D.width, texture2D.height), 0, 0);
                    texture2D.Apply();
                    RenderTexture.active = null;

                    byte[] bytes = texture2D.EncodeToPNG();

                    string filePath = Path.Combine(Application.dataPath, "..", $"{myTarget.gameObject.name} {depthCamera.gameObject.name}.png");
                    File.WriteAllBytes(filePath, bytes);
                }
            }
            foreach (var subCamera in myTarget.subCameras)
            {
                subCamera.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            }
#else
            foreach (var subCamera in myTarget.subCameras)
            {
                subCamera.gameObject.hideFlags |= HideFlags.HideInHierarchy;
            }
#endif

            SceneView.RepaintAll();
            Repaint();
        }       

        void UniformBeamAngles(bool forceRefresh)
        {
            if (forceRefresh)
            {
                if (myTarget.nbOfBeams == 1)
                {
                    myTarget.SetAngle(0, .5f);
                }
                else
                {
                    float current = 0;
                    float step = 1f / (myTarget.nbOfBeams - 1f);
                    for (int i = 0; i < myTarget.nbOfBeams; i++)
                    {
                        myTarget.SetAngle(i, current);
                        current += step;
                    }
                }
            }
        }
        void CustomBeamAngles(bool forceRefresh)
        {
            for (int i = myTarget.nbOfBeams - 1; i >= 0; i--)
            {
                float min = myTarget.GetVerticlAngleInDegreesForNormalizadValue(0);
                float max = myTarget.GetVerticlAngleInDegreesForNormalizadValue(1);
                if (i > 0)
                {
                    min = myTarget.GetVerticlAngleInDegreesForNormalizadValue(myTarget.GetAngle(i - 1));
                }
                if (i < myTarget.nbOfBeams - 1)
                {
                    max = myTarget.GetVerticlAngleInDegreesForNormalizadValue(myTarget.GetAngle(i + 1));
                }

                float v = myTarget.GetVerticlAngleInDegreesForNormalizadValue(myTarget.GetAngle(i));
                var newV = EditorGUILayout.Slider(i.ToString(), v, min, max);
                if (newV != v)
                {
                    myTarget.SetAngle(i, myTarget.GetVerticalNormalizedValueForAngleInDegrees(newV));
                }
            }
        }
        
        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Active | GizmoType.Selected)]
        static void RenderSelected(Lidar myTarget, GizmoType gizmoType)
        {
            var initialColor = Color.blue;
            var finalColor = Color.red;
            for (int i = 0; i < myTarget.nbOfBeams; i++)
            {
                var beamAngle = myTarget.GetAngle(i);
                Handles.color = Color.Lerp(initialColor, finalColor, 
                    myTarget.nbOfBeams == 1 ? .5f : i / (float)(myTarget.nbOfBeams - 1));
                DrawArc(myTarget, beamAngle - .5f);                
            }
        }
        static void DrawArc(Lidar src, float positionInOtherDimension)
        {
            var from = new Vector3(0, 
                Mathf.Sin((src.verticalFieldOfView * positionInOtherDimension * Mathf.Deg2Rad)), 
                Mathf.Cos(-(src.verticalFieldOfView * positionInOtherDimension * Mathf.Deg2Rad)));;
            from = Quaternion.Euler(0, -src.horizontalFieldOfView * .5f, 0) * from;

            Handles.DrawWireArc(src.transform.position, 
                src.transform.rotation * Vector3.up, 
                src.transform.rotation * from, 
                src.horizontalFieldOfView, 
                src.nearClipPlane);            
            Handles.DrawWireArc(src.transform.position, 
                src.transform.rotation * Vector3.up, 
                src.transform.rotation * from, 
                src.horizontalFieldOfView, 
                src.farClipPlane);            
        }
    }
}
