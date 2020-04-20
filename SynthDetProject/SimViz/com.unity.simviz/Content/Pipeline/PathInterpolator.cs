using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public class PathInterpolator
    {
        private int currentIndex;
        private float offsetFromPath;
        private readonly List<PointSampleGlobal> vertices;
        private const float Tolerance = 0.001f;

        public PointSampleGlobal InterpolatedVertex
        {
            get;
            private set;
        }

        public PathInterpolator(List<PointSampleGlobal> vertices)
        {
            this.vertices = vertices;
            currentIndex = 0;
            InterpolatedVertex = vertices[currentIndex];
        }

        public bool Navigate(float distance)
        {
            if (distance < Tolerance)
            {
                Debug.LogWarning(
                    $"Distance parameter ({distance}) is smaller than the accepted tolerance ({Tolerance}). " +
                    "Overriding distance parameter with tolerance value.");
                distance = Tolerance;
            }
            
            while (currentIndex < vertices.Count - 2)
            {
                var nextPoint = vertices[currentIndex + 1];
                var remainingSegmentLength = math.distance(nextPoint.pose.pos, InterpolatedVertex.pose.pos);
                if (remainingSegmentLength > distance)
                {
                    var lastPoint = vertices[currentIndex];
                    InterpolatedVertex = NextInterpolatedPoint(lastPoint, nextPoint, distance);
                    return true;
                }
                if (currentIndex >= vertices.Count - 2) break;
                InterpolatedVertex = nextPoint;
                currentIndex++;
                distance -= remainingSegmentLength;
            } 

            return false;
        }
        
        private PointSampleGlobal NextInterpolatedPoint(PointSampleGlobal previousPoint, PointSampleGlobal nextPoint, float distance)
        {
            var distTotal = math.distance(nextPoint.pose.pos, previousPoint.pose.pos);
            var distConsumed = math.distance(InterpolatedVertex.pose.pos, previousPoint.pose.pos) + distance;
            var t = distConsumed / distTotal;
            
            return new PointSampleGlobal(new RigidTransform
            {
                pos = math.lerp(previousPoint.pose.pos, nextPoint.pose.pos, t),
                // NOTE: encountered small rotation artifacts when interpolating rotations along straight paths using slerp here.
                // Switched to using linear heading
                rot = quaternion.LookRotation(previousPoint.pose.pos - nextPoint.pose.pos, new float3(0, 1, 0))
            });
        }
    }
}