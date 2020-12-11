using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.Experimental.AssetBundlePatching;
using UnityEngine.Networking.PlayerConnection;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    struct ResolvedAssetID : IEquatable<ResolvedAssetID>
    {
        public Hash128 GUID;
        public Hash128 TargetHash;

        public bool Equals(ResolvedAssetID other)
        {
            return GUID == other.GUID && TargetHash == other.TargetHash;
        }
    }
    
    struct ResolvedSubSceneID : IEquatable<ResolvedSubSceneID>
    {
        public SubSceneGUID SubSceneGUID;
        public Hash128 TargetHash;

        public bool Equals(ResolvedSubSceneID other)
        {
            return SubSceneGUID == other.SubSceneGUID && TargetHash == other.TargetHash;
        }
    }
    
    struct WaitingSubScene
    {
        public Hash128 TargetHash;
        public NativeArray<RuntimeGlobalObjectId> RuntimeGlobalObjectIds;
    }


#if UNITY_EDITOR
    [DisableAutoCreation]
#endif
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(LiveLinkRuntimeSystemGroup))]
    class LiveLinkPlayerAssetRefreshSystem : ComponentSystem
    {
        public static GlobalAssetObjectResolver globalAssetObjectResolver = new GlobalAssetObjectResolver();

        Dictionary<Hash128, Hash128>                 m_WaitingForAssets = new Dictionary<Hash128, Hash128>();
        Dictionary<SubSceneGUID, WaitingSubScene>    m_WaitingForSubScenes = new Dictionary<SubSceneGUID, WaitingSubScene>();
        public Dictionary<SubSceneGUID, Hash128>     TrackedSubScenes = new Dictionary<SubSceneGUID, Hash128>();

        EntityQuery                     m_ResourceRequests;
        EntityQuery                     m_SubSceneAssetRequests;

        // The resource has been requested from the editor but not necessarily been loaded yet.
        public struct ResourceRequested : IComponentData {}
        public struct SubSceneRequested : IComponentData {}

        public static GlobalAssetObjectResolver GlobalAssetObjectResolver => globalAssetObjectResolver;


        protected override void OnStartRunning()
        {
            PlayerConnection.instance.Register(LiveLinkMsg.ResponseAssetByGUID, ReceiveAsset);
            PlayerConnection.instance.Register(LiveLinkMsg.ResponseAssetTargetHash, ReceiveAssetTargetHash);
            
            PlayerConnection.instance.Register(LiveLinkMsg.ResponseSubSceneTargetHash, ReceiveSubSceneTargetHash);
            PlayerConnection.instance.Register(LiveLinkMsg.ResponseSubSceneByGUID, ReceiveSubScene);
            
            PlayerConnection.instance.Register(LiveLinkMsg.SendBuildArtifact, ReceiveBuildArtifact);

            m_ResourceRequests = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ResourceGUID>() },
                None = new[] { ComponentType.ReadOnly<ResourceRequested>(), ComponentType.ReadOnly<ResourceLoaded>() }
            });
            
            m_SubSceneAssetRequests = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SubSceneGUID>() },
                None = new[] { ComponentType.ReadOnly<SubSceneRequested>() }
            });
        }

        protected override void OnStopRunning()
        {
            PlayerConnection.instance.Unregister(LiveLinkMsg.ResponseAssetByGUID, ReceiveAsset);
            PlayerConnection.instance.Unregister(LiveLinkMsg.ResponseAssetTargetHash, ReceiveAssetTargetHash);
            
            PlayerConnection.instance.Unregister(LiveLinkMsg.ResponseSubSceneTargetHash, ReceiveSubSceneTargetHash);
            PlayerConnection.instance.Unregister(LiveLinkMsg.ResponseSubSceneByGUID, ReceiveSubScene);
            
            PlayerConnection.instance.Unregister(LiveLinkMsg.SendBuildArtifact, ReceiveBuildArtifact);
        }

        string GetCachePath(Hash128 targetHash)
        {
            return $"{Application.persistentDataPath}/{targetHash}";
        }

        string GetTempCachePath()
        {
            return $"{Application.persistentDataPath}/{Path.GetRandomFileName()}";
        }

        public Hash128 GetTrackedSubSceneTargetHash(SubSceneGUID subSceneGUID)
        {
            if (!TrackedSubScenes.TryGetValue(subSceneGUID, out var targetHash))
            {
                Debug.Log($"Failed to find scubScene in TrackedSubScenes: {subSceneGUID}");
                targetHash = new Hash128();
            }

            return targetHash;
        }

        unsafe void ReceiveBuildArtifact(MessageEventArgs args)
        {
            fixed (byte* ptr = args.data)
            {
                LiveLinkMsg.LogInfo($"ReceiveBuildArtifact => Buffer Size: {args.data.Length}");
                var reader = new UnsafeAppendBuffer.Reader(ptr, args.data.Length);
                reader.ReadNext(out string artifactFileName);
                string artifactPath = EntityScenesPaths.ComposeLiveLinkCachePath(artifactFileName);

                if (!File.Exists(artifactPath))
                {
                    LiveLinkMsg.LogInfo($"ReceiveBuildArtifact => {artifactPath}");

                    var tempCachePath = GetTempCachePath();
                    
                    try
                    {
                        var stream = File.OpenWrite(tempCachePath);
                        stream.Write(args.data, reader.Offset, args.data.Length - reader.Offset);
                        stream.Close();
                        stream.Dispose();
                    
                        File.Move(tempCachePath, artifactPath);
                        
                        LiveLinkMsg.LogInfo($"ReceiveBuildArtifact => Successfully written to disc.");
                    }
                    catch (Exception e)
                    {
                        if (File.Exists(tempCachePath))
                        {
                            File.Delete(tempCachePath);
                        }
                        
                        if (!File.Exists(artifactPath))
                        {
                            Debug.LogError($"Failed to move temporary file. Exception: {e.Message}");
                        }
                    }
                }
            }
        }

        unsafe void ReceiveSubScene(MessageEventArgs args)
        {
            fixed (byte* ptr = args.data)
            {
                var reader = new UnsafeAppendBuffer.Reader(ptr, args.data.Length);
                reader.ReadNext(out ResolvedSubSceneID subSceneId);
                reader.ReadNext(out NativeArray<RuntimeGlobalObjectId> runtimeGlobalObjectIds, Allocator.Persistent);
                
                LiveLinkMsg.LogInfo($"ReceiveSubScene => SubScene received {subSceneId} | Asset Dependencies {runtimeGlobalObjectIds.Length}");
                
                if (!IsSubSceneAvailable(subSceneId))
                {
                    Debug.LogError("SubScene is missing artifacts!");
                    return;
                }
                
                AddWaitForSubScene(subSceneId, runtimeGlobalObjectIds);
            }
        }

        //@TODO: Support some sort of transaction like API so we can reload all changed things in batch.
        unsafe void ReceiveAsset(MessageEventArgs args)
        {
            LiveLinkMsg.LogReceived($"AssetBundle: '{args.data.Length}' bytes");

            fixed (byte* ptr = args.data)
            {
                var reader = new UnsafeAppendBuffer.Reader(ptr, args.data.Length);
                var asset = reader.ReadNext<ResolvedAssetID>();
                var assetBundleCachePath = GetCachePath(asset.TargetHash);
                
                // Not printing error because this can happen when running the same player multiple times on the same machine
                if (File.Exists(assetBundleCachePath))
                {
                    LiveLinkMsg.LogInfo($"Received {asset.GUID} | {asset.TargetHash} but it already exists on disk");
                }
                else
                {
                    // cache: look up asset by target hash to see if the version we want is already on the target device
                    //if we already have the asset bundle revision we want, then just put that in the resolver as the active revision of the asset
                    // cache: if not in cache, write actual file to Application.persistentDatapath
                    var tempCachePath = GetTempCachePath();
                    LiveLinkMsg.LogInfo($"ReceiveAssetBundle => {asset.GUID} | {asset.TargetHash}, '{tempCachePath}' => '{assetBundleCachePath}'");
                    
                    var stream = File.OpenWrite(tempCachePath);
                    stream.Write(args.data, reader.Offset, args.data.Length - reader.Offset);
                    stream.Close();
                    stream.Dispose();

                    try
                    {
                        File.Move(tempCachePath, assetBundleCachePath);
                    }
                    catch (Exception e)
                    {
                        File.Delete(tempCachePath);
                        if (!File.Exists(assetBundleCachePath))
                        {
                            Debug.LogError($"Failed to move temporary file. Exception: {e.Message}");
                            LiveLinkMsg.LogInfo($"Failed to move temporary file. Exception: {e.Message}");
                        }
                    }
                }

                if (!m_WaitingForAssets.ContainsKey(asset.GUID))
                {
                    LiveLinkMsg.LogInfo($"Received {asset.GUID} | {asset.TargetHash} without requesting it");
                }

                m_WaitingForAssets[asset.GUID] = asset.TargetHash;
            }
        }

        void LoadAssetBundles(NativeArray<ResolvedAssetID> assets)
        {
            LiveLinkMsg.LogInfo("--- Begin Load asset bundles");

            var patchAssetBundles = new List<AssetBundle>();
            var patchAssetBundlesPath = new List<string>();
            var newAssetBundles = new List<Hash128>();
            var assetBundleToValidate = new List<Hash128>();


            foreach (var asset in assets)
            {
                var assetGUID = asset.GUID;
                var targetHash = asset.TargetHash;
                var assetBundleCachePath = GetCachePath(targetHash);

                //if we already loaded an asset bundle and we just need a refresh
                var oldAssetBundle = globalAssetObjectResolver.GetAssetBundle(assetGUID);
                if (oldAssetBundle != null)
                {
                    if (oldAssetBundle.isStreamedSceneAssetBundle)
                    {
                        LiveLinkMsg.LogInfo($"Unloading scene bundle: {assetGUID}");
                        var sceneSystem = World.GetExistingSystem<SceneSystem>();
                        if (sceneSystem != null)
                            sceneSystem.ReloadScenesWithHash(assetGUID, targetHash);
                        globalAssetObjectResolver.UnloadAsset(assetGUID);
                        continue;
                    }
                    else
                    {
                        LiveLinkMsg.LogInfo($"patching asset bundle: {assetGUID}");

                        patchAssetBundles.Add(oldAssetBundle);
                        patchAssetBundlesPath.Add(assetBundleCachePath);

                        globalAssetObjectResolver.UpdateTargetHash(assetGUID, targetHash);
                        newAssetBundles.Add(assetGUID);
                    }
                }
                else
                {
                    LiveLinkMsg.LogInfo($"Loaded asset bundle: {assetGUID}");

                    var loadedAssetBundle = AssetBundle.LoadFromFile(assetBundleCachePath);
                    globalAssetObjectResolver.AddAsset(assetGUID, targetHash, null, loadedAssetBundle);
                    newAssetBundles.Add(assetGUID);
                }

                assetBundleToValidate.Add(assetGUID);

                //@TODO: Keep a hashtable of guid -> entity?
                Entities.ForEach((Entity entity, ref ResourceGUID guid) =>
                {
                    if (guid.Guid == assetGUID)
                        EntityManager.AddComponentData(entity, new ResourceLoaded());
                });
            }


            AssetBundleUtility.PatchAssetBundles(patchAssetBundles.ToArray(), patchAssetBundlesPath.ToArray());

            foreach (var assetGUID in newAssetBundles)
            {
                var assetBundle = globalAssetObjectResolver.GetAssetBundle(assetGUID);
                if (assetBundle == null)
                {
                    Debug.LogError($"Could not load requested asset bundle.'");
                    return;
                }

                if (!assetBundle.isStreamedSceneAssetBundle)
                {
                    var loadedManifest = assetBundle.LoadAsset<AssetObjectManifest>(assetGUID.ToString());
                    if (loadedManifest == null)
                    {
                        Debug.LogError($"Loaded {assetGUID} failed to load ObjectManifest");
                        return;
                    }

                    globalAssetObjectResolver.UpdateObjectManifest(assetGUID, loadedManifest);
                }
            }

            foreach(var assetGUID in assetBundleToValidate)
                globalAssetObjectResolver.Validate(assetGUID);

            LiveLinkMsg.LogInfo("--- End Load asset bundles");
        }

        unsafe void ReceiveSubSceneTargetHash(MessageEventArgs args)
        {
            using (var subSceneAssets = args.ReceiveArray<ResolvedSubSceneID>())
            {
                foreach(var subSceneAsset in subSceneAssets)
                {
                    if (m_WaitingForSubScenes.ContainsKey(subSceneAsset.SubSceneGUID))
                        return;

                    // If subscene exists locally already, just load it
                    var assetDependencies = new HashSet<RuntimeGlobalObjectId>();
                    if (IsSubSceneAvailable(subSceneAsset, assetDependencies))
                    {
                        LiveLinkMsg.LogInfo($"ReceiveResponseSubSceneTargetHash => {subSceneAsset.SubSceneGUID} | {subSceneAsset.TargetHash}, File.Exists => 'True'");
                        
                        //TODO: This is a hack to make sure assets are managed by asset manifest when loading from cache for first run
                        AddWaitForSubScene(subSceneAsset, assetDependencies);
                    }
                    else
                    {
                        LiveLinkMsg.LogInfo($"ReceiveResponseSubSceneTargetHash => {subSceneAsset.SubSceneGUID} | {subSceneAsset.TargetHash}, File.Exists => 'False'");

                        PlayerConnection.instance.Send(LiveLinkMsg.RequestSubSceneByGUID, subSceneAsset);
                    }
                }
            }
        }

        unsafe void ReceiveAssetTargetHash(MessageEventArgs args)
        {
            using (var resolvedAssets = args.ReceiveArray<ResolvedAssetID>())
            {
                foreach(var asset in resolvedAssets)
                {
                    if (!asset.TargetHash.IsValid)
                    {
                        // If hash is invalid, then it means we should be waiting on it, but the hash will come later when it finishes importing on the editor
                        LiveLinkMsg.LogReceived($"ReceiveAssetTargetHash => {asset.GUID} | {asset.TargetHash}, Invalid Hash (Still waiting)");
                        m_WaitingForAssets[asset.GUID] = new Hash128();
                    }
                    else
                    {
                        //TODO: Should we compare against already loaded assets here?
                        if (File.Exists(GetCachePath(asset.TargetHash)))
                        {
                            LiveLinkMsg.LogReceived($"ReceiveAssetTargetHash => {asset.GUID} | {asset.TargetHash}, File.Exists => 'True'");
                            m_WaitingForAssets[asset.GUID] = asset.TargetHash;
                        }
                        else
                        {
                            LiveLinkMsg.LogReceived($"ReceiveAssetTargetHash => {asset.GUID} | {asset.TargetHash}, File.Exists => 'False'");
                            m_WaitingForAssets[asset.GUID] = new Hash128();

                            LiveLinkMsg.LogSend($"AssetBundleBuild request '{asset.GUID}'");
                            PlayerConnection.instance.Send(LiveLinkMsg.RequestAssetByGUID, asset.GUID);
                        }
                    }
                }
            }
        }

        public bool IsSubSceneReady(SubSceneGUID subSceneGUID)
        {
            if (!TrackedSubScenes.TryGetValue(subSceneGUID, out var targetHash))
            {
                return false;
            }
            
            return (targetHash != new Hash128());
        }

        unsafe bool IsSubSceneAvailable(in ResolvedSubSceneID subSceneId, HashSet<RuntimeGlobalObjectId> assetDependencies = null)
        {
            var headerPath = EntityScenesPaths.GetLiveLinkCachePath(subSceneId.TargetHash, EntityScenesPaths.PathType.EntitiesHeader, -1);
            if (!File.Exists(headerPath))
            {
                LiveLinkMsg.LogInfo($"Missing SubScene header! {headerPath}");
                return false;
            }
        
            if (!BlobAssetReference<SceneMetaData>.TryRead(headerPath, SceneMetaDataSerializeUtility.CurrentFileFormatVersion, out var sceneMetaDataRef))
            {
                Debug.LogError("Loading Entity Scene failed because the entity header file was an old version: " + subSceneId.SubSceneGUID);
                return false;
            }

            ref SceneMetaData sceneMetaData = ref sceneMetaDataRef.Value;
            for (int i = 0; i < sceneMetaData.Sections.Length; i++)
            {
                var sectionIndex = sceneMetaData.Sections[i].SubSectionIndex;
                var ebfPath = EntityScenesPaths.GetLiveLinkCachePath(subSceneId.TargetHash, EntityScenesPaths.PathType.EntitiesBinary, sectionIndex);
                if (!File.Exists(headerPath))
                {
                    LiveLinkMsg.LogInfo($"Missing Entity binary file! {ebfPath}");
                    return false;
                }
                
                if (sceneMetaData.Sections[i].ObjectReferenceCount != 0)
                {
                    var refObjGuidsPath = EntityScenesPaths.GetLiveLinkCachePath(subSceneId.TargetHash, EntityScenesPaths.PathType.EntitiesUnityObjectRefGuids, sectionIndex);
                    if (!File.Exists(refObjGuidsPath))
                    {
                        LiveLinkMsg.LogInfo($"Missing Entity refObjGuids file! {refObjGuidsPath}");
                        return false;
                    }
                    
                    var assetBundlePath = EntityScenesPaths.GetLiveLinkCachePath(subSceneId.TargetHash, EntityScenesPaths.PathType.EntitiesUnitObjectReferencesBundle, sectionIndex);
                    if (!File.Exists(assetBundlePath))
                    {
                        LiveLinkMsg.LogInfo($"Missing Entity AssetBundle file! {assetBundlePath}");
                        return false;
                    }

                    if (assetDependencies != null)
                    {
                        using(var data = new NativeArray<byte>(File.ReadAllBytes(refObjGuidsPath), Allocator.Temp))
                        using (var reader = new MemoryBinaryReader((byte*)data.GetUnsafePtr()))
                        {
                            var numObjRefGUIDs = reader.ReadInt();
                            var objRefGUIDs = new NativeArray<RuntimeGlobalObjectId>(numObjRefGUIDs, Allocator.Temp);
                            reader.ReadArray(objRefGUIDs, numObjRefGUIDs);

                            foreach (var runtimeGlobalObjectId in objRefGUIDs)
                            {
                                assetDependencies.Add(runtimeGlobalObjectId);
                            }
                        }
                    }
                }
            }
            
            return true;
        }

        void AddWaitForSubScene(in ResolvedSubSceneID subSceneId, HashSet<RuntimeGlobalObjectId> assetDependencies)
        {
            var runtimeGlobalObjectIds = new NativeArray<RuntimeGlobalObjectId>(assetDependencies.Count, Allocator.Persistent);
            int j = 0;
            foreach (var asset in assetDependencies)
                runtimeGlobalObjectIds[j++] = asset;
            
            AddWaitForSubScene(subSceneId, runtimeGlobalObjectIds);
        }

        void AddWaitForSubScene(in ResolvedSubSceneID subSceneId, NativeArray<RuntimeGlobalObjectId> assetDependencies)
        {
            if (m_WaitingForSubScenes.ContainsKey(subSceneId.SubSceneGUID))
            {
                Debug.LogError("Adding SubScene to waiting that we are already waiting for!");
                return;
            }

            var waitingSubScene = new WaitingSubScene {TargetHash = subSceneId.TargetHash, RuntimeGlobalObjectIds = assetDependencies};

            m_WaitingForSubScenes[subSceneId.SubSceneGUID] = waitingSubScene;
            LiveLinkMsg.LogInfo($"AddWaitForSubScene => SubScene added to waiting list. {subSceneId.TargetHash}");
        }

        
        protected override void OnUpdate()
        {
            // Request any new guids that we haven't seen yet from the editor
            using (var requestedGuids = m_ResourceRequests.ToComponentDataArray<ResourceGUID>(Allocator.TempJob))
            {
                if (requestedGuids.Length > 0)
                {
                    EntityManager.AddComponent(m_ResourceRequests, typeof(ResourceRequested));
                    LiveLinkMsg.LogSend($"AssetBundleTargetHash request {requestedGuids.Reinterpret<Hash128>().ToDebugString()}");
                    PlayerConnection.instance.SendArray(LiveLinkMsg.RequestAssetTargetHash, requestedGuids);
                }
            }
            
            // Request any new subscenes that we haven't seen yet from the editor
            using (var requestedSubScenes = m_SubSceneAssetRequests.ToComponentDataArray<SubSceneGUID>(Allocator.TempJob))
            {
                if (requestedSubScenes.Length > 0)
                {
                    EntityManager.AddComponent(m_SubSceneAssetRequests, typeof(SubSceneRequested));
                    PlayerConnection.instance.SendArray(LiveLinkMsg.RequestSubSceneTargetHash, requestedSubScenes);
                }
            }

            // * Ensure all assets we are waiting for have arrived.
            // * LoadAll asset bundles in one go when everything is ready
            if (m_WaitingForAssets.Count != 0)
            {
                bool hasAllAssets = true;
                var assets = new NativeArray<ResolvedAssetID>(m_WaitingForAssets.Count, Allocator.TempJob);
                int o = 0;
                foreach (var asset in m_WaitingForAssets)
                {
                    if (asset.Value == new Hash128())
                        hasAllAssets = false;
                    assets[o++] = new ResolvedAssetID { GUID = asset.Key, TargetHash = asset.Value };
                }

                if (hasAllAssets)
                {
                    LoadAssetBundles(assets);
                    m_WaitingForAssets.Clear();
                }

                assets.Dispose();
            }

            if (m_WaitingForSubScenes.Count != 0)
            {
                bool hasAllSubScenes = true;
                foreach (var subScene in m_WaitingForSubScenes)
                {
                    bool hasSubScene = subScene.Value.TargetHash.IsValid;
                    
                    if(!World.GetExistingSystem<LiveLinkPlayerSystem>().IsResourceReady(subScene.Value.RuntimeGlobalObjectIds))
                    {
                        hasSubScene = false;
                    }

                    if (!hasSubScene)
                    {
                        hasAllSubScenes = false;
                        break;
                    }

                    TrackedSubScenes[subScene.Key] = subScene.Value.TargetHash;
                }

                if (hasAllSubScenes)
                {
                    foreach (var subScene in m_WaitingForSubScenes)
                        subScene.Value.RuntimeGlobalObjectIds.Dispose();

                    m_WaitingForSubScenes.Clear();
                }
            }
        }

        public static void Reset()
        {
            globalAssetObjectResolver.DisposeAssetBundles();
            globalAssetObjectResolver = new GlobalAssetObjectResolver();

            foreach (var world in World.All)
            {
                var system = world.GetExistingSystem<LiveLinkPlayerAssetRefreshSystem>();
                if (system != null)
                {
                    system.m_WaitingForAssets.Clear();
                    system.m_WaitingForSubScenes.Clear();
                    system.TrackedSubScenes.Clear();
                }
            }
        }
    }
}
