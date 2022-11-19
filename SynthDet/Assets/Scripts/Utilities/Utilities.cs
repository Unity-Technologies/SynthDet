using UnityEngine;

namespace SynthDet.Randomizers
{
    public static class Utilities
    {
        public static Component GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component ? component : gameObject.AddComponent<T>();
        }
    }
}
