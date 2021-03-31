using System.Collections.Generic;
using System.Linq;
using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

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

        List<string> m_LabelStringsForAutoLabelConfig = new List<string>();

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
                        
                        LoadAndLabelPrefabs();
                        m_LoadingStatus = AssetLoadingStatus.LoadingPrefabs;
                        return false;
                    }
                    case AssetLoadingStatus.LoadingPrefabs:
                    {
                        lock (m_Prefabs)
                        {
                            if (m_Prefabs.Count < m_NumPrefabsToLoad)
                                return false;
                            SetupLabelConfigs();
                            var randomizer = GetRandomizer<ForegroundObjectPlacementRandomizer>();
                            m_Prefabs.Sort((prefab1, prefab2) => prefab1.name.CompareTo(prefab2.name));
                            randomizer.prefabs = m_Prefabs.ToArray();
                            m_LoadingStatus = AssetLoadingStatus.Complete;
                            return true;
                        }
                    }
                    case AssetLoadingStatus.Failed:
                    {
                        Quit();
                        return false;
                    }
                    default:
                        return false;
                }
            }
        }

        protected override void OnAwake()
        {
            base.OnAwake();

            Caching.ClearCache();

            string InternalIdTransformFunc(IResourceLocation location)
            {
                var internalId = m_BundleToUrlMap.ContainsKey(location.PrimaryKey)
                    ? m_BundleToUrlMap[location.PrimaryKey]
                    : location.InternalId;
                
                Debug.Log($"InternalIdTransformFunc. Old: {location.InternalId} New: {internalId}");
                return internalId;
            }

            Addressables.InternalIdTransformFunc = InternalIdTransformFunc;

            // Addressables.InternalIdTransformFunc = TransformFunc;
            
            m_CatalogHandles = new AsyncOperationHandle<IResourceLocator>[m_CatalogUrls.Count];
            for(var i = 0 ; i < m_CatalogUrls.Count; i++)
                m_CatalogHandles[i] = Addressables.LoadContentCatalogAsync(m_CatalogUrls[i], false);
        }
        
        string TransformFunc(IResourceLocation location)
        {
            Debug.Log(location.InternalId);
            if (location.InternalId.Contains("catalog") && location.InternalId.Contains(".json"))
                return location.InternalId;
            if (location.InternalId.Contains("__placeholder__") && location.InternalId.Contains("_assets_all_")) //"doorgroup6_assets_all_ef13245136c2b5bec4ea18ac92071dca.bundle"))
                return location.InternalId.Replace("__placeholder__/foreground_group_assets_all_0c7829f95130438cde35283652f1fd3f.bundle", "https://storage.googleapis.com/addressables-synthdet/e2e/newe2e/foreground_group_assets_all_0c7829f95130438cde35283652f1fd3f.bundle");
            if (location.InternalId.Contains("__placeholder__") && location.InternalId.Contains("_unitybuiltinshaders_"))
                //"doorgroup6_unitybuiltinshaders_fb3cc65dc055f6c5ef84d30de128788a.bundle"))
                return location.InternalId.Replace("__placeholder__/foreground_group_unitybuiltinshaders_51bd82438b9ab720fa52c2dbd163e111.bundle","https://storage.googleapis.com/addressables-synthdet/e2e/newe2e/foreground_group_unitybuiltinshaders_51bd82438b9ab720fa52c2dbd163e111.bundle");
            return location.InternalId;
        }

        void LoadAndLabelPrefabs()
        {
            foreach (var handle in m_CatalogHandles)
            {
                foreach (var key in handle.Result.Keys)
                {
                    if (!key.ToString().Contains(".prefab"))
                        continue;
                    
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
                            ConfigureLabeling(prefabHandle.Result);
                            prefabHandle.Result.layer = LayerMask.NameToLayer("Foreground");
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

        void ConfigureLabeling(GameObject gObj)
        {
            var labeling = Utilities.GetOrAddComponent<Labeling>(gObj);
            labeling.labels.Clear();
            labeling.labels.Add(gObj.name);
            if(!m_LabelStringsForAutoLabelConfig.Contains(labeling.labels[0]))
                m_LabelStringsForAutoLabelConfig.Add(labeling.labels[0]);
        }
        
        void SetupLabelConfigs()
        {
            var perceptionCamera = FindObjectOfType<PerceptionCamera>();

            var idLabelConfig = ScriptableObject.CreateInstance<IdLabelConfig>();

            idLabelConfig.autoAssignIds = true;
            idLabelConfig.startingLabelId = StartingLabelId.One;

            var idLabelEntries = m_LabelStringsForAutoLabelConfig.Select((t, i) => new IdLabelEntry { id = i, label = t }).ToList();
            idLabelConfig.Init(idLabelEntries);

            var semanticLabelConfig = ScriptableObject.CreateInstance<SemanticSegmentationLabelConfig>();
            var semanticLabelEntries = new List<SemanticSegmentationLabelEntry>();
            for (var i = 0; i < m_LabelStringsForAutoLabelConfig.Count; i++)
            {
                semanticLabelEntries.Add(new SemanticSegmentationLabelEntry()
                {
                    label = m_LabelStringsForAutoLabelConfig[i],
                    color = GetUniqueSemanticSegmentationColor()
                });
            }
            semanticLabelConfig.Init(semanticLabelEntries);

            foreach (var labeler in perceptionCamera.labelers)
            {
                if (!labeler.enabled)
                    continue;

                switch (labeler)
                {
                    case BoundingBox2DLabeler boundingBox2DLabeler:
                        boundingBox2DLabeler.idLabelConfig = idLabelConfig;
                        break;
                    case BoundingBox3DLabeler boundingBox3DLabeler:
                        boundingBox3DLabeler.idLabelConfig = idLabelConfig;
                        break;
                    case ObjectCountLabeler objectCountLabeler:
                        objectCountLabeler.labelConfig.autoAssignIds = idLabelConfig.autoAssignIds;
                        objectCountLabeler.labelConfig.startingLabelId = idLabelConfig.startingLabelId;
                        objectCountLabeler.labelConfig.Init(idLabelEntries);
                        break;
                    case RenderedObjectInfoLabeler renderedObjectInfoLabeler:
                        renderedObjectInfoLabeler.idLabelConfig = idLabelConfig;
                        break;
                    case KeypointLabeler keypointLabeler:
                        keypointLabeler.idLabelConfig = idLabelConfig;
                        break;
                    case InstanceSegmentationLabeler instanceSegmentationLabeler:
                        instanceSegmentationLabeler.idLabelConfig = idLabelConfig;
                        break;
                    case SemanticSegmentationLabeler semanticSegmentationLabeler:
                        semanticSegmentationLabeler.labelConfig = semanticLabelConfig;
                        break;
                }

                labeler.Init(perceptionCamera);
            }
        }

        static HashSet<Color> s_ColorsAlreadyUsed = new HashSet<Color>();
        static ColorRgbParameter s_SemanticColorParameter = new ColorRgbParameter();
        static Color GetUniqueSemanticSegmentationColor()
        {
            var sampledColor = s_SemanticColorParameter.Sample();
            var maxTries = 1000;
            var count = 0;

            while (s_ColorsAlreadyUsed.Contains(sampledColor) && count <= maxTries)
            {
                count++;
                sampledColor = s_SemanticColorParameter.Sample();
                Debug.LogError("Failed to find unique semantic segmentation color for a label.");
            }

            s_ColorsAlreadyUsed.Add(sampledColor);
            return sampledColor;
        }
    }
}