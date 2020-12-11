#if ENABLE_UNITY_COLLECTIONS_CHECKS
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    unsafe struct ComponentSafetyHandles
    {
        const int              kMaxTypes = TypeManager.MaximumTypesCount;

        ComponentSafetyHandle* m_ComponentSafetyHandles;
        ushort                 m_ComponentSafetyHandlesCount;
        const int              EntityTypeIndex = 1;

        ushort*                m_TypeArrayIndices;
        const ushort           NullTypeIndex = 0xFFFF;

        ushort GetTypeArrayIndex(int typeIndex)
        {
            var withoutFlags = typeIndex & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex != NullTypeIndex)
                return arrayIndex;

            Assert.IsFalse(TypeManager.IsZeroSized(typeIndex));

            arrayIndex = m_ComponentSafetyHandlesCount++;
            m_TypeArrayIndices[withoutFlags] = arrayIndex;
            m_ComponentSafetyHandles[arrayIndex].TypeIndex = typeIndex;
            m_ComponentSafetyHandles[arrayIndex].SafetyHandle = AtomicSafetyHandle.Create();
            AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[arrayIndex].SafetyHandle, false);
            
            m_ComponentSafetyHandles[arrayIndex].BufferHandle = AtomicSafetyHandle.Create();

        #if !NET_DOTS // todo: enable when this is supported
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_ComponentSafetyHandles[arrayIndex].BufferHandle, true);
        #endif

            return arrayIndex;
        }

        void ClearAllTypeArrayIndices()
        {
            for(int i=0;i<m_ComponentSafetyHandlesCount;++i)
                m_TypeArrayIndices[m_ComponentSafetyHandles[i].TypeIndex & TypeManager.ClearFlagsMask] = NullTypeIndex;
            m_ComponentSafetyHandlesCount = 0;
        }

        public void OnCreate()
        {
            m_TypeArrayIndices = (ushort*)UnsafeUtility.Malloc(sizeof(ushort) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemSet(m_TypeArrayIndices, 0xFF, sizeof(ushort)*kMaxTypes);

            m_ComponentSafetyHandles = (ComponentSafetyHandle*) UnsafeUtility.Malloc(sizeof(ComponentSafetyHandle) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear(m_ComponentSafetyHandles, sizeof(ComponentSafetyHandle) * kMaxTypes);

            m_TempSafety = AtomicSafetyHandle.Create();
            m_ComponentSafetyHandlesCount = 0;
        }

        public AtomicSafetyHandle ExclusiveTransactionSafety;

        public void CompleteAllJobsAndInvalidateArrays()
        {
            if (m_ComponentSafetyHandlesCount == 0)
                return;

            Profiler.BeginSample("InvalidateArrays");
            for (var i = 0; i != m_ComponentSafetyHandlesCount; i++)
            {
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].SafetyHandle);
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].BufferHandle);
            }

            for (var i = 0; i != m_ComponentSafetyHandlesCount; i++)
            {
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].SafetyHandle);
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].BufferHandle);
            }

            ClearAllTypeArrayIndices();
            Profiler.EndSample();
        }

        public void Dispose()
        {
            for (var i = 0; i < m_ComponentSafetyHandlesCount; i++)
            {
                var res0 = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_ComponentSafetyHandles[i].SafetyHandle);
                var res1 = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_ComponentSafetyHandles[i].BufferHandle);

                if (res0 == EnforceJobResult.DidSyncRunningJobs || res1 == EnforceJobResult.DidSyncRunningJobs)
                    Debug.LogError(
                        "Disposing EntityManager but a job is still running against the ComponentData. It appears the job has not been registered with JobComponentSystem.AddDependency.");
            }

            AtomicSafetyHandle.Release(m_TempSafety);

            UnsafeUtility.Free(m_TypeArrayIndices, Allocator.Persistent);
            UnsafeUtility.Free(m_ComponentSafetyHandles, Allocator.Persistent);
            m_ComponentSafetyHandles = null;
        }

        public void PreDisposeCheck()
        {
            for (var i = 0; i < m_ComponentSafetyHandlesCount; i++)
            {
                var res0 = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_ComponentSafetyHandles[i].SafetyHandle);
                var res1 = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_ComponentSafetyHandles[i].BufferHandle);
                if (res0 == EnforceJobResult.DidSyncRunningJobs || res1 == EnforceJobResult.DidSyncRunningJobs)
                    Debug.LogError(
                        "Disposing EntityManager but a job is still running against the ComponentData. It appears the job has not been registered with JobComponentSystem.AddDependency.");
            }
        }

        public void CompleteWriteDependency(int type)
        {
            var withoutFlags = type & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex == NullTypeIndex)
                return;
            
            AtomicSafetyHandle.CheckReadAndThrow(m_ComponentSafetyHandles[arrayIndex].SafetyHandle);
            AtomicSafetyHandle.CheckReadAndThrow(m_ComponentSafetyHandles[arrayIndex].BufferHandle);
        }

        public void CompleteReadAndWriteDependency(int type)
        {
            var withoutFlags = type & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex == NullTypeIndex)
                return;

            AtomicSafetyHandle.CheckWriteAndThrow(m_ComponentSafetyHandles[arrayIndex].SafetyHandle);
            AtomicSafetyHandle.CheckWriteAndThrow(m_ComponentSafetyHandles[arrayIndex].BufferHandle);
        }
        
        public AtomicSafetyHandle GetEntityManagerSafetyHandle()
        {
            var handle = m_ComponentSafetyHandles[GetTypeArrayIndex(EntityTypeIndex)].SafetyHandle;
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
            return handle;
        }

        public AtomicSafetyHandle GetSafetyHandle(int type, bool isReadOnly)
        {
            if (TypeManager.IsZeroSized(type))
                return GetEntityManagerSafetyHandle();

            var arrayIndex = GetTypeArrayIndex(type);

            var handle = m_ComponentSafetyHandles[arrayIndex].SafetyHandle;
            if (isReadOnly)
                AtomicSafetyHandle.UseSecondaryVersion(ref handle);

            return handle;
        }

        public AtomicSafetyHandle GetBufferSafetyHandle(int type)
        {
            Assert.IsTrue(TypeManager.IsBuffer(type));
            var arrayIndex = GetTypeArrayIndex(type);
            return m_ComponentSafetyHandles[arrayIndex].BufferHandle;
        }

        public void BeginExclusiveTransaction()
        {
            for (var i = 0; i != m_ComponentSafetyHandlesCount; i++)
            {
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].SafetyHandle);
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].BufferHandle);
            }

            for (var i = 0; i != m_ComponentSafetyHandlesCount; i++)
            {
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].SafetyHandle);
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].BufferHandle);
            }

            ExclusiveTransactionSafety = AtomicSafetyHandle.Create();
            ClearAllTypeArrayIndices();
        }

        public void EndExclusiveTransaction()
        {
            var res = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(ExclusiveTransactionSafety);
            if (res != EnforceJobResult.AllJobsAlreadySynced)
                //@TODO: Better message
                Debug.LogError("ExclusiveEntityTransaction job has not been registered");
        }
        
        struct ComponentSafetyHandle
        {
            public AtomicSafetyHandle SafetyHandle;
            public AtomicSafetyHandle BufferHandle;
            public int                TypeIndex;
        }

        AtomicSafetyHandle m_TempSafety;
    }
}
#endif

