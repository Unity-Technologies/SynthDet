using System.Collections.Generic;
using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SynthDet.Scenarios
{
    /// <summary>
    /// This scenario enables addressable asset bundles to be loaded from a remote host before starting the simulation.
    /// The prefabs loaded from these bundles are added to the <see cref="ForegroundObjectPlacementRandomizer"/>.
    /// </summary>
    [AddComponentMenu("SynthDet/SynthDet Scenario")]
    public class SynthDetScenario : FixedLengthScenario
    {
        int m_NumPrefabsToLoad;
        List<GameObject> m_Prefabs = new List<GameObject>();
        AssetLoadingStatus m_LoadingStatus = AssetLoadingStatus.LoadingCatalog;
        AsyncOperationHandle<IResourceLocator>[] m_CatalogHandles;
        
        /// <inheritdoc/>
        protected override bool isScenarioReadyToStart
        {
            get
            {
                switch (m_LoadingStatus)
                {
                    case AssetLoadingStatus.Complete:
                        return true;
                    case AssetLoadingStatus.LoadingCatalog:
                    {
                        foreach (var handle in m_CatalogHandles)
                        {
                            if (!handle.IsDone)
                                return false;
                        }
                        
                        foreach (var handle in m_CatalogHandles)
                        {
                            if (handle.Status != AsyncOperationStatus.Succeeded)
                            {
                                Debug.LogError("Catalog download failed");
                                m_LoadingStatus = AssetLoadingStatus.Failed;
                                return false;
                            }
                        }
                        
                        LoadPrefabs();
                        m_LoadingStatus = AssetLoadingStatus.LoadingPrefabs;
                        return false;
                    }
                    case AssetLoadingStatus.LoadingPrefabs:
                    {
                        lock (m_Prefabs)
                        {
                            if (m_Prefabs.Count < m_NumPrefabsToLoad)
                                return false;
                            var randomizer = GetRandomizer<ForegroundObjectPlacementRandomizer>();
                            m_Prefabs.Sort((prefab1, prefab2) => prefab1.name.CompareTo(prefab2.name));
                            randomizer.prefabs = m_Prefabs.ToArray();
                            m_LoadingStatus = AssetLoadingStatus.Complete;
                            return true;
                        }
                    }
                    default:
                        return false;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnAwake()
        {
            base.OnAwake();
            Addressables.InternalIdTransformFunc = location => m_BundleToUrlMap.ContainsKey(location.PrimaryKey)
                ? m_BundleToUrlMap[location.PrimaryKey] : location.InternalId;
            
            m_CatalogHandles = new AsyncOperationHandle<IResourceLocator>[m_CatalogUrls.Count];
            for(var i = 0 ; i < m_CatalogUrls.Count; i++)
                m_CatalogHandles[i] = Addressables.LoadContentCatalogAsync(m_CatalogUrls[i], false);
        }

        void LoadPrefabs()
        {
            foreach (var handle in m_CatalogHandles)
            {
                foreach (var key in handle.Result.Keys)
                {
                    if (!key.ToString().Contains(".prefab"))
                        return;
                    
                    m_NumPrefabsToLoad++;
                    Addressables.LoadAssetAsync<GameObject>(key).Completed += prefabHandle =>
                    {
                        if (prefabHandle.Status == AsyncOperationStatus.Failed)
                        {
                            Debug.LogError($"Failed to load prefab from key '{key}'");
                            m_LoadingStatus = AssetLoadingStatus.Failed;
                            return;
                        }
                        lock (m_Prefabs)
                        {
                            m_Prefabs.Add(prefabHandle.Result);
                        }
                    };
                }
            }
        }
        
        enum AssetLoadingStatus
        {
            Complete,
            LoadingCatalog,
            LoadingPrefabs,
            Failed
        }
    }
}