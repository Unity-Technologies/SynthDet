using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities
{
    /// <summary>
    /// A system that provides <seealso cref="EntityCommandBuffer"/> objects for other systems.
    /// </summary>
    /// <remarks>
    /// Each system that uses the EntityCommandBuffer provided by a command buffer system must call
    /// <see cref="CreateCommandBuffer"/> to create its own command buffer instance. This buffer system executes each of
    /// these separate command buffers in the order that you created them. The commands are executed during this system's
    /// <see cref="OnUpdate"/> function.
    ///
    /// When you write to a command buffer from a Job, you must add the <see cref="JobHandle"/> of that Job to the buffer
    /// system's dependency list with <see cref="AddJobHandleForProducer"/>.
    ///
    /// If you write to a command buffer from a Job that runs in
    /// parallel (and this includes both <see cref="IJobForEach{T0}"/> and <see cref="IJobChunk"/>), you must use the
    /// concurrent version of the command buffer (<seealso cref="EntityCommandBuffer.ToConcurrent"/>).
    ///
    /// Executing the commands in an EntityCommandBuffer invokes the corresponding functions of the
    /// <see cref="EntityManager"/>. Any structural change, such as adding or removing entities, adding or removing
    /// components from entities, or changing shared component values, creates a sync-point in your application.
    /// At a sync point, all Jobs accessing entity components must complete before new Jobs can start. Such sync points
    /// make it difficult for the Job scheduler to fully utilize available computing power. To avoid sync points,
    /// you should use as few entity command buffer systems as possible.
    ///
    /// The default ECS <see cref="World"/> code creates a <see cref="ComponentSystemGroup"/> setup with
    /// three main groups, <see cref="InitializationSystemGroup"/>, <see cref="SimulationSystemGroup"/>, and
    /// <see cref="PresentationSystemGroup"/>. Each of these main groups provides an existing EntityCommandBufferSystem
    /// executed at the start and the end of other, child systems.
    ///
    /// Note that unused command buffers systems do not create sync points because there are no commands to execute and
    /// thus no structural changes created.
    ///
    /// The EntityCommandBufferSystem class is abstract, so you must implement a subclass to create your own
    /// entity command buffer system. However, none of its methods are abstract, so you do not need to implement
    /// your own logic. Typically, you create an EntityCommandBufferSystem subclass to create a named buffer system
    /// for other systems to use and update it at an appropriate place in a custom <see cref="ComponentSystemGroup"/>
    /// setup.</remarks>
    public abstract class EntityCommandBufferSystem : ComponentSystem
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        List<EntityCommandBuffer> m_PendingBuffers;
        internal List<EntityCommandBuffer> PendingBuffers => m_PendingBuffers;
#else
        NativeList<EntityCommandBuffer> m_PendingBuffers;
        internal NativeList<EntityCommandBuffer> PendingBuffers
        {
            get { return m_PendingBuffers; }
        }
#endif

        JobHandle m_ProducerHandle;

        /// <summary>
        /// Creates an <seealso cref="EntityCommandBuffer"/> and adds it to this system's list of command buffers.
        /// </summary>
        /// <remarks>
        /// This buffer system executes its list of command buffers during its <see cref="OnUpdate"/> function in the
        /// order you created the command buffers.
        ///
        /// If you write to a command buffer in a Job, you must add the
        /// Job as a dependency of this system by calling <see cref="AddJobHandleForProducer"/>. The dependency ensures
        /// that the buffer system waits for the Job to complete before executing the command buffer.
        ///
        /// If you write to a command buffer from a parallel Job, such as <see cref="IJobForEach{T0}"/> or
        /// <see cref="IJobChunk"/>, you must use the concurrent version of the command buffer, provided by
        /// <see cref="EntityCommandBuffer.Concurrent"/>.
        /// </remarks>
        /// <returns>A command buffer that will be executed by this system.</returns>
        public EntityCommandBuffer CreateCommandBuffer()
        {
            var cmds = new EntityCommandBuffer(Allocator.TempJob, -1, PlaybackPolicy.SinglePlayback);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            cmds.SystemID = ms_ExecutingSystem != null ? ms_ExecutingSystem.m_SystemID : 0;
#endif
            m_PendingBuffers.Add(cmds);

            return cmds;
        }

        /// <summary>
        /// Adds the specified JobHandle to this system's list of dependencies.
        /// </summary>
        /// <remarks>
        /// When you write to a command buffer from a Job, you must add the <see cref="JobHandle"/> of that Job to this buffer
        /// system's dependency list by calling this function. Otherwise, the buffer system could execute the commands
        /// currently in the command buffer while the writing Job is still in progress.
        /// </remarks>
        /// <param name="producerJob">The JobHandle of a Job which this buffer system should wait for before playing back its
        /// pending command buffers.</param>
        /// <example>
        /// The following example illustrates how to use one of the default <see cref="EntityCommandBuffer"/> systems.
        /// The code selects all entities that have one custom component, in this case, `AsyncProcessInfo`, and
        /// processes each entity in the `Execute()` function of an <see cref="IJobForEachWithEntity{T0}"/> Job (the
        /// actual process is not shown since that part of the example is hypothetical). After processing, the Job
        /// uses an EntityCommandBuffer to remove the `ProcessInfo` component and add an `ProcessCompleteTag`
        /// component. Another system could use the `ProcessCompleteTag` to find entities that represent the end
        /// results of the process.
        /// <code>
        /// public struct ProcessInfo: IComponentData{ public float Value; }
        /// public struct ProcessCompleteTag : IComponentData{}
        ///
        /// public class AsyncProcessJobSystem : JobComponentSystem
        /// {
        ///     [BurstCompile]
        ///     public struct ProcessInBackgroundJob : IJobForEachWithEntity&lt;ProcessInfo&gt;
        ///     {
        ///         [ReadOnly]
        ///         public EntityCommandBuffer.Concurrent ConcurrentCommands;
        ///
        ///         public void Execute(Entity entity, int index, [ReadOnly] ref ProcessInfo info)
        ///         {
        ///             // Process based on the ProcessInfo component,
        ///             // then remove ProcessInfo and add a ProcessCompleteTag...
        ///
        ///             ConcurrentCommands.RemoveComponent&lt;ProcessInfo&gt;(index, entity);
        ///             ConcurrentCommands.AddComponent(index, entity, new ProcessCompleteTag());
        ///         }
        ///     }
        ///
        ///     protected override JobHandle OnUpdate(JobHandle inputDeps)
        ///     {
        ///         var job = new ProcessInBackgroundJob();
        ///
        ///         var ecbSystem =
        ///             World.GetOrCreateSystem&lt;EndSimulationEntityCommandBufferSystem&gt;();
        ///         job.ConcurrentCommands = ecbSystem.CreateCommandBuffer().ToConcurrent();
        ///
        ///         var handle = job.Schedule(this, inputDeps);
        ///         ecbSystem.AddJobHandleForProducer(handle);
        ///
        ///         return handle;
        ///     }
        /// }
        /// </code>
        /// </example>
        public void AddJobHandleForProducer(JobHandle producerJob)
        {
            m_ProducerHandle = JobHandle.CombineDependencies(m_ProducerHandle, producerJob);
        }

        /// <summary>
        /// Initializes this command buffer system.
        /// </summary>
        /// <remarks>If you override this method, you should call `base.OnCreate()` to retain the default
        /// initialization logic.</remarks>
        protected override void OnCreate()
        {
            base.OnCreate();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_PendingBuffers = new List<EntityCommandBuffer>();
#else
            m_PendingBuffers = new NativeList<EntityCommandBuffer>(Allocator.Persistent);
#endif
        }

        /// <summary>
        /// Destroys this system, executing any pending command buffers first.
        /// </summary>
        /// <remarks>If you override this method, you should call `base.OnDestroy()` to retain the default
        /// destruction logic.</remarks>
        protected override void OnDestroy()
        {
            FlushPendingBuffers(false);
            m_PendingBuffers.Clear();

#if !ENABLE_UNITY_COLLECTIONS_CHECKS
            m_PendingBuffers.Dispose();
#endif

            base.OnDestroy();
        }

        /// <summary>
        /// Executes the command buffers in this system in the order they were created.
        /// </summary>
        /// <remarks>If you override this method, you should call `base.OnUpdate()` to retain the default
        /// update logic.</remarks>
        protected override void OnUpdate()
        {
            FlushPendingBuffers(true);
            m_PendingBuffers.Clear();
        }

        internal void FlushPendingBuffers(bool playBack)
        {
            m_ProducerHandle.Complete();
            m_ProducerHandle = new JobHandle();

            int length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            length = m_PendingBuffers.Count;
#else
            length = m_PendingBuffers.Length;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            List<string> playbackErrorLog = null;
            bool completeAllJobsBeforeDispose = false;
#endif
            for (int i = 0; i < length; ++i)
            {
                var buffer = m_PendingBuffers[i];
                if (!buffer.IsCreated)
                {
                    continue;
                }
                if (playBack)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    try
                    {
                        buffer.Playback(EntityManager);
                    }
                    catch (Exception e)
                    {
                        var system = GetSystemFromSystemID(World, buffer.SystemID);
                        var systemType = system != null ? system.GetType().ToString() : "Unknown";
                        var error = $"{e.Message}\nEntityCommandBuffer was recorded in {systemType} and played back in {GetType()}.\n" + e.StackTrace;
                        if (playbackErrorLog == null)
                        {
                            playbackErrorLog = new List<string>();
                        }
                        playbackErrorLog.Add(error);
                        completeAllJobsBeforeDispose = true;
                    }
#else
                    buffer.Playback(EntityManager);
#endif
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                try
                {
                    if (completeAllJobsBeforeDispose)
                    {
                        // If we get here, there was an error during playback (potentially a race condition on the
                        // buffer itself), and we should wait for all jobs writing to this command buffer to complete before attempting
                        // to dispose of the command buffer to prevent a potential race condition.
                        buffer.WaitForWriterJobs();
                        completeAllJobsBeforeDispose = false;
                    }
                    buffer.Dispose();
                }
                catch (Exception e)
                {
                    var system = GetSystemFromSystemID(World, buffer.SystemID);
                    var systemType = system != null ? system.GetType().ToString() : "Unknown";
                    var error = $"{e.Message}\nEntityCommandBuffer was recorded in {systemType} and disposed in {GetType()}.\n" + e.StackTrace;
                    if (playbackErrorLog == null)
                    {
                        playbackErrorLog = new List<string>();
                    }
                    playbackErrorLog.Add(error);
                }
#else
                buffer.Dispose();
#endif
                m_PendingBuffers[i] = buffer;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (playbackErrorLog != null)
            {
#if !NET_DOTS
                var exceptionMessage = new StringBuilder();
                foreach (var err in playbackErrorLog)
                {
                    exceptionMessage.AppendLine(err);
                }
#else
                var exceptionMessage = "";
                foreach (var err in playbackErrorLog)
                {
                    exceptionMessage += err;
                    exceptionMessage += '\n';
                }
#endif

                throw new System.ArgumentException(exceptionMessage.ToString());
            }
#endif
        }
    }
}