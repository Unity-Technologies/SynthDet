using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;

namespace Unity.Entities
{
    public class EntityQueryDescValidationException : Exception
    {
        public EntityQueryDescValidationException(string message) : base(message)
        {

        }
    }

    /// <summary>
    /// Describes a query used to find archetypes with specific components.
    /// </summary>
    /// <remarks>
    /// A query description combines components in the All, Any, and None sets according to the
    /// following rules:
    ///
    /// * All - Includes archetypes that have every component in this set
    /// * Any - Includes archetypes that have at least one component in this set
    /// * None - Excludes archetypes that have any component in this set
    ///
    /// For example, given entities with the following components:
    ///
    /// * Player has components: Position, Rotation, Player
    /// * Enemy1 has components: Position, Rotation, Melee
    /// * Enemy2 has components: Position, Rotation, Ranger
    ///
    /// The query description below gives you all of the archetypes that:
    /// have any of [Melee or Ranger], AND have none of [Player], AND have all of [Position and Rotation]
    /// <code>
    /// new EntityQueryDesc {
    ///     Any = new ComponentType[] {typeof(Melee), typeof(Ranger)},
    ///     None = new ComponentType[] {typeof(Player)},
    ///     All = new ComponentType[] {typeof(Position), typeof(Rotation)}
    /// }
    /// </code>
    ///
    /// In other words, the query description selects the Enemy1 and Enemy2 entities, but not the Player entity.
    ///
    /// Use an EntityQueryDesc object to create an <see cref="EntityQuery"/> object. In a system, call
    /// <see cref="ComponentSystemBase.GetEntityQuery(EntityQueryDesc[])"/>; otherwise, call
    /// <see cref="EntityManager.CreateEntityQuery(EntityQueryDesc[])"/>.
    /// </remarks>
    public class EntityQueryDesc
    {
        /// <summary>
        /// Include archetypes that contain at least one (but possibly more) of the
        /// components in the `Any` list.
        /// </summary>
        public ComponentType[] Any = Array.Empty<ComponentType>();
        /// <summary>
        /// Exclude archetypes that contain any of the
        /// components in the `None` list.
        /// </summary>
        public ComponentType[] None = Array.Empty<ComponentType>();
        /// <summary>
        /// Include archetypes that contain all of the
        /// components in the `All` list.
        /// </summary>
        public ComponentType[] All = Array.Empty<ComponentType>();
        /// <summary>
        /// Specialized options for the query.
        /// </summary>
        /// <remarks>
        /// You should not need to set these options for most queries.
        ///
        /// Options is a bit mask; use the bitwise OR operator to combine multiple options.
        /// </remarks>
        public EntityQueryOptions Options = EntityQueryOptions.Default;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateComponentTypes(ComponentType[] componentTypes, ref NativeArray<int> allComponentTypeIds, ref int curComponentTypeIndex)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var componentType = componentTypes[i];
                allComponentTypeIds[curComponentTypeIndex++] = componentType.TypeIndex;
                if (componentType.AccessModeType == ComponentType.AccessMode.Exclude)
                    throw new ArgumentException("EntityQueryDesc cannot contain Exclude Component types");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void Validate()
        {
            // Determine the number of ComponentTypes contained in the filters
            var itemCount = None.Length + All.Length + Any.Length;

            // Project all the ComponentType Ids of None, All, Any queryDesc filters into the same array to identify duplicated later on
            // Also, check that queryDesc doesn't contain any ExcludeComponent...

            var allComponentTypeIds = new NativeArray<int>(itemCount, Allocator.Temp);
            var curComponentTypeIndex = 0;
            ValidateComponentTypes(None, ref allComponentTypeIds, ref curComponentTypeIndex);
            ValidateComponentTypes(All, ref allComponentTypeIds, ref curComponentTypeIndex);
            ValidateComponentTypes(Any, ref allComponentTypeIds, ref curComponentTypeIndex);

            // Check for duplicate, only if necessary
            if (itemCount > 1)
            {
                // Sort the Ids to have identical value adjacent
                allComponentTypeIds.Sort();

                // Check for identical values
                var refId = allComponentTypeIds[0];
                for (int i = 1; i < allComponentTypeIds.Length; i++)
                {
                    var curId = allComponentTypeIds[i];
                    if (curId == refId)
                    {
#if NET_DOTS
                        throw new EntityQueryDescValidationException(
                            $"EntityQuery contains a filter with duplicate component type index {curId}.  Queries can only contain a single component of a given type in a filter.");
#else
                        var compType = TypeManager.GetType(curId);
                        throw new EntityQueryDescValidationException(
                            $"EntityQuery contains a filter with duplicate component type name {compType.Name}.  Queries can only contain a single component of a given type in a filter.");
#endif
                    }

                    refId = curId;
                }
            }
        }
    }

    /// <summary>
    /// The bit flags to use for the <see cref="EntityQueryDesc.Options"/> field.
    /// </summary>
    [Flags]
    public enum EntityQueryOptions
    {
        /// <summary>
        /// No options specified.
        /// </summary>
        Default = 0,
        /// <summary>
        /// The query does not exclude the special <see cref="Prefab"/> component.
        /// </summary>
        IncludePrefab = 1,
        /// <summary>
        /// The query does not exclude the special <see cref="Disabled"/> component.
        /// </summary>
        IncludeDisabled = 2,
        /// <summary>
        /// The query filters selected entities based on the
        /// <see cref="WriteGroupAttribute"/> settings of the components specified in the query description.
        /// </summary>
        FilterWriteGroup = 4,
    }

    /// <summary>
    /// A EntityQueryMask provides a fast check of whether an entity would be selected by an entity query.
    /// </summary>
    /// <remarks>
    /// Create an entity query mask using the <seealso cref="EntityManager.GetEntityQueryMask"/> function.
    /// 
    /// You can create up to 1024 unique EntityQueryMasks instances in a given progrom. Entity query masks
    /// cannot be created from entity queries with filters.
    /// </remarks>
    /// <seealso cref="EntityManager.GetEntityQueryMask"/>
    public unsafe struct EntityQueryMask
    {
        internal byte Index;
        internal byte Mask;

        [NativeDisableUnsafePtrRestriction]
        internal readonly EntityComponentStore* EntityComponentStore;

        internal EntityQueryMask(byte index, byte mask, EntityComponentStore* entityComponentStore)
        {
            Index = index;
            Mask = mask;
            EntityComponentStore = entityComponentStore;
        }

        internal bool IsCreated()
        {
            return EntityComponentStore != null;
        }

        /// <summary>
        /// Reports whether an entity would be selected by the EntityQuery instance used to create this entity query mask.
        /// </summary>
        /// <remarks>The check does not take the results of filters into account.</remarks>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if the entity would be returned by the EntityQuery, false if it would not.</returns>
        public bool Matches(Entity entity)
        {
            return EntityComponentStore->GetArchetype(entity)->CompareMask(this);
        }
    };

    /// <summary>
    /// Use an EntityQuery object to select entities with components that meet specific requirements.
    /// </summary>
    /// <remarks>
    /// An entity query defines the set of component types that an [archetype] must contain
    /// in order for its chunks and entities to be selected and specifies whether the components accessed
    /// through the query are read-only or read-write. 
    ///
    /// For simple queries, you can create an EntityQuery based on an array of
    /// component types. The following example defines a EntityQuery that finds all entities
    /// with both Rotation and RotationSpeed components.
    ///
    /// <example>
    /// <code source="../DocCodeSamples.Tests/EntityQueryExamples.cs" region="query-from-list" title="EntityQuery Example"/>
    /// </example>
    ///
    /// The query uses [ComponentType.ReadOnly] instead of the simpler `typeof` expression
    /// to designate that the system does not write to RotationSpeed. Always specify read-only
    /// when possible, since there are fewer constraints on read-only access to data, which can help
    /// the Job scheduler execute your Jobs more efficiently.
    ///
    /// For more complex queries, you can use an <see cref="EntityQueryDesc"/> object to create the entity query.
    /// A query description provides a flexible query mechanism to specify which archetypes to select
    /// based on the following sets of components:
    ///
    /// * `All` = All component types in this array must exist in the archetype
    /// * `Any` = At least one of the component types in this array must exist in the archetype
    /// * `None` = None of the component types in this array can exist in the archetype
    ///
    /// For example, the following query includes archetypes containing Rotation and
    /// RotationSpeed components, but excludes any archetypes containing a Frozen component:
    ///
    /// <example>
    /// <code source="../DocCodeSamples.Tests/EntityQueryExamples.cs" region="query-from-description" title="EntityQuery Example"/>
    /// </example>
    ///
    /// **Note:** Do not include completely optional components in the query description. To handle optional
    /// components, use <see cref="IJobChunk"/> and the [ArchetypeChunk.Has()] method to determine whether a chunk contains the
    /// optional component or not. Since all entities within the same chunk have the same components, you
    /// only need to check whether an optional component exists once per chunk -- not once per entity.
    ///
    /// Within a system class, use the [ComponentSystemBase.GetEntityQuery()] function
    /// to get a EntityQuery instance. Outside a system, use the [EntityManager.CreateEntityQuery()] function.
    ///
    /// You can filter entities based on
    /// whether they have [changed] or whether they have a specific value for a [shared component].
    /// Once you have created an EntityQuery object, you can
    /// [reset] and change the filter settings, but you cannot modify the base query.
    /// 
    /// Use an EntityQuery for the following purposes:
    ///
    /// * To get a [native array] of a the values for a specific <see cref="IComponentData"/> type for all entities matching the query
    /// * To get an [native array] of the <see cref="ArchetypeChunk"/> objects matching the query
    /// * To schedule an <see cref="IJobChunk"/> job
    /// * To control whether a system updates using [ComponentSystemBase.RequireForUpdate(query)]
    /// 
    /// Note that [Entities.ForEach] defines an entity query implicitly based on the methods you call. You can
    /// access this implicit EntityQuery object using [Entities.WithStoreEntityQueryInField]. However, you cannot
    /// create an [Entities.ForEach] construction based on an existing EntityQuery object.
    ///
    /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
    /// [Entities.WithStoreEntityQueryInField]: xref:Unity.Entities.SystemBase.Entities
    /// [ComponentSystemBase.GetEntityQuery()]: xref:Unity.Entities.ComponentSystemBase.GetEntityQuery*
    /// [EntityManager.CreateEntityQuery()]: xref:Unity.Entities.EntityManager.CreateEntityQuery*
    /// [ComponentType.ReadOnly]: xref:Unity.Entities.ComponentType.ReadOnly``1
    /// [ComponentSystemBase.RequireForUpdate()]: xref:Unity.Entities.ComponentSystemBase.RequireForUpdate(Unity.Entities.EntityQuery)
    /// [ArchetypeChunk.Has()]: xref:Unity.Entities.ArchetypeChunk.Has``1(Unity.Entities.ArchetypeChunkComponentType{``0})
    /// [archetype]: xref:Unity.Entities.EntityArchetype
    /// [changed]: xref:Unity.Entities.EntityQuery.SetChangedVersionFilter*
    /// [shared component]: xref:Unity.Entities.EntityQuery.SetSharedComponentFilter*
    /// [reset]: xref:Unity.Entities.EntityQuery.ResetFilter*
    /// [native array]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html
    /// </remarks>
    public unsafe class EntityQuery : IDisposable
    {
        internal ComponentDependencyManager* _DependencyManager;
        internal EntityQueryData*           _QueryData;
        internal EntityComponentStore*      _EntityComponentStore;
        internal EntityQueryFilter          _Filter;
        internal ManagedComponentStore      _ManagedComponentStore;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal string                    _DisallowDisposing = null;
#endif

        // TODO: this is temporary, used to cache some state to avoid recomputing the TransformAccessArray. We need to improve this.
        internal IDisposable                _CachedState;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal ComponentSafetyHandles* SafetyHandles => &_DependencyManager->Safety;
#endif

        internal EntityQuery(EntityQueryData* queryData, ComponentDependencyManager* dependencyManager, EntityComponentStore* entityComponentStore, ManagedComponentStore managedComponentStore)
        {
            _QueryData = queryData;
            _Filter = default(EntityQueryFilter);
            _DependencyManager = dependencyManager;
            _EntityComponentStore = entityComponentStore;
            _ManagedComponentStore = managedComponentStore;
        }

        /// <summary>
        /// Reports whether this query would currently select zero entities.
        /// </summary>
        /// <returns>True, if this EntityQuery matches zero existing entities. False, if it matches one or more entities.</returns>
        public bool IsEmptyIgnoreFilter
        {
            get
            {
                for (var m = 0; m < _QueryData->MatchingArchetypes.Length; ++m)
                {
                    var match = _QueryData->MatchingArchetypes.Ptr[m];
                    if (match->Archetype->EntityCount > 0)
                        return false;
                }

                return true;
            }
        }
#if NET_DOTS
        internal class SlowListSet<T>
        {
            internal List<T> items;

            internal SlowListSet() {
                items = new List<T>();
            }

            internal void Add(T item)
            {
                if (!items.Contains(item))
                    items.Add(item);
            }

            internal int Count => items.Count;

            internal T[] ToArray()
            {
                return items.ToArray();
            }
        }
#endif

        /// <summary>
        /// Gets the array of <see cref="ComponentType"/> objects included in this EntityQuery.
        /// </summary>
        /// <returns>An array of ComponentType objects</returns>
        internal ComponentType[] GetQueryTypes()
        {
#if !NET_DOTS
            var types = new HashSet<ComponentType>();
#else
            var types = new SlowListSet<ComponentType>();
#endif

            for (var i = 0; i < _QueryData->ArchetypeQueryCount; ++i)
            {
                for (var j = 0; j < _QueryData->ArchetypeQuery[i].AnyCount; ++j)
                {
                    types.Add(TypeManager.GetType(_QueryData->ArchetypeQuery[i].Any[j]));
                }
                for (var j = 0; j < _QueryData->ArchetypeQuery[i].AllCount; ++j)
                {
                    types.Add(TypeManager.GetType(_QueryData->ArchetypeQuery[i].All[j]));
                }
                for (var j = 0; j < _QueryData->ArchetypeQuery[i].NoneCount; ++j)
                {
                    types.Add(ComponentType.Exclude(TypeManager.GetType(_QueryData->ArchetypeQuery[i].None[j])));
                }
            }

#if !NET_DOTS
            var array = new ComponentType[types.Count];
            var t = 0;
            foreach (var type in types)
                array[t++] = type;
            return array;
#else
            return types.ToArray();
#endif
        }

        /// <summary>
        ///     Packed array of this EntityQuery's ReadOnly and writable ComponentTypes.
        ///     ReadOnly ComponentTypes come before writable types in this array.
        /// </summary>
        /// <returns>Array of ComponentTypes</returns>
        internal ComponentType[] GetReadAndWriteTypes()
        {
            var types = new ComponentType[_QueryData->ReaderTypesCount + _QueryData->WriterTypesCount];
            var typeArrayIndex = 0;
            for (var i = 0; i < _QueryData->ReaderTypesCount; ++i)
            {
                types[typeArrayIndex++] = ComponentType.ReadOnly(TypeManager.GetType(_QueryData->ReaderTypes[i]));
            }
            for (var i = 0; i < _QueryData->WriterTypesCount; ++i)
            {
                types[typeArrayIndex++] = TypeManager.GetType(_QueryData->WriterTypes[i]);
            }

            return types;
        }

        /// <summary>
        /// Disposes this EntityQuery instance.
        /// </summary>
        /// <remarks>Do not dispose the EntityQuery instances created using
        /// <see cref="SystemBase.Entities"/>. The system automatically disposes of
        /// its own entity queries.</remarks>
        /// <exception cref="InvalidOperationException">Thrown if you attempt to dispose an EntityQuery
        /// belonging to a SystemBase instance.</exception>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_DisallowDisposing != null)
                throw new InvalidOperationException(_DisallowDisposing);
#endif

            if (_CachedState != null)
            {
                _CachedState.Dispose();
                _CachedState = null;
            }

            if (_QueryData != null)
                ResetFilter();

            _DependencyManager = null;
            _QueryData = null;
            _EntityComponentStore = null;
            _ManagedComponentStore = null;
        }

        public bool IsCreated
        {
            get { return _EntityComponentStore != null;  }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        ///     Gets safety handle to a ComponentType required by this EntityQuery.
        /// </summary>
        /// <param name="indexInEntityQuery">Index of a ComponentType in this EntityQuery's RequiredComponents list./param>
        /// <returns>AtomicSafetyHandle for a ComponentType</returns>
        internal AtomicSafetyHandle GetSafetyHandle(int indexInEntityQuery)
        {
            var type = _QueryData->RequiredComponents + indexInEntityQuery;
            var isReadOnly = type->AccessModeType == ComponentType.AccessMode.ReadOnly;
            return SafetyHandles->GetSafetyHandle(type->TypeIndex, isReadOnly);
        }

        /// <summary>
        ///     Gets buffer safety handle to a ComponentType required by this EntityQuery.
        /// </summary>
        /// <param name="indexInEntityQuery">Index of a ComponentType in this EntityQuery's RequiredComponents list./param>
        /// <returns>AtomicSafetyHandle for a buffer</returns>
        internal AtomicSafetyHandle GetBufferSafetyHandle(int indexInEntityQuery)
        {
            var type = _QueryData->RequiredComponents + indexInEntityQuery;
            return SafetyHandles->GetBufferSafetyHandle(type->TypeIndex);
        }
#endif

        bool GetIsReadOnly(int indexInEntityQuery)
        {
            var type = _QueryData->RequiredComponents + indexInEntityQuery;
            var isReadOnly = type->AccessModeType == ComponentType.AccessMode.ReadOnly;
            return isReadOnly;
        }

        /// <summary>
        /// Calculates the number of entities selected by this EntityQuery.
        /// </summary>
        /// <remarks>
        /// The EntityQuery must run the query and apply any filters to calculate the entity count.
        /// </remarks>
        /// <returns>The number of entities based on the current EntityQuery properties.</returns>
        public int CalculateEntityCount()
        {
            SyncFilterTypes();
            return ChunkIterationUtility.CalculateEntityCount(in _QueryData->MatchingArchetypes, ref _Filter);
        }

        /// <summary>
        /// Calculates the number of entities selected by this EntityQuery, ignoring any set filters.
        /// </summary>
        /// <remarks>
        /// The EntityQuery must run the query to calculate the entity count.
        /// </remarks>
        /// <returns>The number of entities based on the current EntityQuery properties.</returns>
        public int CalculateEntityCountWithoutFiltering()
        {
            var dummyFilter = default(EntityQueryFilter);
            return ChunkIterationUtility.CalculateEntityCount(in _QueryData->MatchingArchetypes, ref dummyFilter);
        }

        /// <summary>
        /// Calculates the number of chunks that match this EntityQuery.
        /// </summary>
        /// <remarks>
        /// The EntityQuery must run the query and apply any filters to calculate the chunk count.
        /// </remarks>
        /// <returns>The number of chunks based on the current EntityQuery properties.</returns>
        public int CalculateChunkCount()
        {
            SyncFilterTypes();
            return ChunkIterationUtility.CalculateChunkCount(in _QueryData->MatchingArchetypes, ref _Filter);
        }

        /// <summary>
        /// Calculates the number of chunks that match this EntityQuery, ignoring any set filters.
        /// </summary>
        /// <remarks>
        /// The EntityQuery must run the query to calculate the chunk count.
        /// </remarks>
        /// <returns>The number of chunks based on the current EntityQuery properties.</returns>
        public int CalculateChunkCountWithoutFiltering()
        {
            var dummyFilter = default(EntityQueryFilter);
            return ChunkIterationUtility.CalculateChunkCount(_QueryData->MatchingArchetypes, ref dummyFilter);
        }

        /// <summary>
        /// Gets an ArchetypeChunkIterator which can be used to iterate over every chunk returned by this EntityQuery.
        /// </summary>
        /// <returns>ArchetypeChunkIterator for this EntityQuery</returns>
        public ArchetypeChunkIterator GetArchetypeChunkIterator()
        {
            return new ArchetypeChunkIterator(_QueryData->MatchingArchetypes, _DependencyManager, _EntityComponentStore->GlobalSystemVersion, ref _Filter);
        }

        /// <summary>
        ///     Index of a ComponentType in this EntityQuery's RequiredComponents list.
        ///     For example, you have a EntityQuery that requires these ComponentTypes: Position, Velocity, and Color.
        ///
        ///     These are their type indices (according to the TypeManager):
        ///         Position.TypeIndex == 3
        ///         Velocity.TypeIndex == 5
        ///            Color.TypeIndex == 17
        ///
        ///     RequiredComponents: [Position -> Velocity -> Color] (a linked list)
        ///     Given Velocity's TypeIndex (5), the return value would be 1, since Velocity is in slot 1 of RequiredComponents.
        /// </summary>
        /// <param name="componentType">Index of a ComponentType in the TypeManager</param>
        /// <returns>An index into RequiredComponents.</returns>
        internal int GetIndexInEntityQuery(int componentType)
        {
            // Go through all the required component types in this EntityQuery until you find the matching component type index.
            var componentIndex = 0;
            while (componentIndex < _QueryData->RequiredComponentsCount && _QueryData->RequiredComponents[componentIndex].TypeIndex != componentType)
                ++componentIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentIndex >= _QueryData->RequiredComponentsCount || _QueryData->RequiredComponents[componentIndex].AccessModeType == ComponentType.AccessMode.Exclude)
                throw new InvalidOperationException( $"Trying to get iterator for {TypeManager.GetType(componentType)} but the required component type was not declared in the EntityQuery.");
#endif
            return componentIndex;
        }

        /// <summary>
        /// Asynchronously creates an array of the chunks containing entities matching this EntityQuery.
        /// </summary>
        /// <remarks>
        /// Use <paramref name="jobhandle"/> as a dependency for jobs that use the returned chunk array.
        /// <seealso cref="CreateArchetypeChunkArray(Unity.Collections.Allocator)"/>.</remarks>
        /// <param name="allocator">Allocator to use for the array.</param>
        /// <param name="jobhandle">An `out` parameter assigned the handle to the internal job
        /// that gathers the chunks matching this EntityQuery.
        /// </param>
        /// <returns>NativeArray of all the chunks containing entities matching this query.</returns>
        public NativeArray<ArchetypeChunk> CreateArchetypeChunkArrayAsync(Allocator allocator, out JobHandle jobhandle)
        {
            JobHandle dependency = default;

            var filterCount = _Filter.Changed.Count;
            if (filterCount > 0)
            {
                var readerTypes = stackalloc int[filterCount];
                    for (int i = 0; i < filterCount; ++i)
                        readerTypes[i] = _QueryData->RequiredComponents[_Filter.Changed.IndexInEntityQuery[i]].TypeIndex;

                dependency = _DependencyManager->GetDependency(readerTypes, filterCount, null, 0);
            }

            return ChunkIterationUtility.CreateArchetypeChunkArrayWithoutSync(_QueryData->MatchingArchetypes, allocator, out jobhandle, ref _Filter, dependency);
        }

        /// <summary>
        /// Synchronously creates an array of the chunks containing entities matching this EntityQuery.
        /// </summary>
        /// <remarks>This method blocks until the internal job that performs the query completes.
        /// <seealso cref="CreateArchetypeChunkArray(Unity.Collections.Allocator,out Unity.Jobs.JobHandle)"/>
        /// </remarks>
        /// <param name="allocator">Allocator to use for the array.</param>
        /// <returns>NativeArray of all the chunks in this ComponentChunkIterator.</returns>
        public NativeArray<ArchetypeChunk> CreateArchetypeChunkArray(Allocator allocator)
        {
            SyncFilterTypes();
            JobHandle job;
            var res = ChunkIterationUtility.CreateArchetypeChunkArrayWithoutSync(_QueryData->MatchingArchetypes, allocator, out job, ref _Filter);
            job.Complete();
            return res;
        }

        /// <summary>
        /// Creates a NativeArray containing the selected entities.
        /// </summary>
        /// <param name="allocator">The type of memory to allocate.</param>
        /// <param name="jobhandle">An `out` parameter assigned a handle that you can use as a dependency for a Job
        /// that uses the NativeArray.</param>
        /// <returns>An array containing all the entities selected by the EntityQuery.</returns>
        public NativeArray<Entity> ToEntityArrayAsync(Allocator allocator, out JobHandle jobhandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var entityType = new ArchetypeChunkEntityType(SafetyHandles->GetEntityManagerSafetyHandle());
#else
            var entityType = new ArchetypeChunkEntityType();
#endif

            return ChunkIterationUtility.CreateEntityArray(_QueryData->MatchingArchetypes, allocator, entityType,  this, ref _Filter, out jobhandle, GetDependency());
        }

        /// <summary>
        /// Creates a NativeArray containing the selected entities.
        /// </summary>
        /// <remarks>This version of the function blocks until the Job used to fill the array is complete.</remarks>
        /// <param name="allocator">The type of memory to allocate.</param>
        /// <returns>An array containing all the entities selected by the EntityQuery.</returns>
        public NativeArray<Entity> ToEntityArray(Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var entityType = new ArchetypeChunkEntityType(SafetyHandles->GetEntityManagerSafetyHandle());
#else
            var entityType = new ArchetypeChunkEntityType();
#endif
            JobHandle job;
            var res = ChunkIterationUtility.CreateEntityArray(_QueryData->MatchingArchetypes, allocator, entityType, this, ref _Filter, out job, GetDependency());
            job.Complete();
            return res;
        }

        internal struct GatherEntitiesResult
        {
            public int StartingOffset;
            public int EntityCount;
            public Entity* EntityBuffer;
            public NativeArray<Entity> EntityArray;
        }

        internal void GatherEntitiesToArray(out GatherEntitiesResult result)
        {
            // The ChunkIterationUtility will perform the job if the query is not using filter
            ChunkIterationUtility.GatherEntitiesToArray(_QueryData, ref _Filter, out result);

            if (result.EntityBuffer == null)
            {
                var entityCount = CalculateEntityCount();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var entityType = new ArchetypeChunkEntityType(SafetyHandles->GetEntityManagerSafetyHandle());
#else
                var entityType = new ArchetypeChunkEntityType();
#endif
                var job = new GatherEntitiesJob
                {
                    EntityType = entityType,
                    Entities = new NativeArray<Entity>(entityCount, Allocator.TempJob)
                };
                job.Run(this);
                result.EntityArray = job.Entities;
                result.EntityBuffer = (Entity*)result.EntityArray.GetUnsafeReadOnlyPtr();
                result.EntityCount = result.EntityArray.Length;
            }
        }

        internal void ReleaseGatheredEntities(ref GatherEntitiesResult result)
        {
            ChunkIterationUtility.currentOffsetInResultBuffer = result.StartingOffset;
            if (result.EntityArray.IsCreated)
            {
                result.EntityArray.Dispose();
            }
        }
        
        /// <summary>
        /// Creates a NativeArray containing the components of type T for the selected entities.
        /// </summary>
        /// <param name="allocator">The type of memory to allocate.</param>
        /// <param name="jobhandle">An `out` parameter assigned a handle that you can use as a dependency for a Job
        /// that uses the NativeArray.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>An array containing the specified component for all the entities selected
        /// by the EntityQuery.</returns>
        public NativeArray<T> ToComponentDataArrayAsync<T>(Allocator allocator, out JobHandle jobhandle)
            where T : struct, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var componentType = new ArchetypeChunkComponentType<T>(SafetyHandles->GetSafetyHandle(TypeManager.GetTypeIndex<T>(), true), true, _EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ArchetypeChunkComponentType<T>(true, _EntityComponentStore->GlobalSystemVersion);
#endif
            
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
            if (indexInEntityQuery == -1)
                throw new InvalidOperationException( $"Trying ToComponentDataArrayAsync of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
#endif
            return ChunkIterationUtility.CreateComponentDataArray(_QueryData->MatchingArchetypes, allocator, componentType, this, ref _Filter, out jobhandle, GetDependency());
        }

        /// <summary>
        /// Creates a NativeArray containing the components of type T for the selected entities.
        /// </summary>
        /// <param name="allocator">The type of memory to allocate.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>An array containing the specified component for all the entities selected
        /// by the EntityQuery.</returns>
        /// <exception cref="InvalidOperationException">Thrown if you ask for a component that is not part of
        /// the group.</exception>
        public NativeArray<T> ToComponentDataArray<T>(Allocator allocator)
            where T : struct, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var componentType = new ArchetypeChunkComponentType<T>(SafetyHandles->GetSafetyHandle(TypeManager.GetTypeIndex<T>(), true), true, _EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ArchetypeChunkComponentType<T>(true, _EntityComponentStore->GlobalSystemVersion);
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
            if (indexInEntityQuery == -1)
                throw new InvalidOperationException( $"Trying ToComponentDataArray of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
#endif

            JobHandle job;
            var res = ChunkIterationUtility.CreateComponentDataArray(_QueryData->MatchingArchetypes, allocator, componentType, this, ref _Filter, out job, GetDependency());
            job.Complete();
            return res;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public T[] ToComponentDataArray<T>() where T : class, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var componentType = new ArchetypeChunkComponentType<T>(SafetyHandles->GetSafetyHandle(typeIndex, true), true, _EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ArchetypeChunkComponentType<T>(true, _EntityComponentStore->GlobalSystemVersion);
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
            if (indexInEntityQuery == -1)
                throw new InvalidOperationException( $"Trying ToComponentDataArray of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
#endif
            
            var matches = _QueryData->MatchingArchetypes;
            var entityCount = ChunkIterationUtility.CalculateEntityCount(matches, ref _Filter);
            T[] res = new T[entityCount];
            int i = 0;
            for (int mi = 0; mi < matches.Length; ++mi)
            {
                var match = _QueryData->MatchingArchetypes.Ptr[mi];
                var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(match->Archetype, typeIndex);
                var chunks = match->Archetype->Chunks;

                for (int ci = 0; ci < chunks.Count; ++ci)
                {
                    var chunk = chunks.p[ci];

                    if (_Filter.RequiresMatchesFilter && !chunk->MatchesFilter(match, ref _Filter))
                        continue;

                    var managedComponentArray = (int*)ChunkDataUtility.GetComponentDataRW(chunk,0, indexInTypeArray, _EntityComponentStore->GlobalSystemVersion);
                    for (int entityIndex = 0; entityIndex < chunk->Count; ++entityIndex)
                    {
                        res[i++] = (T)_ManagedComponentStore.GetManagedComponent(managedComponentArray[entityIndex]);
                    }
                }
            }

            return res;
        }
#endif

        public void CopyFromComponentDataArray<T>(NativeArray<T> componentDataArray)
        where T : struct, IComponentData
        {
            // throw if non equal size
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var entityCount = CalculateEntityCount();
            if (entityCount != componentDataArray.Length)
                throw new ArgumentException($"Length of input array ({componentDataArray.Length}) does not match length of EntityQuery ({entityCount})");

            var componentType = new ArchetypeChunkComponentType<T>(SafetyHandles->GetSafetyHandle(TypeManager.GetTypeIndex<T>(), false), false, _EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ArchetypeChunkComponentType<T>(false, _EntityComponentStore->GlobalSystemVersion);
#endif
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
            if (indexInEntityQuery == -1)
                throw new InvalidOperationException( $"Trying CopyFromComponentDataArray of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
#endif
            

            ChunkIterationUtility.CopyFromComponentDataArray(_QueryData->MatchingArchetypes, componentDataArray, componentType, this, ref _Filter, out var job, GetDependency());
            job.Complete();
        }

        public void CopyFromComponentDataArrayAsync<T>(NativeArray<T> componentDataArray, out JobHandle jobhandle)
            where T : struct,IComponentData
        {
            // throw if non equal size
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var entityCount = CalculateEntityCount();
            if(entityCount != componentDataArray.Length)
                throw new ArgumentException($"Length of input array ({componentDataArray.Length}) does not match length of EntityQuery ({entityCount})");
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var componentType = new ArchetypeChunkComponentType<T>(SafetyHandles->GetSafetyHandle(TypeManager.GetTypeIndex<T>(), false), false, _EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ArchetypeChunkComponentType<T>(false, _EntityComponentStore->GlobalSystemVersion);
#endif
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
            if (indexInEntityQuery == -1)
                throw new InvalidOperationException( $"Trying CopyFromComponentDataArrayAsync of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
#endif

            ChunkIterationUtility.CopyFromComponentDataArray(_QueryData->MatchingArchetypes, componentDataArray, componentType, this, ref _Filter, out jobhandle, GetDependency());
        }

        public Entity GetSingletonEntity()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var entityCount = CalculateEntityCount();
            if (entityCount != 1)
                throw new System.InvalidOperationException($"GetSingletonEntity() requires that exactly one exists but there are {entityCount}.");
#endif

            Entity entity;

            var iterator = GetArchetypeChunkIterator();
            iterator.MoveNext();

            var array = iterator.GetCurrentChunkComponentDataPtr(false, 0);
            UnsafeUtility.CopyPtrToStructure(array, out entity);
            
            return entity;
        }

        /// <summary>
        /// Gets the value of a singleton component.
        /// </summary>
        /// <remarks>A singleton component is a component of which only one instance exists in the world
        /// and which has been set with <see cref="SetSingleton{T}(T)"/>.</remarks>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A copy of the singleton component.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public T GetSingleton<T>()
            where T : struct, IComponentData
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(GetIndexInEntityQuery(TypeManager.GetTypeIndex<T>()) != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that {typeof(T)} is the only component type in its archetype.");

            var entityCount = CalculateEntityCount();
            if (entityCount != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that exactly one {typeof(T)} exists but there are {entityCount}.");
            #endif

            CompleteDependency();

            T value;
            var iterator = GetArchetypeChunkIterator();
            iterator.MoveNext();
            
            var array = iterator.GetCurrentChunkComponentDataPtr(false, 1);
            UnsafeUtility.CopyPtrToStructure(array, out value);

            return value;
        }

        /// <summary>
        /// Sets the value of a singleton component.
        /// </summary>
        /// <remarks>
        /// For a component to be a singleton, there can be only one instance of that component
        /// in a <see cref="World"/>. The component must be the only component in its archetype
        /// and you cannot use the same type of component as a normal component.
        ///
        /// To create a singleton, create an entity with the singleton component as its only component,
        /// and then use `SetSingleton()` to assign a value.
        ///
        /// For example, if you had a component defined as:
        /// <code>
        /// public struct Singlet: IComponentData{ public int Value; }
        /// </code>
        ///
        /// You could create a singleton as follows:
        ///
        /// <code>
        /// var singletonEntity = entityManager.CreateEntity(typeof(Singlet));
        /// var singletonGroup = entityManager.CreateEntityQuery(typeof(Singlet));
        /// singletonGroup.SetSingleton&lt;Singlet&gt;(new Singlet {Value = 1});
        /// </code>
        ///
        /// You can set and get the singleton value from a EntityQuery or a ComponentSystem.
        /// </remarks>
        /// <param name="value">An instance of type T containing the values to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if more than one instance of this component type
        /// exists in the world or the component type appears in more than one archetype.</exception>
        public void SetSingleton<T>(T value)
            where T : struct, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(GetIndexInEntityQuery(TypeManager.GetTypeIndex<T>()) != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that {typeof(T)} is the only component type in its archetype.");

            var entityCount = CalculateEntityCount();
            if (entityCount != 1)
                throw new System.InvalidOperationException($"SetSingleton<{typeof(T)}>() requires that exactly one {typeof(T)} exists but there are {entityCount}.");
#endif

            CompleteDependency();

            var iterator = GetArchetypeChunkIterator();
            iterator.MoveNext();

            var array = iterator.GetCurrentChunkComponentDataPtr(true, 1);
            UnsafeUtility.CopyStructureToPtr(ref value, array);
        }

        internal bool CompareComponents(ComponentType* componentTypes, int count)
        {
            return EntityQueryManager.CompareComponents(componentTypes, count, _QueryData);
        }

        /// <summary>
        /// Compares a list of component types to the types defining this EntityQuery.
        /// </summary>
        /// <remarks>Only required types in the query are used as the basis for the comparison.
        /// If you include types that the query excludes or only includes as optional,
        /// the comparison returns false.</remarks>
        /// <param name="componentTypes">An array of ComponentType objects.</param>
        /// <returns>True, if the list of types, including any read/write access specifiers,
        /// matches the list of required component types of this EntityQuery.</returns>
        public bool CompareComponents(ComponentType[] componentTypes)
        {
            fixed (ComponentType* componentTypesPtr = componentTypes)
            {
                return EntityQueryManager.CompareComponents(componentTypesPtr, componentTypes.Length, _QueryData);
            }
        }

        /// <summary>
        /// Compares a list of component types to the types defining this EntityQuery.
        /// </summary>
        /// <remarks>Only required types in the query are used as the basis for the comparison.
        /// If you include types that the query excludes or only includes as optional,
        /// the comparison returns false. Do not include the <see cref="Entity"/> type, which
        /// is included implicitly.</remarks>
        /// <param name="componentTypes">An array of ComponentType objects.</param>
        /// <returns>True, if the list of types, including any read/write access specifiers,
        /// matches the list of required component types of this EntityQuery.</returns>
        public bool CompareComponents(NativeArray<ComponentType> componentTypes)
        {
            return EntityQueryManager.CompareComponents((ComponentType*)componentTypes.GetUnsafeReadOnlyPtr(), componentTypes.Length, _QueryData);
        }

        /// <summary>
        /// Compares a query description to the description defining this EntityQuery.
        /// </summary>
        /// <remarks>The `All`, `Any`, and `None` components in the query description are
        /// compared to the corresponding list in this EntityQuery.</remarks>
        /// <param name="queryDesc">The query description to compare.</param>
        /// <returns>True, if the query description contains the same components with the same
        /// read/write access modifiers as this EntityQuery.</returns>
        public bool CompareQuery(EntityQueryDesc[] queryDesc)
        {
            return EntityQueryManager.CompareQuery(queryDesc, _QueryData);
        }

        /// <summary>
        /// Resets this EntityQuery's filter.
        /// </summary>
        /// <remarks>
        /// Removes references to shared component data, if applicable, then resets the filter type to None.
        /// </remarks>
        public void ResetFilter()
        {
            var sharedCount = _Filter.Shared.Count;
            var sm = _ManagedComponentStore;
            for (var i = 0; i < sharedCount; ++i)
                sm.RemoveReference(_Filter.Shared.SharedComponentIndex[i]);

            _Filter.Changed.Count = 0;
            _Filter.Shared.Count = 0;
        }

        /// <summary>
        /// Filters this EntityQuery so that it only selects entities with shared component values
        /// matching the values specified by the `sharedComponent1` parameter.
        /// </summary>
        /// <param name="sharedComponent1">The shared component values on which to filter.</param>
        /// <typeparam name="SharedComponent1">The type of shared component. (The type must also be
        /// one of the types used to create the EntityQuery.</typeparam>
        public void SetSharedComponentFilter<SharedComponent1>(SharedComponent1 sharedComponent1)
            where SharedComponent1 : struct, ISharedComponentData
        {
            ResetFilter();
            AddSharedComponentFilter(sharedComponent1);
        }

        /// <summary>
        /// Filters this EntityQuery based on the values of two separate shared components.
        /// </summary>
        /// <remarks>
        /// The filter only selects entities for which both shared component values
        /// specified by the `sharedComponent1` and `sharedComponent2` parameters match.
        /// </remarks>
        /// <param name="sharedComponent1">Shared component values on which to filter.</param>
        /// <param name="sharedComponent2">Shared component values on which to filter.</param>
        /// <typeparam name="SharedComponent1">The type of shared component. (The type must also be
        /// one of the types used to create the EntityQuery.</typeparam>
        /// <typeparam name="SharedComponent2">The type of shared component. (The type must also be
        /// one of the types used to create the EntityQuery.</typeparam>
        public void SetSharedComponentFilter<SharedComponent1, SharedComponent2>(SharedComponent1 sharedComponent1,
            SharedComponent2 sharedComponent2)
            where SharedComponent1 : struct, ISharedComponentData
            where SharedComponent2 : struct, ISharedComponentData
        {
            ResetFilter();
            AddSharedComponentFilter(sharedComponent1);
            AddSharedComponentFilter(sharedComponent2);
        }

        /// <summary>
        /// Filters out entities in chunks for which the specified component has not changed.
        /// </summary>
        /// <remarks>
        ///     Saves a given ComponentType's index in RequiredComponents in this group's Changed filter.
        /// </remarks>
        /// <param name="componentType">ComponentType to mark as changed on this EntityQuery's filter.</param>
        public void SetChangedVersionFilter(ComponentType componentType)
        {
            ResetFilter();
            AddChangedVersionFilter(componentType);
        }

        internal void SetChangedFilterRequiredVersion(uint requiredVersion)
        {
            _Filter.RequiredChangeVersion = requiredVersion;
        }

        public void SetChangedVersionFilter(ComponentType[] componentType)
        {
            if (componentType.Length > EntityQueryFilter.ChangedFilter.Capacity)
                throw new ArgumentException(
                    $"EntityQuery.SetFilterChanged accepts a maximum of {EntityQueryFilter.ChangedFilter.Capacity} component array length");
            if (componentType.Length <= 0)
                throw new ArgumentException(
                    $"EntityQuery.SetFilterChanged component array length must be larger than 0");

            ResetFilter();
            for (var i = 0; i != componentType.Length; i++)
                AddChangedVersionFilter(componentType[i]);
        }

        public void AddChangedVersionFilter(ComponentType componentType)
        {
            var newFilterIndex = _Filter.Changed.Count;
            if (newFilterIndex >= EntityQueryFilter.ChangedFilter.Capacity)
                throw new ArgumentException($"EntityQuery accepts a maximum of {EntityQueryFilter.ChangedFilter.Capacity} changed filters.");

            _Filter.Changed.Count = newFilterIndex + 1;
            _Filter.Changed.IndexInEntityQuery[newFilterIndex] = GetIndexInEntityQuery(componentType.TypeIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _Filter.AssertValid();
#endif
        }

        public void AddSharedComponentFilter<SharedComponent>(SharedComponent sharedComponent)
            where SharedComponent : struct, ISharedComponentData
        {
            var sm = _ManagedComponentStore;

            var newFilterIndex = _Filter.Shared.Count;
            if (newFilterIndex >= EntityQueryFilter.SharedComponentData.Capacity)
                throw new ArgumentException($"EntityQuery accepts a maximum of {EntityQueryFilter.SharedComponentData.Capacity} shared component filters.");

            _Filter.Shared.Count = newFilterIndex + 1;
            _Filter.Shared.IndexInEntityQuery[newFilterIndex] = GetIndexInEntityQuery(TypeManager.GetTypeIndex<SharedComponent>());
            _Filter.Shared.SharedComponentIndex[newFilterIndex] = sm.InsertSharedComponent(sharedComponent);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _Filter.AssertValid();
#endif
        }

        /// <summary>
        /// Ensures all jobs running on this EntityQuery complete.
        /// </summary>
        /// <remarks>An entity query uses jobs internally when required to create arrays of
        /// entities and chunks. This function completes those jobs and returns when they are finished.
        /// </remarks>
        public void CompleteDependency()
        {
            _DependencyManager->CompleteDependenciesNoChecks(_QueryData->ReaderTypes, _QueryData->ReaderTypesCount,
                _QueryData->WriterTypes, _QueryData->WriterTypesCount);
        }

        /// <summary>
        /// Combines all dependencies in this EntityQuery into a single JobHandle.
        /// </summary>
        /// <remarks>An entity query uses jobs internally when required to create arrays of
        /// entities and chunks.</remarks>
        /// <returns>JobHandle that represents the combined dependencies of this EntityQuery</returns>
        public JobHandle GetDependency()
        {
            return _DependencyManager->GetDependency(_QueryData->ReaderTypes, _QueryData->ReaderTypesCount,
                _QueryData->WriterTypes, _QueryData->WriterTypesCount);
        }

        /// <summary>
        /// Adds another job handle to this EntityQuery's dependencies.
        /// </summary>
        /// <remarks>An entity query uses jobs internally when required to create arrays of
        /// entities and chunks. This junction adds an external job as a dependency for those
        /// internal jobs.</remarks>
        public JobHandle AddDependency(JobHandle job)
        {
            return _DependencyManager->AddDependency(_QueryData->ReaderTypes, _QueryData->ReaderTypesCount,
                _QueryData->WriterTypes, _QueryData->WriterTypesCount, job);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public int GetCombinedComponentOrderVersion()
        {
            var version = 0;

            for (var i = 0; i < _QueryData->RequiredComponentsCount; ++i)
                version += _EntityComponentStore->GetComponentTypeOrderVersion(_QueryData->RequiredComponents[i].TypeIndex);

            return version;
        }

        internal bool AddReaderWritersToLists(ref UnsafeIntList reading, ref UnsafeIntList writing)
        {
            bool anyAdded = false;
            for (int i = 0; i < _QueryData->ReaderTypesCount; ++i)
                anyAdded |= CalculateReaderWriterDependency.AddReaderTypeIndex(_QueryData->ReaderTypes[i], ref reading, ref writing);

            for (int i = 0; i < _QueryData->WriterTypesCount; ++i)
                anyAdded |= CalculateReaderWriterDependency.AddWriterTypeIndex(_QueryData->WriterTypes[i], ref reading, ref writing);
            return anyAdded;
        }

        /// <summary>
        /// Syncs the needed types for the filter.
        /// For every type that is change filtered we need to CompleteWriteDependency to avoid race conditions on the
        /// change version of those types
        /// </summary>
        internal void SyncFilterTypes()
        {
            for (int i = 0; i < _Filter.Changed.Count; ++i)
            {
                var type = _QueryData->RequiredComponents[_Filter.Changed.IndexInEntityQuery[i]];
                _DependencyManager->CompleteWriteDependency(type.TypeIndex);
            }
        }

        /// <summary>
        /// Syncs the needed types for the filter using the types in UnsafeMatchingArchetypePtrList
        /// This version is used when the EntityQuery is not known
        /// </summary>
        internal static void SyncFilterTypes(ref UnsafeMatchingArchetypePtrList matchingArchetypes, ref EntityQueryFilter filter, ComponentDependencyManager* safetyManager)
        {
            if(matchingArchetypes.Length < 1)
                return;

            var match = *matchingArchetypes.Ptr;
            for (int i = 0; i < filter.Changed.Count; ++i)
            {
                var indexInEntityQuery = filter.Changed.IndexInEntityQuery[i];
                var componentIndexInChunk = match->IndexInArchetype[indexInEntityQuery];
                var type = match->Archetype->Types[componentIndexInChunk];
                safetyManager->CompleteWriteDependency(type.TypeIndex);
            }
        }

        /// <summary>
        /// Returns if the entity query has a filter applied to it.
        /// </summary>
        /// <returns>Returns true if the query has a filter, returns false if the query does not have a filter.</returns>
        public bool HasFilter()
        {
            return _Filter.RequiresMatchesFilter;
        }
        
        [Obsolete("CreateArchetypeChunkArray with out JobHandle parameter renamed to CreateArchetypeChunkArrayAsync (RemovedAfter 2020-04-13). (UnityUpgradable) -> CreateArchetypeChunkArrayAsync(*)", false)]
        public NativeArray<ArchetypeChunk> CreateArchetypeChunkArray(Allocator allocator, out JobHandle jobhandle) => CreateArchetypeChunkArrayAsync(allocator, out jobhandle);
        
        [Obsolete("ToEntityArray with out JobHandle parameter renamed to ToEntityArrayAsync (RemovedAfter 2020-04-13). (UnityUpgradable) -> ToEntityArrayAsync(*)", false)]
        public NativeArray<Entity> ToEntityArray(Allocator allocator, out JobHandle jobhandle) => ToEntityArrayAsync(allocator, out jobhandle);
        
        [Obsolete("ToComponentDataArray with out JobHandle parameter renamed to ToComponentDataArrayAsync (RemovedAfter 2020-04-13). (UnityUpgradable) -> ToComponentDataArrayAsync(*)", false)]
        public NativeArray<T> ToComponentDataArray<T>(Allocator allocator, out JobHandle jobhandle) where T : struct, IComponentData => ToComponentDataArrayAsync<T>(allocator, out jobhandle);
        
        [Obsolete("CopyFromComponentDataArray with out JobHandle parameter renamed to CopyFromComponentDataArrayAsync (RemovedAfter 2020-04-13). (UnityUpgradable) -> CopyFromComponentDataArrayAsync(*)", false)]
        public void CopyFromComponentDataArray<T>(NativeArray<T> componentDataArray, out JobHandle jobhandle) where T : struct, IComponentData => CopyFromComponentDataArrayAsync(componentDataArray, out jobhandle);
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public static unsafe class EntityQueryManagedComponentExtensions
    {
        /// <summary>
        /// Gets the value of a singleton component.
        /// </summary>
        /// <remarks>A singleton component is a component of which only one instance exists in the world
        /// and which has been set with <see cref="SetSingleton{T}(T)"/>.</remarks>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>A copy of the singleton component.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static T GetSingleton<T>(this EntityQuery query) where T : class, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (query.GetIndexInEntityQuery(TypeManager.GetTypeIndex<T>()) != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that {typeof(T)} is the only component type in its archetype.");

            var entityCount = query.CalculateEntityCount();
            if (entityCount != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that exactly one {typeof(T)} exists but there are {entityCount}.");
#endif

            query.CompleteDependency();
            
            var iterator = query.GetArchetypeChunkIterator();
            iterator.MoveNext();

            var managedComponentIndex = *(int*)iterator.GetCurrentChunkComponentDataPtr(false, 1);
            return (T) query._ManagedComponentStore.GetManagedComponent(managedComponentIndex);
        }

        /// <summary>
        /// Sets the value of a singleton component.
        /// </summary>
        /// <remarks>
        /// For a component to be a singleton, there can be only one instance of that component
        /// in a <see cref="World"/>. The component must be the only component in its archetype
        /// and you cannot use the same type of component as a normal component.
        ///
        /// To create a singleton, create an entity with the singleton component as its only component,
        /// and then use `SetSingleton()` to assign a value.
        ///
        /// For example, if you had a component defined as:
        /// <code>
        /// public class Singlet : IComponentData{ public int Value; }
        /// </code>
        ///
        /// You could create a singleton as follows:
        ///
        /// <code>
        /// var singletonEntity = entityManager.CreateEntity(typeof(Singlet));
        /// var singletonGroup = entityManager.CreateEntityQuery(typeof(Singlet));
        /// singletonGroup.SetSingleton&lt;Singlet&gt;(new Singlet {Value = 1});
        /// </code>
        ///
        /// You can set and get the singleton value from a EntityQuery or a ComponentSystem.
        /// </remarks>
        /// <param name="value">An instance of type T containing the values to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if more than one instance of this component type
        /// exists in the world or the component type appears in more than one archetype.</exception>
        public static void SetSingleton<T>(this EntityQuery query, T value) where T : class, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (query.GetIndexInEntityQuery(TypeManager.GetTypeIndex<T>()) != 1)
                throw new System.InvalidOperationException($"GetSingleton<{typeof(T)}>() requires that {typeof(T)} is the only component type in its archetype.");

            var entityCount = query.CalculateEntityCount();
            if (entityCount != 1)
                throw new System.InvalidOperationException($"SetSingleton<{typeof(T)}>() requires that exactly one {typeof(T)} exists but there are {entityCount}.");
#endif

            query.CompleteDependency();

            var iterator = query.GetArchetypeChunkIterator();
            iterator.MoveNext();

            var managedComponentIndex = (int*)iterator.GetCurrentChunkComponentDataPtr(false, 1);
            query._ManagedComponentStore.UpdateManagedComponentValue(managedComponentIndex, value, ref *query._EntityComponentStore);
        }
    }
#endif
}
