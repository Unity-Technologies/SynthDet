using System.Net;

namespace Unity.Simulation.DistributedRendering
{
    public class ClusterNode
    {
        public IPAddress address;
        public uint      uniqueId;

        public ClusterNode(IPAddress address, uint uniqueId)
        {
            this.address  = address;
            this.uniqueId = uniqueId;
        }

        public override string ToString()
        {
            return $"Node id({uniqueId.ToString("X8")}) address({address.ToString()})";
        }
    }
}
