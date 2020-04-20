using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class LaneSamplingSystem : ComponentSystem, IGeneratorSystem<LaneSamplingParameters>
    {
        public LaneSamplingParameters Parameters { get; set; }
        EntityQuery Group;
        private EntityArchetype RoadLaneArchetype;

        protected override void OnCreate()
        {
            RoadLaneArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<RoadLane>(),
                ComponentType.ReadWrite<LaneVertex>());
        }

        struct RoadInfo
        {
            public int GeometryStartIndex;
            public int GeometryCount;
            public NativeString512 Name;
        }
        struct CreateLanesJob : IJobParallelFor
        {
            public EntityCommandBuffer.Concurrent CommandBuffer;
            public EntityArchetype RoadLaneArchetype;
            public SplitScheme SplitScheme;
            [ReadOnly] public NativeArray<Geometry> Geometries;
            [ReadOnly] public NativeArray<RoadInfo> Roads;

            public void Execute(int index)
            {
                var road = Roads[index];
                Entity entity = default;
                DynamicBuffer<LaneVertex> laneVertices = default;
                if (SplitScheme == SplitScheme.Road)
                {
                    entity = CommandBuffer.CreateEntity(index, RoadLaneArchetype);
                    CommandBuffer.SetComponent(index, entity, new RoadLane {Name = road.Name});
                    laneVertices = CommandBuffer.AddBuffer<LaneVertex>(index, entity);
                }
                
                for (var geometryIdx = road.GeometryStartIndex; geometryIdx < road.GeometryStartIndex + road.GeometryCount; geometryIdx++)
                {
                    var geometry = Geometries[geometryIdx];
                    if (SplitScheme == SplitScheme.Geometry)
                    {
                        entity = CommandBuffer.CreateEntity(index, RoadLaneArchetype);
                        CommandBuffer.SetComponent(index, entity, new RoadLane {Name = new NativeString512($"{road.Name} {geometry.geometryKind}")});
                        laneVertices = CommandBuffer.AddBuffer<LaneVertex>(index, entity);
                    }
                    var samples = GeometrySampling.BuildSamplesFromGeometry(geometry, 3f);
                    foreach (var sample in samples)
                    {
                        // TODO: support elevation
                        //Samples use x/y coordinates. Translate to x/z planar Unity coordinates 
                        laneVertices.Add(new LaneVertex
                        {
                            GeometryIndex = geometryIdx - road.GeometryStartIndex,
                            Position = sample.pose.pos,
                            Orientation = sample.pose.rot
                        });
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            if(Parameters.RoadNetworkDescription == null)
                return;
            var geometryList = new NativeList<Geometry>(Allocator.TempJob);
//            var roads = RoadNetworkDescription.AllRoads.Values.Where(r => r.junction == "-1").ToArray();
            var roads = Parameters.RoadNetworkDescription.AllRoads.ToArray();
            var roadInfos = new NativeArray<RoadInfo>(roads.Length, Allocator.TempJob);
            for (var i = 0; i < roads.Length; i++)
            {
                var road = roads[i];
                var start = geometryList.Length;
                foreach (var geometry in road.geometry)
                    geometryList.Add(geometry);
                
                roadInfos[i] = new RoadInfo
                {
                    GeometryStartIndex = start,
                    GeometryCount = road.geometry.Count,
                    Name = new NativeString512(road.roadId)
                };
            }

            using (var entityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob))
            {
                new CreateLanesJob
                {
                    CommandBuffer = entityCommandBuffer.ToConcurrent(),
                    Geometries = geometryList,
                    RoadLaneArchetype = RoadLaneArchetype,
                    SplitScheme = Parameters.SplitScheme,
                    Roads = roadInfos
                }.Schedule(roads.Length, 1).Complete();
                entityCommandBuffer.Playback(this.EntityManager);
            }
            geometryList.Dispose();
            roadInfos.Dispose();
        }
    }

    public struct LaneSamplingParameters
    {
        public RoadNetworkDescription RoadNetworkDescription;
        public SplitScheme SplitScheme;
    }

    public enum SplitScheme
    {
        Road,
        Geometry
    }

    public struct RoadLane : IComponentData
    {
        public NativeString512 Name;
        public bool orientation;
    }
    
    /// <summary>
    /// A position along a lane. The lane is assumed to be flat
    /// </summary>
    
    // 1028 is too big a capacity for ECS, but we know that all lanes will have a decent number of samples, so we chose 256 for now
    [InternalBufferCapacityAttribute(256)]
    public struct LaneVertex : IBufferElementData
    {
        /// <summary>
        /// The index in the road's geometry sequence this vertex was derived from.
        /// </summary>
        public int GeometryIndex;

        public float3 Position;
        public quaternion Orientation;
    }
}