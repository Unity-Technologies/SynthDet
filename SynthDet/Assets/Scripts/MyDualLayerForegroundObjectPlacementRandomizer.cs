using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Randomizers.Utilities;
using UnityEngine.Perception.Randomization.Samplers;


/// <summary>
/// Creates a 2D layer of of evenly spaced GameObjects from a given list of prefabs
/// </summary>
[Serializable]
[AddRandomizerMenu("Perception/My Dual Layer Foreground Object Placement Randomizer")]
public class MyDualLayerForegroundObjectPlacementRandomizer : Randomizer
{
    List<GameObject> m_SpawnedObjects;
    public int maxObjectCount;

    public GameObjectParameter prefabs;

    public bool dualLayer;

    public float layerOneDepth;
    public FloatParameter layerOneSeparationDistance;
    public Vector2 layerOnePlacementArea;

    public float layerTwoDepth;
    public FloatParameter layerTwoSeparationDistance;    
    public Vector2 layerTwoPlacementArea;

    GameObject m_Container;
    GameObjectOneWayCache m_GameObjectOneWayCache;

    protected override void OnAwake()
    {
        m_SpawnedObjects = new List<GameObject>();
        m_Container = new GameObject("Foreground Objects");
        var transform = scenario.transform;
        m_Container.transform.parent = transform;
        m_GameObjectOneWayCache = new GameObjectOneWayCache(m_Container.transform, prefabs.categories.Select(element => element.Item1).ToArray());
    }
    
    /// <summary>
    /// Generates a foreground layer of objects at the start of each scenario iteration
    /// </summary>
    protected override void OnIterationStart()
    {
        m_SpawnedObjects.Clear();
        
        PlaceLayerOneObjects();
        
        if (dualLayer)
            PlaceLayerTwoObjects();
        
        TrimObjects();
    }


    void PlaceLayerOneObjects()
    {
        var seed = SamplerState.NextRandomState();
        var placementSamples = PoissonDiskSampling.GenerateSamples(
            layerOnePlacementArea.x, layerOnePlacementArea.y, layerOneSeparationDistance.Sample(), seed);
        var offset = new Vector3(layerOnePlacementArea.x, layerOnePlacementArea.y, 0) * -0.5f;
        
        foreach (var sample in placementSamples)
        {
            var instance = m_GameObjectOneWayCache.GetOrInstantiate(prefabs.Sample());
            instance.transform.position = new Vector3(sample.x, sample.y, layerOneDepth) + offset;
            m_SpawnedObjects.Add(instance);                
        }
        placementSamples.Dispose();
    }


    void PlaceLayerTwoObjects()
    {
        var seed = SamplerState.NextRandomState();
        var placementSamples = PoissonDiskSampling.GenerateSamples(
            layerTwoPlacementArea.x, layerTwoPlacementArea.y, layerTwoSeparationDistance.Sample(), seed);
        var offset = new Vector3(layerTwoPlacementArea.x, layerTwoPlacementArea.y, 0) * -0.5f;
        
        foreach (var sample in placementSamples)
        {
            var instance = m_GameObjectOneWayCache.GetOrInstantiate(prefabs.Sample());
            instance.transform.position = new Vector3(sample.x, sample.y, layerTwoDepth) + offset;
            m_SpawnedObjects.Add(instance);
        }
        placementSamples.Dispose();
    }

    void TrimObjects()
    {
        var r = new System.Random();
        while (m_SpawnedObjects.Count > maxObjectCount)
        {
            var obj = m_SpawnedObjects.ElementAt(r.Next(0, m_SpawnedObjects.Count));
            m_SpawnedObjects.Remove(obj);
            obj.transform.localPosition = new Vector3(10000, 0, 0);
        }
    }
    
    /// <summary>
    /// Deletes generated foreground objects after each scenario iteration is complete
    /// </summary>
    protected override void OnIterationEnd()
    {
        m_GameObjectOneWayCache.ResetAllObjects();
    }
}

