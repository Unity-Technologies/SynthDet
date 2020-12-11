using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    public sealed unsafe partial class EntityManager
    {
        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates an entity having the specified archetype.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the matching archetype that has
        /// enough space.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="archetype">The archetype for the new entity.</param>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        public Entity CreateEntity(EntityArchetype archetype)
        {
            return m_EntityDataAccess.CreateEntity(archetype);
        }

        /// <summary>
        /// Creates an entity having components of the specified types.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates the entity in the first available chunk with the matching archetype that has
        /// enough space.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="types">The types of components to add to the new entity.</param>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        [StructuralChangeMethod]
        public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        [StructuralChangeMethod]
        public Entity CreateEntity()
        {
            BeforeStructuralChange();
            Entity entity;
            EntityComponentStore->CreateEntities(m_EntityDataAccess.GetEntityOnlyArchetype().Archetype, &entity, 1);
            ManagedComponentStore.Playback(ref EntityComponentStore->ManagedChangesTracker);
            return entity;
        }
        
        /// <summary>
        /// Creates a set of entities of the specified archetype.
        /// </summary>
        /// <remarks>Fills the [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html)
        /// object assigned to the `entities` parameter with the Entity objects of the created entities. Each entity
        /// has the components specified by the <see cref="EntityArchetype"/> object assigned
        /// to the `archetype` parameter. The EntityManager adds these entities to the <see cref="World"/> entity list. Use the
        /// Entity objects in the array for further processing, such as setting the component values.</remarks>
        /// <param name="archetype">The archetype defining the structure for the new entities.</param>
        /// <param name="entities">An array to hold the Entity objects needed to access the new entities.
        /// The length of the array determines how many entities are created.</param>
        [StructuralChangeMethod]
        public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            m_EntityDataAccess.CreateEntity(archetype, entities);
        }

        /// <summary>
        /// Creates a set of entities of the specified archetype.
        /// </summary>
        /// <remarks>Creates a [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities,
        /// each of which has the components specified by the <see cref="EntityArchetype"/> object assigned
        /// to the `archetype` parameter. The EntityManager adds these entities to the <see cref="World"/> entity list.</remarks>
        /// <param name="archetype">The archetype defining the structure for the new entities.</param>
        /// <param name="entityCount">The number of entities to create with the specified archetype.</param>
        /// <param name="allocator">How the created native array should be allocated.</param>
        /// <returns>
        /// A [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities
        /// with the given archetype.
        /// </returns>
        [StructuralChangeMethod]
        public NativeArray<Entity> CreateEntity(EntityArchetype archetype, int entityCount, Allocator allocator)
        {
            var entities = new NativeArray<Entity>(entityCount, allocator);
            m_EntityDataAccess.CreateEntity(archetype, entities);
            
            return entities;
        }
        
        /// <summary>
        /// Destroy all entities having a common set of component types.
        /// </summary>
        /// <remarks>Since entities in the same chunk share the same component structure, this function effectively destroys
        /// the chunks holding any entities identified by the `entityQueryFilter` parameter.</remarks>
        /// <param name="entityQueryFilter">Defines the components an entity must have to qualify for destruction.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(EntityQuery entityQuery)
        {
            Unity.Entities.EntityComponentStore.AssertValidEntityQuery(entityQuery, EntityComponentStore);
            DestroyEntity(entityQuery._QueryData->MatchingArchetypes, entityQuery._Filter);
        }

        /// <summary>
        /// Destroys all entities in an array.
        /// </summary>
        /// <remarks>
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before destroying the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">An array containing the Entity objects of the entities to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(NativeArray<Entity> entities)
        {
            DestroyEntityInternal((Entity*) entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        /// <summary>
        /// Destroys all entities in a slice of an array.
        /// </summary>
        /// <remarks>
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before destroying the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="entities">The slice of an array containing the Entity objects of the entities to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(NativeSlice<Entity> entities)
        {
            DestroyEntityInternal((Entity*) entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        /// <summary>
        /// Destroys an entity.
        /// </summary>
        /// <remarks>
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before destroying the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="entity">The Entity object of the entity to destroy.</param>
        [StructuralChangeMethod]
        public void DestroyEntity(Entity entity)
        {
            DestroyEntityInternal(&entity, 1);
        }

        /// <summary>
        /// Clones an entity.
        /// </summary>
        /// <remarks>
        /// The new entity has the same archetype and component values as the original.
        ///
        /// If the source entity was converted from a prefab and thus has a <see cref="LinkedEntityGroup"/> component, 
        /// the entire group is cloned as a new set of entities.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone</param>
        /// <returns>The Entity object for the new entity.</returns>
        [StructuralChangeMethod]
        public Entity Instantiate(Entity srcEntity)
        {
            Entity entity;
            InstantiateInternal(srcEntity, &entity, 1);
            return entity;
        }

        /// <summary>
        /// Makes multiple clones of an entity.
        /// </summary>
        /// <remarks>
        /// The new entities have the same archetype and component values as the original.
        ///
        /// If the source entity has a <see cref="LinkedEntityGroup"/> component, the entire group is cloned as a new
        /// set of entities.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating these entities and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone.</param>
        /// <param name="outputEntities">An array to receive the Entity objects of the root entity in each clone.
        /// The length of this array determines the number of clones.</param>
        [StructuralChangeMethod]
        public void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            InstantiateInternal(srcEntity, (Entity*) outputEntities.GetUnsafePtr(), outputEntities.Length);
        }

        /// <summary>
        /// Makes multiple clones of an entity.
        /// </summary>
        /// <remarks>
        /// The new entities have the same archetype and component values as the original.
        /// 
        /// If the source entity has a <see cref="LinkedEntityGroup"/> component, the entire group is cloned as a new
        /// set of entities.
        /// 
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating these entities and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="srcEntity">The entity to clone.</param>
        /// <param name="instanceCount">The number of entities to instantiate with the same components as the source entity.</param>
        /// <param name="allocator">How the created native array should be allocated.</param>
        /// <returns>A [NativeArray](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) of entities.</returns>
        [StructuralChangeMethod]
        public NativeArray<Entity> Instantiate(Entity srcEntity, int instanceCount, Allocator allocator)
        {
            var entities = new NativeArray<Entity>(instanceCount, allocator);
            InstantiateInternal(srcEntity, (Entity*) entities.GetUnsafePtr(), entities.Length);            
            
            return entities;
        }

        /// <summary>
        /// Creates a set of chunks containing the specified number of entities having the specified archetype.
        /// </summary>
        /// <remarks>
        /// The EntityManager creates enough chunks to hold the required number of entities.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating these chunks and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="archetype">The archetype for the chunk and entities.</param>
        /// <param name="chunks">An empty array to receive the created chunks.</param>
        /// <param name="entityCount">The number of entities to create.</param>
        [StructuralChangeMethod]
        public void CreateChunk(EntityArchetype archetype, NativeArray<ArchetypeChunk> chunks, int entityCount)
        {
            Unity.Entities.EntityComponentStore.AssertValidArchetype(EntityComponentStore, archetype);
            BeforeStructuralChange();

            EntityComponentStore->CreateChunks(archetype.Archetype, (ArchetypeChunk*) chunks.GetUnsafePtr(), chunks.Length, entityCount);
            ManagedComponentStore.Playback(ref EntityComponentStore->ManagedChangesTracker);
        }


        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

        internal void DestroyEntityInternal(Entity* entities, int count)
        {
            m_EntityDataAccess.DestroyEntityInternal(entities, count);
        }

        internal void InstantiateInternal(Entity srcEntity, Entity* outputEntities, int count)
        {
            m_EntityDataAccess.InstantiateInternal(srcEntity, outputEntities, count);
        }

        void DestroyEntity(UnsafeMatchingArchetypePtrList archetypeList, EntityQueryFilter filter)
        {
            m_EntityDataAccess.DestroyEntity(archetypeList, filter);
        }
    }
}
