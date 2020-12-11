using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Transforms;

#pragma warning disable 649

[assembly: RegisterGenericComponentType(typeof(TypeManagerTests.GenericComponent<int>))]
[assembly: RegisterGenericComponentType(typeof(TypeManagerTests.GenericComponent<short>))]

namespace Unity.Entities
{
    // mock type
    // TODO: RENAME ME - collides with the real type
    class GameObjectEntity
    {
    }
}

namespace Unity.Entities.Tests
{
    class TypeManagerTests : ECSTestsFixture
    {
        struct TestType1 : IComponentData
        {
            int empty;
        }
        struct TestType2 : IComponentData
        {
            int empty;
        }
        struct TestTypeWithBool : IComponentData, IEquatable<TestTypeWithBool>
        {
            bool empty;

            public bool Equals(TestTypeWithBool other)
            {
                return other.empty == empty;
            }

            public override int GetHashCode()
            {
                return empty.GetHashCode();
            }
        }
        struct TestTypeWithChar : IComponentData, IEquatable<TestTypeWithChar>
        {
            char empty;

            public bool Equals(TestTypeWithChar other)
            {
                return empty == other.empty;
            }

            public override int GetHashCode()
            {
                return empty.GetHashCode();
            }
        }

        public struct GenericComponent<T> : IComponentData
        {
            T value;
        }

        [Test]
        public void CreateArchetypes()
        {
            var archetype1 = m_Manager.CreateArchetype(ComponentType.ReadWrite<TestType1>(), ComponentType.ReadWrite<TestType2>());
            var archetype1Same = m_Manager.CreateArchetype(ComponentType.ReadWrite<TestType1>(), ComponentType.ReadWrite<TestType2>());
            Assert.AreEqual(archetype1, archetype1Same);

            var archetype2 = m_Manager.CreateArchetype(ComponentType.ReadWrite<TestType1>());
            var archetype2Same = m_Manager.CreateArchetype(ComponentType.ReadWrite<TestType1>());
            Assert.AreEqual(archetype2Same, archetype2Same);

            Assert.AreNotEqual(archetype1, archetype2);
        }

        [Test]
        public void TestPrimitiveButNotBlittableTypesAllowed()
        {
            Assert.AreEqual(1, TypeManager.GetTypeInfo<TestTypeWithBool>().SizeInChunk);
            Assert.AreEqual(2, TypeManager.GetTypeInfo<TestTypeWithChar>().SizeInChunk);
        }

        // We need to decide whether this should actually be allowed; for now, add a test to make sure
        // we don't break things more than they already are.
        

        [Test]
        public void TestGenericComponents()
        {
            var index1 = TypeManager.GetTypeIndex<GenericComponent<int>>();
            var index2 = TypeManager.GetTypeIndex<GenericComponent<short>>();

            Assert.AreNotEqual(index1, index2);
        }
        
        [Test]
        public void TestGenericComponentsThrowsOnUnregisteredGeneric()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                TypeManager.GetTypeIndex<GenericComponent<long>>();
            });
        }

        [InternalBufferCapacity(99)]
        public struct IntElement : IBufferElementData
        {
            public int Value;
        }

        [Test]
        public void BufferTypeClassificationWorks()
        {
            var t  = TypeManager.GetTypeInfo<IntElement>();
            Assert.AreEqual(TypeManager.TypeCategory.BufferData, t.Category);
            Assert.AreEqual(99, t.BufferCapacity);
            Assert.AreEqual(UnsafeUtility.SizeOf<BufferHeader>() + 99 * sizeof(int), t.SizeInChunk);
        }

        [Test]
        public void TestTypeManager()
        {
            var entityType = ComponentType.ReadWrite<Entity>();
            var testDataType = ComponentType.ReadWrite<EcsTestData>();

            Assert.AreEqual(entityType, ComponentType.ReadWrite<Entity>());
            Assert.AreEqual(entityType, new ComponentType(typeof(Entity)));
            Assert.AreEqual(testDataType, ComponentType.ReadWrite<EcsTestData>());
            Assert.AreEqual(testDataType, new ComponentType(typeof(EcsTestData)));
            Assert.AreNotEqual(ComponentType.ReadWrite<Entity>(), ComponentType.ReadWrite<EcsTestData>());

            Assert.AreEqual(ComponentType.AccessMode.ReadOnly, ComponentType.ReadOnly<EcsTestData>().AccessModeType);
            Assert.AreEqual(ComponentType.AccessMode.ReadOnly, ComponentType.ReadOnly(typeof(EcsTestData)).AccessModeType);

            Assert.AreEqual(typeof(Entity), entityType.GetManagedType());
        }

#if !NET_DOTS
        [Test]
        public void TestCreateTypeIndexFor()
        {
#pragma warning disable 618
            int typeIndex = TypeManager.CreateTypeIndexForComponent<EcsTestData>();
            Assert.Greater(typeIndex, 1);

            typeIndex = TypeManager.CreateTypeIndexForComponent<EcsTestTag>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsZeroSized(typeIndex));

            typeIndex = TypeManager.CreateTypeIndexForComponent<EcsStateTag1>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsZeroSized(typeIndex));
            Assert.IsTrue(TypeManager.IsSystemStateComponent(typeIndex));

            typeIndex = TypeManager.CreateTypeIndexForComponent<EcsTestDataEntity>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.HasEntityReferences(typeIndex));

            typeIndex = TypeManager.CreateTypeIndexForBufferElement<EcsIntElement>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsBuffer(typeIndex));

            typeIndex = TypeManager.CreateTypeIndexForBufferElement<EcsIntStateElement>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsBuffer(typeIndex));
            Assert.IsTrue(TypeManager.IsSystemStateComponent(typeIndex));

            typeIndex = TypeManager.CreateTypeIndexForSharedComponent<EcsTestSharedComp>();
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsSharedComponent(typeIndex));
        }
#pragma warning restore 618
#endif

        [Test]
        public void TestAlignUp_Align0ToPow2()
        {
            Assert.AreEqual(0, CollectionHelper.Align(0, 1));
            Assert.AreEqual(0, CollectionHelper.Align(0, 2));
            Assert.AreEqual(0, CollectionHelper.Align(0, 4));
            Assert.AreEqual(0, CollectionHelper.Align(0, 8));
            Assert.AreEqual(0, CollectionHelper.Align(0, 16));
            Assert.AreEqual(0, CollectionHelper.Align(0, 32));
            Assert.AreEqual(0, CollectionHelper.Align(0, 64));
            Assert.AreEqual(0, CollectionHelper.Align(0, 128));
        }
        
        [Test]
        public void TestAlignUp_AlignMultipleOfAlignment()
        {
            Assert.AreEqual(2, CollectionHelper.Align(2, 1));
            Assert.AreEqual(4, CollectionHelper.Align(4, 2));
            Assert.AreEqual(8, CollectionHelper.Align(8, 4));
            Assert.AreEqual(16, CollectionHelper.Align(16, 8));
            Assert.AreEqual(32, CollectionHelper.Align(32, 16));
            Assert.AreEqual(64, CollectionHelper.Align(64, 32));
            Assert.AreEqual(128, CollectionHelper.Align(128, 64));
            Assert.AreEqual(256, CollectionHelper.Align(256, 128));
        }
        
        [Test]
        public void TestAlignUp_Align1ToPow2()
        {
            Assert.AreEqual(1, CollectionHelper.Align(1, 1));
            Assert.AreEqual(2, CollectionHelper.Align(1, 2));
            Assert.AreEqual(4, CollectionHelper.Align(1, 4));
            Assert.AreEqual(8, CollectionHelper.Align(1, 8));
            Assert.AreEqual(16, CollectionHelper.Align(1, 16));
            Assert.AreEqual(32, CollectionHelper.Align(1, 32));
            Assert.AreEqual(64, CollectionHelper.Align(1, 64));
            Assert.AreEqual(128, CollectionHelper.Align(1, 128));
        }
        
        [Test]
        public void TestAlignUp_Align3ToPow2()
        {
            Assert.AreEqual(3, CollectionHelper.Align(3, 1));
            Assert.AreEqual(4, CollectionHelper.Align(3, 2));
            Assert.AreEqual(4, CollectionHelper.Align(3, 4));
            Assert.AreEqual(8, CollectionHelper.Align(3, 8));
            Assert.AreEqual(16, CollectionHelper.Align(3, 16));
            Assert.AreEqual(32, CollectionHelper.Align(3, 32));
            Assert.AreEqual(64, CollectionHelper.Align(3, 64));
            Assert.AreEqual(128, CollectionHelper.Align(3, 128));
        }
        
        [Test]
        public void TestAlignUp_Align15ToPow2()
        {
            Assert.AreEqual(15, CollectionHelper.Align(15, 1));
            Assert.AreEqual(16, CollectionHelper.Align(15, 2));
            Assert.AreEqual(16, CollectionHelper.Align(15, 4));
            Assert.AreEqual(16, CollectionHelper.Align(15, 8));
            Assert.AreEqual(16, CollectionHelper.Align(15, 16));
            Assert.AreEqual(32, CollectionHelper.Align(15, 32));
            Assert.AreEqual(64, CollectionHelper.Align(15, 64));
            Assert.AreEqual(128, CollectionHelper.Align(15, 128));
        }
        
        [Test]
        public void TestAlignUp_Align63ToPow2()
        {
            Assert.AreEqual(63, CollectionHelper.Align(63, 1));
            Assert.AreEqual(64, CollectionHelper.Align(63, 2));
            Assert.AreEqual(64, CollectionHelper.Align(63, 4));
            Assert.AreEqual(64, CollectionHelper.Align(63, 8));
            Assert.AreEqual(64, CollectionHelper.Align(63, 16));
            Assert.AreEqual(64, CollectionHelper.Align(63, 32));
            Assert.AreEqual(64, CollectionHelper.Align(63, 64));
            Assert.AreEqual(128, CollectionHelper.Align(63, 128));
        }
        
        [Test]
        public void TestAlignUp_ZeroAlignment()
        {
            for (int value = 0; value < 512; ++value)
            {
                Assert.AreEqual(value, CollectionHelper.Align(value, 0));
            }
        }

#if !UNITY_DOTSPLAYER
        [DisableAutoTypeRegistration]
        struct NonBlittableComponentData : IComponentData
        {
            string empty;
        }

        [DisableAutoTypeRegistration]
        struct NonBlittableComponentData2 : IComponentData
        {
            IComponentData empty;
        }

        interface InterfaceComponentData : IComponentData
        {
        }

        [DisableAutoTypeRegistration]
        struct NonBlittableBuffer: IBufferElementData
        {
            string empty;
        }

        class ClassBuffer: IBufferElementData
        {
        }

        interface InterfaceBuffer : IBufferElementData
        {
        }

        class ClassShared : ISharedComponentData
        {
        }

        interface InterfaceShared : ISharedComponentData
        {
        }

        [TestCase(typeof(InterfaceComponentData), @"\binterface\b", TestName = "Interface implementing IComponentData")]
        [TestCase(typeof(NonBlittableComponentData), @"\bblittable\b", TestName = "Non-blittable component data (string)")]
        [TestCase(typeof(NonBlittableComponentData2), @"\bblittable\b", TestName = "Non-blittable component data (interface)")]

        [TestCase(typeof(InterfaceBuffer), @"\binterface\b", TestName = "Interface implementing IBufferElementData")]
        [TestCase(typeof(ClassBuffer), @"\b(struct|class)\b", TestName = "Class implementing IBufferElementData")]
        [TestCase(typeof(NonBlittableBuffer), @"\bblittable\b", TestName = "Non-blittable IBufferElementData")]

        [TestCase(typeof(InterfaceShared), @"\binterface\b", TestName = "Interface implementing ISharedComponentData")]
        [TestCase(typeof(ClassShared), @"\b(struct|class)\b", TestName = "Class implementing ISharedComponentData")]

        [TestCase(typeof(GameObjectEntity), nameof(GameObjectEntity), TestName = "GameObjectEntity type")]

        [TestCase(typeof(float), @"\b(not .*|in)valid\b", TestName = "Not valid component type")]
        public void BuildComponentType_ThrowsArgumentException_WithExpectedFailures(Type type, string keywordPattern)
        {
            Assert.That(
                () => TypeManager.BuildComponentType(type),
                Throws.ArgumentException.With.Message.Matches(keywordPattern)
            );
        }

        [Test]
        public void ManagedFieldLayoutWorks()
        {
            var t  = TypeManager.GetTypeInfo<EcsStringSharedComponent>();
            var layout = TypeManager.GetFastEqualityTypeInfo(t);
            Assert.IsNull(layout.Layouts);
            Assert.IsNotNull(layout.GetHashFn);
            Assert.IsNotNull(layout.EqualFn);
        }


        [TestCase(typeof(UnityEngine.Transform))]
        [TestCase(typeof(TypeManagerTests))]
        public void BuildComponentType_WithClass_WhenUnityEngineObjectTypeIsNull_ThrowsArgumentException(Type type)
        {
            var componentType = TypeManager.UnityEngineObjectType;
            TypeManager.UnityEngineObjectType = null;
            try
            {
                Assert.That(
                    () => TypeManager.BuildComponentType(type),
                    Throws.ArgumentException.With.Message.Matches($"\\bregister\\b.*\\b{nameof(TypeManager.UnityEngineObjectType)}\\b")
                );
            }
            finally
            {
                TypeManager.UnityEngineObjectType = componentType;
            }
        }

        [Test]
        public void BuildComponentType_WithNonComponent_WhenUnityEngineObjectTypeIsCorrect_ThrowsArgumentException()
        {
            var componentType = TypeManager.UnityEngineObjectType;
            TypeManager.UnityEngineObjectType = typeof(UnityEngine.Component);
            try
            {
                var type = typeof(TypeManagerTests);
                Assert.That(
                    () => TypeManager.BuildComponentType(type),
                    Throws.ArgumentException.With.Message.Matches($"\\bmust inherit {typeof(UnityEngine.Component)}\\b")
                );
            }
            finally
            {
                TypeManager.UnityEngineObjectType = componentType;
            }
        }

        [Test]
        public void BuildComponentType_WithComponent_WhenUnityEngineObjectTypeIsCorrect_DoesNotThrowException()
        {
            var componentType = TypeManager.UnityEngineObjectType;
            TypeManager.UnityEngineObjectType = typeof(UnityEngine.Component);
            try
            {
                TypeManager.BuildComponentType(typeof(UnityEngine.Transform));
            }
            finally
            {
                TypeManager.UnityEngineObjectType = componentType;
            }
        }

        [TestCase(null)]
        [TestCase(typeof(TestType1))]
        [TestCase(typeof(InterfaceShared))]
        [TestCase(typeof(ClassShared))]
        [TestCase(typeof(UnityEngine.Transform))]
        public void RegisterUnityEngineObjectType_WithWrongType_ThrowsArgumentException(Type type)
        {
            Assert.Throws<ArgumentException>(() => TypeManager.RegisterUnityEngineObjectType(type));
        }

        [Test]
        public void IsAssemblyReferencingEntities()
        {
            Assert.IsFalse(TypeManager.IsAssemblyReferencingEntities(typeof(UnityEngine.GameObject).Assembly));
            Assert.IsFalse(TypeManager.IsAssemblyReferencingEntities(typeof(System.Collections.Generic.List<>).Assembly));
            Assert.IsFalse(TypeManager.IsAssemblyReferencingEntities(typeof(Collections.NativeList<>).Assembly));

            Assert.IsTrue(TypeManager.IsAssemblyReferencingEntities(typeof(IComponentData).Assembly));
            Assert.IsTrue(TypeManager.IsAssemblyReferencingEntities(typeof(EcsTestData).Assembly));
        }

        class TestSystem : ComponentSystem
        {
            EntityQuery m_EntityQuery;

            protected override void OnCreate()
            {
                m_EntityQuery = GetEntityQuery(typeof(Translation));
            }

            protected override void OnUpdate()
            {
                Entities.With(m_EntityQuery).ForEach((Entity e, ref Translation t) =>
                {
                });
            }
        }

        [DisableAutoTypeRegistration]
        public struct UnregisteredComponent : IComponentData
        {
            public int Int;
        }
        
        [Test]
        public void AddNewComponentTypes()
        {
            var typeToAdd = typeof(UnregisteredComponent);
            bool testAlreadyRan = false;
            try
            {
                TypeManager.GetTypeIndex(typeToAdd);
                testAlreadyRan = true;
            }
            catch (ArgumentException){ }

            // If we haven't registered the component yet we should have thrown above before setting 
            // however, since we cannot remove types from the TypeManager, subsequent test
            // runs without a domain reload will already have our test type registered so simply abort
            if (testAlreadyRan)
                return;

            Dictionary<int, TypeManager.TypeInfo> typeInfoMap = new Dictionary<int, TypeManager.TypeInfo>();
            Dictionary<int, int[]> entityOffsetMap = new Dictionary<int, int[]>();
            Dictionary<int, int[]> blobOffsetMap = new Dictionary<int, int[]>();
            Dictionary<int, int[]> writeGroupMap = new Dictionary<int, int[]>();

            void AddTypeInfoToCache(TypeManager.TypeInfo ti)
            {
                unsafe
                {
                    var typeIndex = ti.TypeIndex;
                    var entityOffsets = new int[ti.EntityOffsetCount];
                    var blobOffsets = new int[ti.BlobAssetRefOffsetCount];
                    var writeGroups = new int[ti.WriteGroupCount];

                    typeInfoMap.Add(typeIndex, ti);

                    var pEntityOffsets = TypeManager.GetEntityOffsets(ti);
                    for (int i = 0; i < ti.EntityOffsetCount; ++i)
                        entityOffsets[i] = pEntityOffsets[i].Offset;
                    entityOffsetMap.Add(typeIndex, entityOffsets);

                    var pBlobOffsets = TypeManager.GetBlobAssetRefOffsets(ti);
                    for (int i = 0; i < ti.BlobAssetRefOffsetCount; ++i)
                        blobOffsets[i] = pBlobOffsets[i].Offset;
                    blobOffsetMap.Add(typeIndex, blobOffsets);

                    var pWriteGroups = TypeManager.GetWriteGroups(ti);
                    for (int i = 0; i < ti.WriteGroupCount; ++i)
                        writeGroups[i] = pWriteGroups[i];
                    writeGroupMap.Add(typeIndex, writeGroups);
                }
            }

            unsafe void EnsureMatch(TypeManager.TypeInfo expected, TypeManager.TypeInfo actual)
            {
                Assert.AreEqual(expected.TypeIndex, actual.TypeIndex);
                Assert.AreEqual(expected.SizeInChunk, actual.SizeInChunk);
                Assert.AreEqual(expected.ElementSize, actual.ElementSize);
                Assert.AreEqual(expected.AlignmentInBytes, actual.AlignmentInBytes);
                Assert.AreEqual(expected.BufferCapacity, actual.BufferCapacity);
                Assert.AreEqual(expected.FastEqualityIndex, actual.FastEqualityIndex);
                Assert.AreEqual(expected.Category, actual.Category);

                Assert.AreEqual(expected.EntityOffsetCount, actual.EntityOffsetCount);
                var expectedEntityOffsets = entityOffsetMap[expected.TypeIndex];
                var pActualEntityOffsets = TypeManager.GetEntityOffsets(actual);
                for (int i = 0; i < actual.EntityOffsetCount; ++i)
                {
                    Assert.AreEqual(expectedEntityOffsets[i], pActualEntityOffsets[i].Offset);
                }

                Assert.AreEqual(expected.BlobAssetRefOffsetCount, actual.BlobAssetRefOffsetCount);
                var expectedBlobOffsets = blobOffsetMap[expected.TypeIndex];
                var pActualBlobOffsets = TypeManager.GetBlobAssetRefOffsets(actual);
                for (int i = 0; i < actual.BlobAssetRefOffsetCount; ++i)
                {
                    Assert.AreEqual(expectedBlobOffsets[i], pActualBlobOffsets[i].Offset);
                }

                Assert.AreEqual(expected.WriteGroupCount, actual.WriteGroupCount);
                var expectedWriteGroups = writeGroupMap[expected.TypeIndex];
                var pActualWriteGroups = TypeManager.GetWriteGroups(actual);
                for (int i = 0; i < actual.WriteGroupCount; ++i)
                {
                    Assert.AreEqual(expectedWriteGroups[i], pActualWriteGroups[i]);
                }

                Assert.AreEqual(expected.MemoryOrdering, actual.MemoryOrdering);
                Assert.AreEqual(expected.StableTypeHash, actual.StableTypeHash);
                Assert.AreEqual(expected.MaximumChunkCapacity, actual.MaximumChunkCapacity);
                Assert.AreEqual(expected.AlignmentInChunkInBytes, actual.AlignmentInChunkInBytes);
                Assert.AreEqual(expected.Type, actual.Type);
                Assert.AreEqual(expected.IsZeroSized, actual.IsZeroSized);
                Assert.AreEqual(expected.HasWriteGroups, actual.HasWriteGroups);
                Assert.AreEqual(expected.HasEntities, actual.HasEntities);
            }

            foreach (var ti in TypeManager.AllTypes)
            {
                AddTypeInfoToCache(ti);
            }

            World w = new World("AddNewComponentsTestWorld");
            w.GetOrCreateSystem<TestSystem>();

            // Ensure all the Types in the TypeManager match what we think they are
            foreach (var ti in TypeManager.AllTypes)
                EnsureMatch(typeInfoMap[ti.TypeIndex], ti);

            
            Assert.Throws<ArgumentException>(() => TypeManager.GetTypeIndex(typeToAdd));
            TypeManager.AddNewComponentTypes(new Type[] { typeToAdd });

            // Now that we added a new type, again ensure all the Types in the TypeManager match what we think they are
            // to ensure adding the new type didn't change any other type info
            foreach (var ti in TypeManager.AllTypes)
            {
                if (typeInfoMap.ContainsKey(ti.TypeIndex))
                    EnsureMatch(typeInfoMap[ti.TypeIndex], ti);
                else
                {
                    // We should only enter this case for 'UnregisteredComponent'
                    Assert.AreEqual(ti.Type, typeof(UnregisteredComponent));
                    AddTypeInfoToCache(ti);
                }
            }

            var e2 = w.EntityManager.CreateEntity(typeof(Translation), typeToAdd);

            // If adding the type did not succeed we might throw for many different reasons
            // stemming from bad type information. In fact even succeeding could cause issues if someone caches the
            // internal SharedStatic pointers (which is done, and now handled for, in the EntityComponentStore) 
            Assert.DoesNotThrow(()=> w.Update());
            Assert.DoesNotThrow(() => w.EntityManager.CreateEntity(typeof(Translation), typeToAdd));
            Assert.DoesNotThrow(() => TypeManager.GetTypeIndex(typeToAdd));

            // We do not allow anyone to re-add the same type so ensure we throw
            Assert.Throws<ArgumentException>(()=>TypeManager.AddNewComponentTypes(new Type[] { typeToAdd }));
        }
#endif

#if !UNITY_DISABLE_MANAGED_COMPONENTS
#if !NET_DOTS
        [DisableAutoTypeRegistration]
        class ManagedComponentDataNoDefaultConstructor : IComponentData, IEquatable<ManagedComponentDataNoDefaultConstructor>
        {
            string String;

            public ManagedComponentDataNoDefaultConstructor(int s)
            {
                String = s.ToString();
            }

            public bool Equals(ManagedComponentDataNoDefaultConstructor other)
            {
                throw new NotImplementedException();
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        [TestCase(typeof(ManagedComponentDataNoDefaultConstructor), @"\b(default constructor)\b", TestName = "Class IComponentData with no default constructor")]
        public void BuildComponentType_ThrowsArgumentException_WithExpectedFailures_ManagedComponents(Type type, string keywordPattern)
        {
            Assert.That(
                () => TypeManager.BuildComponentType(type),
                Throws.ArgumentException.With.Message.Matches(keywordPattern)
            );
        }

        [Test]
        public void TestCreateTypeIndexFor_ManagedComponents()
        {
#pragma warning disable 618
            int typeIndex = TypeManager.CreateTypeIndexForComponent<EcsTestManagedComponent>();
#pragma warning restore 618
            Assert.Greater(typeIndex, 1);
            Assert.IsTrue(TypeManager.IsManagedComponent(typeIndex));
        }
        
#pragma warning disable 649
        class TestScriptableObjectWithFields : UnityEngine.ScriptableObject
        {
            public int Value;
        }
        
        class ComponentWithScriptableObjectReference : IComponentData
        {
            public TestScriptableObjectWithFields Value;
        }
#pragma warning restore 649
        [Test]
        public void TypeManagerGetHashCode_WithNullScriptableObject_DoesNotThrow()
        {
            var component = new ComponentWithScriptableObjectReference();
            Assert.DoesNotThrow(() =>
            {
                TypeManager.GetHashCode(component, TypeManager.GetTypeIndex<ComponentWithScriptableObjectReference>());
            });
        }
#endif
#endif
    }
}
