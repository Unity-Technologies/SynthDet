using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Simulation.DistributedRendering.Render;
using UnityEditor;
using UnityEngine;

public class TagPrefabs : MonoBehaviour
{
    [MenuItem("Simulation/Distributed Rendering/Tag Prefabs")]
    public static void AddGameObjectIDComponent()
    {
        var prefabs = GetAllPrefabs();
        
        foreach (var prefabPath in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject)) as GameObject;
            if (prefab != null)
            {
                Debug.Log("Adding Component to Prefab: " + prefabPath);
                prefab.AddComponent<GameObjectId>();
                EditorUtility.SetDirty(prefab);
            }

        }
        
    }

    private static string[] GetAllPrefabs()
    {
        string[] paths = AssetDatabase.GetAllAssetPaths();

        return paths.Where(p => (p.StartsWith("Assets") && p.EndsWith(".prefab"))).ToArray();
    }
}
