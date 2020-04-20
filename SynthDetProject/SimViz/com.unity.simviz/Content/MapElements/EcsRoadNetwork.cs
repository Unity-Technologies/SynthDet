using System;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;

namespace UnityEngine.SimViz.Content.MapElements
{
    /// <summary>
    /// EcsRoadNetwork identifies a specific road network. <see cref="RoadNetworkDescription.entityGuid"/>
    /// </summary>
    public struct EcsRoadNetwork : ISharedComponentData, IEquatable<EcsRoadNetwork>
    {
        public bool Equals(EcsRoadNetwork other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is EcsRoadNetwork other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }

        public int id;
    }

    #region Road
    public struct EcsRoad : IComponentData
    {
        public float length;
        public Entity junction;
        public NativeString64 name;

        public EcsRoadLink predecessor;
        public EcsRoadLink successor;
    }

    public struct EcsRoadLink
    {
        public Entity linkedEntity;
        public LinkContactPoint linkContactPoint;
        public RoadLinkType roadLinkType;
    }

    [InternalBufferCapacity(8)]
    public struct EcsLaneSection : IBufferElementData, IHasS
    {
        public float sRoad;
        public int firstLaneIndex;
        public int centerLaneIndex;
        public int laneCount;
        public int LeftLaneCount => centerLaneIndex - firstLaneIndex;
        public int RightLaneCount => laneCount - LeftLaneCount - 1;

        public NativeSlice<EcsLane> GetLanes(Entity entity, EntityManager entityManager)
        {
            return new NativeSlice<EcsLane>(entityManager.GetBuffer<EcsLane>(entity).AsNativeArray(),
                firstLaneIndex, laneCount);
        }
        public NativeSlice<EcsLane> GetLanes(DynamicBuffer<EcsLane> lanes)
        {
            return new NativeSlice<EcsLane>(lanes.AsNativeArray(),firstLaneIndex, laneCount);
        }
        public NativeSlice<EcsLane> GetLeftLanes(DynamicBuffer<EcsLane> lanes)
        {
            return new NativeSlice<EcsLane>(lanes.AsNativeArray(),firstLaneIndex, LeftLaneCount);
        }
        public NativeSlice<EcsLane> GetRightLanes(DynamicBuffer<EcsLane> lanes)
        {
            return new NativeSlice<EcsLane>(lanes.AsNativeArray(),centerLaneIndex + 1, RightLaneCount);
        }

        public float GetS() => sRoad;
    }
    [InternalBufferCapacity(16)]
    public struct EcsLane : IBufferElementData
    {
        public int id; // positive for left lanes, negative for right
        public LaneType laneType;

        /// <summary>
        /// The lane id of the first successor lane. When there are multiple successors (<see cref="successorIdCount"/>),
        /// the successors can be found by inspecting the firstPredecessorIds of all lanes in the subsequent laneSection.
        /// There will never be a many-to-many relationship between lanes, so either the predecessor or successor side
        /// of each link will have exactly one id count
        /// </summary>
        public int firstSuccessorId;
        public int successorIdCount;
        public int firstPredecessorId;
        public int predecessorIdCount;

        public int firstLaneWidthRecordIndex;
        public int laneWidthRecordCount;

        public bool TryGetSingleSuccessorId(out int id)
        {
            if (successorIdCount == 1)
            {
                id = firstSuccessorId;
                return true;
            }

            id = 0;
            return false;
        }
        public bool TryGetSinglePredecessorId(out int id)
        {
            if (predecessorIdCount == 1)
            {
                id = firstPredecessorId;
                return true;
            }

            id = 0;
            return false;
        }

        public NativeSlice<LaneWidthRecord> GetLaneWidthRecords(Entity entity, EntityManager entityManager)
        {
            return new NativeSlice<LaneWidthRecord>(entityManager.GetBuffer<LaneWidthRecord>(entity).AsNativeArray(),
                firstLaneWidthRecordIndex, laneWidthRecordCount);
        }
        public NativeSlice<LaneWidthRecord> GetLaneWidthRecords(DynamicBuffer<LaneWidthRecord> laneWidthRecords)
        {
            return new NativeSlice<LaneWidthRecord>(laneWidthRecords.AsNativeArray(),
                firstLaneWidthRecordIndex, laneWidthRecordCount);
        }
    }

    // the following externally-defined structs are part of the ECS road data:

    //public struct RoadElevationProfile : IBufferElementData
    //public struct Geometry : IEquatable<Geometry>, IHasS, IBufferElementData
    //public struct LaneOffset : IEquatable<LaneOffset>, IHasS, IBufferElementData
    //public struct LaneWidthRecord : IHasS, IBufferElementData

    #endregion

    #region Junction
    public struct EcsJunction : IComponentData
    {
        public NativeString64 name;
        public NativeString64 junctionId;
    }

    //almost all junctions have > 8 connections
    [InternalBufferCapacity(0)]
    public struct EcsJunctionConnection : IBufferElementData
    {
        public Entity incomingRoad;
        public Entity connectingRoad;
        public LinkContactPoint linkContactPoint;
    }

    // the following externally-defined struct is part of the ECS junction data:
    //public struct JunctionLaneLink : IBufferElementData
    #endregion
}
