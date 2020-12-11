using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Assertions;
using Unity.Burst;
using Unity.Entities.Serialization;
#if !NET_DOTS
using Unity.Properties;
#endif

namespace Unity.Entities
{
    public interface IRefCounted
    {
        void Retain();
        void Release();
    }

    internal unsafe class ManagedComponentStore
    {
        UnsafeMultiHashMap<int, int> m_HashLookup = new UnsafeMultiHashMap<int, int>(128, Allocator.Persistent);

        List<object> m_SharedComponentData = new List<object>();

        struct SharedComponentInfo
        {
            public int RefCount;
            public int ComponentType;
            public int Version;
            public int HashCode;
        }

        UnsafeList m_SharedComponentInfo = new UnsafeList(Allocator.Persistent);

        internal object[] m_ManagedComponentData = new object[64];

        public void SetManagedComponentCapacity(int newCapacity)
        {
            Assert.IsTrue(m_ManagedComponentData.Length < newCapacity);
            Array.Resize(ref m_ManagedComponentData, newCapacity);
        }

        public object GetManagedComponent(int index)
        {
            return m_ManagedComponentData[index];
        }

        private SharedComponentInfo* SharedComponentInfoPtr
        {
            get { return (SharedComponentInfo*) m_SharedComponentInfo.Ptr; }
        }

        int m_FreeListIndex;

        internal delegate void InstantiateHybridComponentDelegate(int* srcArray, int componentCount, Entity* dstEntities, int* dstComponentLinkIndices, int* dstArray, int instanceCount, ManagedComponentStore managedComponentStore);
        internal static InstantiateHybridComponentDelegate InstantiateHybridComponent;

        internal delegate void AssignHybridComponentsToCompanionGameObjectsDelegate(EntityManager entityManager, NativeArray<Entity> entities);
        internal static AssignHybridComponentsToCompanionGameObjectsDelegate AssignHybridComponentsToCompanionGameObjects;

        private sealed class ManagedComponentStoreKeyContext
        {
        }

        private sealed class CompanionLinkTypeIndexStatic
        {
            public static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<ManagedComponentStoreKeyContext, CompanionLinkTypeIndexStatic>();
        }

        public static int CompanionLinkTypeIndex
        {
            get => CompanionLinkTypeIndexStatic.Ref.Data;
            set => CompanionLinkTypeIndexStatic.Ref.Data = value;
        }

        public ManagedComponentStore()
        {
            ResetSharedComponentData();
        }

        public void Dispose()
        {
            for (var i = 1; i != m_SharedComponentData.Count; i++)
                (m_SharedComponentData[i] as IRefCounted)?.Release();

            for (var i = 0; i != m_ManagedComponentData.Length; i++)
                DisposeManagedObject(m_ManagedComponentData[i]);

            m_SharedComponentInfo.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
            m_HashLookup.Dispose();
        }

        void ResetSharedComponentData()
        {
            m_HashLookup.Clear();
            m_SharedComponentData.Clear();
            m_SharedComponentInfo.Clear();

            m_SharedComponentData.Add(null);
            m_SharedComponentInfo.Add(new SharedComponentInfo { RefCount = 1, ComponentType = -1, Version = 1, HashCode = 0});
            m_FreeListIndex = -1;
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues)
            where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            for (var i = 1; i != m_SharedComponentData.Count; i++)
            {
                var data = m_SharedComponentData[i];
                if (data != null && data.GetType() == typeof(T))
                    sharedComponentValues.Add((T)m_SharedComponentData[i]);
            }
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices)
            where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            sharedComponentIndices.Add(0);
            for (var i = 1; i != m_SharedComponentData.Count; i++)
            {
                var data = m_SharedComponentData[i];
                if (data != null && data.GetType() == typeof(T))
                {
                    sharedComponentValues.Add((T)m_SharedComponentData[i]);
                    sharedComponentIndices.Add(i);
                }
            }
        }

        public int GetSharedComponentCount()
        {
            return m_SharedComponentData.Count;
        }

        public int InsertSharedComponent<T>(T newData) where T : struct
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var index = FindSharedComponentIndex(TypeManager.GetTypeIndex<T>(), newData);

            if (index == 0) return 0;

            if (index != -1)
            {
                SharedComponentInfoPtr[index].RefCount++;
                return index;
            }

            var hashcode = TypeManager.GetHashCode<T>(ref newData);

            object newDataObj = newData;

            (newDataObj as IRefCounted)?.Retain();
            return Add(typeIndex, hashcode, newDataObj);
        }

        private int FindSharedComponentIndex<T>(int typeIndex, T newData) where T : struct
        {
            var defaultVal = default(T);
            if (TypeManager.Equals(ref defaultVal, ref newData))
                return 0;

            return FindNonDefaultSharedComponentIndex(typeIndex, TypeManager.GetHashCode(ref newData),
                UnsafeUtility.AddressOf(ref newData));
        }

        private int FindNonDefaultSharedComponentIndex(int typeIndex, int hashCode, void* newData)
        {
            int itemIndex;
            NativeMultiHashMapIterator<int> iter;

            if (!m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
                return -1;

            var infos = SharedComponentInfoPtr;
            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null && infos[itemIndex].ComponentType == typeIndex)
                {
                    if (TypeManager.Equals(data, newData, typeIndex))
                        return itemIndex;
                }
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }

        private int FindNonDefaultSharedComponentIndex(int typeIndex, int hashCode, object newData)
        {
            int itemIndex;
            NativeMultiHashMapIterator<int> iter;

            if (!m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
                return -1;

            var infos = SharedComponentInfoPtr;
            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null && infos[itemIndex].ComponentType == typeIndex)
                {
                    if (TypeManager.Equals(data, newData, typeIndex))
                        return itemIndex;
                }
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }

        internal int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData)
        {
            var index = FindNonDefaultSharedComponentIndex(typeIndex, hashCode, newData);

            if (-1 == index)
            {
                (newData as IRefCounted)?.Retain();
                index = Add(typeIndex, hashCode, newData);
            }
            else
            {
                SharedComponentInfoPtr[index].RefCount++;
            }

            return index;
        }

        internal int InsertSharedComponentAssumeNonDefaultMove(int typeIndex, int hashCode, object newData)
        {
            var index = FindNonDefaultSharedComponentIndex(typeIndex, hashCode, newData);

            if (-1 == index)
                index = Add(typeIndex, hashCode, newData);
            else
                SharedComponentInfoPtr[index].RefCount++;

            return index;
        }

        private int Add(int typeIndex, int hashCode, object newData)
        {
            var info = new SharedComponentInfo
            {
                RefCount = 1,
                Version = 1,
                ComponentType = typeIndex,
                HashCode = hashCode
            };

            if (m_FreeListIndex != -1)
            {
                var infos = SharedComponentInfoPtr;

                int index = m_FreeListIndex;
                m_FreeListIndex = infos[index].Version;

                Assert.IsTrue(m_SharedComponentData[index] == null);

                m_HashLookup.Add(hashCode, index);
                m_SharedComponentData[index] = newData;
                infos[index] = info;
                return index;
            }
            else
            {
                int index = m_SharedComponentData.Count;
                m_HashLookup.Add(hashCode, index);
                m_SharedComponentData.Add(newData);
                m_SharedComponentInfo.Add(info);
                return index;
            }
        }

        public void IncrementSharedComponentVersion(int index)
        {
            SharedComponentInfoPtr[index].Version++;
        }

        public int GetSharedComponentVersion<T>(T sharedData) where T : struct
        {
            var index = FindSharedComponentIndex(TypeManager.GetTypeIndex<T>(), sharedData);
            return index == -1 ? 0 : SharedComponentInfoPtr[index].Version;
        }

        public T GetSharedComponentData<T>(int index) where T : struct
        {
            if (index == 0)
                return default(T);

            return (T)m_SharedComponentData[index];
        }

        public object GetSharedComponentDataBoxed(int index, int typeIndex)
        {
#if !NET_DOTS
            if (index == 0)
                return Activator.CreateInstance(TypeManager.GetType(typeIndex));
#else
            if (index == 0)
                throw new InvalidOperationException("Implement TypeManager.GetType(typeIndex).DefaultValue");
#endif
            return m_SharedComponentData[index];
        }

        public object GetSharedComponentDataNonDefaultBoxed(int index)
        {
            Assert.AreNotEqual(0, index);
            return m_SharedComponentData[index];
        }

        public void AddReference(int index, int numRefs = 1)
        {
            if (index == 0)
                return;
            Assert.IsTrue(numRefs >= 0);
            SharedComponentInfoPtr[index].RefCount += numRefs;
        }

        public void RemoveReference(int index, int numRefs = 1)
        {
            if (index == 0)
                return;

            var infos = SharedComponentInfoPtr;

            var newCount = infos[index].RefCount -= numRefs;
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;

            var hashCode = infos[index].HashCode;

            object sharedComponent = m_SharedComponentData[index];
            Assert.IsFalse(ReferenceEquals(sharedComponent, null));
            (sharedComponent as IRefCounted)?.Release();

            m_SharedComponentData[index] = null;
            infos[index].ComponentType = -1;
            infos[index].Version = m_FreeListIndex;
            m_FreeListIndex = index;

            int itemIndex;
            NativeMultiHashMapIterator<int> iter;
            if (m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
            {
                do
                {
                    if (itemIndex == index)
                    {
                        m_HashLookup.Remove(iter);
                        return;
                    }
                }
                while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));
            }

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            throw new System.InvalidOperationException("shared component couldn't be removed due to internal state corruption");
            #endif
        }

        public void CheckInternalConsistency()
        {
            var infos = SharedComponentInfoPtr;

            int refcount = 0;
            for (int i = 0; i < m_SharedComponentData.Count; ++i)
            {
                if (m_SharedComponentData[i] != null)
                {
                    refcount++;

                    var hashCode = infos[i].HashCode;

                    bool found = false;
                    int itemIndex;
                    NativeMultiHashMapIterator<int> iter;
                    if (m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
                    {
                        do
                        {
                            if (itemIndex == i)
                                found = true;
                        }
                        while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));
                    }

                    Assert.IsTrue(found);
                }
            }

            Assert.AreEqual(refcount, m_HashLookup.Count());
        }

        public bool IsEmpty()
        {
            var infos = SharedComponentInfoPtr;

            for (int i = 1; i < m_SharedComponentData.Count; ++i)
            {
                if (m_SharedComponentData[i] != null)
                    return false;

                if (infos[i].ComponentType != -1)
                    return false;

                if (infos[i].RefCount != 0)
                    return false;
            }

            if (m_SharedComponentData[0] != null)
                return false;

            if (m_HashLookup.Count() != 0)
                return false;

            return true;
        }

        public void CopySharedComponents(ManagedComponentStore srcManagedComponents, int* sharedComponentIndices, int sharedComponentIndicesCount)
        {
            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;

            for (var i = 0; i != sharedComponentIndicesCount; i++)
            {
                var srcIndex = sharedComponentIndices[i];
                if (srcIndex == 0)
                    continue;

                var srcData = srcManagedComponents.m_SharedComponentData[srcIndex];
                var typeIndex = srcInfos[srcIndex].ComponentType;
                var hashCode = srcInfos[srcIndex].HashCode;
                var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, srcData);

                sharedComponentIndices[i] = dstIndex;
            }
        }

        public bool AllSharedComponentReferencesAreFromChunks(EntityComponentStore* entityComponentStore)
        {
            using (var refCounts = new NativeArray<int>(m_SharedComponentInfo.Length, Allocator.Temp))
            {
                var refCountPtrs = (int*)refCounts.GetUnsafePtr();
                for (var i = 0; i < entityComponentStore->m_Archetypes.Length; ++i)
                {
                    var archetype = entityComponentStore->m_Archetypes.Ptr[i];
                    var chunkCount = archetype->Chunks.Count;
                    for (int j = 0; j < archetype->NumSharedComponents; ++j)
                    {
                        var values = archetype->Chunks.GetSharedComponentValueArrayForType(j);
                        for (var ci = 0; ci < chunkCount; ++ci)
                            refCountPtrs[values[ci]] += 1;
                    }
                }

                var infos = SharedComponentInfoPtr;
                for (int i = 1; i < refCounts.Length; i++)
                {
                    if (refCountPtrs[i] != infos[i].RefCount)
                        return false;
                }

                return true;
            }
        }

        public NativeArray<int> MoveAllSharedComponents(ManagedComponentStore srcManagedComponents, Allocator allocator)
        {
            var remap = new NativeArray<int>(srcManagedComponents.GetSharedComponentCount(), allocator);
            remap[0] = 0;

            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;
            for (int srcIndex = 1; srcIndex < remap.Length; ++srcIndex)
            {
                var srcData = srcManagedComponents.m_SharedComponentData[srcIndex];
                if (srcData == null) continue;

                var typeIndex = srcInfos[srcIndex].ComponentType;
                var hashCode = srcInfos[srcIndex].HashCode;
                var dstIndex = InsertSharedComponentAssumeNonDefaultMove(typeIndex, hashCode, srcData);

                SharedComponentInfoPtr[dstIndex].RefCount += srcInfos[srcIndex].RefCount - 1;
                SharedComponentInfoPtr[dstIndex].Version++;

                remap[srcIndex] = dstIndex;
            }

            srcManagedComponents.ResetSharedComponentData();

            return remap;
        }

        public NativeArray<int> MoveSharedComponents(ManagedComponentStore srcManagedComponents, NativeArray<ArchetypeChunk> chunks,  Allocator allocator)
        {
            var remap = new NativeArray<int>(srcManagedComponents.GetSharedComponentCount(), allocator);
            var remapPtr = (int*)remap.GetUnsafePtr();
            // Build a map of all shared component values that will be moved
            // remap will have a refcount of how many chunks reference the shared component after this loop
            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i].m_Chunk;
                var archetype = chunk->Archetype;
                var sharedComponentValues = chunk->SharedComponentValues;
                for (int sharedComponentIndex = 0; sharedComponentIndex < archetype->NumSharedComponents; ++sharedComponentIndex)
                    remapPtr[sharedComponentValues[sharedComponentIndex]]++;
            }

            remap[0] = 0;

            // Move all shared components that are being referenced
            // remap will have a remap table of src SharedComponentDataIndex -> dst SharedComponentDataIndex
            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;
            for (int srcIndex = 1; srcIndex < remap.Length; ++srcIndex)
            {
                if (remapPtr[srcIndex] == 0)
                    continue;

                var srcData = srcManagedComponents.m_SharedComponentData[srcIndex];
                var typeIndex = srcInfos[srcIndex].ComponentType;
                var hashCode = srcInfos[srcIndex].HashCode;

                var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, srcData);

                // * remove refcount based on refcount table
                // * -1 because InsertSharedComponentAssumeNonDefault above adds 1 refcount
                int srcRefCount = remapPtr[srcIndex];
                SharedComponentInfoPtr[dstIndex].RefCount += srcRefCount - 1;
                srcManagedComponents.RemoveReference(srcIndex, srcRefCount);
                SharedComponentInfoPtr[dstIndex].Version++;

                remapPtr[srcIndex] = dstIndex;
            }

            return remap;
        }

        public void MoveManagedComponentsFromDifferentWorld(NativeArray<int> srcIndices, NativeArray<int> dstIndices, int count, ManagedComponentStore srcManagedComponentStore)
        {
            for (int i = 0; i < count; ++i)
            {
                int src = srcIndices[i];
                int dst = dstIndices[i];
                m_ManagedComponentData[dst] = srcManagedComponentStore.m_ManagedComponentData[src];
                srcManagedComponentStore.m_ManagedComponentData[src] = null;
            }
        }

        public void PrepareForDeserialize()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsEmpty())
                throw new System.ArgumentException("SharedComponentManager must be empty when deserializing a scene");
#endif

            ResetSharedComponentData();
        }

        public void PatchEntities(Archetype* archetype, Chunk* chunk, int entityCount,
            EntityRemapUtility.EntityRemapInfo* remapping)
        {
#if !NET_DOTS
            var firstManagedComponent = archetype->FirstManagedComponent;
            var numManagedComponents= archetype->NumManagedComponents;
            for (int i = 0; i < numManagedComponents; ++i)
            {
                int type = i + firstManagedComponent;
                var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, 0, type);
                for(int ei=0; ei<entityCount; ++ei)
                {
                    if(a[ei] != 0)
                    {
                        var obj = m_ManagedComponentData[a[ei]];
                        EntityRemapUtility.PatchEntityInBoxedType(obj, remapping);
                    }
                }
            }
#endif
        }

        void PatchEntitiesForPrefab(int* managedComponents, int numManagedComponents, int allocatedCount, int remappingCount, Entity* remapSrc, Entity* remapDst)
        {
#if !NET_DOTS
            for (int i = 0; i < allocatedCount; ++i)
            {
                for(int c=0;c<numManagedComponents;c++)
                {
                    var managedComponentIndex = managedComponents[c];
                    if (managedComponentIndex != 0)
                    {
                        var obj = m_ManagedComponentData[managedComponentIndex];
                        EntityRemapUtility.PatchEntityForPrefabInBoxedType(obj, remapSrc, remapDst, remappingCount);
                    }
                }
                managedComponents += numManagedComponents;
                remapDst += remappingCount;
            }
#endif
        }

        public void Playback(ref ManagedDeferredCommands managedDeferredCommands)
        {
            var reader = new UnsafeAppendBuffer.Reader(ref managedDeferredCommands.CommandBuffer);
            while (!reader.EndOfBuffer)
            {
                var cmd = reader.ReadNext<int>();
                switch ((ManagedDeferredCommands.Command)cmd)
                {
                    case (ManagedDeferredCommands.Command.IncrementSharedComponentVersion):
                    {
                        var sharedIndex = reader.ReadNext<int>();
                        IncrementSharedComponentVersion(sharedIndex);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.AddReference):
                    {
                        var index = reader.ReadNext<int>();
                        var numRefs = reader.ReadNext<int>();
                        AddReference(index, numRefs);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.RemoveReference):
                    {
                        var index = reader.ReadNext<int>();
                        var numRefs = reader.ReadNext<int>();
                        RemoveReference(index, numRefs);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.PatchManagedEntities):
                    {
                        var archetype = (Archetype*)reader.ReadNext<IntPtr>();
                        var chunk = (Chunk*)reader.ReadNext<IntPtr>();
                        var entityCount = reader.ReadNext<int>();
                        var remapping = (EntityRemapUtility.EntityRemapInfo*)reader.ReadNext<IntPtr>();

                        PatchEntities(archetype, chunk, entityCount, remapping);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.PatchManagedEntitiesForPrefabs):
                    {
                        var remapSrc = (byte*)reader.ReadNext<IntPtr>();
                        var allocatedCount = reader.ReadNext<int>();
                        var remappingCount = reader.ReadNext<int>();
                        var numManagedComponents = reader.ReadNext<int>();
                        var allocator = (Allocator)reader.ReadNext<int>();


                        var remapSrcSize = UnsafeUtility.SizeOf<Entity>() * remappingCount;
                        var remapDstSize = UnsafeUtility.SizeOf<Entity>() * remappingCount * allocatedCount;

                        var remapDst = remapSrc + remapSrcSize;
                        var managedComponents = remapDst + remapDstSize;

                        PatchEntitiesForPrefab((int*)managedComponents, numManagedComponents, allocatedCount, remappingCount, (Entity*)remapSrc, (Entity*)remapDst);
                        UnsafeUtility.Free(remapSrc, allocator);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.CloneManagedComponents):
                    {
                        var srcArray = (int*)reader.ReadNextArray<int>(out var componentCount);
                        var instanceCount = reader.ReadNext<int>();
                        var dstArray = (int*)reader.ReadNextArray<int>(out _);
                        CloneManagedComponents(srcArray, componentCount, dstArray, instanceCount);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.CloneHybridComponents):
                    {
                        var srcArray = (int*)reader.ReadNextArray<int>(out var componentCount);
                        var entities = (Entity*)reader.ReadNextArray<Entity>(out var instanceCount);
                        var dstComponentLinkIndices = (int*)reader.ReadNextArray<int>(out _);
                        var dstArray = (int*)reader.ReadNextArray<int>(out _);

                        if(InstantiateHybridComponent != null)
                            InstantiateHybridComponent(srcArray, componentCount, entities, dstComponentLinkIndices, dstArray, instanceCount, this);
                        else
                        {
                            // InstantiateHybridComponent was not injected just copy the reference to the object and dont clone it
                            for (int src = 0; src < componentCount; ++src)
                            {
                                object sourceComponent = m_ManagedComponentData[srcArray[src]];
                                for (int i = 0; i < instanceCount; ++i)
                                    m_ManagedComponentData[dstArray[i]] = sourceComponent;
                                dstArray += instanceCount;
                            }
                        }
                    }
                    break;

                    case (ManagedDeferredCommands.Command.FreeManagedComponents):
                    {
                        var count = reader.ReadNext<int>();
                        for (int i = 0; i < count; ++i)
                        {
                            var managedComponentIndex = reader.ReadNext<int>();
                            (m_ManagedComponentData[managedComponentIndex] as IDisposable)?.Dispose();
                            m_ManagedComponentData[managedComponentIndex] = null;
                        }
                    }
                    break;

                    case (ManagedDeferredCommands.Command.SetManagedComponentCapacity):
                    {
                        var capacity = reader.ReadNext<int>();
                        SetManagedComponentCapacity(capacity);
                    }
                    break;
               }
            }

            managedDeferredCommands.Reset();
        }

        public static object CloneManagedComponent(object obj)
        {
            if (obj == null)
            {
                return null;
            }
            if (obj is ICloneable cloneable)
            {
                return cloneable.Clone();
            }

            {
#if !NET_DOTS
                var type = obj.GetType();
                var buffer = new UnsafeAppendBuffer(16, 16, Allocator.Temp);
                var writer = new PropertiesBinaryWriter(&buffer);
                BoxedProperties.WriteBoxedType(obj, writer);

                var readBuffer = buffer.AsReader();
                var r2 = new PropertiesBinaryReader(&readBuffer, writer.GetObjectTable());
                object newObj = BoxedProperties.ReadBoxedClass(type, r2);
                buffer.Dispose();
                return newObj;
#else
                // Until DOTS Runtime supports Properties just reuse the same instance
                return obj;
#endif
            }
        }

#if !NET_DOTS
        internal static object CloneAndPatchManagedComponent(object obj, EntityRemapUtility.EntityRemapInfo* remapping)
        {
            var clone = CloneManagedComponent(obj);
            EntityRemapUtility.PatchEntityInBoxedType(clone, remapping);
            return clone;
        }
#endif

        private void CloneManagedComponents(int* srcArray, int componentCount, int* dstArray, int instanceCount)
        {
            for (int src = 0; src < componentCount; ++src)
            {
                object sourceComponent = m_ManagedComponentData[srcArray[src]];
                for (int i = 0; i < instanceCount; ++i)
                    m_ManagedComponentData[dstArray[i]] = CloneManagedComponent(sourceComponent);
                dstArray += instanceCount;
            }
        }

        internal void SetManagedComponentValue(int index, object componentObject)
        {
            m_ManagedComponentData[index] = componentObject;
        }

        // Ensure there are at least "count" free managed component indices and
        // resize managed component array directly if needed
        public void ReserveManagedComponentIndicesDirect(int count, ref EntityComponentStore entityComponentStore)
        {
            int freeCount = entityComponentStore.ManagedComponentFreeCount;
            if(freeCount >= count)
                return;

            int newCapacity = entityComponentStore.GrowManagedComponentCapacity(count-freeCount);
            SetManagedComponentCapacity(newCapacity);
        }

        public void UpdateManagedComponentValue(int* index, object value, ref EntityComponentStore entityComponentStore)
        {
            entityComponentStore.AssertNoQueuedManagedDeferredCommands();
            var iManagedComponent = *index;
            
            if(iManagedComponent != 0)
                (m_ManagedComponentData[iManagedComponent] as IDisposable)?.Dispose();
            
            if (value != null)
            {
                if (iManagedComponent == 0)
                {
                    ReserveManagedComponentIndicesDirect(1, ref entityComponentStore);
                    iManagedComponent = *index = entityComponentStore.AllocateManagedComponentIndex();
                }
            }
            else
            {
                if (iManagedComponent == 0)
                    return;
                *index = 0;
                entityComponentStore.FreeManagedComponentIndex(iManagedComponent);
            }
            m_ManagedComponentData[iManagedComponent] = value;
        }

        public void CloneManagedComponentsFromDifferentWorld(int* indices, int count, ManagedComponentStore srcManagedComponentStore, ref EntityComponentStore dstEntityComponentStore)
        {
            dstEntityComponentStore.AssertNoQueuedManagedDeferredCommands();
            ReserveManagedComponentIndicesDirect(count, ref dstEntityComponentStore);
            for (int i = 0; i < count; ++i)
            {
                var obj = srcManagedComponentStore.m_ManagedComponentData[indices[i]];
                var clone = CloneManagedComponent(obj);
                int dstIndex = dstEntityComponentStore.AllocateManagedComponentIndex();
                (m_ManagedComponentData[dstIndex] as IDisposable)?.Dispose();
                m_ManagedComponentData[dstIndex] = clone;
            }
        }

        public void ResetManagedComponentStoreForDeserialization(int managedComponentCount, ref EntityComponentStore entityComponentStore)
        {
            managedComponentCount++; // also need space for 0 index (null)
            Assert.AreEqual(0, entityComponentStore.ManagedComponentIndexUsedCount);
            entityComponentStore.m_ManagedComponentFreeIndex.Length = 0;
            entityComponentStore.m_ManagedComponentIndex = managedComponentCount;
            if (managedComponentCount > entityComponentStore.m_ManagedComponentIndexCapacity)
            {
                entityComponentStore.m_ManagedComponentIndexCapacity = managedComponentCount;
                SetManagedComponentCapacity(managedComponentCount);
            }
        }

        public static void DisposeManagedObject(object obj)
        {
            if(obj is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
