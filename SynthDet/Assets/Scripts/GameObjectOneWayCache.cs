using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;
using Unity.Simulation.DistributedRendering;

/// <summary>
/// Facilitates object pooling for a pre-specified collection of prefabs with the caveat that objects can be fetched
/// from the cache but not returned. Every frame, the cache needs to be reset, which will return all objects to the pool
/// </summary>
public class GameObjectOneWayCache
{
    static ProfilerMarker s_ResetAllObjectsMarker = new ProfilerMarker("ResetAllObjects");
    
    // Objects will reset to this origin when not being used
    Transform m_CacheParent;
    Dictionary<int, int> m_InstanceIdToIndex;
    List<GameObject>[] m_InstantiatedObjects;
    int[] m_NumObjectsActive;
    int NumObjectsInCache { get; set; }
    public int NumObjectsActive { get; private set; }
    private Dictionary<string, GameObject> m_ResourcesDictionary;

    public GameObjectOneWayCache(Transform parent, Dictionary<string, GameObject> resourcesDictionary)
    {
        m_CacheParent = parent;
        m_InstanceIdToIndex = new Dictionary<int, int>();
        m_InstantiatedObjects = new List<GameObject>[resourcesDictionary.Count];
        m_NumObjectsActive = new int[resourcesDictionary.Count];
        m_ResourcesDictionary = resourcesDictionary;
        
        var index = 0;
        foreach (var prefab in resourcesDictionary.Values)
        {
            var instanceId = (prefab as GameObject).GetInstanceID();
            m_InstanceIdToIndex.Add(instanceId, index);
            m_InstantiatedObjects[index] = new List<GameObject>();
            m_NumObjectsActive[index] = 0;
            ++index;
        }
    }

    public GameObject GetOrInstantiate(GameObject prefab)
    {
        if (!m_InstanceIdToIndex.TryGetValue(prefab.GetInstanceID(), out var index))
        {
            throw new ArgumentException($"Prefab {prefab.name} (ID: {prefab.GetInstanceID()}) is not in cache.");
        }

        ++NumObjectsActive;
        if (m_NumObjectsActive[index] < m_InstantiatedObjects[index].Count)
        {
            var nextInCache = m_InstantiatedObjects[index][m_NumObjectsActive[index]];
            ++m_NumObjectsActive[index];
            return nextInCache;
        }
        else
        {
            ++NumObjectsInCache;
            var newObject = Object.Instantiate(prefab, m_CacheParent);
            ++m_NumObjectsActive[index];
            m_InstantiatedObjects[index].Add(newObject);
            return newObject;
        }
    }
    
    public GameObject GetOrInstantiate(string path)
    {
        if (m_ResourcesDictionary[path] == null)
        {
            throw new ArgumentException($"Prefab {path} is not in cache.");
        }

        var prefab = m_ResourcesDictionary[path];
        
        if (!m_InstanceIdToIndex.TryGetValue(prefab.GetInstanceID(), out var index))
        {
            throw new ArgumentException($"Prefab {prefab.name} (ID: {prefab.GetInstanceID()}) is not in cache.");
        }

        ++NumObjectsActive;
        if (m_NumObjectsActive[index] < m_InstantiatedObjects[index].Count)
        {
            var nextInCache = m_InstantiatedObjects[index][m_NumObjectsActive[index]];
            ++m_NumObjectsActive[index];
            return nextInCache;
        }
        else
        {
            ++NumObjectsInCache;
            var newObject = FrameManager.Instance.Instantiate(path, m_CacheParent.gameObject) as GameObject;//Object.Instantiate(prefab, m_CacheParent);
            newObject.AddComponent<MaterialPropertyBlockUpdater>();
            ++m_NumObjectsActive[index];
            m_InstantiatedObjects[index].Add(newObject);
            return newObject;
        }
    }

    public void ResetAllObjects()
    {
        using (s_ResetAllObjectsMarker.Auto())
        {
            NumObjectsActive = 0;
            for (var i = 0; i < m_InstantiatedObjects.Length; ++i)
            {
                m_NumObjectsActive[i] = 0;
                foreach (var obj in m_InstantiatedObjects[i])
                {
                    // Position outside the frame
                    obj.transform.localPosition = new Vector3(10000, 0, 0);
                }
            }
        }
    }
}
