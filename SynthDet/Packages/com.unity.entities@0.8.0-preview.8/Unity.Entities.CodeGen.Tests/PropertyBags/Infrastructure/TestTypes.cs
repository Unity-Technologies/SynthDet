using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Entities.CodeGen.Tests.PropertyBags.Types
{
#pragma warning disable 0649
    public struct ValueTypeIComponentData : IComponentData
    {
        public int IntField;
    }

    public struct ValueTypeSharedComponentData : ISharedComponentData, IEquatable<ValueTypeSharedComponentData>
    {
        public int IntField;

        public bool Equals(ValueTypeSharedComponentData other)
        {
            return IntField == other.IntField;
        }

        public override int GetHashCode()
        {
            return IntField;
        }
    }

    public struct TypeWithClosedGenericField
    {
        public NativeArray<int> NativeArrayField;
    }

    public unsafe struct TypeWithPointerField
    {
        public int* IntPointer;
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class ManagedIComponentData : IComponentData
    {
        public string StringField;
    }
#endif
}

namespace Some.Namespace
{
    public class SomeClass
    {
        public int Field0;
        public int Field1;
    }
    
    struct TypeWithListAndArray
    {
        public int MyInt;
        public List<string> MyList;
        public SomeClass[] MyArray;
    }
}
#pragma warning restore 0649