using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
namespace Unity.Entities
{
    unsafe static class SystemDependencySafetyUtility
    {
        internal static string CheckSafetyAfterUpdate(object system, ref UnsafeIntList readingSystems, ref UnsafeIntList writingSystems, ComponentDependencyManager* dependencyManager)
        {
            // Check that all reading and writing jobs are a dependency of the output job, to
            // catch systems that forget to add one of their jobs to the dependency graph.
            //
            // Note that this check is not strictly needed as we would catch the mistake anyway later,
            // but checking it here means we can flag the system that has the mistake, rather than some
            // other (innocent) system that is doing things correctly.

            //@TODO: It is not ideal that we call m_SafetyManager.GetDependency,
            //       it can result in JobHandle.CombineDependencies calls.
            //       Which seems like debug code might have side-effects

            string dependencyError = null;
            for (var index = 0; index < readingSystems.Length && dependencyError == null; index++)
            {
                var type = readingSystems.Ptr[index];
                dependencyError = CheckJobDependencies(system, type, dependencyManager);
            }

            for (var index = 0; index < writingSystems.Length && dependencyError == null; index++)
            {
                var type = writingSystems.Ptr[index];
                dependencyError = CheckJobDependencies(system, type, dependencyManager);
            }

            if (dependencyError != null)
                EmergencySyncAllJobs(ref readingSystems, ref writingSystems, dependencyManager);

            return dependencyError;
        }

        static bool IsSystemV1(object system)
        {
            return system is JobComponentSystem;
        }

        static string CheckJobDependencies(object system, int type, ComponentDependencyManager* dependencyManager)
        {
            var h = dependencyManager->Safety.GetSafetyHandle(type, true);

            var readerCount = AtomicSafetyHandle.GetReaderArray(h, 0, IntPtr.Zero);
            JobHandle* readers = stackalloc JobHandle[readerCount];

            AtomicSafetyHandle.GetReaderArray(h, readerCount, (IntPtr) readers);

            for (var i = 0; i < readerCount; ++i)
            {
                if (!dependencyManager->HasReaderOrWriterDependency(type, readers[i]))
                {
                    if (IsSystemV1(system))
                        return $"The system {system.GetType()} reads {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetReaderName(h, i)} but that type was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.";
                    else
                        return $"The system {system.GetType()} reads {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetReaderName(h, i)} but that type was not assigned to the Dependency property. To ensure correct behavior of other systems, the job or a dependency must be assigned to the Dependency property before returning from the OnUpdate method.";
                }

            }

            if (!dependencyManager->HasReaderOrWriterDependency(type, AtomicSafetyHandle.GetWriter(h)))
            {
                if (IsSystemV1(system))
                    return $"The system {system.GetType()} writes {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetWriterName(h)} but that was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.";
                else
                    return $"The system {system.GetType()} writes {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetWriterName(h)} but that type was not assigned to the Dependency property. To ensure correct behavior of other systems, the job or a dependency must be assigned to the Dependency property before returning from the OnUpdate method.";
            }

            return null;
        }

        static void EmergencySyncAllJobs(ref UnsafeIntList readingSystems, ref UnsafeIntList writingSystems, ComponentDependencyManager* dependencyManager)
        {
            for (int i = 0;i != readingSystems.Length;i++)
            {
                int type = readingSystems.Ptr[i];
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(dependencyManager->Safety.GetSafetyHandle(type, true));
            }

            for (int i = 0;i != writingSystems.Length;i++)
            {
                int type = writingSystems.Ptr[i];
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(dependencyManager->Safety.GetSafetyHandle(type, true));
            }
        }
    }
}
#endif