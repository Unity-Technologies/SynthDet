using System;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    [Serializable]
    public class GameObjectId : MonoBehaviour
    {
        public string uniqueId;

        public string prefabPath;

        public string parentObjectUniqueId;
    }
}
