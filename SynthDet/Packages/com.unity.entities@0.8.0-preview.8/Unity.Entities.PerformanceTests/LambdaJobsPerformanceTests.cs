using System;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;
using Unity.Collections;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace Unity.Entities.PerformanceTests
{
    public class LambdaJobsTestFixture : ECSTestsFixture
    {
        protected class TestComponentSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return default;
            }

            public void OneDataLambda()
            {
                Entities.ForEach((Entity entity, ref EcsTestFloatData d1) =>
                {
                    d1.Value++;
                }).Run();
            }
            
            public void TwoDataLambda()
            {
                Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2) => 
                { 
                    d1.Value++;
                    d2.Value0++;
                }).Run();
            }
            
            public void ThreeDataLambda()
            {
                Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                {
                    d1.Value++;
                    d2.Value0++;
                    d3.Value0++;
                }).Run();
            }
            
            public void SimpleLambda()
            {
                Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                {
                    d1.Value = d2.Value0 + d3.Value0;
                }).Run();
            }
            
            public unsafe void SimpleLambdaWithPointerCapture()
            {
                byte* innerRawPtr = (byte*)IntPtr.Zero;
                Entities
                    .WithNativeDisableUnsafePtrRestriction(innerRawPtr)
                    .ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                {
                    if (innerRawPtr == null) 
                        d1.Value = d2.Value0 + d3.Value0;
                }).Run();
            }

#pragma warning disable 618
            [BurstCompile]
            public struct OneDataJob : IJobForEachWithEntity<EcsTestFloatData>
            {
                public void Execute(Entity entity, int index, ref EcsTestFloatData d1)
                {
                    d1.Value++;
                }
            }
        
            [BurstCompile]
            public struct TwoDataJob : IJobForEachWithEntity<EcsTestFloatData, EcsTestFloatData2>
            {
                public void Execute(Entity entity, int index, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2)
                {
                    d1.Value++;
                    d2.Value0++;
                }
            }
        
            [BurstCompile]
            public struct ThreeDataJob : IJobForEachWithEntity<EcsTestFloatData, EcsTestFloatData2, EcsTestFloatData3>
            {
                public int count;
                public void Execute(Entity entity, int index, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3)
                {
                    d1.Value++;
                    d2.Value0++;
                    d3.Value0++;
                }
            }
#pragma warning restore 618
            
            public void StructuralChangesWithECB(EntityManager manager)
            {
                {
                    var ecb = new EntityCommandBuffer(Allocator.Temp, -1, PlaybackPolicy.SinglePlayback);
                    Entities
                        .ForEach((Entity entity) =>
                        {
                            ecb.AddComponent<EcsTestFloatData>(entity);
                        }).Run();
                    ecb.Playback(manager);
                }
                {
                    var ecb = new EntityCommandBuffer(Allocator.Temp, -1, PlaybackPolicy.SinglePlayback);
                    Entities
                        .ForEach((Entity entity) =>
                        {
                            ecb.RemoveComponent<EcsTestFloatData>(entity);
                        }).Run();
                    ecb.Playback(manager);
                }
            }

            public void StructuralChangesInLambda(EntityManager manager)
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity) =>
                    {
                        manager.AddComponent<EcsTestFloatData>(entity);
                    }).Run();
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity) =>
                    {
                        manager.RemoveComponent<EcsTestFloatData>(entity);
                    }).Run();
            }
        }

        protected TestComponentSystem TestSystem => World.GetOrCreateSystem<TestComponentSystem>();
    }
    
    [Category("Performance")]
    class LambdaJobsPerformanceTests : LambdaJobsTestFixture
    {
        // Tests the performance of the LambdaJobs ForEach & ForEach on ReadOnly components
        // No structural change expected
        [Test, Performance]
        [Category("Performance")]  
        public void LambdaJobsForEach_Performance_LJ_vs_IJFE([Values(1, 1000, 100000)] int entityCount, [Range(1, 3)] int componentCount)
        {
            EntityArchetype archetype = new EntityArchetype();
            switch (componentCount)
            {
                case 1: archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData)); break;
                case 2: archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2)); break;
                case 3: archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2), typeof(EcsTestFloatData3)); break;
            }
            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);
                switch (componentCount)
                {
                    case 1:
                        Measure.Method(() =>
                            {
                                TestSystem.OneDataLambda();
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("LambdaJobForEach")
                            .Run();
                        Measure.Method(() =>
                            {
                                var job = new TestComponentSystem.OneDataJob();
                                job.Run(TestSystem);
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("IJobForEachWithEntity")
                            .Run();
                        break;
                    case 2:
                        Measure.Method(() =>
                            {
                                TestSystem.TwoDataLambda();
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("LambdaJobForEach")
                            .Run();
                        Measure.Method(() =>
                            {
                                var job = new TestComponentSystem.TwoDataJob();
                                job.Run(TestSystem);
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("IJobForEachWithEntity")
                            .Run();
                        break;
                    case 3:
                        Measure.Method(() =>
                            {
                                TestSystem.ThreeDataLambda();
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("LambdaJobForEach")
                            .Run();
                        Measure.Method(() =>
                            {
                                var job = new TestComponentSystem.ThreeDataJob();
                                job.Run(TestSystem);
                            })
                            .WarmupCount(5)
                            .MeasurementCount(100)
                            .Definition("IJobForEachWithEntity")
                            .Run();
                        break;
                }
            }
        }
        
        // Tests the performance of the LambdaJobs ForEach & ForEach on ReadOnly components
        // Also tests capturing a pointer (could affect bursted performance if NoAlias not applied correctly)
        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_Simple([Values(true, false)] bool withPointerCapture)
        {
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2), typeof(EcsTestFloatData3));

            var entityCount = 1000000;
            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);

                if (withPointerCapture)
                {
                    Measure.Method(() => { TestSystem.SimpleLambdaWithPointerCapture(); })
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .Definition("LambdaJobsForEach_Performance_WithPointerCapture")
                        .Run();
                }
                else
                {
                    Measure.Method(() => { TestSystem.SimpleLambda(); })
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .Definition("LambdaJobsForEach_Performance_Simple")
                        .Run();
                }
            }
        }

        [Test, Performance]
        [Category("Performance")]  
        public void LambdaJobsForEachStructuralChanges_Performance_InLambda_vs_WithECB([Values(1, 1000, 10000)] int entityCount, [Values(true, false)] bool withECB)
        {
            EntityArchetype archetype = new EntityArchetype();
            archetype = m_Manager.CreateArchetype();
            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);
                if (withECB)
                {
                    Measure.Method(() =>
                        {
                            TestSystem.StructuralChangesWithECB(m_Manager);
                        })
                        .Definition("StructuralChangesWithECB")
                        .Run();
                }
                else
                {
                    Measure.Method(() =>
                        {
                            TestSystem.StructuralChangesInLambda(m_Manager);
                        })
                        .Definition("StructuralChangesInLambda")
                        .Run();
                }
            }
        }
    }
}
