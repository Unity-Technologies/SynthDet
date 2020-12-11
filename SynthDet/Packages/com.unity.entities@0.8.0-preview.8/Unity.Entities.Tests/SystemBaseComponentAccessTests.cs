
using System;
#if !UNITY_DOTSPLAYER
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
#pragma warning disable 649

namespace Unity.Entities.Tests
{
    class SystemBaseComponentAccessTests : ECSTestsFixture
    {
        SystemBase_TestSystem TestSystem;
        static Entity TestEntity1;
        static Entity TestEntity2;
        
        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystem<SystemBase_TestSystem>();
            
            var myArch = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestDataEntity>(),
                ComponentType.ReadWrite<EcsTestData>());
            
            TestEntity1 = m_Manager.CreateEntity(myArch);
            TestEntity2 = m_Manager.CreateEntity(myArch);
            m_Manager.SetComponentData(TestEntity1, new EcsTestDataEntity() { value0 = 1, value1 = TestEntity2 });
            m_Manager.SetComponentData(TestEntity1, new EcsTestData() { value = 1 });
            m_Manager.SetComponentData(TestEntity2, new EcsTestDataEntity() { value0 = 2, value1 = TestEntity1 });
            m_Manager.SetComponentData(TestEntity2, new EcsTestData() { value = 2 });
        }
        
        public class SystemBase_TestSystem : SystemBase
        {
            protected override void OnUpdate() {}
            
            public void HasComponentInRun_HasComponent(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).Run();
                Dependency.Complete();
            }
            
            public void HasComponentInSchedule_HasComponent(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).Schedule();
                Dependency.Complete();
            }
            
            public void HasComponentInScheduleParallel_HasComponent(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = HasComponent<EcsTestDataEntity>(entity) ? 333 : 0; }).ScheduleParallel();
                Dependency.Complete();
            }
            
            public void GetComponentInRun_GetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = GetComponent<EcsTestDataEntity>(entity).value0; }).Run();
                Dependency.Complete();
            }
            
            public void GetComponentInSchedule_GetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = GetComponent<EcsTestDataEntity>(entity).value0; }).Schedule();
                Dependency.Complete();
            }
            
            public void GetComponentInScheduleParallel_GetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestData td) => { td.value = GetComponent<EcsTestDataEntity>(entity).value0; }).ScheduleParallel();
                Dependency.Complete();
            }

            public void SetComponentInRun_SetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { SetComponent<EcsTestData>(entity, new EcsTestData(){ value = 2 }); }).Run();
                Dependency.Complete();
            }
            
            public void SetComponentInSchedule_SetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { SetComponent<EcsTestData>(entity, new EcsTestData(){ value = 2 }); }).Schedule();
                Dependency.Complete();
            }
            
            public void SetComponentInScheduleParallel_SetsValue(Entity entity)
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { SetComponent<EcsTestData>(entity, new EcsTestData(){ value = 2 }); }).ScheduleParallel();
                Dependency.Complete();
            }

            public void GetComponentFromComponentDataField_GetsValue()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { tde.value0 = GetComponent<EcsTestData>(tde.value1).value; }).Schedule();
                Dependency.Complete();
            }
            
            public void GetComponentFromStaticField_GetsValue()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) => { tde.value0 = GetComponent<EcsTestData>(tde.value1).value; }).Schedule();
                Dependency.Complete();
            }
            
            public void MultipleGetComponents_GetsValues()
            {
                Entities.ForEach((ref EcsTestDataEntity tde) =>
                {
                    tde.value0 = GetComponent<EcsTestData>(tde.value1).value + GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Dependency.Complete();
            }
            
            public void GetComponentSetComponent_SetsValue()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((Entity entity, in EcsTestDataEntity tde) =>
                    {
                        var val = GetComponent<EcsTestData>(tde.value1).value;
                        SetComponent(entity, new EcsTestData(val));
                    }).Schedule();
                Dependency.Complete();
            }
            
            public void GetComponentSetComponentThroughLocalFunction_GetsSetsValue()
            {
                Entities.ForEach((Entity entity, in EcsTestDataEntity tde) =>
                {
                    int GetEntityValue(Entity e)
                    {
                        return GetComponent<EcsTestData>(e).value;
                    }
                    
                    void SetEntityValue(Entity e, int value)
                    {
                        SetComponent(e, new EcsTestData(value));
                    }
                    
                    SetEntityValue(entity, GetEntityValue(tde.value1));
                }).Schedule();
                Dependency.Complete();
            }
            
            public void GetSameComponentInTwoEntitiesForEach_GetsValue()
            {
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += GetComponent<EcsTestData>(tde.value1).value;
                    tde.value0 += GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Entities.ForEach((Entity entity, ref EcsTestDataEntity tde) =>
                {
                    tde.value0 += GetComponent<EcsTestData>(tde.value1).value;
                    tde.value0 += GetComponent<EcsTestData>(tde.value1).value;
                }).Schedule();
                Dependency.Complete();
            }
            
            public void GetComponentOnOtherSystemInVar_GetsValue(Entity entity)
            {
                var otherSystem = new SystemBase_TestSystem();
                Entities.ForEach((ref EcsTestData td) => { td.value = otherSystem.GetComponent<EcsTestDataEntity>(entity).value0; }).Schedule();
                Dependency.Complete();
            }
            
            SystemBase_TestSystem otherSystemField;
            public void GetComponentOnOtherSystemInField_GetsValue(Entity entity)
            {
                var systemField = otherSystemField;
                Entities.ForEach((ref EcsTestData td) => { td.value = systemField.GetComponent<EcsTestDataEntity>(entity).value0; }).Schedule();
                Dependency.Complete();
            }
        }

        [Test]
        public void HasComponentInRun_HasComponent()
        {
            TestSystem.HasComponentInRun_HasComponent(TestEntity2);
            Assert.AreEqual(333, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void HasComponentInSchedule_HasComponent()
        {
            TestSystem.HasComponentInSchedule_HasComponent(TestEntity2);
            Assert.AreEqual(333, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void HasComponentInScheduleParallel_HasComponent()
        {
            TestSystem.HasComponentInScheduleParallel_HasComponent(TestEntity2);
            Assert.AreEqual(333, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void GetComponentInRun_GetsValue()
        {
            TestSystem.GetComponentInRun_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void GetComponentInSchedule_GetsValue()
        {
            TestSystem.GetComponentInSchedule_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void GetComponentInScheduleParallel_GetsValue()
        {
            TestSystem.GetComponentInScheduleParallel_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }

        [Test]
        public void SetComponentInRun_SetsValue()
        {
            TestSystem.SetComponentInRun_SetsValue(TestEntity1);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void SetComponentInSchedule_SetsValue()
        {
            TestSystem.SetComponentInSchedule_SetsValue(TestEntity1);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void SetComponentInScheduleParallel_ThrowsSafetyException()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                TestSystem.SetComponentInScheduleParallel_SetsValue(TestEntity1);
            });
        }
        
        [Test]
        public void GetComponentFromComponentDataField_GetsValue()
        {
            TestSystem.GetComponentFromComponentDataField_GetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }
        
        [Test]
        public void GetComponentFromStaticField_GetsValue()
        {
            TestSystem.GetComponentFromStaticField_GetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }
        
        
        [Test]
        public void MultipleGetComponents_GetsValues()
        {
            TestSystem.MultipleGetComponents_GetsValues();
            Assert.AreEqual(4, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }
        
        [Test]
        public void GetComponentSetComponent_SetsValue()
        {
            TestSystem.GetComponentSetComponent_SetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void GetComponentSetComponentThroughLocalFunction_GetsSetsValue()
        {
            TestSystem.GetComponentSetComponentThroughLocalFunction_GetsSetsValue();
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void GetSameComponentInTwoEntitiesForEach_GetsValue()
        {
            TestSystem.GetSameComponentInTwoEntitiesForEach_GetsValue();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestDataEntity>(TestEntity1).value0);
        }
        
        [Test]
        public void GetComponentOnOtherSystemInVar_GetsValue()
        {
            TestSystem.GetComponentOnOtherSystemInVar_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
        
        [Test]
        public void GetComponentOnOtherSystemInField_GetsValue()
        {
            TestSystem.GetComponentOnOtherSystemInField_GetsValue(TestEntity2);
            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData>(TestEntity1).value);
        }
    }
}
#endif
