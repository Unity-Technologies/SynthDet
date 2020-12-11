using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace Doc.CodeSamples.Tests
{
    #region declare-chunk-component
    public struct ChunkComponentA : IComponentData
    {
        public float Value;
    }
    #endregion

    #region full-chunk-example
    public class ChunkComponentExamples : SystemBase
    {
        private EntityQuery ChunksWithChunkComponentA;
        protected override void OnCreate()
        {
            EntityQueryDesc ChunksWithComponentADesc = new EntityQueryDesc()
            {
                All = new ComponentType[] { ComponentType.ChunkComponent<ChunkComponentA>() }
            };
            ChunksWithChunkComponentA = GetEntityQuery(ChunksWithComponentADesc);
        }

        [BurstCompile]
        struct ChunkComponentCheckerJob : IJobChunk
        {
            public ArchetypeChunkComponentType<ChunkComponentA> ChunkComponentATypeInfo;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var compValue = chunk.GetChunkComponentData(ChunkComponentATypeInfo);
                //...
                var squared = compValue.Value * compValue.Value;
                chunk.SetChunkComponentData(ChunkComponentATypeInfo,
                            new ChunkComponentA() { Value = squared });
            }
        }

        protected override void OnUpdate()
        {
            var job = new ChunkComponentCheckerJob()
            {
                ChunkComponentATypeInfo = GetArchetypeChunkComponentType<ChunkComponentA>()
            };
            this.Dependency = job.Schedule(ChunksWithChunkComponentA, this.Dependency);
        }
    }
    #endregion

    #region aabb-chunk-component
    public struct ChunkAABB : IComponentData
    {
        public AABB Value;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdateAABBSystem))]
    public class AddAABBSystem : SystemBase
    {
        EntityQuery queryWithoutChunkComponent;
        protected override void OnCreate()
        {
            queryWithoutChunkComponent = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[]  { ComponentType.ReadOnly<LocalToWorld>() },
                None = new ComponentType[] { ComponentType.ChunkComponent<ChunkAABB>() }
            });
        }
        protected override void OnUpdate()
        {
            // This is a structural change and a sync point
            EntityManager.AddChunkComponentData<ChunkAABB>(queryWithoutChunkComponent, new ChunkAABB());
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UpdateAABBSystem : SystemBase
    {
        EntityQuery queryWithChunkComponent;
        protected override void OnCreate()
        {
            queryWithChunkComponent = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] { ComponentType.ReadOnly<LocalToWorld>(),
                                            ComponentType.ChunkComponent<ChunkAABB>()}
            });
        }
        [BurstCompile]
        struct AABBJob : IJobChunk
        {
            [ReadOnly] public ArchetypeChunkComponentType<LocalToWorld> LocalToWorldTypeInfo;
            public ArchetypeChunkComponentType<ChunkAABB> ChunkAABBTypeInfo;
            public uint L2WChangeVersion;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                bool chunkHasChanges = chunk.DidChange(LocalToWorldTypeInfo, L2WChangeVersion);

                if (!chunkHasChanges)
                    return; // early out if the chunk transforms haven't changed

                NativeArray<LocalToWorld> transforms = chunk.GetNativeArray<LocalToWorld>(LocalToWorldTypeInfo);
                UnityEngine.Bounds bounds = new UnityEngine.Bounds();
                bounds.center = transforms[0].Position;
                for (int i = 1; i < transforms.Length; i++)
                {
                    bounds.Encapsulate(transforms[i].Position);
                }
                chunk.SetChunkComponentData(ChunkAABBTypeInfo, new ChunkAABB() { Value = bounds.ToAABB() });
            }
        }

        protected override void OnUpdate()
        {
            var job = new AABBJob()
            {
                LocalToWorldTypeInfo = GetArchetypeChunkComponentType<LocalToWorld>(true),
                ChunkAABBTypeInfo = GetArchetypeChunkComponentType<ChunkAABB>(false),
                L2WChangeVersion = this.LastSystemVersion
            };
            this.Dependency = job.Schedule(queryWithChunkComponent, this.Dependency);
        }
    }
    #endregion

    //snippets
    public class ChunkComponentSnippets : SystemBase
    {
        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }

        private void snippets()
        {
            #region component-list-chunk-component
            ComponentType[] compTypes = {ComponentType.ChunkComponent<ChunkComponentA>(),
                             ComponentType.ReadOnly<GeneralPurposeComponentA>()};
            Entity entity = EntityManager.CreateEntity(compTypes);
            #endregion

            #region em-snippet
            EntityManager.AddChunkComponentData<ChunkComponentA>(entity);
            #endregion

            #region desc-chunk-component
            EntityQueryDesc ChunksWithoutComponentADesc = new EntityQueryDesc()
            {
                None = new ComponentType[] { ComponentType.ChunkComponent<ChunkComponentA>() }
            };
            EntityQuery ChunksWithoutChunkComponentA = GetEntityQuery(ChunksWithoutComponentADesc);

            EntityManager.AddChunkComponentData<ChunkComponentA>(ChunksWithoutChunkComponentA,
                    new ChunkComponentA() { Value = 4 });
            #endregion

            #region use-chunk-component
            EntityQueryDesc ChunksWithChunkComponentADesc = new EntityQueryDesc()
            {
                All = new ComponentType[] { ComponentType.ChunkComponent<ChunkComponentA>() }
            };
            #endregion

            #region archetype-chunk-component
            EntityArchetype ArchetypeWithChunkComponent = EntityManager.CreateArchetype(
                            ComponentType.ChunkComponent(typeof(ChunkComponentA)),
                            ComponentType.ReadWrite<GeneralPurposeComponentA>());
            Entity newEntity = EntityManager.CreateEntity(ArchetypeWithChunkComponent);
            #endregion
            {
                EntityQuery ChunksWithChunkComponentA = null;
                #region read-chunk-component
                NativeArray<ArchetypeChunk> chunks = ChunksWithChunkComponentA.CreateArchetypeChunkArray(Allocator.TempJob);
                foreach (var chunk in chunks)
                {
                    var compValue = EntityManager.GetChunkComponentData<ChunkComponentA>(chunk);
                    //..
                }
                chunks.Dispose();
                #endregion
            }
            
            #region read-entity-chunk-component
            if (EntityManager.HasChunkComponent<ChunkComponentA>(entity))
            {
                ChunkComponentA chunkComponentValue = EntityManager.GetChunkComponentData<ChunkComponentA>(entity);
            }
            #endregion
            
            {
                ArchetypeChunk chunk;
                #region set-chunk-component
                EntityManager.SetChunkComponentData<ChunkComponentA>(chunk,
                        new ChunkComponentA() { Value = 7 });
                #endregion
            }

            #region set-entity-chunk-component
            var entityChunk = EntityManager.GetChunk(entity);
            EntityManager.SetChunkComponentData<ChunkComponentA>(entityChunk,
                                new ChunkComponentA() { Value = 8 });
            #endregion

        }
    }
}