using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Tests {
    class EntityQueryWithUnityComponentTests : ECSTestsFixture
    {
        [Test]
        public void ToComponentArrayContainsAllInstances()
        {
            var go1 = new GameObject();
            var go2 = new GameObject();

            try
            {
                {
                    var entity = m_Manager.CreateEntity();
                    m_Manager.AddComponentObject(entity, go1.transform);
                }
                {
                    var entity = m_Manager.CreateEntity();
                    m_Manager.AddComponentObject(entity, go2.transform);
                }

                var query = EmptySystem.GetEntityQuery(typeof(Transform));
                var arr = query.ToComponentArray<Transform>();
                Assert.AreEqual(2, arr.Length);
                Assert.That(arr.Any(t => ReferenceEquals(t, go1.transform)), "Output doesn't contain transform 1");
                Assert.That(arr.Any(t => ReferenceEquals(t, go2.transform)), "Output doesn't contain transform 2");
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);   
            }
        }
    }
}
