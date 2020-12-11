using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    public class WorldTests
    {
        World m_PreviousWorld;

        [SetUp]
        public virtual void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
        }

        [TearDown]
        public virtual void TearDown()
        {
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }


        [Test]
        public void ActiveWorldResets()
        {
            var copy = CopyWorlds(alsoClear: true);
            try
            {
                var worldA = new World("WorldA");
                var worldB = new World("WorldB");

                World.DefaultGameObjectInjectionWorld = worldB;

                Assert.AreEqual(worldB, World.DefaultGameObjectInjectionWorld);
                Assert.That(World.All[0], Is.EqualTo(worldA));
                Assert.That(World.All[1], Is.EqualTo(worldB));

                worldB.Dispose();

                Assert.That(worldB.IsCreated, Is.False);
                Assert.That(worldA.IsCreated, Is.True);
                Assert.That(World.DefaultGameObjectInjectionWorld, Is.Null);

                worldA.Dispose();

                Assert.That(World.All.Count, Is.EqualTo(0));
            }
            finally
            {
                ResetWorlds(copy);
            }
        }

        class TestManager : ComponentSystem
        {
            protected override void OnUpdate() {}
        }

        [Test]
        public void WorldVersionIsConsistent()
        {
            var world = new World("WorldX");

            Assert.AreEqual(0, world.Version);

            var version = world.Version;
            world.GetOrCreateSystem<TestManager>();
            Assert.AreNotEqual(version, world.Version);

            version = world.Version;
            var manager = world.GetOrCreateSystem<TestManager>();
            Assert.AreEqual(version, world.Version);

            version = world.Version;
            world.DestroySystem(manager);
            Assert.AreNotEqual(version, world.Version);

            world.Dispose();
        }

        [Test]
        public void UsingDisposedWorldThrows()
        {
            var world = new World("WorldX");
            world.Dispose();

            Assert.Throws<ArgumentException>(() => world.GetExistingSystem<TestManager>());
        }

        class AddWorldDuringConstructorThrowsSystem : ComponentSystem
        {
            public AddWorldDuringConstructorThrowsSystem()
            {
                Assert.AreEqual(null, World);
                World.DefaultGameObjectInjectionWorld.AddSystem(this);
            }

            protected override void OnUpdate() { }
        }
        [Test]
        [StandaloneFixme]
        public void AddWorldDuringConstructorThrows ()
        {
            var world = new World("WorldX");
            World.DefaultGameObjectInjectionWorld = world;
            // Adding a manager during construction is not allowed
            Assert.Throws<TargetInvocationException>(() => world.CreateSystem<AddWorldDuringConstructorThrowsSystem>());
            // The manager will not be added to the list of managers if throws
            Assert.AreEqual(0, world.Systems.Count);

            world.Dispose();
        }

        class SystemThrowingInOnCreateIsRemovedSystem : ComponentSystem
        {
            protected override void OnCreate()
            {
                throw new AssertionException("");
            }

            protected override void OnUpdate() { }
        }
        [Test]
        public void SystemThrowingInOnCreateIsRemoved()
        {
            var world = new World("WorldX");
            Assert.AreEqual(0, world.Systems.Count);

            Assert.Throws<AssertionException>(() => world.GetOrCreateSystem<SystemThrowingInOnCreateIsRemovedSystem>());

            // throwing during OnCreateManager does not add the manager to the behaviour manager list
            Assert.AreEqual(0, world.Systems.Count);

            world.Dispose();
        }

#if !NET_DOTS
        class SystemWithNonDefaultConstructor : ComponentSystem
        {
            public int data;

            public SystemWithNonDefaultConstructor(int param)
            {
                data = param;
            }

            protected override void OnUpdate() { }
        }
        [Test]
        public void SystemWithNonDefaultConstructorThrows()
        {
            var world = new World("WorldX");
            Assert.That(() => { world.CreateSystem<SystemWithNonDefaultConstructor>(); },
                Throws.TypeOf<MissingMethodException>().With.InnerException.TypeOf<MissingMethodException>());
            world.Dispose();
        }
#endif

        class SystemIsAccessibleDuringOnCreateManagerSystem : ComponentSystem
        {
            protected override void OnCreate()
            {
                Assert.AreEqual(this, World.GetOrCreateSystem<SystemIsAccessibleDuringOnCreateManagerSystem>());
            }

            protected override void OnUpdate() { }
        }
        [Test]
        public void SystemIsAccessibleDuringOnCreateManager ()
        {
            var world = new World("WorldX");
            Assert.AreEqual(0, world.Systems.Count);
            world.CreateSystem<SystemIsAccessibleDuringOnCreateManagerSystem>();
            Assert.AreEqual(1, world.Systems.Count);

            world.Dispose();
        }

        //@TODO: Test for adding a manager from one world to another.

        [Test]
        public unsafe void WorldNoOverlappingChunkSequenceNumbers()
        {
            var worldA = new World("WorldA");
            var worldB = new World("WorldB");

            World.DefaultGameObjectInjectionWorld = worldB;

            worldA.EntityManager.CreateEntity();
            worldB.EntityManager.CreateEntity();

            var worldAChunks = worldA.EntityManager.GetAllChunks();
            var worldBChunks = worldB.EntityManager.GetAllChunks();

            for (int i = 0; i < worldAChunks.Length; i++)
            {
                var chunkA = worldAChunks[i].m_Chunk;
                for (int j = 0; j < worldBChunks.Length; j++)
                {
                    var chunkB = worldBChunks[i].m_Chunk;
                    var sequenceNumberDiff = chunkA->SequenceNumber - chunkB->SequenceNumber;

                    // Any chunk sequence numbers in different worlds should be separated by at least 32 bits
                    Assert.IsTrue(sequenceNumberDiff > 1<<32 );
                }
            }

            worldAChunks.Dispose();
            worldBChunks.Dispose();

            worldA.Dispose();
            worldB.Dispose();
        }

        [Test]
        public unsafe void WorldChunkSequenceNumbersNotReused()
        {
            var worldA = new World("WorldA");

            ulong lastChunkSequenceNumber = 0;
            {
                var entity = worldA.EntityManager.CreateEntity();
                var chunk = worldA.EntityManager.GetChunk(entity);
                lastChunkSequenceNumber = chunk.m_Chunk->SequenceNumber;

                worldA.EntityManager.DestroyEntity(entity);
            }

            for (int i = 0; i < 1000; i++)
            {
                var entity = worldA.EntityManager.CreateEntity();
                var chunk = worldA.EntityManager.GetChunk(entity);
                var chunkSequenceNumber = chunk.m_Chunk->SequenceNumber;

                // Sequence numbers should be increasing and should not be reused when chunk is re-used (after zero count)
                Assert.IsTrue(chunkSequenceNumber > lastChunkSequenceNumber );
                lastChunkSequenceNumber = chunkSequenceNumber;

                worldA.EntityManager.DestroyEntity(entity);
            }

            worldA.Dispose();
        }

        [UpdateInGroup(typeof(SimulationSystemGroup))]
        public class UpdateCountSystem : ComponentSystem
        {
            public double lastUpdateTime;
            public float lastUpdateDeltaTime;
            public int updateCount;

            protected override void OnUpdate()
            {
                lastUpdateTime = Time.ElapsedTime;
                lastUpdateDeltaTime = Time.DeltaTime;
                updateCount++;
            }
        }

        [Test]
        public void WorldSimulationFixedStep()
        {
            var world = new World("World A");
            var sim = world.GetOrCreateSystem<SimulationSystemGroup>();
            var uc = world.GetOrCreateSystem<UpdateCountSystem>();
            sim.AddSystemToUpdateList(uc);

            // Unity.Core.Hybrid.UpdateWorldTimeSystem
            var timeData = new TimeData();

            void AdvanceWorldTime(float amount)
            {
                uc.updateCount = 0;
                timeData = new TimeData(timeData.ElapsedTime + amount, amount);
                world.SetTime(timeData);
            }

            FixedRateUtils.EnableFixedRateWithCatchUp(sim, 1.0f);

            // first frame will tick immediately
            AdvanceWorldTime(0.5f);
            world.Update();
            Assert.AreEqual(0.5, uc.lastUpdateTime, 0.001f);
            Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
            Assert.AreEqual(1, uc.updateCount);

            AdvanceWorldTime(1.1f);
            world.Update();
            Assert.AreEqual(1.5, uc.lastUpdateTime, 0.001f);
            Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
            Assert.AreEqual(1, uc.updateCount);

            // No update should happen because the time elapsed is less than the interval
            AdvanceWorldTime(0.1f);
            world.Update();
            Assert.AreEqual(1.5, uc.lastUpdateTime, 0.001f);
            Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
            Assert.AreEqual(0, uc.updateCount);

            AdvanceWorldTime(1.0f);
            world.Update();
            Assert.AreEqual(2.5, uc.lastUpdateTime, 0.001f);
            Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
            Assert.AreEqual(1, uc.updateCount);

            // If time jumps by a lot, we should tick the fixed rate systems
            // multiple times
            AdvanceWorldTime(2.0f);
            world.Update();
            Assert.AreEqual(4.5, uc.lastUpdateTime, 0.001f);
            Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
            Assert.AreEqual(2, uc.updateCount);

            world.Dispose();
        }

        [Test]
        public void DisposeAllWorlds()
        {
            var worlds = CopyWorlds(alsoClear: true);
            try
            {
                var createdWorlds = new[] { new World("a"), new World("b") };

                foreach (var world in World.All)
                {
                    Assert.That(world.IsCreated, Is.True);

                }

                World.DisposeAllWorlds();

                Assert.That(World.All.Count, Is.EqualTo(0));
                Assert.That(createdWorlds.All(w => w.IsCreated), Is.False);
            }
            finally
            {
                ResetWorlds(worlds);
            }
        }

        [Test]
        public void IteratingOverBoxedNoAllocReadOnlyCollectionThrows()
        {
            var sourceList = Enumerable.Range(1, 10).ToList();
            var readOnlyCollection = new World.NoAllocReadOnlyCollection<int>(sourceList);

            var ex = Assert.Throws<NotSupportedException>(() => ((IEnumerable<int>) readOnlyCollection).GetEnumerator());
            var ex2 = Assert.Throws<NotSupportedException>(() => ((IEnumerable) readOnlyCollection).GetEnumerator());
            Assert.That(ex.Message, Is.EqualTo($"To avoid boxing, do not cast {nameof(World.NoAllocReadOnlyCollection<int>)} to IEnumerable<T>."));
            Assert.That(ex2.Message, Is.EqualTo($"To avoid boxing, do not cast {nameof(World.NoAllocReadOnlyCollection<int>)} to IEnumerable."));
        }

#if UNITY_EDITOR
        [Test]
        public void WorldTimeSingletonHasAnEntityName()
        {
            using (var world = new World("w"))
            using (var timeSingletonQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<WorldTime>(), ComponentType.ReadWrite<WorldTimeQueue>()))
            {
                Assert.That(timeSingletonQuery.IsEmptyIgnoreFilter, Is.True);

                world.SetTime(new TimeData(10, 0.1f));
                Assert.That(timeSingletonQuery.IsEmptyIgnoreFilter, Is.False);
                var timeSingleton = timeSingletonQuery.GetSingletonEntity();
                Assert.That(world.EntityManager.GetName(timeSingleton), Is.EqualTo("WorldTime"));
            }
        }
#endif

        public class ContainerOwnerSystem : JobComponentSystem
        {
            public NativeArray<int> Container;
            protected override void OnCreate()
            {
                Container = new NativeArray<int>(1, Allocator.Persistent);
            }
            protected override void OnDestroy()
            {
                Container.Dispose();
            }
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return inputDeps;
            }
        }

        public class ContainerUsingSystem : JobComponentSystem
        {
            public struct ContainerJob : IJob
            {
                public NativeArray<int> Container;
                public void Execute()
                {}
            }
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                var job = new ContainerJob{Container = World.GetExistingSystem<ContainerOwnerSystem>().Container};
                return job.Schedule(inputDeps);
            }
        }

        [Test]
        public void World_DisposeWithRunningJobs_Succeeds()
        {
            var w = new World("Test");
            // Ordering is important, the owner system needs to be destroyed before the user system
            var user = w.GetOrCreateSystem<ContainerUsingSystem>();
            var owner = w.GetOrCreateSystem<ContainerOwnerSystem>();

            owner.Update();
            user.Update();
            w.Dispose();
        }

        public static World[] CopyWorlds(bool alsoClear = false)
        {
            var worlds = World.s_AllWorlds.ToArray();
            if (alsoClear)
                World.s_AllWorlds.Clear();
            return worlds;
        }

        public static void ResetWorlds(params World[] world)
        {
            World.s_AllWorlds.Clear();
            foreach (var w in world)
            {
                World.s_AllWorlds.Add(w);
            }
        }
    }
}
