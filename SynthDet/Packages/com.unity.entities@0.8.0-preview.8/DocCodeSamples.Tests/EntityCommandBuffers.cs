using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

// The files in this namespace are used to compile/test the code samples in the documentation.
// Snippets used in entity_command_buffer.md
namespace Doc.CodeSamples.Tests
{
    #region ecb_concurrent
    struct Lifetime : IComponentData
    {
        public byte Value;
    }
    
    class LifetimeSystem : SystemBase
    {
        EndSimulationEntityCommandBufferSystem m_EndSimulationEcbSystem;
        protected override void OnCreate()
        {
            base.OnCreate();
            // Find the ECB system once and store it for later usage
            m_EndSimulationEcbSystem = World
                .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // Acquire an ECB and convert it to a concurrent one to be able
            // to use it from a parallel job.
            var ecb = m_EndSimulationEcbSystem.CreateCommandBuffer().ToConcurrent();
            Entities
                .ForEach((Entity entity, int entityInQueryIndex, ref Lifetime lifetime) =>
                {
                    // Track the lifetime of an entity and destroy it once
                    // the lifetime reaches zero
                    if (lifetime.Value == 0)
                    {
                        // pass the entityInQueryIndex to the operation so
                        // the ECB can play back the commands in the right
                        // order
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                    }
                    else
                    {
                        lifetime.Value -= 1;
                    }
                }).ScheduleParallel();
            
            // Make sure that the ECB system knows about our job
            m_EndSimulationEcbSystem.AddJobHandleForProducer(this.Dependency);
        }
    }
    #endregion
}
