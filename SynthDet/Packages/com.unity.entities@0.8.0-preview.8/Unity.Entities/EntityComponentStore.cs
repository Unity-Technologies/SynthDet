using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Assertions;
using Unity.Mathematics;

// ---------------------------------------------------------------------------------------------------------
// EntityComponentStore
// ---------------------------------------------------------------------------------------------------------
// - Internal interface to Archetype, Entity, and Chunk data.
// - Throwing exceptions in this code not supported. (Can be called from burst delegate from main thread.)
// ---------------------------------------------------------------------------------------------------------

// Notes on upcoming changes to EntityComponentStore:
//
// Checklist @macton Where is entityComponentStore and the EntityBatch interface going?
// [ ] Replace all internal interfaces to entityComponentStore to work with EntityBatch via entityComponentStore
//   [x] Convert AddComponent NativeArray<Entity>
//   [x] Convert AddComponent NativeArray<ArchetypeChunk>
//   [x] Convert AddSharedComponent NativeArray<ArchetypeChunk>
//   [x] Convert AddChunkComponent NativeArray<ArchetypeChunk>
//   [x] Move AddComponents(entity)
//   [ ] Need AddComponents for NativeList<EntityBatch>
//   [ ] Convert DestroyEntities
//   [x] Convert RemoveComponent NativeArray<ArchetypeChunk>
//   [x] Convert RemoveComponent Entity
// [x] EntityDataManager just becomes thin shim on top of EntityComponentStore
// [x] Remove EntityDataManager
// [x] Rework internal storage so that structural changes are blittable (and burst job)
// [ ] Expose EntityBatch interface public via EntityManager
// [ ] Other structural interfaces (e.g. NativeArray<Entity>) are then (optional) utility functions.
//
// 1. Ideally EntityComponentStore is the internal interface that EntityCommandBuffer can use (fast).
// 2. That would be the only access point for JobComponentSystem.
// 3. "Easy Mode" can have (the equivalent) of EntityManager as utility functions on EntityComponentStore.
// 4. EntityDataManager goes away.
//
// Input data protocol to support for structural changes:
//    1. NativeList<EntityBatch>
//    2. NativeArray<ArchetypeChunk>
//    3. Entity
//
// Expected public (internal) API:
//
// ** Add Component **
//
// IComponentData and ISharedComponentData can be added via:
//    AddComponent NativeList<EntityBatch>
//    AddComponent Entity
//    AddComponents NativeList<EntityBatch>
//    AddComponents Entity
//
// Chunk Components can only be added via;
//    AddChunkComponent NativeArray<ArchetypeChunk>
//
// Alternative to add ISharedComponeentData when changing whole chunks.
//    AddSharedComponent NativeArray<ArchetypeChunk>
//
// ** Remove Component **
//
// Any component type can be removed via:
//    RemoveComponent NativeList<EntityBatch>
//    RemoveComponent Entity
//    RemoveComponent NativeArray<ArchetypeChunk>
//    RemoveComponents NativeList<EntityBatch>
//    RemoveComponents Entity
//    RemoveComponents NativeArray<ArchetypeChunk>


namespace Unity.Entities
{
    internal unsafe struct ManagedDeferredCommands : IDisposable
    {
        public UnsafeAppendBuffer CommandBuffer;
        public bool Empty => CommandBuffer.IsEmpty;

        public enum Command
        {
            IncrementSharedComponentVersion,
            PatchManagedEntities,
            PatchManagedEntitiesForPrefabs,
            AddReference,
            RemoveReference,
            CloneManagedComponents,
            CloneHybridComponents,
            FreeManagedComponents,
            SetManagedComponentCapacity
        }

        public void Init()
        {
            CommandBuffer = new UnsafeAppendBuffer(1024, 16, Allocator.Persistent);
        }

        public void Dispose()
        {
            CommandBuffer.Dispose();
        }

        public void Reset()
        {
            CommandBuffer.Reset();
        }

        public unsafe void IncrementComponentOrderVersion(Archetype* archetype,
            SharedComponentValues sharedComponentValues)
        {
            for (var i = 0; i < archetype->NumSharedComponents; i++)
            {
                CommandBuffer.Add<int>((int)Command.IncrementSharedComponentVersion);
                CommandBuffer.Add<int>(sharedComponentValues[i]);
            }
        }

        public void PatchEntities(Archetype* archetype, Chunk* chunk, int entityCount,
            NativeArray<EntityRemapUtility.EntityRemapInfo> remapping)
        {
            // In every case this is called ManagedChangesTracker.Playback() is called in the same calling function.
            // There is no question of lifetime. So the pointer is safely deferred.
            
            CommandBuffer.Add<int>((int)Command.PatchManagedEntities);
            CommandBuffer.Add<IntPtr>((IntPtr)archetype);
            CommandBuffer.Add<IntPtr>((IntPtr)chunk);
            CommandBuffer.Add<int>(entityCount);
            CommandBuffer.Add<IntPtr>((IntPtr)remapping.GetUnsafePtr());
        }

        public void PatchEntitiesForPrefab(Archetype* archetype, Chunk* chunk, int indexInChunk, int allocatedCount,
            Entity* remapSrc, Entity* remapDst, int remappingCount, Allocator allocator)
        {
            // We are deferring the patching so we need a copy of the remapping info since we can't be certain of its lifetime.
            // We will free this ptr in the ManagedComponentStore.PatchEntitiesForPrefab call

            var numManagedComponents= archetype->NumManagedComponents;
            var totalComponentCount = numManagedComponents * allocatedCount;
            var remapSrcSize = UnsafeUtility.SizeOf<Entity>() * remappingCount;
            var remapDstSize = UnsafeUtility.SizeOf<Entity>() * remappingCount * allocatedCount;
            var managedComponentSize = totalComponentCount * sizeof(int);

            var remapSrcCopy = (byte*) UnsafeUtility.Malloc(remapSrcSize + remapDstSize + managedComponentSize, 16, Allocator.Temp);
            var remapDstCopy = remapSrcCopy + remapSrcSize;
            var managedComponents = (int*)(remapDstCopy + remapDstSize);

            UnsafeUtility.MemCpy(remapSrcCopy, remapSrc, remapSrcSize);
            UnsafeUtility.MemCpy(remapDstCopy, remapDst, remapDstSize);

            var firstManagedComponent = archetype->FirstManagedComponent;
            for (int i = 0; i < numManagedComponents; ++i)
            {
                int type = i + firstManagedComponent;
                var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, 0, type);
                for(int ei=0; ei<allocatedCount; ++ei)
                {
                    managedComponents[ei * numManagedComponents + i] = a[ei + indexInChunk];
                }
            }

            CommandBuffer.Add<int>((int)Command.PatchManagedEntitiesForPrefabs);
            CommandBuffer.Add<IntPtr>((IntPtr)remapSrcCopy);
            CommandBuffer.Add<int>(allocatedCount);
            CommandBuffer.Add<int>(remappingCount);
            CommandBuffer.Add<int>(archetype->NumManagedComponents);
            CommandBuffer.Add<int>((int)allocator);
        }

        public void AddReference(int index, int numRefs = 1)
        {
            if (index == 0)
                return;
            CommandBuffer.Add<int>((int)Command.AddReference);
            CommandBuffer.Add<int>(index);
            CommandBuffer.Add<int>(numRefs);
        }

        public void RemoveReference(int index, int numRefs = 1)
        {
            if (index == 0)
                return;
            CommandBuffer.Add<int>((int)Command.RemoveReference);
            CommandBuffer.Add<int>(index);
            CommandBuffer.Add<int>(numRefs);
        }
        
        public void CloneManagedComponentBegin(int* srcIndices, int componentCount, int instanceCount)
        {
            CommandBuffer.Add<int>((int)Command.CloneManagedComponents);
            CommandBuffer.AddArray<int>(srcIndices, componentCount);
            CommandBuffer.Add<int>(instanceCount);
            CommandBuffer.Add<int>(instanceCount * componentCount);
        }

        public void CloneManagedComponentAddDstIndices(int* dstIndices, int count)
        {
            CommandBuffer.Add(dstIndices, count * sizeof(int));
        }

        public void CloneHybridComponentBegin(int* srcIndices, int componentCount, Entity* dstEntities, int instanceCount, int* dstCompanionLinkIndices)
        {
            CommandBuffer.Add<int>((int)Command.CloneHybridComponents);
            CommandBuffer.AddArray<int>(srcIndices, componentCount);
            CommandBuffer.AddArray<Entity>(dstEntities, instanceCount);
            CommandBuffer.AddArray<int>(dstCompanionLinkIndices, dstCompanionLinkIndices == null ? 0 : instanceCount);
            CommandBuffer.Add<int>(instanceCount * componentCount);
        }

        public void CloneHybridComponentAddDstIndices(int* dstIndices, int count)
        {
            CommandBuffer.Add(dstIndices, count * sizeof(int));
        }

        
        public int BeginFreeManagedComponentCommand()
        {
            CommandBuffer.Add<int>((int)Command.FreeManagedComponents);
                        
            CommandBuffer.Add<int>(-1); // this will contain the array count
            return CommandBuffer.Length - sizeof(int);
        }

        public void AddToFreeManagedComponentCommand(int managedComponentIndex)
        {
            CommandBuffer.Add<int>(managedComponentIndex);
        }
        
        public void EndDeallocateManagedComponentCommand(int handle)
        {
            int count = (CommandBuffer.Length - handle)/sizeof(int) - 1;
            if (count == 0)
            {
                CommandBuffer.Length -= sizeof(int) * 2;
            }
            else
            {
                int* countInCommand = (int*)(CommandBuffer.Ptr + handle);
                Assert.AreEqual(-1, *countInCommand);
                *countInCommand = count;
            }
        }

        public void SetManagedComponentCapacity(int capacity)
        {
            CommandBuffer.Add<int>((int)Command.SetManagedComponentCapacity);
            CommandBuffer.Add<int>(capacity);
        }
    }

    internal unsafe partial struct EntityComponentStore
    {
        [NativeDisableUnsafePtrRestriction]
        int* m_VersionByEntity;

        [NativeDisableUnsafePtrRestriction]
        Archetype** m_ArchetypeByEntity;

        [NativeDisableUnsafePtrRestriction]
        EntityInChunk* m_EntityInChunkByEntity;

        [NativeDisableUnsafePtrRestriction]
        int* m_ComponentTypeOrderVersion;

        ChunkAllocator m_ArchetypeChunkAllocator;

        internal UnsafeChunkPtrList m_EmptyChunks;
        internal UnsafeArchetypePtrList m_Archetypes;
        internal UnsafeChunkPtrList m_DeleteChunks;

        ArchetypeListMap m_TypeLookup;

        internal int m_ManagedComponentIndex;
        internal int m_ManagedComponentIndexCapacity;
        internal UnsafeAppendBuffer m_ManagedComponentFreeIndex;

        internal ManagedDeferredCommands ManagedChangesTracker;

        ulong m_NextChunkSequenceNumber;

        int  m_NextFreeEntityIndex;
        uint m_GlobalSystemVersion;
        int  m_EntitiesCapacity;
        uint m_ArchetypeTrackingVersion;

        int m_LinkedGroupType;
        int m_ChunkHeaderType;
        int m_PrefabType;
        int m_CleanupEntityType;
        int m_DisabledType;
        int m_EntityType;

        ComponentType m_ChunkHeaderComponentType;
        ComponentType m_EntityComponentType;

        TypeManager.TypeInfo* m_TypeInfos;
        TypeManager.EntityOffsetInfo* m_EntityOffsetInfos;

        internal byte memoryInitPattern;
        internal byte useMemoryInitPattern;        // should be bool, but it doesn't get along nice with burst so far, so we use a byte instead

        const int kMaximumEmptyChunksInPool = 16; // can't alloc forever
        const int kDefaultCapacity = 1024;
        const int kMaxSharedComponentCount = 8;


#if UNITY_EDITOR
        [NativeDisableUnsafePtrRestriction]
        NumberedWords* m_NameByEntity;
#endif

        public int EntityOrderVersion => GetComponentTypeOrderVersion(m_EntityType);
        public int EntitiesCapacity => m_EntitiesCapacity;
        public uint GlobalSystemVersion => m_GlobalSystemVersion;

        public void SetGlobalSystemVersion(uint value)
        {
            m_GlobalSystemVersion = value;
        }

        void IncreaseCapacity()
        {
            EnsureCapacity(m_EntitiesCapacity * 2);
        }

        internal void EnsureCapacity(int value)
        {
            // Capacity can never be decreased since entity lookups would start failing as a result
            if (value <= m_EntitiesCapacity)
                return;

            var versionBytes = (value * sizeof(int) + 63) & ~63;
            var archetypeBytes = (value * sizeof(Archetype*) + 63) & ~63;
            var chunkDataBytes = (value * sizeof(EntityInChunk) + 63) & ~63;
            var bytesToAllocate = versionBytes + archetypeBytes + chunkDataBytes;
#if UNITY_EDITOR
            var nameBytes = (value * sizeof(NumberedWords) + 63) & ~63;
            bytesToAllocate += nameBytes;
#endif

            var bytes = (byte*)UnsafeUtility.Malloc(bytesToAllocate, 64, Allocator.Persistent);

            var version = (int*)(bytes);
            var archetype = (Archetype**)(bytes + versionBytes);
            var chunkData = (EntityInChunk*)(bytes + versionBytes + archetypeBytes);
#if UNITY_EDITOR
            var name = (NumberedWords*)(bytes + versionBytes + archetypeBytes + chunkDataBytes);
#endif

            var startNdx = 0;
            if (m_EntitiesCapacity > 0)
            {
                UnsafeUtility.MemCpy(version, m_VersionByEntity, m_EntitiesCapacity * sizeof(int));
                UnsafeUtility.MemCpy(archetype, m_ArchetypeByEntity, m_EntitiesCapacity * sizeof(Archetype*));
                UnsafeUtility.MemCpy(chunkData, m_EntityInChunkByEntity, m_EntitiesCapacity * sizeof(EntityInChunk));
#if UNITY_EDITOR
                UnsafeUtility.MemCpy(name, m_NameByEntity, m_EntitiesCapacity * sizeof(NumberedWords));
#endif
                UnsafeUtility.Free(m_VersionByEntity, Allocator.Persistent);
                startNdx = m_EntitiesCapacity - 1;
            }

            m_VersionByEntity = version;
            m_ArchetypeByEntity = archetype;
            m_EntityInChunkByEntity = chunkData;
#if UNITY_EDITOR
            m_NameByEntity = name;
#endif

            m_EntitiesCapacity = value;
            InitializeAdditionalCapacity(startNdx);
        }

        public void CopyNextFreeEntityIndex(EntityComponentStore* src)
        {
            m_NextFreeEntityIndex = src->m_NextFreeEntityIndex;
        }

        private void InitializeAdditionalCapacity(int start)
        {
            for (var i = start; i != EntitiesCapacity; i++)
            {
                m_EntityInChunkByEntity[i].IndexInChunk = i + 1;
                m_VersionByEntity[i] = 1;
                m_EntityInChunkByEntity[i].Chunk = null;
#if UNITY_EDITOR
                m_NameByEntity[i] = new NumberedWords();
#endif
            }

            // Last entity indexInChunk identifies that we ran out of space...
            m_EntityInChunkByEntity[EntitiesCapacity - 1].IndexInChunk = -1;
        }

        public static EntityComponentStore* Create(ulong startChunkSequenceNumber, int newCapacity = kDefaultCapacity)
        {
            var entities = (EntityComponentStore*)UnsafeUtility.Malloc(sizeof(EntityComponentStore), 64, Allocator.Persistent);
            UnsafeUtility.MemClear(entities, sizeof(EntityComponentStore));

            entities->EnsureCapacity(newCapacity);
            entities->m_GlobalSystemVersion = ChangeVersionUtility.InitialGlobalSystemVersion;

            const int componentTypeOrderVersionSize = sizeof(int) * TypeManager.MaximumTypesCount;
            entities->m_ComponentTypeOrderVersion = (int*)UnsafeUtility.Malloc(componentTypeOrderVersionSize,
                UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            UnsafeUtility.MemClear(entities->m_ComponentTypeOrderVersion, componentTypeOrderVersionSize);

            entities->m_ArchetypeChunkAllocator = new ChunkAllocator();
            entities->m_TypeLookup = new ArchetypeListMap();
            entities->m_TypeLookup.Init(16);
            entities->m_NextChunkSequenceNumber = startChunkSequenceNumber;
            entities->m_EmptyChunks = new UnsafeChunkPtrList(0, Allocator.Persistent);
            entities->m_DeleteChunks = new UnsafeChunkPtrList(0, Allocator.Persistent);
            entities->m_Archetypes = new UnsafeArchetypePtrList(0, Allocator.Persistent);
            entities->ManagedChangesTracker = new ManagedDeferredCommands();
            entities->ManagedChangesTracker.Init();
            entities->m_ManagedComponentIndex = 1;
            entities->m_ManagedComponentIndexCapacity = 64;
            entities->m_ManagedComponentFreeIndex = new UnsafeAppendBuffer(1024, 16, Allocator.Persistent);
            entities->m_LinkedGroupType = TypeManager.GetTypeIndex<LinkedEntityGroup>();
            entities->m_ChunkHeaderType = TypeManager.GetTypeIndex<ChunkHeader>();
            entities->m_PrefabType = TypeManager.GetTypeIndex<Prefab>();
            entities->m_CleanupEntityType = TypeManager.GetTypeIndex<CleanupEntity>();
            entities->m_DisabledType = TypeManager.GetTypeIndex<Disabled>();
            entities->m_EntityType = TypeManager.GetTypeIndex<Entity>();

            entities->m_ChunkHeaderComponentType = ComponentType.ReadWrite<ChunkHeader>();
            entities->m_EntityComponentType = ComponentType.ReadWrite<Entity>();
            entities->InitializeTypeManagerPointers();

            // Sanity check a few alignments
#if UNITY_ASSERTIONS
            // Buffer should be 16 byte aligned to ensure component data layout itself can gurantee being aligned
            var offset = UnsafeUtility.GetFieldOffset(typeof(Chunk).GetField("Buffer"));
            Assert.IsTrue(offset % TypeManager.MaximumSupportedAlignment == 0, $"Chunk buffer must be {TypeManager.MaximumSupportedAlignment} byte aligned (buffer offset at {offset})");
            Assert.IsTrue(sizeof(Entity) == 8, $"Unity.Entities.Entity is expected to be 8 bytes in size (is {sizeof(Entity)}); if this changes, update Chunk explicit layout");
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var bufHeaderSize = UnsafeUtility.SizeOf<BufferHeader>();
            Assert.IsTrue(bufHeaderSize % TypeManager.MaximumSupportedAlignment == 0,
                $"BufferHeader total struct size must be a multiple of the max supported alignment ({TypeManager.MaximumSupportedAlignment})");
#endif

            return entities;
        }

        internal void InitializeTypeManagerPointers()
        {
            m_TypeInfos = TypeManager.GetTypeInfoPointer();
            m_EntityOffsetInfos = TypeManager.GetEntityOffsetsPointer();
        }
        
        public TypeManager.TypeInfo GetTypeInfo(int typeIndex)
        {
            return m_TypeInfos[typeIndex & TypeManager.ClearFlagsMask];
        }

        public TypeManager.EntityOffsetInfo* GetEntityOffsets(TypeManager.TypeInfo typeInfo)
        {
            if (!typeInfo.HasEntities)
                return null;
            return m_EntityOffsetInfos + typeInfo.EntityOffsetStartIndex;
        }

        public TypeManager.EntityOffsetInfo* GetEntityOffsets(int typeIndex)
        {
            var typeInfo = m_TypeInfos[typeIndex & TypeManager.ClearFlagsMask];
            return GetEntityOffsets(typeInfo);
        }

        public int ChunkComponentToNormalTypeIndex(int typeIndex) => m_TypeInfos[typeIndex & TypeManager.ClearFlagsMask].TypeIndex;

        public static void Destroy(EntityComponentStore* entityComponentStore)
        {
            entityComponentStore->Dispose();
            UnsafeUtility.Free(entityComponentStore, Allocator.Persistent);
        }

        void Dispose()
        {
            if (m_EntitiesCapacity > 0)
            {
                UnsafeUtility.Free(m_VersionByEntity, Allocator.Persistent);

                m_VersionByEntity = null;
                m_ArchetypeByEntity = null;
                m_EntityInChunkByEntity = null;
#if UNITY_EDITOR
                m_NameByEntity = null;
#endif

                m_EntitiesCapacity = 0;
            }

            if (m_ComponentTypeOrderVersion != null)
            {
                UnsafeUtility.Free(m_ComponentTypeOrderVersion, Allocator.Persistent);
                m_ComponentTypeOrderVersion = null;
            }

            // Move all chunks to become pooled chunks
            for (var i = 0; i < m_Archetypes.Length; i++)
            {
                var archetype = m_Archetypes.Ptr[i];

                for (int c = 0; c != archetype->Chunks.Count; c++)
                {
                    var chunk = archetype->Chunks.p[c];

                    ChunkDataUtility.DeallocateBuffers(chunk);
                    UnsafeUtility.Free(archetype->Chunks.p[c], Allocator.Persistent);
                }

                archetype->Chunks.Dispose();
                archetype->ChunksWithEmptySlots.Dispose();
                archetype->FreeChunksBySharedComponents.Dispose();
            }

            m_Archetypes.Dispose();

            // And all pooled chunks
            for (var i = 0; i != m_EmptyChunks.Length; ++i)
            {
                var chunk = m_EmptyChunks.Ptr[i];
                UnsafeUtility.Free(chunk, Allocator.Persistent);
            }

            m_EmptyChunks.Dispose();

            m_TypeLookup.Dispose();
            m_ArchetypeChunkAllocator.Dispose();
            ManagedChangesTracker.Dispose();
            m_ManagedComponentFreeIndex.Dispose();
        }

        public void FreeAllEntities()
        {
            for (var i = 0; i != EntitiesCapacity; i++)
            {
                m_EntityInChunkByEntity[i].IndexInChunk = i + 1;
                m_VersionByEntity[i] += 1;
                m_EntityInChunkByEntity[i].Chunk = null;
#if UNITY_EDITOR
                m_NameByEntity[i] = new NumberedWords();
#endif
            }

            // Last entity indexInChunk identifies that we ran out of space...
            m_EntityInChunkByEntity[EntitiesCapacity - 1].IndexInChunk = -1;
            m_NextFreeEntityIndex = 0;
        }

        public void FreeEntities(Chunk* chunk)
        {
            var count = chunk->Count;
            var entities = (Entity*)chunk->Buffer;
            int freeIndex = m_NextFreeEntityIndex;
            for (var i = 0; i != count; i++)
            {
                int index = entities[i].Index;
                m_VersionByEntity[index] += 1;
                m_EntityInChunkByEntity[index].Chunk = null;
                m_EntityInChunkByEntity[index].IndexInChunk = freeIndex;
#if UNITY_EDITOR
                m_NameByEntity[index] = new NumberedWords();
#endif
                freeIndex = index;
            }

            m_NextFreeEntityIndex = freeIndex;
        }

#if UNITY_EDITOR
        public string GetName(Entity entity)
        {
            return m_NameByEntity[entity.Index].ToString();
        }

        public void SetName(Entity entity, string name)
        {
            m_NameByEntity[entity.Index].SetString(name);
        }

        public void CopyName(Entity dstEntity, Entity srcEntity)
        {
            m_NameByEntity[dstEntity.Index] = m_NameByEntity[srcEntity.Index];
        }

#endif

        public int GetStoredVersion(Entity entity) => m_VersionByEntity[entity.Index];

        public Archetype* GetArchetype(Entity entity)
        {
            return m_ArchetypeByEntity[entity.Index];
        }

        public void SetArchetype(Entity entity, Archetype* archetype)
        {
            m_ArchetypeByEntity[entity.Index] = archetype;
        }

        public void SetArchetype(Chunk* srcChunk, Archetype* dstArchetype)
        {
            var entities = (Entity*)srcChunk->Buffer;
            var count = srcChunk->Count;
            for (int i = 0; i < count; ++i)
            {
                m_ArchetypeByEntity[entities[i].Index] = dstArchetype;
            }

            srcChunk->Archetype = dstArchetype;
        }

        public Chunk* GetChunk(Entity entity)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;

            return entityChunk;
        }

        public void SetEntityInChunk(Entity entity, EntityInChunk entityInChunk)
        {
            m_EntityInChunkByEntity[entity.Index] = entityInChunk;
        }

        public EntityInChunk GetEntityInChunk(Entity entity)
        {
            return m_EntityInChunkByEntity[entity.Index];
        }

        public void IncrementComponentTypeOrderVersion(Archetype* archetype)
        {
            // Increment type component version
            for (var t = 0; t < archetype->TypesCount; ++t)
            {
                var typeIndex = archetype->Types[t].TypeIndex;
                m_ComponentTypeOrderVersion[typeIndex & TypeManager.ClearFlagsMask]++;
            }
        }

        public bool Exists(Entity entity)
        {
            int index = entity.Index;

            ValidateEntity(entity);

            var versionMatches = m_VersionByEntity[index] == entity.Version;
            var hasChunk = m_EntityInChunkByEntity[index].Chunk != null;

            return versionMatches && hasChunk;
        }

        public int GetComponentTypeOrderVersion(int typeIndex)
        {
            return m_ComponentTypeOrderVersion[typeIndex & TypeManager.ClearFlagsMask];
        }

        public void IncrementGlobalSystemVersion()
        {
            ChangeVersionUtility.IncrementGlobalSystemVersion(ref m_GlobalSystemVersion);
        }

        public bool HasComponent(Entity entity, int type)
        {
            if (!Exists(entity))
                return false;

            var archetype = m_ArchetypeByEntity[entity.Index];
            return ChunkDataUtility.GetIndexInTypeArray(archetype, type) != -1;
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            if (!Exists(entity))
                return false;

            var archetype = m_ArchetypeByEntity[entity.Index];
            return ChunkDataUtility.GetIndexInTypeArray(archetype, type.TypeIndex) != -1;
        }

        public int GetSizeInChunk(Entity entity, int typeIndex, ref int typeLookupCache)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            return ChunkDataUtility.GetSizeInChunk(entityChunk, typeIndex, ref typeLookupCache);
        }

        public void SetChunkComponent<T>(NativeArray<ArchetypeChunk> chunks, T componentData)
            where T : unmanaged, IComponentData
        {
            var type = ComponentType.ReadWrite<T>();
            var chunkTypeIndex = TypeManager.MakeChunkComponentTypeIndex(type.TypeIndex);
            ArchetypeChunk* chunkPtr = (ArchetypeChunk*)NativeArrayUnsafeUtility.GetUnsafePtr(chunks);

            SetChunkComponent(chunkPtr, chunks.Length, &componentData, chunkTypeIndex );
        }

        public void SetChunkComponent(ArchetypeChunk* chunks, int chunkCount, void* componentData, int componentTypeIndex)
        {
            var type = ComponentType.FromTypeIndex(componentTypeIndex);
            if (type.IsZeroSized)
                return;

            for (int i = 0; i < chunkCount; i++)
            {
                var srcChunk = chunks[i].m_Chunk;
                var ptr = GetComponentDataWithTypeRW(srcChunk->metaChunkEntity, componentTypeIndex, m_GlobalSystemVersion);
                var sizeInChunk = GetTypeInfo(componentTypeIndex).SizeInChunk;
                UnsafeUtility.MemCpy(ptr, componentData, sizeInChunk);
            }
        }

        public void AllocateEntities(Archetype* arch, Chunk* chunk, int baseIndex, int count, Entity* outputEntities)
        {
            Assert.AreEqual(chunk->Archetype->Offsets[0], 0);
            Assert.AreEqual(chunk->Archetype->SizeOfs[0], sizeof(Entity));

            var entityInChunkStart = (Entity*)chunk->Buffer + baseIndex;

            for (var i = 0; i != count; i++)
            {
                var entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                if (entityIndexInChunk == -1)
                {
                    IncreaseCapacity();
                    entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                }

                var entityVersion = m_VersionByEntity[m_NextFreeEntityIndex];

                if (outputEntities != null)
                {
                    outputEntities[i].Index = m_NextFreeEntityIndex;
                    outputEntities[i].Version = entityVersion;
                }

                var entityInChunk = entityInChunkStart + i;

                entityInChunk->Index = m_NextFreeEntityIndex;
                entityInChunk->Version = entityVersion;

                m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk = baseIndex + i;
                m_ArchetypeByEntity[m_NextFreeEntityIndex] = arch;
                m_EntityInChunkByEntity[m_NextFreeEntityIndex].Chunk = chunk;
#if UNITY_EDITOR
                m_NameByEntity[m_NextFreeEntityIndex] = new NumberedWords();
#endif

                m_NextFreeEntityIndex = entityIndexInChunk;
            }
        }

        public void GetChunk(Entity entity, out Chunk* chunk, out int chunkIndex)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var entityIndexInChunk = m_EntityInChunkByEntity[entity.Index].IndexInChunk;

            chunk = entityChunk;
            chunkIndex = entityIndexInChunk;
        }

        public byte* GetComponentDataWithTypeRO(Entity entity, int typeIndex)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var entityIndexInChunk = m_EntityInChunkByEntity[entity.Index].IndexInChunk;

            return ChunkDataUtility.GetComponentDataWithTypeRO(entityChunk, entityIndexInChunk, typeIndex);
        }

        public byte* GetComponentDataWithTypeRW(Entity entity, int typeIndex, uint globalVersion)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var entityIndexInChunk = m_EntityInChunkByEntity[entity.Index].IndexInChunk;

            return ChunkDataUtility.GetComponentDataWithTypeRW(entityChunk, entityIndexInChunk, typeIndex,
                globalVersion);
        }

        public byte* GetComponentDataWithTypeRO(Entity entity, int typeIndex, ref int typeLookupCache)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var entityIndexInChunk = m_EntityInChunkByEntity[entity.Index].IndexInChunk;

            return ChunkDataUtility.GetComponentDataWithTypeRO(entityChunk, entityIndexInChunk, typeIndex,
                ref typeLookupCache);
        }

        public byte* GetComponentDataWithTypeRW(Entity entity, int typeIndex, uint globalVersion,
            ref int typeLookupCache)
        {
            var entityChunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var entityIndexInChunk = m_EntityInChunkByEntity[entity.Index].IndexInChunk;

            return ChunkDataUtility.GetComponentDataWithTypeRW(entityChunk, entityIndexInChunk, typeIndex,
                globalVersion, ref typeLookupCache);
        }
        
        public void* GetComponentDataRawRW(Entity entity, int typeIndex)
        {
            AssertEntityHasComponent(entity, typeIndex);
            return GetComponentDataRawRWEntityHasComponent(entity, typeIndex);
        }

        internal void* GetComponentDataRawRWEntityHasComponent(Entity entity, int typeIndex)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (TypeManager.GetTypeInfo(typeIndex).IsZeroSized)
                throw new System.ArgumentException(
                    "GetComponentData() can not be called with a zero sized component.");
#endif

            var ptr = GetComponentDataWithTypeRW(entity, typeIndex, GlobalSystemVersion);
            return ptr;
        }
        
        public void SetComponentDataRawEntityHasComponent(Entity entity, int typeIndex, void* data, int size)
        {
            AssertEntityHasComponent(entity, typeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (TypeManager.GetTypeInfo(typeIndex).SizeInChunk != size)
                throw new System.ArgumentException(
                    "SetComponentData can not be called with a zero sized component and must have same size as sizeof(T).");
#endif

            var ptr = GetComponentDataWithTypeRW(entity, typeIndex,
                GlobalSystemVersion);
            UnsafeUtility.MemCpy(ptr, data, size);
        }
        
        public void SetBufferRawWithValidation(Entity entity, int componentTypeIndex, BufferHeader* tempBuffer, int sizeInChunk)
        {
            AssertEntityHasComponent(entity, componentTypeIndex);

            var ptr = GetComponentDataWithTypeRW(entity, componentTypeIndex,
                GlobalSystemVersion);

            BufferHeader.Destroy((BufferHeader*) ptr);

            UnsafeUtility.MemCpy(ptr, tempBuffer, sizeInChunk);
        }

        public int GetSharedComponentDataIndex(Entity entity, int typeIndex)
        {
            var archetype = m_ArchetypeByEntity[entity.Index];
            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            var chunk = m_EntityInChunkByEntity[entity.Index].Chunk;
            var sharedComponentValueArray = chunk->SharedComponentValues;
            var sharedComponentOffset = indexInTypeArray - archetype->FirstSharedComponent;
            return sharedComponentValueArray[sharedComponentOffset];
        }

        public void LockChunks(ArchetypeChunk* archetypeChunks, int count, ChunkFlags flags)
        {
            for (int i = 0; i < count; i++)
            {
                var chunk = archetypeChunks[i].m_Chunk;

                Assert.IsFalse(chunk->Locked);

                chunk->Flags |= (uint)flags;
                if (chunk->Count < chunk->Capacity && (flags & ChunkFlags.Locked) != 0)
                    chunk->Archetype->EmptySlotTrackingRemoveChunk(chunk);
            }
        }

        public void UnlockChunks(ArchetypeChunk* archetypeChunks, int count, ChunkFlags flags)
        {
            for (int i = 0; i < count; i++)
            {
                var chunk = archetypeChunks[i].m_Chunk;

                Assert.IsTrue(chunk->Locked);

                chunk->Flags &= ~(uint)flags;
                if (chunk->Count < chunk->Capacity && (flags & ChunkFlags.Locked) != 0)
                    chunk->Archetype->EmptySlotTrackingAddChunk(chunk);
            }
        }

        public void AllocateConsecutiveEntitiesForLoading(int count)
        {
            int newCapacity = count + 1; // make room for Entity.Null
            EnsureCapacity(newCapacity + 1); // the last entity is used to indicate we ran out of space
            m_NextFreeEntityIndex = newCapacity;
            for (int i = 1; i < newCapacity; ++i)
            {
                Assert.IsTrue(m_EntityInChunkByEntity[i].Chunk == null); //  Loading into non-empty entity manager is not supported.

                m_EntityInChunkByEntity[i].IndexInChunk = 0;
                m_VersionByEntity[i] = 0;
#if UNITY_EDITOR
                m_NameByEntity[i] = new NumberedWords();
#endif
            }
        }

        public void AddExistingEntitiesInChunk(Chunk* chunk)
        {
            for (int iEntity = 0; iEntity < chunk->Count; ++iEntity)
            {
                var entity = (Entity*)ChunkDataUtility.GetComponentDataRO(chunk, iEntity, 0);

                m_EntityInChunkByEntity[entity->Index].Chunk = chunk;
                m_EntityInChunkByEntity[entity->Index].IndexInChunk = iEntity;
                m_ArchetypeByEntity[entity->Index] = chunk->Archetype;
                m_VersionByEntity[entity->Index] = entity->Version;
            }
        }

        public void AllocateEntitiesForRemapping(EntityComponentStore* srcEntityComponentStore,
            ref NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            var count = srcEntityComponentStore->EntitiesCapacity;
            for (var i = 0; i != count; i++)
            {
                if (srcEntityComponentStore->m_EntityInChunkByEntity[i].Chunk != null)
                {
                    var entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                    if (entityIndexInChunk == -1)
                    {
                        IncreaseCapacity();
                        entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                    }

                    var entityVersion = m_VersionByEntity[m_NextFreeEntityIndex];

                    EntityRemapUtility.AddEntityRemapping(ref entityRemapping,
                        new Entity {Version = srcEntityComponentStore->m_VersionByEntity[i], Index = i},
                        new Entity {Version = entityVersion, Index = m_NextFreeEntityIndex});
                    m_NextFreeEntityIndex = entityIndexInChunk;
                }
            }
        }

        public void AllocateEntitiesForRemapping(Chunk* chunk,
            ref NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            var count = chunk->Count;
            var entities = (Entity*)chunk->Buffer;
            for (var i = 0; i != count; i++)
            {
                var entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                if (entityIndexInChunk == -1)
                {
                    IncreaseCapacity();
                    entityIndexInChunk = m_EntityInChunkByEntity[m_NextFreeEntityIndex].IndexInChunk;
                }

                var entityVersion = m_VersionByEntity[m_NextFreeEntityIndex];

                EntityRemapUtility.AddEntityRemapping(ref entityRemapping,
                    new Entity {Version = entities[i].Version, Index = entities[i].Index},
                    new Entity {Version = entityVersion, Index = m_NextFreeEntityIndex});
                m_NextFreeEntityIndex = entityIndexInChunk;
            }
        }

        public void RemapChunk(Archetype* arch, Chunk* chunk, int baseIndex, int count,
            ref NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            Assert.AreEqual(chunk->Archetype->Offsets[0], 0);
            Assert.AreEqual(chunk->Archetype->SizeOfs[0], sizeof(Entity));

            var entityInChunkStart = (Entity*)(chunk->Buffer) + baseIndex;

            for (var i = 0; i != count; i++)
            {
                var entityInChunk = entityInChunkStart + i;
                var target = EntityRemapUtility.RemapEntity(ref entityRemapping, *entityInChunk);
                var entityVersion = m_VersionByEntity[target.Index];

                Assert.AreEqual(entityVersion, target.Version);

                entityInChunk->Index = target.Index;
                entityInChunk->Version = entityVersion;
                m_EntityInChunkByEntity[target.Index].IndexInChunk = baseIndex + i;
                m_ArchetypeByEntity[target.Index] = arch;
                m_EntityInChunkByEntity[target.Index].Chunk = chunk;
            }

            if (chunk->metaChunkEntity != Entity.Null)
            {
                chunk->metaChunkEntity = EntityRemapUtility.RemapEntity(ref entityRemapping, chunk->metaChunkEntity);
            }
        }

        public enum ComponentOperation
        {
            Null,
            AddComponent,
            RemoveComponent
        }

        [BurstCompile]
        struct EntityBatchFromEntityChunkDataShared : IJob
        {
            [ReadOnly] public NativeArray<EntityInChunk> EntityChunkData;
            public NativeList<EntityBatchInChunk> EntityBatchList;

            public ComponentOperation Operation;
            public ComponentType ComponentType;
            [NativeDisableUnsafePtrRestriction] public int* FoundError;

            void TryAddBatch(EntityBatchInChunk entityBatch)
            {
                // Deleted Entity (not an error)
                if (entityBatch.Chunk == null)
                    return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS // dots-runtime tests do not have enabled. Tests rely on appropriate error.
#endif
                if (Operation == ComponentOperation.AddComponent)
                {
                    if (ComponentType.IsSharedComponent &&
                        (entityBatch.Chunk->Archetype->NumSharedComponents == kMaxSharedComponentCount))
                    {
                        *FoundError = 1;
                        return;
                    }
                }

                EntityBatchList.Add(entityBatch);
            }
            public void Execute()
            {
                *FoundError = 0;

                var entityIndex = 0;
                var entityBatch = new EntityBatchInChunk
                {
                    Chunk = EntityChunkData[entityIndex].Chunk,
                    StartIndex = EntityChunkData[entityIndex].IndexInChunk,
                    Count = 1
                };
                entityIndex++;
                while (entityIndex < EntityChunkData.Length)
                {
                    // Skip this entity if it's a duplicate.  Checking previous entityIndex is sufficient
                    // since arrays are sorted.
                    if (EntityChunkData[entityIndex].Equals(EntityChunkData[entityIndex - 1]))
                    {
                        entityIndex++;
                        continue;
                    }

                    var chunk = EntityChunkData[entityIndex].Chunk;
                    var indexInChunk = EntityChunkData[entityIndex].IndexInChunk;
                    var chunkBreak = (chunk != entityBatch.Chunk);
                    var indexBreak = (indexInChunk != (entityBatch.StartIndex + entityBatch.Count));
                    var runBreak = chunkBreak || indexBreak;
                    if (runBreak)
                    {
                        TryAddBatch(entityBatch);

                        entityBatch = new EntityBatchInChunk
                        {
                            Chunk = chunk,
                            StartIndex = indexInChunk,
                            Count = 1
                        };
                    }
                    else
                    {
                        entityBatch = new EntityBatchInChunk
                        {
                            Chunk = entityBatch.Chunk,
                            StartIndex = entityBatch.StartIndex,
                            Count = entityBatch.Count + 1
                        };
                    }
                    entityIndex++;
                }

                TryAddBatch(entityBatch);
            }
        }

        public bool CreateEntityBatchListForAddComponent(NativeArray<Entity> entities, ComponentType componentType,
            out NativeList<EntityBatchInChunk> entityBatchList)
        {
            return CreateEntityBatchList(entities, ComponentOperation.AddComponent, componentType, out entityBatchList);
        }

        public bool CreateEntityBatchListForRemoveComponent(NativeArray<Entity> entities, ComponentType componentType,
            out NativeList<EntityBatchInChunk> entityBatchList)
        {
            return CreateEntityBatchList(entities, ComponentOperation.RemoveComponent, componentType, out entityBatchList);
        }

        public bool CreateEntityBatchList(NativeArray<Entity> entities, ComponentOperation componentOperation, ComponentType componentType, out NativeList<EntityBatchInChunk> entityBatchList)
        {
            if (entities.Length == 0)
            {
                entityBatchList = default;
                return false;
            }

            var entityChunkData = new NativeArray<EntityInChunk>(entities.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var gatherEntityChunkDataForEntitiesJobHandle = GatherEntityInChunkForEntitiesJob(entities, entityChunkData);
            var entityChunkDataSortJobHandle = entityChunkData.SortJob(gatherEntityChunkDataForEntitiesJobHandle);

            entityBatchList = new NativeList<EntityBatchInChunk>(Allocator.Persistent);
            int foundError = 0;

            var entityBatchFromEntityInChunksSharedJob = new EntityBatchFromEntityChunkDataShared
            {
                EntityChunkData = entityChunkData,
                EntityBatchList = entityBatchList,
                FoundError = &foundError,
                Operation = componentOperation,
                ComponentType = componentType
            };
            var entityBatchFromEntityInChunksSharedJobHandle = entityBatchFromEntityInChunksSharedJob.Schedule(entityChunkDataSortJobHandle);
            entityBatchFromEntityInChunksSharedJobHandle.Complete();

            entityChunkData.Dispose();
            if (foundError != 0)
            {
                entityBatchList.Dispose();
                entityBatchList = default;
                return false;
            }

            return true;
        }

        [BurstCompile]
        struct GatherEntityInChunkForEntities : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Entities;

            [ReadOnly][NativeDisableUnsafePtrRestriction]
            public EntityInChunk* globalEntityInChunk;

            public NativeArray<EntityInChunk> EntityChunkData;

            public void Execute(int index)
            {
                var entity = Entities[index];
                EntityChunkData[index] = new EntityInChunk
                {
                    Chunk = globalEntityInChunk[entity.Index].Chunk,
                    IndexInChunk = globalEntityInChunk[entity.Index].IndexInChunk
                };
            }
        }

        JobHandle GatherEntityInChunkForEntitiesJob(NativeArray<Entity> entities,
            NativeArray<EntityInChunk> entityChunkData, JobHandle inputDeps = new JobHandle())
        {
            var gatherEntityInChunkForEntitiesJob = new GatherEntityInChunkForEntities
            {
                Entities = entities,
                globalEntityInChunk = m_EntityInChunkByEntity,
                EntityChunkData = entityChunkData
            };
            var gatherEntityInChunkForEntitiesJobHandle =
                gatherEntityInChunkForEntitiesJob.Schedule(entities.Length, 32, inputDeps);
            return gatherEntityInChunkForEntitiesJobHandle;
        }

        public ulong AssignSequenceNumber(Chunk* chunk)
        {
            var sequenceNumber = m_NextChunkSequenceNumber;
            m_NextChunkSequenceNumber++;
            return sequenceNumber;
        }

        public Chunk* AllocateChunk()
        {
            Chunk* newChunk;
            // Try empty chunk pool
            if (m_EmptyChunks.Length == 0)
            {
                // Allocate new chunk
                newChunk = Chunk.MallocChunk(Allocator.Persistent);

                if (useMemoryInitPattern != 0)
                {
                    UnsafeUtility.MemSet(newChunk, memoryInitPattern, Chunk.kChunkSize);
                }
            }
            else
            {
                Assert.IsTrue(m_EmptyChunks.Length > 0);
                var lastIdx = m_EmptyChunks.Length - 1;
                newChunk = m_EmptyChunks.Ptr[lastIdx];
                m_EmptyChunks.RemoveAtSwapBack(lastIdx);
                m_EmptyChunks.TrimExcess();
            }

            return newChunk;
        }

        public void FreeChunk(Chunk* chunk)
        {
            if (m_EmptyChunks.Length == kMaximumEmptyChunksInPool)
            {
                UnsafeUtility.Free(chunk, Allocator.Persistent);
            }
            else
            {
                if (useMemoryInitPattern != 0)
                {
                    UnsafeUtility.MemSet(chunk, memoryInitPattern, Chunk.kChunkSize);
                }
                m_EmptyChunks.Add(chunk);
                chunk->Count = 0;
            }
        }

        public Archetype* GetExistingArchetype(ComponentTypeInArchetype* typesSorted, int count)
        {
            return m_TypeLookup.TryGet(typesSorted, count);
        }

        void ChunkAllocate<T>(void* pointer, int count = 1) where T : struct
        {
            void** pointerToPointer = (void**)pointer;
            *pointerToPointer =
                m_ArchetypeChunkAllocator.Allocate(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>());
        }

        internal static int GetComponentArraySize(int componentSize, int entityCount) => CollectionHelper.Align(componentSize * entityCount, CollectionHelper.CacheLineSize);

        static int CalculateSpaceRequirement(int* componentSizes, int componentCount, int entityCount)
        {
            int size = 0;
            for (int i = 0; i < componentCount; ++i)
                size += GetComponentArraySize(componentSizes[i], entityCount);
            return size;
        }

        static int CalculateChunkCapacity(int bufferSize, int* componentSizes, int count)
        {
            int totalSize = 0;
            for (int i = 0; i < count; ++i)
                totalSize += componentSizes[i];

            int capacity = bufferSize / totalSize;
            while (CalculateSpaceRequirement(componentSizes, count, capacity) > bufferSize)
                --capacity;
            return capacity;
        }

        internal Archetype* CreateArchetype(ComponentTypeInArchetype* types, int count)
        {
            AssertArchetypeComponents(types, count);

            // Compute how many IComponentData types store Entities and need to be patched.
            // Types can have more than one entity, which means that this count is not necessarily
            // the same as the type count.
            var scalarEntityPatchCount = 0;
            var bufferEntityPatchCount = 0;
            var managedEntityPatchCount = 0;
            var numManagedArrays = 0;
            var numSharedComponents = 0;
            for (var i = 0; i < count; ++i)
            {
                var ct = GetTypeInfo(types[i].TypeIndex);

                if (ct.Category == TypeManager.TypeCategory.ISharedComponentData)
                {
                    ++numSharedComponents;
                }
                else if (TypeManager.IsManagedComponent(types[i].TypeIndex))
                {
                    ++numManagedArrays;
                    ++managedEntityPatchCount;
                }
                else
                {
                    if (!ct.HasEntities)
                        continue;

                    if (ct.BufferCapacity >= 0)
                        bufferEntityPatchCount += ct.EntityOffsetCount;
                    else if (ct.SizeInChunk > 0)
                        scalarEntityPatchCount += ct.EntityOffsetCount;
                }
            }

            Archetype* dstArchetype = null;
            ChunkAllocate<Archetype>(&dstArchetype);
            ChunkAllocate<ComponentTypeInArchetype>(&dstArchetype->Types, count);
            ChunkAllocate<int>(&dstArchetype->Offsets, count);
            ChunkAllocate<int>(&dstArchetype->SizeOfs, count);
            ChunkAllocate<int>(&dstArchetype->BufferCapacities, count);
            ChunkAllocate<int>(&dstArchetype->TypeMemoryOrder, count);
            ChunkAllocate<EntityRemapUtility.EntityPatchInfo>(&dstArchetype->ScalarEntityPatches, scalarEntityPatchCount);
            ChunkAllocate<EntityRemapUtility.BufferEntityPatchInfo>(&dstArchetype->BufferEntityPatches, bufferEntityPatchCount);
            ChunkAllocate<EntityRemapUtility.ManagedEntityPatchInfo>(&dstArchetype->ManagedEntityPatches, managedEntityPatchCount);

            dstArchetype->TypesCount = count;
            UnsafeUtility.MemCpy(dstArchetype->Types, types, sizeof(ComponentTypeInArchetype) * count);
            dstArchetype->EntityCount = 0;
            dstArchetype->Chunks = new ArchetypeChunkData(count, numSharedComponents);
            dstArchetype->ChunksWithEmptySlots = new UnsafeChunkPtrList(0, Allocator.Persistent);
            dstArchetype->InstantiableArchetype = null;
            dstArchetype->MetaChunkArchetype = null;
            dstArchetype->SystemStateResidueArchetype = null;

            dstArchetype->Flags = 0;

            {
                short i = (short)count;
                do dstArchetype->FirstChunkComponent = i;
                while (types[--i].IsChunkComponent);
                i++;
                do dstArchetype->FirstSharedComponent = i;
                while (types[--i].IsSharedComponent);
                i++;
                do dstArchetype->FirstTagComponent = i;
                while (types[--i].IsZeroSized);
                i++;
                do dstArchetype->FirstManagedComponent = i;
                while (types[--i].IsManagedComponent);
                i++;
                do dstArchetype->FirstBufferComponent = i;
                while (types[--i].IsBuffer);
            }

            for (var i = 0; i < count; ++i)
            {
                var typeIndex = types[i].TypeIndex; 
                var typeInfo = GetTypeInfo(typeIndex);
                if (typeIndex == m_DisabledType)
                    dstArchetype->Flags |= ArchetypeFlags.Disabled;
                if (typeIndex == m_PrefabType)
                    dstArchetype->Flags |= ArchetypeFlags.Prefab;
                if (typeIndex == m_ChunkHeaderType)
                    dstArchetype->Flags |= ArchetypeFlags.HasChunkHeader;
                if (typeInfo.BlobAssetRefOffsetCount > 0)
                    dstArchetype->Flags |= ArchetypeFlags.ContainsBlobAssetRefs;
                if(!types[i].IsChunkComponent && types[i].IsManagedComponent && typeInfo.Category == TypeManager.TypeCategory.Class)
                    dstArchetype->Flags |= ArchetypeFlags.HasHybridComponents;
            }

            if(dstArchetype->NumManagedComponents > 0)
                dstArchetype->Flags |= ArchetypeFlags.HasManagedComponents;

            if(dstArchetype->NumBufferComponents > 0)
                dstArchetype->Flags |= ArchetypeFlags.HasBufferComponents;
            
            
            var chunkDataSize = Chunk.GetChunkBufferSize();

            dstArchetype->ScalarEntityPatchCount = scalarEntityPatchCount;
            dstArchetype->BufferEntityPatchCount = bufferEntityPatchCount;
            dstArchetype->ManagedEntityPatchCount = managedEntityPatchCount;

            int maxCapacity = TypeManager.MaximumChunkCapacity;
            for (var i = 0; i < count; ++i)
            {
                var cType = GetTypeInfo(types[i].TypeIndex);
                if (i < dstArchetype->NonZeroSizedTypesCount)
                {
                    dstArchetype->SizeOfs[i] = cType.SizeInChunk;
                    dstArchetype->BufferCapacities[i] = cType.BufferCapacity;
                }
                else
                {
                    dstArchetype->SizeOfs[i] = 0;
                    dstArchetype->BufferCapacities[i] = 0;
                }
                maxCapacity = math.min(maxCapacity, cType.MaximumChunkCapacity);
            }

            dstArchetype->ChunkCapacity = math.min(maxCapacity, CalculateChunkCapacity(chunkDataSize, dstArchetype->SizeOfs, dstArchetype->NonZeroSizedTypesCount));

            dstArchetype->InstanceSize = 0;
            dstArchetype->InstanceSizeWithOverhead = 0;
            for (var i = 0; i < dstArchetype->NonZeroSizedTypesCount; ++i)
            {
                dstArchetype->InstanceSize += dstArchetype->SizeOfs[i];
                dstArchetype->InstanceSizeWithOverhead += GetComponentArraySize(dstArchetype->SizeOfs[i], 1);
            }

            Assert.IsTrue(dstArchetype->ChunkCapacity > 0);
            Assert.IsTrue(Chunk.kMaximumEntitiesPerChunk >= dstArchetype->ChunkCapacity);

            // For serialization a stable ordering of the components in the
            // chunk is desired. The type index is not stable, since it depends
            // on the order in which types are added to the TypeManager.
            // A permutation of the types ordered by a TypeManager-generated
            // memory ordering is used instead.
            var memoryOrderings = stackalloc UInt64[count];
            var typeFlags = stackalloc int[count];

            for (int i = 0; i < count; ++i)
            {
                int typeIndex = types[i].TypeIndex;
                memoryOrderings[i] = GetTypeInfo(typeIndex).MemoryOrdering;
                typeFlags[i] = typeIndex & ~TypeManager.ClearFlagsMask;
            }

            // Having memory order depend on type flags has the advantage that
            // TypeMemoryOrder is stable within component types
            // i.e. if Types[X] is a buffer component then Types[TypeMemoryOrder[X]] is also a buffer component
            // this simplifies iterating types in memory order (mainly serialization code)
            bool MemoryOrderCompare(int lhs, int rhs)
            {
                if(typeFlags[lhs] == typeFlags[rhs])
                    return memoryOrderings[lhs] < memoryOrderings[rhs];
                return typeFlags[lhs] < typeFlags[rhs];
            }

            for (int i = 0; i < count; ++i)
            {
                int index = i;
                while (index > 1 && MemoryOrderCompare(i, dstArchetype->TypeMemoryOrder[index - 1]))
                {
                    dstArchetype->TypeMemoryOrder[index] = dstArchetype->TypeMemoryOrder[index - 1];
                    --index;
                }
                dstArchetype->TypeMemoryOrder[index] = i;
            }

            var usedBytes = 0;
            for (var i = 0; i < count; ++i)
            {
                var index = dstArchetype->TypeMemoryOrder[i];
                var sizeOf = dstArchetype->SizeOfs[index];

                // align usedBytes upwards (eating into alignExtraSpace) so that
                // this component actually starts at its required alignment.
                // Assumption is that the start of the entire data segment is at the
                // maximum possible alignment.
                dstArchetype->Offsets[index] = usedBytes;
                usedBytes += GetComponentArraySize(sizeOf, dstArchetype->ChunkCapacity);
            }

            // Fill in arrays of scalar, buffer and managed entity patches
            var scalarPatchInfo = dstArchetype->ScalarEntityPatches;
            var bufferPatchInfo = dstArchetype->BufferEntityPatches;
            var managedPatchInfo = dstArchetype->ManagedEntityPatches;
            for (var i = 0; i != count; i++)
            {
                var ct = GetTypeInfo(types[i].TypeIndex);
                var offsets = GetEntityOffsets(ct);
                var offsetCount = ct.EntityOffsetCount;

                if (ct.BufferCapacity >= 0)
                {
                    bufferPatchInfo = EntityRemapUtility.AppendBufferEntityPatches(bufferPatchInfo, offsets, offsetCount, dstArchetype->Offsets[i], dstArchetype->SizeOfs[i], ct.ElementSize);
                }
                else if (TypeManager.IsManagedComponent(ct.TypeIndex))
                {
                    var index = dstArchetype->TypeMemoryOrder[i];
                    managedPatchInfo = EntityRemapUtility.AppendManagedEntityPatches(managedPatchInfo, ComponentType.FromTypeIndex(ct.TypeIndex));
                }
                else if (ct.SizeInChunk > 0)
                {
                    scalarPatchInfo = EntityRemapUtility.AppendEntityPatches(scalarPatchInfo, offsets, offsetCount, dstArchetype->Offsets[i], dstArchetype->SizeOfs[i]);
                }
            }
            Assert.AreEqual(scalarPatchInfo - dstArchetype->ScalarEntityPatches, scalarEntityPatchCount);

            dstArchetype->ScalarEntityPatchCount = scalarEntityPatchCount;
            dstArchetype->BufferEntityPatchCount = bufferEntityPatchCount;
            dstArchetype->ManagedEntityPatchCount = managedEntityPatchCount;
            UnsafeUtility.MemClear(dstArchetype->QueryMaskArray, sizeof(byte) * 128);

            // Update the list of all created archetypes
            m_Archetypes.Add(dstArchetype);

            dstArchetype->FreeChunksBySharedComponents = new ChunkListMap();
            dstArchetype->FreeChunksBySharedComponents.Init(16);

            m_TypeLookup.Add(dstArchetype);

            if(ArchetypeSystemStateCleanupComplete(dstArchetype))
                dstArchetype->Flags |= ArchetypeFlags.SystemStateCleanupComplete;
            if(ArchetypeSystemStateCleanupNeeded(dstArchetype))
                dstArchetype->Flags |= ArchetypeFlags.SystemStateCleanupNeeded;

            return dstArchetype;
        }

        private bool ArchetypeSystemStateCleanupComplete(Archetype* archetype)
        {
            if (archetype->TypesCount == 2 && archetype->Types[1].TypeIndex == m_CleanupEntityType) return true;
            return false;
        }

        private bool ArchetypeSystemStateCleanupNeeded(Archetype* archetype)
        {
            for (var t = 1; t < archetype->TypesCount; ++t)
            {
                var type = archetype->Types[t];
                if (type.IsSystemStateComponent)
                {
                    return true;
                }
            }

            return false;
        }

        public int CountEntities()
        {
            int entityCount = 0;
            for (var i = 0; i < m_Archetypes.Length; ++i)
            {
                var archetype = m_Archetypes.Ptr[i];
                entityCount += archetype->EntityCount;
            }

            return entityCount;
        }

        public struct ArchetypeChanges
        {
            public int StartIndex;
            public uint ArchetypeTrackingVersion;
        }

        public ArchetypeChanges BeginArchetypeChangeTracking()
        {
            m_ArchetypeTrackingVersion++;
            return new ArchetypeChanges
            {
                StartIndex = m_Archetypes.Length,
                ArchetypeTrackingVersion = m_ArchetypeTrackingVersion
            };
        }

        public void EndArchetypeChangeTracking(ArchetypeChanges changes, EntityQueryManager* queries)
        {
            Assert.AreEqual(m_ArchetypeTrackingVersion, changes.ArchetypeTrackingVersion);
            if (m_Archetypes.Length - changes.StartIndex == 0)
                return;
            
            var changeList = new UnsafeArchetypePtrList(m_Archetypes.Ptr + changes.StartIndex, m_Archetypes.Length - changes.StartIndex);
            queries->AddAdditionalArchetypes(changeList);
        }

        public int ManagedComponentIndexUsedCount => m_ManagedComponentIndex - 1 - m_ManagedComponentFreeIndex.Length / 4;
        public int ManagedComponentFreeCount => m_ManagedComponentIndexCapacity - m_ManagedComponentIndex + m_ManagedComponentFreeIndex.Length / 4;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertNoQueuedManagedDeferredCommands()
        {
            var isEmpty = ManagedChangesTracker.Empty;
            ManagedChangesTracker.Reset();
            Assert.IsTrue(isEmpty);
        } 
        
        public void DeallocateManagedComponents(Chunk* chunk, int indexInChunk, int batchCount)
        {
            var archetype = chunk->Archetype;
            if (archetype->NumManagedComponents == 0)
                return;

            var firstManagedComponent = archetype->FirstManagedComponent;
            var numManagedComponents= archetype->NumManagedComponents;
            var freeCommandHandle = ManagedChangesTracker.BeginFreeManagedComponentCommand();
            for (int i = 0; i < numManagedComponents; ++i)
            {
                int type = i + firstManagedComponent;
                var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, 0, type);
                for(int ei=0; ei<batchCount; ++ei)
                {
                    var managedComponentIndex = a[ei + indexInChunk];
                    if(managedComponentIndex == 0)
                        continue;

                    FreeManagedComponentIndex(managedComponentIndex);
                    ManagedChangesTracker.AddToFreeManagedComponentCommand(managedComponentIndex);
                }
            }
            ManagedChangesTracker.EndDeallocateManagedComponentCommand(freeCommandHandle);
        }

        public int GrowManagedComponentCapacity(int count)
        {
            return m_ManagedComponentIndexCapacity += math.max(m_ManagedComponentIndexCapacity / 2, count);
        }

        public void ReserveManagedComponentIndices(int count)
        {
            int freeCount = ManagedComponentFreeCount;
            if(freeCount >= count)
                return;
            int newCapacity = GrowManagedComponentCapacity(count-freeCount);
            ManagedChangesTracker.SetManagedComponentCapacity(newCapacity);
        }

        public int AllocateManagedComponentIndex()
        {
            if (!m_ManagedComponentFreeIndex.IsEmpty)
                return m_ManagedComponentFreeIndex.Pop<int>();

            if (m_ManagedComponentIndex == m_ManagedComponentIndexCapacity)
            {
                m_ManagedComponentIndexCapacity += m_ManagedComponentIndexCapacity / 2;
                ManagedChangesTracker.SetManagedComponentCapacity(m_ManagedComponentIndexCapacity);
            }
            return m_ManagedComponentIndex++;
        }

        public void AllocateManagedComponentIndices(int* dst, int count)
        {
            int freeCount = m_ManagedComponentFreeIndex.Length / sizeof(int);
            if (freeCount >= count)
            {
                var newFreeCount = freeCount - count;
                UnsafeUtility.MemCpy(dst,(int*)m_ManagedComponentFreeIndex.Ptr + newFreeCount, count * sizeof(int));
                m_ManagedComponentFreeIndex.Length = newFreeCount * sizeof(int);
            }
            else
            {
                UnsafeUtility.MemCpy(dst,(int*)m_ManagedComponentFreeIndex.Ptr, freeCount * sizeof(int));
                m_ManagedComponentFreeIndex.Length = 0;
                ReserveManagedComponentIndices(count - freeCount);
                for (int i = freeCount; i < count; ++i)
                    dst[i] = m_ManagedComponentIndex++;
            }
        }

        public void FreeManagedComponentIndex(int index)
        {
            Assert.AreNotEqual(0, index);
            m_ManagedComponentFreeIndex.Add(index);
        }
    }
}
