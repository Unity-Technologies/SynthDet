using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public struct RoadNetworkDescriptionToEcsSystemParameters
    {
        public RoadNetworkDescription roadNetworkDescription;
    }

    public class RoadNetworkDescriptionToEcsSystem : ComponentSystem, IGeneratorSystem<RoadNetworkDescriptionToEcsSystemParameters>
    {
        public static RoadNetworkDescription staticRnd = null;
        public static int nextId = 1;
        EntityArchetype m_RoadArchetype;
        EntityArchetype m_JunctionArchetype;

        protected override void OnCreate()
        {
            m_RoadArchetype = EntityManager.CreateArchetype(
                typeof(EcsRoadNetwork),
                typeof(EcsRoad),
                typeof(RoadElevationProfile),
                typeof(Geometry),
                typeof(LaneOffset),
                typeof(EcsLaneSection),
                typeof(EcsLane),
                typeof(LaneWidthRecord));

            m_JunctionArchetype = EntityManager.CreateArchetype(
                typeof(EcsRoadNetwork),
                typeof(EcsJunction),
                typeof(EcsJunctionConnection));
        }

        protected override void OnUpdate()
        {

            var rnd = Parameters.roadNetworkDescription == null ? staticRnd : Parameters.roadNetworkDescription;
            if (rnd == null)
                return;

            var id = rnd.entityRoadId == 0 ? nextId++ : rnd.entityRoadId;
            rnd.entityRoadId = id;
            var ecsRoadNetwork = new EcsRoadNetwork() { id = id };

            //check for an existing road network
            var query = EntityManager.CreateEntityQuery(typeof(EcsRoadNetwork), typeof(EcsRoad));
            query.AddSharedComponentFilter(ecsRoadNetwork);
            if (query.CalculateChunkCount() > 0)
                return;

            var roadEntities = new NativeArray<Entity>(rnd.AllRoads.Length, Allocator.Temp);
            EntityManager.CreateEntity(m_RoadArchetype, roadEntities);

            var junctionEntities = new NativeArray<Entity>(rnd.AllJunctions.Length, Allocator.Temp);
            EntityManager.CreateEntity(m_JunctionArchetype, junctionEntities);

            for (var roadIndex = 0; roadIndex < rnd.AllRoads.Length; roadIndex++)
            {
                var road = rnd.AllRoads[roadIndex];
                var entity = roadEntities[roadIndex];
                EntityManager.SetSharedComponentData(entity, ecsRoadNetwork);
                EntityManager.SetComponentData(entity, new EcsRoad
                {
                    length = road.length,
                    junction = string.IsNullOrEmpty(road.junction) || string.Equals("-1", road.junction) ? Entity.Null : junctionEntities[rnd.GetJunctionIndexById(road.junction)],
                    name = road.name,
                    predecessor = CreateEcsRoadLink(road.predecessor, rnd, roadEntities, junctionEntities),
                    successor = CreateEcsRoadLink(road.successor, rnd, roadEntities, junctionEntities)
                });

                //multiple copies - there is almost certainly a better way
                FillBuffer(road.elevationProfiles, entity);
                FillBuffer(road.geometry, entity);
                FillBuffer(road.laneOffsets, entity);

                var laneSections = EntityManager.GetBuffer<EcsLaneSection>(entity);
                var lanes = EntityManager.GetBuffer<EcsLane>(entity);
                var laneWidthRecords = EntityManager.GetBuffer<LaneWidthRecord>(entity);
                laneSections.ResizeUninitialized(road.laneSections.Count);
                for (var i = 0; i < road.laneSections.Count; i++)
                {
                    var section = road.laneSections[i];

                    laneSections[i] = new EcsLaneSection
                    {
                        centerLaneIndex = lanes.Length + section._centerIdx,
                        firstLaneIndex = lanes.Length,
                        laneCount = section._allLanes.Length,
                        sRoad = section.sRoad
                    };

                    foreach (var lane in section._allLanes)
                    {
                        var ecsLane = new EcsLane
                        {
                            firstLaneWidthRecordIndex = laneWidthRecords.Length,
                            laneWidthRecordCount = lane.widthRecords.Length,
                            firstPredecessorId = FirstOrZero(lane.link.predecessors),
                            predecessorIdCount = lane.link.predecessors?.Length ?? 0,
                            firstSuccessorId = FirstOrZero(lane.link.successors),
                            successorIdCount = lane.link.successors?.Length ?? 0,
                            id = lane.id
                        };

                        var nativeData = new NativeArray<LaneWidthRecord>(lane.widthRecords, Allocator.Temp);
                        laneWidthRecords.AddRange(nativeData);
                        nativeData.Dispose();

                        lanes.Add(ecsLane);
                    }
                }
            }

            for (var junctionIndex = 0; junctionIndex < rnd.AllJunctions.Length; junctionIndex++)
            {
                var junction = rnd.AllJunctions[junctionIndex];
                var entity = junctionEntities[junctionIndex];

                EntityManager.SetSharedComponentData(entity, ecsRoadNetwork);
                EntityManager.SetComponentData(entity, new EcsJunction
                {
                    name = junction.name,
                    junctionId = junction.junctionId
                });
                var connectionBuffer = EntityManager.GetBuffer<EcsJunctionConnection>(entity);
                connectionBuffer.ResizeUninitialized(junction.connections.Count);
                for (int connectionIndex = 0; connectionIndex < junction.connections.Count; connectionIndex++)
                {
                    var connection = junction.connections[connectionIndex];
                    connectionBuffer[connectionIndex] = new EcsJunctionConnection
                    {
                        connectingRoad = roadEntities[rnd.GetRoadIndexById(connection.connectingRoadId)],
                        incomingRoad = roadEntities[rnd.GetRoadIndexById(connection.incomingRoadId)],
                        linkContactPoint = connection.contactPoint
                    };
                }
            }
        }

        static int FirstOrZero(int[] array)
        {
            return array == null || array.Length == 0 ? 0 : array[0];
        }

        void FillBuffer<T>(List<T> data, Entity entity) where T : struct, IBufferElementData
        {
            var buffer = EntityManager.GetBuffer<T>(entity);
            buffer.ResizeUninitialized(data.Count);
            for (int i = 0; i < data.Count; i++)
                buffer[i] = data[i];
        }

        static EcsRoadLink CreateEcsRoadLink(RoadLink roadLink, RoadNetworkDescription rnd, NativeArray<Entity> roadEntities, NativeArray<Entity> junctionEntities)
        {
            Entity other;
            switch (roadLink.linkType)
            {
                case RoadLinkType.None:
                    other = Entity.Null;
                    break;
                case RoadLinkType.Junction:
                    other = junctionEntities[rnd.GetJunctionIndexById(roadLink.nodeId)];
                    break;
                case RoadLinkType.Road:
                    other = roadEntities[rnd.GetRoadIndexById(roadLink.nodeId)];
                    break;
                default:
                    throw new NotSupportedException("Invalid RoadLinkType");
            }
            return new EcsRoadLink
            {
                linkContactPoint = roadLink.contactPoint,
                roadLinkType = roadLink.linkType,
                linkedEntity = other
            };
        }

        public RoadNetworkDescriptionToEcsSystemParameters Parameters { get; set; }
    }
}
