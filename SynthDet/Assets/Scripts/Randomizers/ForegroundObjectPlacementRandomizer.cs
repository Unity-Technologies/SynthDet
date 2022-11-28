using System;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Perception.Randomization;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;

using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Perception.Randomization.Utilities;

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
        
        public AssetSource<GameObject> foregroundAssets;
        GameObject[] m_ProcessedAssetInstances;
        IntegerParameter m_ObjectIndexParameter = new IntegerParameter();

        public float depth = 3f;
        public FloatParameter separationDistance = new FloatParameter { value = new UniformSampler(0.7f, 1.2f) };
        public Vector2 placementArea = new Vector2(5f, 5f);

        GameObject m_Container;
        GameObjectOneWayCache m_GameObjectOneWayCache;

        [Tooltip("Enable this to normalize mesh sizes across all included objects, so that they all have a similar size before any further scale adjustments are applied during randomization. Note that this flag greatly influences the size of the objects on the screen, so any scale randomization will need to be adjusted based on the state of this flag.")]
        public bool normalizeObjectBounds = false;
        
        protected override void OnScenarioStart()
        {
            m_Container = new GameObject("Foreground Objects");
            var transform = scenario.transform;
            m_Container.transform.parent = transform;
            m_ProcessedAssetInstances = foregroundAssets.CreateProcessedInstances();
            
            m_ProcessedAssetInstances = m_ProcessedAssetInstances.Where(p =>
            {
                var isValid = ComputeBoundsUnchecked(p).IsValid;
                if (!isValid)
                    Debug.LogError($"Object {p} does not contain a mesh");

                return isValid;
            }).ToArray();
            
            m_GameObjectOneWayCache = new GameObjectOneWayCache(m_Container.transform, m_ProcessedAssetInstances, this);
            m_ObjectIndexParameter.value = new UniformSampler(0, m_ProcessedAssetInstances.Length);
        }
    
        /// <summary>
        /// Generates a foreground layer of objects at the start of each scenario iteration
        /// </summary>
        protected override void OnIterationStart()
        {
            PlaceObjects();
        }

        void PlaceObjects()
        {
            var spawnedCount = 0;
            var seed = SamplerState.NextRandomState();
            var placementSamples = PoissonDiskSampling.GenerateSamples(
                placementArea.x, placementArea.y, separationDistance.Sample(), seed);
            var offset = new Vector3(placementArea.x, placementArea.y, 0) * -0.5f;
        
            foreach (var sample in placementSamples)
            {
                var index = Math.Min(m_ProcessedAssetInstances.Length, m_ObjectIndexParameter.Sample());
                var prefab = m_ProcessedAssetInstances[index];
                var instance = m_GameObjectOneWayCache.GetOrInstantiate(prefab);

                if (normalizeObjectBounds)
                {
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localScale = Vector3.one;
                    instance.transform.localRotation = Quaternion.identity;
                    var bounds = ComputeBounds(instance);
                
                    instance.transform.localPosition = new Vector3(sample.x, sample.y, depth) + offset - bounds.center;
                    var scale = instance.transform.localScale;
                    var magnitude = bounds.extents.magnitude;
                    scale.Scale(new Vector3(1/magnitude, 1/magnitude, 1/magnitude));
                    instance.transform.localScale = scale;
                }
                else
                {
                    instance.transform.position = new Vector3(sample.x, sample.y, depth) + offset;    
                }
                
                if (++spawnedCount == maxObjectCount)
                    break;
            }
            placementSamples.Dispose();
        }

        /// <summary>
        /// Deletes generated foreground objects after each scenario iteration is complete
        /// </summary>
        protected override void OnIterationEnd()
        {
            m_GameObjectOneWayCache.ResetAllObjects();
        }

        static NativeArray<Bounds> ComputeObjectBounds(GameObject[] prefabs)
        {
            var objectBounds = new NativeArray<Bounds>(prefabs.Length, Allocator.TempJob);
            for (int i = 0; i < prefabs.Length; i++)
            {
                var bounds = ComputeBounds(prefabs[i]);
                //assume objects will be aligned at origin
                bounds.center = Vector3.zero;
                objectBounds[i] = bounds;
            }

            return objectBounds;
        }
        
        public static Bounds ComputeBounds(GameObject gameObject)
        {
            var bounds = ComputeBoundsUnchecked(gameObject);
            if (!bounds.IsValid)
                throw new ArgumentException($"GameObject {gameObject.name} must have a MeshFilter in its hierarchy.");

            var result = new Bounds();
            result.SetMinMax(bounds.Min, bounds.Max);
            return result;
        }

        static SynthDetMinMaxAABB ComputeBoundsUnchecked(GameObject gameObject)
        {
            SynthDetMinMaxAABB aabb = new SynthDetMinMaxAABB(
                new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), 
                new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                var bounds = meshFilter.sharedMesh.bounds;
                aabb = SynthDetMinMaxAABB.CreateFromCenterAndExtents(bounds.center, bounds.extents);
            }

            var transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var childAabb = ComputeBoundsUnchecked(transform.GetChild(i).gameObject);
                aabb.Encapsulate(childAabb);
            }

            aabb = SynthDetMinMaxAABB.Transform(float4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale), aabb);
            return aabb;
        }
        
    }
}

