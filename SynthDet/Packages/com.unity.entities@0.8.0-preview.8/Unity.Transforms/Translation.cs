using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [Serializable]
    [WriteGroup(typeof(LocalToWorld))]
    [WriteGroup(typeof(LocalToParent))]
    public struct Translation : IComponentData
    {
        public float3 Value;
    }
}
