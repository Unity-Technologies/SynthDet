using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using UnityEngine;

using Unity.Collections.LowLevel.Unsafe;

using Debug  = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Unity.Simulation.DistributedRendering
{
    public partial class ClusterManager : IDisposable
    {
        // Public Members

        // Per client statistics
        // Round trip time for peers is only inclusive of the time for 1 peer to ack.
        // Timeouts are ignored.
        public struct PeerStats
        {
            public float minRTT;                 // smallest round trip time.
            public float maxRTT;                 // largest round trip time.
            public float avgRTT;                 // approximate round trip time when sending messages and receiving a response.
            public float avgMessageDispatchTime; // approximate time delta between receiving a message and processing it on the main thread.
        }

        // Per message statistics
        // Round trip time for server includes timeouts, resends, and all acks from clients.
        public struct MessageStats
        {
            public float minRTT;                 // smallest round trip time.
            public float maxRTT;                 // largest round trip time.
            public float avgRTT;                 // approximate round trip time when sending messages and receiving a response.
        }

        public void LogStats()
        {
            lock (_peerStatistics)
            {
                foreach (var kv in _peerStatistics)
                    Log.I(string.Format("RTT for address {0} min {1} max {2} avg {3}", kv.Key.ToString(), kv.Value.minRTT.ToString(ClusterManager.printFloatPrecision), kv.Value.maxRTT.ToString(ClusterManager.printFloatPrecision), kv.Value.avgRTT.ToString(ClusterManager.printFloatPrecision)));
            }
            lock (_messageStatistics)
            {
                foreach (var kv in _messageStatistics)
                    Log.I(string.Format("RTT for message {0} min {1} max {2} avg {3}", Utils.FourCCToString(kv.Key), kv.Value.minRTT.ToString(ClusterManager.printFloatPrecision), kv.Value.maxRTT.ToString(ClusterManager.printFloatPrecision), kv.Value.avgRTT.ToString(ClusterManager.printFloatPrecision)));
            }
        }

        public Dictionary<IPAddress, PeerStats> peerStats
        {
            get { lock (_peerStatistics) { return new Dictionary<IPAddress, PeerStats>(_peerStatistics); } }
        }

        public PeerStats GetStatsForPeer(IPAddress address)
        {
            lock (_peerStatistics) { return _peerStatistics.ContainsKey(address) ? _peerStatistics[address] : default(PeerStats); }
        }

        public MessageStats GetStatsForMessage(uint messageId)
        {
            lock (_messageStatistics) { return _messageStatistics.ContainsKey(messageId) ? _messageStatistics[messageId] : default(MessageStats); }
        }

        public Dictionary<uint, MessageStats> messageStats
        {
            get { lock (_messageStatistics) { return new Dictionary<uint, MessageStats>(_messageStatistics); } }
        }
    }
}
