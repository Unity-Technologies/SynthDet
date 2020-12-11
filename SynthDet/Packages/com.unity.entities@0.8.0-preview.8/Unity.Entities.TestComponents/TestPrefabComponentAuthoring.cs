using System;
using UnityEngine;

namespace Unity.Entities.Tests {

    [ConverterVersion("sschoener", 1)]
    public class TestPrefabComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public int IntValue;
        public Material Material;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new TestPrefabComponent
            {
                IntValue = IntValue
            });
#if !NET_DOTS && !UNITY_DISABLE_MANAGED_COMPONENTS
            dstManager.AddComponentObject(entity, new TestManagedComponent
            {
                Material = Material
            });
#endif
        }
    }

#if !NET_DOTS && !UNITY_DISABLE_MANAGED_COMPONENTS
    public class TestManagedComponent : IComponentData
    {
        public Material Material;
    }
#endif
    
    [GenerateAuthoringComponent]
    public struct TestPrefabComponent : IComponentData
    {
        public int IntValue;
    }
}
