using System;

namespace UnityEngine.SimViz.Content.Pipeline
{
    [CreateAssetMenu(fileName = "PlacementCategory", menuName = "Content/PlacementCategory", order = 2)]
    public class PlacementCategory : ScriptableObject
    {
        public GameObject[] prefabs;

        public GameObject NextGameObject()
        {
            if (prefabs.Length == 0)
                throw new Exception("Prefabs array is empty");
            var index = Random.Range(0, prefabs.Length);
            return prefabs[index];
        }
        
        public PlacementObject NextPlacementObject()
        {
            if (prefabs.Length == 0)
                throw new Exception("Prefabs array is empty");
            var index = Random.Range(0, prefabs.Length);
            return new PlacementObject(prefabs[index]);
        }
    }
}