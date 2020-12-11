//#define WRITE_TO_DISK

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Entities.Serialization;
using Object = UnityEngine.Object;
using Unity.Collections;
#pragma warning disable 649

namespace Unity.Entities.Tests
{
    class SharedComponentSerializeTests : ECSTestsFixture
    {
        [Test]
        public void SharedComponentSerialize()
        {
            for (int i = 0; i != 20; i++)
            {
                var entity = m_Manager.CreateEntity();
                m_Manager.AddSharedComponentData(entity, new MockSharedData { Value = i });
                m_Manager.AddComponentData(entity, new EcsTestData(i));
                var buffer = m_Manager.AddBuffer<EcsIntElement>(entity);
                foreach (var val in Enumerable.Range(i, i + 5))
                    buffer.Add(new EcsIntElement { Value = val });
            }

            var writer = new TestBinaryWriter();

            ReferencedUnityObjects objRefs = null;
            SerializeUtilityHybrid.Serialize(m_Manager, writer, out objRefs);

            var reader = new TestBinaryReader(writer);

            var world = new World("temp");
            SerializeUtilityHybrid.Deserialize(world.EntityManager, reader, objRefs);

            var newWorldEntities = world.EntityManager;

            {
                var entities = newWorldEntities.GetAllEntities();

                Assert.AreEqual(20, entities.Length);

                for (int i = 0; i != 20; i++)
                {
                    Assert.AreEqual(i, newWorldEntities.GetComponentData<EcsTestData>(entities[i]).value);
                    Assert.AreEqual(i, newWorldEntities.GetSharedComponentData<MockSharedData>(entities[i]).Value);
                    var buffer = newWorldEntities.GetBuffer<EcsIntElement>(entities[i]);
                    Assert.That(
                        buffer.AsNativeArray().ToArray(),
                        Is.EqualTo(Enumerable.Range(i, i + 5).Select(x => new EcsIntElement { Value = x }))
                    );
                }
                for (int i = 0; i != 20; i++)
                    newWorldEntities.DestroyEntity(entities[i]);

                entities.Dispose();
            }

            Assert.IsTrue(newWorldEntities.Debug.IsSharedComponentManagerEmpty());

            world.Dispose();
            reader.Dispose();
        }


        public struct SharedComponentWithUnityObject : ISharedComponentData, IEquatable<SharedComponentWithUnityObject>
        {
            public Object obj;

            public bool Equals(SharedComponentWithUnityObject other)
            {
                return Equals(obj, other.obj);
            }

            public override bool Equals(object obj)
            {
                return obj is SharedComponentWithUnityObject other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (obj != null ? obj.GetHashCode() : 0);
            }
        }

        [Test]
        // https://fogbugz.unity3d.com/f/cases/1204153/
        public void SharedComponentUnityObjectSerialize_Case_1204153()
        {
            var go1 = new GameObject();
            var go2 = new GameObject();
            var shared = new SharedComponentWithUnityObject { obj = go1 };
            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentData(entity, shared);
            var writer = new TestBinaryWriter();

            ReferencedUnityObjects objRefs = null;
            SerializeUtilityHybrid.Serialize(m_Manager, writer, out objRefs);
            var reader = new TestBinaryReader(writer);

            // Swap the obj ref to a different instance but same value.  This can occur with any Unity.Object
            // during subscene serialize/deserialize since the editor's Unity.Object.GetHashCode() returns
            // an instance ID instead of a hash of the values in it.  There is no reasonable expectation that
            // the instance IDs would match between a serialize and deserialize.
            objRefs.Array[0] = go2;

            var world = new World("temp");
            SerializeUtilityHybrid.Deserialize(world.EntityManager, reader, objRefs);

            var newWorldEntities = world.EntityManager;
            var uniqueShared = new List<SharedComponentWithUnityObject>();
            var query = newWorldEntities.CreateEntityQuery(ComponentType.ReadWrite<SharedComponentWithUnityObject>());
            newWorldEntities.GetAllUniqueSharedComponentData(uniqueShared);
            Assert.AreEqual(2, uniqueShared.Count);
            query.SetSharedComponentFilter(uniqueShared[1]);
            Assert.AreEqual(1, query.CalculateEntityCount());

            query.Dispose();
            world.Dispose();
            reader.Dispose();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public class ManagedComponentWithObjectReference : IComponentData
        {
            public UnityEngine.Texture2D Texture;
        }

        [Test]
        public void ManagedComponentWithObjectReferenceSerialize()
        {
            for (int i = 0; i != 20; i++)
            {
                var e1 = m_Manager.CreateEntity();

                UnityEngine.Texture2D tex = new UnityEngine.Texture2D(i + 1, i + 1);
                var expectedManagedComponent = new ManagedComponentWithObjectReference { Texture = tex };

                m_Manager.AddComponentData(e1, expectedManagedComponent);
            }

            var writer = new TestBinaryWriter();
            ReferencedUnityObjects objRefs = null;
            SerializeUtilityHybrid.Serialize(m_Manager, writer, out objRefs);

            var world = new World("temp");
            var reader = new TestBinaryReader(writer);
            SerializeUtilityHybrid.Deserialize(world.EntityManager, reader, objRefs);

            var newWorldEntities = world.EntityManager;
            {
                var entities = newWorldEntities.GetAllEntities();
                Assert.AreEqual(20, entities.Length);

                var seenWidths = new NativeArray<bool>(entities.Length, Allocator.Temp);
                var seenHeights = new NativeArray<bool>(entities.Length, Allocator.Temp);
                for (int i = 0; i < entities.Length; ++i)
                {
                    var e = entities[i];

                    var actualManagedComponent = newWorldEntities.GetComponentData<ManagedComponentWithObjectReference>(e);
                    Assert.NotNull(actualManagedComponent);
                    var tex = actualManagedComponent.Texture;
                    seenWidths[tex.width - 1] = true;
                    seenHeights[tex.height - 1] = true;
                }

                for (int i = 0; i < entities.Length; ++i)
                {
                    Assert.IsTrue(seenWidths[i]);
                    Assert.IsTrue(seenHeights[i]);
                }

                seenWidths.Dispose();
                seenHeights.Dispose();

                for (int i = 0; i != 20; i++)
                    newWorldEntities.DestroyEntity(entities[i]);

                entities.Dispose();
            }

            Assert.IsTrue(newWorldEntities.Debug.IsSharedComponentManagerEmpty());

            world.Dispose();
            reader.Dispose();
        } 
#endif
    }
}
