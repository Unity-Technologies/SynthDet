using System;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    [Flags]
    internal enum ArchetypeFlags : ushort
    {
        SystemStateCleanupComplete = 1,
        SystemStateCleanupNeeded = 2,
        Disabled = 4,
        Prefab = 8,
        HasChunkHeader = 16,
        ContainsBlobAssetRefs = 32,
        HasHybridComponents = 64,
        HasBufferComponents = 128,
        HasManagedComponents = 256
    }
    
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct Archetype
    {
        public ArchetypeChunkData Chunks;
        public UnsafeChunkPtrList ChunksWithEmptySlots;

        public ChunkListMap FreeChunksBySharedComponents;
        public ComponentTypeInArchetype* Types;

        public int EntityCount;
        public int ChunkCapacity;
        
        public int TypesCount;
        public int InstanceSize;
        public int InstanceSizeWithOverhead;
        public int ManagedEntityPatchCount;
        public int ScalarEntityPatchCount;
        public int BufferEntityPatchCount;
        
        // Index matches archetype types
        public int* Offsets;
        public int* SizeOfs;
        public int* BufferCapacities;

        // TypesCount indices into Types/Offsets/SizeOfs in the order that the
        // components are laid out in memory.
        public int* TypeMemoryOrder;

        // Order of components in the types array is always:
        // Entity, native component data, buffer components, managed component data, tag component, shared components, chunk components
        public short FirstBufferComponent;
        public short FirstManagedComponent;
        public short FirstTagComponent;
        public short FirstSharedComponent;
        public short FirstChunkComponent;

        public ArchetypeFlags Flags;

        public Archetype* InstantiableArchetype;
        public Archetype* SystemStateResidueArchetype;
        public Archetype* MetaChunkArchetype;

        public EntityRemapUtility.EntityPatchInfo* ScalarEntityPatches;
        public EntityRemapUtility.BufferEntityPatchInfo* BufferEntityPatches;
        public EntityRemapUtility.ManagedEntityPatchInfo* ManagedEntityPatches;
		
        public fixed byte QueryMaskArray[128];

        public bool SystemStateCleanupComplete => (Flags & ArchetypeFlags.SystemStateCleanupComplete) != 0;
        public bool SystemStateCleanupNeeded => (Flags & ArchetypeFlags.SystemStateCleanupNeeded) != 0;
        public bool Disabled => (Flags & ArchetypeFlags.Disabled) != 0;
        public bool Prefab => (Flags & ArchetypeFlags.Prefab) != 0;
        public bool HasChunkHeader => (Flags & ArchetypeFlags.HasChunkHeader) != 0;
        public bool ContainsBlobAssetRefs => (Flags & ArchetypeFlags.ContainsBlobAssetRefs) != 0;
        public bool HasHybridComponents => (Flags & ArchetypeFlags.HasHybridComponents) != 0;

        public int NumNativeComponentData => FirstBufferComponent - 1;
        public int NumBufferComponents => FirstManagedComponent - FirstBufferComponent;
        public int NumManagedComponents => FirstTagComponent - FirstManagedComponent;
        public int NumTagComponents => FirstSharedComponent - FirstTagComponent;
        public int NumSharedComponents => FirstChunkComponent - FirstSharedComponent;
        public int NumChunkComponents => TypesCount - FirstChunkComponent;
        public int NonZeroSizedTypesCount => FirstTagComponent;

        // These help when iterating specific component types
        // for(int iType=archetype->FirstBufferComponent; iType<archetype->BufferComponentsEnd;++iType) {...}
        public int NativeComponentsEnd => FirstBufferComponent;
        public int BufferComponentsEnd => FirstManagedComponent;
        public int ManagedComponentsEnd => FirstTagComponent;
        public int TagComponentsEnd => FirstSharedComponent;
        public int SharedComponentsEnd => FirstChunkComponent;
        public int ChunkComponentsEnd => TypesCount;
        
        public bool HasChunkComponents => FirstChunkComponent != TypesCount;

        public bool IsManaged(int typeIndexInArchetype) => Types[typeIndexInArchetype].IsManagedComponent;

        public override string ToString()
        {
            var info = "";
            for (var i = 0; i < TypesCount; i++)
            {
                var componentTypeInArchetype = Types[i];
                info += $"  - {componentTypeInArchetype}";
            }

            return info;
        }

        public void AddToChunkList(Chunk *chunk, SharedComponentValues sharedComponentIndices, uint changeVersion)
        {
            chunk->ListIndex = Chunks.Count;
            if (Chunks.Count == Chunks.Capacity)
            {
                int newCapacity = Chunks.Capacity == 0 ? 1 : Chunks.Capacity * 2;
                if (Chunks.data <= sharedComponentIndices.firstIndex &&
                    sharedComponentIndices.firstIndex < Chunks.data + Chunks.Count)
                {
                    int sourceChunk = (int)(sharedComponentIndices.firstIndex - Chunks.data);
                    // The shared component indices we are inserting belong to the same archetype so they need to be adjusted after reallocation
                    Chunks.Grow(newCapacity);
                    sharedComponentIndices = Chunks.GetSharedComponentValues(sourceChunk);
                }
                else
                    Chunks.Grow(newCapacity);
            }

            Chunks.Add(chunk, sharedComponentIndices, changeVersion);
        }
        
        

        public void RemoveFromChunkList(Chunk *chunk)
        {
            Chunks.RemoveAtSwapBack(chunk->ListIndex);
            var chunkThatMoved = Chunks.p[chunk->ListIndex];
            chunkThatMoved->ListIndex = chunk->ListIndex;
        }

        public void AddToChunkListWithEmptySlots(Chunk *chunk)
        {
            chunk->ListWithEmptySlotsIndex = ChunksWithEmptySlots.Length;
            ChunksWithEmptySlots.Add(chunk);
        }

        public void RemoveFromChunkListWithEmptySlots(Chunk *chunk)
        {
            var index = chunk->ListWithEmptySlotsIndex;
            Assert.IsTrue(index >= 0 && index < ChunksWithEmptySlots.Length);
            Assert.IsTrue(ChunksWithEmptySlots.Ptr[index] == chunk);
            ChunksWithEmptySlots.RemoveAtSwapBack(index);

            if (chunk->ListWithEmptySlotsIndex < ChunksWithEmptySlots.Length)
            {
                var chunkThatMoved = ChunksWithEmptySlots.Ptr[chunk->ListWithEmptySlotsIndex];
                chunkThatMoved->ListWithEmptySlotsIndex = chunk->ListWithEmptySlotsIndex;
            }
        }

        /// <summary>
        /// Remove chunk from archetype tracking of chunks with available slots.
        /// - Does not check if chunk has space.
        /// - Does not check if chunk is locked.
        /// </summary>
        /// <param name="chunk"></param>
        internal void EmptySlotTrackingRemoveChunk(Chunk* chunk)
        {
            if (NumSharedComponents == 0)
                RemoveFromChunkListWithEmptySlots(chunk);
            else
                FreeChunksBySharedComponents.Remove(chunk);
        }

        /// <summary>
        /// Add chunk to archetype tracking of chunks with available slots.
        /// - Does not check if chunk has space.
        /// - Does not check if chunk is locked.
        /// </summary>
        /// <param name="chunk"></param>
        internal void EmptySlotTrackingAddChunk(Chunk* chunk)
        {
            if (NumSharedComponents == 0)
                AddToChunkListWithEmptySlots(chunk);
            else
                FreeChunksBySharedComponents.Add(chunk);
        }

        internal Chunk* GetExistingChunkWithEmptySlots(SharedComponentValues sharedComponentValues)
        {
            if (NumSharedComponents == 0)
            {
                if (ChunksWithEmptySlots.Length != 0)
                {
                    var chunk = ChunksWithEmptySlots.Ptr[0];
                    Assert.AreNotEqual(chunk->Count, chunk->Capacity);
                    return chunk;
                }
            }
            else
            {
                var chunk = FreeChunksBySharedComponents.TryGet(sharedComponentValues, NumSharedComponents);
                if (chunk != null)
                {
                    return chunk;
                }
            }

            return null;
        }

        internal bool CompareMask(EntityQueryMask mask)
        {
            return (byte)(QueryMaskArray[mask.Index] & mask.Mask) == mask.Mask;
        }

        internal void SetMask(EntityQueryMask mask)
        {
            QueryMaskArray[mask.Index] |= mask.Mask;
        }
    }
}
