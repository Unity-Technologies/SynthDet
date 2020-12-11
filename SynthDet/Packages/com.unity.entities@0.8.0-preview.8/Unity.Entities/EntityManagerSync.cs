using System;
using Unity.Assertions;

namespace Unity.Entities
{
    public sealed unsafe partial class EntityManager
    {
        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------
        
        //@TODO: Not clear to me what this method is really for...
        /// <summary>
        /// Waits for all Jobs to complete.
        /// </summary>
        /// <remarks>Calling CompleteAllJobs() blocks the main thread until all currently running Jobs finish.</remarks>
        public void CompleteAllJobs()
        {
            DependencyManager->CompleteAllJobsAndInvalidateArrays();
        }

        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

        internal void BeforeStructuralChange()
        {
            m_EntityDataAccess.BeforeStructuralChange();
        }
    }
}
