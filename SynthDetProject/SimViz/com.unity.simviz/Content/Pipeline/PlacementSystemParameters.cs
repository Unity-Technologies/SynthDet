using Unity.Mathematics;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public struct PlacementSystemParameters
    {
        ///<summary>The minimum path distance between consecutively placed objects</summary>
        public float spacing;
        
        ///<summary>The normal distance away from a path an object will be set. Can be negative.</summary>
        public float offsetFromPath;

        ///<summary>The rotation applied to each placed object relative to the current path heading</summary>
        public quaternion rotationFromPath;
        
        ///<summary>The parent transform of which each placed object will become a child of</summary>
        public Transform parent;
        
        ///<summary>The layer mask used for occupancy detection when an object is being placed</summary>
        public int collisionLayerMask;
        
        ///<summary>The placement category from which to place objects from</summary>
        public PlacementCategory category;
    }
}