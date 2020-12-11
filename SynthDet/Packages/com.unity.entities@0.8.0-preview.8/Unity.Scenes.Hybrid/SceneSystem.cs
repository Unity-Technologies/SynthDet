using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    /// <summary>
    /// High level API for loading & unloading scenes
    /// </summary>
    [ExecuteAlways]
    [UpdateInGroup(typeof(SceneSystemGroup))]
    public class SceneSystem : ComponentSystem
    {
        public const string k_SceneInfoFileName = "catalog.bin";

        static internal string GetSceneInfoPath()
        {
            return Path.Combine(Application.streamingAssetsPath, k_SceneInfoFileName);
        }

        /// <summary>
        /// Parameters for loading scenes.
        /// </summary>
        public struct LoadParameters
        {
            public bool AutoLoad
            {
                get { return (Flags & SceneLoadFlags.DisableAutoLoad) == 0; }
                set => Flags = value ? Flags & ~SceneLoadFlags.DisableAutoLoad : Flags | SceneLoadFlags.DisableAutoLoad;
            }
            public SceneLoadFlags Flags;
            public int Priority;
        }

        struct GameObjectSceneLoadRequest : IComponentData
        {
            public Hash128 sceneGUID;
            public Scene loadedScene;
            public Entity dependency;
            public LoadSceneParameters loadParameters;
            public int priority;
            public bool activateOnLoad;
        }

        protected EntityArchetype sceneLoadRequestArchetype;
        protected EntityQuery sceneLoadRequestQuery;
        BlobAssetReference<ResourceCatalogData> catalogData;

        static bool IsSceneEmbedded(string path) => SceneUtility.GetBuildIndexByScenePath(path) >= 0;

        protected override void OnCreate()
        {
            sceneLoadRequestArchetype = EntityManager.CreateArchetype(typeof(GameObjectSceneLoadRequest));
            sceneLoadRequestQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadWrite<GameObjectSceneLoadRequest>() } });
            
            var liveLinkEnabled = World.GetExistingSystem<LiveLinkRuntimeSystemGroup>()?.Enabled ?? false;

            var sceneInfoPath = GetSceneInfoPath();
            if (File.Exists(sceneInfoPath))
            {
                if (!BlobAssetReference<ResourceCatalogData>.TryRead(sceneInfoPath, ResourceCatalogData.CurrentFileFormatVersion, out catalogData))
                {
                    Debug.LogError($"Unable to read catalog data from {sceneInfoPath}.");
                    return;
                }

                //if running in LiveLink mode, the initial scenes list is sent from the editor.  otherwise use the flags in the scene data.
                if (!liveLinkEnabled)
                {
                    for (int i = 1; i < catalogData.Value.resources.Length; i++)
                    {
                        if (catalogData.Value.resources[i].ResourceType == ResourceMetaData.Type.Scene &&
                            (catalogData.Value.resources[i].ResourceFlags & ResourceMetaData.Flags.AutoLoad) == ResourceMetaData.Flags.AutoLoad)
                            LoadSceneAsync(catalogData.Value.resources[i].ResourceId, new LoadParameters() { Flags = SceneLoadFlags.LoadAsGOScene | SceneLoadFlags.LoadAdditive });
                    }
                }
            }
        }

        /// <summary>
        /// Get the guid for a scene path or name.  This is a slow method - it is best to use the guid directly.
        /// </summary>
        /// <param name="scenePath">The scene path or name.</param>
        /// <returns>True if the scene guid exists.</returns>
        public Hash128 GetSceneGUID(string scenePath)
        {
            return catalogData.Value.GetGUIDFromPath(scenePath);
        }

        /// <summary>
        /// Check if a scene or subscene is loaded.
        /// </summary>
        /// <param name="entity">The entity with the loading component data.  This is the entity returned by LoadSceneAsync.</param>
        /// <returns>True if the scene is loaded.</returns>
        public bool IsSceneLoaded(Entity entity)
        {
            if (EntityManager.HasComponent<GameObjectSceneLoadRequest>(entity))
            {
                var scene = EntityManager.GetComponentData<GameObjectSceneLoadRequest>(entity).loadedScene;
                return scene.IsValid() && scene.isLoaded;
            }
            else
            {
                return EntityManager.HasComponent<SceneReference>(entity) && !EntityManager.HasComponent<RequestSceneLoaded>(entity);
            }

        }

        public Hash128 BuildConfigurationGUID { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("BuildSettingsGUID has been renamed to BuildConfigurationGUID. (RemovedAfter 2020-04-15) (UnityUpgradable) -> BuildConfigurationGUID")]
        public Hash128 BuildSettingsGUID
        {
            get => BuildConfigurationGUID;
            set => BuildConfigurationGUID = value;
        }

        /// <summary>
        /// Load a scene by its asset GUID.
        /// </summary>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <param name="parameters">The load parameters for the scene.</param>
        /// <returns>An entity representing the loading state of the scene.</returns>
        public Entity LoadSceneAsync(Hash128 sceneGUID, LoadParameters parameters = default)
        {
            if (!sceneGUID.IsValid)
            {
                Debug.LogError($"LoadSceneAsync - Invalid sceneGUID.");
                return Entity.Null;
            }

            if ((parameters.Flags & SceneLoadFlags.LoadAsGOScene) == SceneLoadFlags.LoadAsGOScene)
            {
                var loadSceneMode = ((parameters.Flags & SceneLoadFlags.LoadAdditive) == SceneLoadFlags.LoadAdditive) ? LoadSceneMode.Additive : LoadSceneMode.Single;
                var activeOnLoad = !((parameters.Flags & SceneLoadFlags.DisableAutoLoad) == SceneLoadFlags.DisableAutoLoad);
                var newEntity = EntityManager.CreateEntity(sceneLoadRequestArchetype);
                EntityManager.SetComponentData(newEntity, new GameObjectSceneLoadRequest() { sceneGUID = sceneGUID, loadParameters = new LoadSceneParameters(loadSceneMode), activateOnLoad = activeOnLoad, priority = 50 });
                return newEntity;
            }

            var sceneEntity = Entity.Null;
            Entities.ForEach((Entity entity, ref SceneReference scene) =>
            {
                if (scene.SceneGUID == sceneGUID)
                    sceneEntity = entity;
            });

            var requestSceneLoaded = new RequestSceneLoaded { LoadFlags = parameters.Flags};
            
            if (sceneEntity != Entity.Null)
            {
                EntityManager.AddComponentData(sceneEntity, requestSceneLoaded);
                if (parameters.AutoLoad)
                {
                    if (EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
                    {
                        foreach(var s in EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity))
                            EntityManager.AddComponentData(s.SectionEntity, requestSceneLoaded);
                    }
                }
                return sceneEntity;
            }
            else
            {
                sceneEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(sceneEntity, new SceneReference {SceneGUID = sceneGUID});
                EntityManager.AddComponentData(sceneEntity, requestSceneLoaded);
                return sceneEntity;
            }
        }

        /// <summary>
        /// Flags controlling the unload process for SubScenes.
        /// </summary>
        [Flags]
        public enum UnloadParameters
        {
            Default = 0,
            DestroySectionProxyEntities = 1 << 1,
            DestroySceneProxyEntity = 1 << 2,
            DontRemoveRequestSceneLoaded = 1 << 3
        }

        /// <summary>
        /// Unload the scene.
        /// </summary>
        /// <param name="sceneEntity">The entity for the scene.</param>
        /// <param name="unloadParams">Parameters controlling the unload process.  These are ignored for GameObject scenes.</param>
        public void UnloadScene(Entity sceneEntity, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            if (EntityManager.HasComponent<GameObjectSceneLoadRequest>(sceneEntity))
            {
                var req = EntityManager.GetComponentData<GameObjectSceneLoadRequest>(sceneEntity);
                SceneManager.UnloadSceneAsync(req.loadedScene);
                EntityManager.DestroyEntity(req.dependency);
                req.dependency = default;
                EntityManager.DestroyEntity(sceneEntity);
                return;
            }

            var streamingSystem = World.GetExistingSystem<SceneSectionStreamingSystem>();

            bool removeRequest = (unloadParams & UnloadParameters.DontRemoveRequestSceneLoaded) == 0;
            bool destroySceneProxyEntity = (unloadParams & UnloadParameters.DestroySceneProxyEntity) != 0;
            bool destroySectionProxyEntities = (unloadParams & UnloadParameters.DestroySectionProxyEntities) != 0;

            if (destroySceneProxyEntity && !destroySectionProxyEntities)
                throw new ArgumentException("When unloading a scene it's not possible to destroy the scene entity without also destroying the section entities. Please also add the UnloadParameters.DestroySectionProxyEntities flag");

            if (EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
            {
                using (var sections = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).ToNativeArray(Allocator.Temp))
                {
                    foreach (var section in sections)
                    {
                        //@TODO: Should this really be in SubSceneStreamingSystem?
                        streamingSystem.UnloadSectionImmediate(section.SectionEntity);

                        if (destroySectionProxyEntities)
                            EntityManager.DestroyEntity(section.SectionEntity);
                        else if (removeRequest)
                            EntityManager.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                    }
                }
            }

            if (destroySceneProxyEntity)
            {
                EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
                EntityManager.DestroyEntity(sceneEntity);
            }
            else
            {
                if (destroySectionProxyEntities)
                {
                    EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
                    EntityManager.RemoveComponent<ResolvedSceneHash>(sceneEntity);
                }

                if (removeRequest)
                    EntityManager.RemoveComponent<RequestSceneLoaded>(sceneEntity);
            }
        }

        /// <summary>
        /// Unload a SubScene by GUID.  If this is called on a GameObject scene, it will only unload the first matching scene.
        /// </summary>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <param name="unloadParams">Parameters controlling the unload process.  These are ignored for GameObject scenes.</param>
        public void UnloadScene(Hash128 sceneGUID, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            var sceneEntity = GetSceneEntity(sceneGUID);
            if (sceneEntity != Entity.Null)
                UnloadScene(sceneEntity, unloadParams);
        }

        /// <summary>
        /// Find the scene given a guid.  For GameObject scenes, this will only return the first matching scene.
        /// </summary>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <returns>The entity for the scene.</returns>
        public Entity GetSceneEntity(Hash128 sceneGUID)
        {
            Entity sceneEntity = Entity.Null;
            Entities.ForEach((Entity entity, ref SceneReference scene) =>
            {
                if (scene.SceneGUID == sceneGUID)
                    sceneEntity = entity;
            });

            if (sceneEntity == Entity.Null)
            {
                Entities.ForEach((Entity entity, ref GameObjectSceneLoadRequest req) =>
                {
                    if (req.sceneGUID == sceneGUID)
                        sceneEntity = entity;
                });
            }

            return sceneEntity;
        }

        protected override void OnUpdate()
        {
            var streamingSystem = World.GetExistingSystem<SceneSectionStreamingSystem>();

            // Cleanup all Scenes that were destroyed explicitly 
            Entities.WithNone<SceneReference>().ForEach((Entity sceneEntity, DynamicBuffer<ResolvedSectionEntity> sections) =>
            {
                foreach (var section in sections.ToNativeArray(Allocator.Temp))
                {
                    streamingSystem.UnloadSectionImmediate(section.SectionEntity);
                    EntityManager.DestroyEntity(section.SectionEntity);
                }

                EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
            });

            var assetResolver = LiveLinkPlayerAssetRefreshSystem.GlobalAssetObjectResolver;
            Entities.With(sceneLoadRequestQuery).ForEach((Entity entity, ref GameObjectSceneLoadRequest req) =>
            {
                if (!req.loadedScene.IsValid())
                {
                    if (!EntityManager.Exists(req.dependency))
                    {
                        req.dependency = EntityManager.CreateEntity(typeof(ResourceGUID));
                        EntityManager.SetComponentData(req.dependency, new ResourceGUID() { Guid = req.sceneGUID });
                    }
                    if (assetResolver.HasAsset(req.sceneGUID))
                    {
                        var firstBundle = assetResolver.GetAssetBundle(req.sceneGUID);
                        var scenePath = firstBundle.GetAllScenePaths()[0];
                        LiveLinkMsg.LogInfo($"Loading GameObject Scene with path {scenePath}.");
                        var sceneLoadOperation = SceneManager.LoadSceneAsync(scenePath, req.loadParameters);
                        sceneLoadOperation.allowSceneActivation = req.activateOnLoad;
                        sceneLoadOperation.priority = req.priority;

                        req.loadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                    }
                }
            });
        }

        internal void ReloadScenesWithHash(Hash128 assetGUID, Hash128 newHash)
        {
            Entities.With(sceneLoadRequestQuery).ForEach((Entity entity, ref GameObjectSceneLoadRequest req) =>
            {
                if (req.sceneGUID == assetGUID)
                {
                    LiveLinkMsg.LogInfo($"Reloading GameObject Scene with path {req.loadedScene.path}.");
                    SceneManager.UnloadSceneAsync(req.loadedScene);
                    EntityManager.DestroyEntity(req.dependency);
                    req.dependency = default;
                }
            });
        }

        internal void UnloadAllScenes()
        {
            LiveLinkMsg.LogInfo($"Unloading all GameObject Scenes.");
            Entities.With(sceneLoadRequestQuery).ForEach((Entity entity, ref GameObjectSceneLoadRequest req) =>
            {                    
                SceneManager.UnloadSceneAsync(req.loadedScene);
                EntityManager.DestroyEntity(req.dependency);
            });
            EntityManager.DestroyEntity(sceneLoadRequestQuery);
        }
    }
}