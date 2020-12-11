using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    [TestFixture]
    sealed class EntityDifferTests : EntityDifferTestFixture
    {
        [Test]
        public void EntityDiffer_GetChanges_NoChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
            
        }
        
        /// <summary>
        /// Generates a change set over the world and efficiently updates the internal shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithFastForward()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                var entityGuid = CreateEntityGuid();

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                                                           EntityManagerDifferOptions.IncludeReverseChangeSet |
                                                           EntityManagerDifferOptions.FastForwardShadowWorld;

                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    // Forward changes is all changes needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    // Reverse changes is all changes needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }

                // The inner shadow world was updated during the last call which means no new changes should be found.
                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
        }

        /// <summary>
        /// Generates a change set over the world without updating the shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithoutFastForward()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                                                           EntityManagerDifferOptions.IncludeReverseChangeSet;

                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    // ForwardChanges defines all operations needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    // ReverseChanges defines all operations needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }

                // Since we did not fast forward the inner shadow world. We should be able to generate the exact same changes again.
                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Mutate some component data.
                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 10 });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the one data change.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    // The ForwardChangeSet will contain a set value 10
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);

                    // The ReverseChangeSet will contain a set value 9
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                }

                SrcEntityManager.DestroyEntity(entity);
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(2, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetSharedComponentData_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedComp));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentData(entity, new EcsTestSharedComp { value = 2 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetSharedComponents.Length);
                }
            }
        }

        [Test]
        public unsafe void EntityDiffer_GetChanges_RefCounts_SharedComponents()
        {
            int RefCount1 = 0;
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedCompWithRefCount));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentData(entity, new EcsTestSharedCompWithRefCount(&RefCount1));

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }

                Assert.AreEqual(1, RefCount1);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(3, RefCount1);
                }

                Assert.AreEqual(2, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }
                
                Assert.AreEqual(2, RefCount1);
                
                SrcEntityManager.RemoveComponent<EcsTestSharedCompWithRefCount>(entity);
                
                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(1, RefCount1);
                }
                
                Assert.AreEqual(1, RefCount1);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.IncludeReverseChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }
                
                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(0, RefCount1);
                }
            }            
            Assert.AreEqual(0, RefCount1);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public unsafe void EntityDiffer_GetChanges_Clones_And_Disposes_ManagedComponents()
        {
            int RefCount1 = 1;
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestManagedCompWithRefCount));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedCompWithRefCount(&RefCount1));

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }

                Assert.AreEqual(1, RefCount1);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(3, RefCount1);
                }

                Assert.AreEqual(2, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }
                
                Assert.AreEqual(2, RefCount1);
                
                SrcEntityManager.RemoveComponent<EcsTestManagedCompWithRefCount>(entity);
                
                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(1, RefCount1);
                }
                
                Assert.AreEqual(1, RefCount1);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.IncludeReverseChangeSet, Allocator.Temp))
                {
                    Assert.AreEqual(2, RefCount1);
                }
                
                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, Allocator.Temp))
                {
                    Assert.AreEqual(0, RefCount1);
                }
            }            
            Assert.AreEqual(0, RefCount1);
        }
#endif

        [Test]
        public void EntityDiffer_GetChanges_DuplicateEntityGuidThrows()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                void GetChanges()
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp)) { }
                }

                var entityGuid0 = CreateEntityGuid();
                var entityGuid1 = CreateEntityGuid();
                var entityGuid2 = CreateEntityGuid();
                var entityGuid3 = CreateEntityGuid();

                var entity0 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity3 = SrcEntityManager.CreateEntity(typeof(EntityGuid));

                SrcEntityManager.SetComponentData(entity0, entityGuid0);
                SrcEntityManager.SetComponentData(entity1, entityGuid1);
                SrcEntityManager.SetComponentData(entity2, entityGuid2);
                SrcEntityManager.SetComponentData(entity3, entityGuid3);

                Assert.DoesNotThrow(GetChanges);

                SrcEntityManager.SetComponentData(entity1, entityGuid0);
                SrcEntityManager.SetComponentData(entity2, entityGuid0);
                SrcEntityManager.SetComponentData(entity3, entityGuid0);

                var dup0 = Assert.Throws<DuplicateEntityGuidException>(GetChanges).DuplicateEntityGuids;
                Assert.That(dup0, Is.EquivalentTo(new[] { new DuplicateEntityGuid(entityGuid0, 3) }));

                SrcEntityManager.SetComponentData(entity0, entityGuid1);
                SrcEntityManager.SetComponentData(entity3, entityGuid1);

                var dup1 = Assert.Throws<DuplicateEntityGuidException>(GetChanges).DuplicateEntityGuids;
                Assert.That(dup1, Is.EquivalentTo(new[] { new DuplicateEntityGuid(entityGuid0, 1), new DuplicateEntityGuid(entityGuid1, 1) }));
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_IgnoresSystemState()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity();
                SrcEntityManager.AddComponentData(entity, entityGuid);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp)) {}

                SrcEntityManager.AddComponentData(entity, new EcsState1 {Value = 9});
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }

                SrcEntityManager.SetComponentData(entity, new EcsState1 {Value = 10});
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
                
                // NOTE: the system state component being copied to shadow world is not required by the public API.
                //       This is simply defining the expected internal behaviour.
                Assert.AreEqual(10, differ.ShadowEntityManager.GetComponentData<EcsState1>(entity).Value);
            }
        }
        
        [Test]
        public void EntityDiffer_AddingZeroSizeComponentToWholeChunk()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                for (int i = 0; i != 10; i++)
                {
                    var entityGuid = CreateEntityGuid();
                    var entity = SrcEntityManager.CreateEntity();
                    SrcEntityManager.AddComponentData(entity, entityGuid);
                }
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp)) {}

                SrcEntityManager.AddSharedComponentData(SrcEntityManager.UniversalQuery, new SharedData1(9));
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.AnyChanges);
                    Assert.AreEqual(10, changes.ForwardChangeSet.AddComponents.Length);
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        /// <summary>
        /// Generates a change set over the world and efficiently updates the internal shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithFastForward_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                var entityGuid = CreateEntityGuid();

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                                                           EntityManagerDifferOptions.IncludeReverseChangeSet |
                                                           EntityManagerDifferOptions.FastForwardShadowWorld;

                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    // Forward changes is all changes needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    // Reverse changes is all changes needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }

                // The inner shadow world was updated during the last call which means no new changes should be found.
                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
        }

        /// <summary>
        /// Generates a change set over the world without updating the shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithoutFastForward_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                                                           EntityManagerDifferOptions.IncludeReverseChangeSet;

                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    // ForwardChanges defines all operations needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    // ReverseChanges defines all operations needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }

                // Since we did not fast forward the inner shadow world. We should be able to generate the exact same changes again.
                using (var changes = differ.GetChanges(options, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_IncrementalChanges_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Mutate some component data.
                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 10 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeOtherString" });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }


        [Test]
        public void SetComponentData_WithManagedComponent_IsDetectedBy_GetChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Only mutate managed component.
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeOtherString" });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        [StandaloneFixme] // Requires Unity.Properties support for cloning the managed components
        public void GetComponentData_WithManagedComponent_IsDetectedBy_GetChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Only mutate managed component.
                SrcEntityManager.GetComponentData<EcsTestManagedComponent>(entity).value = "SomeOtherString";

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetSharedComponentData_IncrementalChanges_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });
                SrcEntityManager.SetSharedComponentData(entity, new EcsTestSharedComp { value = 2 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetSharedComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                }
            }
        }
        
        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);

                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));
                
                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference0
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;
                    
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));
                    
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));
                    
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*) forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }
                
                blobAssetReference0.Dispose();
                
                var blobAssetReference1 = BlobAssetReference<int>.Create(20);

                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    
                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*) forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(20));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));
                    
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    
                    var reverse = changes.ReverseChangeSet;
                    Assert.That(reverse.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*) reverse.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                    Assert.That(reverse.BlobAssetReferenceChanges[0].Value, Is.EqualTo(reverse.CreatedBlobAssets[0].Hash));
                }
                
                blobAssetReference1.Dispose();
            }
        }
        
        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_SameContentHash()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);
                var blobAssetReference1 = BlobAssetReference<int>.Create(10);

                var entity0 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity0, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity0, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference0
                });
                
                var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity1, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity1, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;
                    
                    // Only one blob asset is created
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));
                    
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(2));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));
                    Assert.That(forward.BlobAssetReferenceChanges[1].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));
                    
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*) forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }
                
                SrcEntityManager.DestroyEntity(entity1);
                
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.That(changes.ForwardChangeSet.DestroyedBlobAssets.Length, Is.EqualTo(0));
                }
                
                SrcEntityManager.DestroyEntity(entity0);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.That(changes.ForwardChangeSet.DestroyedBlobAssets.Length, Is.EqualTo(1));
                }
                
                blobAssetReference0.Dispose();
                blobAssetReference1.Dispose();
            }
        }
        
        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_SharedAsset()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReference = BlobAssetReference<int>.Create(10);

                try
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));
                        SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                        SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef { value = blobAssetReference });
                    }

                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                        Assert.That(*(int*) forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                    }
                }
                finally
                {
                    blobAssetReference.Dispose();
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetComponent_Null()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));
                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(0));
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(0));
                }
            }
        }
        
        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetBuffer()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);
                
                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);
                }
                
                try
                {
                    var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                    var buffer = SrcEntityManager.AddBuffer<EcsTestDataBlobAssetElement>(entity);
                    
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i] });
                    }

                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }
                    
                    blobAssetReferences.Dispose();
                }
            }
        }
        
        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetBuffer_MultipleEntities()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);

                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);
                }
                
                for (var i = 0; i < blobAssetReferences.Length; i += 4)
                {
                    var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    
                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                    
                    var buffer = SrcEntityManager.AddBuffer<EcsTestDataBlobAssetElement>(entity);

                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 0] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 1] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 2] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 3] });
                }

                try
                {
                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }

                    blobAssetReferences.Dispose();
                }

            }
        }

        [Test]
        [StandaloneFixme]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_TypeMemoryOrdering()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.TempJob))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);

                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    // Construct the archetype in an order which will NOT match the memory order.
                    var archetype = SrcEntityManager.CreateArchetype(
                        typeof(EntityGuid),
                        typeof(EcsTestDataBlobAssetRef),
                        typeof(EcsTestData4));
                    
                    // Validate the assumption that the archetype is created in this way.
                    Assert.That(archetype.Archetype->TypeMemoryOrder[0], Is.EqualTo(0));
                    Assert.That(archetype.Archetype->TypeMemoryOrder[1], Is.EqualTo(2));
                    Assert.That(archetype.Archetype->TypeMemoryOrder[2], Is.EqualTo(1));
                    
                    // Validate the component sizes are different
                    Assert.That(UnsafeUtility.SizeOf<EcsTestDataBlobAssetRef>(), Is.Not.EqualTo(UnsafeUtility.SizeOf<EcsTestData4>()));

                    var entity = SrcEntityManager.CreateEntity(archetype);

                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);
                    
                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                    SrcEntityManager.SetComponentData(entity, new EcsTestData4());
                    SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                    {
                        value = blobAssetReferences[i]
                    });
                }

                try
                {
                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }
                    
                    blobAssetReferences.Dispose();
                }
                
            }
        }
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS
    }
}

