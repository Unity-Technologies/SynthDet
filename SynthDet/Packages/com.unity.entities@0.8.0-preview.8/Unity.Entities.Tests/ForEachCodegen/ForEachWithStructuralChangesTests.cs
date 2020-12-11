using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Entities.Tests.ForEachCodegen;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Entities.Tests.ForEachWithStructuralChangesCodegen
{
    [TestFixture]
    public class ForEachWithStructuralChangesCodegenTests : ECSTestsFixture
    {
        MyTestSystem TestSystem;
        Entity TestEntity;

        struct ExtractTestDataFromEntityManager<T> : IDisposable
            where T : struct, IComponentData
        {
            EntityManager m_mgr;
            public NativeArray<T> Values;

            public ExtractTestDataFromEntityManager(EntityManager mgr)
            {
                m_mgr = mgr;

                using (var group = m_mgr.CreateEntityQuery(typeof(T)))
                {
                    Values = group.ToComponentDataArray<T>(Allocator.TempJob);
                }
            }

            public void Sort<U>(U comparer) where U : System.Collections.Generic.IComparer<T>
            {
                Values.Sort(comparer);
            }

            public void Dispose()
            {
                Values.Dispose();
            }
        }

        struct ExtractTestSharedDataFromEntityManager<T> : IDisposable
            where T : struct, ISharedComponentData
        {
            EntityManager m_mgr;
            public NativeArray<T> Values;

            public ExtractTestSharedDataFromEntityManager(EntityManager mgr)
            {
                m_mgr = mgr;
                int count = 0;
                using (var group = m_mgr.CreateEntityQuery(typeof(T)))
                {
                    Values = new NativeArray<T>(group.CalculateEntityCount(), Allocator.TempJob);
                    using (var chunks = group.CreateArchetypeChunkArray(Allocator.TempJob))
                        for (int i = 0; i < chunks.Length; ++i)
                        {
                            var chunk = chunks[i];
                            var shared = chunk.GetSharedComponentData(m_mgr.GetArchetypeChunkSharedComponentType<T>(), m_mgr);
                            Values[count++] = shared;
                        }

                    Assert.AreEqual(group.CalculateEntityCount(), count);
                }
            }

            public void Dispose()
            {
                Values.Dispose();
            }
        }

        class EcsTestData2Comparer : System.Collections.Generic.IComparer<EcsTestData2>
        {
            public int Compare(EcsTestData2 lhs, EcsTestData2 rhs)
            {
                if (lhs.value0 < rhs.value0) return -1;
                if (lhs.value0 > rhs.value0) return +1;
                return 0;
            }
        }

        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystem<MyTestSystem>();
        }

        [Test]
        public void AddComponentData_IsNotIteratedOver()
        {
            TestSystem.AddComponentData_IsNotIteratedOver();
        }

        [Test]
        public void DestroyEntity_EMHasTheRightNumberOfEntities()
        {
            TestSystem.DestroyEntity_EMHasTheRightNumberOfEntities();
        }

        [Test]
        public void DestroyEntity_OfAForwardEntity_CanBeIterated()
        {
            TestSystem.DestroyEntity_OfAForwardEntity_CanBeIterated();
        }

        [Test]
        public void RemoveComponent_OfAForwardEntity_CanBeIterated()
        {
            TestSystem.RemoveComponent_OfAForwardEntity_CanBeIterated();
        }

        [Test]
        public void Buffer_ModifiedEntities_VisibleFromInsideForEach()
        {
            TestSystem.Buffer_ModifiedEntities_VisibleFromInsideForEach();
        }

        [Test]
        public void Instantiate_HasGetComponent_VisibleFromInsideForEach()
        {
            TestSystem.Instantiate_HasGetComponent_VisibleFromInsideForEach();
        }

        [Test]
        public void Instatiate_BasicOperations_VisibleFromInsideForEach()
        {
            TestSystem.Instatiate_BasicOperations_VisibleFromInsideForEach();
        }

        [Test]
        public void SharedComponent_ModifiedEntities_VisibleFromInsideForEach()
        {
            TestSystem.SharedComponent_ModifiedEntities_VisibleFromInsideForEach();
        }

        [Test]
        public void DestroyEntity_EntityOperations_ShouldThrowWhenRequired()
        {
            TestSystem.DestroyEntity_EntityOperations_ShouldThrowWhenRequired();
        }

        [Test]
        public void RemoveSharedComponent_ModifiedEntity_VisibleFromInsideForEach()
        {
            TestSystem.RemoveSharedComponent_ModifiedEntity_VisibleFromInsideForEach();
        }

        [Test]
        public void RemoveComponent_ModifiedEntity_VisibleFromInsideForEach()
        {
            TestSystem.RemoveComponent_ModifiedEntity_VisibleFromInsideForEach();
        }

        [Test]
        public void RemoveComponent_GetOrSetOfRemovedComponent_Throws()
        {
            TestSystem.RemoveComponent_GetOrSetOfRemovedComponent_Throws();
        }

        [Test]
        public void RemoveComponent_WhenArchetypeModifiedInsideForEach_CanModifySafely()
        {
            TestSystem.RemoveComponent_WhenArchetypeModifiedInsideForEach_CanModifySafely();
        }

        [Test]
        public void SetComponentData_WhenBothSetAndRefAreModified_RefWins()
        {
            TestSystem.SetComponentData_WhenBothSetAndRefAreModified_RefWins();
        }

        [Test]
        public void AddToDynamicBuffer_WithStructuralChanges()
        {
            TestSystem.AddToDynamicBuffer_WithStructuralChanges();
        }

        [Test]
        public void UseEntityIndex_WithStructuralChanges()
        {
            TestSystem.UseEntityIndex_WithStructuralChanges();
        }

        [Test]
        public void TagComponent_WithStructuralChanges()
        {
            TestSystem.TagComponent_WithStructuralChanges();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void Many_ManagedComponents_WithStructuralChanges()
        {
            TestSystem.Many_ManagedComponents_WithStructuralChanges();
        }
#endif

        class MyTestSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps) => inputDeps;

            public void AddComponentData_IsNotIteratedOver()
            {
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(1));
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(2));
                int count = 0;
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData d) =>
                    {
                        ++count;
                        EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(12));
                    }).Run();
                Assert.AreEqual(2, count);
            }

            public void DestroyEntity_EMHasTheRightNumberOfEntities()
            {
                const int kRepeat = 3 * 4; // Make a multiple of 3 for easy math

                var archetype = EntityManager.CreateArchetype(typeof(EcsTestData));
                EntityManager.SetComponentData(EntityManager.CreateEntity(archetype), new EcsTestData(12));
                for (int i = 0; i < kRepeat; i++)
                    EntityManager.SetComponentData(EntityManager.CreateEntity(archetype), new EcsTestData(-i));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity, ref EcsTestData ed) =>
                    {
                        if ((ed.value % 3) == 0)
                        {
                            EntityManager.DestroyEntity(entity);
                        }
                    }).Run();

                using (var group = EntityManager.CreateEntityQuery(typeof(Entity), typeof(EcsTestData)))
                    using (var arr = group.ToComponentDataArray<EcsTestData>(Allocator.TempJob))
                    {
                        Assert.AreEqual(kRepeat - kRepeat / 3, arr.Length);
                    }
            }

            public void DestroyEntity_OfAForwardEntity_CanBeIterated()
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestData));
                Entity e0 = EntityManager.CreateEntity(archetype);
                EntityManager.SetComponentData(e0, new EcsTestData(12));
                Entity eLast = Entity.Null;
                for (int i = 0; i < 10; i++)
                {
                    eLast = EntityManager.CreateEntity(archetype);
                    EntityManager.SetComponentData(eLast, new EcsTestData(-i));
                }

                EntityManager.AddComponentData(e0, new EcsTestDataEntity(-12, eLast));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity, ref EcsTestData ed) =>
                    {
                        if (EntityManager.HasComponent<EcsTestDataEntity>(entity))
                        {
                            Entity elast = EntityManager.GetComponentData<EcsTestDataEntity>(entity).value1;
                            EntityManager.DestroyEntity(elast);
                        }
                    }).Run();

                using (var group = EntityManager.CreateEntityQuery(typeof(Entity), typeof(EcsTestData)))
                    using (var arr = group.ToComponentDataArray<EcsTestData>(Allocator.TempJob))
                    {
                        Assert.AreEqual(10, arr.Length);
                    }
            }

            public void RemoveComponent_OfAForwardEntity_CanBeIterated()
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestData));
                Entity e0 = EntityManager.CreateEntity(archetype);
                EntityManager.SetComponentData(e0, new EcsTestData(12));
                Entity eLast = Entity.Null;
                for (int i = 0; i < 10; i++)
                {
                    eLast = EntityManager.CreateEntity(archetype);
                    EntityManager.SetComponentData(eLast, new EcsTestData(-i));
                }

                EntityManager.AddComponentData(e0, new EcsTestDataEntity(-12, eLast));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity, ref EcsTestData ed) =>
                    {
                        if (EntityManager.HasComponent<EcsTestDataEntity>(entity))
                        {
                            EntityManager.RemoveComponent<EcsTestDataEntity>(entity);
                        }
                    }).Run();

                using (var group = EntityManager.CreateEntityQuery(typeof(Entity), typeof(EcsTestData)))
                    using (var arr = group.ToComponentDataArray<EcsTestData>(Allocator.TempJob))
                    {
                        Assert.AreEqual(10 + 1, arr.Length);
                    }
            }

            public void Buffer_ModifiedEntities_VisibleFromInsideForEach()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        {
                            var buffer = EntityManager.AddBuffer<EcsIntElement>(e);
                            for (int i = 0; i < 189; ++i)
                                buffer.Add(i);
                        }
                        {
                            var buffer = EntityManager.GetBuffer<EcsIntElement>(e);
                            for (int i = 0; i < 189; ++i)
                            {
                                Assert.AreEqual(i, buffer[i].Value);
                                buffer[i] = i * 2;
                            }
                        }
                        {
                            var buffer = EntityManager.GetBuffer<EcsIntElement>(e);
                            for (int i = 0; i < 189; ++i)
                                Assert.AreEqual(i * 2, buffer[i].Value);
                        }

                    }).Run();
                var finalbuffer = EntityManager.GetBuffer<EcsIntElement>(entity);
                for (int i = 0; i < 189; ++i)
                    Assert.AreEqual(i * 2, finalbuffer[i].Value);
            }
            
            public void Instantiate_HasGetComponent_VisibleFromInsideForEach()
            {
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(5));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        Assert.AreEqual(5, testData.value);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(e));
                        Entity newe1 = EntityManager.Instantiate(e);
                        EntityManager.AddComponentData(e, new EcsTestData2() {value0 = 1, value1 = 3});
                        {
                            EcsTestData2 ed2 = EntityManager.GetComponentData<EcsTestData2>(e);
                            Assert.AreEqual(3, ed2.value1);
                            Assert.AreEqual(1, ed2.value0);
                        }

                        Entity deferred = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(deferred, testData);
                        {
                            var ed = EntityManager.GetComponentData<EcsTestData>(deferred);
                            Assert.AreEqual(testData.value, ed.value);
                        }
                        Entity newe2 = EntityManager.Instantiate(e);

                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(newe1));
                        {
                            EcsTestData ed = EntityManager.GetComponentData<EcsTestData>(newe1);
                            Assert.AreEqual(5, ed.value);
                        }
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(e));
                        {
                            EcsTestData2 ed2 = EntityManager.GetComponentData<EcsTestData2>(newe2);
                            Assert.AreEqual(3, ed2.value1);
                            Assert.AreEqual(1, ed2.value0);
                        }
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(newe1));
                        EntityManager.RemoveComponent<EcsTestData>(newe1);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(newe1));
                    }).Run();

                using (var allEntities = EntityManager.GetAllEntities())
                    Assert.AreEqual(4, allEntities.Length);

                using (var group = new ExtractTestDataFromEntityManager<EcsTestData>(EntityManager))
                {
                    Assert.AreEqual(3, group.Values.Length);
                    Assert.AreEqual(5, group.Values[0].value); // e
                    Assert.AreEqual(5, group.Values[1].value); // deferred
                    Assert.AreEqual(5, group.Values[2].value); // newe2
                }

                using (var group = new ExtractTestDataFromEntityManager<EcsTestData2>(EntityManager))
                {
                    Assert.AreEqual(2, group.Values.Length); // (e && newe2)
                    Assert.AreEqual(3, group.Values[0].value1);
                    Assert.AreEqual(1, group.Values[0].value0);
                    Assert.AreEqual(3, group.Values[1].value1);
                    Assert.AreEqual(1, group.Values[1].value0);
                }
            }
            
            public void Instatiate_BasicOperations_VisibleFromInsideForEach()
            {
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData() {value = 3});
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        Entity e0 = EntityManager.Instantiate(e);
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(e));
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(e0));
                        Entity e1 = EntityManager.Instantiate(e0);
                        EntityManager.SetComponentData(e1, new EcsTestData() {value = 12});
                        Entity e2 = EntityManager.Instantiate(e1);
                        Assert.AreEqual(3, EntityManager.GetComponentData<EcsTestData>(e).value);
                        Assert.AreEqual(3, EntityManager.GetComponentData<EcsTestData>(e0).value);
                        Assert.AreEqual(12, EntityManager.GetComponentData<EcsTestData>(e1).value);
                        Assert.AreEqual(12, EntityManager.GetComponentData<EcsTestData>(e2).value);
                    }).Run();
            }

            public void SharedComponent_ModifiedEntities_VisibleFromInsideForEach()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        EntityManager.AddSharedComponentData(e, new EcsTestSharedComp(10));
                        EntityManager.SetSharedComponentData(e, new EcsTestSharedComp(20));

                        Assert.AreEqual(20, EntityManager.GetSharedComponentData<EcsTestSharedComp>(e).value);
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp>(e));
                    }).Run();
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) => { Assert.AreEqual(20, EntityManager.GetSharedComponentData<EcsTestSharedComp>(e).value); }).Run();

                Assert.AreEqual(20, EntityManager.GetSharedComponentData<EcsTestSharedComp>(entity).value);
            }
            
            public void DestroyEntity_EntityOperations_ShouldThrowWhenRequired()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        EntityManager.DestroyEntity(e);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(e));
                        Assert.Throws<InvalidOperationException>(() => EntityManager.AddComponentData(e, new EcsTestData2(22)));
                        Assert.Throws<ArgumentException>(() => EntityManager.Instantiate(e));
                        Assert.Throws<ArgumentException>(() => EntityManager.SetComponentData(e, new EcsTestData(1)));
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(e));
                        Assert.Throws<InvalidOperationException>(() =>
                            EntityManager.AddSharedComponentData(e, new EcsTestSharedComp(1)));
                        Assert.Throws<ArgumentException>(() => EntityManager.GetSharedComponentData<EcsTestSharedComp>(e));
                        Assert.Throws<ArgumentException>(() =>
                            EntityManager.SetSharedComponentData(e, new EcsTestSharedComp(1)));
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestSharedComp>(e));

                        Assert.Throws<InvalidOperationException>(() => EntityManager.AddBuffer<EcsIntElement>(e));
                        Assert.IsFalse(EntityManager.HasComponent<EcsIntElement>(e));
                        Assert.Throws<ArgumentException>(() => EntityManager.Instantiate(e));
                        EntityManager.RemoveComponent<EcsIntElement>(e);
                        EntityManager.RemoveComponent<EcsTestSharedComp>(e);
                        EntityManager.RemoveComponent<EcsIntElement>(e);
                        Assert.IsFalse(EntityManager.Exists(e));
                    }).Run();
            }

            public void RemoveSharedComponent_ModifiedEntity_VisibleFromInsideForEach()
            {
                EntityManager.AddSharedComponentData(EntityManager.CreateEntity(), new EcsTestSharedComp(5));
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, EcsTestSharedComp testData) =>
                    {
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp>(e));
                        EntityManager.RemoveComponent<EcsTestSharedComp>(e);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestSharedComp>(e));
                    }).Run();
                using (var group = new ExtractTestSharedDataFromEntityManager<EcsTestSharedComp>(EntityManager))
                {
                    Assert.AreEqual(0, group.Values.Length);
                }
            }

            public void RemoveComponent_ModifiedEntity_VisibleFromInsideForEach()
            {
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(5));
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(e));
                        EntityManager.RemoveComponent<EcsTestData>(e);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(e));
                        EntityManager.AddComponentData(e, new EcsTestData(123));
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(e));
                        {
                            EcsTestData d = EntityManager.GetComponentData<EcsTestData>(e);
                            Assert.AreEqual(123, d.value);
                            testData.value = 123;
                        }

                        EntityManager.AddSharedComponentData(e, new EcsTestSharedComp(22));
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp>(e));
                        EntityManager.RemoveComponent<EcsTestSharedComp>(e);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestSharedComp>(e));

                        Entity c = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(c, new EcsTestData(-22));
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(c));
                        EntityManager.RemoveComponent<EcsTestData>(c);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(c));
                        EntityManager.AddComponentData(c, new EcsTestData(-123));

                        EntityManager.RemoveComponent<EcsTestData>(c);
                    }).Run();

                using (var group = EntityManager.CreateEntityQuery(typeof(EcsTestData)))
                    using (var arr = group.ToComponentDataArray<EcsTestData>(Allocator.TempJob))
                    {
                        Assert.AreEqual(1, arr.Length); // (e)
                        Assert.AreEqual(123, arr[0].value);
                    }

            }

            public void RemoveComponent_GetOrSetOfRemovedComponent_Throws()
            {
                EntityManager.AddComponentData(EntityManager.CreateEntity(), new EcsTestData(5));
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity e, ref EcsTestData testData) =>
                    {
                        Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(e));
                        EntityManager.RemoveComponent<EcsTestData>(e);
                        Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(e));
                        Assert.Throws<ArgumentException>(() => EntityManager.GetComponentData<EcsTestData>(e));
                        Assert.Throws<ArgumentException>(() => EntityManager.SetComponentData(e, new EcsTestData(12)));

                        EntityManager.AddSharedComponentData(e, new EcsTestSharedComp(22));
                        EntityManager.RemoveComponent<EcsTestSharedComp>(e);
                        Assert.Throws<ArgumentException>(() => EntityManager.GetSharedComponentData<EcsTestSharedComp>(e));
                        Assert.Throws<ArgumentException>(() =>
                            EntityManager.SetSharedComponentData(e, new EcsTestSharedComp(-22)));
                    }).Run();
            }

            public void RemoveComponent_WhenArchetypeModifiedInsideForEach_CanModifySafely()
            {
                var arch = EntityManager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
                using (var ents = new NativeArray<Entity>(10, Allocator.Persistent))
                {
                    EntityManager.CreateEntity(arch, ents);
                    for (int i = 0; i < 10; i++)
                    {
                        EntityManager.SetComponentData(ents[i], new EcsTestData(i + 1));
                    }

                    Entities
                        .WithStructuralChanges()
                        .ForEach((Entity entity, ref EcsTestData c0, ref EcsTestData2 c1) =>
                        {
                            EntityManager.RemoveComponent<EcsTestData>(entity);
                            c1.value0 = -c0.value;
                        }).Run();

                    using (var group = new ExtractTestDataFromEntityManager<EcsTestData>(EntityManager))
                    {
                        Assert.AreEqual(0, group.Values.Length);
                    }

                    using (var group = new ExtractTestDataFromEntityManager<EcsTestData2>(EntityManager))
                    {
                        group.Sort(new EcsTestData2Comparer());
                        Assert.AreEqual(10, group.Values.Length);
                        for (int i = 0; i < 10; i++)
                            Assert.AreEqual(-10 + i, group.Values[i].value0);
                    }
                }
            }
            
            public void SetComponentData_WhenBothSetAndRefAreModified_RefWins()
            {
                var archA = EntityManager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
                using (var ents = new NativeArray<Entity>(4, Allocator.Persistent))
                {
                    EntityManager.CreateEntity(archA, ents);
                    int count = 0;
                    Entities
                        .WithStructuralChanges()
                        .ForEach((Entity entity, ref EcsTestData ca, ref EcsTestData2 ca2) =>
                    {
                        switch (count)
                        {
                            case 0:
                                EntityManager.SetComponentData(entity, new EcsTestData2(count * 100));
                                break;
                            case 1:
                                EntityManager.SetComponentData(entity, new EcsTestData2(count * 100));
                                break;
                            case 2:
                                EntityManager.SetComponentData(entity, new EcsTestData2(count * 100));
                                ca2.value0 = 1;
                                break;
                            case 3:
                                ca2.value0 = -count * 100;
                                break;
                            case 4:
                                EntityManager.CreateEntity();
                                break;
                        }

                        count = count + 1;
                    }).Run();
                    Assert.AreEqual(ents.Length, count);
                    using (var group = new ExtractTestDataFromEntityManager<EcsTestData2>(EntityManager))
                    {
                        Assert.AreEqual(4, group.Values.Length);
                        Assert.AreEqual(0, group.Values[0].value0);     // case 0
                        Assert.AreEqual(100, group.Values[1].value0);   // case 1
                        Assert.AreEqual(1, group.Values[2].value0);     // case 2
                        Assert.AreEqual(-300, group.Values[3].value0);  // case 3
                    }
                }
            }
            
            public void AddToDynamicBuffer_WithStructuralChanges()
            {
                var archA = EntityManager.CreateArchetype(typeof(EcsTestData), typeof(ForEachCodegenTests.TestBufferElement));
                var newEntity = EntityManager.CreateEntity(archA);
                EntityManager.SetComponentData(newEntity, new EcsTestData(10));

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity, ref EcsTestData e1, ref DynamicBuffer<ForEachCodegenTests.TestBufferElement> buf) =>
                    {
                        buf.Add(e1.value);
                        EntityManager.RemoveComponent(entity, typeof(EcsTestData));
                    }).Run();
                
                var buffer = EntityManager.GetBuffer<ForEachCodegenTests.TestBufferElement>(newEntity);
                Assert.AreEqual(1, buffer.Length);
                Assert.AreEqual(10, buffer[buffer.Length-1].Value);
            }
            
            
            public void UseEntityIndex_WithStructuralChanges()
            {
                var archA = EntityManager.CreateArchetype(typeof(EcsTestData));
                var newEntity = EntityManager.CreateEntity(archA);
                EntityManager.SetComponentData(newEntity, new EcsTestData(10));
                
                Entities
                    .WithStructuralChanges()
                    .ForEach((int entityInQueryIndex, ref EcsTestData etd) =>
                {
                    etd.value = entityInQueryIndex + 1234;
                    EntityManager.CreateEntity(archA);
                }).Run();
                
                Assert.AreEqual(1234, EntityManager.GetComponentData<EcsTestData>(newEntity).value);
            }
            
            public void TagComponent_WithStructuralChanges()
            {
                var archA = EntityManager.CreateArchetype(typeof(EcsTestTag));
                var newEntity = EntityManager.CreateEntity(archA);

                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity, in EcsTestTag tag) =>
                    {
                        EntityManager.AddComponentData(entity, new EcsTestData(1234));
                    }).Run();
                
                Assert.AreEqual(1234, EntityManager.GetComponentData<EcsTestData>(newEntity).value);
            }
            
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            public void Many_ManagedComponents_WithStructuralChanges()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent() { value = "SomeString" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent2() { value2 = "SomeString2" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent3() { value3 = "SomeString3" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent4() { value4 = "SomeString4" });
                
                var counter = 0;
                Entities
                    .WithStructuralChanges()
                    .ForEach(
                    (EcsTestManagedComponent t0, EcsTestManagedComponent2 t1, EcsTestManagedComponent3 t2, in EcsTestManagedComponent4 t3) =>
                    {
                        Assert.AreEqual("SomeString", t0.value);
                        Assert.AreEqual("SomeString2", t1.value2);
                        Assert.AreEqual("SomeString3", t2.value3);
                        Assert.AreEqual("SomeString4", t3.value4);
                        counter++;
                        EntityManager.CreateEntity();
                    }).Run();
                
                Assert.AreEqual(1, counter);
            }
#endif
        }
    }
}