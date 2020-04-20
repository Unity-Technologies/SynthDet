using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace UnityEngine.SimViz.Content.MapElements
{
    [Serializable]
    internal struct SerializableRigidTransform
    {
        public float3 pos;
        public quaternion rot;

        internal SerializableRigidTransform(quaternion rot, float3 pos)
        {
            this.rot = rot;
            this.pos = pos;
        }

        public static implicit operator SerializableRigidTransform(RigidTransform tf)
        {
            return new SerializableRigidTransform(tf.rot, tf.pos);
        }

        public static implicit operator RigidTransform(SerializableRigidTransform tf)
        {
            return new RigidTransform(tf.rot, tf.pos);
        }
    }

    [Serializable]
    [InternalBufferCapacity(8)]
    public struct Geometry : IEquatable<Geometry>, IHasS, IBufferElementData
    {
        // Geometry's s-value is respect to the encapsulating Road length
        [FormerlySerializedAs("s")] public float sRoad;
        float IHasS.GetS() => sRoad;

        public RigidTransform startPose
        {
            get => pose;
            set => pose = value;
            //set => pose = new SerializableRigidTransform(math.normalize(value.rot), value.pos);
        }

        [SerializeField]
        internal SerializableRigidTransform pose;
        public float length;

        public ArcData arcData;
        public SpiralData spiralData;
        // TODO: AISV-161 Remove Poly3Data entirely - parse in Poly3's as ParamPoly3 instead
        public Poly3Data poly3Data;
        public ParamPoly3Data paramPoly3Data;

        public GeometryKind geometryKind;

        public override string ToString()
        {
            var toString =
                $"{nameof(sRoad)}: {sRoad}, {nameof(startPose)}: {startPose}, {nameof(length)}: {length}, {nameof(geometryKind)}: {geometryKind}";
            switch (geometryKind)
            {
                case GeometryKind.Arc:
                    toString += $" ArcData: {arcData}";
                    break;
                case GeometryKind.Poly3:
                    toString += $" Poly3Data: {poly3Data}";
                    break;
                case GeometryKind.ParamPoly3:
                    toString += $" ParamPoly3Data: {paramPoly3Data}";
                    break;
            }

            return toString;
        }

        public bool Equals(Geometry other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return sRoad.Equals(other.sRoad) && startPose.Equals(other.startPose) &&
                   length.Equals(other.length) &&
                   arcData.Equals(other.arcData) && spiralData.Equals(other.spiralData) &&
                   poly3Data.Equals(other.poly3Data) && paramPoly3Data.Equals(other.paramPoly3Data) &&
                   geometryKind == other.geometryKind;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Geometry) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = sRoad.GetHashCode();
                hashCode = (hashCode * 397) ^ startPose.GetHashCode();
                hashCode = (hashCode * 397) ^ length.GetHashCode();
                hashCode = (hashCode * 397) ^ arcData.GetHashCode();
                hashCode = (hashCode * 397) ^ spiralData.GetHashCode();
                hashCode = (hashCode * 397) ^ poly3Data.GetHashCode();
                hashCode = (hashCode * 397) ^ paramPoly3Data.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) geometryKind;
                return hashCode;
            }
        }
    }

    [Serializable]
    public struct SpiralData : IEquatable<SpiralData>
    {
        public float curvatureStart;
        public float curvatureEnd;

        public SpiralData(float curvatureStart, float curvatureEnd)
        {
            this.curvatureStart = curvatureStart;
            this.curvatureEnd = curvatureEnd;
        }

        public bool Equals(SpiralData other)
        {
            return curvatureStart.Equals(other.curvatureStart) && curvatureEnd.Equals(other.curvatureEnd);
        }

        public override bool Equals(object obj)
        {
            return obj is SpiralData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (curvatureStart.GetHashCode() * 397) ^ curvatureEnd.GetHashCode();
            }
        }
    }

    [Serializable]
    public struct ArcData : IEquatable<ArcData>
    {
        public float curvature;

        public ArcData(float curvature)
        {
            this.curvature = curvature;
        }

        public bool Equals(ArcData other)
        {
            return curvature.Equals(other.curvature);
        }

        public override bool Equals(object obj)
        {
            return obj is ArcData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return curvature.GetHashCode();
        }

        public override string ToString()
        {
            return $"{nameof(curvature)}: {curvature}";
        }
    }

    [Serializable]
    public struct Poly3Data : IEquatable<Poly3Data>
    {
        // v = a + bu + cu^2 + du^3
        // (a, b, c, d) => (x, y, z, w)

        public float4 v;

        public Poly3Data(float4 v)
        {
            this.v = v;
        }

        public Poly3Data(Vector4 v)
            : this(new float4(v.x, v.y, v.z, v.w))
        {}

        public bool Equals(Poly3Data other)
        {
            return v.Equals(other.v);
        }

        public override bool Equals(object obj)
        {
            return obj is Poly3Data other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = v.GetHashCode();
                return hashCode;
            }
        }
    }

    [Serializable]
    public struct ParamPoly3Data : IEquatable<ParamPoly3Data>
    {
        public Vector4 u;
        public Vector4 v;
        public Poly3PRange pRange;

        public ParamPoly3Data(Vector4 u, Vector4 v, Poly3PRange pRange = Poly3PRange.Unknown)
        {
            this.u = u;
            this.v = v;
            this.pRange = pRange;
        }

        // Poly3's can effectively be represented as paramPoly3s with u(p) = u
        public ParamPoly3Data(Poly3Data poly3)
        {
            this.v = poly3.v;
            this.u = new float4(0, 1, 0, 0);
            this.pRange = Poly3PRange.Domain;
        }

        public bool Equals(ParamPoly3Data other)
        {
            return u.Equals(other.u) && v.Equals(other.v) && pRange.Equals(other.pRange);
        }

        public override bool Equals(object obj)
        {
            return obj is ParamPoly3Data other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = u.GetHashCode();
                hashCode = (hashCode * 397) ^ v.GetHashCode();
                hashCode = (hashCode * 397) ^ pRange.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            var toString =
                $"{nameof(u)}: {u}, {nameof(v)}: {v}, {nameof(pRange)}: {pRange}";
            return toString;
        }
    }
}
