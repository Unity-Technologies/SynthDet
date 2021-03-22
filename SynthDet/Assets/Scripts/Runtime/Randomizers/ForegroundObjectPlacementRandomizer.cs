using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Randomizers.Utilities;
using UnityEngine.Perception.Randomization.Samplers;

namespace SynthDet.Randomizers
{
    /// <summary>
    /// Creates a 2D layer of evenly spaced GameObjects from a given list of prefabs
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("Perception/My Foreground Object Placement Randomizer")]
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
    }
}

