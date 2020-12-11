using Unity.Entities.UniversalDelegates;

namespace Doc.CodeSamples.SyBase.Tests
{
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Transforms;
    using Unity.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Mathematics;

    #region basic-system

    public struct Position : IComponentData
    {
        public float3 Value;
    }

    public struct Velocity : IComponentData
    {
        public float3 Value;
    }

    public class ECSSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Local variable captured in ForEach
            float dT = Time.DeltaTime; 

            Entities
                .WithName("Update_Displacement")
                .ForEach(
                    (ref Position position, in Velocity velocity) =>
                    {
                        position = new Position()
                        {
                            Value = position.Value + velocity.Value * dT
                        };
                    }
                )
                .ScheduleParallel();
        }
    }

    #endregion

    public class EntitiesBasicExample : SystemBase
    {
        protected override void OnUpdate()
        {
            float dT = Time.DeltaTime; // Captured variable

            #region entities-foreach-basic

            Entities
                .WithName("Update_Position") // Shown in error messages and profiler
                .WithAll<LocalToWorld>() // Require the LocalToWorld component
                .ForEach(
                    // Write to Displacement (ref), read Velocity (in)
                    (ref Position position, in Velocity velocity) =>
                    {
                        //Execute for each selected entity
                        position = new Position()
                        {
                            // dT is a captured variable
                            Value = position.Value + velocity.Value * dT
                        };
                    }
                )
                .ScheduleParallel(); // Schedule as a parallel job

            #endregion
        }
    }

    #region basic-job

    public class JobSystem : SystemBase
    {
        NativeArray<int> EndlessSequence;

        protected override void OnUpdate()
        {
            NativeArray<int> sequence = EndlessSequence; // Can only capture local variables
            if (!sequence.IsCreated)
            {
                EndlessSequence = new NativeArray<int>(1000, Allocator.Persistent);
                sequence[sequence.Length - 2] = 1;
                sequence[sequence.Length - 1] = 1;
            }
            Job
                .WithName("Fibonacci_Job")
                .WithCode(() =>
                {
                    sequence[0] = sequence[sequence.Length - 2];
                    sequence[1] = sequence[sequence.Length - 1];
                    for (int i = 3; i < sequence.Length; i++)
                    {
                        sequence[i] = sequence[i - 1] + sequence[i - 2];
                    }
                })
                .Schedule();
        }

        protected override void OnDestroy()
        {
            if (EndlessSequence.IsCreated)
                EndlessSequence.Dispose();
        }
    }

    #endregion

    public struct WritableComponent : IComponentData
    {
    }

    public struct ReadonlyComponent : IComponentData
    {
    }

    public class LambdaParamsEx : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                #region lambda-params
                .ForEach((Entity entity,
                    int entityInQueryIndex,
                    ref WritableComponent aReadwriteComponent,
                    in ReadonlyComponent aReadonlyComponent) =>
                    {
                        /*..*/
                    })
                #endregion
                .ScheduleParallel();
        }
    }

    public struct AComponent : IComponentData
    {
    }

    public struct AnotherComponent : IComponentData
    {
    }

    public class SimpleDependencyManagement : SystemBase
    {
        #region simple-dependency

        protected override void OnUpdate()
        {
            Entities
                .WithName("ForEach_Job_One")
                .ForEach((ref AComponent c) =>
                {
                    /*...*/
                })
                .ScheduleParallel();

            Entities
                .WithName("ForEach_Job_Two")
                .ForEach((ref AnotherComponent c) =>
                {
                    /*...*/
                })
                .ScheduleParallel();

            Job
                .WithName("Job_Three")
                .WithCode(() =>
                {
                    /*...*/
                })
                .Schedule();
        }

        #endregion
    }

    public class ManualDependencyManagement : SystemBase
    {
        #region manual-dependency

        protected override void OnUpdate()
        {
            JobHandle One = Entities
                .WithName("ForEach_Job_One")
                .ForEach((ref AComponent c) =>
                {
                    /*...*/
                })
                .ScheduleParallel(this.Dependency);

            JobHandle Two = Entities
                .WithName("ForEach_Job_Two")
                .ForEach((ref AnotherComponent c) =>
                {
                    /*...*/
                })
                .ScheduleParallel(this.Dependency);

            JobHandle intermediateDependencies =
                JobHandle.CombineDependencies(One, Two);

            JobHandle finalDependency = Job
                .WithName("Job_Three")
                .WithCode(() =>
                {
                    /*...*/
                })
                .Schedule(intermediateDependencies);

            this.Dependency = finalDependency;
        }

        #endregion
    }
}