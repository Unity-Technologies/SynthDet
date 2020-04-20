using System;
using System.Collections;
using System.Collections.Generic;
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
    [CustomEditor(typeof(PanoramicCamera))]
    public class PanoramicCameraInspector : Editor
    {
        PanoramicCamera myTarget => (PanoramicCamera) target;

        public override void OnInspectorGUI()
        {
            var camera = myTarget.GetComponent<Camera>();
            camera.hideFlags = myTarget.debugCamera ? HideFlags.None : HideFlags.HideInInspector;
            
            foreach (var subCamera in myTarget.subCameras)
            {
                subCamera.hideFlags = myTarget.debugCamera ? HideFlags.None : HideFlags.HideInInspector;

                subCamera.hideFlags |= HideFlags.DontSave;
            }
            
            var clearFlags = (CameraClearFlags) EditorGUILayout.EnumPopup("Clear Flags", myTarget.clearFlags);
            if (myTarget.clearFlags != clearFlags)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.clearFlags = clearFlags;
            }

            var backgroundColor = EditorGUILayout.ColorField("Background", myTarget.backgroundColor);
            if (myTarget.backgroundColor != backgroundColor)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.backgroundColor = backgroundColor;
            }

            var cullingMask =
                EditorGUILayout.MaskField("Culling Mask", myTarget.cullingMask, InternalEditorUtility.layers);
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

            var depth = EditorGUILayout.FloatField("Depth", myTarget.depth);
            if (myTarget.depth != depth)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.depth = depth;
            }            

            var renderingPath =
                (RenderingPath) EditorGUILayout.EnumPopup("Rendering Path", myTarget.renderingPath);
            if (myTarget.renderingPath != renderingPath)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.renderingPath = renderingPath;
            }            
            
            var targetTexture =
                (RenderTexture) EditorGUILayout.ObjectField("Target Texture", myTarget.targetTexture,
                    typeof(RenderTexture), true);
            if (myTarget.targetTexture  != targetTexture)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.targetTexture = targetTexture;
            }            

            var useOcclusionCulling =
                EditorGUILayout.Toggle("Occlusion Culling", myTarget.useOcclusionCulling);
            if (myTarget.useOcclusionCulling != useOcclusionCulling)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.useOcclusionCulling = useOcclusionCulling;
            }            

            var allowHDR = EditorGUILayout.Toggle("Allow HDR", myTarget.allowHDR);
            if (myTarget.allowHDR != allowHDR)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.allowHDR = allowHDR;
            }            
            var allowMSAA = EditorGUILayout.Toggle("Allow MSAA", myTarget.allowMSAA);
            if (myTarget.allowMSAA != allowMSAA)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.allowMSAA = allowMSAA;
            }            

            var resolutionMultiplier =
                EditorGUILayout.Vector2Field("Resolution multiplier", myTarget.resolutionMultiplier);
            if (myTarget.resolutionMultiplier != resolutionMultiplier)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.resolutionMultiplier = resolutionMultiplier;
            }            
            
            var debugCamera = EditorGUILayout.Toggle("Debug Camera", myTarget.debugCamera);
            if (myTarget.debugCamera != debugCamera)
            {
                if(!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                myTarget.debugCamera = debugCamera;
            }            

#if DEBUG_PANORAMIC_CAMERA
//            EditorGUILayout.Separator();
//            EditorGUILayout.Separator();
//            base.OnInspectorGUI();
#endif

            SceneView.RepaintAll();
            Repaint();
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Active | GizmoType.Selected)]
        static void RenderSelected(PanoramicCamera src, GizmoType gizmoType)
        {
            Handles.color = Color.gray;
            
            DrawDoubleArc(src, Vector3.up, 0f);            
            DrawDoubleArc(src,  Vector3.left, 0f);

            for (float i = 0.5f; i > 0f; i -= .25f)
            {
                DrawQuadrupleArc(src, Vector3.left, i);
                DrawQuadrupleArc(src, Vector3.up, i);
            }

            DrawLine(src, new Vector2(.5f, .5f));
            DrawLine(src, new Vector2(.5f, -.5f));
            DrawLine(src, new Vector2(-.5f, -.5f));
            DrawLine(src, new Vector2(-.5f, .5f));
        }

        static void DrawLine(PanoramicCamera src, Vector2 angles)
        {
            var p = new Vector3(0, Mathf.Sin(-(src.verticalFieldOfView * angles.y * Mathf.Deg2Rad)), Mathf.Cos(-(src.verticalFieldOfView * angles.y * Mathf.Deg2Rad)));;
            p = Quaternion.Euler(0, -src.horizontalFieldOfView * angles.x, 0) * p;
            Handles.DrawLine(
                src.transform.TransformPoint(p * src.farClipPlane), 
                src.transform.TransformPoint(p * src.nearClipPlane));            
        }

        static void DrawQuadrupleArc(PanoramicCamera src, Vector3 normal, float positionInOtherDimension)
        {
            DrawDoubleArc(src, normal, positionInOtherDimension);            
            DrawDoubleArc(src, normal, -positionInOtherDimension);            
        }
        static void DrawDoubleArc(PanoramicCamera src, Vector3 normal, float positionInOtherDimension)
        {
            DrawArc(src, normal, positionInOtherDimension, src.nearClipPlane);            
            DrawArc(src, normal, positionInOtherDimension, src.farClipPlane);            
        }
        static void DrawArc(PanoramicCamera src, Vector3 normal, float positionInOtherDimension, float radius)
        {
            if (normal == Vector3.up)
            {
                //var from = new Vector3(Mathf.Sin(-(src.horizontalFieldOfView / 2f * Mathf.Deg2Rad)), 0, Mathf.Cos(-(src.horizontalFieldOfView / 2f * Mathf.Deg2Rad)));
                var from = new Vector3(0, 
                    Mathf.Sin((src.verticalFieldOfView * positionInOtherDimension * Mathf.Deg2Rad)), 
                    Mathf.Cos(-(src.verticalFieldOfView * positionInOtherDimension * Mathf.Deg2Rad)));;
                from = Quaternion.Euler(0, -src.horizontalFieldOfView * .5f, 0) * from;

                Handles.DrawWireArc(src.transform.position, 
                    src.transform.rotation * normal, 
                    src.transform.rotation * from, 
                    src.horizontalFieldOfView, 
                    radius);            
            }
            else
            {
                var from = new Vector3(0, Mathf.Sin(-(src.verticalFieldOfView / 2f * Mathf.Deg2Rad)), Mathf.Cos(-(src.verticalFieldOfView / 2f * Mathf.Deg2Rad)));

                Handles.DrawWireArc(src.transform.position, 
                    src.transform.rotation * Quaternion.Euler(0, src.horizontalFieldOfView * positionInOtherDimension, 0) * normal, 
                    src.transform.rotation * Quaternion.Euler(0, src.horizontalFieldOfView * positionInOtherDimension, 0) * from, 
                    src.verticalFieldOfView, 
                    radius);            
            }
        }
    }
}
