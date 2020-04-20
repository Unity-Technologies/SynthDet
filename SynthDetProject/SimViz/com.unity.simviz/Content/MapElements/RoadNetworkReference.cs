using System;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content
{
    // We want to keep the RoadNetworkDescription as a ScriptableObject because we don't want it ticking with the
    // game loop. However we'd also like to be able to attach it as a component, so we need to wrap it in a
    // monobehaviour
    [Serializable]
    public class RoadNetworkReference : MonoBehaviour
    {
        [SerializeReference]
        public RoadNetworkDescription RoadNetwork;
    }
}