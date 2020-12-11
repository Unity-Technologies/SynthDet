#region stateful-example
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;


public struct GeneralPurposeComponentA : IComponentData
{
    public bool IsAlive;
}

public struct StateComponentB : ISystemStateComponentData
{
    public int State;
}

public class StatefulSystem : JobComponentSystem
{
    private EntityQuery m_newEntities;
    private EntityQuery m_activeEntities;
    private EntityQuery m_destroyedEntities;
    private EntityCommandBufferSystem m_ECBSource;

    protected override void OnCreate()
    {
        // Entities with GeneralPurposeComponentA but not StateComponentB
        m_newEntities = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {ComponentType.ReadOnly<GeneralPurposeComponentA>()},
            None = new ComponentType[] {ComponentType.ReadWrite<StateComponentB>()}
        });

        // Entities with both GeneralPurposeComponentA and StateComponentB
        m_activeEntities = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[]
            {
                ComponentType.ReadWrite<GeneralPurposeComponentA>(),
                ComponentType.ReadOnly<StateComponentB>()
            }
        });

        // Entities with StateComponentB but not GeneralPurposeComponentA
        m_destroyedEntities = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {ComponentType.ReadWrite<StateComponentB>()},
            None = new ComponentType[] {ComponentType.ReadOnly<GeneralPurposeComponentA>()}
        });

        m_ECBSource = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

#pragma warning disable 618
    struct NewEntityJob : IJobForEachWithEntity<GeneralPurposeComponentA>
    {
        public EntityCommandBuffer.Concurrent ConcurrentECB;

        public void Execute(Entity entity, int jobIndex, [ReadOnly] ref GeneralPurposeComponentA gpA)
        {
            // Add an ISystemStateComponentData instance
            ConcurrentECB.AddComponent<StateComponentB>(jobIndex, entity, new StateComponentB() {State = 1});
        }
    }

    struct ProcessEntityJob : IJobForEachWithEntity<GeneralPurposeComponentA>
    {
        public EntityCommandBuffer.Concurrent ConcurrentECB;

        public void Execute(Entity entity, int jobIndex, ref GeneralPurposeComponentA gpA)
        {
            // Process entity, possibly setting IsAlive false --
            // In which case, destroy the entity
            if (!gpA.IsAlive)
            {
                ConcurrentECB.DestroyEntity(jobIndex, entity);
            }
        }
    }

    struct CleanupEntityJob : IJobForEachWithEntity<StateComponentB>
    {
        public EntityCommandBuffer.Concurrent ConcurrentECB;

        public void Execute(Entity entity, int jobIndex, [ReadOnly] ref StateComponentB state)
        {
            // This system is responsible for removing any ISystemStateComponentData instances it adds
            // Otherwise, the entity is never truly destroyed.
            ConcurrentECB.RemoveComponent<StateComponentB>(jobIndex, entity);
        }
    }
#pragma warning restore 618

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var newEntityJob = new NewEntityJob()
        {
            ConcurrentECB = m_ECBSource.CreateCommandBuffer().ToConcurrent()
        };
        var newJobHandle = newEntityJob.ScheduleSingle(m_newEntities, inputDependencies);
        m_ECBSource.AddJobHandleForProducer(newJobHandle);

        var processEntityJob = new ProcessEntityJob()
            {ConcurrentECB = m_ECBSource.CreateCommandBuffer().ToConcurrent()};
        var processJobHandle = processEntityJob.Schedule(m_activeEntities, newJobHandle);
        m_ECBSource.AddJobHandleForProducer(processJobHandle);

        var cleanupEntityJob = new CleanupEntityJob()
        {
            ConcurrentECB = m_ECBSource.CreateCommandBuffer().ToConcurrent()
        };
        var cleanupJobHandle = cleanupEntityJob.ScheduleSingle(m_destroyedEntities, processJobHandle);
        m_ECBSource.AddJobHandleForProducer(cleanupJobHandle);

        return cleanupJobHandle;
    }

    protected override void OnDestroy()
    {
        // Implement OnDestroy to cleanup any resources allocated by this system.
        // (This simplified example does not allocate any resources.)
    }
}
#endregion