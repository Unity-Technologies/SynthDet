using System;
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
using Debug = UnityEngine.Debug;

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
        
        List<string> m_LabelStringsForAutoLabelConfig = new List<string>();
        
        /// <summary>
        /// Skip the first frame since the simulation capture package cannot capture it.
        /// </summary>
        protected override bool isScenarioReadyToStart => Time.frameCount != 1;

        protected override void OnAwake()
        {
            base.OnAwake();
            try
            {
                LoadAssets();
            }
            catch (Exception)
            {
                Quit();
                throw;
            }
        }

        void LoadAssets()
        {
            if (m_CatalogUrls.Count == 0)
                throw new Exception("No content catalogs specified to load");

            // Map bundle urls from app-param to addressables resource locations.
            // This mapping enables dynamically assigned urls to be used for remote bundle locations.
            Addressables.InternalIdTransformFunc = location => m_BundleToUrlMap.ContainsKey(location.PrimaryKey)
                ? m_BundleToUrlMap[location.PrimaryKey]
                : location.InternalId;
            
            var catalogs = LoadCatalogs();
            LoadAndLabelPrefabs(catalogs);
        }

        List<IResourceLocator> LoadCatalogs()
        {
            // Clear addressables cache to ensure that we are loading asset bundles from their remote locations
            Caching.ClearCache();
            
            // Begin loading the remote content catalogs included in the app-param
            var catalogHandles = new List<AsyncOperationHandle<IResourceLocator>>();
            foreach (var url in m_CatalogUrls)
            {
                var catalogHandle = Addressables.LoadContentCatalogAsync(url);
                catalogHandles.Add(catalogHandle);
            }
            
            // Wait for catalogs to load
            for (var i = 0; i < catalogHandles.Count; i++)
            {
                var handle = catalogHandles[i];
                handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"Catalog failed to load from URL {m_CatalogUrls[i]}");
            }

            return catalogHandles.Select(handle => handle.Result).ToList();
        }
        
        void LoadAndLabelPrefabs(List<IResourceLocator> catalogs)
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
            var handle = Addressables.LoadAssetsAsync<GameObject>(
                prefabKeys, null, Addressables.MergeMode.Union);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception("Prefabs failed to load from content catalogs");

            // Sort prefabs and configure labeling
            SetupLabelConfigs();
            var prefabsList = new List<GameObject>(handle.Result);
            prefabsList.Sort((prefab1, prefab2) => prefab1.name.CompareTo(prefab2.name));
            foreach (var prefab in prefabsList)
                ConfigureLabeling(prefab);
            
            // Inject the loaded prefabs into the ForegroundObjectPlacementRandomizer
            var randomizer = GetRandomizer<ForegroundObjectPlacementRandomizer>();
            randomizer.prefabs = prefabsList.ToArray();
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
    }
}