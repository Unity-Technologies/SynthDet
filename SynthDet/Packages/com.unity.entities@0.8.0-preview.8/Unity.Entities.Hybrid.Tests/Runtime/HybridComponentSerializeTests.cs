#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Serialization;
using Unity.Entities.Tests.Conversion;

namespace Unity.Entities.Tests
{
    class HybridComponentSerializeTests : ConversionTestFixtureBase
    {
        [Test]
        public void HybridComponentSerialize()
        {
            var root = CreateGameObject();
            var values = new[] { 123, 234, 345 };

            foreach(var value in values)
            {
                var gameObject = CreateGameObject().ParentTo(root);
                gameObject.AddComponent<EcsTestMonoBehaviourComponent>().SomeValue = value;
            }

            GameObjectConversionUtility.ConvertGameObjectHierarchy(root, MakeDefaultSettings().WithExtraSystem<ConversionHybridTests.MonoBehaviourComponentConversionSystem>());

            ReferencedUnityObjects objRefs = null;
            var writer = new TestBinaryWriter();
            SerializeUtilityHybrid.Serialize(m_Manager, writer, out objRefs);

            var world = new World("temp");

            var reader = new TestBinaryReader(writer);
            SerializeUtilityHybrid.Deserialize(world.EntityManager, reader, objRefs);

            var query = world.EntityManager.CreateEntityQuery(typeof(EcsTestMonoBehaviourComponent));
            var components = query.ToComponentArray<EcsTestMonoBehaviourComponent>();

            CollectionAssert.AreEquivalent(components.Select(c => c.SomeValue), values);

            query.Dispose();
            world.Dispose();
            reader.Dispose();
        }
    }
}
#endif
