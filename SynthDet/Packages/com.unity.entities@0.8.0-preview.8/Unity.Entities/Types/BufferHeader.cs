using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct BufferHeader
    {
        public const int kMinimumCapacity = 8;

        [FieldOffset(0)] public byte* Pointer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int Capacity;

        public static byte* GetElementPointer(BufferHeader* header)
        {
            if (header->Pointer != null)
                return header->Pointer;

            return (byte*) (header + 1);
        }

        public enum TrashMode
        {
            TrashOldData,
            RetainOldData
        }

        public static void EnsureCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern, byte memoryInitPattern)
        {
            if (count <= header->Capacity)
                return;
            var adjustedCount = Math.Max(kMinimumCapacity, Math.Max(2 * header->Capacity, count)); // stop pathological performance of ++Capacity allocating every time, tiny Capacities
            SetCapacity(header, adjustedCount, typeSize, alignment, trashMode, useMemoryInitPattern, memoryInitPattern, 0);
        }
        

        public static void SetCapacity(BufferHeader* header, int count, int typeSize, int alignment, TrashMode trashMode, bool useMemoryInitPattern, byte memoryInitPattern, int internalCapacity)
        {
            var newCapacity = count;
            if (newCapacity == header->Capacity)
                return;

            long newSizeInBytes = (long)newCapacity * typeSize;

            byte* oldData = GetElementPointer(header);
            byte* newData = (newCapacity <= internalCapacity) ? (byte*)(header + 1) : (byte*) UnsafeUtility.Malloc(newSizeInBytes, alignment, Allocator.Persistent);

            if (oldData != newData) // if at least one of them isn't the internal pointer...
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (useMemoryInitPattern)
                {
                    if (trashMode == TrashMode.RetainOldData)
                    {
                        var oldSizeInBytes = (header->Capacity * typeSize);
                        var bytesToInitialize = newSizeInBytes - oldSizeInBytes;
                        if (bytesToInitialize > 0)
                        {
                            UnsafeUtility.MemSet(newData + oldSizeInBytes, memoryInitPattern, bytesToInitialize);
                        }
                    }
                    else
                    {
                        UnsafeUtility.MemSet(newData, memoryInitPattern, newSizeInBytes);
                    }
                }
#endif
                if (trashMode == TrashMode.RetainOldData)
                {
                    long bytesToCopy = Math.Min((long) header->Capacity, count) * typeSize;
                    UnsafeUtility.MemCpy(newData, oldData, bytesToCopy);
                }
                // Note we're freeing the old buffer only if it was not using the internal capacity. Don't change this to 'oldData', because that would be a bug.
                if (header->Pointer != null)
                {
                    UnsafeUtility.Free(header->Pointer, Allocator.Persistent);
                }
            }

            header->Pointer = (newData == (byte*)(header + 1)) ? null : newData;
            header->Capacity = newCapacity;
        }

        public static void Assign(BufferHeader* header, byte* source, int count, int typeSize, int alignment, bool useMemoryInitPattern, byte memoryInitPattern)
        {
            EnsureCapacity(header, count, typeSize, alignment, TrashMode.TrashOldData, useMemoryInitPattern, memoryInitPattern);

            // Select between internal capacity buffer and heap buffer.
            byte* elementPtr = GetElementPointer(header);

            UnsafeUtility.MemCpy(elementPtr, source, (long)typeSize * count);

            header->Length = count;
        }

        public static void Initialize(BufferHeader* header, int bufferCapacity)
        {
            header->Pointer = null;
            header->Length = 0;
            header->Capacity = bufferCapacity;
        }

        public static void Destroy(BufferHeader* header)
        {
            if (header->Pointer != null)
            {
                UnsafeUtility.Free(header->Pointer, Allocator.Persistent);
            }

            Initialize(header, 0);
        }

        // After cloning two worlds have access to the same malloc'ed buffer pointer leading to double deallocate etc.
        // So after cloning, just allocate all malloc based buffers and copy the data.
        public static void PatchAfterCloningChunk(Chunk* chunk)
        {
            for (int i = 0; i < chunk->Archetype->TypesCount; ++i)
            {
                var type = chunk->Archetype->Types[i];
                if (!type.IsBuffer)
                    continue;
                var ti = TypeManager.GetTypeInfo(type.TypeIndex);
                var sizeOf = chunk->Archetype->SizeOfs[i];
                var offset = chunk->Archetype->Offsets[i];
                for (var j = 0; j < chunk->Count; ++j)
                {
                    var offsetOfBuffer = offset + sizeOf * j;
                    var header = (BufferHeader*)(chunk->Buffer + offsetOfBuffer);
                    if (header->Pointer != null) // hoo boy, it's a malloc
                    {
                        BufferHeader newHeader = *header;
                        long bytesToAllocate = (long)header->Capacity * ti.ElementSize;
                        long bytesToCopy = (long)header->Length * ti.ElementSize;
                        newHeader.Pointer = (byte*)UnsafeUtility.Malloc(bytesToAllocate, TypeManager.MaximumSupportedAlignment, Allocator.Persistent);
                        UnsafeUtility.MemCpy(newHeader.Pointer, header->Pointer, bytesToCopy);
                        *header = newHeader;
                    }
                }
            }
        }
    }
}
