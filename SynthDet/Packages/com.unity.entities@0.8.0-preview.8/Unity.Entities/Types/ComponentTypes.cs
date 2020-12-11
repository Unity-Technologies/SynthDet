using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Entities
{
    public unsafe struct ComponentTypes
    {
        FixedListInt64 m_sorted;

        public struct Masks
        {
            public UInt16 m_BufferMask;
            public UInt16 m_SystemStateComponentMask;
            public UInt16 m_SharedComponentMask;
            public UInt16 m_ZeroSizedMask;

            public bool IsSharedComponent(int index)
            {
                return (m_SharedComponentMask & (1 << index)) != 0;
            }

            public bool IsZeroSized(int index)
            {
                return (m_ZeroSizedMask & (1 << index)) != 0;
            }

            public int Buffers => math.countbits((UInt32) m_BufferMask);
            public int SystemStateComponents => math.countbits((UInt32) m_SystemStateComponentMask);
            public int SharedComponents => math.countbits((UInt32) m_SharedComponentMask);
            public int ZeroSizeds => math.countbits((UInt32) m_ZeroSizedMask);
        }

        public Masks m_masks;

        private void ComputeMasks()
        {
            for (var i = 0; i < m_sorted.Length; ++i)
            {
                var typeIndex = m_sorted[i];
                var mask = (UInt16) (1 << i);
                if (TypeManager.IsBuffer(typeIndex))
                    m_masks.m_BufferMask |= mask;
                if (TypeManager.IsSystemStateComponent(typeIndex))
                    m_masks.m_SystemStateComponentMask |= mask;
                if (TypeManager.IsSharedComponent(typeIndex))
                    m_masks.m_SharedComponentMask |= mask;
                if (TypeManager.IsZeroSized(typeIndex))
                    m_masks.m_ZeroSizedMask |= mask;
            }
        }

        public int Length
        {
            get => m_sorted.Length;
        }

        public int GetTypeIndex(int index)
        {
            return m_sorted[index];
        }

        public ComponentType GetComponentType(int index)
        {
            return TypeManager.GetType(m_sorted[index]);
        }

        public ComponentTypes(ComponentType a)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            m_sorted.Add(a.TypeIndex);
            ComputeMasks();
        }

        public ComponentTypes(ComponentType a, ComponentType b)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            m_sorted.Add(a.TypeIndex);
            m_sorted.Add(b.TypeIndex);
            m_sorted.Sort();
            ComputeMasks();
        }

        public ComponentTypes(ComponentType a, ComponentType b, ComponentType c)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            m_sorted.Add(a.TypeIndex);
            m_sorted.Add(b.TypeIndex);
            m_sorted.Add(c.TypeIndex);
            m_sorted.Sort();
            ComputeMasks();
        }

        public ComponentTypes(ComponentType a, ComponentType b, ComponentType c, ComponentType d)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            m_sorted.Add(a.TypeIndex);
            m_sorted.Add(b.TypeIndex);
            m_sorted.Add(c.TypeIndex);
            m_sorted.Add(d.TypeIndex);
            m_sorted.Sort();
            ComputeMasks();
        }

        public ComponentTypes(ComponentType a, ComponentType b, ComponentType c, ComponentType d, ComponentType e)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            m_sorted.Add(a.TypeIndex);
            m_sorted.Add(b.TypeIndex);
            m_sorted.Add(c.TypeIndex);
            m_sorted.Add(d.TypeIndex);
            m_sorted.Add(e.TypeIndex);
            m_sorted.Sort();
            ComputeMasks();
        }

        public ComponentTypes(ComponentType[] componentType)
        {
            m_sorted = new FixedListInt64();
            m_masks = new Masks();
            for (var i = 0; i < componentType.Length; ++i)
                m_sorted.Add(componentType[i].TypeIndex);
            m_sorted.Sort();
            ComputeMasks();
        }
    }
}
