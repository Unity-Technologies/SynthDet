using System.Collections.Generic;
using System.Linq;
using SynthDet.Randomizers;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Samplers;
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
        
        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            if (m_PerceptionCamera && currentIterationFrame == constants.framesPerIteration - 1
            && currentIteration > 1)
            {
                //skip first iteration for capturing because labelers are not yet initialized. They are currently initialized at the end of the first iteration.
                //TO DO: Make scheduling more robust in order to capture first iteration too
                m_PerceptionCamera.RequestCapture();
            }
        }

        /// <inheritdoc/>
        protected override void OnIterationEnd()
        {
            if (currentIteration == constants.instanceCount + 1)
            {
                //it is the penultimate frame of the first iteration, so all placement randomizers have woken up and labeled their prefabs by now
                SetupLabelConfigs();
            }
        }

        static void SetupLabelConfigs()
        {
            var perceptionCamera = FindObjectOfType<PerceptionCamera>();

            var idLabelConfig = ScriptableObject.CreateInstance<IdLabelConfig>();

            idLabelConfig.autoAssignIds = true;
            idLabelConfig.startingLabelId = StartingLabelId.One;

            var stringList = LabelManager.singleton.LabelStringsForAutoLabelConfig;

            var idLabelEntries = new List<IdLabelEntry>();
            for (var i = 0; i < stringList.Count; i++)
            {
                idLabelEntries.Add(new IdLabelEntry
                {
                    id = i,
                    label = stringList[i]
                });
            }
            idLabelConfig.Init(idLabelEntries);

            var semanticLabelConfig = ScriptableObject.CreateInstance<SemanticSegmentationLabelConfig>();
            var semanticLabelEntries = new List<SemanticSegmentationLabelEntry>();
            for (var i = 0; i < stringList.Count; i++)
            {
                semanticLabelEntries.Add(new SemanticSegmentationLabelEntry()
                {
                    label = stringList[i],
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
            var seed = SamplerState.NextRandomState();
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