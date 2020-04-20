using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class PolygonDrawingSystem : ComponentSystem, IGeneratorSystem<PolygonDrawingParameters>
    {
        public PolygonDrawingParameters Parameters { get; set; }
        
        protected override void OnUpdate()
        {
            var parentObj = new GameObject("Polygons");
            parentObj.transform.parent = Parameters.parent;
            
            Entities.ForEach((ref PolygonOrientationComponent roadLane, DynamicBuffer<PointSampleGlobal> vertices) =>
            {
                DrawVertices(vertices, Color.blue, parentObj.transform);
            });
        }

        private static void DrawVertices(DynamicBuffer<PointSampleGlobal> vertices, Color color, Transform parent)
        {
            var points = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                points[i] = vertices[i].pose.pos;
            }
            CreateLineRenderer(points, color, parent);
        }

        private static void CreateLineRenderer(Vector3[] positions, Color color, Transform parent)
        {
            var newRenderer = new GameObject("Polygon");
            var lineRenderer = newRenderer.AddComponent<LineRenderer>();
            lineRenderer.widthCurve = new AnimationCurve
            {
                keys = new Keyframe[2]
                {
                    new Keyframe(0, 1f),
                    new Keyframe(1, 1f)
                }
            };
            lineRenderer.colorGradient = new Gradient
            {
                colorKeys = new GradientColorKey[1]
                {
                    new GradientColorKey(color, 0)
                }
            };
            lineRenderer.loop = true;
            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);
            newRenderer.transform.parent = parent;
        }
    }

    public struct PolygonDrawingParameters
    {
        public Transform parent;
    }
}