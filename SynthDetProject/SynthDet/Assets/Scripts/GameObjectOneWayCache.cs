using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Facilitates object pooling for a pre-specified collection of prefabs with the caveat that objects can be fetched
/// from the cache but not returned. Every frame, the cache needs to be reset, which will return all objects to the pool
/// </summary>
public class GameObjectOneWayCache
{
    static ProfilerMarker s_ResetAllObjectsMarker = new ProfilerMarker("ResetAllObjects");
    
    // Objects will reset to this origin when not being used
    private Transform m_cacheParent;
    private Dictionary<int, int> m_instanceIdToIndex;
    private List<GameObject>[] m_instantiatedObjects;
    private int[] m_numObjectsActive;
    public int NumObjectsInCache { get; private set; }
    public int NumObjectsActive { get; private set; }

    public GameObjectOneWayCache(Transform parent, GameObject[] prefabs)
    {
        m_cacheParent = parent;
        m_instanceIdToIndex = new Dictionary<int, int>();
        m_instantiatedObjects = new List<GameObject>[prefabs.Length];
        m_numObjectsActive = new int[prefabs.Length];
        
        var index = 0;
        foreach (var prefab in prefabs)
        {
            var instanceId = prefab.GetInstanceID();
            m_instanceIdToIndex.Add(instanceId, index);
            m_instantiatedObjects[index] = new List<GameObject>();
            m_numObjectsActive[index] = 0;
            ++index;
        }
    }

    public GameObject GetOrInstantiate(GameObject prefab)
    {
        if (!m_instanceIdToIndex.TryGetValue(prefab.GetInstanceID(), out var index))
        {
            throw new ArgumentException($"Prefab {prefab.name} (ID: {prefab.GetInstanceID()}) is not in cache.");
        }

        ++NumObjectsActive;
        if (m_numObjectsActive[index] < m_instantiatedObjects[index].Count)
        {
            var nextInCache = m_instantiatedObjects[index][m_numObjectsActive[index]];
            ++m_numObjectsActive[index];
            return nextInCache;
        }
        else
        {
            ++NumObjectsInCache;
            var newObject = GameObject.Instantiate(prefab, m_cacheParent);
            ++m_numObjectsActive[index];
            m_instantiatedObjects[index].Add(newObject);
            return newObject;
        }
    }

    public void ResetAllObjects()
    {
        using (s_ResetAllObjectsMarker.Auto())
        {
            NumObjectsActive = 0;
            for (var i = 0; i < m_instantiatedObjects.Length; ++i)
            {
                m_numObjectsActive[i] = 0;
                foreach (var obj in m_instantiatedObjects[i])
                {
                    //position outside the frame
                    obj.transform.localPosition = new Vector3(10000, 0, 0);
                }
            }
        }
    }
}
