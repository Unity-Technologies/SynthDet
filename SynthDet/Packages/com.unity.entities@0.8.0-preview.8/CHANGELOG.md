# Change log

## [0.8.0] - 2020-03-13

### Added

* Added missing dynamic component version API: `ArchetypeChunk.GetComponentVersion(ArchetypeChunkComponentTypeDynamic)`
* Added missing dynamic component has API: `ArchetypeChunk.Has(ArchetypeChunkComponentTypeDynamic)`
* `EntityArchetype` didn't expose whether it was Prefab or not. Added bool `EntityArchetype.Prefab`. This is needed for meta entity queries, because meta entity queries don't avoid Prefabs.
* Added Build Configurations and Build Pipelines for Linux
* LiveLink now gives an error if a LiveLink player attempts to connect to the wrong Editor, and advises the user on how to correct this.

### Changed

* Optimized `ArchetypeChunkComponentTypeDynamic` memory layout. 48->40 bytes.
* LiveLink: Editor no longer freezes when sending LiveLink assets to a LiveLinked player.
* LiveLink: No longer includes every Asset from builtin_extra to depend on a single Asset, and sends only what is used. This massively speeds up the first-time LiveLink to a Player.
* Upgraded Burst to fix multiple issues and introduced native debugging feature.

### Deprecated

* Types that implement `IJobForEach` interfaces have been deprecated.  Use `IJobChunk` and `Entities.ForEach` for these jobs.

### Fixed

* Fixed LiveLinking with SubScene Sections indices that were not contiguous (0, 1, 2..). Now works with whatever index you use.
* Fixed warning when live converting disabled GameObjects.
* Allow usage of `Entities.WithReadOnly`, `Entities.WithDeallocateOnJobCompletion`, `Entities.WithNativeDisableContainerSafetyRestriction`, and `Entities.WithNativeDisableParallelForRestriction` on types that contain valid NativeContainers.


## [0.7.0] - 2020-03-03

### Added

* Added `HasComponent`/`GetComponent`/`SetComponent` methods that streamline access to components through entities when using the `SystemBase` class.  These methods call through to `EntityManager` methods when in OnUpdate code and codegen access through `ComponentDataFromEntity` when inside of `Entities.ForEach`.
* `SubScene` support for hybrid components, allowing Editor LiveLink (Player LiveLink is not supported yet).
* Added `GameObjectConversionSettings.Systems` to allow users to explicitly specify what systems should be included in the conversion

### Changed

* Fixed an issue where shared component filtering could be broken until the shared component data is manually set/added when using a deserialized world.
* Users can control the update behaviour of a `ComponentSystemGroup` via an update callback.  See the documentation for `ComponentSystemGroup.UpdateCallback`, as well as examples in `FixedRateUtils`.
* `IDisposable` and `ICloneable` are now supported on managed components.
* `World` now exposes a `Flags` field allowing the editor to improve how it filters world to show in various tooling windows.
* `World.Systems` is now a read only collection that does not allocate managed memory while being iterated over.
* Updated package `com.unity.platforms` to version `0.2.1-preview.4`.

### Deprecated

* Property `World.AllWorlds` is now replaced by `World.All` which now returns a read only collection that does not allocate managed memory while being iterated over.

### Removed

* Removed expired API `implicit operator GameObjectConversionSettings(World)`
* Removed expired API `implicit operator GameObjectConversionSettings(Hash128)`
* Removed expired API `implicit operator GameObjectConversionSettings(UnityEditor.GUID)`
* Removed expired API `TimeData.deltaTime`
* Removed expired API `TimeData.time`
* Removed expired API `TimeData.timeSinceLevelLoad`
* Removed expired API `TimeData.captureFramerate`
* Removed expired API `TimeData.fixedTime`
* Removed expired API `TimeData.frameCount`
* Removed expired API `TimeData.timeScale`
* Removed expired API `TimeData.unscaledTime`
* Removed expired API `TimeData.captureDeltaTime`
* Removed expired API `TimeData.fixedUnscaledTime`
* Removed expired API `TimeData.maximumDeltaTime`
* Removed expired API `TimeData.realtimeSinceStartup`
* Removed expired API `TimeData.renderedFrameCount`
* Removed expired API `TimeData.smoothDeltaTime`
* Removed expired API `TimeData.unscaledDeltaTime`
* Removed expired API `TimeData.fixedUnscaledDeltaTime`
* Removed expired API `TimeData.maximumParticleDeltaTime`
* Removed expired API `TimeData.inFixedTimeStep`
* Removed expired API `ComponentSystemBase.OnCreateManager()`
* Removed expired API `ComponentSystemBase.OnDestroyManager()`
* Removed expired API `ConverterVersionAttribute(int)`

### Fixed

* Non-moving children in transform hierarchies no longer trigger transform system updates.
* Fixed a bug where dynamic buffer components would sometimes leak during live link.
* Fixed crash that would occur if only method in a module was generated from a `[GenerateAuthoringComponent]` type.
* `Entities.ForEach` now throws a correct error message when it is used with a delegate stored in a variable, field or returned from a method.
* Fix IL2CPP compilation error with `Entities.ForEach` that uses a tag component and `WithStructuralChanges`.
* `Entities.ForEach` now marshals lambda parameters for DOTS Runtime when the lambda is burst compiled and has collection checks enabled. Previously using `EntityCommandBuffer` or other types with a `DisposeSentinel` field as part of your lambda function (when using DOTS Runtime) may have resulted in memory access violation.
* `.Run()` on `IJobChunk` may have dereferenced null or invalid chunk on filtered queries.


### Security

* Throw correct error message if accessing `ToComponentDataArrayAsync` `CopyFromComponentDataArray` or `CopyFromComponentDataArrayAsync` from an unrelated query.



## [0.6.0] - 2020-02-17

### Added

* The `[GenerateAuthoringComponent]` attribute is now allowed on structs implementing `IBufferElementData`. An authoring component is automatically generated to support adding a `DynamicBuffer` of the type implementing `IBufferElementData` to an entity.
* Added new `SystemBase` base class for component systems.  This new way of defining component systems manages dependencies for the user (manual dependency management is still possible by accessing the `SystemBase.Dependency` field directly).
* New `ScheduleParallel` methods in `IJobChunk` and `Entities.ForEach` (in `SystemBase`) to make parallel scheduling of jobs explicit.  `ScheduleSingle` in `IJobChunk` indicates scheduling work to be done in a non-parallel manner.
* New editor workflow to quickly and easily build LiveLink player using the `BuildConfiguration` API.
* Adds Live Link support for `GameObject` scenes.
* The `SceneSystem` API now also loads `GameObject` scenes via `LoadSceneAsync` API.
* Added new build component for LiveLink settings in `Unity.Scenes.Editor` to control how initial scenes are handled (LiveLink all, embed all, embed first).
* Users can now inspect post-procssed IL code inside Unity Editor: `DOTS` -> `DOTS Compiler` -> `Open Inspector`
* `GetAssignableComponentTypes()` can now be called with or without a List&lt;Type&gt; argument to collect the data. When omitted, the list will be allocated, which is the same behavior as before.

### Changed

 * The package `com.unity.build` has been merged into the package `com.unity.platforms`. As such, removed the dependency on `com.unity.build@0.1.0-preview` and replaced it with `com.unity.platforms@0.2.1-preview.1`. Please read the changelog of `com.unity.platforms` for more details.
* Managed components are now stored in a way that will generate less GC allocations when entities change archetype.
* Moved `Unity.Entities.ICustomBootstrap` from Unity.Entities.Hybrid to Unity.Entities.
* `World.Dispose()` now completes all reader/writer jobs on the `World`'s `EntityManager` before releasing any resources, to avoid use-after-free errors.
* Fix `AssemblyResolveException` when loading a project with dependent packages that are using Burst in static initializers or `InitializeOnLoad`.
* `.sceneWithBuildSettings` files that are stored in Assets/SceneDependencyCache are no longer rebuilt constantly. Because they are required for SubScene behaviour to work in the editor, if these are deleted they are recreated by OnValidate of the SubScene in the edited Scene. They should also be recreated on domain reload (restarting unity, entering/exiting playmode, etc).
* `EntityQuery.cs`: Overloads of `CreateArchetypeChunkArray`, `ToComponentDataArray`, `ToEntityArray`, and `CopyFromComponentDataArray` that return a JobHandle (allowing the work to be done asynchronously) have been renamed to add `Async` to the title (i.e. `ToComponentDataArrayAsync`). The old overloads have been deprecated and an API Updater clause has been added.
 * `Entities.WithName` now only accepts names that use letters, digits, and underscores (not starting with a digit, no two consecutive underscores)
 * Updated package `com.unity.properties` to version `0.10.4-preview`.
 * Updated package `com.unity.serialization` to version `0.6.4-preview`.
 * The entity debugger now remembers whether chunk info panel is visible
 * The entity debugger now displays the full name for nested types in the system list
 * The entity debugger now sorts previously used filter components to the top of the filter GUI
 * Bumped burst version to include the new features and fixes including:
 * Fix an issue with function pointers being corrupted after a domain reload that could lead to hard crashes.
 * Fix potential deadlock between Burst and the AssetDatabase if burst is being used when building the database.

### Deprecated

 * Method `GetBuildSettingsComponent` on class `GameObjectConversionSystem` has been renamed to `GetBuildConfigurationComponent`.
 * Method `TryGetBuildSettingsComponent` on class `GameObjectConversionSystem` has been renamed to `TryGetBuildConfigurationComponent`.
 * Member `BuildSettings` on class `GameObjectConversionSettings` has been renamed to `BuildConfiguration`.
 * Member `BuildSettingsGUID` on class `SceneSystem` has been renamed to `BuildConfigurationGUID`.

### Removed

 * Removed expired API `SceneSectionData.SharedComponentCount`
 * Removed expired API `struct SceneData`
 * Removed expired API `SubScene._SceneEntities`
 * Removed expired API `World.Active`

### Fixed

* Ability to open and close SubScenes from the scene hierarchy window (Without having to move cursor to inspector window).
* Ability to create a new empty Sub Scene without first creating a game object.
* Improve performance of SubScene loading and change tracking in the editor.
* Fixed regression where `GetSingleton` would create a new query on every call.
* Fixed SubScenes trying to load an already loaded AssetBundle when loaded multiple times on the same player, but with different Worlds.
* Make it clear that SubScenes in Prefabs are not supported.
* Lambda job codegen tests now fail if the error message does not contain the expected contents.
* Improved performance of setting up the world required for game object conversion
* The `chunkIndex` parameter passed to `IJobChunk.Execute()` now has the correct value.
* Fixed an error which caused entities with `ISystemStateSharedComponentData` components to not be cleaned up correctly.
* Managed components containing `Entity` fields will now correctly serialize.
* Fixed issue where `BlobAssetVerifier` will throw error if it can't resolve a type.
* Exposed the Managed Component extensions for `EntityQuery`.
* `Entities.ForEach` now identifies when `this` of the enclosing system is captured due to calling an extension method on it when compilation fails since the lambda was emitted as a member function
* `Entities.ForEach` now reports when a field of the outer system is captured and used by reference when compilation fails since the lambda was emitted as a member function
* `Entities.ForEach` does not erronously point to calling static functions as the source of the error when compilation fails since the lambda was emitted as a member function
* Debugging inside of `Entities.ForEach` with Visual Studio 2017/2019 (some debugging features will need an upcoming update of the com.unity.ide.visualstudio package).
* `EntityQuery.ToComponentArray<T>` with `T` deriving from `UnityEngine.Component` now correctly collects all data in a chunk
* Fixed an issue with `ComponentSystemBase.GetEntityQuery` and `EntityManager.CreateEntityQuery` calls made with `EntityQueryDesc` not respecting read-only permissions.


## [0.5.1] - 2020-01-28

### Changed

* Constructor-related exceptions thrown during `World.CreateSystem` will now included the inner exception details.
* `DefaultWorldInitialization.GetAllSystems` now returns `IReadOnlyList<Type>` instead of `List<Type>`
* `DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups` now takes `IEnumerable<Type>` instead of `List<Type>`

### Fixed

* Fixed an issue where `BlobAssetReference` types was not guaranteed to be 8-byte aligned on all platforms which could result in failing to read Blob data in components correctly on 32-bit platforms.
* Fixed issue in `MinMaxAABB.Equals()` comparing `Min` to itself rather than `other`.
* `Entities.ForEach` now properly treats `in` parameters of `DynamicBuffer` type as read-only
* Fixed potential crash caused by a leaked job after an exception is thrown during a call to `IJobChunk.Schedule`.
* Fixed regression in `ComponentSystemBase.GetSingleton()` where a new query would be created every timee the function is called.


## [0.5.0] - 2020-01-16

### Added

* Added AndroidHybrid.buildpipeline with RunStepAndroid

### Changed

* `Entities.WithReadOnly`, `Entities.WithNativeDisableParallelForRestriction`, `Entities.WithDeallocateOnJobCompletion`, `Entities.WithNativeDisableSafetyRestriction` and `Entities.WithNativeDisableUnsafePtrRestriction` now check their argument types for the proper attributes (`[NativeContainer]`, `[NativeContainerSupportsDeallocateOnJobCompletion]`) at compile time and throw an error when used on a field of a user defined type.
* Log entries emitted during subscene conversion without a context object are now displayed in the subscene inspector instead of discarded

### Deprecated

* Adding removal dates to the API that have been deprecated but did not have the date set.
* `BlobAssetReference<T>`: `Release()` was deprecated, use `Dispose()` instead.

### Removed

 * Adding removal dates to the API that have been deprecated but did not have the date set.
 * `BlobAssetReference<T>`: `Release()` was deprecated, use `Dispose()` instead.
 * `EntityQuery.cs`: Removed expired API `CalculateLength()`, `SetFilter()` and `SetFilterChanged()`.

### Fixed
* Fixed an issue where trying to perform EntityRemapping on Managed Components could throw if a component field was null.
* `EntityManager.MoveEntitiesFrom` with query was not bumping shared component versions, order versions or dirty versions correctly. Now it does.
* Fixed that adding a Sub Scene component from the Add Components dropdown was not reflected in the Hierarchy.
* Fixed so that Undo/Redo of changes to SceneAsset objectfield in the Sub Scene Inspector is reflected in the Hierarchy.
* Make it clear when Sub Scene duplicates are present: shown in Hierarchy and by showing a warning box in the Inspector.
* Support Undo for 'Create Sub Scene From Selection' context menu item.
* Better file name error handling for the 'New Sub Scene From Selection' context menu item.
* Keep sibling order for new Sub Scene when created using 'New Sub Scene From Selection' (prevents the new Sub Scene from ending as the last sibling).
* Handle if selection contains part of a Prefab instance when creating Sub Scene from Selection.
* Fix dangling loaded Sub Scenes not visualized in the Hierarchy when removing Scene Asset reference in Sub Scene component.
* Fixed an issue with invalid IL generated by `Entities.ForEach` when structs are captured as locals from two different scopes and their fields are accessed.
* Make it clear in the Hierarchy and Sub Scene Inspector that nesting Sub Scenes is not yet supported.
* Fixed an issue with `BinaryWriter` where serializing a `System.String[]` with a single element would throw an exception.
* Fixed an issue with `ComponentSystem.GetEntityQuery` and `JobComponentSystem.GetEntityQuery` which caused improper caching of queries when using "None" or "Any" fields.


## [0.4.0] - 2019-12-16

**This version requires Unity 2019.3.0f1+**

### New Features

* Two new methods added to the public API:
  * `void EntityCommandBuffer.AddComponent<T>(EntityQuery entityQuery)`
  * `void EntityCommandBuffer.RemoveComponent<T>(EntityQuery entityQuery)`
* BlobArray, BlobString & BlobPtr are not allowed to be copied by value since they carry offset pointers that aree relative to the location of the memory. This could easily result in programming mistakes. The compiler now prevents incorrect usage by enforcing any type attributed with [MayOnlyLiveInBlobStorage] to never be copied by value.

### Changes

* Deprecates `TypeManager.CreateTypeIndexForComponent` and it's other component type variants. Types can be dynamically added (in Editor builds) by instead passing the new unregistered types to `TypeManager.AddNewComponentTypes` instead.
* `RequireForUpdate(EntityQuery)` and `RequireSingletonForUpdate` on a system with `[AlwaysUpdate]` will now throw an exception instead of being ignored.
* ChangeVersionUtility.IncrementGlobalSystemVersion & ChangeVersionUtility.InitialGlobalSystemVersion is now internal. They were accidentally public previously.
* Entity inspector now shows entity names and allows to rename the selected entity
* Improved entity debugger UI
* Create WorldRenderBounds for prefabs and disabled entities with renderers during conversion, this make instantiation of those entities significantly faster.
* Reduced stack depth of System.Update / OnUpdate method (So it looks better in debugger)
* Assert when using EntityQuery from another world
* Using an EntityQuery created in one world on another world was resulting in memory corruption. We now detect it in the EntityManager API and throw an argument exception
* Structural changes now go through a bursted codepath and are significantly faster
* DynamicBuffer.Capacity is now settable

### Fixes

* Remove unnecessary & incorrect warning in DeclareReferencedPrefab when the referenced game object is a scene object
* GameObjects with ConvertAndInject won't get detached from a non-converted parent (fixes regression)
* Fixed a crash that could occur when destroying an entity with an empty LinkedEntityGroup.
* Updated performance package dependency to 1.3.2 which fixes an obsoletion warning
* The `EntityCommandBuffer` can be replayed repeatedly.
* Fixed exception in entity binary scene serialization when referencing a null UnityEngine.Object from a shared component
* Moving scripts between assemblies now triggers asset bundle rebuilds where necessary for live link
* Fixed LiveLink on Android


## [0.3.0] - 2019-12-03

### New Features

* ENABLE_SIMPLE_SYSTEM_DEPENDENCIES define can now be used to replace the automatic dependency chaining with a much simplified strategy. With ENABLE_SIMPLE_SYSTEM_DEPENDENCIES it simply chains jobs in the order of the systems against previous jobs. Without ENABLE_SIMPLE_SYSTEM_DEPENDENCIES, dependencies are automatically chained based on read / write access of component data of each system. In cases when there game code is forced to very few cores or there are many systems, this can improve performance since it reduces overhead in calculating optimal dependencies.
* Added `DebuggerTypeProxy` for `MultiListEnumerator<T>` (e.g. this makes the results of `GameObjectConversionSystem.GetEntities` calls readable in the debugger)
* Two new methods added to the public API:
  * EntityManager.CreateEntity(Archetype type, int count, Allocator allocator);
  * EntityManager.Instantiate(Entity entity, int count, Allocator allocator);
  Both methods return a `NativeArray<Entity>`.

### Changes

Removed the following deprecated API as announced in/before `0.1.1-preview`:

* From GameObjectConversionUtility.cs: `ConvertIncrementalInitialize()` and `ConvertScene()`.
* From Translation.cs: `struct Position`.
* From EditorEntityScenes.cs: `WriteEntityScene()`.
* From GameObjectConversionSystem.cs: `AddReferencedPrefab()`, `AddDependency()`, `AddLinkedEntityGroup()`, `DstWorld`.
* From DefaultWorld.cs: `class EndPresentationEntityCommandBufferSystem`.

### Fixes

* ConvertAndInject won't destroy the root GameObject anymore (fixes regression introduced in 0.2.0)
* Fix Android/iOS build when using new build pipeline
  * Provide correct application extension apk, aab or empty for project export when building to Android


## [0.2.0] - 2019-11-22

**This version requires Unity 2019.3 0b11+**

### New Features

* Automatically generate authoring components for IComponentData with IL post-processing. Any component data marked with a GenerateAuthoringComponent attribute will generate the corresponding authoring MonoBehaviour with a Convert method.
* BuildSettings assets are now used to define a single build recipe asset on disk. This gives full control over the build pipeline in a modular way from C# code.
  * BuildSettings let you attach builtin or your own custom IBuildSettingsComponents for full configurability
  * BuildPipelines let you define the exact IBuildStep that should be run and in which order
  * IBuildStep is either builtin or your own custom build step
  * BuildSettings files can be inherited so you can easily make base build settings with most configuration complete and then do minor adjustments per build setting
  * Right now most player configuration is still in the existing PlayerSettings, our plan is to over time expose all Player Settings via BuildSettings as well to ease configuration of complex projects with many build recipes & artifacts
* SubScenes are now automatically converted to entity binary files & cached by the asset pipeline. The entity cache files previously present in the project folder should be removed. Conversion systems can use the ConverterVersion attribute to convert to trigger a reconversion if the conversion system has changed behaviour. The conversion happens asynchronously in another process. Thus on first open the subscenes might not show up immediately.

* Live link builds can be built with the new BuildSettings pipeline.
  Open sub scene
  * Closed Entity scenes are built by the asset pipeline and loaded via livelink on demand
  * Opened Entity scenes are send via live entity patcher with patches on a per component / entity basis based on what has changed
  * Assets referenced by entity scenes are transferred via livelink when saving the asset
  * Scenes loaded as game objects are currently not live linked (This is in progress)
 by assigning the LiveLink build pipeline

* `Entities.ForEach` syntax for supplying jobified code in a `JobComponentSystem`'s `OnUpdate` method directly by using a lambda (instead of supplying an additional `IJobForEach`).

* `EntityQueryMask` has been added, which allows for quick confirmation of if an Entity would be returned by an `EntityQuery` without filters via `EntityQueryMask.Matches(Entity entity)`.  An EntityQueryMask can be obtained by calling `EntityManager.GetEntityQueryMask(EntityQuery query).`
* Unity Entities now supports the _Fast Enter playmode_ which can be enabled in the project settings. It is recommended to be turned on for all dots projects.
* The UnityEngine component `StopConvertToEntity` can be used to interrupt `ConvertToEntity` recursion, and should be preferred over a `ConvertToEntity` set to "convert and inject" for that purpose.
* _EntityDebugger_ now shows IDs in a separate column, so you can still see them when entities have custom names
* Entity references in the Entity Inspector have a "Show" button which will select the referenced Entity in the Debugger.
* An `ArchetypeChunkIterator` can be created by calling `GetArchetypeChunkIterator` on an `EntityQuery`. You may run an `IJobChunk` while bypassing the Jobs API by passing an `ArchetypeChunkIterator` into `IJobChunk.RunWithoutJobs()`.
* The `[AlwaysSynchronizeSystem]` attribute has been added, which can be applied to a `JobComponentSystem` to force it to synchronize on all of its dependencies before every update.
* `BoneIndexOffset` has been added, which allows the Animation system to communicate a bone index offset to the Hybrid Renderer.
* Initial support for using Hybrid Components during conversion, see the HybridComponent sample in the StressTests folder.
* New `GameObjectConversionSystem.ForkSettings()` that provides a very specialized method for creating a fork of the current conversion settings with a different "EntityGuid namespace", which can be used for nested conversions. This is useful for example in net code where multiple root-level variants of the same authoring object need to be created in the destination world.
* `EntityManager` `LockChunkOrder` and `UnlockChunkOrder` are deprecated.
* Entity Scenes can be loaded synchronously (during the next streaming system update) by using `SceneLoadFlags.BlockOnStreamIn` in `SceneSystem.LoadParameters`.
* `EntityCommandBuffer` can now be played back on an `ExclusiveEntityTransaction` as well as an `EntityManager`. This allows ECB playback to   be invoked from a job (though exclusive access to the EntityManager data is still required for the duration of playback).

### Upgrade guide
* If you are using SubScenes you must use the new BuildSettings assets to make a build & run it. SubScenes are not supported from the File -> BuildSettings... & File -> Build and Run workflows.
* Entities requires AssetDatabase V2 for certain new features, we do not provide support for AssetDatabase V1.

### Fixes

* Setting `ComponentSystemGroup.Enabled` to `false` now calls `OnStopRunning()` recursively on the group's member systems, not just on the group itself.
* Updated Properties pacakge to `0.10.3-preview` to fix an exception when showing Physics ComponentData in the inspector as well as fix IL2CPP Ahead of Time linker errors for generic virtual function calls.
* The `LocalToParentSystem` will no longer write to the `LocalToWorld` component of entities that have a component with the `WriteGroup(typeof(LocalToWorld))`.
* Entity Debugger styling work better with Pro theme
* Entity Inspector no longer has runaway indentation
* Fixed issue where `AddSharedComponentData`, `SetSharedComponentData` did not always update `SharedComponentOrderVersion`.
* Fixes serialization issue when reading in managed `IComponentData` containing array types and `UnityEngine.Object` references.
* No exception is thrown when re-adding a tag component with `EntityQuery`.
* `AddComponent<T>(NativeArray<Entity>)` now reliably throws an `ArgumentException` if any of the target entities are invalid.
* Fixed an issue where the Entity Debugger would not repaint in edit mode
* Marking a system as `[UpdateInGroup(typeof(LateSimulationSystemGroup))]` no longer emits a warning about `[DisableAutoCreation]`.
* Fixed rendering of chunk info to be compatible with HDRP
* Fixed issue where `ToComponentDataArray` ignored the filter settings on the `EntityQuery` for managed component types.

### Changes

* Deprecated `DynamicBuffer.Reserve` and made `DynamicBuffer.Capacity` a settable property. `DynamicBuffer.Reserve(10)` should now be `DynamicBuffer.Capacity = 10`.
* Moved `NativeString` code from Unity.Entities to Unity.Collections.
* Updated dependencies for this package.
* Significantly improved `Entity` instantiation performance when running in-Editor.
* Added support for managed `IComponentData` types such as `class MyComponent : IComponentData {}` which allows managed types such as GameObjects or List<>s to be stored in components. Users should use managed components sparingly in production code when possible as these components cannot be used by the Job System or archetype chunk storage and thus will be significantly slower to work with. Refer to the documentation for [component data](Documentation~/component_data.md) for more details on managed component use, implications and prevention.
* 'SubSceneStreamingSystem' has been renamed to `SceneSectionStreamingSystem` and is now internal
* Deprecated `_SceneEntities` in `SubScene.cs`. Please use `SceneSystem.LoadAsync` / `Unload` with the respective SceneGUID instead.
* Updated `com.unity.serialization` to `0.6.3-preview`.
* The deprecated `GetComponentGroup()` APIs are now `protected` and can only be called from inside a System like their `GetEntityQuery()` successors.
* All GameObjects with a ConvertToEntity set to "Convert and Destroy" will all be processed within the same conversion pass, this allows cross-referencing.
* Duplicate component adds are always ignored
* When adding component to single entity via EntityQuery, entity is moved to matching chunk instead of chunk achetype changing.
* "Used by Systems" list skips queries with filters
* Managed `IComponentData` no longer require all fields to be non-null after default construction.
* `ISharedComponentData` is serialized inline with entity and managed `IComponentData`. If a shared component references a `UnityEngine.Object` type, that type is serialized separately in an "objrefs" resource asset.
* `EntityManager` calls `EntityComponentStore` via burst delegates for `Add`/`Remove` components.
* `EntityComponentStore` cannot throw exceptions (since called as burst delegate from main thread.)
* `bool ICustomBootstrap.Initialize(string defaultWorldName)` has changed API with no deprecated fallback. It now simply gives you a chance to completely replace the default world initialization by returning true.
* `ICustomBootstrap` & `DefaultWorldInitialization` is now composable like this:
```
class MyCustomBootStrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        Debug.Log("Executing bootstrap");
        var world = new World("Custom world");
        World.DefaultGameObjectInjectionWorld = world;
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
        return true;
    }
}
```
* `ICustomBootstrap` can now be inherited and only the most deepest subclass bootstrap will be executed.
* `DefaultWorldInitialization.GetAllSystems` is not affected by bootstrap, it simply returns a list of systems based on the present dlls & attributes.
* `Time` is now available per-World, and is a property in a `ComponentSystem`.  It is updated from the `UnityEngine.Time` during the `InitializationSystemGroup` of each world.  If you need access to time in a sytem that runs in the `InitializationSystemGroup`, make sure you schedule your system after `UpdateWorldTimeSystem`.  `Time` is also a limited `TimeData` struct; if you need access to any of the extended fields available in `UnityEngine.Time`, access `UnityEngine.Time` explicitly`
* Systems are no longer removed from a `ComponentSystemGroup` if they throw an exception from their `OnUpdate`. This behavior was more confusing than helpful.
* Managed IComponentData no longer require implementing the `IEquatable<>` interface and overriding `GetHashCode()`. If either function is provided it will be preferred, otherwise the component will be inspected generically for equality.
* `EntityGuid` is now constructed from an originating ID, a namespace ID, and a serial, which can be safely extracted from their packed form using new getters. Use `a` and `b` fields when wanting to treat this as an opaque struct (the packing may change again in the future, as there are still unused bits remaining). The a/b constructor has been removed, to avoid any ambiguity.
* Updated `com.unity.platforms` to `0.1.6-preview`.
* The default Api Compatibility Level should now be `.NET Standard 2.0` and a warning is generated when the project uses `.NET 4.x`.
* Added `[UnityEngine.ExecuteAlways]` to `LateSimulationSystemGroup`, so its systems run in Edit Mode.


## [0.1.1] - 2019-08-06

### New Features
* EntityManager.SetSharedComponentData(EntityQuery query, T componentData) has been added which lets you efficiently swap a shared component data for a whole query. (Without moving any component data)

### Upgrade guide

* The deprecated `OnCreateManager` and `OnDestroyManager` are now compilation errors in the `NET_DOTS` profile as overrides can not be detected reliably (without reflection).
To avoid the confusion of "why is that not being called", especially when there is no warning issued, this will now be a compilation error. Use `OnCreate` and `OnDestroy` instead.

### Changes

* Updated default version of burst to `1.1.2`

### Fixes

* Fixed potential memory corruption when calling RemoveComponent on a batch of entities that didn't have the component.
* Fixed an issue where an assert about chunk layout compatibility could be triggered when adding a shared component via EntityManager.AddSharedComponentData<T>(EntityQuery entityQuery, T componentData).
* Fixed an issue where Entities without any Components would cause UI errors in the Chunk Info view
* Fixed EntityManager.AddComponent(NativeArray<Entity> entities, ComponentType componentType) so that it handles duplicate entities in the input NativeArray. Duplicate entities are discarded and the component is added only once. Prior to this fix, an assert would be triggered when checking for chunk layout compatibility.
* Fixed invalid update path for `ComponentType.Create`. Auto-update is available in Unity `2019.3` and was removed for previous versions where it would fail (the fallback implementation will work as before).


## [0.1.0] - 2019-07-30

### New Features

* Added the `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD` and `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_EDITOR_WORLD` defines which respectively can be used to disable runtime and editor default world generation.  Defining `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP` will still disable all default world generation.
* Allow structural changes to entities (add/remove components, add/destroy entities, etc.) while inside of `ForEach` lambda functions.  This negates the need for using `PostUpdateCommands` inside of ForEach.
* `EntityCommandBuffer` has some additional methods for adding components based on `ComponentType`, or for adding empty components of a certain type (`<T>`)
* EntityManagerDiffer & EntityManagerPatcher provides highly optimized diffing & patching functionality. It is used in the editor for providing scene conversion live link.
* Added support for `EntityManager.MoveEntitiesFrom` with managed arrays (Object Components).
* EntityManager.SetArchetype lets you change an entity to a specific archetype. Removing & adding the necessary components with default values. System state components are not allowed to be removed with this method, it throws an exception to avoid accidental system state removal. (Used in incremental live link conversion it made conversion from 100ms -> 40ms for 1000 changed game objects)
* Entity Debugger's system list now has a string filter field. This makes it easier to find a system by name when you have a lot of systems.
* Added IComponentData type `Asset` that will be used by Tiny to convert Editor assets to runtime assets
* Filled in some `<T>` holes in the overloads we provide in `EntityManager`
* New `Entities.WithIncludeAll()` that will include in matching all components that are normally ignored by default (currently `Prefab` and `Disabled`)
* EntityManager.CopyAndReplaceEntitiesFrom has been added it can be used to store & restore a backup of the world for the purposes of general purpose simulation rollback.

### Upgrade guide

* WorldDiff has been removed. It has been replaced by EntityManagerDiff & EntityManagerPatch.
* Renamed `EntityGroupManager` to `EntityQueryManager`.

### Changes

* EntityArchetype.GetComponentTypes no longer includes Entity in the list of components (it is implied). Behaviour now matches the EntityMangager.GetComponentTypes method. This matches the behavior of the corresponding `EntityManager` function.
* `EntityCommandBuffer.AddComponent(Entity, ComponentType)` no longer fails if the target entity already has the specified component.
*  DestroyEntity(EntityQuery entityQuery) now uses burst internally.

### Fixes

* Entity Inspector now shows DynamicBuffer elements in pages of five at a time
* Resources folder renamed to Styles so as not to add editor assets to built player
* `EntityQueryBuilder.ShallowEquals` (used from `Entities.ForEach`) no longer boxes and allocs GC
* Improved error message for unnecessary/invalid `UpdateBefore` and `UpdateAfter`
* Fixed leak in BlobBuilder.CreateBlobAssetReference
* ComponentSystems are now properly preserved when running the UnityLinker. Note this requires 19.3a10 to work correctly. If your project is not yet using 19.3 you can workaround the issue using the link.xml file. https://docs.unity3d.com/Manual//IL2CPP-BytecodeStripping.html
* Types that trigger an exception in the TypeManager won't prevent other types from initializing properly.

## [0.0.12-preview.33] - 2019-05-24

### New Features

* `[DisableAutoCreation]` can now apply to entire assemblies, which will cause all systems contained within to be excluded from automatic system creation. Useful for test assemblies.
* Added `ComponentSystemGroup.RemoveSystemFromUpdateList()`
* `EntityCommandBuffer` has commands for adding/removing components, deleting entities and adding shared components based on an EntityQuery and its filter. Not available in the `Concurrent` version

### Changes

* Generic component data types must now be registered in advance. Use [RegisterGenericComponentType] attribute to register each concrete use. e.g. `[assembly: RegisterGenericComponentType(typeof(TypeManagerTests.GenericComponent<int>))]`
* Attempting to call `Playback()` more than once on the same EntityCommandBuffer will now throw an error.
* Improved error checking for `[UpdateInGroup]`, `[UpdateBefore]`, and `[UpdateAfter]` attributes
* TypeManager no longer imposes alignment requirements on components containing pointers. Instead, it now throws an exception if you try to serialize a blittable component containing an unmanaged pointer, which suggests different alternatives.

### Fixes

* Fixed regression where accessing and destroying a blob asset in a burst job caused an exception
* Fixed bug where entities with manually specified `CompositeScale` were not updated by `TRSLocalToWorldSystem`.
* Error message when passing in invalid parameters to CreateSystem() is improved.
* Fixed bug where an exception due to aggressive pointer restrictions could leave the `TypeManager` in an invalid state
* SceneBoundingVolume is now generated seperately for each subsection
* SceneBoundingVolume no longer throws exceptions in conversion flow
* Fixed regression where calling AddComponent(NativeArray<Entity> entities, ComponentType componentType) could cause a crash.
* Fixed bug causing error message to appear in Inspector header when `ConvertToEntity` component was added to a disabled GameObject.

## [0.0.12-preview.32] - 2019-05-16

### New Features

* Added BlobBuilder which is a new API to build Blob Assets that does not require preallocating one contiguous block of memory. The BlobAllocator is now marked obsolete.
* Added versions of `IJobForEach` that support `DynamicBuffer`s
  * Due to C# language constraints, these overloads needed different names. The format for these overloads follows the following structure:
    * All job names begin with either `IJobForEach` or `IJobForEachEntity`
    * All jobs names are then followed by an underscore `_` and a combination of letter corresponding to the parameter types of the job
      * `B` - `IBufferElementData`
      * `C` - `IComponentData`
      * `E` - `Entity` (`IJobForEachWithEntity` only)
    * All suffixes for `WithEntity` jobs begin with `E`
    * All data types in a suffix are in alphabetical order
  * Here is the complete list of overloads:
    * `IJobForEach_C`, `IJobForEach_CC`, `IJobForEach_CCC`, `IJobForEach_CCCC`, `IJobForEach_CCCCC`, `IJobForEach_CCCCCC`
    * `IJobForEach_B`, `IJobForEach_BB`, `IJobForEach_BBB`, `IJobForEach_BBBB`, `IJobForEach_BBBBB`, `IJobForEach_BBBBBB`
    * `IJobForEach_BC`, `IJobForEach_BCC`, `IJobForEach_BCCC`, `IJobForEach_BCCCC`, `IJobForEach_BCCCCC`, `IJobForEach_BBC`, `IJobForEach_BBCC`, `IJobForEach_BBCCC`, `IJobForEach_BBCCCC`, `IJobForEach_BBBC`, `IJobForEach_BBBCC`, `IJobForEach_BBBCCC`, `IJobForEach_BBBCCC`, `IJobForEach_BBBBC`, `IJobForEach_BBBBCC`, `IJobForEach_BBBBBC`
    * `IJobForEachWithEntity_EB`, `IJobForEachWithEntity_EBB`, `IJobForEachWithEntity_EBBB`, `IJobForEachWithEntity_EBBBB`, `IJobForEachWithEntity_EBBBBB`, `IJobForEachWithEntity_EBBBBBB`
    * `IJobForEachWithEntity_EC`, `IJobForEachWithEntity_ECC`, `IJobForEachWithEntity_ECCC`, `IJobForEachWithEntity_ECCCC`, `IJobForEachWithEntity_ECCCCC`, `IJobForEachWithEntity_ECCCCCC`
    * `IJobForEachWithEntity_BC`, `IJobForEachWithEntity_BCC`, `IJobForEachWithEntity_BCCC`, `IJobForEachWithEntity_BCCCC`, `IJobForEachWithEntity_BCCCCC`, `IJobForEachWithEntity_BBC`, `IJobForEachWithEntity_BBCC`, `IJobForEachWithEntity_BBCCC`, `IJobForEachWithEntity_BBCCCC`, `IJobForEachWithEntity_BBBC`, `IJobForEachWithEntity_BBBCC`, `IJobForEachWithEntity_BBBCCC`, `IJobForEachWithEntity_BBBCCC`, `IJobForEachWithEntity_BBBBC`, `IJobForEachWithEntity_BBBBCC`, `IJobForEachWithEntity_BBBBBC`
    * Note that you can still use `IJobForEach` and `IJobForEachWithEntity` as before if you're using only `IComponentData`.
* EntityManager.SetEnabled API automatically enables & disables an entity or set of entities. If LinkedEntityGroup is present the whole group is enabled / disabled. Inactive game objects automatically get a LinkedEntityGroup added so that EntityManager.SetEnabled works as expected out of the box.
* Add `WithAnyReadOnly` and `WithAllReadyOnly` methods to EntityQueryBuilder to specify queries that filter on components with access type ReadOnly.
* No longer throw when the same type is in a WithAll and ForEach delegate param for ForEach queries.
* `DynamicBuffer` CopyFrom method now supports another DynamicBuffer as a parameter.
* Fixed cases that would not be handled correctly by the api updater.

### Upgrade guide

* Usages of BlobAllocator will need to be changed to use BlobBuilder instead. The API is similar but Allocate now returns the data that can be populated:

  ```csharp
  ref var root = ref builder.ConstructRoot<MyData>();
  var floatArray = builder.Allocate(3, ref root.floatArray);
  floatArray[0] = 0; // root.floatArray[0] can not be used and will throw on access
  ```

* ISharedComponentData with managed fields must implement IEquatable and GetHashCode
* IComponentData and ISharedComponentData implementing IEquatable must also override GetHashCode

### Fixes

* Comparisons of managed objects (e.g. in shared components) now work as expected
* Prefabs referencing other prefabs are now supported in game object entity conversion process
* Fixed a regression where ComponentDataProxy was not working correctly on Prefabs due to a ordering issue.
* Exposed GameObjectConversionDeclarePrefabsGroup for declaring prefab references. (Must happen before any conversion systems run)
* Inactive game objects are automatically converted to be Disabled entities
* Disabled components are ignored during conversion process. Behaviour.Enabled has no direct mapping in ECS. It is recommended to Disable whole entities instead
* Warnings are now issues when asking for a GetPrimaryEntity that is not a game object that is part of the converted group. HasPrimaryEntity can be used to check if the game object is part of the converted group in case that is necessary.
* Fixed a race condition in `EntityCommandBuffer.AddBuffer()` and `EntityCommandBuffer.SetBuffer()`

## [0.0.12-preview.31] - 2019-05-01

### New Features

### Upgrade guide

* Serialized entities file format version has changed, Sub Scenes entity caches will require rebuilding.

### Changes

* Adding components to entities that already have them is now properly ignored in the cases where no data would be overwritten. That means the inspectable state does not change and thus determinism can still be guaranteed.
* Restored backwards compatibility for `ForEach` API directly on `ComponentSystem` to ease people upgrading to the latest Unity.Entities package on top of Megacity.
* Rebuilding the entity cache files for sub scenes will now properly request checkout from source control if required.

### Fixes

* `IJobForEach` will only create new entity queries when scheduled, and won't rely on injection anymore. This avoids the creation of useless queries when explicit ones are used to schedule those jobs. Those useless queries could cause systems to keep updating even though the actual queries were empty.
* APIs changed in the previous version now have better obsolete stubs and upgrade paths.  All obsolete APIs requiring manual code changes will now soft warn and continue to work, instead of erroring at compile time.  These respective APIs will be removed in a future release after that date.
* LODGroup conversion now handles renderers being present in a LOD Group in multipe LOD levels correctly
* Fixed potential memory leak when disposing an EntityCommandBuffer after certain types of playback errors
* Fixed an issue where chunk utilization histograms weren't properly clipped in EntityDebugger
* Fixed an issue where tag components were incorrectly shown as subtractive in EntityDebugger
* ComponentSystem.ShouldRunSystem() exception message now more accurately reports the most likely reason for the error when the system does not exist.

### Known Issues

* It might happen that shared component data with managed references is not compared for equality correctly with certain profiles.


## [0.0.12-preview.30] - 2019-04-05

### New Features
Script templates have been added to help you create new component types and systems, similar to Unity's built-in template for new MonoBehaviours. Use them via the Assets/Create/ECS menu.

### Upgrade guide

Some APIs have been deprecated in this release:

[API Deprecation FAQ](https://forum.unity.com/threads/api-deprecation-faq-0-0-23.636994/)

** Removed obsolete ComponentSystem.ForEach
** Removed obsolete [Inject]
** Removed obsolete ComponentDataArray
** Removed obsolete SharedComponentDataArray
** Removed obsolete BufferArray
** Removed obsolete EntityArray
** Removed obsolete ComponentGroupArray

####ScriptBehaviourManager removal
* The ScriptBehaviourManager class has been removed.
* ComponentSystem and JobComponentSystem remain as system base classes (with a common ComponentSystemBase class)
  * ComponentSystems have overridable methods OnCreateManager and OnDestroyManager. These have been renamed to OnCreate and OnDestroy.
    * This is NOT handled by the obsolete API updater and will need to be done manually.
    * The old OnCreateManager/OnDestroyManager will continue to work temporarily, but will print a warning if a system contains them.
* World APIs have been updated as follows:
  * CreateManager, GetOrCreateManager, GetExistingManager, DestroyManager, BehaviourManagers have been renamed to CreateSystem, GetOrCreateSystem, GetExistingSystem, DestroySystem, Systems.
    * These should be handled by the obsolete API updater.
  * EntityManager is no longer accessed via GetExistingManager. There is now a property directly on World: World.EntityManager.
    * This is NOT handled by the obsolete API updater and will need to be done manually.
    * Searching and replacing Manager<EntityManager> should locate the right spots. For example, world.GetExistingManager<EntityManager>() should become just world.EntityManager.

#### IJobProcessComponentData renamed to IJobForeach
This rename unfortunately cannot be handled by the obsolete API updater.
A global search and replace of IJobProcessComponentData to IJobForEach should be sufficient.

#### ComponentGroup renamed to EntityQuery
ComponentGroup has been renamed to EntityQuery to better represent what it does.
All APIs that refer to ComponentGroup have been changed to refer to EntityQuery in their name, e.g. CreateEntityQuery, GetEntityQuery, etc.

#### EntityArchetypeQuery renamed to EntityQueryDesc
EntityArchetypeQuery has been renamed to EntityQueryDesc

### Changes
* Minimum required Unity version is now 2019.1.0b9
* Adding components to entities that already have them is now properly ignored in the cases where no data would be overwritten.
* UNITY_CSHARP_TINY is now NET_DOTS to match our other NET_* defines

### Fixes
* Fixed exception in inspector when Script is missing
* The presence of chunk components could lead to corruption of the entity remapping during deserialization of SubScene sections.
* Fix for an issue causing filtering with IJobForEachWithEntity to try to access entities outside of the range of the group it was scheduled with.
