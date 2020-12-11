using System;
using System.Diagnostics;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    internal static unsafe class ChunkDataUtility
    {
        public static int GetIndexInTypeArray(Archetype* archetype, int typeIndex)
        {
            var types = archetype->Types;
            var typeCount = archetype->TypesCount;
            for (var i = 0; i != typeCount; i++)
                if (typeIndex == types[i].TypeIndex)
                    return i;

            return -1;
        }
        public static int GetTypeIndexFromType(Archetype* archetype, Type componentType)
        {
            var types = archetype->Types;
            var typeCount = archetype->TypesCount;
            for (var i = 0; i != typeCount; i++)
                if (componentType.IsAssignableFrom(TypeManager.GetType(types[i].TypeIndex)))
                    return types[i].TypeIndex;

            return -1;
        }


        public static void GetIndexInTypeArray(Archetype* archetype, int typeIndex, ref int typeLookupCache)
        {
            var types = archetype->Types;
            var typeCount = archetype->TypesCount;

            if (typeLookupCache >= 0 && typeLookupCache < typeCount && types[typeLookupCache].TypeIndex == typeIndex)
                return;

            for (var i = 0; i != typeCount; i++)
            {
                if (typeIndex != types[i].TypeIndex)
                    continue;

                typeLookupCache = i;
                return;
            }

            typeLookupCache = -1;
        }

        public static int GetSizeInChunk(Chunk* chunk, int typeIndex, ref int typeLookupCache)
        {
            var archetype = chunk->Archetype;
            GetIndexInTypeArray(archetype, typeIndex, ref typeLookupCache);
            var indexInTypeArray = typeLookupCache;

            var sizeOf = archetype->SizeOfs[indexInTypeArray];

            return sizeOf;
        }

        public static byte* GetComponentDataWithTypeRO(Chunk* chunk, int index, int typeIndex, ref int typeLookupCache)
        {
            var archetype = chunk->Archetype;
            GetIndexInTypeArray(archetype, typeIndex, ref typeLookupCache);
            var indexInTypeArray = typeLookupCache;

            var offset = archetype->Offsets[indexInTypeArray];
            var sizeOf = archetype->SizeOfs[indexInTypeArray];

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataWithTypeRW(Chunk* chunk, int index, int typeIndex, uint globalSystemVersion,
            ref int typeLookupCache)
        {
            var archetype = chunk->Archetype;
            GetIndexInTypeArray(archetype, typeIndex, ref typeLookupCache);
            var indexInTypeArray = typeLookupCache;

            var offset = archetype->Offsets[indexInTypeArray];
            var sizeOf = archetype->SizeOfs[indexInTypeArray];

            chunk->SetChangeVersion(indexInTypeArray, globalSystemVersion);

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataWithTypeRO(Chunk* chunk, int index, int typeIndex)
        {
            var indexInTypeArray = GetIndexInTypeArray(chunk->Archetype, typeIndex);

            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataWithTypeRW(Chunk* chunk, int index, int typeIndex, uint globalSystemVersion)
        {
            var indexInTypeArray = GetIndexInTypeArray(chunk->Archetype, typeIndex);

            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            chunk->SetChangeVersion(indexInTypeArray, globalSystemVersion);

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataRO(Chunk* chunk, int index, int indexInTypeArray)
        {
            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataRW(Chunk* chunk, int index, int indexInTypeArray, uint globalSystemVersion)
        {
            var offset = chunk->Archetype->Offsets[indexInTypeArray];
            var sizeOf = chunk->Archetype->SizeOfs[indexInTypeArray];

            chunk->SetChangeVersion(indexInTypeArray, globalSystemVersion);

            return chunk->Buffer + (offset + sizeOf * index);
        }

        public static void Copy(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count)
        {
            Assert.IsTrue(srcChunk->Archetype == dstChunk->Archetype);

            var arch = srcChunk->Archetype;
            var srcBuffer = srcChunk->Buffer;
            var dstBuffer = dstChunk->Buffer;
            var offsets = arch->Offsets;
            var sizeOfs = arch->SizeOfs;
            var typesCount = arch->TypesCount;

            for (var t = 0; t < typesCount; t++)
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var src = srcBuffer + (offset + sizeOf * srcIndex);
                var dst = dstBuffer + (offset + sizeOf * dstIndex);

                UnsafeUtility.MemCpy(dst, src, sizeOf * count);
            }
        }

        public static void SwapComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count, uint srcGlobalSystemVersion, uint dstGlobalSystemVersion)
        {
            var srcArch = srcChunk->Archetype;
            var typesCount = srcArch->TypesCount;


#if UNITY_ASSERTIONS
            // This function is used to swap data between different world so assert that the layout is identical if
            // the archetypes dont match
            var dstArch = dstChunk->Archetype;
            if (srcArch != dstArch)
            {
                Assert.AreEqual(typesCount, dstChunk->Archetype->TypesCount);
                for (int i = 0; i < typesCount; ++i)
                {
                    Assert.AreEqual(srcArch->Types[i], dstArch->Types[i]);
                    Assert.AreEqual(srcArch->Offsets[i], dstArch->Offsets[i]);
                    Assert.AreEqual(srcArch->SizeOfs[i], dstArch->SizeOfs[i]);
                }
            }
#endif

            var srcBuffer = srcChunk->Buffer;
            var dstBuffer = dstChunk->Buffer;
            var offsets = srcArch->Offsets;
            var sizeOfs = srcArch->SizeOfs;

            for (var t = 1; t < typesCount; t++) // Only swap component data, not Entity
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var src = srcBuffer + (offset + sizeOf * srcIndex);
                var dst = dstBuffer + (offset + sizeOf * dstIndex);
                Byte* buffer = stackalloc Byte[sizeOf * count];

                dstChunk->SetChangeVersion(t, dstGlobalSystemVersion);
                srcChunk->SetChangeVersion(t, srcGlobalSystemVersion);

                UnsafeUtility.MemCpy(buffer, src, sizeOf * count);
                UnsafeUtility.MemCpy(src, dst, sizeOf * count);
                UnsafeUtility.MemCpy(dst, buffer, sizeOf * count);
            }
        }

        public static void InitializeComponents(Chunk* dstChunk, int dstIndex, int count)
        {
            var arch = dstChunk->Archetype;

            var offsets = arch->Offsets;
            var sizeOfs = arch->SizeOfs;
            var bufferCapacities = arch->BufferCapacities;
            var dstBuffer = dstChunk->Buffer;
            var typesCount = arch->TypesCount;
            var types = arch->Types;

            for (var t = 1; t != typesCount; t++)
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var dst = dstBuffer + (offset + sizeOf * dstIndex);

                if (types[t].IsBuffer)
                {
                    for (var i = 0; i < count; ++i)
                    {
                        BufferHeader.Initialize((BufferHeader*)dst, bufferCapacities[t]);
                        dst += sizeOf;
                    }
                }
                else
                {
                    UnsafeUtility.MemClear(dst, sizeOf * count);
                }
            }
        }

        public static void ReplicateManagedComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstBaseIndex, int count, ref EntityComponentStore entityComponentStore)
        {
            var dstArchetype = dstChunk->Archetype;
            var srcArchetype = srcChunk->Archetype;
            var srcTypes= srcArchetype->Types;
            var dstTypes= dstArchetype->Types;
            var srcOffsets          = srcArchetype->Offsets;
            var dstOffsets          = dstArchetype->Offsets;
            int componentCount = dstArchetype->NumManagedComponents;            

            int nonNullManagedComponents = 0;
            int nonNullHybridComponents = 0;
            var componentIndices = stackalloc int[componentCount];
            var componentDstArrayStart = stackalloc IntPtr[componentCount];

            var firstDstManagedComponent = dstArchetype->FirstManagedComponent;
            var dstTypeIndex = firstDstManagedComponent; 
            var managedComponentsEnd = srcArchetype->ManagedComponentsEnd;
            var srcBaseAddr = srcChunk->Buffer + sizeof(int) * srcIndex;
            var dstBaseAddr = dstChunk->Buffer + sizeof(int) * dstBaseIndex;

            bool hasHybridComponents = dstArchetype->HasHybridComponents;

            for (var srcTypeIndex = srcArchetype->FirstManagedComponent; srcTypeIndex != managedComponentsEnd; srcTypeIndex++)
            {
                var srcType = srcTypes[srcTypeIndex];
                var dstType = dstTypes[dstTypeIndex];
                // Type does not exist in destination. Skip it.
                if (srcType.TypeIndex != dstType.TypeIndex)
                    continue;
                int srcManagedComponentIndex = *(int*)(srcBaseAddr + srcOffsets[srcTypeIndex]);
                var dstArrayStart = dstBaseAddr + dstOffsets[dstTypeIndex];
                
                if (srcManagedComponentIndex == 0)
                {
                    UnsafeUtility.MemClear(dstArrayStart, sizeof(int)*count);
                }
                else
                {
                    if (hasHybridComponents && TypeManager.GetTypeInfo(srcType.TypeIndex).Category == TypeManager.TypeCategory.Class)
                    {
                        //Hybrid component, put at end of array
                        var index = componentCount - nonNullHybridComponents - 1;
                        componentIndices[index] = srcManagedComponentIndex;
                        componentDstArrayStart[index] = (IntPtr)dstArrayStart;
                        ++nonNullHybridComponents;
                    }
                    else
                    {
                        componentIndices[nonNullManagedComponents] = srcManagedComponentIndex;
                        componentDstArrayStart[nonNullManagedComponents] = (IntPtr)dstArrayStart;
                        ++nonNullManagedComponents;
                    }
                }

                dstTypeIndex++;
            }

            entityComponentStore.ReserveManagedComponentIndices(count * (nonNullManagedComponents + nonNullHybridComponents));
            entityComponentStore.ManagedChangesTracker.CloneManagedComponentBegin(componentIndices, nonNullManagedComponents, count);
            for (int c=0; c<nonNullManagedComponents; ++c)
            {
                var dst = (int*)(componentDstArrayStart[c]);
                entityComponentStore.AllocateManagedComponentIndices(dst, count);
                entityComponentStore.ManagedChangesTracker.CloneManagedComponentAddDstIndices(dst, count);
            }            
                
            if(hasHybridComponents)
            {
                var companionLinkIndexInTypeArray = GetIndexInTypeArray(dstArchetype, ManagedComponentStore.CompanionLinkTypeIndex);
                var companionLinkIndices = (companionLinkIndexInTypeArray == -1) ? null : (int*)(dstBaseAddr + dstOffsets[companionLinkIndexInTypeArray]);

                var dstEntities = (Entity*) dstChunk->Buffer + dstBaseIndex;
                entityComponentStore.ManagedChangesTracker.CloneHybridComponentBegin(
                    componentIndices + componentCount - nonNullHybridComponents, nonNullHybridComponents, dstEntities, count, companionLinkIndices);
                for (int c = componentCount - nonNullHybridComponents; c < componentCount; ++c)
                {
                    var dst = (int*) (componentDstArrayStart[c]);
                    entityComponentStore.AllocateManagedComponentIndices(dst, count);
                    entityComponentStore.ManagedChangesTracker.CloneHybridComponentAddDstIndices(dst, count);
                }
            }        
        }
        

        public static void ReplicateComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstBaseIndex, int count, ref EntityComponentStore entityComponentStore)
        {
            var srcArchetype        = srcChunk->Archetype;
            var srcBuffer           = srcChunk->Buffer;
            var dstBuffer           = dstChunk->Buffer;
            var dstArchetype        = dstChunk->Archetype;
            var srcOffsets          = srcArchetype->Offsets;
            var srcSizeOfs          = srcArchetype->SizeOfs;
            var srcBufferCapacities = srcArchetype->BufferCapacities;
            var srcTypes            = srcArchetype->Types;
            var dstTypes            = dstArchetype->Types;
            var dstOffsets          = dstArchetype->Offsets;
            var dstTypeIndex        = 1;

            var nativeComponentsEnd = srcArchetype->NativeComponentsEnd;
            for (var srcTypeIndex = 1; srcTypeIndex != nativeComponentsEnd; srcTypeIndex++)
            {
                var srcType = srcTypes[srcTypeIndex];
                var dstType = dstTypes[dstTypeIndex];
                // Type does not exist in destination. Skip it.
                if (srcType.TypeIndex != dstType.TypeIndex)
                    continue;
                var srcSizeOf = srcSizeOfs[srcTypeIndex];
                var src = srcBuffer + (srcOffsets[srcTypeIndex] + srcSizeOf * srcIndex);
                var dst = dstBuffer + (dstOffsets[dstTypeIndex] + srcSizeOf * dstBaseIndex);

                UnsafeUtility.MemCpyReplicate(dst, src, srcSizeOf, count);
                dstTypeIndex++;
            }

            dstTypeIndex = dstArchetype->FirstBufferComponent;
            var bufferComponentsEnd = srcArchetype->BufferComponentsEnd;
            for (var srcTypeIndex = srcArchetype->FirstBufferComponent; srcTypeIndex != bufferComponentsEnd; srcTypeIndex++)
            {
                var srcType = srcTypes[srcTypeIndex];
                var dstType = dstTypes[dstTypeIndex];
                // Type does not exist in destination. Skip it.
                if (srcType.TypeIndex != dstType.TypeIndex)
                    continue;
                var srcSizeOf = srcSizeOfs[srcTypeIndex];
                var src = srcBuffer + (srcOffsets[srcTypeIndex] + srcSizeOf * srcIndex);
                var dst = dstBuffer + (dstOffsets[dstTypeIndex] + srcSizeOf * dstBaseIndex);

                var srcBufferCapacity = srcBufferCapacities[srcTypeIndex];
                var alignment = 8; // TODO: Need a way to compute proper alignment for arbitrary non-generic types in TypeManager
                var elementSize = TypeManager.GetTypeInfo(srcType.TypeIndex).ElementSize;
                for (int i = 0; i < count; ++i)
                {
                    BufferHeader* srcHdr = (BufferHeader*) src;
                    BufferHeader* dstHdr = (BufferHeader*) dst;
                    BufferHeader.Initialize(dstHdr, srcBufferCapacity);
                    BufferHeader.Assign(dstHdr, BufferHeader.GetElementPointer(srcHdr), srcHdr->Length, elementSize, alignment, false, 0);

                    dst += srcSizeOf;
                }
                
                dstTypeIndex++;
            }

            if (dstArchetype->NumManagedComponents > 0)
            {
                ReplicateManagedComponents(srcChunk, srcIndex, dstChunk, dstBaseIndex, count, ref entityComponentStore);
            }
        }

        public static void InitializeBuffersInChunk(byte* p, int count, int stride, int bufferCapacity)
        {
            for (int i = 0; i < count; i++)
            {
                BufferHeader.Initialize((BufferHeader*)p, bufferCapacity);
                p += stride;
            }
        }
        
        public static void Convert(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count, ref EntityComponentStore entityComponentStore)
        {
            Assert.IsFalse(srcChunk == dstChunk);
            var srcArch = srcChunk->Archetype;
            var dstArch = dstChunk->Archetype;
            if (srcArch != dstArch)
            {
                Assert.IsFalse(srcArch == null);
            }

            var srcI = srcArch->NonZeroSizedTypesCount - 1;
            var dstI = dstArch->NonZeroSizedTypesCount - 1;

            var sourceTypesToDealloc = stackalloc int[srcI+1];
            int sourceTypesToDeallocCount = 0;

            while (dstI >= 0)
            {
                var srcType = srcArch->Types[srcI];
                var dstType = dstArch->Types[dstI];

                if (srcType > dstType)
                {
                    //Type in source is not moved so deallocate it
                    sourceTypesToDealloc[sourceTypesToDeallocCount++] = srcI;
                    --srcI;
                    continue;
                }

                var srcStride = srcArch->SizeOfs[srcI];
                var dstStride = dstArch->SizeOfs[dstI];
                var src = srcChunk->Buffer + srcArch->Offsets[srcI] + srcIndex * srcStride;
                var dst = dstChunk->Buffer + dstArch->Offsets[dstI] + dstIndex * dstStride;

                if (srcType == dstType)
                {
                    UnsafeUtility.MemCpy(dst, src, count * srcStride);
                    --srcI;
                    --dstI;
                }
                else
                {
                    if(dstType.IsBuffer)
                        InitializeBuffersInChunk(dst, count, dstStride, dstArch->BufferCapacities[dstI]);
                    else
                        UnsafeUtility.MemClear(dst, count * dstStride);
                    --dstI;
                }
            }

            if (sourceTypesToDeallocCount == 0)
                return;

            sourceTypesToDealloc[sourceTypesToDeallocCount] = 0;

            int iDealloc = 0;
            if (sourceTypesToDealloc[iDealloc] >= srcArch->FirstManagedComponent)
            {
                var freeCommandHandle = entityComponentStore.ManagedChangesTracker.BeginFreeManagedComponentCommand();
                do
                {
                    srcI = sourceTypesToDealloc[iDealloc];
                    var srcStride = srcArch->SizeOfs[srcI];
                    var src = srcChunk->Buffer + srcArch->Offsets[srcI] + srcIndex * srcStride;

                    var a = (int*)src;
                    for(int i = 0; i < count; i++)
                    {
                        var managedComponentIndex = a[i];
                        if(managedComponentIndex == 0)
                            continue;
                        entityComponentStore.FreeManagedComponentIndex(managedComponentIndex);
                        entityComponentStore.ManagedChangesTracker.AddToFreeManagedComponentCommand(managedComponentIndex);
                    }
                } while ((sourceTypesToDealloc[++iDealloc] >= srcArch->FirstManagedComponent));
                entityComponentStore.ManagedChangesTracker.EndDeallocateManagedComponentCommand(freeCommandHandle);
            }

            while(sourceTypesToDealloc[iDealloc] >= srcArch->FirstBufferComponent)
            {
                srcI = sourceTypesToDealloc[iDealloc];
                var srcStride = srcArch->SizeOfs[srcI];
                var srcPtr = srcChunk->Buffer + srcArch->Offsets[srcI] + srcIndex * srcStride;
                for(int i = 0; i < count; i++)
                {
                    BufferHeader.Destroy((BufferHeader*)srcPtr);
                    srcPtr += srcStride;
                }
                ++iDealloc;
            }
        }

        public static void MemsetUnusedChunkData(Chunk* chunk, byte value)
        {
            var arch = chunk->Archetype;
            var bufferSize = Chunk.GetChunkBufferSize();
            var buffer = chunk->Buffer;
            var count = chunk->Count;

            for (int i = 0; i<arch->TypesCount-1; ++i)
            {
                var index = arch->TypeMemoryOrder[i];

                var nextIndex = arch->TypeMemoryOrder[i + 1];
                var componentSize = arch->SizeOfs[index];
                var startOffset = arch->Offsets[index] + count * componentSize;
                var endOffset = arch->Offsets[nextIndex];
                var componentDataType = &arch->Types[index];
                
                // Start Offset needs to be fixed if we have a Dynamic Buffer
                if (componentDataType->IsBuffer)
                {
                    var elementSize = TypeManager.GetTypeInfo(componentDataType->TypeIndex).ElementSize;
                    var bufferCapacity = arch->BufferCapacities[index];
                    
                    for (int chunkI = 0; chunkI < count; chunkI++)
                    {
                        var bufferHeader = (BufferHeader*)(buffer + arch->Offsets[index] + (chunkI * componentSize));
                        
                        // If bufferHeader->Pointer is not null it means with rely on a dedicated buffer instead of the internal one (that follows the header) to store the elements
                        //  in this case we wipe everything after the header. Otherwise we wipe after the used elements.
                        var elementCountToClean = bufferHeader->Pointer != null ? bufferCapacity : (bufferHeader->Capacity - bufferHeader->Length);
                        var firstElementToClean = bufferHeader->Pointer != null ? 0 : bufferHeader->Length;

                        byte* internalBuffer = (byte*)(bufferHeader + 1);

                        UnsafeUtility.MemSet(internalBuffer + (firstElementToClean*elementSize), value, elementCountToClean*elementSize);
                    }
                }
                
                UnsafeUtility.MemSet(buffer + startOffset, value, endOffset - startOffset);
            }
            var lastIndex = arch->TypeMemoryOrder[arch->TypesCount - 1];
            var lastStartOffset = arch->Offsets[lastIndex] + count * arch->SizeOfs[lastIndex];
            UnsafeUtility.MemSet(buffer + lastStartOffset, value, bufferSize - lastStartOffset);
            
            // 0 the sequence number and the chunk header padding zone
            UnsafeUtility.MemClear(40 + (byte*)chunk, 24);    // End of chunk header at 40, we clear the header padding (24) and the Buffer value which is the very first data after the header
        }

        public static bool AreLayoutCompatible(Archetype* a, Archetype* b)
        {
            if ((a == null) || (b == null) ||
                (a->ChunkCapacity != b->ChunkCapacity))
                return false;

            var typeCount = a->NonZeroSizedTypesCount;
            if (typeCount != b->NonZeroSizedTypesCount)
                return false;

            for (int i = 0; i < typeCount; ++i)
            {
                if (a->Types[i] != b->Types[i])
                    return false;
            }

            return true;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void AssertAreLayoutCompatible(Archetype* a, Archetype* b)
        {
            Assert.IsTrue(AreLayoutCompatible(a,b));

            var typeCount = a->NonZeroSizedTypesCount;

            //If types are identical; SizeOfs, Offsets and BufferCapacities should match
            for (int i = 0; i < typeCount; ++i)
            {
                Assert.AreEqual(a->SizeOfs[i], b->SizeOfs[i]);
                Assert.AreEqual(a->Offsets[i], b->Offsets[i]);
                Assert.AreEqual(a->BufferCapacities[i], b->BufferCapacities[i]);
            }
        }
        
        public static void DeallocateBuffers(Chunk* chunk)
        {
            var archetype = chunk->Archetype;

            var bufferComponentsEnd = archetype->BufferComponentsEnd;
            for (var ti = archetype->FirstBufferComponent; ti < bufferComponentsEnd; ++ti)
            {
                Assert.IsTrue(archetype->Types[ti].IsBuffer);
                var basePtr = chunk->Buffer + archetype->Offsets[ti];
                var stride = archetype->SizeOfs[ti];

                for (int i = 0; i < chunk->Count; ++i)
                {
                    byte* bufferPtr = basePtr + stride * i;
                    BufferHeader.Destroy((BufferHeader*) bufferPtr);
                }
            }
        }
    }
}
