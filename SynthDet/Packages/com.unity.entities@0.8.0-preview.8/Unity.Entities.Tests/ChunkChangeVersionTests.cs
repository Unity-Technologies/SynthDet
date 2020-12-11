using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    class ChunkChangeVersionTests : ECSTestsFixture
    {
        const uint OldVersion = 42;
        const uint NewVersion = 43;

        public override void Setup()
        {
            base.Setup();
            m_Manager.Debug.SetGlobalSystemVersion(OldVersion);
        }

        void BumpGlobalSystemVersion()
        {
            m_Manager.Debug.SetGlobalSystemVersion(NewVersion);
        }

        [Test]
        public void VersionWrapAround()
        {
            var firstSystemFrame = 0U;
            var initial = ChangeVersionUtility.InitialGlobalSystemVersion;
            var lastVersion = uint.MaxValue;
            var lastVersionPlus = lastVersion;
            ChangeVersionUtility.IncrementGlobalSystemVersion(ref lastVersionPlus);

            // In order to support wrap around we wrap numbers
            Assert.IsTrue(ChangeVersionUtility.DidChange(initial+1, initial));
            Assert.IsTrue(ChangeVersionUtility.DidChange(lastVersion / 2 - 10U, initial));
            Assert.IsFalse(ChangeVersionUtility.DidChange(lastVersion / 2 + 10U, initial));
            Assert.IsFalse(ChangeVersionUtility.DidChange(lastVersion, initial));
            Assert.IsFalse(ChangeVersionUtility.DidChange(initial, initial));

            // Wrap around
            Assert.IsTrue(ChangeVersionUtility.DidChange(lastVersionPlus, lastVersion));
            Assert.IsTrue(ChangeVersionUtility.DidChange(lastVersionPlus, lastVersion - 1000));
            Assert.IsFalse(ChangeVersionUtility.DidChange(lastVersionPlus, 10));
            
            // first frame is always changed
            Assert.IsTrue(ChangeVersionUtility.DidChange(initial, firstSystemFrame));
            Assert.IsTrue(ChangeVersionUtility.DidChange(lastVersion, firstSystemFrame));
            Assert.IsTrue(ChangeVersionUtility.DidChange(lastVersion / 2, firstSystemFrame));
        }
        
        [Test]
        public void NewlyCreatedChunkGetsCurrentVersion()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            AssertHasVersion<EcsTestData>(e0, OldVersion);
            AssertHasVersion<EcsTestData2>(e0, OldVersion);
            BumpGlobalSystemVersion();
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData3));
            AssertHasVersion<EcsTestData3>(e1, NewVersion);
        }

        [Test]
        public void CreateEntityMarksDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
        }

        [Test]
        public void AddComponentMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.AddComponentData(e1, new EcsTestData3(7));
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
            AssertHasVersion<EcsTestData3>(e1, NewVersion);
        }

        [Test]
        public void AddTagMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.AddComponentData(e1, new EcsTestTag());
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
            AssertHasVersion<EcsTestTag>(e1, NewVersion);
        }
        
        [Test]
        public void AddTagWithQueryKeepsVersion()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData));
            
            BumpGlobalSystemVersion();
            m_Manager.AddComponent(m_Manager.UniversalQuery, typeof(EcsTestTag));
            
            AssertHasVersion<EcsTestData>(e0, OldVersion);
            AssertHasVersion<EcsTestTag>(e0, NewVersion);
        }

        [Test]
        public void AddSharedWithQueryKeepsVersion()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData));
            
            BumpGlobalSystemVersion();
            m_Manager.AddSharedComponentData(m_Manager.UniversalQuery, new SharedData1(5));
            
            AssertHasVersion<EcsTestData>(e0, OldVersion);
            AssertHasSharedVersion<SharedData1>(e0, NewVersion);
        }
        
        [Test]
        public void AddComponentWithDefaultValueMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.AddComponent(e1, typeof(EcsTestData3));
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
            AssertHasVersion<EcsTestData3>(e1, NewVersion);
        }
        
        [Test]
        public void AddComponentWithDefaultValueMarksSrcAndDestChunkAsChangedEntityArray()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            var entities = new NativeArray<Entity>(1, Allocator.TempJob);
            entities[0] = e1;
            m_Manager.AddComponent(entities, typeof(EcsTestData3));
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
            AssertHasVersion<EcsTestData3>(e1, NewVersion);
            entities.Dispose();
        }

        [Test]
        public void SetComponentDataMarksOnlySetTypeAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.SetComponentData(e1, new EcsTestData(1));
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, OldVersion);
        }

        [Test]
        public void ModifyingBufferComponentMarksOnlySetTypeAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsIntElement));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsIntElement));
            BumpGlobalSystemVersion();
            var buffer = m_Manager.GetBuffer<EcsIntElement>(e1);
            buffer.Add(7);
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestData>(e0, OldVersion);
            AssertHasBufferVersion<EcsIntElement>(e0, NewVersion);
        }

        [Test]
        public void AddSharedComponentMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.AddSharedComponentData(e1, new EcsTestSharedComp(7));
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e1, NewVersion);
        }


        [Test]
        public void SetSharedComponentMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            BumpGlobalSystemVersion();
            m_Manager.SetSharedComponentData(e1, new EcsTestSharedComp(7));
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e1, NewVersion);
        }

        [Test]
        public void SwapComponentsMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));

            m_Manager.SetSharedComponentData(e0, new EcsTestSharedComp(1));
            m_Manager.SetSharedComponentData(e1, new EcsTestSharedComp(2));

            var chunk0 = m_Manager.GetChunk(e0);
            var chunk1 = m_Manager.GetChunk(e1);

            Assert.AreNotEqual(chunk0, chunk1);
            BumpGlobalSystemVersion();

            m_Manager.SwapComponents(chunk0, 0, chunk1, 0);
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e1, NewVersion);
        }

        [Test]
        public void AddChunkComponentMarksSrcAndDestChunkAsChanged()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            BumpGlobalSystemVersion();
            m_Manager.AddChunkComponentData<EcsTestData3>(e1);
            AssertHasVersion<EcsTestData>(e0, NewVersion);
            AssertHasVersion<EcsTestData2>(e0, NewVersion);
            AssertHasVersion<EcsTestData>(e1, NewVersion);
            AssertHasVersion<EcsTestData2>(e1, NewVersion);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void NewlyCreatedChunkGetsCurrentVersion_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            AssertHasVersion<EcsTestManagedComponent>(e0, OldVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e0, OldVersion);
            BumpGlobalSystemVersion();
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent3));
            AssertHasVersion<EcsTestManagedComponent3>(e1, NewVersion);
        }

        [Test]
        public void CreateEntityMarksDestChunkAsChanged_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            BumpGlobalSystemVersion();
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestManagedComponent>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent>(e1, NewVersion);
        }

        [Test]
        public void AddComponentMarksSrcAndDestChunkAsChanged_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            BumpGlobalSystemVersion();
            m_Manager.AddComponentData(e1, new EcsTestManagedComponent3() { value = "SomeString" });
            AssertHasVersion<EcsTestManagedComponent>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent>(e1, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e1, NewVersion);
            AssertHasVersion<EcsTestManagedComponent3>(e1, NewVersion);
        }

        [Test]
        public void AddSharedComponentMarksSrcAndDestChunkAsChanged_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            BumpGlobalSystemVersion();
            m_Manager.AddSharedComponentData(e1, new EcsTestSharedComp(7));
            AssertHasVersion<EcsTestManagedComponent>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent>(e1, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e1, NewVersion);
            AssertHasSharedVersion<EcsTestSharedComp>(e1, NewVersion);
        }

        [Test]
        public void SetComponentDataMarksOnlySetTypeAsChanged_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            BumpGlobalSystemVersion();
            m_Manager.SetComponentData(e1, new EcsTestManagedComponent {value = "SomeString"});
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestManagedComponent>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e0, OldVersion);
        }

        [Test]
        public void GetComponentDataMarksOnlySetTypeAsChanged_ManagedComponents()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2));
            m_Manager.SetComponentData(e0, new EcsTestManagedComponent{value = "e0"});
            m_Manager.SetComponentData(e1, new EcsTestManagedComponent{value = "e1"});
            m_Manager.SetComponentData(e0, new EcsTestManagedComponent2{value = "e0"});
            m_Manager.SetComponentData(e1, new EcsTestManagedComponent2{value = "e1"});
            BumpGlobalSystemVersion();
            m_Manager.GetComponentData<EcsTestManagedComponent>(e1).value = "SomeString";
            AssertSameChunk(e0, e1);
            AssertHasVersion<EcsTestManagedComponent>(e0, NewVersion);
            AssertHasVersion<EcsTestManagedComponent2>(e0, OldVersion);
        }

#endif
    }
}
