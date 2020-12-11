using Unity.Entities;

namespace Unity.Entities.CodeGen.Tests.TestTypes
{
    public struct BoidInAnotherAssembly : IComponentData
    {
    }

    public struct TranslationInAnotherAssembly : IComponentData
    {
        public float Value;
    }

    public struct VelocityInAnotherAssembly : IComponentData
    {
        public float Value;
    }

    public struct AccelerationInAnotherAssembly : IComponentData
    {
        public float Value;
    }

    public struct RotationInAnotherAssembly : IComponentData
    {
        public int Value;
    }
    
    public struct SharedDataInAnotherAssembly : ISharedComponentData
    {
        public int Value;
    }
    
    public struct TagComponentInAnotherAssembly : IComponentData
    {
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class SpeedInAnotherAssembly : IComponentData
    {
        public float Value;
    }
#endif
    
    [InternalBufferCapacity(8)]
    public struct TestBufferElementInAnotherAssembly : IBufferElementData
    {
        public static implicit operator float(TestBufferElementInAnotherAssembly e) { return e.Value; }
        public static implicit operator TestBufferElementInAnotherAssembly(float e) { return new TestBufferElementInAnotherAssembly { Value = e }; }
        public float Value;
    }
}