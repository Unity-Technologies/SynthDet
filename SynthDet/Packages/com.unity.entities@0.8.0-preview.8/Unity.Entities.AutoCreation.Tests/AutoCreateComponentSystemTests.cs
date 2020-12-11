using System;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
#if UNITY_DOTSPLAYER
using Unity.Tiny;
#endif

namespace Unity.Entities.Tests
{
    public class AutoCreateComponentSystemTestsFixture
    {
        protected World m_PreviousWorld;
        protected World World;
        protected EntityManager m_Manager;
        protected EntityManager.EntityManagerDebug m_ManagerDebug;

        protected int StressTestEntityCount = 1000;

        [SetUp]
        public void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            const string kTestWorldName = "Test World";
#if UNITY_DOTSPLAYER
            // Tiny always runs outside of process from the Editor, as such we need to
            // invoke the tiny bootstrapping for worlds manually
            World = DefaultWorldInitialization.Initialize(kTestWorldName);
#else
            World = World.DefaultGameObjectInjectionWorld = new World(kTestWorldName);
#endif

            m_Manager = World.EntityManager;
            m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);
        }

        [TearDown]
        public void TearDown()
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
    }

    // Editor manages its own SystemGroups, so this test case is meaningless there.
#if !UNITY_EDITOR
    class Issue1792 : AutoCreateComponentSystemTestsFixture
    {
        static bool aCreated = false;
        static bool bCreated = false;
        private static bool systemBWasCreated = false;

        [UpdateBefore(typeof(SystemB))]
        class SystemA : ComponentSystem
        {
            SystemB OtherSystem;

            protected override void OnCreate()
            {
                aCreated = true;
                base.OnCreate();
                OtherSystem = World.GetOrCreateSystem<SystemB>();
                systemBWasCreated = bCreated;
            }
            protected override void OnUpdate()
            {
            }
        }

        [UpdateAfter(typeof(SystemA))]
        class SystemB : ComponentSystem
        {
            protected override void OnCreate()
            {
                bCreated = true;
                base.OnCreate();
            }

            protected override void OnUpdate()
            {
            }
        }

        [Test]
        public void Test1792()
        {
            Assert.NotNull(World.GetExistingSystem<SystemA>());
            Assert.NotNull(World.GetExistingSystem<SystemB>());

            var sim = World.GetExistingSystem<SimulationSystemGroup>();
            Assert.NotNull(sim.Systems.FirstOrDefault(i => i.GetType() == typeof(SystemA)));
            Assert.NotNull(sim.Systems.FirstOrDefault(i => i.GetType() == typeof(SystemB)));
            Assert.IsTrue(aCreated);
            Assert.IsTrue(bCreated);
            Assert.IsTrue(systemBWasCreated);
        }
    }
#endif

    class AutoCreateComponentSystemTests : AutoCreateComponentSystemTestsFixture
    {
    
        internal class SystemA : ComponentSystem
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystem<SystemC>();
            }

            protected override void OnUpdate()
            {
            }
        }

        internal class SystemB : ComponentSystem
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystem<SystemA>();
            }

            protected override void OnUpdate()
            {
            }
        }

        internal class SystemC : ComponentSystem
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystem<SystemB>();
            }

            protected override void OnUpdate()
            {
            }
        }

        [Test]
        public void TestCircularAutoCreateComponentSystem()
        {
#if UNITY_EDITOR
            World.CreateSystem<SystemA>();    // Everyone else should auto-create.
#endif

            Assert.NotNull(World.GetExistingSystem<SystemA>(), "Test A not null");
            Assert.NotNull(World.GetExistingSystem<SystemB>(), "Test B not null");
            Assert.NotNull(World.GetExistingSystem<SystemC>(), "Test C not null");

#if !UNITY_EDITOR
            // Editor manages its own SystemGroups.
            var sim = World.GetExistingSystem<SimulationSystemGroup>();
            Assert.NotNull(sim.Systems.FirstOrDefault(i => i.GetType() == typeof(SystemA)), "Query A not null");
            Assert.NotNull(sim.Systems.FirstOrDefault(i => i.GetType() == typeof(SystemB)), "Query B not null");
            Assert.NotNull(sim.Systems.FirstOrDefault(i => i.GetType() == typeof(SystemC)), "QueryC not null");
#endif
        }
    }
}
