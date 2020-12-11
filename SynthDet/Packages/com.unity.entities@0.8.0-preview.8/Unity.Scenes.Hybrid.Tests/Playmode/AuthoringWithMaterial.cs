using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.Scenes.Hybrid.Tests
{
    public struct SharedWithMaterial : ISharedComponentData, IEquatable<SharedWithMaterial>
    {
        public Material material;

        public bool Equals(SharedWithMaterial other)
        {
            return Equals(material, other.material);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedWithMaterial other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (material != null ? material.GetHashCode() : 0);
        }
    }

    [DisallowMultipleComponent]
    [RequiresEntityConversion]
    public class AuthoringWithMaterial : MonoBehaviour, IConvertGameObjectToEntity
    {
        public Material material;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddSharedComponentData(entity, new SharedWithMaterial {material = material});
        }
    }
}
