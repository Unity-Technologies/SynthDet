using System.Collections.Generic;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public class PlacementObject
    {
        public GameObject Prefab { get; private set; }
        public Bounds[] BoundingBoxes { get; private set; }
        public Bounds BoundingVolume { get; private set; }

        public PlacementObject(GameObject prefab)
        {
            Prefab = prefab;
            BoundingBoxes = GetBoundingBoxes(prefab);
            BoundingVolume = GetBoundingVolume(BoundingBoxes);
        }
        
        private static Bounds[] GetBoundingBoxes(GameObject prefab)
        {
            var renderComponents = prefab.GetComponentsInChildren<Renderer>();
            if (renderComponents.Length == 0) return new Bounds[1] { new Bounds()};

            var boundingBoxes = new Bounds[renderComponents.Length];
            for (var i = 0; i < renderComponents.Length; i++)
            {
                boundingBoxes[i] = renderComponents[i].bounds;
            }
            return boundingBoxes;
        }

        private static Bounds GetBoundingVolume(IEnumerable<Bounds> boundingBoxes)
        {
            var bounds = new Bounds();
            foreach (var boundingBox in boundingBoxes)
            {
                bounds.Encapsulate(boundingBox);
            }
            return bounds;
        }
    }
}