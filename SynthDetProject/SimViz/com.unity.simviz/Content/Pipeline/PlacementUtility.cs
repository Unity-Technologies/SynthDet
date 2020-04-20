using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    internal static class PlacementUtility
    {
        public const float UpScaleFactor = 1000f;
        public const float DownScaleFactor = 1 / UpScaleFactor;
        
        public static bool CheckForCollidingObjects(
            IEnumerable<Bounds> bounds,
            float3 position,
            quaternion rotation,
            int layerMask)
        {
            return bounds.Any(boundingBox => Physics.CheckBox(
                boundingBox.center + (Vector3)position,
                boundingBox.extents,
                rotation,
                layerMask));
        }

        public static List<List<PointSampleGlobal>> OffsetPolygon(DynamicBuffer<PointSampleGlobal> samples, float distance)
        {
            var solution = new List<List<IntPoint>>();
            var polygon = FromSamplesToClipper(samples);

            if (!Clipper.Orientation(polygon))
                distance *= -1;

            var clipperOffset = new ClipperOffset();
            clipperOffset.AddPath(polygon, JoinType.jtRound, EndType.etClosedPolygon);
            clipperOffset.Execute(ref solution, distance * UpScaleFactor);

            return ConvertToSamples(solution);
        }

        public static List<IntPoint> FromSamplesToClipper(DynamicBuffer<PointSampleGlobal> samples)
        {
            var polygon = new List<IntPoint>(samples.Length);
            foreach (var sample in samples)
            {
                polygon.Add(new IntPoint(
                    sample.pose.pos.x * UpScaleFactor,
                    sample.pose.pos.z * UpScaleFactor
                ));
            }

            return polygon;
        }

        public static List<List<PointSampleGlobal>> ConvertToSamples(List<List<IntPoint>> polygons)
        {
            var convertedPolygons = new List<List<PointSampleGlobal>>(polygons.Count);
            foreach (var polygon in polygons)
            {
                var count = polygon.Count;
                var convertedPolygon = new List<PointSampleGlobal>(count);
                for (var i = 0; i < polygon.Count; i++)
                {
                    var lastPoint = polygon[((i - 1) % count + count) % count];
                    var point = polygon[i];
                    var nextPoint = polygon[(i + 1) % polygon.Count];

                    var v1 = new float3(point.X - lastPoint.X, 0, point.Y - lastPoint.Y);
                    var v2 = new float3(nextPoint.X - point.X, 0, nextPoint.Y - point.Y);
                    var rotation = quaternion.LookRotation((v1 + v2) / 2, new float3(0, 1, 0));
                    
                    convertedPolygon.Add(new PointSampleGlobal(new RigidTransform
                    {
                        pos = new float3(point.X * DownScaleFactor, 0, point.Y * DownScaleFactor),
                        rot = rotation
                    }));
                }
                convertedPolygons.Add(convertedPolygon);
            }

            return convertedPolygons;
        }

        public static List<float3> PoissonPointsOnPolygon(DynamicBuffer<PointSampleGlobal> samples, float spacing)
        {
            var polygon = FromSamplesToClipper(samples);

            var bounds = new Bounds();
            foreach (var sample in samples)
                bounds.Encapsulate(sample.pose.pos);
            
            var poissonPoints = PoissonDiscSampling.BuildSamples(bounds.size.x, bounds.size.z, spacing);
            var validPoints = new List<float3>();

            var offset = new float2(bounds.center.x - bounds.extents.x, bounds.center.z - bounds.extents.z);
            for (var i = 0; i < poissonPoints.Length; i++)
            {
                poissonPoints[i] += offset;
            }

            foreach (var point in poissonPoints)
            {
                var clipperPoint = new IntPoint(point.x * UpScaleFactor, point.y * UpScaleFactor);
                if (Clipper.PointInPolygon(clipperPoint, polygon) != 0)
                    validPoints.Add(new float3(point.x, 0, point.y));
            }

            poissonPoints.Dispose();
            return validPoints;
        }

        public static List<float3> PoissonPointsOutsidePolygon(List<List<IntPoint>> polygons, float poissonSpacing, float polygonOffset)
        {
            var clipperOffset = new ClipperOffset();
            clipperOffset.AddPaths(polygons, JoinType.jtRound, EndType.etClosedPolygon);

            var solution = new List<List<IntPoint>>();
            clipperOffset.Execute(ref solution, polygonOffset * UpScaleFactor);

            var validPoints = new List<float3>();
            
            foreach (var polygon in solution)
            {
                var bounds = new Bounds();
                foreach (var point in polygon)
                    bounds.Encapsulate(new Vector3(point.X * DownScaleFactor, 0f, point.Y * DownScaleFactor));
            
                var poissonPoints = PoissonDiscSampling.BuildSamples(bounds.size.x, bounds.size.z, poissonSpacing);

                var offset = new float2(bounds.center.x - bounds.extents.x, bounds.center.z - bounds.extents.z);
                for (var i = 0; i < poissonPoints.Length; i++)
                {
                    poissonPoints[i] += offset;
                }

                foreach (var point in poissonPoints)
                {
                    var clipperPoint = new IntPoint(point.x * UpScaleFactor, point.y * UpScaleFactor);

                    var pointOutsideNegativePolygons = true;
                    foreach (var innerPolygon in polygons)
                    {
                        if (Clipper.PointInPolygon(clipperPoint, innerPolygon) != 1) continue;
                        pointOutsideNegativePolygons = false;
                        break;
                    }
                    if (pointOutsideNegativePolygons && Clipper.PointInPolygon(clipperPoint, polygon) != 0)
                        validPoints.Add(new float3(point.x, 0, point.y));
                }

                poissonPoints.Dispose();
            }

            return validPoints;
        }
    }
}