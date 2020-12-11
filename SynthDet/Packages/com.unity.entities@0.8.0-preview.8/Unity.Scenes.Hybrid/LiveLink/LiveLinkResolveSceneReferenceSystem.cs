//#define LOG_RESOLVING

using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    internal struct WaitingForEditor : IComponentData
    {
    }

    internal struct EditorTriggeredLoad : IComponentData
    {
    }

#if UNITY_EDITOR
    [DisableAutoCreation]
#endif
    [ExecuteAlways]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [UpdateAfter(typeof(SceneSystem))]
    class LiveLinkResolveSceneReferenceSystem : ComponentSystem
    {
        private EntityQuery m_NotYetRequestedScenes;
        private EntityQuery m_WaitingForEditorScenes;
        private EntityQuery m_ResolvedScenes;

        [Conditional("LOG_RESOLVING")]
        void LogResolving(string type, Hash128 sceneGUID)
        {
            Debug.Log(type + ": " + sceneGUID);
        }

        void ResolveScene(Entity sceneEntity, ref SceneReference scene, RequestSceneLoaded requestSceneLoaded, Hash128 artifactHash)
        {
            // Resolve first (Even if the file doesn't exist we want to stop continously trying to load the section)
            EntityManager.AddBuffer<ResolvedSectionEntity>(sceneEntity);
            EntityManager.AddComponentData(sceneEntity, new ResolvedSceneHash { ArtifactHash = artifactHash });

            var sceneHeaderPath = EntityScenesPaths.GetLiveLinkCachePath(artifactHash, EntityScenesPaths.PathType.EntitiesHeader, -1);

            if (!File.Exists(sceneHeaderPath))
            {
                Debug.LogError($"Loading Entity Scene failed because the entity header file could not be found: {scene.SceneGUID}\n{sceneHeaderPath}");
                return;
            }
            
            if (!BlobAssetReference<SceneMetaData>.TryRead(sceneHeaderPath, SceneMetaDataSerializeUtility.CurrentFileFormatVersion, out var sceneMetaDataRef))
            {
                Debug.LogError("Loading Entity Scene failed because the entity header file was an old version: " + scene.SceneGUID);
                return;
            }

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
                var hybridPath = EntityScenesPaths.GetLiveLinkCachePath(artifactHash, EntityScenesPaths.PathType.EntitiesUnitObjectReferencesBundle, sectionIndex);
                var scenePath = EntityScenesPaths.GetLiveLinkCachePath(artifactHash, EntityScenesPaths.PathType.EntitiesBinary, sectionIndex);

                sectionPath.ScenePath.SetString(scenePath);
                if (hybridPath != null)
                    sectionPath.HybridPath.SetString(hybridPath);
                
                EntityManager.AddComponentData(sectionEntity, sectionPath);

                var buffer = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
                buffer.Add(new ResolvedSectionEntity { SectionEntity = sectionEntity });
            }
            sceneMetaDataRef.Dispose();            
        }

        protected override void OnUpdate()
        {
            var liveLinkEnabled = World.GetExistingSystem<LiveLinkRuntimeSystemGroup>()?.Enabled ?? false;
            Enabled = liveLinkEnabled;
            if (!Enabled)
                return;

            var buildConfigurationGUID = World.GetExistingSystem<SceneSystem>().BuildConfigurationGUID;
            var liveLinkPlayerAssetRefreshSystem = World.GetExistingSystem<LiveLinkPlayerAssetRefreshSystem>();
            var sceneSystem = World.GetExistingSystem<SceneSystem>();

            Entities.With(m_ResolvedScenes).ForEach((Entity sceneEntity, ref SceneReference scene, ref ResolvedSceneHash resolvedScene) =>
            {
                var subSceneGUID = new SubSceneGUID(scene.SceneGUID, buildConfigurationGUID);
                if (liveLinkPlayerAssetRefreshSystem.TrackedSubScenes[subSceneGUID] != resolvedScene.ArtifactHash)
                {
                    if (sceneEntity != Entity.Null && !EntityManager.HasComponent<DisableSceneResolveAndLoad>(sceneEntity))
                    {
                        var unloadFlags = SceneSystem.UnloadParameters.DestroySectionProxyEntities | SceneSystem.UnloadParameters.DontRemoveRequestSceneLoaded;
                        sceneSystem.UnloadScene(sceneEntity, unloadFlags);
                    }
                }
            });
            
            Entities.With(m_WaitingForEditorScenes).ForEach((Entity sceneEntity, ref SceneReference scene, ref RequestSceneLoaded requestSceneLoaded) =>
            {
                var subSceneGUID = new SubSceneGUID(scene.SceneGUID, buildConfigurationGUID);
                // Check if Scene is ready?
                if (liveLinkPlayerAssetRefreshSystem.IsSubSceneReady(subSceneGUID))
                {
                    var trackedTargetHash = liveLinkPlayerAssetRefreshSystem.GetTrackedSubSceneTargetHash(subSceneGUID);
                    EntityManager.RemoveComponent<WaitingForEditor>(sceneEntity);
                    ResolveScene(sceneEntity, ref scene, requestSceneLoaded, trackedTargetHash);
                }
            });

            // We are seeing this scene for the first time, so we need to schedule a request.
            Entities.With(m_NotYetRequestedScenes).ForEach((Entity sceneEntity, ref SceneReference scene, ref RequestSceneLoaded requestSceneLoaded) =>
            {
                var subSceneGUID = new SubSceneGUID(scene.SceneGUID, buildConfigurationGUID);

                liveLinkPlayerAssetRefreshSystem.TrackedSubScenes[subSceneGUID] = new Hash128();
                var archetype = EntityManager.CreateArchetype(typeof(SubSceneGUID));
                var entity = EntityManager.CreateEntity(archetype);
                EntityManager.SetComponentData(entity, new SubSceneGUID
                {
                    Guid = scene.SceneGUID,
                    BuildConfigurationGuid = buildConfigurationGUID
                });
                EntityManager.AddComponentData(sceneEntity, new WaitingForEditor());
            });
        }

        protected override void OnCreate()
        {
            m_NotYetRequestedScenes = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadOnly<EditorTriggeredLoad>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.Exclude<ResolvedSectionEntity>(),
                ComponentType.Exclude<WaitingForEditor>(),
                ComponentType.Exclude<DisableSceneResolveAndLoad>());
            
            m_WaitingForEditorScenes = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadOnly<EditorTriggeredLoad>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.ReadWrite<WaitingForEditor>(),
                ComponentType.Exclude<ResolvedSectionEntity>(),
                ComponentType.Exclude<DisableSceneResolveAndLoad>());

            m_ResolvedScenes = GetEntityQuery(ComponentType.ReadWrite<SceneReference>(),
                ComponentType.ReadOnly<EditorTriggeredLoad>(),
                ComponentType.ReadWrite<RequestSceneLoaded>(),
                ComponentType.ReadWrite<ResolvedSectionEntity>(),
                ComponentType.ReadWrite<ResolvedSceneHash>(),
                ComponentType.Exclude<DisableSceneResolveAndLoad>());
        }
    }

}
