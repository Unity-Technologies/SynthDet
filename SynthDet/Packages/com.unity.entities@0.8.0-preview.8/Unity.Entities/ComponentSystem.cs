using System;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities
{
    /// <summary>
    /// An abstract class to implement in order to create a system.
    /// </summary>
    /// <remarks>Implement a ComponentSystem subclass for systems that perform their work on the main thread or that
    /// use Jobs not specifically optimized for ECS. To use the ECS-specific Jobs, such as <see cref="IJobForEach{T0}"/> or
    /// <see cref="IJobChunk"/>, implement <seealso cref="JobComponentSystem"/> instead.</remarks>
    public abstract partial class ComponentSystem : ComponentSystemBase
    {
        EntityCommandBuffer m_DeferredEntities;
        EntityQueryCache m_EntityQueryCache;

        /// <summary>
        /// This system's <see cref="EntityCommandBuffer"/>.
        /// </summary>
        /// <value>A queue of entity-related commands to playback after the system's update function finishes.</value>
        /// <remarks>When iterating over a collection of entities with <see cref="ComponentSystem.Entities"/>, the system
        /// prohibits structural changes that would invalidate that collection. Such changes include creating and
        /// destroying entities, adding or removing components, and changing the value of shared components.
        ///
        /// Instead, add structural change commands to this PostUpdateCommands command buffer. The system executes
        /// commands added to this command buffer in order after this system's <see cref="OnUpdate"/> function returns.
        /// PostUpdateCommands are created with a PlaybackPolicy.SinglePlayback and RecordingMode.Managed.</remarks>
        public EntityCommandBuffer PostUpdateCommands => m_DeferredEntities;

        /// <summary>
        /// Initializes this system's internal cache of <see cref="EntityQuery"/> objects to the specified number of
        /// queries.
        /// </summary>
        /// <param name="cacheSize">The initial capacity of the system's <see cref="EntityQuery"/> array.</param>
        /// <remarks>A system's entity query cache expands automatically as you add additional queries. However,
        /// initializing the cache to the correct size when you initialize a system is more efficient and avoids
        /// unnecessary, garbage-collected memory allocations.</remarks>
        protected internal void InitEntityQueryCache(int cacheSize) =>
            m_EntityQueryCache = new EntityQueryCache(cacheSize);

        internal EntityQueryCache EntityQueryCache => m_EntityQueryCache;

        internal EntityQueryCache GetOrCreateEntityQueryCache()
            => m_EntityQueryCache ?? (m_EntityQueryCache = new EntityQueryCache());

        /// <summary>
        /// This system's query builder object.
        /// </summary>
        /// <value>Use to select and iterate over entities.</value>
        protected internal EntityQueryBuilder Entities => new EntityQueryBuilder(this);

        void BeforeOnUpdate()
        {
            BeforeUpdateVersioning();
            CompleteDependencyInternal();

            m_DeferredEntities = new EntityCommandBuffer(Allocator.TempJob, -1, PlaybackPolicy.SinglePlayback);
        }

        void AfterOnUpdate()
        {
            AfterUpdateVersioning();

            JobHandle.ScheduleBatchedJobs();

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            try
            {
                m_DeferredEntities.Playback(EntityManager);
            }
            catch (Exception e)
            {
                m_DeferredEntities.Dispose();
                var error = $"{e.Message}\nEntityCommandBuffer was recorded in {GetType()} using PostUpdateCommands.\n" + e.StackTrace;
                throw new System.ArgumentException(error);
            }
        #else
            m_DeferredEntities.Playback(EntityManager);
        #endif
            m_DeferredEntities.Dispose();
        }

        public sealed override void Update()
        {
#if ENABLE_PROFILER
            using (m_ProfilerMarker.Auto())
#endif

            {
                if (Enabled && ShouldRunSystem())
                {
                    if (!m_PreviouslyEnabled)
                    {
                        m_PreviouslyEnabled = true;
                        OnStartRunning();
                    }

                    BeforeOnUpdate();

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var oldExecutingSystem = ms_ExecutingSystem;
                    ms_ExecutingSystem = this;
            #endif

                    try
                    {
                        OnUpdate();
                    }
                    finally
                    {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
                        ms_ExecutingSystem = oldExecutingSystem;
            #endif
                        AfterOnUpdate();
                    }
                }
                else if (m_PreviouslyEnabled)
                {
                    m_PreviouslyEnabled = false;
                    OnStopRunningInternal();
                }            
            }
        }

        internal sealed override void OnBeforeCreateInternal(World world)
        {
            base.OnBeforeCreateInternal(world);
        }

        internal sealed override void OnBeforeDestroyInternal()
        {
            base.OnBeforeDestroyInternal();
        }

        /// <summary>Implement OnUpdate to perform the major work of this system.</summary>
        /// <remarks>
        /// The system invokes OnUpdate once per frame on the main thread when any of this system's
        /// EntityQueries match existing entities, or if the system has the <see cref="AlwaysUpdateSystemAttribute"/>.
        /// </remarks>
        /// <seealso cref="ComponentSystemBase.ShouldRunSystem"/>
        protected abstract void OnUpdate();
    }
}
