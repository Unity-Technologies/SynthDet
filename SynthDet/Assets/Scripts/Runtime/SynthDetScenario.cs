using System.Collections.Generic;
using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SynthDet.Scenarios
{
    [AddComponentMenu("SynthDet/SynthDet Scenario")]
    public class SynthDetScenario : FixedLengthScenario
    {
        int m_NumPrefabsToLoad;
        List<GameObject> m_Prefabs = new List<GameObject>();
        AssetLoadingStatus m_LoadingStatus = AssetLoadingStatus.LoadingCatalog;
        AsyncOperationHandle<IResourceLocator> m_CatalogHandle;
        Dictionary<string, string> m_BundleUrlMap = new Dictionary<string, string>
        {
            {"defaultlocalgroup_assets_all_4bedd9ba8c2c5396f3eba5a4c2ba13e1.bundle", "https://storage.googleapis.com/steven-addressables-tests/StandaloneWindows64/defaultlocalgroup_assets_all_4bedd9ba8c2c5396f3eba5a4c2ba13e1.bundle?Expires=1616400659&GoogleAccessId=bhram-test-usc1-taquito%40unity-ml-bhram-test.iam.gserviceaccount.com&Signature=eBRPIFoqBCr6dVE%2Ff%2BfAaz9moFebc0X3hk7TbPPkeoLL8i2f9m2MPgGYJcVSbupPtTpaQVo99lLkgGGXKCYGpXyTRKW9lmll5QfTeDdCT4vMHvlp8VOIZWL%2FypqjBt%2FAgy3zChnig13a9Z6X%2Bpc%2BqYY26%2B5y8H7Odftx6vGt8F1%2BvyLIkPdwApisvC%2FsMl9UjX1JjRA9wky%2FbU2jA%2BQqFq9Sr4S1yw9zm6E2G8B9KS8gvvtllL0UrC%2Bdj8PWWjvJMMlh4qBxwr2KyJf03QkhECeT8iR%2F6gOCak8UDxLDquT8Z61dr4L%2BOdBQT43w14mSl%2FKkpYo0kdgQyMG551keZA%3D%3D"}
        };
        
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
                        if (!m_CatalogHandle.IsDone)
                            return false;
                        
                        if (m_CatalogHandle.Status == AsyncOperationStatus.Succeeded)
                        {
                            LoadPrefabs();
                            m_LoadingStatus = AssetLoadingStatus.LoadingPrefabs;
                        }
                        else
                        {
                            Debug.LogError("Catalog download failed");
                            m_LoadingStatus = AssetLoadingStatus.Failed;
                        }
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
            Addressables.InternalIdTransformFunc = location =>
            {
                if (m_BundleUrlMap.ContainsKey(location.PrimaryKey))
                    return m_BundleUrlMap[location.PrimaryKey];
                return location.InternalId;
            };
            m_CatalogHandle = Addressables.LoadContentCatalogAsync(AddressablesConfiguration.signedCatalogUrl, false);
        }

        void LoadPrefabs()
        {
            foreach (var key in m_CatalogHandle.Result.Keys)
            {
                if (key.ToString().Contains(".prefab"))
                {
                    m_NumPrefabsToLoad++;
                    Addressables.LoadAssetAsync<GameObject>(key).Completed += handle =>
                    {
                        if (handle.Status == AsyncOperationStatus.Failed)
                        {
                            Debug.LogError($"Failed to load prefab from key '{key}'");
                            m_LoadingStatus = AssetLoadingStatus.Failed;
                            return;
                        }
                        lock (m_Prefabs)
                        {
                            m_Prefabs.Add(handle.Result);
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
