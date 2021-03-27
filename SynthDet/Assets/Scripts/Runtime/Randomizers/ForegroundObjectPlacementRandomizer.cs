using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Randomizers.Utilities;
using UnityEngine.Perception.Randomization.Samplers;
using Object = UnityEngine.Object;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Creates a 2D layer of evenly spaced GameObjects from a given list of prefabs
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/Foreground Object Placement Randomizer")]
    public class ForegroundObjectPlacementRandomizer : Randomizer
    {
        public int maxObjectCount = 100;
        public float depth = 3f;
        public FloatParameter separationDistance = new FloatParameter { value = new UniformSampler(0.7f, 1.2f) };
        public Vector2 placementArea = new Vector2(5f, 5f);

        UniformSampler m_PrefabSampler = new UniformSampler();
        public GameObject[] prefabs = new GameObject[0];
        GameObject m_Container;
        GameObjectOneWayCache m_GameObjectOneWayCache;

        /// <inheritdoc/>
        protected override void OnScenarioStart()
        {
            m_Container = new GameObject("Foreground Objects");
            var transform = scenario.transform;
            m_Container.transform.parent = transform;

            var labelings = new List<Labeling>();
            foreach (var prefab in prefabs)
            {
                labelings.Add(ConfigureLabeling(prefab));
                ConfigureRandomizerTags(prefab);
            }

            SetupLabelConfigs(labelings);

            m_GameObjectOneWayCache = new GameObjectOneWayCache(m_Container.transform, prefabs);
        }

        /// <summary>
        /// Generates a foreground layer of objects at the start of each scenario iteration
        /// </summary>
        protected override void OnIterationStart()
        {
            PlaceObjects();
        }

        /// <summary>
        /// Deletes generated foreground objects after each scenario iteration is complete
        /// </summary>
        protected override void OnIterationEnd()
        {
            m_GameObjectOneWayCache.ResetAllObjects();
        }

        void PlaceObjects()
        {
            var seed = SamplerState.NextRandomState();
            var placementSamples = PoissonDiskSampling.GenerateSamples(
                placementArea.x, placementArea.y, separationDistance.Sample(), seed);
            var offset = new Vector3(placementArea.x, placementArea.y, 0) * -0.5f;

            var spawnedCount = 0;
            foreach (var sample in placementSamples)
            {
                var instance = m_GameObjectOneWayCache.GetOrInstantiate(GetRandomPrefab());
                instance.transform.position = new Vector3(sample.x, sample.y, depth) + offset;

                if (++spawnedCount == maxObjectCount)
                    break;
            }

            placementSamples.Dispose();
        }

        GameObject GetRandomPrefab()
        {
            return prefabs[(int)(m_PrefabSampler.Sample() * prefabs.Length)];
        }

        void ConfigureRandomizerTags(GameObject gObj)
        {
            Utilities.GetOrAddComponent<ForegroundObjectMetricReporterTag>(gObj);
            Utilities.GetOrAddComponent<UnifiedRotationRandomizerTag>(gObj);
            Utilities.GetOrAddComponent<ForegroundScaleRandomizerTag>(gObj);
        }

        public static Labeling ConfigureLabeling(GameObject gObj)
        {
            var labeling = Utilities.GetOrAddComponent<Labeling>(gObj);
            labeling.labels.Clear();
            labeling.labels.Add(gObj.name);
            return labeling;
        }

        void SetupLabelConfigs(List<Labeling> labelings)
        {
            var perceptionCamera = Object.FindObjectOfType<PerceptionCamera>();

            var idLabelConfig = ScriptableObject.CreateInstance<IdLabelConfig>();

            idLabelConfig.autoAssignIds = true;
            idLabelConfig.startingLabelId = StartingLabelId.One;

            var idLabelEntries = new List<IdLabelEntry>();
            for (var i = 0; i < labelings.Count; i++)
            {
                idLabelEntries.Add(new IdLabelEntry
                {
                    id = i,
                    label = labelings[i].labels[0]
                });
            }
            idLabelConfig.Init(idLabelEntries);

            var semanticLabelConfig = ScriptableObject.CreateInstance<SemanticSegmentationLabelConfig>();
            var semanticLabelEntries = new List<SemanticSegmentationLabelEntry>();
            for (var i = 0; i < labelings.Count; i++)
            {
                semanticLabelEntries.Add(new SemanticSegmentationLabelEntry()
                {
                    label = labelings[i].labels[0]
                });
            }
            semanticLabelConfig.Init(semanticLabelEntries);

            foreach (var labeler in perceptionCamera.labelers)
            {
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
    }
}
