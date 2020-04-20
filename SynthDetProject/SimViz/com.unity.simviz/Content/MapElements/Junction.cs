using System;
using System.Collections.Generic;
using Unity.Entities;

namespace UnityEngine.SimViz.Content.MapElements
{
    [Serializable]
    public struct Junction
    {
        public string name;
        public string junctionId;
        public List<JunctionConnection> connections;

        public Junction(string name, string junctionId, List<JunctionConnection> connections)
        {
            this.name = name;
            this.junctionId = junctionId;
            this.connections = connections;
        }

    }

    [Serializable]
    [InternalBufferCapacity(4)]
    public struct JunctionLaneLink : IBufferElementData
    {
        public int laneIdFrom;
        public int laneIdTo;

        public JunctionLaneLink(int laneIdFrom, int laneIdTo)
        {
            this.laneIdFrom = laneIdFrom;
            this.laneIdTo = laneIdTo;
        }

    }

    [Serializable]
    public struct JunctionConnection
    {
        public string connectionId;
        public string incomingRoadId;
        // XXX: Should this be renamed to outgoingRoadId?
        public string connectingRoadId;
        // The point on the successor road that this junction connects to
        public LinkContactPoint contactPoint;
        public List<JunctionLaneLink> laneLinks;

        public JunctionConnection(string connectionId, string incomingRoadId, string connectingRoadId,
            LinkContactPoint contactPoint, List<JunctionLaneLink> laneLinks)
        {
            this.connectionId = connectionId;
            this.incomingRoadId = incomingRoadId;
            this.connectingRoadId = connectingRoadId;
            this.contactPoint = contactPoint;
            this.laneLinks = laneLinks;
        }
    }
}
