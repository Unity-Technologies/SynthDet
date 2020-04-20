using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.MapElements
{
    [Serializable]
    public struct Lane
    {
        public int id; // positive for left lanes, negative for right
        public LaneType laneType;
        public LaneSectionLink link; // (optional)
        public LaneWidthRecord[] widthRecords;

        // roadmark, material, visibility, speed, access, height, rule
        public Lane(int id, LaneType laneType, LaneSectionLink link, LaneWidthRecord[] widthRecords)
        {
            this.id = id;
            this.laneType = laneType;
            this.link = link;
            this.widthRecords = widthRecords;
        }

        public Lane(int id, LaneType laneType, LaneSectionLink link)
        {
            if (id != 0)
            {
                throw new ArgumentException("Only center lanes can be constructed without the width record argument.");
            }

            this.id = id;
            this.laneType = laneType;
            this.link = link;
            widthRecords = new LaneWidthRecord[0];
        }
    }

    [Serializable]
    public struct LaneSection : IHasS
    {
        // Offset defined relative to the reference line, must be in range [0,road.length)
        [FormerlySerializedAs("sAbsoluteOffset")] public float sRoad;

        float IHasS.GetS() => sRoad;

        // Positive id's only
        public IEnumerable<Lane> leftLanes => _allLanes.Take(_centerIdx);
        // Id must be 0
        public Lane centerLane => _allLanes[_centerIdx];
        // Negative id's only
        public IEnumerable<Lane> rightLanes => _allLanes.Skip(_centerIdx + 1);

        public IEnumerable<Lane> edgeLanes => _allLanes.Take(_centerIdx).Concat(_allLanes.Skip(_centerIdx + 1));

        [SerializeField]
        public int _centerIdx;
        [SerializeField]
        public Lane[] _allLanes;

        public LaneSection(float s, Lane[] leftLanes, Lane[] centerLanes, Lane[] rightLanes)
            : this(s, leftLanes, centerLanes[0], rightLanes)
        {
            Assert.AreEqual(1, centerLanes.Length);
        }

        public LaneSection(float s, Lane[] leftLanes, Lane centerLane, Lane[] rightLanes)
        {
            this.sRoad = s;
            _allLanes = new Lane[leftLanes.Length + rightLanes.Length + 1];
            _centerIdx = leftLanes.Length;
            leftLanes.CopyTo(_allLanes, 0);
            _allLanes[_centerIdx] = centerLane;
            rightLanes.CopyTo(_allLanes, _centerIdx + 1);
        }

        internal Lane GetLane(int laneId)
        {
            if (laneId < 0)
            {
                return _allLanes[-laneId + _centerIdx];
            }

            if (laneId > 0)
            {
                return _allLanes[laneId - 1];
            }

            return _allLanes[_centerIdx];
        }

        internal bool HasLane(int laneId)
        {
            if (laneId < 0)
            {
                return -laneId < _allLanes.Length - _centerIdx;
            }

            if (laneId > 0)
            {
                return laneId < _centerIdx + 1;
            }

            // Always has a center lane
            return true;
        }
    }

    [Serializable]
    public struct LaneSectionLink
    {
        public int[] predecessors;
        public int[] successors;

        public LaneSectionLink(int[] predecessors, int[] successors)
        {
            this.predecessors = predecessors;
            this.successors = successors;
        }
    }

    [Serializable]
    [InternalBufferCapacity(16)]
    public struct LaneWidthRecord : IHasS, IBufferElementData
    {
        public bool isBorder;
        // offset defined relative to the start of the enclosing laneSection
        // must be in range [0,laneSection_next.sAbsoluteOffset - laneSection_this.sAbsoluteOffset) if next section
        // exists, else [0, road.length - laneSection_this.sAbsoluteOffset)
        [FormerlySerializedAs("sOffset")] public float sSectionOffset;
        float IHasS.GetS() => sSectionOffset;
        public Poly3Data poly3;

        public LaneWidthRecord(bool isBorder, float sOffset, float a, float b, float c, float d) :
            this(isBorder, sOffset, new Poly3Data(new Vector4(a, b, c, d))) { }

        public LaneWidthRecord(bool isBorder, float sSectionOffset, Poly3Data poly3)
        {
            this.isBorder = isBorder;
            this.sSectionOffset = sSectionOffset;
            this.poly3 = poly3;
        }
    }

    [Serializable]
    [InternalBufferCapacity(8)]
    public struct LaneOffset : IEquatable<LaneOffset>, IHasS, IBufferElementData
    {
        // Offset defined relative to the reference line, must be in range [0,road.length)
        [FormerlySerializedAs("sAbsoluteOffset")] public float sRoad;
        float IHasS.GetS() => sRoad;
        public Poly3Data poly3;

        public LaneOffset(float s, Vector4 poly3)
        {
            this.sRoad = s;
            this.poly3 = new Poly3Data(poly3);
        }

        public LaneOffset(float sRoad, float4 poly3)
        {
            this.sRoad = sRoad;
            this.poly3 = new Poly3Data(poly3);
        }

        public bool Equals(LaneOffset other)
        {
            return sRoad.Equals(other.sRoad) && poly3.Equals(other.poly3);
        }

        public override bool Equals(object obj)
        {
            return obj is LaneOffset other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (sRoad.GetHashCode() * 397) ^ poly3.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{nameof(sRoad)}: {sRoad}, {nameof(poly3)}: {poly3}";
        }
    }
}
