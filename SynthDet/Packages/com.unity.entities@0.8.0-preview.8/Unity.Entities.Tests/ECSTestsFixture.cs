using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Profiling;

namespace Unity.Entities.Tests
{
#if NET_DOTS
    public class EmptySystem : ComponentSystem
    {
        protected override void OnUpdate()
        {

        }
        public new EntityQuery GetEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            return base.GetEntityQuery(queriesDesc);
        }

        public new EntityQuery GetEntityQuery(params ComponentType[] componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }
        public new EntityQuery GetEntityQuery(NativeArray<ComponentType> componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }
        public new BufferFromEntity<T> GetBufferFromEntity<T>(bool isReadOnly = false) where T : struct, IBufferElementData
        {
            AddReaderWriter(isReadOnly ? ComponentType.ReadOnly<T>() : ComponentType.ReadWrite<T>());
            return EntityManager.GetBufferFromEntity<T>(isReadOnly);
        }
    }
#else
    public class EmptySystem : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle dep) { return dep; }


        new public EntityQuery GetEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            return base.GetEntityQuery(queriesDesc);
        }

        new public EntityQuery GetEntityQuery(params ComponentType[] componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }
        new public EntityQuery GetEntityQuery(NativeArray<ComponentType> componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }
    }
    
#endif
    
    public abstract class ECSTestsFixture
    {
        protected World m_PreviousWorld;
        protected World World;
        protected EntityManager m_Manager;
        protected EntityManager.EntityManagerDebug m_ManagerDebug;

        protected int StressTestEntityCount = 1000;

        [SetUp]
        public virtual void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            m_Manager = World.EntityManager;
            m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (m_Manager != null && m_Manager.IsCreated)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (World.Systems.Count > 0)
                {
                    World.DestroySystem(World.Systems[0]);
                }

                m_ManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = null;
            }
        }

        public void AssertDoesNotExist(Entity entity)
        {
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsFalse(m_Manager.Exists(entity));
        }

        public void AssertComponentData(Entity entity, int index)
        {
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            Assert.AreEqual(-index, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            Assert.AreEqual(-index, m_Manager.GetComponentData<EcsTestData2>(entity).value1);
            Assert.AreEqual(index, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

        public Entity CreateEntityWithDefaultData(int index)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            // HasComponent & Exists setup correctly
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            // Create must initialize values to zero
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity).value1);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);

            // Setup some non zero default values
            m_Manager.SetComponentData(entity, new EcsTestData2(-index));
            m_Manager.SetComponentData(entity, new EcsTestData(index));

            AssertComponentData(entity, index);

            return entity;
        }

        public void AssertSameChunk(Entity e0, Entity e1)
        {
            Assert.AreEqual(m_Manager.GetChunk(e0), m_Manager.GetChunk(e1));
        }

        public void AssertHasVersion<T>(Entity e, uint version) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct, 
#endif
            IComponentData
        {
            var type = m_Manager.GetArchetypeChunkComponentType<T>(true);
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetComponentVersion(type));
            Assert.IsFalse(chunk.DidChange(type, version));
            Assert.IsTrue(chunk.DidChange(type, version-1));
        }
        
        public void AssertHasBufferVersion<T>(Entity e, uint version) where T : struct, IBufferElementData
        {
            var type = m_Manager.GetArchetypeChunkBufferType<T>(true);
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetComponentVersion(type));
            Assert.IsFalse(chunk.DidChange(type, version));
            Assert.IsTrue(chunk.DidChange(type, version-1));
        }

        public void AssertHasSharedVersion<T>(Entity e, uint version) where T : struct, ISharedComponentData
        {
            var type = m_Manager.GetArchetypeChunkSharedComponentType<T>();
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetComponentVersion(type));
            Assert.IsFalse(chunk.DidChange(type, version));
            Assert.IsTrue(chunk.DidChange(type, version-1));
        }

        class EntityForEachSystem : ComponentSystem
        {
            protected override void OnUpdate() {  }
        }
        protected EntityQueryBuilder Entities
        {
            get
            {
                return new EntityQueryBuilder(World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EntityForEachSystem>());
            }
        }

        public EmptySystem EmptySystem
        {
            get
            {
                return World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EmptySystem>();
            }
        }
    }
}
