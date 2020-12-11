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
    //Snippets used in chunk_iteration_job.md
    public struct Rotation : IComponentData
    {
        public quaternion Value;
    }

    public struct RotationSpeed : IComponentData
    {
        public float RadiansPerSecond;
    }

    #region rotationspeedsystem
    public class RotationSpeedSystem : SystemBase
    {
        private EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(ComponentType.ReadOnly<Rotation>(),
                                     ComponentType.ReadOnly<RotationSpeed>());
            //...
        }
        #endregion

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ArchetypeChunkComponentType<Rotation> RotationType;
            [ReadOnly] public ArchetypeChunkComponentType<RotationSpeed> RotationSpeedType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                #region chunkiteration

                var chunkRotations = chunk.GetNativeArray(RotationType);
                var chunkRotationSpeeds = chunk.GetNativeArray(RotationSpeedType);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var rotation = chunkRotations[i];
                    var rotationSpeed = chunkRotationSpeeds[i];

                    // Rotate something about its up vector at the speed given by RotationSpeed.
                    chunkRotations[i] = new Rotation
                    {
                        Value = math.mul(math.normalize(rotation.Value),
                            quaternion.AxisAngle(math.up(), rotationSpeed.RadiansPerSecond * DeltaTime))
                    };
                }

                #endregion
            }
        }

        #region schedulequery

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationType = GetArchetypeChunkComponentType<Rotation>(false),
                RotationSpeedType = GetArchetypeChunkComponentType<RotationSpeed>(true),
                DeltaTime = Time.DeltaTime
            };
            this.Dependency =  job.ScheduleParallel(m_Query, this.Dependency);
        }

        #endregion
    }


    public class RotationSpeedSystemExample2 : SystemBase
    {
        private EntityQuery m_Query;

        #region oncreate2
        protected override void OnCreate()
        {
            var queryDescription = new EntityQueryDesc()
            {
                None = new ComponentType[]
                {
                    typeof(Static)
                },
                All = new ComponentType[]
                {
                    ComponentType.ReadWrite<Rotation>(),
                    ComponentType.ReadOnly<RotationSpeed>()
                }
            };
            m_Query = GetEntityQuery(queryDescription);
        }
        #endregion

        #region speedjob

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ArchetypeChunkComponentType<Rotation> RotationType;
            [ReadOnly] public ArchetypeChunkComponentType<RotationSpeed> RotationSpeedType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                // ...
            }
        }

        #endregion

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationType = GetArchetypeChunkComponentType<Rotation>(false),
                RotationSpeedType = GetArchetypeChunkComponentType<RotationSpeed>(true),
                DeltaTime = Time.DeltaTime
            };

            this.Dependency =  job.ScheduleParallel(m_Query, this.Dependency);
        }
    }

    public class RotationSpeedSystemExample3 : SystemBase
    {
        private EntityQuery m_Query;

        #region oncreate3

        protected override void OnCreate()
        {
            var queryDescription0 = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(Rotation)}
            };

            var queryDescription1 = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(RotationSpeed)}
            };

            m_Query = GetEntityQuery(new EntityQueryDesc[] {queryDescription0, queryDescription1});
        }

        #endregion

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ArchetypeChunkComponentType<Rotation> RotationType;
            [ReadOnly] public ArchetypeChunkComponentType<RotationSpeed> RotationSpeedType;

            #region execsignature

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)

                #endregion

            {
                #region getcomponents

                var chunkRotations = chunk.GetNativeArray(RotationType);
                var chunkRotationSpeeds = chunk.GetNativeArray(RotationSpeedType);

                #endregion
            }
        }

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationType = GetArchetypeChunkComponentType<Rotation>(false),
                RotationSpeedType = GetArchetypeChunkComponentType<RotationSpeed>(true),
                DeltaTime = Time.DeltaTime
            };

            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);
        }
    }

    public struct Output : IComponentData
    {
        public float Value;
    }

    public struct InputA : IComponentData
    {
        public float Value;
    }

    public struct InputB : IComponentData
    {
        public float Value;
    }

    public class UpdateSystemExample : SystemBase
    {
        #region changefilter

        private EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadWrite<Output>(),
                ComponentType.ReadOnly<InputA>(),
                ComponentType.ReadOnly<InputB>());
            m_Query.SetChangedVersionFilter(
                new ComponentType[]
                {
                    ComponentType.ReadWrite<InputA>(),
                    ComponentType.ReadWrite<InputB>()
                });
        }

        #endregion

        #region changefilterjobstruct

        [BurstCompile]
        struct UpdateJob : IJobChunk
        {
            public ArchetypeChunkComponentType<InputA> InputAType;
            public ArchetypeChunkComponentType<InputB> InputBType;
            [ReadOnly] public ArchetypeChunkComponentType<Output> OutputType;
            public uint LastSystemVersion;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var inputAChanged = chunk.DidChange(InputAType, LastSystemVersion);
                var inputBChanged = chunk.DidChange(InputBType, LastSystemVersion);

                // If neither component changed, skip the current chunk
                if (!(inputAChanged || inputBChanged))
                    return;

                var inputAs = chunk.GetNativeArray(InputAType);
                var inputBs = chunk.GetNativeArray(InputBType);
                var outputs = chunk.GetNativeArray(OutputType);

                for (var i = 0; i < outputs.Length; i++)
                {
                    outputs[i] = new Output{ Value = inputAs[i].Value + inputBs[i].Value };
                }
            }
        }

        #endregion

        #region changefilteronupdate

        protected override void OnUpdate()
        {
            var job = new UpdateJob();

            job.LastSystemVersion = this.LastSystemVersion;

            job.InputAType = GetArchetypeChunkComponentType<InputA>(true);
            job.InputBType = GetArchetypeChunkComponentType<InputB>(true);
            job.OutputType = GetArchetypeChunkComponentType<Output>(false);
            
            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);
        }

        #endregion
    }

    #region basic-ijobchunk
    [GenerateAuthoringComponent]
    public struct Target : IComponentData
    {
        public Entity entity;
    }

    public class ChaserSystem : SystemBase
    {
        private EntityQuery query; // Initialized in Oncreate()

        [BurstCompile]
        private struct ChaserSystemJob : IJobChunk
        {
            // Read-write data in the current chunk
            public ArchetypeChunkComponentType<Translation> PositionTypeAccessor;

            // Read-only data in the current chunk
            [ReadOnly]
            public ArchetypeChunkComponentType<Target> TargetTypeAccessor;

            // Read-only data stored (potentially) in other chunks
            [ReadOnly]
            //[NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<LocalToWorld> EntityPositions;

            // Non-entity data
            public float deltaTime;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                NativeArray<Translation> positions = chunk.GetNativeArray<Translation>(PositionTypeAccessor);
                NativeArray<Target> targets = chunk.GetNativeArray<Target>(TargetTypeAccessor);

                for (int i = 0; i < positions.Length; i++)
                {
                    Entity targetEntity = targets[i].entity;
                    float3 targetPosition = EntityPositions[targetEntity].Position;
                    float3 chaserPosition = positions[i].Value;

                    float3 displacement = (targetPosition - chaserPosition);
                    positions[i] = new Translation { Value = chaserPosition + displacement * deltaTime };
                }
            }
        }

        protected override void OnCreate()
        {
            query = this.GetEntityQuery(typeof(Translation), ComponentType.ReadOnly<Target>());
        }

        protected override void OnUpdate()
        {
            var job = new ChaserSystemJob();
            job.PositionTypeAccessor = this.GetArchetypeChunkComponentType<Translation>(false);
            job.TargetTypeAccessor = this.GetArchetypeChunkComponentType<Target>(true);

            job.EntityPositions = this.GetComponentDataFromEntity<LocalToWorld>(true);
            job.deltaTime = this.Time.DeltaTime;

            this.Dependency = job.Schedule(query, this.Dependency);
        }
    }
    #endregion
}
