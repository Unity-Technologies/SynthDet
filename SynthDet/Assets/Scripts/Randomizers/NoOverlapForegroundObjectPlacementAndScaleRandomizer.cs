using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Randomizers.Utilities;
using UnityEngine.Perception.Randomization.Samplers;

[Serializable]
[AddRandomizerMenu("Example/No Overlap Foreground Object Placement Randomizer")]
public class NoOverlapForegroundObjectPlacementRandomizer : Randomizer
{
    public float depth;
    [Tooltip("Range of scales used for objects. All objects in each frame will use the same scale.")]
    public FloatParameter scaleParameter = new FloatParameter { value = new UniformSampler(4, 8) };
    public Vector2 placementArea;
    public GameObjectParameter prefabs;
    
    [Tooltip("Degree to which we can guarantee that no objects will overlap. Use 1 for no overlap and smaller values (down to 0) for more dense placement with a possibility of some overlap.")]
    public float nonOverlapGuarantee = 1;
    
    float m_ScaleFactor = 1f;
    GameObject m_Container;
    GameObjectOneWayCache m_GameObjectOneWayCache;
    Dictionary<GameObject, float> m_GameObjectBoundsSizeCache;
    List<GameObject> m_SelectedPrefabs;
    int m_SelectionPoolSizePerFrame = 1;
    FloatParameter m_IndexSelector = new FloatParameter { value = new UniformSampler(0, 1) };

    protected override void OnCreate()
    {
        m_Container = new GameObject("Foreground Objects");
        m_Container.transform.parent = scenario.transform;
        m_GameObjectOneWayCache = new GameObjectOneWayCache(
            m_Container.transform, prefabs.categories.Select(element => element.Item1).ToArray());
        m_GameObjectBoundsSizeCache = new Dictionary<GameObject, float>();
        m_SelectedPrefabs = new List<GameObject>();

        //Calculate the average bounds size for the prefabs included in this categorical parameter
        var averageBoundsSize = CalculateAverageBoundsSize();
        
        //Calculate average scale based on the scale range given
        var averageScale = 1f;
        var sampler = (UniformSampler)scaleParameter.value;
        if (sampler != null)
        {
            averageScale = (sampler.range.minimum + sampler.range.maximum) / 2;
        }

        //Use average bounds size and average scale to guess the maximum number of objects that can be placed without having them overlap. 
        //This is a heuristic to help us start the placement process. The actual number of items placed will usually be usually much smaller.
        m_SelectionPoolSizePerFrame = (int)(placementArea.x * placementArea.y / (averageBoundsSize * averageScale));
    }

    protected override void OnIterationStart()
    {
        m_ScaleFactor = scaleParameter.Sample();
        m_SelectedPrefabs.Clear();

        //Select a random number of prefabs for this frame. Placement calculations will be done based on this subset. 
        for (var i = 0; i < m_SelectionPoolSizePerFrame; i++)
        {
            var randIndex = (int)Mathf.Round((m_IndexSelector.Sample() * prefabs.categories.Count) - 0.5f);
            m_SelectedPrefabs.Add(prefabs.categories[randIndex].Item1);
        }

        //Calculate the minimum separation distance needed for the selected prefabs to not overlap.  
        var separationDistance = CalculateMaxSeparationDistance(m_SelectedPrefabs);
        var seed = SamplerState.NextRandomState();
        var placementSamples = PoissonDiskSampling.GenerateSamples(
            placementArea.x, placementArea.y, separationDistance, seed);
        var offset = new Vector3(placementArea.x, placementArea.y, 0f) * -0.5f;
        foreach (var sample in placementSamples)
        {
            //Pick a random prefab from the selected subset and instantiate it.
            var randIndex = (int)Mathf.Round((m_IndexSelector.Sample() * m_SelectedPrefabs.Count) - 0.5f);
            var instance = m_GameObjectOneWayCache.GetOrInstantiate(m_SelectedPrefabs[randIndex]);
            instance.transform.position = new Vector3(sample.x, sample.y, depth) + offset;
            instance.transform.localScale = Vector3.one * m_ScaleFactor;
        }

        placementSamples.Dispose();
    }

    protected override void OnIterationEnd()
    {
        m_GameObjectOneWayCache.ResetAllObjects();
    }

    /// <summary>
    /// Calculates the separation distance needed between placed objects to be sure that no two objects will overlap
    /// </summary>
    /// <returns>The max separation distance</returns>
    float CalculateMaxSeparationDistance(ICollection<GameObject> categories)
    {
        var maxBoundsSize = m_GameObjectBoundsSizeCache.Where(item => categories.Contains(item.Key)).Max(pair => pair.Value); 
        return maxBoundsSize * m_ScaleFactor * nonOverlapGuarantee;
    }

    float CalculateAverageBoundsSize()
    {
        foreach (var category in prefabs.categories)
        {
            var prefab = category.Item1;

            prefab.transform.localScale = Vector3.one;
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            var totalBounds = new Bounds();
            foreach (var renderer in renderers)
            {
                totalBounds.Encapsulate(renderer.bounds);
            }

            var boundsSize = totalBounds.size.magnitude;
            m_GameObjectBoundsSizeCache.Add(prefab, boundsSize);
        }

        return m_GameObjectBoundsSizeCache.Values.Average();
    }
}
