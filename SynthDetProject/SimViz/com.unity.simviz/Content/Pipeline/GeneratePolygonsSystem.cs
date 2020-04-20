using System;
using System.Collections.Generic;
using ClipperLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class GeneratePolygonsSystem : ComponentSystem, IGeneratorSystem<PolygonSystemParameters>
    {
        public PolygonSystemParameters Parameters { get; set; }

        private EntityArchetype samplesArchetype;

        protected override void OnCreate()
        {
            samplesArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<PolygonOrientationComponent>(),
                ComponentType.ReadWrite<PointSampleGlobal>());
        }

        protected override void OnUpdate()
        {
            var polygons = PolygonsFromRoadOutline(Parameters.extensionDistance);

            var clipper = new Clipper();
            foreach (var polygon in polygons)
            {
                if (!Clipper.Orientation(polygon))
                    polygon.Reverse();
                if (Clipper.Area(polygon) > 0)
                    clipper.AddPath(polygon, PolyType.ptSubject, true);
            }

            var solution = new List<List<IntPoint>>();
            clipper.Execute(ClipType.ctUnion, solution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

            solution.RemoveAll(IsSmallerThanMinimumArea);

            foreach (var polygon in solution)
            {
                var entity = EntityManager.CreateEntity(samplesArchetype);
                EntityManager.SetComponentData(entity, new PolygonOrientationComponent
                {
                    orientation = Clipper.Orientation(polygon)
                        ? PolygonOrientation.Outside
                        : PolygonOrientation.Inside
                });

                var buffer = EntityManager.GetBuffer<PointSampleGlobal>(entity);
                var count = polygon.Count;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var previousPoint = polygon[((i - 1) % count + count) % count];
                    var point = polygon[i];
                    var nextPoint = polygon[(i + 1) % polygon.Count];

                    var v1 = new float3(point.X - previousPoint.X, 0, point.Y - previousPoint.Y);
                    var v2 = new float3(nextPoint.X - point.X, 0, nextPoint.Y - point.Y);
                    var rotation = quaternion.LookRotation(v2 + v1, new float3(0, 1, 0));

                    var fromClipperToGlobal = new PointSampleGlobal(new RigidTransform
                    {
                        pos = new float3(
                            point.X * PlacementUtility.DownScaleFactor,
                            0,
                            point.Y * PlacementUtility.DownScaleFactor),
                        rot = rotation
                    });
                    buffer.Add(fromClipperToGlobal);
                }
            }
        }

        private List<List<IntPoint>> PolygonsFromRoadOutline(float extensionDistance)
        {
            var polygons = new List<List<IntPoint>>();
            var rnd = Parameters.roadNetworkDescription;
            foreach (var road in rnd.AllRoads)
            {
                var points = GeometrySampling.BuildSamplesFromRoadOutlineWithExtensionDistance(
                    road, 0.5f, extensionDistance);
                var path = new List<IntPoint>(points.Length);
                foreach (var point in points)
                {
                    path.Add(new IntPoint(
                        point.pose.pos.x * PlacementUtility.UpScaleFactor,
                        point.pose.pos.z * PlacementUtility.UpScaleFactor));
                }

                if (!Clipper.Orientation(path))
                    path.Reverse();

                polygons.Add(path);

                points.Dispose();
            }

            return polygons;
        }

        private bool IsSmallerThanMinimumArea(List<IntPoint> polygon)
        {
            var area = Parameters.minimumPolygonArea *
                         (PlacementUtility.UpScaleFactor * PlacementUtility.UpScaleFactor);
            return Math.Abs(Clipper.Area(polygon)) < area;
        }
    }

    public struct PolygonSystemParameters
    {
        public RoadNetworkDescription roadNetworkDescription;

        ///<summary>Filters polygons containing an area smaller than the specified value</summary>
        public float minimumPolygonArea;

        ///<summary>Offset applied to the beginning and end of road segment polygons. Increases the likelihood
        /// of polygon overlap to encourage the creation of one contiguous road network polygon.</summary>
        public float extensionDistance;
    }

    public enum PolygonOrientation
    {
        Outside, Inside
    }

    public struct PolygonOrientationComponent : IComponentData
    {
        public PolygonOrientation orientation;
    }
}
