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
    /// Creates multiple layers of evenly distributed but randomly placed objects
    /// </summary>
    [Serializable]
    [AddRandomizerMenu("SynthDet/My Background Object Placement Randomizer")]
    public class BackgroundObjectPlacementRandomizer : Randomizer
    {
        /// <summary>
        /// The Z offset component applied to all generated background layers
        /// </summary>
        public float depth;

        /// <summary>
        /// The number of background layers to generate
        /// </summary>
        public int layerCount = 2;

        /// <summary>
        /// The minimum distance between placed background objects
        /// </summary>
        public float separationDistance = 0.5f;

        /// <summary>
        /// The 2D size of the generated background layers
        /// </summary>
        public Vector2 placementArea = new Vector2(6f, 6f);

        /// <summary>
        /// A categorical parameter for sampling random prefabs to place
        /// </summary>
        public GameObjectParameter prefabs;

        GameObject m_Container;
        GameObjectOneWayCache m_GameObjectOneWayCache;

        protected override void OnAwake()
        {
            m_Container = new GameObject("Background Distractors");
            m_Container.transform.parent = scenario.transform;
            m_GameObjectOneWayCache = new GameObjectOneWayCache(m_Container.transform,
                prefabs.categories.Select((element) => element.Item1).ToArray());
        }

        /// <summary>
        /// Generates background layers of objects at the start of each scenario iteration
        /// </summary>
        protected override void OnIterationStart()
        {
            for (var i = 0; i < layerCount; i++)
            {
                var seed = SamplerState.NextRandomState();
                var placementSamples = PoissonDiskSampling.GenerateSamples(
                    placementArea.x, placementArea.y, separationDistance, seed);
                var offset = new Vector3(placementArea.x, placementArea.y, 0f) * -0.5f;
                foreach (var sample in placementSamples)
                {
                    var instance = m_GameObjectOneWayCache.GetOrInstantiate(prefabs.Sample());
                    instance.transform.position = new Vector3(sample.x, sample.y, separationDistance * i + depth) + offset;
                }

                placementSamples.Dispose();
            }
        }

        /// <summary>
        /// Deletes generated background objects after each scenario iteration is complete
        /// </summary>
        protected override void OnIterationEnd()
        {
            m_GameObjectOneWayCache.ResetAllObjects();
        }
    }
}