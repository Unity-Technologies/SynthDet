
using System;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Implement SystemBase to create a systems in ECS.
    /// </summary>
    /// <remarks>
    /// ### Systems in ECS
    /// 
    /// A typical system operates on a set of entities that have specific components. The system identifies
    /// the components of interest, reading and writing data, and performing other entity operations as appropriate.
    ///
    /// The following example shows a basic system that iterates over entities using a [Entities.ForEach] construction.
    /// In this  example, the system iterates over all entities with both a Displacement and a Velocity component and
    /// updates the Displacement based on the delta time elapsed since the last frame.
    /// 
    /// <example>
    /// <code source="../DocCodeSamples.Tests/SystemBaseExamples.cs" region="basic-system" title="Basic System Example"/>
    /// </example>
    ///
    /// #### System lifecycle callbacks
    /// 
    /// You can define a set of system lifecycle event functions when you implement a system. The runtime invokes these
    /// functions in the following order:
    ///
    /// * <see cref="ComponentSystemBase.OnCreate"/> -- called when the system is created.
    /// * <see cref="ComponentSystemBase.OnStartRunning"/> -- before the first OnUpdate and whenever the system resumes
    ///   running.
    /// * <see cref="OnUpdate"/> -- every frame as long as the system has work to do (see
    ///   <see cref="ComponentSystemBase.ShouldRunSystem"/>) and the system is <see cref="ComponentSystemBase.Enabled"/>.
    /// * <see cref="ComponentSystemBase.OnStopRunning"/> -- whenever the system stops updating because it finds no
    ///   entities matching its queries. Also called before OnDestroy.
    /// * <see cref="ComponentSystemBase.OnDestroy"/> -- when the system is destroyed.
    ///
    /// All of these functions are executed on the main thread. To perform work on background threads, you can schedule
    /// jobs from the <see cref="SystemBase.OnUpdate"/> function.
    ///
    /// #### System update order
    /// 
    /// The runtime executes systems in the order determined by their <seealso cref="ComponentSystemGroup"/>. Place a system
    /// in a group using <seealso cref="UpdateInGroupAttribute"/>. Use <seealso cref="UpdateBeforeAttribute"/> and
    /// <seealso cref="UpdateAfterAttribute"/> to specify the execution order within a group.
    ///
    /// If you do not explicitly place a system in a specific group, the runtime places it in the default <see cref="World"/>
    /// <see cref="SimulationSystemGroup"/>. By default, all systems are discovered, instantiated, and added to the
    /// default World. You can use the <seealso cref="DisableAutoCreationAttribute"/> to prevent a system from being
    /// created automatically.
    ///
    /// #### Entity queries
    ///
    /// A system caches all queries created through an [Entities.ForEach] construction, through
    /// [ComponentSystemBase.GetEntityQuery], or through [ComponentSystemBase.RequireForUpdate]. By default,
    /// the runtime only calls a system's `OnUpdate()` function when one of these cached queries finds entities. You
    /// can use the <seealso cref="AlwaysUpdateSystemAttribute"/> to have the system always update. Note that a system
    /// with no queries is also updated every frame.
    ///
    /// #### Entities.ForEach and Job.WithCode constructions
    /// 
    /// The <see cref="Entities"/> property provides a convenient mechanism for iterating over entity
    /// data. Using an [Entities.ForEach] construction, you can define your entity query, specify a lambda function
    /// to run for each entity, and either schedule the work to be done on a background thread or execute the work
    /// immediately on the main thread. 
    ///
    /// The [Entities.ForEach] construction uses a C# compiler extension to take a data query syntax that describes
    /// your intent and translate it into efficient (optionally) job-based code.  
    ///
    /// The <see cref="Job"/> property provides a similar mechanism for defining a [C# Job]. You can only use
    /// `Schedule()`to run a [Job.WithCode] construction, which executes the lambda function as a single job.
    ///
    /// #### System attributes
    ///
    /// You can use a number of attributes on your SystemBase implementation to control when it updates:
    /// 
    /// * <seealso cref="UpdateInGroupAttribute"/> -- place the system in a <seealso cref="ComponentSystemGroup"/>.
    /// * <seealso cref="UpdateBeforeAttribute"/> -- always update the system before another system in the same group.
    /// * <seealso cref="UpdateAfterAttribute"/> -- always update the system after another system in the same group.
    /// * <seealso cref="AlwaysUpdateSystemAttribute"/> -- invoke `OnUpdate` every frame.
    /// * <seealso cref="DisableAutoCreationAttribute"/> -- do not create the system automatically.
    /// * <seealso cref="AlwaysSynchronizeSystemAttribute"/> -- force a [sync point](xref:sync-points) before invoking
    ///   `OnUpdate`.
    ///
    /// [JobHandle]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html
    /// [JobHandle.CompleteDependencies]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.CombineDependencies.html
    /// [C# Job]: https://docs.unity3d.com/2019.2/Documentation/Manual/JobSystem.html
    /// [ECB]: xref:Unity.Entities.EntityCommandBuffer
    /// [ComponentSystemBase.GetEntityQuery]: xref:Unity.entities.ComponentSystemBase.GetEntityQuery*
    /// [ComponentSystemBase.RequireForUpdate]: xref:Unity.Entities.ComponentSystemBase.RequireForUpdate*
    /// [Entities.ForEach]: xref:ecs-entities-foreach
    /// [Job.WithCode]: xref:ecs-entities-foreach
    /// </remarks>
    public abstract class SystemBase : ComponentSystemBase
    {
        bool      _GetDependencyFromSafetyManager;
        JobHandle _JobHandle;

        /// <summary>
        /// The ECS-related data dependencies of the system.
        /// </summary>
        /// <remarks>
        /// Before <see cref="OnUpdate"/>, the Dependency property represents the combined job handles of any job that
        /// writes to the same components that the current system reads -- or reads the same components that the current
        /// system writes to. When you use [Entities.ForEach] or [Job.WithCode], the system uses the Dependency property
        /// to specify a job’s dependencies when scheduling it. The system also combines the new job's [JobHandle]
        /// with Dependency so that any subsequent job scheduled in the system depends on the earlier jobs (in sequence).
        ///
        /// The following example illustrates an `OnUpdate()` implementation that relies on implicit dependency
        /// management. The function schedules three jobs, each depending on the previous one:
        /// 
        /// <example>
        /// <code source="../DocCodeSamples.Tests/SystemBaseExamples.cs" region="simple-dependency" title="Implicit Dependency Example"/>
        /// </example>
        ///
        /// You can opt out of this default dependency management by explicitly passing a [JobHandle] to
        /// [Entities.ForEach] or [Job.WithCode]. When you pass in a [JobHandle], these constructions also return a
        /// [JobHandle] representing the input dependencies combined with the new job. The [JobHandle] objects of any
        /// jobs scheduled with explicit dependencies are not combined with the system’s Dependency property. You must set the Dependency
        /// property manually to make sure that later systems receive the correct job dependencies.
        /// 
        /// The following <see cref="OnUpdate"/> function illustrates manual dependency management. The function uses
        /// two [Entity.ForEach] constructions that schedule jobs which do not depend upon each other, only the incoming
        /// dependencies of the system. Then a [Job.WithCode] construction schedules a job that depends on both of the
        /// prior jobs, who’s dependencies are combined using [JobHandle.CombineDependencies]. Finally, the [JobHandle]
        /// of the last job is assigned to the Dependency property so that the ECS safety manager can propagate the
        /// dependencies to subsequent systems.
        /// 
        /// <example>
        /// <code source="../DocCodeSamples.Tests/SystemBaseExamples.cs" region="manual-dependency" title="Manual Dependency Example"/>
        /// </example>
        ///
        /// You can combine implicit and explicit dependency management (by using [JobHandle.CombineDependencies]);
        /// however, doing so can be error prone. When you set the Dependency property, the assigned [JobHandle]
        /// replaces any existing dependency, it is not combined with them.
        /// 
        /// Note that the default, implicit dependency management does not include <see cref="IJobChunk"/> jobs.
        /// You must manage the dependencies for IJobChunk explicitly.
        /// 
        /// [JobHandle]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html
        /// [JobHandle.CompleteDependencies]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.CombineDependencies.html
        /// [Entities.ForEach]: xref:ecs-entities-foreach
        /// [Job.WithCode]: xref:ecs-entities-foreach
        /// </remarks>
        unsafe protected JobHandle Dependency
        {
            get
            {
                if (_GetDependencyFromSafetyManager)
                {
                    _GetDependencyFromSafetyManager = false;
                    _JobHandle = m_DependencyManager->GetDependency(m_JobDependencyForReadingSystems.Ptr,
                        m_JobDependencyForReadingSystems.Length, m_JobDependencyForWritingSystems.Ptr,
                        m_JobDependencyForWritingSystems.Length);
                }

                return _JobHandle;
            }
            set
            {
                _GetDependencyFromSafetyManager = false;
                _JobHandle = value;
            }
        }

        unsafe protected void CompleteDependency()
        {
            // Previous frame job
            _JobHandle.Complete();

            // We need to get more job handles from other systems
            if (_GetDependencyFromSafetyManager)
            {
                _GetDependencyFromSafetyManager = false;
                CompleteDependencyInternal();
            }
        }

        /// <summary>
        /// Provides a mechanism for defining an entity query and invoking a lambda expression on each entity
        /// selected by that query.
        /// </summary>
        /// <remarks>
        /// The Entities property provides a convenient mechanism for implementing the most common operation
        /// performed by systems in ECS, namely, iterating over a set of entities to read and update component
        /// data. Entities provides a LINQ method-style syntax that you use to describe the work to be performed.
        /// Unity uses a compiler extension to convert the description into efficient, (optionally) multi-threaded
        /// executable code.
        /// 
        /// <example>
        /// <code source="../DocCodeSamples.Tests/SystemBaseExamples.cs" region="entities-foreach-basic" title="Basic ForEach Example"/>
        /// </example>
        /// 
        /// ##### **Describing the entity query**
        ///
        /// The components that you specify as parameters for your lambda function are automatically added to
        /// the entity query created for an Entities.Foreach construction. You can also add a number of "With"
        /// clauses to identify which entities that you want to process These clauses include:
        /// 
        /// * **`WithAll`** -- An entity must have all of these component types (in addition to having all
        /// the component types found in the lambda parameter list).
        ///   
        /// * **`WithAny`** -- An entity must have one or more of these component types. 
        /// 
        /// * **`WithNone`** -- An entity must not have any of these component types.
        /// 
        /// * **`WithChangeFilter()`** -- Only selects entities in chunks in which the specified component might have
        ///   changed since the last time this system instance updated.
        /// 
        /// * **`WithSharedComponentFilter(ISharedComponentData)`** -- Only select chunks that have a specified value
        ///   for a shared component.
        /// 
        /// * **`WithEntityQueryOptions(EntityQueryOptions)`** -- Specify additonal options defined in a
        ///   <see cref="EntityQueryOptions"/> object.
        /// 
        /// * **`WithStoreEntityQueryInField(EntityQuery)`** -- Stores the <see cref="EntityQuery"/> object generated
        ///   by the Entities.ForEach in an EntityQuery field on your system. You can use this EntityQuery object for
        ///   such purposes as getting the number of entities that will be selected by the query. Note that this function
        ///   assigns the EntityQuery instance to your field
        ///   when the system is created. This means that you can use the query before the first execution of the
        ///   lambda function.
        ///
        /// ##### **Defining the lambda function**
        ///
        /// Define the lambda function inside the `ForEach()` method of the entities property. When the system invokes the
        /// lambda function, it assigns values to the function parameters based on the current entity. You can pass ECS
        /// component types as parameters as well as a set of special, named parameters.
        ///
        /// 1. Parameters passed-by-value first (no parameter modifiers)
        /// 2. Writable parameters second(`ref` parameter modifier)
        /// 3. Read-only parameters last(`in` parameter modifier)
        ///
        /// All components should use either the `ref` or the `in` parameter modifier keywords.
        ///
        /// You can pass up to eight parameters to the lambda function. In addition to ECS component types, you can use
        /// the following:
        ///
        /// * **`Entity entity`** — the Entity instance of the current entity. (The parameter can be named anything as
        ///   long as the type is Entity.)
        /// 
        /// * **`int entityInQueryIndex`** — the index of the entity in the list of all entities selected by the query.
        ///   Use the entity index value when you have a [native array] that you need to fill with a unique value for
        ///   each entity. You can use the entityInQueryIndex as the index in that array. The entityInQueryIndex should
        ///   also be used as the jobIndex for adding commands to a concurrent <see cref="EntityCommandBuffer"/>.
        /// 
        /// * **`int nativeThreadIndex`** — a unique index for the thread executing the current iteration of the
        ///   lambda function. When you execute the lambda function using Run(), nativeThreadIndex is always zero.
        ///
        /// <example>
        /// <code source="../DocCodeSamples.Tests/SystemBaseExamples.cs" region="lambda-params" title="Lambda Parameters"/>
        /// </example>
        ///
        /// ##### **Capturing variables**
        ///
        /// You can capture local variables in the lambda function. When you execute the function using a job (by
        /// calling `ScheduleParallel()` or `ScheduleSingle()` instead of `Run()`) there are some restrictions on the
        /// captured variables and how you use them:
        ///
        /// * Only native containers and blittable types can be captured.
        /// * A job can only write to captured variables that are native containers.
        ///   (To “return” a single value, create a [native array] with one element.)
        ///
        /// You can use the following functions to apply modifiers and attributes to the captured [native container]
        /// variables, including [native arrays]. See [Job.WithCode] for a list of these modifiers and attributes.
        ///
        /// ##### **Executing the lambda function**
        ///
        /// To execute a ForEach construction, you have three options:
        /// 
        /// * **`ScheduleParallel()`** -- schedules the work to be done in parallel using the [C# Job] system. Each
        ///   parallel job instance processes at least one chunk of entities at a time. In other words, if all the
        ///   selected entities are in the same chunk, then only one job instance is spawned.
        ///
        /// * **`Schedule()`** -- schedules the work to be done in a single job (no matter how many entities are
        ///   selected).
        ///
        /// * **`Run()`** -- evaluates the entity query and invokes the lambda function for each selected entity
        ///   immediately on the main thread. Calling `Run()` completes the system <see cref="Dependency"/> [JobHandle]
        ///   before running, blocking the main thread, if necessary, while it waits for those jobs to finish.
        /// 
        /// When you call Schedule() or ScheduleParallel() without parameters, then the scheduled jobs use the current
        /// value of <see cref="Dependency"/>. You can also pass a [JobHandle] to these functions to define the dependencies
        /// of the scheduled job. In this case, the Entities.forEach construction returns a new [JobHandle] that adds the
        /// scheduled job to the passed in [JobHandle]. See <see cref="Dependency"/> for more information.
        ///
        /// ##### **Additional options**
        ///
        /// * **`WithName(string)`** -— assigns the specified string as the name of the generated job class. Assigning
        ///   a name is optional, but can help identify the function when debugging and profiling.
        /// * **`WithStructuralChanges()`** -— executes the lambda function on the main thread and disables Burst so
        ///   that you can make structural changes to your entity data within the function. For better performance, use
        ///   an <see cref="EntityCommandBuffer"/> instead.
        /// * **`WithoutBurst()`** —- disables Burst compilation. Use this function when your lambda function contains
        ///   code not supported by Burst or while debugging.
        /// * **`WithBurst(FloatMode, FloatPrecision, bool)`** — sets options for the Burst compiler:
        ///    * **floatMode** —- sets the floating point math optimization mode.Fast mode executes faster, but
        ///      produces larger floating point error than Strict mode.Defaults to Strict. See [Burst FloatMode].
        ///    * **floatPrecision** —- sets the floating point math precision. See [Burst FloatPrecision].
        ///    * **synchronousCompilation** —- compiles the function immediately instead of scheduling the function
        ///      for compilation later.
        ///
        /// [Job.WithCode]: xref:Unity.Entities.SystemBase.Job
        /// [JobHandle]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html
        /// [native array]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html
        /// [native arrays]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html
        /// [native container]: https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute.html
        /// [C# Job]: https://docs.unity3d.com/2019.2/Documentation/Manual/JobSystem.html
        /// [Burst FloatMode]: https://docs.unity3d.com/Packages/com.unity.burst@latest?subfolder=/api/Unity.Burst.FloatMode.html
        /// [Burst FloatPrecision]: https://docs.unity3d.com/Packages/com.unity.burst@latest?subfolder=/api/Unity.Burst.FloatPrecision.html
        /// </remarks>
        protected internal ForEachLambdaJobDescription Entities => new ForEachLambdaJobDescription();

#if ENABLE_DOTS_COMPILER_CHUNKS
            /// <summary>
            /// Use query.Chunks.ForEach((ArchetypeChunk chunk, int chunkIndex, int indexInQueryOfFirstEntity) => { YourCodeGoesHere(); }).Schedule();
            /// </summary>
            public LambdaJobChunkDescription Chunks
            {
                get
                {
                    return new LambdaJobChunkDescription();
                }
            }
#endif

        /// <summary>
        /// Provides a mechanism for defining and executing an [IJob].
        /// </summary>
        /// <remarks>
        /// The Jobs property provides a convenient mechanism for implementing single jobs. Unity uses a compiler
        /// extension to convert the job description you create with Job.WithCode into efficient, executable code that
        /// (optionally) runs in a background thread.
        /// 
        /// <example>
        /// <code source="../DocCodeSamples.Tests/LambdaJobExamples.cs" region="job-with-code-example" title="Basic Job Example"/>
        /// </example>
        /// 
        /// Implement your lambda function inside the `Job.WithCode(lambda)` function. The lambda function cannot
        /// take any parameters. You can capture local variables.
        ///
        /// * `Schedule()` -- executes the lambda function as a single job.
        /// * `Run()` -- executes immediately on the main thread. Immediately before it invokes `Run()` the system
        ///    completes all jobs with a [JobHandle] in the system <see cref="Dependency"/> property as well as any
        ///    jobs with a [JobHandle] passed as a dependency to `Run()` as an (optional) parameter.
        ///    
        /// When scheduling a job, you can pass a [JobHandle] to set the job's dependencies explicitly and the
        /// construction returns the updated [JobHandle] combining the earlier dependencies with the new job. If you
        /// do not provide a [JobHandle], the system uses <see cref="Dependency"/> when scheduling the job, and updates
        /// the property to include the new job automatically.
        ///
        /// You can use the additional options listed for [Entities.ForEach] with a `Job.WithCode` construction.
        ///
        /// ##### **Capturing variables**
        ///
        /// You can capture local variables in the lambda function. When you execute the function using a job (by
        /// calling `Schedule()`, `ScheduleParallel()` or `ScheduleSingle()` instead of `Run()`) there are some
        /// restrictions on the captured variables and how you use them:
        ///
        /// * Only native containers and blittable types can be captured.
        /// * A job can only write to captured variables that are native containers.
        ///   (To “return” a single value, create a [native array] with one element.)
        ///
        /// You can use the following functions to apply modifiers and attributes to the captured [native container]
        /// variables, including [native arrays]:
        ///
        /// * **`WithReadOnly(myvar)`** — restricts access to the variable as read-only.
        ///
        /// * **`WithDeallocateOnJobCompletion(myvar)`** — deallocates the native container after the job is complete.
        ///   See [DeallocateOnJobCompletionAttribute].
        ///
        /// * **`WithNativeDisableParallelForRestriction(myvar)`** — permits multiple threads to access the same
        ///   writable native container. Parallel access is only safe when each thread only accesses its own, unique
        ///   range of elements in the container. If more than one thread accesses the same element a race condition is
        ///   created in which the timing of the access changes the result. See [NativeDisableParallelForRestriction].
        ///
        /// * **`WithNativeDisableContainerSafetyRestriction(myvar)`** — disables normal safety restrictions that
        ///   prevent dangerous access to the native container. Disabling safety restrictions unwisely can lead to race
        ///   conditions, subtle bugs, and crashes in your application. See [NativeDisableContainerSafetyRestrictionAttribute].
        ///
        /// * **`WithNativeDisableUnsafePtrRestrictionAttribute(myvar)`** — Allows you to use unsafe pointers provided
        ///   by the native container. Incorrect pointer use can lead to subtle bugs, instability, and crashes in your
        ///   application. See [NativeDisableUnsafePtrRestrictionAttribute].
        ///
        /// [DeallocateOnJobCompletionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.DeallocateOnJobCompletionAttribute.html
        /// [NativeDisableParallelForRestriction]: https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html
        /// [NativeDisableContainerSafetyRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute.html
        /// [NativeDisableUnsafePtrRestrictionAttribute]: https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute.html
        /// [JobHandle]: https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html
        /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
        /// [IJob]: https://docs.unity3d.com/2019.2/Documentation/ScriptReference/Unity.Jobs.IJob.html
        /// </remarks>
        protected internal LambdaSingleJobDescription Job
        {
            get { return new LambdaSingleJobDescription(); }
        }

        void BeforeOnUpdate()
        {
            BeforeUpdateVersioning();

            // We need to wait on all previous frame dependencies, otherwise it is possible that we create infinitely long dependency chains
            // without anyone ever waiting on it
            _JobHandle.Complete();
            _GetDependencyFromSafetyManager = true;
        }
#pragma warning disable 649
        private unsafe struct JobHandleData
        {
            public void* jobGroup;
            public int version;
        }
#pragma warning restore 649

        unsafe void AfterOnUpdate(bool throwException)
        {
            AfterUpdateVersioning();

            // If outputJob says no relevant jobs were scheduled,
            // then no need to batch them up or register them.
            // This is a big optimization if we only Run methods on main thread...
            var outputJob = _JobHandle;
            if (((JobHandleData*) &outputJob)->jobGroup != null)
            {
                JobHandle.ScheduleBatchedJobs();
                _JobHandle = m_DependencyManager->AddDependency(m_JobDependencyForReadingSystems.Ptr,
                    m_JobDependencyForReadingSystems.Length, m_JobDependencyForWritingSystems.Ptr,
                    m_JobDependencyForWritingSystems.Length, outputJob);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (JobsUtility.JobDebuggerEnabled)
            {
                var dependencyError = SystemDependencySafetyUtility.CheckSafetyAfterUpdate(this, ref m_JobDependencyForReadingSystems, ref m_JobDependencyForWritingSystems, m_DependencyManager);
                if (throwException && dependencyError != null)
                    throw new InvalidOperationException(dependencyError);
            }
#endif
        }

        /// <summary>
        /// Update the system manually.
        /// </summary>
        /// <remarks>
        /// Most programs should not update systems manually. SystemBase implementations
        /// cannot override `Update()`. Instead, implement system behavior in <see cref="OnUpdate"/>.
        /// </remarks>
        public sealed override void Update()
        {
#if ENABLE_PROFILER
            using (m_ProfilerMarker.Auto())
#endif
            {
                if (Enabled && ShouldRunSystem())
                {
                    if (!m_PreviouslyEnabled)
                    {
                        m_PreviouslyEnabled = true;
                        OnStartRunning();
                    }

                    BeforeOnUpdate();

    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var oldExecutingSystem = ms_ExecutingSystem;
                    ms_ExecutingSystem = this;
    #endif
                    try
                    {
                        OnUpdate();
                    }
                    catch
                    {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                        ms_ExecutingSystem = oldExecutingSystem;
    #endif

                        AfterOnUpdate(false);
                        throw;
                    }

    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    ms_ExecutingSystem = oldExecutingSystem;
    #endif

                    AfterOnUpdate(true);
                }
                else if (m_PreviouslyEnabled)
                {
                    m_PreviouslyEnabled = false;
                    OnStopRunning();
                }                
            }
        }

        internal sealed override void OnBeforeCreateInternal(World world)
        {
            base.OnBeforeCreateInternal(world);
        }

        internal sealed override void OnBeforeDestroyInternal()
        {
            base.OnBeforeDestroyInternal();
            _JobHandle.Complete();
        }

        /// <summary>Implement `OnUpdate()` to perform the major work of this system.</summary>
        /// <remarks>
        /// The system invokes `OnUpdate()` once per frame on the main thread when any of this system's
        /// [EntityQueries] match existing entities, the system has the [AlwaysUpdateSystem] 
        /// attribute, or the system has no queries at all. `OnUpdate()` is triggered by the system's
        /// parent system group, which calls the <see cref="Update"/> method of all its child
        /// systems in its own `OnUpdate()` function. The `Update()` function evaluates whether a system
        /// should, in fact, update before calling `OnUpdate()`.
        ///
        /// The [Entities.ForEach] and [Job.WithCode] constructions provide convenient mechanisms for defining jobs.
        /// You can also instantiate and schedule an <see cref="IJobChunk"/> instance; you can use the
        /// [C# JobSystem] or you can perform work on the main thread. If you call <see cref="EntityManager"/> methods that
        /// perform structural changes on the main thread, be sure to arrange the system order to minimize the
        /// performance impact of the resulting [sync points].
        ///
        /// [sync points]: xref:sync-points
        /// [C# Job System]: https://docs.unity3d.com/2019.2/Documentation/Manual/JobSystem.html
        /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
        /// [Job.WithCode]: xref:Unity.Entities.SystemBase.Job
        /// [EntityQueries]: xref:Unity.Entities.EntityQuery
        /// [AlwaysUpdate]: xref:Unity.Entities.AlwaysUpdateSystemAttribute
        /// </remarks>
        protected abstract void OnUpdate();
        
        /// <summary>
        /// Look up the value of a component for an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A struct of type T containing the component value.</returns>
        /// <remarks>
        /// Use this method to look up data in another entity using its <see cref="Entity"/> object. For example, if you
        /// have a component that contains an Entity field, you can look up the component data for the referenced
        /// entity using this method.
        ///
        /// When iterating over a set of entities via [Entities.ForEach], do not use this method to access data of the
        /// current entity in the set. This function is much slower than accessing the data directly (by passing the
        /// component containing the data to your lambda iteration function as a parameter).
        ///
        /// When you call this method on the main thread, it invokes <see cref="EntityManager.GetComponentData{T}"/>.
        /// (An [Entities.ForEach] function invoked with `Run()` executes on the main thread.) When you call this method
        /// inside a job scheduled using [Entities.ForEach], this method gets replaced with component access methods
        /// through <see cref="ComponentDataFromEntity{T}"/>.
        ///
        /// In both cases, this lookup method results in a slower, indirect memory access. When possible, organize your
        /// data to minimize the need for indirect lookups.
        ///
        /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        protected internal T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            return EntityManager.GetComponentData<T>(entity);
        }
        
        /// <summary>
        /// Sets the value of a component of an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="component">The data to set.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <remarks>
        /// Use this method to look up and set data in another entity using its <see cref="Entity"/> object. For example, if you
        /// have a component that contains an Entity field, you can update the component data for the referenced
        /// entity using this method.
        ///
        /// When iterating over a set of entities via [Entities.ForEach], do not use this method to update data of the
        /// current entity in the set. This function is much slower than accessing the data directly (by passing the
        /// component containing the data to your lambda iteration function as a parameter).
        ///
        /// When you call this method on the main thread, it invokes <see cref="EntityManager.SetComponentData{T}"/>.
        /// (An [Entities.ForEach] function invoked with `Run()` executes on the main thread.) When you call this method
        /// inside a job scheduled using [Entities.ForEach], this method gets replaced with component access methods
        /// through <see cref="ComponentDataFromEntity{T}"/>.
        ///
        /// In both cases, this lookup method results in a slower, indirect memory access. When possible, organize your
        /// data to minimize the need for indirect lookups.
        ///
        /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        protected internal void SetComponent<T>(Entity entity, T component) where T : struct, IComponentData
        {
            EntityManager.SetComponentData(entity, component);
        }
        
        /// <summary>
        /// Checks whether an entity has a specific type of component.
        /// </summary>
        /// <param name="entity">The Entity object.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <remarks>
        /// Always returns false for an entity that has been destroyed.
        ///
        /// Use this method to check if another entity has a given type of component using its <see cref="Entity"/>
        /// object. For example, if you have a component that contains an Entity field, you can check whether the
        /// referenced entity has a specific type of component using this method. (Entities in the set always have
        /// required components, so you don’t need to check for them.)
        ///
        /// When iterating over a set of entities via [Entities.ForEach], avoid using this method with the
        /// current entity in the set. It is generally faster to change your entity query methods to avoid
        /// optional components; this may require a different [Entities.ForEach] construction to handle
        /// each combination of optional and non-optional components.
        ///
        /// When you call this method on the main thread, it invokes <see cref="EntityManager.HasComponent{T}"/>.
        /// (An [Entities.ForEach] function invoked with `Run()` executes on the main thread.) When you call this method
        /// inside a job scheduled using [Entities.ForEach], this method gets replaced with component access methods
        /// through <see cref="ComponentDataFromEntity{T}"/>.
        ///
        /// In both cases, this lookup method results in a slower, indirect memory access. When possible, organize your
        /// data to minimize the need for indirect lookups.
        ///
        /// [Entities.ForEach]: xref:Unity.Entities.SystemBase.Entities
        /// </remarks>
        /// <returns>True, if the specified entity has the component.</returns>
        protected internal bool HasComponent<T>(Entity entity) where T : struct, IComponentData
        {
            return EntityManager.HasComponent<T>(entity);
        }
    }
}