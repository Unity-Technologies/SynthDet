
using Unity.Entities.UniversalDelegates;

namespace Doc.CodeSamples.Tests
{
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Transforms;
    using Unity.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Mathematics;
    using Random = Unity.Mathematics.Random;

    #region entities-foreach-example
    class ApplyVelocitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .ForEach((ref Translation translation,
                          in Velocity velocity) =>
                {
                    translation.Value += velocity.Value;
                })
                .Schedule();
        }
    }
    #endregion
    #region job-with-code-example
    public class RandomSumJob : SystemBase
    {
        private uint seed = 1;

        protected override void OnUpdate()
        {
            Random randomGen = new Random(seed++);
            NativeArray<float> randomNumbers
                = new NativeArray<float>(500, Allocator.TempJob);

            Job.WithCode(() =>
            {
                for (int i = 0; i < randomNumbers.Length; i++)
                {
                    randomNumbers[i] = randomGen.NextFloat();
                }
            }).Schedule();

            // To get data out of a job, you must use a NativeArray
            // even if there is only one value
            NativeArray<float> result
                = new NativeArray<float>(1, Allocator.TempJob);

            Job.WithCode(() =>
            {
                for (int i = 0; i < randomNumbers.Length; i++)
                {
                    result[0] += randomNumbers[i];
                }
            }).Schedule();

            // This completes the scheduled jobs to get the result immediately, but for
            // better efficiency you should schedule jobs early in the frame with one 
            // system and get the results late in the frame with a different system.
            this.CompleteDependency();
            UnityEngine.Debug.Log("The sum of "
                                  + randomNumbers.Length + " numbers is " + result[0]);

            randomNumbers.Dispose();
            result.Dispose();
        }
    }

    #endregion

    //Used to verify the BuffersByEntity example (not shown in docs)
    public class MakeData : SystemBase
    {
        protected override void OnCreate()
        {
            var sum = 0;
            for (int i = 0; i < 100; i++)
            {
                var ent = EntityManager.CreateEntity(typeof(IntBufferElement));
                var buff = EntityManager.GetBuffer<IntBufferElement>(ent).Reinterpret<int>();
                for (int j = 0; j < 5; j++)
                {
                    buff.Add(j);
                    sum += j;
                }
            }

            UnityEngine.Debug.Log("Sum should equal " + sum);
        }

        protected override void OnUpdate()
        {
        }
    }

    public struct IntBufferData : IBufferElementData
    {
        public int Value;
    }

    #region dynamicbuffer
    public class BufferSum : SystemBase
    {
        private EntityQuery query;

        //Schedules the two jobs with a dependency between them
        protected override void OnUpdate()
        {
            //The query variable can be accessed here because we are
            //using WithStoreEntityQueryInField(query) in the entities.ForEach below
            int entitiesInQuery = query.CalculateEntityCount();

            //Create a native array to hold the intermediate sums
            //(one element per entity)
            NativeArray<int> intermediateSums
                = new NativeArray<int>(entitiesInQuery, Allocator.TempJob);

            //Schedule the first job to add all the buffer elements
            Entities
                .ForEach((int entityInQueryIndex, in DynamicBuffer<IntBufferData> buffer) =>
                {
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        intermediateSums[entityInQueryIndex] += buffer[i].Value;
                    }
                })
                .WithStoreEntityQueryInField(ref query)
                .WithName("IntermediateSums")
                .ScheduleParallel(); // Execute in parallel for each chunk of entities

            //Schedule the second job, which depends on the first
            Job
                .WithCode(() =>
                {
                    int result = 0;
                    for (int i = 0; i < intermediateSums.Length; i++)
                    {
                        result += intermediateSums[i];
                    }
                    //Not burst compatible:
                    Debug.Log("Final sum is " + result);
                })
                .WithDeallocateOnJobCompletion(intermediateSums)
                .WithoutBurst()
                .WithName("FinalSum")
                .Schedule(); // Execute on a single, background thread
        }
    }
    #endregion

    public struct Source : IComponentData
    {
        public int Value;
    }
    public struct Destination : IComponentData
    {
        public int Value;
    }

    public class WithAllExampleSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            #region entity-query
            Entities.WithAll<LocalToWorld>()
                .WithAny<Rotation, Translation, Scale>()
                .WithNone<LocalToParent>()
                .ForEach((ref Destination outputData, in Source inputData) =>
                {
                    /* do some work */
                })
                .Schedule();
            #endregion
        }
    }

    public struct Data : IComponentData
    {
        public float Value;
    }
    public class WithStoreQuerySystem : SystemBase
    {
        #region store-query
        private EntityQuery query;
        protected override void OnUpdate()
        {
            int dataCount = query.CalculateEntityCount();
            NativeArray<float> dataSquared
                = new NativeArray<float>(dataCount, Allocator.Temp);
            Entities
                .WithStoreEntityQueryInField(ref query)
                .ForEach((int entityInQueryIndex, in Data data) =>
                    {
                        dataSquared[entityInQueryIndex] = data.Value * data.Value;
                    })
                .ScheduleParallel();

            Job
                .WithCode(() =>
                {
                    //Use dataSquared array...
                    var v = dataSquared[dataSquared.Length -1];
                })
                .WithDeallocateOnJobCompletion(dataSquared)
                .Schedule();
        }
        #endregion
    }

    public class WithChangeExampleSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            #region with-change-filter
            Entities
                .WithChangeFilter<Source>()
                .ForEach((ref Destination outputData,
                    in Source inputData) =>
                {
                    /* Do work */
                })
                .ScheduleParallel();
            #endregion
        }
    }

    public struct Cohort : ISharedComponentData
    {
        public int Value;
    }
    public struct DisplayColor : IComponentData
    {
        public int Value;
    }

    public class ColorTable
    {
        public static DisplayColor GetNextColor(int current){return new DisplayColor();}
    }

    #region with-shared-component
    public class ColorCycleJob : SystemBase
    {
        protected override void OnUpdate()
        {
            List<Cohort> cohorts = new List<Cohort>();
            EntityManager.GetAllUniqueSharedComponentData<Cohort>(cohorts);
            foreach (Cohort cohort in cohorts)
            {
                DisplayColor newColor = ColorTable.GetNextColor(cohort.Value);
                Entities.WithSharedComponentFilter(cohort)
                    .ForEach((ref DisplayColor color) => { color = newColor; })
                    .ScheduleParallel();
            }
        }
    }
    #endregion

    public class ReadWriteModExample : SystemBase
    {
        protected override void OnUpdate()
        {
            #region read-write-modifiers
            Entities.ForEach(
                    (ref Destination outputData,
                        in Source inputData) =>
                    {
                        outputData.Value = inputData.Value;
                    })
                .ScheduleParallel();
            #endregion
        }
    }

    #region basic-ecb
    public class MyJobSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World
                .DefaultGameObjectInjectionWorld
                .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer.Concurrent commandBuffer
                = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

            //.. The rest of the job system code
        }
    }
    #endregion

    public struct Movement:IComponentData
    {
        public float3 Value;
    }

    public class EFESystem : SystemBase
    {
        protected override void OnUpdate()
        {

            #region lambda-params
            Entities.ForEach(
                (Entity entity,
                 int entityInQueryIndex,
                 ref Translation translation,
                 in Movement move) => {/* .. */})
            #endregion
                .Run();
        }


    }
}

namespace Doc.CodeSamples.Tests
{
    #region full-ecb-pt-one
    // ParticleSpawner.cs
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Transforms;

    public struct Velocity : IComponentData
    {
        public float3 Value;
    }

    public struct TimeToLive : IComponentData
    {
        public float LifeLeft;
    }

    public class ParticleSpawner : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
            commandBufferSystem = World
                .DefaultGameObjectInjectionWorld
                .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer.Concurrent commandBufferCreate
                = commandBufferSystem.CreateCommandBuffer().ToConcurrent();
            EntityCommandBuffer.Concurrent commandBufferCull
                = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

            float dt = Time.DeltaTime;
            Random rnd = new Random();
            rnd.InitState((uint) (dt * 100000));


            JobHandle spawnJobHandle = Entities
                .ForEach((int entityInQueryIndex,
                          in SpawnParticles spawn,
                          in LocalToWorld center) =>
                {
                    int spawnCount = spawn.Rate;
                    for (int i = 0; i < spawnCount; i++)
                    {
                        Entity spawnedEntity = commandBufferCreate
                            .Instantiate(entityInQueryIndex,
                                         spawn.ParticlePrefab);

                        LocalToWorld spawnedCenter = center;
                        Translation spawnedOffset = new Translation()
                        {
                            Value = center.Position +
                                    rnd.NextFloat3(-spawn.Offset, spawn.Offset)
                        };
                        Velocity spawnedVelocity = new Velocity()
                        {
                            Value = rnd.NextFloat3(-spawn.MaxVelocity, spawn.MaxVelocity)
                        };
                        TimeToLive spawnedLife = new TimeToLive()
                        {
                            LifeLeft = spawn.Lifetime
                        };

                        commandBufferCreate.SetComponent(entityInQueryIndex,
                                                         spawnedEntity,
                                                         spawnedCenter);
                        commandBufferCreate.SetComponent(entityInQueryIndex,
                                                         spawnedEntity,
                                                         spawnedOffset);
                        commandBufferCreate.AddComponent(entityInQueryIndex,
                                                         spawnedEntity,
                                                         spawnedVelocity);
                        commandBufferCreate.AddComponent(entityInQueryIndex,
                                                         spawnedEntity,
                                                         spawnedLife);
                    }
                })
                .WithName("ParticleSpawning")
                .Schedule(this.Dependency);

            JobHandle MoveJobHandle = Entities
                .ForEach((ref Translation translation, in Velocity velocity) =>
                {
                    translation = new Translation()
                    {
                        Value = translation.Value + velocity.Value * dt
                    };
                })
                .WithName("MoveParticles")
                .Schedule(spawnJobHandle);

            JobHandle cullJobHandle = Entities
                .ForEach((Entity entity, int entityInQueryIndex, ref TimeToLive life) =>
                {
                    life.LifeLeft -= dt;
                    if (life.LifeLeft < 0)
                        commandBufferCull.DestroyEntity(entityInQueryIndex, entity);
                })
                .WithName("CullOldEntities")
                .Schedule(this.Dependency);

            this.Dependency
                = JobHandle.CombineDependencies(MoveJobHandle, cullJobHandle);

            commandBufferSystem.AddJobHandleForProducer(spawnJobHandle);
            commandBufferSystem.AddJobHandleForProducer(cullJobHandle);
        }
    }
    #endregion
}

namespace Doc.CodeSamples.Tests
{
    #region full-ecb-pt-two
    // SpawnParticles.cs
    using Unity.Entities;
    using Unity.Mathematics;

    [GenerateAuthoringComponent]
    public struct SpawnParticles : IComponentData
    {
        public Entity ParticlePrefab;
        public int Rate;
        public float3 Offset;
        public float3 MaxVelocity;
        public float Lifetime;
    }
    #endregion
}