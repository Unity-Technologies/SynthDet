using System;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.SimViz.Content.Sampling
{
    public enum SampleType
    {
        Unknown,
        LaneEdge,
        RoadEdge,
        CurbEdge
    }

    // Local coordinate space is a right-handed, 2-dimensional system with its origin at a point sample
    public struct PointSampleLocal : IEquatable<PointSampleLocal>
    {
        public readonly float2 position;
        public readonly float headingRadians;
        public readonly SampleType sampleType;
        public float slope => math.tan(headingRadians);

        public PointSampleLocal(float u, float v, float headingRadians, SampleType sampleType = SampleType.Unknown) :
            this(new float2(u, v), headingRadians, sampleType) {}

        public PointSampleLocal(float2 position, float headingRadians, SampleType sampleType = SampleType.Unknown)
        {
            this.position = position;
            this.headingRadians = headingRadians;
            this.sampleType = sampleType;
        }

        public bool Equals(PointSampleLocal other)
        {
            return position.Equals(other.position) && headingRadians.Equals(other.slope);
        }

        public override bool Equals(object obj)
        {
            return obj is PointSampleLocal other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (position.GetHashCode() * 397) ^ headingRadians.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{nameof(position)}: {position}, {nameof(headingRadians)}: {headingRadians}";
        }
    }

    public struct PointSampleGlobal : IBufferElementData
    {
        /// <summary>
        /// Pose of the sample in global coordinates
        /// </summary>
        public readonly RigidTransform pose;
        public Vector3 Position => new Vector3(pose.pos.x, pose.pos.y, pose.pos.z);

        public readonly SampleType sampleType;

        public PointSampleGlobal(RigidTransform pose, SampleType sampleType = SampleType.Unknown)
        {
            this.pose = pose;
            this.sampleType = sampleType;
        }

        public PointSampleGlobal(quaternion orientation, float3 position, SampleType sampleType = SampleType.Unknown) :
            this(new RigidTransform(orientation, position), sampleType)
        { }

        public bool Equals(PointSampleGlobal other)
        {
            return pose.Equals(other.pose);
        }

        public override bool Equals(object obj)
        {
            return obj is PointSampleGlobal other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return pose.GetHashCode();
            }
        }

        public override string ToString()
        {
            return pose.ToString();
        }
    }
}
