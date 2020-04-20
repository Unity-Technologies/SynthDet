using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SimViz.Scenarios;
using ControlPointType = UnityEngine.SimViz.Scenarios.ControlPointType;

namespace UnityEditor.SimViz.Scenarios
{
    [CustomEditor(typeof(WaypointPath))]
    public class WaypointPathEditor : Editor
    {
        bool showPositions = false;

        [MenuItem("GameObject/Simviz/Waypoint Path", false, 12)]
        public static void CreateWaypointPathObject()
        {
            var go = new GameObject("WaypointPath", typeof(WaypointPath));
            if (Selection.gameObjects.Length > 0)
            {
                go.transform.parent = Selection.gameObjects[0].transform;
            }
            Selection.activeGameObject = go;
        }

        private static void ShowNewPointContextMenu(WaypointPath path, int segmentIndex, int i)
        {
            GenericMenu menu = new GenericMenu();
            {
                var captureSegmentIndex = segmentIndex;
                var captureI = i;
                menu.AddItem(new GUIContent("New point"), false, () => { path.AddNewPointAt(captureSegmentIndex, captureI); });
            }
            for (int menuSegmentIndex = 0; menuSegmentIndex < path.GetSegmentCount(); menuSegmentIndex++)
            {
                for (int menuPointIndex = 0; menuPointIndex < path.GetPointCount(menuSegmentIndex); menuPointIndex++)
                {
                    var point = path.GetPoint(menuSegmentIndex, menuPointIndex);
                    var contentString = string.Format("Existing point/Segment: {0}/Point: {1}  <{2}>", menuSegmentIndex, point.Id, point.Point.ToString());

                    // Capture variables for lambda:
                    var captureSegmentIndex = segmentIndex;
                    var captureI = i;
                    menu.AddItem(new GUIContent(contentString), false, () => { path.AddExistingPointAt(captureSegmentIndex, captureI, point.Id); });
                }
            }

            menu.ShowAsContext();
        }

        public override void OnInspectorGUI()
        {
            WaypointPath path = (WaypointPath)target;

            path.raycastPosition = EditorGUILayout.Toggle("Raycast position", path.raycastPosition);
            path.resolution = EditorGUILayout.FloatField("Spline Resolution", path.resolution);

            showPositions = EditorGUILayout.Foldout(showPositions, "Positions:");
            if (showPositions)
            {
                EditorGUI.indentLevel++;
                for (int segmentIndex = 0; segmentIndex < path.GetSegmentCount(); segmentIndex++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Segment " + segmentIndex, EditorStyles.boldLabel);
                    if (GUILayout.Button(new GUIContent("Remove segment", "Remove this segment")))
                    {
                        if (!path.RemoveSegment(segmentIndex))
                        {
                            Debug.Log($"Unable to remove Segment index {segmentIndex}");
                        }
                        continue;
                    }

                    if (GUILayout.Button(new GUIContent("Insert starting point", "Insert starting point")))
                    {
                        ShowNewPointContextMenu(path, segmentIndex, 0);
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < path.GetPointCount(segmentIndex); i++)
                    {
                        var controlPoint = path.GetPoint(segmentIndex, i);

                        if (controlPoint.Type == ControlPointType.Point)
                        {
                            EditorGUILayout.LabelField("Point: " + controlPoint.Id, EditorStyles.boldLabel);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Point: " + controlPoint.Id + " (reference)", EditorStyles.boldLabel);
                        }

                        EditorGUI.indentLevel++;

                        EditorGUILayout.BeginHorizontal();
                        GUI.enabled = controlPoint.Type == ControlPointType.Point;
                        var newPosition = EditorGUILayout.Vector3Field("", controlPoint.Point);
                        GUI.enabled = true;
                        if (newPosition != controlPoint.Point)
                        {
                            path.MovePointPosition(segmentIndex, i, newPosition);
                        }

                        if (GUILayout.Button(new GUIContent("A", "Add point after this point"), GUILayout.MaxWidth(20)))
                        {
                            ShowNewPointContextMenu(path, segmentIndex, i + 1);
                        }

                        if (GUILayout.Button(new GUIContent("R", "Remove this Point"), GUILayout.MaxWidth(20)))
                        {
                            if (!path.RemovePoint(segmentIndex, i))
                            {
                                Debug.Log($"Unable to find Segment index {segmentIndex} Point index {i}");
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        EditorGUI.indentLevel--;

                    }

                    EditorGUI.indentLevel--;

                }

                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Add segment"))
            {
                path.AddSegment();
            }

            if (GUILayout.Button("Clear all points"))
            {
                path.RemoveAllSegments();
            }

            if (GUILayout.Button("Raycast points"))
            {
                for (int segmentIndex = 0; segmentIndex < path.GetSegmentCount(); segmentIndex++)
                {
                    for (int pointIndex = 0; pointIndex < path.GetPointCount(segmentIndex); pointIndex++)
                    {
                        var point = path.GetPoint(segmentIndex, pointIndex);
                        if (point.Type == ControlPointType.PointReference) continue;
                        Ray ray = new Ray(point.Point + Vector3.up * 100, Vector3.down);
                        RaycastHit hit;

                        if (Physics.Raycast(ray, out hit))
                        {
                            path.MovePointPosition(segmentIndex, pointIndex, hit.point);
                        }
                    }
                }
            }

            serializedObject.Update();
        }

        int currentSegment = 0;
        int currentPoint = 0;

        void OnSceneGUI()
        {
            Color baseColor = Handles.color;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            var movementManager = serializedObject.targetObject as WaypointPath;
            for (int segmentIndex = 0; segmentIndex < movementManager.GetSegmentCount(); segmentIndex++)
            {
                for (int pointIndex = 0; pointIndex < movementManager.GetPointCount(segmentIndex); pointIndex++)
                {
                    var point = movementManager.GetPoint(segmentIndex, pointIndex);
                    Vector3 handlePos = point.Point;

                    var handleString = string.Format("Seg: {0}  Point: {1}", segmentIndex, point.Id);
                    Handles.Label(handlePos + Vector3.up, handleString);

                    if (Tools.current == Tool.Move && point.Type == ControlPointType.Point)
                    {
                        EditorGUI.BeginChangeCheck();

                        // Render handles
                        float size = 0.6f;
                        size = HandleUtility.GetHandleSize(handlePos) * size;

                        if (currentSegment == segmentIndex && currentPoint == pointIndex)
                        {
                            Handles.color = Color.magenta;
                            Handles.SphereHandleCap(controlId, handlePos, Quaternion.identity, 0.5f, Event.current.type);
                        }

                        Handles.color = Handles.xAxisColor;
                        handlePos = Handles.Slider(handlePos, Vector3.right, size, Handles.ArrowHandleCap, 0.01f);
                        Handles.color = Handles.yAxisColor;
                        handlePos = Handles.Slider(handlePos, Vector3.up, size, Handles.ArrowHandleCap, 0.01f);
                        Handles.color = Handles.zAxisColor;
                        handlePos = Handles.Slider(handlePos, Vector3.forward, size, Handles.ArrowHandleCap, 0.01f);

                        Vector3 halfPos = (Vector3.right + Vector3.forward) * size * 0.3f;
                        Handles.color = Handles.yAxisColor;
                        handlePos = Handles.Slider2D(handlePos + halfPos, Vector3.up, Vector3.right, Vector3.forward, size * 0.3f, Handles.RectangleHandleCap, 0.01f) - halfPos;
                        halfPos = (Vector3.right + Vector3.up) * size * 0.3f;
                        Handles.color = Handles.zAxisColor;
                        handlePos = Handles.Slider2D(handlePos + halfPos, Vector3.forward, Vector3.right, Vector3.up, size * 0.3f, Handles.RectangleHandleCap, 0.01f) - halfPos;
                        halfPos = (Vector3.up + Vector3.forward) * size * 0.3f;
                        Handles.color = Handles.xAxisColor;
                        handlePos = Handles.Slider2D(handlePos + halfPos, Vector3.right, Vector3.up, Vector3.forward, size * 0.3f, Handles.RectangleHandleCap, 0.01f) - halfPos;

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(movementManager, "Change Position");
                            currentPoint = pointIndex;
                            currentSegment = segmentIndex;

                            movementManager.MovePointPosition(segmentIndex, pointIndex, handlePos);

                            //Debug.Log($"Moved <{segmentIndex},{pointIndex}> to {handlePos}");
                            serializedObject.ApplyModifiedProperties();
                            Repaint();
                        }
                    }

                    // Render line segments
                    if (pointIndex < movementManager.GetPointCount(segmentIndex) - 1)
                    {
                        var nextPoint = movementManager.GetPoint(segmentIndex, pointIndex + 1);
                        Handles.color = segmentIndex == currentSegment ? Color.red : Color.yellow;
                        Handles.DrawLine(point.Point, nextPoint.Point);
                        var vect = nextPoint.Point - point.Point;
                        // Do not generate cone cap if next point and current point are too close
                        if (vect != Vector3.zero)
                        {
                            Handles.ConeHandleCap(controlId, nextPoint.Point - vect.normalized * 0.4f, Quaternion.LookRotation(vect), 0.8f, Event.current.type);
                        }
                    }
                }

                if (Event.current.type == EventType.MouseDown && Event.current.control)
                {
                    if (Event.current.button == 1)
                    {
                        // Add a new point to a new segment
                        movementManager.AddSegment();
                        currentSegment = movementManager.GetSegmentCount() - 1;
                        currentPoint = -1;
                    }

                    if (Event.current.button == 0 || Event.current.button == 1)
                    {
                        // Add a point after the currently selected point in the currently selected segment.
                        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        RaycastHit hit;

                        if (Physics.Raycast(ray, out hit))
                        {
                            Undo.RecordObject(movementManager, "Add point");

                            var position = hit.point;
                            movementManager.AddNewPointAt(currentSegment, ++currentPoint, position);

                            GUIUtility.hotControl = controlId;
                            Event.current.Use();
                            HandleUtility.Repaint();
                        }
                    }
                }
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && Event.current.control)
            {
                GUIUtility.hotControl = 0;
            }
        }

    }
}