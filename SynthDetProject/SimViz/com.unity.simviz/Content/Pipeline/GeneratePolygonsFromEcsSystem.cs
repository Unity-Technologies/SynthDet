using System;
using System.Collections.Generic;
using System.ComponentModel;
using ClipperLib;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [DisableAutoCreation]
    public class GeneratePolygonsFromEcsSystem : JobComponentSystem, IGeneratorSystem<PolygonSystemFromEcsParameters>
    {
        public PolygonSystemFromEcsParameters Parameters { get; set; }

        EntityArchetype m_SamplesArchetype;
        EntityArchetype m_RoadArchetype;
        EntityArchetype m_JunctionArchetype;

        protected override void OnCreate()
        {
            m_RoadArchetype = EcsRoadData.CreateRoadArchetype(EntityManager);

            m_JunctionArchetype = EntityManager.CreateArchetype(
                typeof(EcsRoadNetwork),
                typeof(EcsJunction),
                typeof(EcsJunctionConnection));

            m_SamplesArchetype = EntityManager.CreateArchetype(
                ComponentType.ReadWrite<PolygonOrientationComponent>(),
                ComponentType.ReadWrite<PointSampleGlobal>());
        }


        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            List<List<IntPoint>> polygons;
            switch (Parameters.processingScheme)
            {
                case ProcessingScheme.JobWithCode:
                    polygons = PolygonsFromRoadOutlineJobWithCode(Parameters.extensionDistance, inputDeps);
                    break;
                case ProcessingScheme.IJobParallelFor:
                    polygons = PolygonsFromRoadOutlineIJobParallelFor(Parameters.extensionDistance, inputDeps);
                    break;
                case ProcessingScheme.EntityForEachTempDynamicArrays:
                    polygons = PolygonsFromRoadOutlineEntityForEach(Parameters.extensionDistance, inputDeps);
                    break;
                case ProcessingScheme.EntityForEachSeparateBuffer:
                    polygons = PolygonsFromRoadOutlineEntityForEachSeparateBuffer(Parameters.extensionDistance, inputDeps);
                    break;
                default:
                    throw new NotSupportedException();
            }

            Profiler.BeginSample("Clipper");
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
            Profiler.EndSample();

            Profiler.BeginSample("Create sample entities");
            foreach (var polygon in solution)
            {
                var entity = EntityManager.CreateEntity(m_SamplesArchetype);
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
            Profiler.EndSample();

            return inputDeps;
        }

        /// <summary>
        /// This approach extends the idea in Job.WithCode() by manually generating batches of work with one job per batch for better load balancing.
        /// Setup is tedious, but is slightly faster (~1-2ms) than Entities.ForEach with a separate buffer. We should probably avoid this pattern due to the large overhead and brittle code.
        ///
        /// Test results:
        /// Total: 36ms
        /// Setup: 1.12ms
        /// Combine: 2.12ms
        /// </summary>
        private List<List<IntPoint>> PolygonsFromRoadOutlineIJobParallelFor(float extensionDistance, JobHandle jobHandle)
        {
            Profiler.BeginSample("PolygonsFromRoadOutlineIJobParallelFor");
            Profiler.BeginSample("PolygonsFromRoadOutlineIJobParallelFor_Setup");
            var polygons = new List<List<IntPoint>>();

            var outlineJobParams = new NativeList<OutlineJobParams>(300, Allocator.TempJob);
            var sampleCountTotal = 0;

            Entities.ForEach((int entityInQueryIndex, Entity entity, ref EcsRoad road) =>
            {
                var sampleCount = GeometrySampling.ComputeSampleCountForRoadOutline(road, .5f);
                outlineJobParams.Add(new OutlineJobParams
                {
                    entity = entity,
                    startIndex = sampleCountTotal,
                    count = sampleCount
                });
                sampleCountTotal += sampleCount;
            }).Run();

            var samples = new NativeArray<PointSampleGlobal>(sampleCountTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);


            var ecsRoadGetter = GetComponentDataFromEntity<EcsRoad>(true);
            var geometryGetter = GetBufferFromEntity<Geometry>(true);
            var laneSectionGetter = GetBufferFromEntity<EcsLaneSection>(true);
            var laneGetter = GetBufferFromEntity<EcsLane>(true);
            var laneOffsetGetter = GetBufferFromEntity<LaneOffset>(true);
            var laneWidthRecordGetter = GetBufferFromEntity<LaneWidthRecord>(true);

            var batchSize = samples.Length / 64;
            var jobBatches = new NativeList<JobHandle>(Allocator.TempJob);
            int currentBatchSize = 0;
            int currentBatchStartIndex = 0;
            for (int i = 0; i < outlineJobParams.Length; i++)
            {
                var currentJobParams = outlineJobParams[i];
                currentBatchSize += currentJobParams.count;
                if (currentBatchSize >= batchSize || i == outlineJobParams.Length - 1)
                {
                    var job = new BuildSamplesJob()
                    {
                        extensionDistance = extensionDistance,
                        outlineJobBatch = new OutlineJobBatch
                        {
                            startIndex = currentBatchStartIndex,
                            count = i - currentBatchStartIndex + 1
                        },
                        outlineJobParams = outlineJobParams,
                        ecsRoadGetter = ecsRoadGetter,
                        geometryGetter = geometryGetter,
                        laneSectionGetter = laneSectionGetter,
                        laneGetter = laneGetter,
                        laneOffsetGetter = laneOffsetGetter,
                        laneWidthRecordGetter = laneWidthRecordGetter,
                        samples = samples
                    }.Schedule(jobHandle);
                    jobBatches.Add(job);

                    currentBatchSize = 0;
                    currentBatchStartIndex = i + 1;
                }
            }

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineIJobParallelFor_CompleteJob");
            JobHandle.CombineDependencies(jobBatches).Complete();

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineIJobParallelFor_Combine");

            foreach (var outlineJobParam in outlineJobParams)
            {
                var sampleSlice = new NativeSlice<PointSampleGlobal>(samples, outlineJobParam.startIndex, outlineJobParam.count);
                var path = new List<IntPoint>(sampleSlice.Length);
                foreach (var point in sampleSlice)
                {
                    path.Add(new IntPoint(
                        point.pose.pos.x * PlacementUtility.UpScaleFactor,
                        point.pose.pos.z * PlacementUtility.UpScaleFactor));
                }

                if (!Clipper.Orientation(path))
                    path.Reverse();

                polygons.Add(path);
            }

            outlineJobParams.Dispose();
            jobBatches.Dispose();
            samples.Dispose();
            Profiler.EndSample();
            Profiler.EndSample();

            return polygons;
        }

        struct BuildSamplesJob : IJob
        {
            public float extensionDistance;
            public OutlineJobBatch outlineJobBatch;
            [Unity.Collections.ReadOnly]
            public NativeArray<OutlineJobParams> outlineJobParams;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<PointSampleGlobal> samples;

            [Unity.Collections.ReadOnly]
            public ComponentDataFromEntity<EcsRoad> ecsRoadGetter;
            [Unity.Collections.ReadOnly]
            public BufferFromEntity<Geometry> geometryGetter;
            [Unity.Collections.ReadOnly]
            public BufferFromEntity<EcsLaneSection> laneSectionGetter;
            [Unity.Collections.ReadOnly]
            public BufferFromEntity<EcsLane> laneGetter;
            [Unity.Collections.ReadOnly]
            public BufferFromEntity<LaneOffset> laneOffsetGetter;
            [Unity.Collections.ReadOnly]
            public BufferFromEntity<LaneWidthRecord> laneWidthRecordGetter;

            public void Execute()
            {
                for (int i = 0; i < outlineJobBatch.count; i++)
                {
                    var outlineJobParam = outlineJobParams[outlineJobBatch.startIndex + i];
                    var entity = outlineJobParam.entity;
                    var ecsRoadData = new EcsRoadData()
                    {
                        ecsRoad = ecsRoadGetter[entity],
                        ecsGeometries = geometryGetter[entity],
                        ecsLaneSections = laneSectionGetter[entity],
                        ecsLanes = laneGetter[entity],
                        laneOffsets = laneOffsetGetter[entity],
                        laneWidthRecords = laneWidthRecordGetter[entity]
                    };
                    GeometrySampling.BuildSamplesFromRoadOutlineWithExtensionDistance(
                        ecsRoadData, 0.5f, extensionDistance, new NativeSlice<PointSampleGlobal>(samples, outlineJobParam.startIndex, outlineJobParam.count));
                }
            }
        }

        private struct OutlineJobBatch
        {
            public int startIndex, count;
        }
        private struct OutlineJobParams
        {
            public Entity entity;
            public int startIndex, count;
        }

        private struct RoadSampleSpan
        {
            public int startIndex, count;
        }

        /// <summary>
        /// This approach uses a single large NativeArray of samples filled in by each job instead of adding temporary DynamicBuffers to the entities.
        /// This appears to be the best balance between speed and simplicity.
        ///
        /// Test results:
        /// Total: ~40ms
        /// Setup: ~0ms
        /// Combine: 2.27ms
        /// </summary>
        private List<List<IntPoint>> PolygonsFromRoadOutlineEntityForEachSeparateBuffer(float extensionDistance, JobHandle jobHandle)
        {
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEachSeparateBuffer");
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEachSeparateBuffer_Setup");
            var polygons = new List<List<IntPoint>>();

            var sampleStartIndexMap = new NativeHashMap<Entity, RoadSampleSpan>(300, Allocator.TempJob);
            var sampleCountTotal = 0;

            Entities.ForEach((int entityInQueryIndex, Entity entity, ref EcsRoad road) =>
            {
                var sampleCount = GeometrySampling.ComputeSampleCountForRoadOutline(road, .5f);
                sampleStartIndexMap[entity] = new RoadSampleSpan
                {
                    startIndex = sampleCountTotal,
                    count = sampleCount
                };
                sampleCountTotal += sampleCount;
            }).Run();

            var samples = new NativeArray<PointSampleGlobal>(sampleCountTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var job = Entities.ForEach(( Entity entity,
                ref EcsRoad road,
                in DynamicBuffer<Geometry> ecsGeometries,
                in DynamicBuffer<EcsLane> ecsLanes,
                in DynamicBuffer<EcsLaneSection> ecsLaneSections,
                in DynamicBuffer<LaneOffset> laneOffsets,
                in DynamicBuffer<LaneWidthRecord> laneWidthRecords) => //(Entity entity, ref EcsRoad road) =>
            {
                var ecsRoadData = new EcsRoadData()
                {
                    ecsRoad = road,
                    ecsGeometries = ecsGeometries,
                    ecsLaneSections = ecsLaneSections,
                    ecsLanes = ecsLanes,
                    laneOffsets = laneOffsets,
                    laneWidthRecords = laneWidthRecords
                };;
                var roadSampleSpan = sampleStartIndexMap[entity];
                GeometrySampling.BuildSamplesFromRoadOutlineWithExtensionDistance(
                    ecsRoadData, 0.5f, extensionDistance, new NativeSlice<PointSampleGlobal>(samples, roadSampleSpan.startIndex, roadSampleSpan.count));

            }).WithReadOnly(sampleStartIndexMap).WithoutBurst().Schedule(jobHandle);

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEachSeparateBuffer_CompleteJob");
            job.Complete();

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEachSeparateBuffer_Combine");

            Entities.ForEach((Entity entity, ref EcsRoad road) =>
            {
                var roadSampleSpan = sampleStartIndexMap[entity];
                var sampleSlice = new NativeSlice<PointSampleGlobal>(samples, roadSampleSpan.startIndex, roadSampleSpan.count);
                var path = new List<IntPoint>(sampleSlice.Length);
                foreach (var point in sampleSlice)
                {
                    path.Add(new IntPoint(
                        point.pose.pos.x * PlacementUtility.UpScaleFactor,
                        point.pose.pos.z * PlacementUtility.UpScaleFactor));
                }

                if (!Clipper.Orientation(path))
                    path.Reverse();

                polygons.Add(path);
            }).WithoutBurst().Run();

            sampleStartIndexMap.Dispose();
            samples.Dispose();
            Profiler.EndSample();
            Profiler.EndSample();

            return polygons;
        }


        /// <summary>
        /// This approach adds temporary DynamicBuffers to the entities to hold the samples.
        /// It is simple, but the creation and deletion of temporary buffers is expensive
        ///
        /// Test results:
        /// Total: ~41ms
        /// Setup: ~2.5ms
        /// Combine: 7.16ms
        /// </summary>
        private List<List<IntPoint>> PolygonsFromRoadOutlineEntityForEach(float extensionDistance, JobHandle jobHandle)
        {
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEach_Setup");
            var polygons = new List<List<IntPoint>>();
            //var commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
            //var entityArray = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsRoad>()).ToEntityArray(Allocator.TempJob);
            Entities.ForEach((Entity entity, ref EcsRoad road) =>
            {
                EntityManager.AddBuffer<PointSampleGlobal>(entity);
            }).WithStructuralChanges().Run();

            var job = Entities.ForEach((ref EcsRoad road,
                in DynamicBuffer<Geometry> ecsGeometries,
                in DynamicBuffer<EcsLane> ecsLanes,
                in DynamicBuffer<EcsLaneSection> ecsLaneSections,
                in DynamicBuffer<LaneOffset> laneOffsets,
                in DynamicBuffer<LaneWidthRecord> laneWidthRecords,
                in DynamicBuffer<PointSampleGlobal> pointSampleGlobals) => //(Entity entity, ref EcsRoad road) =>
            {
                var ecsRoadData = new EcsRoadData()
                {
                    ecsRoad = road,
                    ecsGeometries = ecsGeometries,
                    ecsLaneSections = ecsLaneSections,
                    ecsLanes = ecsLanes,
                    laneOffsets = laneOffsets,
                    laneWidthRecords = laneWidthRecords
                };;
                var points = GeometrySampling.BuildSamplesFromRoadOutlineWithExtensionDistance(
                    ecsRoadData, 0.5f, extensionDistance);

                pointSampleGlobals.AddRange(points);

                points.Dispose();
            }).WithoutBurst().Schedule(jobHandle);

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEach_CompleteJob");
            job.Complete();

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineEntityForEach_Combine");

            Entities.ForEach((Entity entity, ref EcsRoad road, in DynamicBuffer<PointSampleGlobal> pointSampleGlobals) =>
            {
                var path = new List<IntPoint>(pointSampleGlobals.Length);
                foreach (var point in pointSampleGlobals)
                {
                    path.Add(new IntPoint(
                        point.pose.pos.x * PlacementUtility.UpScaleFactor,
                        point.pose.pos.z * PlacementUtility.UpScaleFactor));
                }

                if (!Clipper.Orientation(path))
                    path.Reverse();

                polygons.Add(path);
                EntityManager.RemoveComponent<PointSampleGlobal>(entity);
            }).WithoutBurst().WithStructuralChanges().Run();
            Profiler.EndSample();

            return polygons;
        }

        /// <summary>
        /// This approach uses Job.WithCode() to spawn a job for each entity. Setup is expensive due to large number of jobs.
        ///
        /// Test results:
        /// Total: 52.25ms total
        /// Setup: ~16.75ms
        /// Combine: 3.52ms
        /// </summary>
        private List<List<IntPoint>> PolygonsFromRoadOutlineJobWithCode(float extensionDistance, JobHandle jobHandle)
        {
            Profiler.BeginSample("PolygonsFromRoadOutlineJobWithCode");
            Profiler.BeginSample("PolygonsFromRoadOutlineJobWithCode_Setup");

            //Compute samples in jobs. Cannot find a way to use Entities.ForEach, since we need to have one NativeList per EcsRoadData
            var polygons = new List<List<IntPoint>>();
            var entityArray = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsRoad>()).ToEntityArray(Allocator.TempJob);
            List<NativeList<PointSampleGlobal>> sampleLists = new List<NativeList<PointSampleGlobal>>(entityArray.Length);
            var sampleHandles = new NativeArray<JobHandle>(entityArray.Length, Allocator.TempJob);

            // Must set isReadOnly to true to allow multiple jobs to read in parallel.
            // Opportunity for simplification/utility method.
            var ecsRoadGetter = GetComponentDataFromEntity<EcsRoad>(true);
            var geometryGetter = GetBufferFromEntity<Geometry>(true);
            var laneSectionGetter = GetBufferFromEntity<EcsLaneSection>(true);
            var laneGetter = GetBufferFromEntity<EcsLane>(true);
            var laneOffsetGetter = GetBufferFromEntity<LaneOffset>(true);
            var laneWidthRecordGetter = GetBufferFromEntity<LaneWidthRecord>(true);
            for (var entityIndex = 0; entityIndex < entityArray.Length; entityIndex++)
            {
                var innerExtensionDistance = extensionDistance;
                var nativeList = new NativeList<PointSampleGlobal>(Allocator.TempJob);
                sampleLists.Add(nativeList);
                var entity = entityArray[entityIndex];
                var ecsRoadData = new EcsRoadData()
                {
                    ecsRoad = ecsRoadGetter[entity],
                    ecsGeometries = geometryGetter[entity],
                    ecsLaneSections = laneSectionGetter[entity],
                    ecsLanes = laneGetter[entity],
                    laneOffsets = laneOffsetGetter[entity],
                    laneWidthRecords = laneWidthRecordGetter[entity]
                };
                sampleHandles[entityIndex] = Job.WithCode(() =>
                {
                    var points = GeometrySampling.BuildSamplesFromRoadOutlineWithExtensionDistance(
                        ecsRoadData, 0.5f, innerExtensionDistance);
                    nativeList.AddRange(points);
                    points.Dispose();
                }).WithoutBurst().Schedule(jobHandle);
            }
            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineJobWithCode_Complete");

            JobHandle.CompleteAll(sampleHandles);

            Profiler.EndSample();
            Profiler.BeginSample("PolygonsFromRoadOutlineJobWithCode_Combine");
            sampleHandles.Dispose();
            entityArray.Dispose();

            foreach (var sampleList in sampleLists)
            {
                var path = new List<IntPoint>(sampleList.Length);
                foreach (var point in sampleList)
                {
                    path.Add(new IntPoint(
                        point.pose.pos.x * PlacementUtility.UpScaleFactor,
                        point.pose.pos.z * PlacementUtility.UpScaleFactor));
                }
                polygons.Add(path);
                sampleList.Dispose();
            }
            foreach (var polygon in polygons)
            {
                if (!Clipper.Orientation(polygon))
                    polygon.Reverse();
            }
            Profiler.EndSample();
            Profiler.EndSample();

            return polygons;
        }

        bool IsSmallerThanMinimumArea(List<IntPoint> polygon)
        {
            var area = Parameters.minimumPolygonArea *
                         (PlacementUtility.UpScaleFactor * PlacementUtility.UpScaleFactor);
            return Math.Abs(Clipper.Area(polygon)) < area;
        }
    }

    public enum ProcessingScheme
    {
        JobWithCode,
        IJobParallelFor,
        EntityForEachTempDynamicArrays,
        EntityForEachSeparateBuffer
    }

    public struct PolygonSystemFromEcsParameters
    {
        ///<summary>Filters polygons containing an area smaller than the specified value</summary>
        public float minimumPolygonArea;

        ///<summary>Offset applied to the beginning and end of road segment polygons. Increases the likelihood
        /// of polygon overlap to encourage the creation of one contiguous road network polygon.</summary>
        public float extensionDistance;

        public ProcessingScheme processingScheme;
    }

    public struct EcsRoadData
    {
        public EcsRoad ecsRoad;
        public DynamicBuffer<Geometry> ecsGeometries;
        public DynamicBuffer<EcsLane> ecsLanes;
        public DynamicBuffer<EcsLaneSection> ecsLaneSections;
        public DynamicBuffer<LaneOffset> laneOffsets;
        public DynamicBuffer<LaneWidthRecord> laneWidthRecords;

        public static EntityArchetype CreateRoadArchetype(EntityManager entityManager)
        {
            return entityManager.CreateArchetype(
                typeof(EcsRoadNetwork),
                typeof(EcsRoad),
                typeof(RoadElevationProfile),
                typeof(Geometry),
                typeof(LaneOffset),
                typeof(EcsLaneSection),
                typeof(EcsLane),
                typeof(LaneWidthRecord));
        }

        public static EcsRoadData Create(EntityManager entityManager, Entity roadEntity)
        {
            return new EcsRoadData()
            {
                ecsRoad = entityManager.GetComponentData<EcsRoad>(roadEntity),
                ecsGeometries = entityManager.GetBuffer<Geometry>(roadEntity),
                ecsLanes = entityManager.GetBuffer<EcsLane>(roadEntity),
                ecsLaneSections = entityManager.GetBuffer<EcsLaneSection>(roadEntity),
                laneOffsets = entityManager.GetBuffer<LaneOffset>(roadEntity),
                laneWidthRecords = entityManager.GetBuffer<LaneWidthRecord>(roadEntity)
            };
        }
    }

}
