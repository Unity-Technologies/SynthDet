using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Collections.LowLevel.Unsafe;

using Debug = UnityEngine.Debug;

namespace Unity.Simulation.DistributedRendering
{
    internal class ReceiveData
    {
        public uint recipientId;
        public uint messageId;
        public ulong instanceId;
        public IPAddress address;
        public float timeoutSecs;
        public ulong secret;
        public byte[] payload;
    };

    /// <summary>
    /// Class that represents a cluster of nodes on the same network.
    /// </summary>
    public partial class ClusterManager : IDisposable
    {
        // Public Members

        /// <summary>
        /// Constant for the float precision to use when printing log statements.
        /// </summary>
        public static string printFloatPrecision = "F4";

        /// <summary>
        /// Delegate type for message reception handling.
        /// </summary>
        /// <param name="message">The inbound message that was received.</param>
        public delegate void HandleMessageDelegate(Message message);

        /// <summary>
        /// Delegate type for message completion notification.
        /// </summary>
        /// <param name="message">The outbound message that was successfully delivered.</param>
        public delegate void CompleteMessageDelegate(OutboundMessage message);

        /// <summary>
        /// Delegate type for cluster state change notification.
        /// </summary>
        /// <param name="clusterState">The current state of the cluster.</param>
        public delegate void StateChangedDelegate(ClusterState clusterState);

        /// <summary>
        /// Delegate type for node count change notification.
        /// </summary>
        /// <param name="clusterNode">The node that was added or removed.</param>
        /// <param name="nodeCount">The new node count.</param>
        /// <param name="previousNodeCount">The previous node count.</param>
        public delegate void NodeCountChangedDelegate(ClusterNode clusterNode, int nodeCount, int previousNodeCount);

        /// <summary>
        /// Delegate that is invoked when the cluster state changes.
        /// </summary>
        public StateChangedDelegate StateChanged { get; set; }

        /// <summary>
        /// Delegate that is invoked when the number of sibling nodes changes.
        /// </summary>
        public NodeCountChangedDelegate NodeCountChanged { get; set; }

        /// <summary>
        /// The configurable options for this cluster.
        /// </summary>
        public ClusterOptions Options { get; protected set; }

        /// <summary>
        /// The current state of the cluster.
        /// When the state of the cluster changes, it will invoke the stateChangedDelegate.
        /// </summary>
        public ClusterState ClusterState
        {
            get { return _clusterState; }
            protected set
            {
                if (_clusterState != value)
                {
                    _clusterState = value;
                    this.StateChanged?.Invoke(_clusterState);
                }
            }
        }

        /// <summary>
        /// The current node count of the cluster.
        /// </summary>
        public int NodeCount { get; protected set; }

        /// <summary>
        /// Retrieves the array of nodes currently known by the cluster.
        /// Note: This is an expensive call, and should not be used often.
        /// It's best to cache this return value, and update when the node count changes.
        /// </summary>
        public ClusterNode[] Nodes
        {
            get
            {
                lock (_mutex)
                {
                    return (new List<ClusterNode>(_nodes.Values)).ToArray();
                }
            }
        }

        /// <summary>
        /// Constant for the multicast node.
        /// Use this node to broadcast to all known nodes.
        /// </summary>
        public ClusterNode MulticastClusterNode { get; protected set; }

        /// <summary>
        /// Constant for the multicast IP address.
        /// </summary>
        public IPAddress MulticastIpAddress { get; protected set; }

        /// <summary>
        /// The local IP address of this node.
        /// </summary>
        public IPAddress LocalIpAddress { get; protected set; }

        /// <summary>
        /// Unique identifier for this node.
        /// When multiple clients are running on the same machine, this will be used to identify each on uniquely.
        /// </summary>
        public uint UniqueIdentifier { get; protected set; }

        /// <summary>
        /// Constructor for creating a new cluster.
        /// </summary>
        /// <param name="options">The configuration options to use for the cluster.</param>
        /// <param name="stateChanged">The optional delegate to invoke when the cluster state changes.</param>
        /// <param name="autoDispose">Flag to instruct the ClusterManager to be automatically disposed on shutdown.</param>
        public ClusterManager(
            ClusterOptions options,
            StateChangedDelegate stateChanged = null,
            bool autoDispose = true
        )
        {
            this.Options = options;

            _timer.Start();

            this.StateChanged = stateChanged;

            Log.I($"Initialize Cluster listening on port {options.port}");

            this.UniqueIdentifier = (uint)UnityEngine.Random.Range(0, int.MaxValue) ^ (uint)Process.GetCurrentProcess().Id;
            this.MulticastIpAddress = IPAddress.Parse("224.0.0.2");
            this.LocalIpAddress = Utils.LocalIPAddress();
            this.MulticastClusterNode = new ClusterNode(this.MulticastIpAddress, 0);

            _remoteEndpoint = new IPEndPoint(this.MulticastIpAddress, options.port);
            _localEndpoint = new IPEndPoint(IPAddress.Any, options.port);

            _udpClient = new UdpClient();
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(_localEndpoint);

            _udpClient.JoinMulticastGroup(this.MulticastIpAddress, IPAddress.Any);

            _processThread = new Thread(new ThreadStart(_ProcessThreadFunc));
            _processThread.Start();

            _receiveThread = new Thread(new ThreadStart(_ReceiveThreadFunc));
            _receiveThread.Start();

            _forward = Utils.CreateForward("ClusterUpdate");
            _forward.updateDelegate += _DispatchMainThreadActions;
            if (autoDispose)
            {
                _forward.shutdownDelegate += Dispose;
            }

            this.ClusterState = ClusterState.Discovering;

            RegisterHandler(kMessageIdIdentify, true, (Message message) =>
            {
                if (NodeCount >= options.expectedNodes && this.ClusterState == ClusterState.Discovering)
                {
                    Log.V($"Cluster disovery completed in {ElapsedTime()} seconds.");
                    this.ClusterState = ClusterState.Ready;
                }
            });

            RegisterHandler(kMessageIdShutdown, true, (Message message) =>
            {
                _RemoveNodeIfPresent(Utils.UniqueIdFromInstanceId(message.instanceId));
            });

            SendMessage(this.MulticastClusterNode, kMessageIdIdentify, options.identifyClusterTimeoutSec, (OutboundMessage message) =>
            {
                if (message.timedOut)
                {
                    Log.V($"Cluster disovery timed out after {options.identifyClusterTimeoutSec} seconds.");
                    this.ClusterState = ClusterState.TimedOut;
                }
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                bool done = false;
                SendMessage(this.MulticastClusterNode, kMessageIdShutdown, Options.sendMessageTimeoutSec, (OutboundMessage message) =>
                {
                    done = true;
                });

                while (!done)
                    ;

                this.ClusterState = ClusterState.Disposed;
                _run = false;

                _processThread?.Join();
                _processThread = null;

                _receiveThread?.Join();
                _receiveThread = null;

                // Close the socket *after* joining the threads since the threads may be using it.
                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;

                _processSemaphore?.Dispose();
                _processSemaphore = null;
            }
        }

        public void RegisterHandler(uint messageId, bool immediate, HandleMessageDelegate callback)
        {
            lock (_mutex)
            {
                List<Handler> handlers;
                if (!_handlers.TryGetValue(messageId, out handlers))
                {
                    handlers = new List<Handler>();
                    _handlers.Add(messageId, handlers);
                }
                handlers.Add(new Handler(callback, immediate));
            }
        }

        public void UnregisterHandler(uint messageId, HandleMessageDelegate handler)
        {
            lock (_mutex)
            {
                Debug.Assert(_handlers.ContainsKey(messageId));
                _handlers.Remove(messageId);
            }
        }

        public ulong SendMessage(ClusterNode clusterNode, uint messageId, float timeoutSecs = -1, CompleteMessageDelegate completion = null)
        {
            if (timeoutSecs < 0) timeoutSecs = this.Options.sendMessageTimeoutSec;

            var instanceId = _GenerateInstanceId();
            var expectedAcks = clusterNode == this.MulticastClusterNode ? NodeCount : 1;
            var messageData = _WriteMessageData(messageId, this.LocalIpAddress, clusterNode.uniqueId, instanceId, timeoutSecs, Options.secret, null);

            _StartSendingMessage(new OutboundMessage(messageId, clusterNode.address, timeoutSecs, expectedAcks, instanceId, ElapsedTime(), messageData, completion));

            return instanceId;
        }

        public ulong SendMessage<T>(ClusterNode clusterNode, uint messageId, T[] payload, float timeoutSecs = -1, CompleteMessageDelegate completion = null) where T : struct
        {
            if (timeoutSecs < 0) timeoutSecs = this.Options.sendMessageTimeoutSec;

            var instanceId = _GenerateInstanceId();
            var expectedAcks = clusterNode == this.MulticastClusterNode ? NodeCount : 1;
            var messageData = _WriteMessageData(messageId, this.LocalIpAddress, clusterNode.uniqueId, instanceId, timeoutSecs, Options.secret, Utils.ToByteArray(payload));

            _StartSendingMessage(new OutboundMessage(messageId, clusterNode.address, timeoutSecs, expectedAcks, instanceId, ElapsedTime(), messageData, completion));

            return instanceId;
        }

        public ulong SendMessage<T>(ClusterNode clusterNode, uint messageId, ref T payload, float timeoutSecs = -1, CompleteMessageDelegate completion = null) where T : struct
        {
            Debug.Assert(UnsafeUtility.IsBlittable(payload.GetType()));

            if (timeoutSecs < 0) timeoutSecs = this.Options.sendMessageTimeoutSec;

            var instanceId = _GenerateInstanceId();
            var expectedAcks = clusterNode == this.MulticastClusterNode ? NodeCount : 1;
            var messageData = _WriteMessageData(messageId, this.LocalIpAddress, clusterNode.uniqueId, instanceId, timeoutSecs, Options.secret, Utils.ToByteArray(ref payload));

            _StartSendingMessage(new OutboundMessage(messageId, clusterNode.address, timeoutSecs, expectedAcks, instanceId, ElapsedTime(), messageData, completion));

            return instanceId;
        }

        public double ElapsedTime(double startTime = 0)
        {
            return _timer.Elapsed.TotalSeconds - startTime;
        }

        // Non Public Members

        ulong _GenerateInstanceId()
        {
            return ((ulong)this.UniqueIdentifier) << 32 | (uint)Interlocked.Increment(ref _nextMessageId);
        }

        void _ReceiveThreadFunc()
        {
            try
            {
                Task<UdpReceiveResult> receiveTask = null;

                while (_run)
                {
                    if (receiveTask == null)
                    {
                        receiveTask = _udpClient.ReceiveAsync();
                    }
                    else
                    {
                        if (receiveTask.IsCompleted)
                        {
                            do
                            {
                                var endpoint = receiveTask.Result.RemoteEndPoint;
                                byte[] bytes = receiveTask.Result.Buffer;

                                Datagram datagram = new Datagram(bytes, bytes.Length, endpoint);
                                ReceiveData receiveData = _ExtractMessageData(datagram);

                                // Discard any invalid messages.
                                if (!_VerifyReceivedMessage(receiveData))
                                {
                                    break;
                                }

                                Log.V($"_ReceiveThreadFunc: Received messageId {Utils.FourCCToString(receiveData.messageId)} instanceId {Utils.InstanceToString(receiveData.instanceId)} {Utils.HexDump(datagram.data, datagram.length)}");

                                _receiveQueue.Enqueue(receiveData);
                                _processSemaphore.Release();
                            } while (false);
                        }

                        else if (receiveTask.IsCanceled)
                        {
                            Log.W("_ReceiveThreadFunc: UDP receive task canceled");
                        }

                        else if (receiveTask.IsFaulted)
                        {
                            Log.E($"_ReceiveThreadFunc: UDP receive task encountered an exception: {receiveTask.Exception}");
                        }

                        if (receiveTask.IsCompleted || receiveTask.IsCanceled || receiveTask.IsFaulted)
                        {
                            // Clear the task object when no longer waiting on a result.
                            receiveTask = null;
                        }
                    }
                }
            }
            catch (ObjectDisposedException e)
            {
                // This may happen when shutting down, so we only need to log while we're still running.
                if (_run)
                {
                    Log.V($"_ReceiveThreadFunc: Object Disposed Exception: {e.Message}");
                }
            }
            catch (SocketException e)
            {
                // when shutting down, there's no way to tell the receive socket to stop.
                // It will timeout when disposing, and throw an exception. We catch that
                // here and ignore, unless we are still running, in which case we log.
                if (_run)
                {
                    Log.V($"_ReceiveThreadFunc: Socket Exception: {e.Message}");
                }
            }
        }

        void _ProcessThreadFunc()
        {
            OutboundMessage[] outbound = null;
            Message[] inbound = null;

            while (_run)
            {
                do
                {
                    // The semaphore is authoritative, so we wait on it until it unblocks or times out.
                    // If it times out, then nothing is in the queue. We also need to wait for each item
                    // in the queue since it effectively functions as a counter. If we don't wait on it
                    // once for each release, we'll eventually overflow the semaphore.
                    if (!_processSemaphore.WaitOne((int) (1000 * Options.resendThreadSleepTimeSec)))
                    {
                        break;
                    }

                    // Pull received data from the queue, then process it.
                    if (_receiveQueue.TryDequeue(out ReceiveData receiveData))
                    {
                        _ReceiveMessage(receiveData);
                    }
                } while (true);

                var time = ElapsedTime();

                int outboundCount = 0;
                int inboundCount = 0;

                lock (_mutex)
                {
                    if (outbound == null || outbound.Length < _outboundMessages.Count)
                        outbound = new OutboundMessage[_outboundMessages.Count];
                    _outboundMessages.Values.CopyTo(outbound, 0);
                    outboundCount = _outboundMessages.Count;

                    if (inbound == null || inbound.Length < _inboundMessages.Count)
                        inbound = new Message[_inboundMessages.Count];
                    _inboundMessages.Values.CopyTo(inbound, 0);
                    inboundCount = _inboundMessages.Count;
                }

                for (var i = 0; i < outboundCount; ++i)
                {
                    var message = outbound[i];

                    if (message.arrivalTime + message.timeoutSecs < time)
                    {
                        _StopSendingMessage(message);
                    }
                    else if (time > message.lastTime + Options.messageAckRetryTimeoutSec)
                    {
                        message.lastTime = time;
                        _Send(message, Options.port);
                    }
                }

                for (var i = 0; i < inboundCount; ++i)
                {
                    var message = inbound[i];

                    if (message.arrivalTime + message.timeoutSecs < time)
                    {
                        lock (_inboundMessages)
                        {
                            if (_inboundMessages.ContainsKey(message.instanceId))
                                _inboundMessages.Remove(message.instanceId);
                        }

                        Log.V(string.Format("Expiring inbound message {0} instance {1} after {2} seconds.", Utils.FourCCToString(message.messageId), Utils.InstanceToString(message.instanceId), message.timeoutSecs));
                    }
                }
            }
        }

        bool _VerifyReceivedMessage(ReceiveData receiveData)
        {
            if (receiveData.recipientId != 0 && receiveData.recipientId != UniqueIdentifier)
            {
                Log.V($"_ReceiveThreadFunc: Ignoring received data intended for recipient using same address. messageId {Utils.FourCCToString(receiveData.messageId)} instanceId {Utils.InstanceToString(receiveData.instanceId)} uniqueId {receiveData.recipientId.ToString("X7")}");
                return false;
            }

            if (receiveData.secret != Options.secret)
            {
                Log.V($"_ReceiveThreadFunc: Rejecting message from foreign cluster address {receiveData.address}");
                return false;
            }

            var nodeUniqueId = Utils.UniqueIdFromInstanceId(receiveData.instanceId);
            if (nodeUniqueId == UniqueIdentifier)
            {
                Log.V($"_ReceiveThreadFunc: Ignoring received data from self. messageId {Utils.FourCCToString(receiveData.messageId)} instanceId {Utils.InstanceToString(receiveData.instanceId)}");
                return false;
            }

            return true;
        }

        ReceiveData _ExtractMessageData(Datagram datagram)
        {
            var output = new ReceiveData();
            var offset = _ReadMessageData(
                datagram,
                out output.recipientId,
                out output.messageId,
                out output.instanceId,
                out output.address,
                out output.timeoutSecs,
                out output.secret
            );

            var payloadLength = datagram.length - offset;

            output.payload = new byte[payloadLength];
            Array.Copy(
                datagram.data,
                offset,
                output.payload,
                0,
                payloadLength
            );

            return output;
        }

        void _ReceiveMessage(ReceiveData receiveData)
        {
            _AddNodeIfNotPresent(receiveData.address, Utils.UniqueIdFromInstanceId(receiveData.instanceId));

            lock (_mutex)
            {
                // Only ack will have a corresponding outbound message.
                if (receiveData.messageId == kMessageIdAck)
                {
                    AckPayload ack;
                    Utils.Read(receiveData.payload, 0, out ack);

                    OutboundMessage outbound;
                    if (_outboundMessages.TryGetValue(ack.instanceId, out outbound))
                    {
                        _UpdateClientStats(receiveData.address, outbound, ack);

                        Log.V($"_ReceiveCallback: Ack ({Utils.FourCCToString(outbound.messageId)}) took {(ElapsedTime() - outbound.lastTime).ToString(ClusterManager.printFloatPrecision)} seconds. Dispatch delay {ack.avgMessageDispatchTime.ToString(ClusterManager.printFloatPrecision)}");

                        outbound.HandleAck(receiveData.address, ElapsedTime());

                        if (outbound.completed)
                        {
                            _StopSendingMessage(outbound);
                            _DispatchMessage(receiveData.messageId, outbound);
                        }
                    }
                }
                else
                {
                    Message message = null;
                    if (!_inboundMessages.TryGetValue(receiveData.instanceId, out message))
                    {
                        Log.V($"_ReceiveCallback: Dispatching messageId {Utils.FourCCToString(receiveData.messageId)} instanceId {Utils.InstanceToString(receiveData.instanceId)}");

                        message = new Message(receiveData.messageId, receiveData.address, receiveData.instanceId, ElapsedTime(), receiveData.timeoutSecs, receiveData.payload);
                        lock (_inboundMessages)
                        {
                            _inboundMessages.Add(receiveData.instanceId, message);
                        }
                        _DispatchMessage(receiveData.messageId, message);
                    }
                    else
                    {
                        Log.V($"_ReceiveCallback: Already dispatched message messageId {Utils.FourCCToString(receiveData.messageId)} instanceId {Utils.InstanceToString(receiveData.instanceId)}");
                    }
                    _AckMessage(message);
                }
            }
        }

        void _DispatchMessage(uint messageId, Message message)
        {
            List<Handler> handlers;
            if (_handlers.TryGetValue(messageId, out handlers))
            {
                foreach (var handler in handlers)
                {
                    if (handler.immediate)
                        handler.callback(message);
                    else
                        _QueueForMainThread(() =>
                        {
                            _Smooth(ref _avgMessageDispatchTime, (float)ElapsedTime(message.arrivalTime));
                            handler.callback(message);
                        });
                }
            }
        }

        void _AckMessage(Message message)
        {
            var instanceId = _GenerateInstanceId();
            var recipientUniqueId = Utils.UniqueIdFromInstanceId(message.instanceId);

            AckPayload payload;
            payload.instanceId = message.instanceId;
            payload.avgMessageDispatchTime = _avgMessageDispatchTime;

            var messageData = _WriteMessageData(kMessageIdAck, this.LocalIpAddress, recipientUniqueId, instanceId, 0, Options.secret, Utils.ToByteArray(ref payload));

            _Send(new Message(kMessageIdAck, message.address, instanceId, 0, 0, messageData), Options.port, string.Format("({0})", Utils.FourCCToString(message.messageId)));
        }

        int _AddNodeIfNotPresent(IPAddress address, uint nodeUniqueId)
        {
            if (nodeUniqueId == UniqueIdentifier)
            {
                Log.E($"Adding self as node: {nodeUniqueId}; this should never happen");
                throw new InvalidDataException(nodeUniqueId.ToString("X8"));
            }

            lock (_mutex)
            {
                if (!_nodes.ContainsKey(nodeUniqueId))
                {
                    var lastNodeCount = NodeCount;
                    var node = new ClusterNode(address, nodeUniqueId);
                    _nodes.Add(nodeUniqueId, node);
                    NodeCount = _nodes.Count;
                    NodeCountChanged?.Invoke(node, NodeCount, lastNodeCount);
                    Log.V($"_AddNodeIfNotPresent: Adding new node id {node.uniqueId.ToString("X8")} address {node.address}. Node count is now {NodeCount}");
                }
            }

            // expectedNodes is inclusive of us, so we test for one less.
            if (NodeCount >= (Options.expectedNodes - 1) && this.ClusterState == ClusterState.Discovering)
            {
                _QueueForMainThread(() => { this.ClusterState = ClusterState.Ready; });
            }

            return NodeCount;
        }

        public ClusterNode GetNodeIfPresent(uint nodeUniqueId)
        {
            lock (_mutex)
            {
                if (!_nodes.ContainsKey(nodeUniqueId))
                    return null;
                else
                    return _nodes[nodeUniqueId];
            }
        }

        int _RemoveNodeIfPresent(uint nodeUniqueId)
        {
            lock (_mutex)
            {
                if (_nodes.ContainsKey(nodeUniqueId))
                {
                    var lastNodeCount = NodeCount;
                    var node = _nodes[nodeUniqueId];
                    _nodes.Remove(nodeUniqueId);
                    NodeCount = _nodes.Count;
                    NodeCountChanged?.Invoke(node, NodeCount, lastNodeCount);
                    Log.V($"_RemoveNodeIfPresent: Removing node id {node.uniqueId.ToString("X8")} address {node.address}. Node count is now {NodeCount}");
                }
            }

            return NodeCount;
        }

        static byte[] _WriteMessageData(uint messageId, IPAddress localIPAddress, uint recipientId, ulong instanceId, float timeoutSecs, ulong secret, byte[] additionalPayload)
        {
            var addressLength = localIPAddress.AddressFamily == AddressFamily.InterNetworkV6 ? 16 : 4;
            var payloadLength = additionalPayload != null ? additionalPayload.Length : 0;
            var length = sizeof(uint) + sizeof(uint) + addressLength + sizeof(ulong) + sizeof(float) + sizeof(ulong) + payloadLength;
            var buffer = new byte[length];

            var index = 0;
            Utils.Write(buffer, index, ref recipientId); index += sizeof(uint);
            Utils.Write(buffer, index, ref messageId); index += sizeof(uint);
            Utils.Write(buffer, index, ref instanceId); index += sizeof(ulong);
            Utils.Write(buffer, index, ref secret); index += sizeof(ulong);
            Utils.Write(buffer, index, ref timeoutSecs); index += sizeof(float);
            Utils.Write(buffer, index, localIPAddress.GetAddressBytes()); index += sizeof(uint);

            if (additionalPayload != null)
                Utils.Write(buffer, index, additionalPayload); index += payloadLength;

            return buffer;
        }

        static int _ReadMessageData(Datagram datagram, out uint recipientId, out uint messageId, out ulong instanceId, out IPAddress address, out float timeoutSecs, out ulong secret)
        {
            var offset = 0;
            Utils.Read(datagram.data, offset, out recipientId); offset += sizeof(uint);
            Utils.Read(datagram.data, offset, out messageId); offset += sizeof(uint);
            Utils.Read(datagram.data, offset, out instanceId); offset += sizeof(ulong);
            Utils.Read(datagram.data, offset, out secret); offset += sizeof(ulong);
            Utils.Read(datagram.data, offset, out timeoutSecs); offset += sizeof(float);

            var addressLength = datagram.endpoint.AddressFamily == AddressFamily.InterNetworkV6 ? 16 : 4;
            byte[] addressBytes = new byte[addressLength];
            Utils.Read(datagram.data, offset, addressBytes); offset += sizeof(uint);
            address = new IPAddress(addressBytes);

            return offset;
        }

        void _StartSendingMessage(OutboundMessage message)
        {
            lock (_mutex)
            {
                Debug.Assert(!_outboundMessages.ContainsKey(message.instanceId));
                _outboundMessages.Add(message.instanceId, message);
            }
            _Send(message, Options.port);
        }

        void _StopSendingMessage(OutboundMessage message)
        {
            message.Complete(ElapsedTime());

            lock (_mutex)
            {
                if (_outboundMessages.ContainsKey(message.instanceId))
                    _outboundMessages.Remove(message.instanceId);
            }

            _UpdateMessageStats(message);

            Log.V(string.Format("_StopSendingMessage: Message messageId {0} instanceId {1} has completed. timeout {2}", Utils.FourCCToString(message.messageId), Utils.InstanceToString(message.instanceId), message.timedOut.ToString()));

            // DECIDE!!!!! should completion be on main thread or immediate (kinda need both)
            //_QueueForMainThread(() => { message.completion?.Invoke(message); });
            message.completion?.Invoke(message);
        }

        void _Send(Message message, ushort port, string context = null)
        {
            Log.V(string.Format("_Send: message {0} instanceId {1} to {2}:{3} payload: {4}", Utils.FourCCToString(message.messageId, context), Utils.InstanceToString(message.instanceId), message.address.ToString(), port, Utils.HexDump(message.payload, message.payload.Length)));
            var bytes = _udpClient.Send(message.payload, message.payload.Length, _remoteEndpoint);
            Debug.Assert(bytes == message.payload.Length);
        }

        void _QueueForMainThread(Action action)
        {
            lock (_queueForMainThread)
            {
                _queueForMainThread.Enqueue(action);
            }
        }

        void _DispatchMainThreadActions()
        {
            Debug.Assert(Utils.IsMainThread(), "_DispatchMainThreadActions must be called on main thread.");

            var count = 0;
            lock (_queueForMainThread)
            {
                if (_queueForMainThread.Count > 0)
                {
                    count = _queueForMainThread.Count;
                    if (_actionsForMainThread == null || _actionsForMainThread.Length < _queueForMainThread.Count)
                        _actionsForMainThread = _queueForMainThread.ToArray();
                    else
                        _queueForMainThread.CopyTo(_actionsForMainThread, 0);
                    _queueForMainThread.Clear();
                }
            }

            for (var i = 0; i < count; ++i)
            {
                _actionsForMainThread[i].Invoke();
                _actionsForMainThread[i] = null;
            }
        }

        void _UpdateClientStats(IPAddress address, OutboundMessage message, AckPayload ack)
        {
            lock (_peerStatistics)
            {
                if (!_peerStatistics.ContainsKey(address))
                    _peerStatistics.Add(address, new PeerStats());

                var stat = _peerStatistics[address];
                stat.minRTT = Math.Min(stat.minRTT, message.totalTime);
                stat.maxRTT = Math.Max(stat.maxRTT, message.totalTime);
                _Smooth(ref stat.avgRTT, (float)ElapsedTime(message.lastTime));
                stat.avgMessageDispatchTime = ack.avgMessageDispatchTime;
                _peerStatistics[address] = stat;
            }
        }

        void _UpdateMessageStats(OutboundMessage message)
        {
            if (!message.timedOut)
            {
                double now = _timer.Elapsed.TotalSeconds;

                lock (_messageStatistics)
                {
                    if (!_messageStatistics.ContainsKey(message.messageId))
                        _messageStatistics.Add(message.messageId, new MessageStats());

                    var stat = _messageStatistics[message.messageId];
                    stat.minRTT = Math.Min(stat.minRTT, message.totalTime);
                    stat.maxRTT = Math.Max(stat.maxRTT, message.totalTime);
                    _Smooth(ref stat.avgRTT, (float)ElapsedTime(message.lastTime));
                    _messageStatistics[message.messageId] = stat;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void _Smooth(ref float value, float nextValue, float threshold = .1f)
        {
            value = (1 - threshold) * value + threshold * nextValue;
        }

        struct AckPayload
        {
            public ulong instanceId;
            public float avgMessageDispatchTime;
        }

        struct Handler
        {
            public bool immediate;
            public HandleMessageDelegate callback;
            public Handler(HandleMessageDelegate callback, bool immediate = false)
            {
                this.callback = callback;
                this.immediate = immediate;
            }
        }

        static uint kMessageIdAck = Utils.FourCC("_ACK");
        static uint kMessageIdIdentify = Utils.FourCC("_IDF");
        static uint kMessageIdShutdown = Utils.FourCC("_EXT");

        object _mutex = new object();

        Stopwatch _timer = new Stopwatch();

        int _nextMessageId;

        ClusterState _clusterState;

        Dictionary<uint, ClusterNode> _nodes = new Dictionary<uint, ClusterNode>();
        Dictionary<ulong, OutboundMessage> _outboundMessages = new Dictionary<ulong, OutboundMessage>();
        Dictionary<ulong, Message> _inboundMessages = new Dictionary<ulong, Message>();
        Dictionary<uint, List<Handler>> _handlers = new Dictionary<uint, List<Handler>>();
        Dictionary<IPAddress, PeerStats> _peerStatistics = new Dictionary<IPAddress, PeerStats>();
        Dictionary<uint, MessageStats> _messageStatistics = new Dictionary<uint, MessageStats>();

        class Datagram
        {
            public byte[] data;
            public int length;
            public IPEndPoint endpoint;
            public Datagram(byte[] data, int length, IPEndPoint endpoint)
            {
                this.data = data;
                this.length = length;
                this.endpoint = endpoint;
            }
        }

        ConcurrentQueue<ReceiveData> _receiveQueue = new ConcurrentQueue<ReceiveData>();

        float _avgMessageDispatchTime;

        Queue<Action> _queueForMainThread = new Queue<Action>();
        Action[] _actionsForMainThread;

        UdpClient _udpClient;
        IPEndPoint _remoteEndpoint;
        IPEndPoint _localEndpoint;

        bool _run = true;
        bool _disposed = false;

        Thread _processThread;
        Thread _receiveThread;

        Semaphore _processSemaphore = new Semaphore(0, 1000);

        Utils.Forward _forward;
    }
}
