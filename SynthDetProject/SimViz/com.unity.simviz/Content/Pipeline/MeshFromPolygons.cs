using System;
using System.Collections.Generic;
using System.Linq;
using Poly2Tri;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline 
{
    [DisableAutoCreation]
    public class MeshFromPolygons : ComponentSystem, IGeneratorSystem<MeshFromPolygonsParameters>
    {
        public MeshFromPolygonsParameters Parameters { get; set; }
        protected override void OnUpdate()
        {
            Polygon poly = null;
            var holes = new List<Polygon>(); 
            
            Entities.ForEach((ref PolygonOrientationComponent polygonOrientation, DynamicBuffer<PointSampleGlobal> points) =>
            {
                PolygonPoint[] pointArray = new PolygonPoint[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    pointArray[i] = new PolygonPoint(points[i].pose.pos.x, points[i].pose.pos.z);
                }
                var polygon = new Polygon(pointArray);
                if (polygonOrientation.orientation == PolygonOrientation.Outside)
                {
                    if (poly != null)
                        Debug.LogError("Multiple outside polygons detected. " +
                                       "Multiple outside polygons indicate a disconnected road network.");
                    poly = polygon;
                }
                else
                {
                    holes.Add(polygon);
                }
            });

            foreach (var hole in holes)
                poly.AddHole(hole);

            var mesh = RoadNetworkMesher.PolygonToMesh(poly, Parameters.UVMultiplier);
            
            GameObject go = new GameObject("Road mesh");
            go.transform.parent = Parameters.Parent;
            go.transform.localPosition = new Vector3(0, .1f, 0);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = Parameters.Material;
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            go.AddComponent<MeshCollider>();
        }
    }

    public struct MeshFromPolygonsParameters
    {
        public Material Material;
        public Transform Parent;
        public float UVMultiplier;
    }
}