#if SIMULATION_CLUSTERMANAGER_PRESENT

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unclassified.Net;
using Unity.Simulation.DistributedRendering;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;
using UnityEngine.Assertions;

public class UdpClusterFrameDataConsumer : UdpClusterSharedData, IFrameDataConsumer
{
    private AsyncTcpClient _client;
    private ConcurrentQueue<byte[]> _frameData = new ConcurrentQueue<byte[]>();
    private bool _isConnected;
    private NodeOptions _nodeOptions;

#if !UNITY_SIMULATION_SPRAWL
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void RegisterWithFrameManager()
    {
        if (DistributedRenderingOptions.mode == Mode.Render)
        {
            Log.I("Registering consumer..");
            FrameManager.Instance.RegisterRenderNodeDataConsumer(new UdpClusterFrameDataConsumer());
        }
    }
#endif
    
    public void Initialize(NodeOptions options)
    {
        _nodeOptions = options;

        StartClusterDiscovery(_nodeOptions);

        _cluster.StateChanged = ClusterStateChanged;
        _cluster.RegisterHandler((uint)MessageType.AnnounceServerId, true, ProcessUdpMessage);
    }

    public void OnShutdown()
    {
        Dispose();

        _client?.Dispose();
        _client = null;

        _frameData = null;
        _nodeOptions = null;
    }

    public byte[] RequestFrame()
    {
        if (_cluster == null || _cluster.ClusterState != ClusterState.Ready)
        {
            //Log.I("Cluster is null: " + (_cluster == null)  + " ClusterState: " +_cluster.ClusterState);
            return null;
        }

        if (_client == null || !_isConnected)
        {
            Log.I("Sending message, multicastclusternode");
            _cluster.SendMessage(_cluster.MulticastClusterNode, (uint)MessageType.RequestServerId);
            return null;
        }

        if (_frameData.Count == 0)
        {
            Log.I("Requesting Frame from the server..");
            SendMessage(MessageType.RequestFrame);
        }
        else
        {
            Log.I("Dequeuing frameData from _frameData");
            byte[] data;
            if (_frameData.TryDequeue(out data))
            {
                var encodedData = Encoding.UTF8.GetString(data);
                Log.I("Decoding framedata");
                data = Util.DecodeData(encodedData);
                return data;
            }
        }
        return null;
    }
    
    private void ClusterStateChanged(ClusterState clusterState)
    {
    }
    
    private void ProcessUdpMessage(Message message)
    {
        var msg = (MessageType) message.messageId;
        var node = GetClusterNodeFromMessage(message);

        switch (msg)
        {
            case MessageType.AnnounceServerId:
                // open a TCP connection to the server.
                StartNetwork(node.address, _nodeOptions.udpClusterOptions.port);
                break;

            default:
                Log.E("CLIENT: UDP Unhandled message type: " + msg.ToString());
                break;
        }
    }
    
    private async void StartNetwork(IPAddress address, int port)
    {
        // TODO: should make sure this is coming from the server
        // to which we're already connected. Otherwise, indicates
        // that two server instances have been launched, which is
        // bad.
        if (null != _client)
        {
            return;
        }

        _client = new AsyncTcpClient()
        {
            IPAddress = address,
            Port = port,
            ClosedCallback = OnDisconnect,
            ConnectedCallback = OnConnect,
            ReceivedCallback = OnReceive,
        };

        Log.I("Running async tcp client");
        await _client.RunAsync();
    }
    
    private void OnDisconnect(AsyncTcpClient thisClient, bool isRemote)
    {
        Log.I("CLIENT OnDisconnect.");
        Assert.AreEqual(_client, thisClient);

        _client?.Dispose();
        _client = null;
        _isConnected = false;
    }

    private Task OnReceive(AsyncTcpClient thisClient, int count)
    {
        Log.I($"CLIENT OnReceive {count} bytes");

        _frameData.Enqueue(thisClient.ByteBuffer.Dequeue(count));

        Log.I("CLIENT OnReceive complete\n");

        return Task.CompletedTask;
    }

    private Task OnConnect(AsyncTcpClient thisClient, bool isReconnected)
    {
        Log.I($"CLIENT OnConnect {thisClient.IPAddress}, isReconnected: {isReconnected}");
        _isConnected = true;
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Sends a message to the IP cluster.
    /// NOTE: This doesn't send message length first!!!!
    /// </summary>
    /// <param name="msg"></param>
    private async void SendMessage(MessageType msg)
    {
        var msgBytes = BitConverter.GetBytes((UInt32)msg);
        var payload = Util.GetBase64EncodedPayload(msgBytes);
        await _client.Send(new ArraySegment<byte>(payload));
    }
    
    private MessageType PeekMessage()
    {
        if (_client.ByteBuffer.Count < sizeof(MessageType))
        {
            return MessageType.Unknown;
        }

        var bytes = _client.ByteBuffer.Peek(sizeof(MessageType));
        var message = (MessageType) BitConverter.ToUInt32(bytes, 0);

        return message;
    }

    private MessageType ReadMessage()
    {
        if (_client.ByteBuffer.Count < sizeof(MessageType))
        {
            return MessageType.Unknown;
        }

        var bytes = _client.ByteBuffer.Dequeue(sizeof(MessageType));
        var msg = (MessageType)BitConverter.ToUInt32(bytes, 0);

        return msg;
    }
}

#endif
