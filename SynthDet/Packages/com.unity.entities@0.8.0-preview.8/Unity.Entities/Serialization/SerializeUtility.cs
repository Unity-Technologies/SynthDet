using System;
using System.Collections.Generic;
#if !NET_DOTS
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

// Remove this once DOTSRuntime can use Unity.Properties
[assembly: InternalsVisibleTo("Unity.TinyConversion")]
[assembly: InternalsVisibleTo("Unity.Entities.Runtime.Build")]

namespace Unity.Entities.Serialization
{
    public static partial class SerializeUtility
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct BufferPatchRecord
        {
            public int ChunkOffset;
            public int AllocSizeBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BlobAssetRefPatchRecord
        {
            public int ChunkOffset;
            public int BlobDataOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SharedComponentRecord
        {
            public ulong StableTypeHash;
            public int ComponentSize;
        }

        public static int CurrentFileFormatVersion = 36;

        public static unsafe void DeserializeWorld(ExclusiveEntityTransaction manager, BinaryReader reader, object[] unityObjects = null)
        {
            if (manager.EntityComponentStore->CountEntities() != 0)
            {
                throw new ArgumentException(
                    $"DeserializeWorld can only be used on completely empty EntityManager. Please create a new empty World and use EntityManager.MoveEntitiesFrom to move the loaded entities into the destination world instead.");
            }
            int storedVersion = reader.ReadInt();
            if (storedVersion != CurrentFileFormatVersion)
            {
                throw new ArgumentException(
                    $"Attempting to read a entity scene stored in an old file format version (stored version : {storedVersion}, current version : {CurrentFileFormatVersion})");
            }

            var types = ReadTypeArray(reader);
            int totalEntityCount;

            var archetypes = ReadArchetypes(reader, types, manager, out totalEntityCount);

            var numSharedComponents = ReadSharedComponentMetadata(reader, out var sharedComponentArray, out var sharedComponentRecordArray);
            var sharedComponentRemap = new NativeArray<int>(numSharedComponents + 1, Allocator.Temp);

            int sharedAndManagedDataSize = reader.ReadInt();
            int managedComponentCount = reader.ReadInt();
#if !NET_DOTS
            var sharedAndManagedBuffer = new UnsafeAppendBuffer(sharedAndManagedDataSize, 16, Allocator.Temp);
            sharedAndManagedBuffer.ResizeUninitialized(sharedAndManagedDataSize);
            reader.ReadBytes(sharedAndManagedBuffer.Ptr, sharedAndManagedDataSize);
            var sharedAndManagedStream = sharedAndManagedBuffer.AsReader();
            var managedDataReader = new ManagedDataReader(&sharedAndManagedStream, (UnityEngine.Object[])unityObjects);
            ReadSharedComponents(manager, managedDataReader, sharedComponentRemap, sharedComponentRecordArray);
            manager.ManagedComponentStore.ResetManagedComponentStoreForDeserialization(managedComponentCount, ref *manager.EntityComponentStore);

            // Deserialize all managed components
            for (int i = 0; i < managedComponentCount; ++i)
            {
                ulong typeHash = sharedAndManagedStream.ReadNext<ulong>();
                int typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(typeHash);
                Type managedType = TypeManager.GetTypeInfo(typeIndex).Type;
                object obj = managedDataReader.ReadManagedComponent(managedType);
                manager.ManagedComponentStore.SetManagedComponentValue(i+1, obj);
            }
#else
            ReadSharedComponents(manager, reader, sharedAndManagedDataSize, sharedComponentRemap, sharedComponentRecordArray);
#endif

            manager.AllocateConsecutiveEntitiesForLoading(totalEntityCount);

            int totalChunkCount = reader.ReadInt();
            var chunksWithMetaChunkEntities = new NativeList<ArchetypeChunk>(totalChunkCount, Allocator.Temp);

            var totalBlobAssetSize = reader.ReadInt();
            byte* allBlobAssetData = null;

            var blobAssetRefChunks = new NativeList<ArchetypeChunk>();
            var blobAssetOwner = default(BlobAssetOwner);
            if (totalBlobAssetSize != 0)
            {
                allBlobAssetData = (byte*)UnsafeUtility.Malloc((long)totalBlobAssetSize, 16, Allocator.Persistent);
                if (totalBlobAssetSize > int.MaxValue)
                    throw new System.ArgumentException("Blobs larger than 2GB are currently not supported");

                reader.ReadBytes(allBlobAssetData, totalBlobAssetSize);

                blobAssetOwner = new BlobAssetOwner(allBlobAssetData, totalBlobAssetSize);
                blobAssetRefChunks = new NativeList<ArchetypeChunk>(32, Allocator.Temp);
            }

            int sharedComponentArraysIndex = 0;
            for (int i = 0; i < totalChunkCount; ++i)
            {
                var chunk = manager.EntityComponentStore->AllocateChunk();
                reader.ReadBytes(chunk, Chunk.kChunkSize);

                var archetype = chunk->Archetype = archetypes[(int)chunk->Archetype].Archetype;
                var numSharedComponentsInArchetype = chunk->Archetype->NumSharedComponents;
                int* sharedComponentValueArray = (int*)sharedComponentArray.GetUnsafePtr() + sharedComponentArraysIndex;

                for (int j = 0; j < numSharedComponentsInArchetype; ++j)
                {
                    // The shared component 0 is not part of the array, so an index equal to the array size is valid.
                    if (sharedComponentValueArray[j] > numSharedComponents)
                    {
                        throw new ArgumentException(
                            $"Archetype uses shared component at index {sharedComponentValueArray[j]} but only {numSharedComponents} are available, check if the shared scene has been properly loaded.");
                    }
                }

                var remapedSharedComponentValues = stackalloc int[archetype->NumSharedComponents];

                RemapSharedComponentIndices(remapedSharedComponentValues, archetype, sharedComponentRemap, sharedComponentValueArray);

                sharedComponentArraysIndex += numSharedComponentsInArchetype;

                // Allocate additional heap memory for buffers that have overflown into the heap, and read their data.
                int bufferAllocationCount = reader.ReadInt();
                if (bufferAllocationCount > 0)
                {
                    var bufferPatches = new NativeArray<BufferPatchRecord>(bufferAllocationCount, Allocator.Temp);
                    reader.ReadArray(bufferPatches, bufferPatches.Length);

                    // TODO: PERF: Batch malloc interface.
                    for (int pi = 0; pi < bufferAllocationCount; ++pi)
                    {
                        var target = (BufferHeader*)OffsetFromPointer(chunk->Buffer, bufferPatches[pi].ChunkOffset);

                        // TODO: Alignment
                        target->Pointer = (byte*) UnsafeUtility.Malloc(bufferPatches[pi].AllocSizeBytes, 8, Allocator.Persistent);

                        reader.ReadBytes(target->Pointer, bufferPatches[pi].AllocSizeBytes);
                    }

                    bufferPatches.Dispose();
                }

                if (totalBlobAssetSize != 0 && archetype->ContainsBlobAssetRefs)
                {
                    blobAssetRefChunks.Add(new ArchetypeChunk(chunk, manager.EntityComponentStore));
                    PatchBlobAssetsInChunkAfterLoad(chunk, allBlobAssetData);
                }

                manager.EntityComponentStore->AddExistingChunk(chunk, remapedSharedComponentValues);
                manager.ManagedComponentStore.Playback(ref manager.EntityComponentStore->ManagedChangesTracker);

                if (chunk->metaChunkEntity != Entity.Null)
                {
                    chunksWithMetaChunkEntities.Add(new ArchetypeChunk(chunk, manager.EntityComponentStore));
                }
            }

            if (totalBlobAssetSize != 0)
            {
                manager.AddSharedComponent(blobAssetRefChunks.AsArray(), blobAssetOwner);
                blobAssetRefChunks.Dispose();
            }

            for (int i = 0; i < chunksWithMetaChunkEntities.Length; ++i)
            {
                var chunk = chunksWithMetaChunkEntities[i].m_Chunk;
                manager.SetComponentData(chunk->metaChunkEntity, new ChunkHeader{ArchetypeChunk = chunksWithMetaChunkEntities[i]});
            }

            blobAssetOwner.Release();
            chunksWithMetaChunkEntities.Dispose();
            archetypes.Dispose();
            types.Dispose();
            sharedComponentRemap.Dispose();
#if !NET_DOTS
            sharedAndManagedBuffer.Dispose();
#endif

            // Chunks have now taken over ownership of the shared components (reference counts have been added)
            // so remove the ref that was added on deserialization
            for (int i = 0; i < numSharedComponents; ++i)
            {
                manager.ManagedComponentStore.RemoveReference(i + 1);
            }
        }

        private static unsafe NativeArray<EntityArchetype> ReadArchetypes(BinaryReader reader, NativeArray<int> types, ExclusiveEntityTransaction entityManager,
            out int totalEntityCount)
        {
            int archetypeCount = reader.ReadInt();
            var archetypes = new NativeArray<EntityArchetype>(archetypeCount, Allocator.Temp);
            totalEntityCount = 0;
            var tempComponentTypes = new NativeList<ComponentType>(Allocator.Temp);
            for (int i = 0; i < archetypeCount; ++i)
            {
                var archetypeEntityCount = reader.ReadInt();
                totalEntityCount += archetypeEntityCount;
                int archetypeComponentTypeCount = reader.ReadInt();
                tempComponentTypes.Clear();
                for (int iType = 0; iType < archetypeComponentTypeCount; ++iType)
                {
                    int typeHashIndexInFile = reader.ReadInt();
                    int typeHashIndexInFileNoFlags = typeHashIndexInFile & TypeManager.ClearFlagsMask;
                    int typeIndex = types[typeHashIndexInFileNoFlags];
                    if (TypeManager.IsChunkComponent(typeHashIndexInFile))
                        typeIndex = TypeManager.MakeChunkComponentTypeIndex(typeIndex);

                    tempComponentTypes.Add(ComponentType.FromTypeIndex(typeIndex));
                }

                archetypes[i] = entityManager.CreateArchetype((ComponentType*) tempComponentTypes.GetUnsafePtr(),
                    tempComponentTypes.Length);
            }

            tempComponentTypes.Dispose();
            return archetypes;
        }

        private static NativeArray<int> ReadTypeArray(BinaryReader reader)
        {
            int typeCount = reader.ReadInt();
            var typeHashBuffer = new NativeArray<ulong>(typeCount, Allocator.Temp);

            reader.ReadArray(typeHashBuffer, typeCount);

            var types = new NativeArray<int>(typeCount, Allocator.Temp);
            for (int i = 0; i < typeCount; ++i)
            {
                var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(typeHashBuffer[i]);
                if (typeIndex < 0)
                    throw new ArgumentException($"Cannot find TypeIndex for type hash {typeHashBuffer[i].ToString()}. Ensure your runtime depends on all assemblies defining the Component types your data uses.");

                types[i] = typeIndex;
            }

            typeHashBuffer.Dispose();
            return types;
        }

        unsafe internal static UnsafeArchetypePtrList GetAllArchetypes(EntityComponentStore* entityComponentStore, Allocator allocator)
        {
            int count = 0;
            for (var i = 0; i < entityComponentStore->m_Archetypes.Length; ++i)
            {
                var archetype = entityComponentStore->m_Archetypes.Ptr[i];
                if (archetype->EntityCount > 0)
                    count++;
            }

            var archetypes = new UnsafeArchetypePtrList(count, allocator);
            archetypes.Resize(count, NativeArrayOptions.UninitializedMemory);
            count = 0;
            for (var i = 0; i < entityComponentStore->m_Archetypes.Length; ++i)
            {
                var archetype = entityComponentStore->m_Archetypes.Ptr[i];
                if (archetype->EntityCount > 0)
                    archetypes.Ptr[count++] = entityComponentStore->m_Archetypes.Ptr[i];
            }

            return archetypes;
        }

        public static unsafe void SerializeWorld(EntityManager entityManager, BinaryWriter writer)
        {
            var entityRemapInfos = new NativeArray<EntityRemapUtility.EntityRemapInfo>(entityManager.EntityCapacity, Allocator.Temp);
            SerializeWorldInternal(entityManager, writer, out var referencedObjects, entityRemapInfos);
            entityRemapInfos.Dispose();
        }

        public static unsafe void SerializeWorld(EntityManager entityManager, BinaryWriter writer, out object[] referencedObjects)
        {
            var entityRemapInfos = new NativeArray<EntityRemapUtility.EntityRemapInfo>(entityManager.EntityCapacity, Allocator.Temp);
            SerializeWorldInternal(entityManager, writer, out referencedObjects, entityRemapInfos);
            entityRemapInfos.Dispose();
        }

        public static unsafe void SerializeWorld(EntityManager entityManager, BinaryWriter writer, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos)
        {
            SerializeWorldInternal(entityManager, writer, out var referencedObjects, entityRemapInfos);
        }

        public static unsafe void SerializeWorld(EntityManager entityManager, BinaryWriter writer, out object[] referencedObjects, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos)
        {
            SerializeWorldInternal(entityManager, writer, out referencedObjects, entityRemapInfos);
        }

        internal static unsafe void SerializeWorldInternal(EntityManager entityManager, BinaryWriter writer, out object[] referencedObjects, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos, bool isDOTSRuntime = false)
        {
            writer.Write(CurrentFileFormatVersion);
            var entityComponentStore = entityManager.EntityComponentStore;

            var archetypeArray = GetAllArchetypes(entityComponentStore, Allocator.Temp);

            var typeHashToIndexMap = new UnsafeHashMap<ulong, int>(1024, Allocator.Temp);
            for (int i = 0;i != archetypeArray.Length;i++)
            {
                var archetype = archetypeArray.Ptr[i];
                for (int iType = 0; iType < archetype->TypesCount; ++iType)
                {
                    var typeIndex = archetype->Types[iType].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var hash = typeInfo.StableTypeHash;
#if !NET_DOTS
                    ValidateTypeForSerialization(typeInfo);
#endif
                    typeHashToIndexMap.TryAdd(hash, i);
                }
            }

            using (var typeHashSet = typeHashToIndexMap.GetKeyArray(Allocator.Temp))
            {
                writer.Write(typeHashSet.Length);
                foreach (ulong hash in typeHashSet)
                    writer.Write(hash);

                for (int i = 0; i < typeHashSet.Length; ++i)
                    typeHashToIndexMap[typeHashSet[i]] = i;
            }

            WriteArchetypes(writer, archetypeArray, typeHashToIndexMap);
            var sharedComponentMapping = GatherSharedComponents(archetypeArray, out var sharedComponentArraysTotalCount);
            var sharedComponentArrays = new NativeArray<int>(sharedComponentArraysTotalCount, Allocator.Temp);
            FillSharedComponentArrays(sharedComponentArrays, archetypeArray, sharedComponentMapping);
            writer.Write(sharedComponentArrays.Length);
            writer.WriteArray(sharedComponentArrays);
            sharedComponentArrays.Dispose();

            var sharedComponentsToSerialize = new int[sharedComponentMapping.Count() - 1];
            using (var keyArray = sharedComponentMapping.GetKeyArray(Allocator.Temp))
            {
                foreach (var key in keyArray)
                {
                    if (key == 0)
                        continue;

                    if (sharedComponentMapping.TryGetValue(key, out var val))
                        sharedComponentsToSerialize[val - 1] = key;
                }
            }

            //TODO: ensure chunks are defragged?
            var bufferPatches = new NativeList<BufferPatchRecord>(128, Allocator.Temp);
            var totalChunkCount = GenerateRemapInfo(entityManager, archetypeArray, entityRemapInfos);

            referencedObjects = null;
            WriteSharedAndManagedComponents(entityManager, archetypeArray, sharedComponentsToSerialize, writer, out referencedObjects,
                isDOTSRuntime, (EntityRemapUtility.EntityRemapInfo*)entityRemapInfos.GetUnsafePtr());

            writer.Write(totalChunkCount);

            GatherAllUsedBlobAssets(archetypeArray, out var blobAssets, out var blobAssetMap);

            var blobAssetOffsets = new NativeArray<int>(blobAssets.Length, Allocator.Temp);
            int totalBlobAssetSize = sizeof(BlobAssetBatch);

            for(int i = 0; i<blobAssets.Length; ++i)
            {
                totalBlobAssetSize += sizeof(BlobAssetHeader);
                blobAssetOffsets[i] = totalBlobAssetSize;
                totalBlobAssetSize += Align16(blobAssets[i].header->Length);
            }

            writer.Write(totalBlobAssetSize);
            var blobAssetBatch = BlobAssetBatch.CreateForSerialize(blobAssets.Length, totalBlobAssetSize);
            writer.WriteBytes(&blobAssetBatch, sizeof(BlobAssetBatch));
            var zeroBytes = int4.zero;
            for(int i = 0; i<blobAssets.Length; ++i)
            {
                var blobAssetLength = blobAssets[i].header->Length;
                var blobAssetHash = blobAssets[i].header->Hash;
                var header = BlobAssetHeader.CreateForSerialize(Align16(blobAssetLength), blobAssetHash);
                writer.WriteBytes(&header, sizeof(BlobAssetHeader));
                writer.WriteBytes(blobAssets[i].header + 1, blobAssetLength);
                writer.WriteBytes(&zeroBytes, header.Length - blobAssetLength);
            }

            var tempChunk = Chunk.MallocChunk(Allocator.Temp);
            int currentManagedComponentIndex = 1;
            for(int a = 0; a < archetypeArray.Length; ++a)
            {
                var archetype = archetypeArray.Ptr[a];

                for (var ci = 0; ci < archetype->Chunks.Count; ++ci)
                {
                    var chunk = archetype->Chunks.p[ci];
                    bufferPatches.Clear();

                    UnsafeUtility.MemCpy(tempChunk, chunk, Chunk.kChunkSize);
                    tempChunk->metaChunkEntity = EntityRemapUtility.RemapEntity(ref entityRemapInfos, tempChunk->metaChunkEntity);

                    // Prevent patching from touching buffers allocated memory
                    BufferHeader.PatchAfterCloningChunk(tempChunk);
                    PatchManagedComponentIndices(tempChunk, archetype, ref currentManagedComponentIndex, entityManager.ManagedComponentStore);

                    byte* tempChunkBuffer = tempChunk->Buffer;
                    EntityRemapUtility.PatchEntities(archetype->ScalarEntityPatches, archetype->ScalarEntityPatchCount, archetype->BufferEntityPatches, archetype->BufferEntityPatchCount, tempChunkBuffer, tempChunk->Count, ref entityRemapInfos);
                    if(archetype->ContainsBlobAssetRefs)
                        PatchBlobAssetsInChunkBeforeSave(tempChunk, chunk, blobAssetOffsets, blobAssetMap);

                    FillPatchRecordsForChunk(chunk, bufferPatches);

                    ClearChunkHeaderComponents(tempChunk);
                    ChunkDataUtility.MemsetUnusedChunkData(tempChunk, 0);
                    tempChunk->Archetype = (Archetype*) a;

                    writer.WriteBytes(tempChunk, Chunk.kChunkSize);

                    writer.Write(bufferPatches.Length);

                    if (bufferPatches.Length > 0)
                    {
                        writer.WriteList(bufferPatches);

                        // Write heap backed data for each required patch.
                        // TODO: PERF: Investigate static-only deserialization could manage one block and mark in pointers somehow that they are not indiviual
                        for (int i = 0; i < bufferPatches.Length; ++i)
                        {
                            var patch = bufferPatches[i];
                            var header = (BufferHeader*)OffsetFromPointer(tempChunk->Buffer, patch.ChunkOffset);
                            writer.WriteBytes(header->Pointer, patch.AllocSizeBytes);
                            BufferHeader.Destroy(header);
                        }
                    }
                }
            }


            archetypeArray.Dispose();
            blobAssets.Dispose();
            blobAssetMap.Dispose();

            bufferPatches.Dispose();
            UnsafeUtility.Free(tempChunk, Allocator.Temp);

            typeHashToIndexMap.Dispose();
        }

        static int Align16(int x)
        {
            return (x + 15) & ~15;
        }

        unsafe static void PatchManagedComponentIndices(Chunk* chunk, Archetype* archetype, ref int currentManagedIndex, ManagedComponentStore managedComponentStore)
        {
            for (int i = 0; i < archetype->NumManagedComponents; ++i)
            {
                var index = archetype->TypeMemoryOrder[i + archetype->FirstManagedComponent];
                var managedComponentIndices = (int*)ChunkDataUtility.GetComponentDataRO(chunk, 0, index);
                for (int ei = 0; ei < chunk->Count; ++ei)
                {
                    if(managedComponentIndices[ei] == 0)
                        continue;

                    var obj = managedComponentStore.GetManagedComponent(managedComponentIndices[ei]);
                    if (obj == null)
                        managedComponentIndices[ei] = 0;
                    else
                        managedComponentIndices[ei] = currentManagedIndex++;
                }
            }
        }

        private static unsafe void WriteSharedAndManagedComponents(EntityManager entityManager, UnsafeArchetypePtrList archetypeArray,
            int[] sharedComponentIndicies, BinaryWriter writer, out object[] referencedObjects, bool isDOTSRuntime, EntityRemapUtility.EntityRemapInfo* remapping)
        {
            int managedComponentCount = 0;
            referencedObjects = null;
            var allManagedObjectsBuffer = new UnsafeAppendBuffer(0, 16, Allocator.Temp);

// We only support serialization in dots runtime for some unit tests but we currently can't support shared component serialization so skip it
#if !NET_DOTS
            var sharedComponentRecordArray = new NativeArray<SharedComponentRecord>(sharedComponentIndicies.Length, Allocator.Temp);
            if (!isDOTSRuntime)
            {
                var propertiesWriter = new ManagedDataWriter(&allManagedObjectsBuffer);
                for (int i = 0; i < sharedComponentIndicies.Length; ++i)
                {
                    var index = sharedComponentIndicies[i];
                    var sharedData = entityManager.ManagedComponentStore.GetSharedComponentDataNonDefaultBoxed(index);
                    var type = sharedData.GetType();
                    var managedObject = Convert.ChangeType(sharedData, type);

                    BoxedProperties.WriteBoxedType(managedObject, propertiesWriter);

                    var typeInfo = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(type));
                    sharedComponentRecordArray[i] = new SharedComponentRecord()
                    {
                        StableTypeHash = typeInfo.StableTypeHash,
                        ComponentSize = -1
                    };
                }

                for (int a = 0; a < archetypeArray.Length; ++a)
                {
                    var archetype = archetypeArray.Ptr[a];
                    if (archetype->NumManagedComponents == 0)
                        continue;

                    for (var ci = 0; ci < archetype->Chunks.Count; ++ci)
                    {
                        var chunk = archetype->Chunks.p[ci];

                        for (int i = 0; i < archetype->NumManagedComponents; ++i)
                        {
                            var index = archetype->TypeMemoryOrder[i + archetype->FirstManagedComponent];
                            var managedComponentIndices = (int*)ChunkDataUtility.GetComponentDataRO(chunk, 0, index);
                            var cType = TypeManager.GetTypeInfo(archetype->Types[index].TypeIndex);

                            for (int ei = 0; ei < chunk->Count; ++ei)
                            {
                                if (managedComponentIndices[ei] == 0)
                                    continue;

                                var obj = entityManager.ManagedComponentStore.GetManagedComponent(managedComponentIndices[ei]);
                                if (obj == null)
                                    continue;

                                if (obj.GetType() != cType.Type)
                                {
                                    throw new InvalidOperationException($"Managed object type {obj.GetType()} doesn't match component type in archetype {cType.Type}");
                                }

                                managedComponentCount++;
                                allManagedObjectsBuffer.Add<ulong>(cType.StableTypeHash);

                                if (cType.HasEntities)
                                {
                                    var patchedClone = ManagedComponentStore.CloneAndPatchManagedComponent(obj, remapping);
                                    propertiesWriter.WriteManagedComponent(patchedClone);
                                    ManagedComponentStore.DisposeManagedObject(patchedClone);
                                }
                                else
                                {
                                    propertiesWriter.WriteManagedComponent(obj);
                                }
                            }
                        }
                    }
                }
                referencedObjects = propertiesWriter.GetObjectTable();
            }
            else
            {
                for (int i = 0; i < sharedComponentIndicies.Length; ++i)
                {
                    var index = sharedComponentIndicies[i];
                    object obj = entityManager.ManagedComponentStore.GetSharedComponentDataNonDefaultBoxed(index);

                    Type type = obj.GetType();
                    var typeIndex = TypeManager.GetTypeIndex(type);
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    int size = UnsafeUtility.SizeOf(type);

                    sharedComponentRecordArray[i] = new SharedComponentRecord()
                    {
                        StableTypeHash = typeInfo.StableTypeHash,
                        ComponentSize = size
                    };

                    var dataPtr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(obj, out ulong handle);
                    dataPtr += TypeManager.ObjectOffset;
                    allManagedObjectsBuffer.Add(dataPtr, size);
                    UnsafeUtility.ReleaseGCObject(handle);
                }
            }
#else
            var sharedComponentRecordArray = new NativeArray<SharedComponentRecord>(0, Allocator.Temp);
#endif

            writer.Write(sharedComponentRecordArray.Length);
            writer.WriteArray(sharedComponentRecordArray);

            writer.Write(allManagedObjectsBuffer.Length);

            writer.Write(managedComponentCount);
            writer.WriteBytes(allManagedObjectsBuffer.Ptr, allManagedObjectsBuffer.Length);

            sharedComponentRecordArray.Dispose();
            allManagedObjectsBuffer.Dispose();
        }

#if NET_DOTS
        static unsafe void ReadSharedComponents(ExclusiveEntityTransaction manager, BinaryReader reader, int expectedReadSize, NativeArray<int> sharedComponentRemap, NativeArray<SharedComponentRecord> sharedComponentRecordArray)
        {
            int tempBufferSize = 0;
            for (int i = 0; i < sharedComponentRecordArray.Length; ++i)
                tempBufferSize = math.max(sharedComponentRecordArray[i].ComponentSize, tempBufferSize);
            var buffer = stackalloc byte[tempBufferSize];

            sharedComponentRemap[0] = 0;

            int sizeRead = 0;
            for (int i = 0; i < sharedComponentRecordArray.Length; ++i)
            {
                var record = sharedComponentRecordArray[i];

                reader.ReadBytes(buffer, record.ComponentSize);
                sizeRead += record.ComponentSize;

                var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(record.StableTypeHash);
                if (typeIndex == -1)
                {
                    Console.WriteLine($"Can't find type index for type hash {record.StableTypeHash.ToString()}");
                    throw new InvalidOperationException();
                }

                var data = TypeManager.ConstructComponentFromBuffer(typeIndex, buffer);

                // TODO: this recalculation should be removed once we merge the NET_DOTS and non NET_DOTS hashcode calculations
                var hashCode = TypeManager.GetHashCode(data, typeIndex); // record.hashCode;
                int runtimeIndex = manager.ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, data);

                sharedComponentRemap[i + 1] = runtimeIndex;
            }

            Assert.AreEqual(expectedReadSize, sizeRead, "The amount of shared component data we read doesn't match the amount we serialized.");
        }
#else
        static void ReadSharedComponents(ExclusiveEntityTransaction manager, ManagedDataReader managedDataReader, NativeArray<int> sharedComponentRemap, NativeArray<SharedComponentRecord> sharedComponentRecordArray)
        {
            // 0 index is special and means default shared component value
            // Also see below the offset + 1 indices for the same reason
            sharedComponentRemap[0] = 0;

            for (int i = 0; i < sharedComponentRecordArray.Length; ++i)
            {
                var record = sharedComponentRecordArray[i];
                var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(record.StableTypeHash);
                var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                var managedObject = BoxedProperties.ReadBoxedClass(typeInfo.Type, managedDataReader);
                var currentHash = TypeManager.GetHashCode(managedObject, typeInfo.TypeIndex);
                int sharedComponentIndex = manager.ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, currentHash, managedObject);

                // When deserialization a shared component it is possible that it's hashcode changes if for example the referenced object (a UnityEngine.Object for example) becomes null.
                // This can result in the sharedComponentIndex at serialize time being different from the sharedComponentIndex at load time.
                // Thus we keep a remap table to handle this potential remap.
                // NOTE: in most cases the remap table will always be all indices matching,
                // But it doesn't look like it's worth optimizing this away at this point.
                sharedComponentRemap[i + 1] = sharedComponentIndex;
            }
        }

        // True when a component is valid to using in world serialization. A component IsSerializable when it is valid to blit
        // the data across storage media. Thus components containing pointers have an IsSerializable of false as the component
        // is blittable but no longer valid upon deserialization.
        private static bool IsTypeValidForSerialization(Type type)
        {
            if (type.GetCustomAttribute<ChunkSerializableAttribute>() != null)
                return true;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.IsStatic)
                    continue;

                if (field.FieldType.IsPointer || (field.FieldType == typeof(UIntPtr) || field.FieldType == typeof(IntPtr)))
                {
                    return false;
                }
                else if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    if (!IsTypeValidForSerialization(field.FieldType))
                        return false;
                }
            }

            return true;
        }

        private static void ValidateTypeForSerialization(TypeManager.TypeInfo typeInfo)
        {
            // Shared Components are expected to be handled specially and are not required to be blittable
            if (typeInfo.Category == TypeManager.TypeCategory.ISharedComponentData)
                return;

            if (!IsTypeValidForSerialization(typeInfo.Type))
            {
                throw new ArgumentException($"Blittable component type '{TypeManager.GetType(typeInfo.TypeIndex)}' contains a (potentially nested) pointer field. " +
                    $"Serializing bare pointers will likely lead to runtime errors. Remove this field and consider serializing the data " +
                    $"it points to another way such as by using a BlobAssetReference or a [Serializable] ISharedComponent. If for whatever " +
                    $"reason the pointer field should in fact be serialized, add the [ChunkSerializable] attribute to your type to bypass this error.");
            }
        }
#endif

        static unsafe int ReadSharedComponentMetadata(BinaryReader reader, out NativeArray<int> sharedComponentArrays, out NativeArray<SharedComponentRecord> sharedComponentRecordArray)
        {
            int sharedComponentArraysLength = reader.ReadInt();
            sharedComponentArrays = new NativeArray<int>(sharedComponentArraysLength, Allocator.Temp);
            reader.ReadArray(sharedComponentArrays, sharedComponentArraysLength);

            var sharedComponentRecordArrayLength = reader.ReadInt();
            sharedComponentRecordArray = new NativeArray<SharedComponentRecord>(sharedComponentRecordArrayLength, Allocator.Temp);
            reader.ReadArray(sharedComponentRecordArray, sharedComponentRecordArrayLength);

            return sharedComponentRecordArrayLength;
        }




        unsafe struct BlobAssetPtr : IEquatable<BlobAssetPtr>
        {
            public BlobAssetPtr(BlobAssetHeader* header)
            {
                this.header = header;
            }
            public readonly BlobAssetHeader* header;
            public bool Equals(BlobAssetPtr other)
            {
                return header == other.header;
            }

            public override int GetHashCode()
            {
                BlobAssetHeader* onStack = header;
                return (int)math.hash(&onStack, sizeof(BlobAssetHeader*));
            }
        }

        private static unsafe void GatherAllUsedBlobAssets(UnsafeArchetypePtrList archetypeArray, out NativeList<BlobAssetPtr> blobAssets, out NativeHashMap<BlobAssetPtr, int> blobAssetMap)
        {
            blobAssetMap = new NativeHashMap<BlobAssetPtr, int>(100, Allocator.Temp);

            blobAssets = new NativeList<BlobAssetPtr>(100, Allocator.Temp);
            for (int a = 0; a < archetypeArray.Length; ++a)
            {
                var archetype = archetypeArray.Ptr[a];
                if (!archetype->ContainsBlobAssetRefs)
                    continue;

                var typeCount = archetype->TypesCount;
                for (var ci = 0; ci < archetype->Chunks.Count; ++ci)
                {
                    var chunk = archetype->Chunks.p[ci];
                    var entityCount = chunk->Count;
                    for (var unordered_ti = 0; unordered_ti < typeCount; ++unordered_ti)
                    {
                        var ti = archetype->TypeMemoryOrder[unordered_ti];
                        var type = archetype->Types[ti];
                        if(type.IsZeroSized)
                            continue;

                        var ct = TypeManager.GetTypeInfo(type.TypeIndex);
                        var blobAssetRefCount = ct.BlobAssetRefOffsetCount;
                        if(blobAssetRefCount == 0)
                            continue;

                        var blobAssetRefOffsets = TypeManager.GetBlobAssetRefOffsets(ct);
                        var chunkBuffer = chunk->Buffer;

                        if (blobAssetRefCount > 0)
                        {
                            int subArrayOffset = archetype->Offsets[ti];
                            byte* componentArrayStart = OffsetFromPointer(chunkBuffer, subArrayOffset);

                            if (type.IsBuffer)
                            {
                                BufferHeader* header = (BufferHeader*)componentArrayStart;
                                int strideSize = archetype->SizeOfs[ti];
                                int elementSize = ct.ElementSize;

                                for (int bi = 0; bi < entityCount; ++bi)
                                {
                                    var bufferStart = BufferHeader.GetElementPointer(header);
                                    var bufferEnd = bufferStart + header->Length * elementSize;
                                    for (var componentData = bufferStart; componentData < bufferEnd; componentData += elementSize)
                                    {
                                        AddBlobAssetRefInfo(componentData, blobAssetRefOffsets, blobAssetRefCount, ref blobAssetMap, ref blobAssets);
                                    }

                                    header = (BufferHeader*)OffsetFromPointer(header, strideSize);
                                }
                            }
                            else
                            {
                                int componentSize = archetype->SizeOfs[ti];
                                byte* end = componentArrayStart + componentSize * entityCount;
                                for (var componentData = componentArrayStart; componentData < end; componentData += componentSize)
                                {
                                    AddBlobAssetRefInfo(componentData, blobAssetRefOffsets, blobAssetRefCount, ref blobAssetMap, ref blobAssets);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static unsafe void AddBlobAssetRefInfo(byte* componentData, TypeManager.EntityOffsetInfo* blobAssetRefOffsets, int blobAssetRefCount,
            ref NativeHashMap<BlobAssetPtr, int> blobAssetMap, ref NativeList<BlobAssetPtr> blobAssets)
        {
            for (int i = 0; i < blobAssetRefCount; ++i)
            {
                var blobAssetRefOffset = blobAssetRefOffsets[i].Offset;
                var blobAssetRefPtr = (BlobAssetReferenceData*)(componentData + blobAssetRefOffset);
                if (blobAssetRefPtr->m_Ptr == null)
                    continue;

                var blobAssetPtr = new BlobAssetPtr(blobAssetRefPtr->Header);
                if (!blobAssetMap.TryGetValue(blobAssetPtr, out var blobAssetIndex))
                {
                    blobAssetIndex = blobAssets.Length;
                    blobAssets.Add(blobAssetPtr);
                    blobAssetMap.TryAdd(blobAssetPtr, blobAssetIndex);
                }
            }
        }

        private static unsafe void PatchBlobAssetsInChunkBeforeSave(Chunk* tempChunk, Chunk* originalChunk,
            NativeArray<int> blobAssetOffsets, NativeHashMap<BlobAssetPtr, int> blobAssetMap)
        {
            var archetype = originalChunk->Archetype;
            var typeCount = archetype->TypesCount;
            var entityCount = originalChunk->Count;
            for (var unordered_ti = 0; unordered_ti < typeCount; ++unordered_ti)
            {
                var ti = archetype->TypeMemoryOrder[unordered_ti];
                var type = archetype->Types[ti];
                if(type.IsZeroSized)
                    continue;

                var ct = TypeManager.GetTypeInfo(type.TypeIndex);
                var blobAssetRefCount = ct.BlobAssetRefOffsetCount;
                if(blobAssetRefCount == 0)
                    continue;

                var blobAssetRefOffsets = TypeManager.GetBlobAssetRefOffsets(ct);
                var chunkBuffer = tempChunk->Buffer;
                int subArrayOffset = archetype->Offsets[ti];
                byte* componentArrayStart = OffsetFromPointer(chunkBuffer, subArrayOffset);

                if (type.IsBuffer)
                {
                    BufferHeader* header = (BufferHeader*)componentArrayStart;
                    int strideSize = archetype->SizeOfs[ti];
                    var elementSize = ct.ElementSize;

                    for (int bi = 0; bi < entityCount; ++bi)
                    {
                        var bufferStart = BufferHeader.GetElementPointer(header);
                        var bufferEnd = bufferStart + header->Length * elementSize;
                        for (var componentData = bufferStart; componentData < bufferEnd; componentData += elementSize)
                        {
                            PatchBlobAssetRefInfoBeforeSave(componentData, blobAssetRefOffsets, blobAssetRefCount, blobAssetOffsets, blobAssetMap);
                        }

                        header = (BufferHeader*) OffsetFromPointer(header, strideSize);
                    }
                }
                else if (blobAssetRefCount > 0)
                {
                    int size = archetype->SizeOfs[ti];
                    byte* end = componentArrayStart + size * entityCount;
                    for (var componentData = componentArrayStart; componentData < end; componentData += size)
                    {
                        PatchBlobAssetRefInfoBeforeSave(componentData, blobAssetRefOffsets, blobAssetRefCount, blobAssetOffsets, blobAssetMap);
                    }
                }
            }
        }

        private static unsafe void PatchBlobAssetRefInfoBeforeSave(byte* componentData, TypeManager.EntityOffsetInfo* blobAssetRefOffsets, int blobAssetRefCount,
            NativeArray<int> blobAssetOffsets, NativeHashMap<BlobAssetPtr, int> blobAssetMap)
        {
            for (int i = 0; i < blobAssetRefCount; ++i)
            {
                var blobAssetRefOffset = blobAssetRefOffsets[i].Offset;
                var blobAssetRefPtr = (BlobAssetReferenceData*)(componentData + blobAssetRefOffset);
                int value = -1;
                if (blobAssetRefPtr->m_Ptr != null)
                {
                    value = blobAssetMap[new BlobAssetPtr(blobAssetRefPtr->Header)];
                    value = blobAssetOffsets[value];
                }
                blobAssetRefPtr->m_Ptr = (byte*)value;
            }
        }

        private static unsafe void PatchBlobAssetsInChunkAfterLoad(Chunk* chunk, byte* allBlobAssetData)
        {
            var archetype = chunk->Archetype;
            var typeCount = archetype->TypesCount;
            var entityCount = chunk->Count;
            for (var unordered_ti = 0; unordered_ti < typeCount; ++unordered_ti)
            {
                var ti = archetype->TypeMemoryOrder[unordered_ti];
                var type = archetype->Types[ti];
                if(type.IsZeroSized)
                    continue;

                var ct = TypeManager.GetTypeInfo(type.TypeIndex);
                var blobAssetRefCount = ct.BlobAssetRefOffsetCount;
                if(blobAssetRefCount == 0)
                    continue;

                var blobAssetRefOffsets = TypeManager.GetBlobAssetRefOffsets(ct);
                var chunkBuffer = chunk->Buffer;
                int subArrayOffset = archetype->Offsets[ti];
                byte* componentArrayStart = OffsetFromPointer(chunkBuffer, subArrayOffset);

                if (type.IsBuffer)
                {
                    BufferHeader* header = (BufferHeader*)componentArrayStart;
                    int strideSize = archetype->SizeOfs[ti];
                    var elementSize = ct.ElementSize;

                    for (int bi = 0; bi < entityCount; ++bi)
                    {
                        var bufferStart = BufferHeader.GetElementPointer(header);
                        for (int ei = 0; ei < header->Length; ++ei)
                        {
                            byte* componentData = bufferStart + ei * elementSize;
                            for (int i = 0; i < blobAssetRefCount; ++i)
                            {
                                var offset = blobAssetRefOffsets[i].Offset;
                                var blobAssetRefPtr = (BlobAssetReferenceData*)(componentData + offset);
                                int value = (int)blobAssetRefPtr->m_Ptr;
                                byte* ptr = null;
                                if (value != -1)
                                {
                                    ptr = allBlobAssetData + value;
                                }
                                blobAssetRefPtr->m_Ptr = ptr;
                            }
                        }

                        header = (BufferHeader*)OffsetFromPointer(header, strideSize);
                    }
                }
                else if (blobAssetRefCount > 0)
                {
                    int size = archetype->SizeOfs[ti];
                    byte* end = componentArrayStart + size * entityCount;
                    for (var componentData = componentArrayStart; componentData < end; componentData += size)
                    {
                        for (int i = 0; i < blobAssetRefCount; ++i)
                        {
                            var offset = blobAssetRefOffsets[i].Offset;
                            var blobAssetRefPtr = (BlobAssetReferenceData*)(componentData + offset);
                            int value = (int) blobAssetRefPtr->m_Ptr;
                            byte* ptr = null;
                            if (value != -1)
                            {
                                ptr = allBlobAssetData + value;
                            }
                            blobAssetRefPtr->m_Ptr = ptr;
                        }
                    }
                }
            }
        }

        private static unsafe void FillPatchRecordsForChunk(Chunk* chunk, NativeList<BufferPatchRecord> bufferPatches)
        {
            var archetype = chunk->Archetype;
            byte* tempChunkBuffer = chunk->Buffer;
            int entityCount = chunk->Count;

            // Find all buffer pointer locations and work out how much memory the deserializer must allocate on load.
            for (int ti = 0; ti < archetype->TypesCount; ++ti)
            {
                int index = archetype->TypeMemoryOrder[ti];
                var type = archetype->Types[index];
                if(type.IsZeroSized)
                    continue;

                if (type.IsBuffer)
                {
                    var ct = TypeManager.GetTypeInfo(type.TypeIndex);
                    int subArrayOffset = archetype->Offsets[index];
                    BufferHeader* header = (BufferHeader*) OffsetFromPointer(tempChunkBuffer, subArrayOffset);
                    int stride = archetype->SizeOfs[index];
                    var elementSize = ct.ElementSize;

                    for (int bi = 0; bi < entityCount; ++bi)
                    {
                        if (header->Pointer != null)
                        {
                            int capacityInBytes = elementSize * header->Capacity;
                            bufferPatches.Add(new BufferPatchRecord
                            {
                                ChunkOffset = (int) (((byte*) header) - tempChunkBuffer),
                                AllocSizeBytes = capacityInBytes
                            });
                        }

                        header = (BufferHeader*) OffsetFromPointer(header, stride);
                    }
                }
            }
        }

        static unsafe void FillSharedComponentIndexRemap(int* remapArray, Archetype* archetype)
        {
            int i = 0;
            for (int iType = 1; iType < archetype->TypesCount; ++iType)
            {
                int orderedIndex = archetype->TypeMemoryOrder[iType] - archetype->FirstSharedComponent;
                if (0 <= orderedIndex && orderedIndex < archetype->NumSharedComponents)
                    remapArray[i++] = orderedIndex;
            }
        }

        static unsafe void RemapSharedComponentIndices(int* destValues, Archetype* archetype, NativeArray<int> remappedIndices, int* sourceValues)
        {
            int i = 0;
            for (int iType = 1; iType < archetype->TypesCount; ++iType)
            {
                int orderedIndex = archetype->TypeMemoryOrder[iType] - archetype->FirstSharedComponent;
                if (0 <= orderedIndex && orderedIndex < archetype->NumSharedComponents)
                    destValues[orderedIndex] = remappedIndices[sourceValues[i++]];
            }
        }

        private static unsafe void FillSharedComponentArrays(NativeArray<int> sharedComponentArrays, UnsafeArchetypePtrList archetypeArray, NativeHashMap<int, int> sharedComponentMapping)
        {
            int index = 0;
            for (int iArchetype = 0; iArchetype < archetypeArray.Length; ++iArchetype)
            {
                var archetype = archetypeArray.Ptr[iArchetype];
                int numSharedComponents = archetype->NumSharedComponents;
                if(numSharedComponents==0)
                    continue;
                var sharedComponentIndexRemap = stackalloc int[numSharedComponents];

                FillSharedComponentIndexRemap(sharedComponentIndexRemap, archetype);
                for (int iChunk = 0; iChunk < archetype->Chunks.Count; ++iChunk)
                {
                    var sharedComponents = archetype->Chunks.p[iChunk]->SharedComponentValues;
                    for (int iType = 0; iType < numSharedComponents; iType++)
                    {
                        int remappedIndex = sharedComponentIndexRemap[iType];
                        sharedComponentArrays[index++] = sharedComponentMapping[sharedComponents[remappedIndex]];
                    }
                }
            }
            Assert.AreEqual(sharedComponentArrays.Length,index);
        }

        private static unsafe NativeHashMap<int, int> GatherSharedComponents(UnsafeArchetypePtrList archetypeArray, out int sharedComponentArraysTotalCount)
        {
            sharedComponentArraysTotalCount = 0;
            var sharedIndexToSerialize = new NativeHashMap<int, int>(1024, Allocator.Temp);
            sharedIndexToSerialize.TryAdd(0, 0); // All default values map to 0
            int nextIndex = 1;
            for (int iArchetype = 0; iArchetype < archetypeArray.Length; ++iArchetype)
            {
                var archetype = archetypeArray.Ptr[iArchetype];
                sharedComponentArraysTotalCount += archetype->Chunks.Count * archetype->NumSharedComponents;

                int numSharedComponents = archetype->NumSharedComponents;
                for (int iType = 0; iType < numSharedComponents; iType++)
                {
                    var sharedComponents = archetype->Chunks.GetSharedComponentValueArrayForType(iType);
                    for (int iChunk = 0; iChunk < archetype->Chunks.Count; ++iChunk)
                    {
                        int sharedComponentIndex = sharedComponents[iChunk];
                        if (!sharedIndexToSerialize.TryGetValue(sharedComponentIndex, out var val))
                        {
                            sharedIndexToSerialize.TryAdd(sharedComponentIndex, nextIndex++);
                        }
                    }
                }
            }

            return sharedIndexToSerialize;
        }

        private static unsafe void ClearChunkHeaderComponents(Chunk* chunk)
        {
            int chunkHeaderTypeIndex = TypeManager.GetTypeIndex<ChunkHeader>();
            var archetype = chunk->Archetype;
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(chunk->Archetype, chunkHeaderTypeIndex);
            if (typeIndexInArchetype == -1)
                return;

            var buffer = chunk->Buffer;
            var length = chunk->Count;
            var startOffset = archetype->Offsets[typeIndexInArchetype];
            var chunkHeaders = (ChunkHeader*)(buffer + startOffset);
            for (int i = 0; i < length; ++i)
            {
                chunkHeaders[i] = ChunkHeader.Null;
            }
        }

        static unsafe byte* OffsetFromPointer(void* ptr, int offset)
        {
            return ((byte*)ptr) + offset;
        }

        static unsafe void WriteArchetypes(BinaryWriter writer, UnsafeArchetypePtrList archetypeArray, UnsafeHashMap<ulong, int> typeHashToIndexMap)
        {
            writer.Write(archetypeArray.Length);

            for (int a = 0; a < archetypeArray.Length; ++a)
            {
                var archetype = archetypeArray.Ptr[a];

                writer.Write(archetype->EntityCount);
                writer.Write(archetype->TypesCount - 1);
                for (int i = 1; i < archetype->TypesCount; ++i)
                {
                    var componentType = archetype->Types[i];
                    int flag = componentType.IsChunkComponent ? TypeManager.ChunkComponentTypeFlag : 0;
                    var hash = TypeManager.GetTypeInfo(componentType.TypeIndex).StableTypeHash;
                    writer.Write(typeHashToIndexMap[hash] | flag);
                }
            }
        }

        static unsafe int GenerateRemapInfo(EntityManager entityManager, UnsafeArchetypePtrList archetypeArray, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos)
        {
            int nextEntityId = 1; //0 is reserved for Entity.Null;

            int totalChunkCount = 0;
            for (int archetypeIndex = 0; archetypeIndex < archetypeArray.Length; ++archetypeIndex)
            {
                var archetype = archetypeArray.Ptr[archetypeIndex];
                for (int i = 0; i < archetype->Chunks.Count; ++i)
                {
                    var chunk = archetype->Chunks.p[i];
                    for (int iEntity = 0; iEntity < chunk->Count; ++iEntity)
                    {
                        var entity = *(Entity*)ChunkDataUtility.GetComponentDataRO(chunk, iEntity, 0);
                        EntityRemapUtility.AddEntityRemapping(ref entityRemapInfos, entity, new Entity { Version = 0, Index = nextEntityId });
                        ++nextEntityId;
                    }

                    totalChunkCount += 1;
                }
            }

            return totalChunkCount;
        }

#if !NET_DOTS
        unsafe class ManagedDataReader : PropertiesBinaryReader
        {
            public ManagedDataReader(UnsafeAppendBuffer.Reader* stream, UnityEngine.Object[] objectTable)
                : base(stream, objectTable)
            {
            }
            
            public object ReadManagedComponent(Type type)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    return ReadObject();

                return BoxedProperties.ReadBoxedClass(type, this);                
            }
        }

        unsafe class ManagedDataWriter : PropertiesBinaryWriter
        {
            public ManagedDataWriter(UnsafeAppendBuffer* stream)
                : base(stream)
            {
            }
            
            public void WriteManagedComponent(object obj)
            {
                if (obj is UnityEngine.Object uobj) 
                    AppendObject(uobj);
                else
                    BoxedProperties.WriteBoxedType(obj, this);
            }
        }
#endif
    }
}
