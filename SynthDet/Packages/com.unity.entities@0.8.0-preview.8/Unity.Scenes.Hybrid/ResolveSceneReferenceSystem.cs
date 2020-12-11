//#define LOG_RESOLVING

using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.IO.LowLevel.Unsafe;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    struct ResolvedSectionEntity : ISystemStateBufferElementData
    {
        public Entity SectionEntity;
    }

    struct ResolvedSceneHash : IComponentData
    {
        public Hash128 ArtifactHash;
    }
    struct ResolvedSectionPath : IComponentData
    {
        //@TODO: Switch back to NativeString512 once bugs are fixed
        public Words ScenePath;
        public Words HybridPath;
    }

    struct SceneMetaData
    {
        public BlobArray<SceneSectionData> Sections;
        public BlobString                  SceneName;
    }

    internal struct DisableSceneResolveAndLoad : IComponentData
    {
    }


    static class SceneMetaDataSerializeUtility
    {
        public static readonly int CurrentFileFormatVersion = 1;
    }

    /// <summary>
    /// Scenes are made out of sections, but to find out how many sections there are and extract their data like bounding volume or file size.
    /// The meta data for the scene has to be loaded first.
    /// ResolveSceneReferenceSystem creates section entities for each scene by loading the scenesection's metadata from disk.
    /// </summary>
    [ExecuteAlways]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [UpdateAfter(typeof(SceneSystem))]
    class ResolveSceneReferenceSystem : ComponentSystem
    {
        private NativeList<Hash128> m_ChangedScenes = new NativeList<Hash128>(Allocator.Persistent);
        private EntityQuery m_ScenesToRequest;
#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
        private EntityQuery m_ImportingScenes;
#endif
        private EntityQuery m_ResolvedScenes;

        public void NotifySceneContentsHasChanged(Hash128 scene)
        {
            m_ChangedScenes.Add(scene);
        }

        [Conditional("LOG_RESOLVING")]
        void LogResolving(string type, Hash128 sceneGUID)
        {
            Debug.Log(type + ": " + sceneGUID);
        }

        void UpdateSceneContentsChanged(Hash128 buildConfigurationGUID)
        {
#if UNITY_EDITOR
            Entities.With(m_ResolvedScenes).ForEach((Entity sceneEntity, ref SceneReference scene, ref ResolvedSceneHash resolvedScene) =>
            {
                LogResolving("Queuing UpdateSceneContentsChanged", scene.SceneGUID);
                var hash = EntityScenesPaths.GetSubSceneArtifactHash(scene.SceneGUID, buildConfigurationGUID, UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode.Queue);
                if ((hash != default) && (hash != resolvedScene.ArtifactHash))
                {
                    LogResolving("Scene hash changed", scene.SceneGUID);
                    NotifySceneContentsHasChanged(scene.SceneGUID);
                }
            });
#endif

            if (m_ChangedScenes.Length != 0)
            {
                var sceneSystem = World.GetExistingSystem<SceneSystem>();
                foreach (var scene in m_ChangedScenes)
                {
                    var sceneEntity = sceneSystem.GetSceneEntity(scene);

                    // Don't touch it if the scene is under live link control (@Todo: SubSceneStreamingSystem.IgnoreTag could be live link specific?)
                    if (sceneEntity != Entity.Null && !EntityManager.HasComponent<DisableSceneResolveAndLoad>(sceneEntity))
                    {
                        var unloadFlags = SceneSystem.UnloadParameters.DestroySectionProxyEntities | SceneSystem.UnloadParameters.DontRemoveRequestSceneLoaded;
                        sceneSystem.UnloadScene(sceneEntity, unloadFlags);
                    }
                    Assertions.Assert.IsTrue(EntityManager.GetEntityQueryMask(m_ScenesToRequest).Matches(sceneEntity));
                }
                m_ChangedScenes.Clear();
            }
        }

        void ResolveScene(Entity sceneEntity, ref SceneReference scene, RequestSceneLoaded requestSceneLoaded, Hash128 artifactHash)
        {
            // Resolve first (Even if the file doesn't exist we want to stop continously trying to load the section)
            EntityManager.AddBuffer<ResolvedSectionEntity>(sceneEntity);

#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
            EntityManager.AddComponentData(sceneEntity, new ResolvedSceneHash { ArtifactHash = artifactHash });

            UnityEditor.Experimental.AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out var paths);

            var sceneHeaderPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesHeader);
#else
            var sceneHeaderPath = EntityScenesPaths.GetLoadPath(scene.SceneGUID, EntityScenesPaths.PathType.EntitiesHeader, -1);
#endif
            
#if UNITY_ANDROID && !UNITY_EDITOR
            var uwrFile = new UnityWebRequest(sceneHeaderPath);
            uwrFile.downloadHandler = new DownloadHandlerBuffer();
            uwrFile.SendWebRequest();
            while(!uwrFile.isDone) {}

            if (uwrFile.isNetworkError || uwrFile.isHttpError)
            {
                Debug.LogError($"Loading Entity Scene failed because the entity header file could not be found: {scene.SceneGUID}\nNOTE: In order to load SubScenes in the player you have to use the new BuildConfiguration asset based workflow to build & run your player.\n{sceneHeaderPath}");
                return;
            }
            
            if (!BlobAssetReference<SceneMetaData>.TryRead(uwrFile.downloadHandler.data, SceneMetaDataSerializeUtility.CurrentFileFormatVersion, out var sceneMetaDataRef))
            {
                Debug.LogError("Loading Entity Scene failed because the entity header file was an old version: " + scene.SceneGUID);
                return;
            }
#else
            if (!File.Exists(sceneHeaderPath))
            {
                #if UNITY_EDITOR
                Debug.LogError($"Loading Entity Scene failed because the entity header file could not be found: {scene.SceneGUID}\n{sceneHeaderPath}");
                #else
                Debug.LogError($"Loading Entity Scene failed because the entity header file could not be found: {scene.SceneGUID}\nNOTE: In order to load SubScenes in the player you have to use the new BuildConfiguration asset based workflow to build & run your player.\n{sceneHeaderPath}");
                #endif
                return;
            }
            
            if (!BlobAssetReference<SceneMetaData>.TryRead(sceneHeaderPath, SceneMetaDataSerializeUtility.CurrentFileFormatVersion, out var sceneMetaDataRef))
            {
                Debug.LogError("Loading Entity Scene failed because the entity header file was an old version: " + scene.SceneGUID);
                return;
            }
#endif

            LogResolving("ResolveScene (success)", scene.SceneGUID);

            ref var sceneMetaData = ref sceneMetaDataRef.Value;

#if UNITY_EDITOR
            var sceneName = sceneMetaData.SceneName.ToString();
            EntityManager.SetName(sceneEntity, $"Scene: {sceneName}");
#endif

            var loadSections = !requestSceneLoaded.LoadFlags.HasFlag(SceneLoadFlags.DisableAutoLoad);

            for (int i = 0; i != sceneMetaData.Sections.Length; i++)
            {
                var sectionEntity = EntityManager.CreateEntity();
                var sectionIndex = sceneMetaData.Sections[i].SubSectionIndex;
#if UNITY_EDITOR
                EntityManager.SetName(sectionEntity, $"SceneSection: {sceneName} ({sectionIndex})");
#endif

                if (loadSections)
                {
                    EntityManager.AddComponentData(sectionEntity, requestSceneLoaded);
                }

                EntityManager.AddComponentData(sectionEntity, sceneMetaData.Sections[i]);
                EntityManager.AddComponentData(sectionEntity, new SceneBoundingVolume { Value = sceneMetaData.Sections[i].BoundingVolume });

                var sectionPath = new ResolvedSectionPath();
#if !UNITY_EDITOR || USE_SUBSCENE_EDITORBUNDLES
                var hybridPath = EntityScenesPaths.GetLoadPath(scene.SceneGUID, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, sectionIndex);
                var scenePath = EntityScenesPaths.GetLoadPath(scene.SceneGUID, EntityScenesPaths.PathType.EntitiesBinary, sectionIndex);
#else
                var scenePath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesBinary, sectionIndex);
                var hybridPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, sectionIndex);
#endif

                sectionPath.ScenePath.SetString(scenePath);
                if (hybridPath != null)
                    sectionPath.HybridPath.SetString(hybridPath);

                EntityManager.AddComponentData(sectionEntity, sectionPath);

#if UNITY_EDITOR
                if (EntityManager.HasComponent<SubScene>(sceneEntity))
                    EntityManager.AddComponentObject(sectionEntity, EntityManager.GetComponentObject<SubScene>(sceneEntity));
#endif

                var buffer = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
                buffer.Add(new ResolvedSectionEntity { SectionEntity = sectionEntity });
            }
            sceneMetaDataRef.Dispose();
            Assertions.Assert.IsTrue(EntityManager.GetEntityQueryMask(m_ResolvedScenes).Matches(sceneEntity));
        }

        //@TODO: What happens if we change source assets between queuing a request for the first time and it being resolved?

        protected override void OnUpdate()
        {
            //TODO: How can we disable systems in specific builds?
#if !UNITY_EDITOR
            var liveLinkEnabled = World.GetExistingSystem<LiveLinkRuntimeSystemGroup>()?.Enabled ?? false;
            Enabled = !liveLinkEnabled;
            if (!Enabled)
                return;
#else
            SceneWithBuildConfigurationGUIDs.ValidateBuildSettingsCache();
#endif
            var buildConfigurationGUID = World.GetExistingSystem<SceneSystem>().BuildConfigurationGUID;

            UpdateSceneContentsChanged(buildConfigurationGUID);

#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
            Entities.With(m_ImportingScenes).ForEach((Entity sceneEntity, ref SceneReference scene, ref RequestSceneLoaded requestSceneLoaded) =>
            {
                var hash = EntityScenesPaths.GetSubSceneArtifactHash(scene.SceneGUID, buildConfigurationGUID, UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode.Poll);
                if (hash.IsValid)
                {
                    LogResolving("Polling Importing (completed)", scene.SceneGUID);
                    ResolveScene(sceneEntity, ref scene, requestSceneLoaded, hash);
                }
                else
                {
                    LogResolving("Polling Importing (not complete)", scene.SceneGUID);
                }

            });
#endif


            //@TODO: Temporary workaround to prevent crash after build player
            if (m_ScenesToRequest.IsEmptyIgnoreFilter)
                return;

            // We are seeing this scene for the first time, so we need to schedule a request.
            Entities.With(m_ScenesToRequest).ForEach((Entity sceneEntity, ref SceneReference scene, ref RequestSceneLoaded requestSceneLoaded) =>
            {
#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
                 var blocking = (requestSceneLoaded.LoadFlags & SceneLoadFlags.BlockOnImport) != 0;
                 var importMode = blocking ? UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode.Block : UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode.Queue;

                 var hash = EntityScenesPaths.GetSubSceneArtifactHash(scene.SceneGUID, buildConfigurationGUID, importMode);
                 if (hash.IsValid)
                 {
                     LogResolving(blocking ? "Blocking import (completed)" : "Queue not yet requested (completed)", scene.SceneGUID);
                     ResolveScene(sceneEntity, ref scene, requestSceneLoaded, hash);
                 }
                 else
                     LogResolving(blocking ? "Blocking import (failed)" : "Queue not yet requested (not complete)", scene.SceneGUID);
#else
                ResolveScene(sceneEntity, ref scene, requestSceneLoaded, new Hash128());
#endif
            });
            EntityManager.AddComponent(m_ScenesToRequest, ComponentType.ReadWrite<ResolvedSectionEntity>());
        }

        protected override void OnCreate()
        {
            m_ScenesToRequest = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.Exclude<ResolvedSectionEntity>(),
                ComponentType.Exclude<DisableSceneResolveAndLoad>());

#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
            m_ImportingScenes = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.ReadWrite<ResolvedSectionEntity>(),
                ComponentType.Exclude<ResolvedSceneHash>(),
                ComponentType.Exclude<DisableSceneResolveAndLoad>());
#endif

            m_ResolvedScenes = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.ReadWrite<ResolvedSectionEntity>(),
#if UNITY_EDITOR && !USE_SUBSCENE_EDITORBUNDLES
                ComponentType.ReadWrite<ResolvedSceneHash>(),
#endif
                ComponentType.Exclude<DisableSceneResolveAndLoad>());
        }

        protected override void OnDestroy()
        {
            m_ChangedScenes.Dispose();
        }
    }

}
