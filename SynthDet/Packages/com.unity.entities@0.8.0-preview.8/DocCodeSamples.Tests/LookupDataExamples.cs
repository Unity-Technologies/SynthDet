using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using NUnit.Framework;

// The files in this namespace are used to compile/test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{

    #region lookup-foreach
    public class TrackingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = this.Time.DeltaTime;

            Entities
                .ForEach((ref Rotation orientation,
                          in LocalToWorld transform,
                          in Target target) =>
                {
                    // Check to make sure the target Entity still exists and has
                    // the needed component
                    if (!HasComponent<LocalToWorld>(target.entity))
                        return;

                    // Look up the entity data
                    LocalToWorld targetTransform
                        = GetComponent<LocalToWorld>(target.entity);
                    float3 targetPosition = targetTransform.Position;

                    // Calculate the rotation
                    float3 displacement = targetPosition - transform.Position;
                    float3 upReference = new float3(0, 1, 0);
                    quaternion lookRotation =
                        quaternion.LookRotationSafe(displacement, upReference);

                    orientation.Value =
                        math.slerp(orientation.Value, lookRotation, deltaTime);
                })
                .ScheduleParallel();
        }
    }
    #endregion
    #region lookup-foreach-buffer
    public struct BufferData : IBufferElementData
    {
        public float Value;
    }
    public class BufferLookupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            BufferFromEntity<BufferData> buffersOfAllEntities
                = this.GetBufferFromEntity<BufferData>(true);

            Entities
                .ForEach((ref Rotation orientation,
                          in LocalToWorld transform,
                          in Target target) =>
                {
                    // Check to make sure the target Entity still exists
                    if (!buffersOfAllEntities.Exists(target.entity))
                        return;

                    // Get a reference to the buffer
                    DynamicBuffer<BufferData> bufferOfOneEntity =
                        buffersOfAllEntities[target.entity];

                    // Use the data in the buffer
                    float avg = 0;
                    for (var i = 0; i < bufferOfOneEntity.Length; i++)
                    {
                        avg += bufferOfOneEntity[i].Value;
                    }
                    if (bufferOfOneEntity.Length > 0)
                        avg /= bufferOfOneEntity.Length;
                })
                .ScheduleParallel();
        }
    }
    #endregion
    #region lookup-ijobchunk
    public class MoveTowardsEntitySystem : SystemBase
    {
        private EntityQuery query;

        [BurstCompile]
        private struct MoveTowardsJob : IJobChunk
        {
            // Read-write data in the current chunk
            public ArchetypeChunkComponentType<Translation> PositionTypeAccessor;

            // Read-only data in the current chunk
            [ReadOnly]
            public ArchetypeChunkComponentType<Target> TargetTypeAccessor;
            
            // Read-only data stored (potentially) in other chunks
            [ReadOnly]
            public ComponentDataFromEntity<LocalToWorld> EntityPositions;

            // Non-entity data
            public float deltaTime;

            public void Execute(ArchetypeChunk chunk,
                                int chunkIndex,
                                int firstEntityIndex)
            {
                // Get arrays of the components in chunk
                NativeArray<Translation> positions
                    = chunk.GetNativeArray<Translation>(PositionTypeAccessor);
                NativeArray<Target> targets
                    = chunk.GetNativeArray<Target>(TargetTypeAccessor);

                for (int i = 0; i < positions.Length; i++)
                {
                    // Get the target Entity object
                    Entity targetEntity = targets[i].entity;

                    // Check that the target still exists
                    if (!EntityPositions.Exists(targetEntity))
                        continue;

                    // Update translation to move the chasing enitity toward the target 
                    float3 targetPosition = EntityPositions[targetEntity].Position;
                    float3 chaserPosition = positions[i].Value;

                    float3 displacement = targetPosition - chaserPosition;
                    positions[i] = new Translation
                    {
                        Value = chaserPosition + displacement * deltaTime
                    };
                }
            }
        }

        protected override void OnCreate()
        {
            // Select all entities that have Translation and Target Componentx
            query = this.GetEntityQuery
                    (
                        typeof(Translation),
                        ComponentType.ReadOnly<Target>()
                    );
        }

        protected override void OnUpdate()
        {
            // Create the job
            var job = new MoveTowardsJob();

            // Set the chunk data accessors
            job.PositionTypeAccessor =
                this.GetArchetypeChunkComponentType<Translation>(false);
            job.TargetTypeAccessor =
                this.GetArchetypeChunkComponentType<Target>(true);

            // Set the component data lookup field
            job.EntityPositions = this.GetComponentDataFromEntity<LocalToWorld>(true);

            // Set non-ECS data fields
            job.deltaTime = this.Time.DeltaTime;

            // Schedule the job using Dependency property
            this.Dependency = job.Schedule(query, this.Dependency);
        }
    }
    #endregion

    public class Snippets : SystemBase
    {
        private EntityQuery query;
        protected override void OnCreate()
        {
            // Select all entities that have Translation and Target Componentx
            query = this.GetEntityQuery(typeof(Translation), ComponentType.ReadOnly<Target>());
        }

        [BurstCompile]
        private struct ChaserSystemJob : IJobChunk
        {
            // Read-write data in the current chunk
            public ArchetypeChunkComponentType<Translation> PositionTypeAccessor;

            // Read-only data in the current chunk
            [ReadOnly]
            public ArchetypeChunkComponentType<Target> TargetTypeAccessor;

            // Read-only data stored (potentially) in other chunks
            #region lookup-ijobchunk-declare
            [ReadOnly]
            public ComponentDataFromEntity<LocalToWorld> EntityPositions;
            #endregion

            // Non-entity data
            public float deltaTime;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                // Get arrays of the components in chunk
                NativeArray<Translation> positions
                    = chunk.GetNativeArray<Translation>(PositionTypeAccessor);
                NativeArray<Target> targets
                    = chunk.GetNativeArray<Target>(TargetTypeAccessor);

                for (int i = 0; i < positions.Length; i++)
                {
                    // Get the target Entity object
                    Entity targetEntity = targets[i].entity;

                    // Check that the target still exists
                    if (!EntityPositions.Exists(targetEntity))
                        continue;

                    // Update translation to move the chasing enitity toward the target
                    #region lookup-ijobchunk-read
                    float3 targetPosition = EntityPositions[targetEntity].Position;
                    #endregion
                    float3 chaserPosition = positions[i].Value;

                    float3 displacement = targetPosition - chaserPosition;
                    positions[i] = new Translation { Value = chaserPosition + displacement * deltaTime };
                }
            }
        }

        protected override void OnUpdate()
        {
            // Create the job
            #region lookup-ijobchunk-set
            var job = new ChaserSystemJob();
            job.EntityPositions = this.GetComponentDataFromEntity<LocalToWorld>(true);
            #endregion
            // Set the chunk data accessors
            job.PositionTypeAccessor = this.GetArchetypeChunkComponentType<Translation>(false);
            job.TargetTypeAccessor = this.GetArchetypeChunkComponentType<Target>(true);


            // Set non-ECS data fields
            job.deltaTime = this.Time.DeltaTime;

            // Schedule the job using Dependency property
            this.Dependency = job.Schedule(query, this.Dependency);
        }
    }
}
