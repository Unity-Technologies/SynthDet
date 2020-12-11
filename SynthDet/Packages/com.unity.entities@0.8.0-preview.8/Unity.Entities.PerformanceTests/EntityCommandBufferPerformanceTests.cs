using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public sealed class EntityCommandBufferPerformanceTests : EntityPerformanceTestFixture
    {
        EntityArchetype archetype1;
        EntityArchetype archetype2;
        EntityArchetype archetype3;
        NativeArray<Entity> entities1;
        NativeArray<Entity> entities2;
        NativeArray<Entity> entities3;
        EntityQuery group;

        const int count = 1024 * 128;
        
        public override void Setup()
        {
            base.Setup();

            archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
#endif
            entities1 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities2 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities3 = new NativeArray<Entity>(count, Allocator.Persistent);
            group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
        }
        
        [TearDown]
        public override void TearDown()
        {
            if (m_Manager != null)
            {
                entities1.Dispose();
                entities2.Dispose();
                entities3.Dispose();
                group.Dispose();
            }
            base.TearDown();
        }
        
        struct EcsTestDataWithEntity : IComponentData
        {
            public int value;
            public Entity entity;
        }

        void FillWithEcsTestDataWithEntity(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                var e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestDataWithEntity {value = i});
            }
        }

        void FillWithEcsTestData(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                var e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestData {value = i});
            }
        }
        
        void FillWithCreateEntityCommands(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.CreateEntity();
            }
        }
        
        void FillWithInstantiateEntityCommands(EntityCommandBuffer cmds, int repeat, Entity prefab)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.Instantiate(prefab);
            }
        }
        
        void FillWithAddComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities, ComponentType componentType)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddComponent(entities[i], componentType);
            }
        }
        
        void FillWithRemoveComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.RemoveComponent(entities[i], typeof(EcsTestData));
            }
        }
        
        void FillWithSetComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetComponent(entities[i], new EcsTestData {value = i});
            }
        }
        
        void FillWithDestroyEntityCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.DestroyEntity(entities[i]);
            }
        }
        
        void FillWithEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddSharedComponent(entities[i], new EcsTestSharedComp{value = 1});
            }
        }
        
        void FillWithSetEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetSharedComponent(entities[i], new EcsTestSharedComp{value = 2});
            }
        }
        
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        void FillWithEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddComponent(entities[i], new EcsTestManagedComponent{value = "string1"});
            }
        }
        
        void FillWithSetEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetComponent(entities[i], new EcsTestManagedComponent{value = "string2"});
            }
        }
#endif

        [Test, Performance]
        public void EntityCommandBuffer_512SimpleEntities()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(Allocator.TempJob);
                        FillWithEcsTestData(cmds, kCreateLoopCount);
                        ecbs.Add(cmds);
                    }
                })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();

            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        ecbs[repeat].Playback(m_Manager);
                    }
                })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .CleanUp(() =>
                {
                })
                .Run();

            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_512EntitiesWithEmbeddedEntity()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                    () =>
                    {
                        for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                        {
                            var cmds = new EntityCommandBuffer(Allocator.TempJob);
                            FillWithEcsTestDataWithEntity(cmds, kCreateLoopCount);
                            ecbs.Add(cmds);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                    () =>
                    {
                        for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                        {
                            ecbs[repeat].Playback(m_Manager);
                        }
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_OneEntityWithEmbeddedEntityAnd512SimpleEntities()
        {
            // This test should not be any slower than EntityCommandBuffer_SimpleEntities_512x1000
            // It shows that adding one component that needs fix up will not make the fast
            // path any slower

            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;


            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            Measure.Method(
                    () =>
                    {
                        for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                        {
                            var cmds = new EntityCommandBuffer(Allocator.TempJob);
                            Entity e0 = cmds.CreateEntity();
                            cmds.AddComponent(e0, new EcsTestDataWithEntity {value = -1, entity = e0 });
                            FillWithEcsTestData(cmds, kCreateLoopCount);
                            ecbs.Add(cmds);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                    () =>
                    {
                        for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                            ecbs[repeat].Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
        }
        
        // ----------------------------------------------------------------------------------------------------------
        // BLITTABLE
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntity([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithDestroyEntityCommands(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithDestroyEntityCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_CreateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        FillWithCreateEntityCommands(ecb, size);
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    FillWithCreateEntityCommands(ecb, size);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_InstantiateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            var prefabEntity= m_Manager.CreateEntity(archetype1);
            Measure.Method(
                    () =>
                    {
                        FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    prefabEntity= m_Manager.CreateEntity(archetype1);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    prefabEntity= m_Manager.CreateEntity(archetype1);
                    FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_AddComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_SetComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithSetComponentCommands(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithSetComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithRemoveComponentCommands(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithRemoveComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        
        // ----------------------------------------------------------------------------------------------------------
        // MANAGED
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithEcsTestSharedComp(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_AddManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithEcsTestManagedComp(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
#endif
        
        [Test, Performance]
        public void EntityCommandBuffer_SetSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithSetEcsTestSharedComp(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithSetEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_SetManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        using (var entities = group.ToEntityArray(Allocator.TempJob))
                        {
                            FillWithSetEcsTestManagedComp(ecb, entities);
                        }
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        FillWithSetEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
#endif
        
        [Test, Performance]
        public void EntityCommandBuffer_AddComponentToEntityQuery([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        ecb.AddComponent(group, typeof(EcsTestData2));
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    ecb.AddComponent(group, typeof(EcsTestData2));
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponentFromEntityQuery([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        ecb.RemoveComponent(group, typeof(EcsTestData));
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    ecb.RemoveComponent(group, typeof(EcsTestData));
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntitiesInEntityQuery([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        ecb.DestroyEntity(group);
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    ecb.DestroyEntity(group);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
        
        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponentToEntityQuery([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                    () =>
                    {
                        ecb.AddSharedComponent(group, new EcsTestSharedComp{value = 1});
                    })
                .Definition("Record")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .Definition("Playback")
                .WarmupCount(0)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(Allocator.TempJob);
                    ecb.AddSharedComponent(group, new EcsTestSharedComp{value = 1});
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.TempJob))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }
    }
}

