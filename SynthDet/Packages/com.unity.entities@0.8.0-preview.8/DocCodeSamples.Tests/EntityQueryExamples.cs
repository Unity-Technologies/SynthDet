using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Doc.CodeSamples.Tests
{
    #region singleton-type-example
    public struct Singlet : IComponentData
    {
        public int Value;
    }
    #endregion

    public class EntityQueryExamples : SystemBase
    {
        void queryFromList()
        {
            #region query-from-list
            EntityQuery query = GetEntityQuery(typeof(Rotation),
                ComponentType.ReadOnly<RotationSpeed>());
            #endregion
        }

        void queryFromDescription()
        {
            #region query-from-description
            EntityQueryDesc description = new EntityQueryDesc
            {
                None = new ComponentType[]
                {
                    typeof(Frozen)
                },
                All = new ComponentType[]
                {
                    typeof(Rotation),
                    ComponentType.ReadOnly<RotationSpeed>()
                }
            };
            EntityQuery query = GetEntityQuery(description);
            #endregion
        }

        protected override void OnUpdate()
        {
            var queryForSingleton = EntityManager.CreateEntityQuery(typeof(Singlet));
            var entityManager = EntityManager;
            #region create-singleton
            Entity singletonEntity = entityManager.CreateEntity(typeof(Singlet));
            entityManager.SetComponentData(singletonEntity, new Singlet { Value = 1 });
            #endregion

            #region set-singleton
            queryForSingleton.SetSingleton<Singlet>(new Singlet {Value = 1});
            #endregion
        }
    }
}