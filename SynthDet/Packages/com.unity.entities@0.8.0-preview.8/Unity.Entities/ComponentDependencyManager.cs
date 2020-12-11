using System;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    /// <summary>
    /// The ComponentDependencyManager maintains JobHandles for each type with any jobs that read or write those component types.
    /// ComponentSafetyHandles which is embedded maintains a safety handle for each component type registered in the TypeManager.
    /// Safety and job handles are only maintained for components that can be modified by jobs:
    /// That means only dynamic buffer components and component data that are not tag components will have valid
    /// safety and job handles. For those components the safety handle represents ReadOnly or ReadWrite access to those
    /// components as well as their change versions.
    /// The Entity type is a special case: It can not be modified by jobs and its safety handle is used to represent the
    /// entire EntityManager state. Any job reading from any part of the EntityManager must contain either a safety handle
    /// for the Entity type OR a safety handle for any other component type.
    /// Job component systems that have no other type dependencies have their JobHandles registered on the Entity type
    /// to ensure that they are completed by CompleteAllJobsAndInvalidateArrays
    /// </summary>
#if !ENABLE_SIMPLE_SYSTEM_DEPENDENCIES    
    unsafe partial struct ComponentDependencyManager
    {
        struct DependencyHandle
        {
            public JobHandle WriteFence;
            public int       NumReadFences;
            public int       TypeIndex;
        }
   
        const int              kMaxReadJobHandles = 17;
        const int              kMaxTypes = TypeManager.MaximumTypesCount;

        JobHandle*             m_JobDependencyCombineBuffer;
        int                    m_JobDependencyCombineBufferCount;
        
        // Indexed by TypeIndex
        ushort*                m_TypeArrayIndices;
        DependencyHandle*      m_DependencyHandles;
        ushort                 m_DependencyHandlesCount;
        JobHandle*             m_ReadJobFences;

        const int              EntityTypeIndex = 1;
        const ushort           NullTypeIndex = 0xFFFF;

        JobHandle              m_ExclusiveTransactionDependency;

        bool                   _IsInTransaction;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public ComponentSafetyHandles Safety;
#endif
        public int IsInForEachDisallowStructuralChange;

        ushort GetTypeArrayIndex(int typeIndex)
        {
            var withoutFlags = typeIndex & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex != NullTypeIndex)
                return arrayIndex;

            Assert.IsFalse(TypeManager.IsZeroSized(typeIndex));
            
            arrayIndex = m_DependencyHandlesCount++;
            m_TypeArrayIndices[withoutFlags] = arrayIndex;
            m_DependencyHandles[arrayIndex].TypeIndex = typeIndex;
            m_DependencyHandles[arrayIndex].NumReadFences = 0;
            m_DependencyHandles[arrayIndex].WriteFence = new JobHandle();

            return arrayIndex;
        }

        void ClearAllTypeArrayIndices()
        {
            for(int i=0;i<m_DependencyHandlesCount;++i)
                m_TypeArrayIndices[m_DependencyHandles[i].TypeIndex & TypeManager.ClearFlagsMask] = NullTypeIndex;
            m_DependencyHandlesCount = 0;
        }

        public void OnCreate()
        {
            m_TypeArrayIndices = (ushort*)UnsafeUtility.Malloc(sizeof(ushort) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemSet(m_TypeArrayIndices, 0xFF, sizeof(ushort)*kMaxTypes);

            m_ReadJobFences = (JobHandle*) UnsafeUtility.Malloc(sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear(m_ReadJobFences, sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes);

            m_DependencyHandles = (DependencyHandle*) UnsafeUtility.Malloc(sizeof(DependencyHandle) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear(m_DependencyHandles, sizeof(DependencyHandle) * kMaxTypes);

            m_JobDependencyCombineBufferCount = 4 * 1024;
            m_JobDependencyCombineBuffer = (JobHandle*) UnsafeUtility.Malloc(sizeof(DependencyHandle) * m_JobDependencyCombineBufferCount, 16, Allocator.Persistent);

            m_DependencyHandlesCount = 0;
            _IsInTransaction = false;
            IsInForEachDisallowStructuralChange = 0;
            m_ExclusiveTransactionDependency = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.OnCreate();
#endif

        }

        public void CompleteAllJobsAndInvalidateArrays()
        {
            if (m_DependencyHandlesCount != 0)
            {
                Profiler.BeginSample("CompleteAllJobs");
                for (int t = 0; t < m_DependencyHandlesCount; ++t)
                {
                    m_DependencyHandles[t].WriteFence.Complete();

                    var readFencesCount = m_DependencyHandles[t].NumReadFences;
                    var readFences = m_ReadJobFences + t * kMaxReadJobHandles;
                    for (var r = 0; r != readFencesCount; r++)
                        readFences[r].Complete();
                    m_DependencyHandles[t].NumReadFences = 0;
                }
                ClearAllTypeArrayIndices();
                Profiler.EndSample();
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteAllJobsAndInvalidateArrays();
#endif
        }

        public void Dispose()
        {
            for (var i = 0; i < m_DependencyHandlesCount; i++)
                m_DependencyHandles[i].WriteFence.Complete();

            for (var i = 0; i < m_DependencyHandlesCount * kMaxReadJobHandles; i++)
                m_ReadJobFences[i].Complete();

            UnsafeUtility.Free(m_JobDependencyCombineBuffer, Allocator.Persistent);

            UnsafeUtility.Free(m_TypeArrayIndices, Allocator.Persistent);
            UnsafeUtility.Free(m_DependencyHandles, Allocator.Persistent);
            m_DependencyHandles = null;

            UnsafeUtility.Free(m_ReadJobFences, Allocator.Persistent);
            m_ReadJobFences = null;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.Dispose();
#endif
        }

        public void PreDisposeCheck()
        {
            for (var i = 0; i < m_DependencyHandlesCount; i++)
                m_DependencyHandles[i].WriteFence.Complete();

            for (var i = 0; i < m_DependencyHandlesCount * kMaxReadJobHandles; i++)
                m_ReadJobFences[i].Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.PreDisposeCheck();
#endif
        }


        public void CompleteDependenciesNoChecks(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
            for (var i = 0; i != writerTypesCount; i++)
                CompleteReadAndWriteDependencyNoChecks(writerTypes[i]);

            for (var i = 0; i != readerTypesCount; i++)
                CompleteWriteDependencyNoChecks(readerTypes[i]);
        }

        public bool HasReaderOrWriterDependency(int type, JobHandle dependency)
        {
            var typeArrayIndex = m_TypeArrayIndices[type & TypeManager.ClearFlagsMask];
            if (typeArrayIndex == NullTypeIndex)
                return true;

            var writer = m_DependencyHandles[typeArrayIndex].WriteFence;
            if (JobHandle.CheckFenceIsDependencyOrDidSyncFence(dependency, writer))
                return true;

            var count = m_DependencyHandles[typeArrayIndex].NumReadFences;
            for (var r = 0; r < count; r++)
            {
                var reader = m_ReadJobFences[typeArrayIndex * kMaxReadJobHandles + r];
                if (JobHandle.CheckFenceIsDependencyOrDidSyncFence(dependency, reader))
                    return true;
            }

            return false;
        }

        public JobHandle GetDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (readerTypesCount * kMaxReadJobHandles + writerTypesCount > m_JobDependencyCombineBufferCount)
                throw new ArgumentException("Too many readers & writers in GetDependency");
#endif

            var count = 0;
            for (var i = 0; i != readerTypesCount; i++)
            {
                var typeArrayIndex = m_TypeArrayIndices[readerTypes[i] & TypeManager.ClearFlagsMask];
                if(typeArrayIndex != NullTypeIndex)
                    m_JobDependencyCombineBuffer[count++] = m_DependencyHandles[typeArrayIndex].WriteFence;
            }

            for (var i = 0; i != writerTypesCount; i++)
            {
                var typeArrayIndex = m_TypeArrayIndices[writerTypes[i] & TypeManager.ClearFlagsMask];
                if (typeArrayIndex == NullTypeIndex)
                    continue;

                m_JobDependencyCombineBuffer[count++] = m_DependencyHandles[typeArrayIndex].WriteFence;

                var numReadFences = m_DependencyHandles[typeArrayIndex].NumReadFences;
                for (var j = 0; j != numReadFences; j++)
                    m_JobDependencyCombineBuffer[count++] = m_ReadJobFences[typeArrayIndex * kMaxReadJobHandles + j];
            }

            return JobHandleUnsafeUtility.CombineDependencies(m_JobDependencyCombineBuffer, count);
        }

        public JobHandle AddDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount,
            JobHandle dependency)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            JobHandle* combinedDependencies = null;
            var combinedDependenciesCount = 0;
#endif
            if (readerTypesCount == 0 && writerTypesCount == 0)
            {
                ushort entityTypeArrayIndex = GetTypeArrayIndex(EntityTypeIndex);
                // if no dependency types are provided add read dependency to the Entity type
                // to ensure these jobs are still synced by CompleteAllJobsAndInvalidateArrays
                m_ReadJobFences[entityTypeArrayIndex * kMaxReadJobHandles +
                                m_DependencyHandles[entityTypeArrayIndex].NumReadFences] = dependency;
                m_DependencyHandles[entityTypeArrayIndex].NumReadFences++;

                if (m_DependencyHandles[entityTypeArrayIndex].NumReadFences == kMaxReadJobHandles)
                {
                    //@TODO: Check dynamically if the job debugger is enabled?
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    return CombineReadDependencies(entityTypeArrayIndex);
#else
                    CombineReadDependencies(entityTypeArrayIndex);
#endif
                }
                return dependency;
            }

            for (var i = 0; i != writerTypesCount; i++)
            {
                m_DependencyHandles[GetTypeArrayIndex(writerTypes[i])].WriteFence = dependency;
            }


            for (var i = 0; i != readerTypesCount; i++)
            {
                var reader = GetTypeArrayIndex(readerTypes[i]);
                m_ReadJobFences[reader * kMaxReadJobHandles + m_DependencyHandles[reader].NumReadFences] =
                    dependency;
                m_DependencyHandles[reader].NumReadFences++;

                if (m_DependencyHandles[reader].NumReadFences == kMaxReadJobHandles)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var combined = CombineReadDependencies(reader);
                    if (combinedDependencies == null)
                    {
                        JobHandle* temp = stackalloc JobHandle[readerTypesCount];
                        combinedDependencies = temp;
                    }

                    combinedDependencies[combinedDependenciesCount++] = combined;
#else
                    CombineReadDependencies(reader);
#endif
                }
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (combinedDependencies != null)
                return JobHandleUnsafeUtility.CombineDependencies(combinedDependencies, combinedDependenciesCount);
            return dependency;
#else
            return dependency;
#endif
        }

        void CompleteWriteDependencyNoChecks(int type)
        {
            var withoutFlags = type & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex != NullTypeIndex)
                m_DependencyHandles[arrayIndex].WriteFence.Complete();
        }

        void CompleteReadAndWriteDependencyNoChecks(int type)
        {
            var withoutFlags = type & TypeManager.ClearFlagsMask;
            var arrayIndex = m_TypeArrayIndices[withoutFlags];
            if (arrayIndex != NullTypeIndex)
            {
                for (var i = 0; i < m_DependencyHandles[arrayIndex].NumReadFences; ++i)
                    m_ReadJobFences[arrayIndex * kMaxReadJobHandles + i].Complete();
                m_DependencyHandles[arrayIndex].NumReadFences = 0;

                m_DependencyHandles[arrayIndex].WriteFence.Complete();
            }
        }

        public void CompleteWriteDependency(int type)
        {
            CompleteWriteDependencyNoChecks(type);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteWriteDependency(type);
#endif
        }

        public void CompleteReadAndWriteDependency(int type)
        {
            CompleteReadAndWriteDependencyNoChecks(type);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteReadAndWriteDependency(type);
#endif
        }

        JobHandle CombineReadDependencies(ushort typeArrayIndex)
        {
            var combined = JobHandleUnsafeUtility.CombineDependencies(
                m_ReadJobFences + typeArrayIndex * kMaxReadJobHandles, m_DependencyHandles[typeArrayIndex].NumReadFences);

            m_ReadJobFences[typeArrayIndex * kMaxReadJobHandles] = combined;
            m_DependencyHandles[typeArrayIndex].NumReadFences = 1;

            return combined;
        }

        public void BeginExclusiveTransaction()
        {
            if (_IsInTransaction)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.BeginExclusiveTransaction();
#endif

            _IsInTransaction = true;
            m_ExclusiveTransactionDependency = GetAllDependencies();
            ClearAllTypeArrayIndices();
        }

        public void EndExclusiveTransaction()
        {
            if (!_IsInTransaction)
                return;

            m_ExclusiveTransactionDependency.Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.EndExclusiveTransaction();
#endif
            _IsInTransaction = false;
        }

        JobHandle GetAllDependencies()
        {
            var jobHandles = new NativeArray<JobHandle>(m_DependencyHandlesCount * (kMaxReadJobHandles + 1), Allocator.Temp);

            var count = 0;
            for (var i = 0; i != m_DependencyHandlesCount; i++)
            {
                jobHandles[count++] = m_DependencyHandles[i].WriteFence;

                var numReadFences = m_DependencyHandles[i].NumReadFences;
                for (var j = 0; j != numReadFences; j++)
                    jobHandles[count++] = m_ReadJobFences[i * kMaxReadJobHandles + j];
            }

            var combined = JobHandle.CombineDependencies(jobHandles);
            jobHandles.Dispose();

            return combined;
        }
    }
#else
    unsafe partial struct ComponentDependencyManager
    {
        JobHandle              m_Dependency;
        JobHandle              m_ExclusiveTransactionDependency;
        bool                   _IsInTransaction;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public ComponentSafetyHandles Safety;
#endif
        public int IsInForEachDisallowStructuralChange;

        public void OnCreate()
        {
            m_Dependency = default;
            _IsInTransaction = false;
            IsInForEachDisallowStructuralChange = 0;
            m_ExclusiveTransactionDependency = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.OnCreate();
#endif
        }

        public void CompleteAllJobsAndInvalidateArrays()
        {
            m_Dependency.Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteAllJobsAndInvalidateArrays();
#endif
        }

        public void Dispose()
        {
            m_Dependency.Complete();
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.Dispose();
#endif
        }

        
        public void PreDisposeCheck()
        {
            m_Dependency.Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.PreDisposeCheck();
#endif
        }

        public void CompleteDependenciesNoChecks(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
            m_Dependency.Complete();
        }

        public bool HasReaderOrWriterDependency(int type, JobHandle dependency)
        {
            return JobHandle.CheckFenceIsDependencyOrDidSyncFence(dependency, m_Dependency);
        }

        public JobHandle GetDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
            return m_Dependency;
        }

        public JobHandle AddDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount, JobHandle dependency)
        {
            m_Dependency = dependency;
            return dependency;
        }

        public void CompleteWriteDependency(int type)
        {
            m_Dependency.Complete();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteWriteDependency(type);
#endif
        }

        public void CompleteReadAndWriteDependency(int type)
        {
            m_Dependency.Complete();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.CompleteReadAndWriteDependency(type);
#endif
        }

        public void BeginExclusiveTransaction()
        {
            if (_IsInTransaction)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.BeginExclusiveTransaction();
#endif

            _IsInTransaction = true;
            m_ExclusiveTransactionDependency = m_Dependency;
            m_Dependency = default;
        }

        public void EndExclusiveTransaction()
        {
            if (!_IsInTransaction)
                return;

            m_ExclusiveTransactionDependency.Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Safety.EndExclusiveTransaction();
#endif
            _IsInTransaction = false;
        }
    }
#endif
    
    // Shared code of the above two different implementation
    partial struct ComponentDependencyManager
    {
        public bool IsInTransaction => _IsInTransaction;

        public JobHandle ExclusiveTransactionDependency
        {
            get { return m_ExclusiveTransactionDependency; }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!IsInTransaction)
                    throw new InvalidOperationException(
                        "EntityManager.TransactionDependency can only after EntityManager.BeginExclusiveEntityTransaction has been called.");

                if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(m_ExclusiveTransactionDependency, value))
                    throw new InvalidOperationException(
                        "EntityManager.TransactionDependency must depend on the Entity Transaction job.");
#endif
                m_ExclusiveTransactionDependency = value;
            }
        }
    }
}
