using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Parameters;
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
        static HashSet<Color> s_ColorsAlreadyUsed = new HashSet<Color>();
        static ColorRgbParameter s_SemanticColorParameter = new ColorRgbParameter();
        
        AssetLoadingStatus m_LoadingStatus = AssetLoadingStatus.InProgress;
        List<string> m_LabelStringsForAutoLabelConfig = new List<string>();

        /// <inheritdoc/>
        protected override bool isScenarioReadyToStart
        {
            get
            {
                if (m_LoadingStatus == AssetLoadingStatus.Complete)
                    return true;
                if (m_LoadingStatus == AssetLoadingStatus.Failed)
                    Quit();
                return false;
            }
        }

        protected override void OnAwake()
        {
            base.OnAwake();

            // Map bundle urls from app-param to addressables resource locations.
            // This mapping enables dynamically assigned urls to be used for remote bundle locations.
            Addressables.InternalIdTransformFunc = location => m_BundleToUrlMap.ContainsKey(location.PrimaryKey)
                ? m_BundleToUrlMap[location.PrimaryKey]
                : location.InternalId;
            
            LoadCatalogs();
        }

        async void LoadCatalogs()
        {
            // Begin loading the remote content catalogs included in the app-param
            var catalogHandles = new List<AsyncOperationHandle<IResourceLocator>>();
            foreach (var url in m_CatalogUrls)
            {
                var catalogHandle = Addressables.LoadContentCatalogAsync(url, false);
                catalogHandles.Add(catalogHandle);
            }

            await Task.WhenAll(catalogHandles.Select(handle => handle.Task));

            var allSucceeded = catalogHandles.All(handle => handle.Status == AsyncOperationStatus.Succeeded);
            if (allSucceeded)
            {
                var catalogs = catalogHandles.Select(completedHandle => completedHandle.Result);
                LoadAndLabelPrefabs(catalogs);
            }
            else
            {
                Debug.LogError("Catalogs failed to load");
                m_LoadingStatus = AssetLoadingStatus.Failed;
            }
        }
        
        void LoadAndLabelPrefabs(IEnumerable<IResourceLocator> catalogs)
        {
            // Gather only the prefab keys from the remote catalogs
            var prefabKeys = new List<string>();
            foreach (var catalog in catalogs)
            {
                foreach (var key in catalog.Keys)
                {
                    var keyPath = key.ToString();
                    if (keyPath.Contains(".prefab"))
                        prefabKeys.Add(keyPath);
                }
            }
            
            // Load all the prefabs from the remote catalogs, label them, and finally assign
            // them to the ForegroundObjectPlacementRandomizer.
            Addressables.LoadAssetsAsync<GameObject>(
                prefabKeys, null, Addressables.MergeMode.Union, false).Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    SetupLabelConfigs();
                
                    var prefabsList = new List<GameObject>(handle.Result);
                    prefabsList.Sort((prefab1, prefab2) => prefab1.name.CompareTo(prefab2.name));
                    foreach (var prefab in prefabsList)
                        ConfigureLabeling(prefab);
                
                    var randomizer = GetRandomizer<ForegroundObjectPlacementRandomizer>();
                    randomizer.prefabs = prefabsList.ToArray();
                
                    m_LoadingStatus = AssetLoadingStatus.Complete;
                }
                else
                {
                    Debug.LogError("Prefabs failed to load");
                    m_LoadingStatus = AssetLoadingStatus.Failed;
                }
            };
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
        
        static Color GetUniqueSemanticSegmentationColor()
        {
            var sampledColor = s_SemanticColorParameter.Sample();
            const int maxTries = 1000;
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
        
        enum AssetLoadingStatus
        {
            InProgress,
            Complete,
            Failed
        }
    }
}