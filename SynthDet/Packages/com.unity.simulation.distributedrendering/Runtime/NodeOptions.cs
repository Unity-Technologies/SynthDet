using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering
{
    [CreateAssetMenu(menuName = "Distributed Rendering/UDP Node Options")]
    public class NodeOptions : ScriptableObject
    {
#if SIMULATION_CLUSTERMANAGER_PRESENT
        public ClusterOptions udpClusterOptions;
#endif
    }
}
