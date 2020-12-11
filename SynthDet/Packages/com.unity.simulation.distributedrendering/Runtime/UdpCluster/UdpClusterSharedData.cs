#if SIMULATION_CLUSTERMANAGER_PRESENT
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    /// <summary>
    /// Cluster interface for physics & render nodes
    /// </summary>
    public abstract class UdpClusterSharedData : IWorkerSharedData
    {
        protected ClusterManager _cluster;
        protected ClusterNode[] _clusterNodes;

        public void StartClusterDiscovery(NodeOptions nodeOptions)
        {
            if (Application.isBatchMode)
            {
                Log.logToConsole = true;
            }
            
            _cluster = new ClusterManager(nodeOptions.udpClusterOptions, state =>
                {
                    switch (state)
                    {
                        case ClusterState.Discovering:
                            Log.I("Cluster Discovery has begun.");
                            break;

                        case ClusterState.Ready:
                            // This call is expensive, so cache it.
                            // There is a delegate for when nodes change if you need to know this.
                            _clusterNodes = _cluster.Nodes;
                            Log.I("Cluster ready...");
                            foreach (var node in _clusterNodes)
                            {
                                Log.I($"Found node: {node.ToString()}");
                            }
                            break;

                        case ClusterState.TimedOut:
                            Log.W("Cluster Discovery timed out.");
                            break;

                        case ClusterState.Disposed:
                            Log.I("Cluster has been disposed.");
                            break;
                    }
                },
                false
            );
        }

        protected ClusterNode GetClusterNodeFromMessage(Message message)
        {
            var uniqueId = Utils.UniqueIdFromInstanceId(message.instanceId);
            var node = _cluster.GetNodeIfPresent(uniqueId);
            Debug.Assert(null != node, "Could not find destination node for this message");

            return node;
        }

        public virtual void Dispose()
        {
            _cluster?.Dispose();
            _cluster = null;
        }
    }
}
#endif

