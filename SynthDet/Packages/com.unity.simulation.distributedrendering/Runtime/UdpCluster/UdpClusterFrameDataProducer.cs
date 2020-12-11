#if SIMULATION_CLUSTERMANAGER_PRESENT

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unclassified.Net;
using Unity.Simulation.DistributedRendering;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

public class UdpClusterFrameDataProducer : UdpClusterSharedData, IFrameDataProducer
{
    public int framesSent = 0;
    public int queueLength = 0;
    
    private ConcurrentQueue<byte[]>      _outgoing = new ConcurrentQueue<byte[]>();
    private AsyncTcpListener             _listener = new AsyncTcpListener();
    private NodeOptions                  _nodeOptions;
    private ClusterTimer                 _clusterTimer;

#if !UNITY_SIMULATION_SPRAWL
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void RegisterWithFrameManager()
    {
        if (DistributedRenderingOptions.mode == Mode.Physics)
        {
            FrameManager.Instance.RegisterSceneServerDataProducer(new UdpClusterFrameDataProducer());
        }
    }
#endif
    
    public void Initialize(NodeOptions nodeOptions)
    {
        _nodeOptions = nodeOptions;
        _clusterTimer = GameObject.FindObjectOfType<ClusterTimer>();
        if (_clusterTimer == null)
        {
            Log.E("No Cluster Timer Component found.");
        }

        StartClusterDiscovery(_nodeOptions);
        StartNetwork();
    }

    async void StartNetwork()
    {
        _cluster.RegisterHandler((uint) MessageType.RequestServerId, true, AnnounceServerId);
        
        _listener = new AsyncTcpListener
        {
            IPAddress = IPAddress.IPv6Any,
            Port = _nodeOptions.udpClusterOptions.port,
            ClientConnectedCallback = tcpClient =>
                new AsyncTcpClient
                {
                    ServerTcpClient = tcpClient,
                    ConnectedCallback = OnConnect,
                    ReceivedCallback = OnReceivedDataFromClient,
                    ClosedCallback = OnDisconnect
                }.RunAsync(),
        };
        
        await _listener.RunAsync();
    }
    
    private void AnnounceServerId(Message message)
    {
        var node = GetClusterNodeFromMessage(message);

        _cluster.SendMessage(node, (uint) MessageType.AnnounceServerId);
        Log.I($"SERVER: Received AnnounceServerID from {node.address}");
    }

    public void Consume(byte[] data)
    {
        _outgoing.Enqueue(Util.GetBase64EncodedPayload(data));
    }

    public bool ReadyToQuitAfterFramesProduced()
    {
        return _outgoing.IsEmpty;
    }

    public void OnShutdown(SimulationStats stats)
    {
        //Report PhysicsNode metrics here..
        stats.clusterFPS = _clusterTimer.AverageTimePerUpdateIntervalMs;
        Log.I($"Physics Node done producing {stats.framesProcessed} frames. Stats: {JsonUtility.ToJson(stats)}");
    }

    public override void Dispose()
    {
        base.Dispose();

        _listener?.Stop(true);
        _listener = null;

        _outgoing = null;
        _nodeOptions = null;
        _clusterTimer = null;
    }

    private Task OnConnect(AsyncTcpClient client, bool isReconnected)
    {
        Log.I($"SERVER: Client connected: {client.IPAddress}:{client.Port}");

        return Task.CompletedTask;
    }

    private void OnDisconnect(AsyncTcpClient client, bool isRemote)
    {
        Log.I($"SERVER: Client disconnected {client.IPAddress}:{client.Port}");
    }

    private async Task OnReceivedDataFromClient(AsyncTcpClient serverClient, int count)
    {
        Log.I($"SERVER: Received {count} bytes from client {serverClient.IPAddress}:{serverClient.Port}");
        
        var messageBytes = await serverClient.ByteBuffer.DequeueAsync(count);
        var decodedMsg = Util.DecodeData(Encoding.UTF8.GetString(messageBytes));
        var message = (MessageType) BitConverter.ToUInt32(decodedMsg, 0);
        queueLength = _outgoing.Count;

        switch (message)
        {
            case MessageType.RequestFrame:
            {
                // send a frame to the client
                byte[] payload;
                if (_outgoing.TryDequeue(out payload))
                {
                    await serverClient.Send(new ArraySegment<byte>(payload));
                    Interlocked.Increment(ref framesSent);
                    Log.I("FramesSent : " + framesSent);
                }
                else
                {
                    var b0 = BitConverter.GetBytes((long) sizeof(MessageType));
                    var b1 = BitConverter.GetBytes((UInt32) MessageType.EndSimulation);
                    var bytes = new List<byte>();
                    bytes.AddRange(b0);
                    bytes.AddRange(b1);
                    var msg = Util.GetBase64EncodedPayload(bytes.ToArray());
                    Debug.Log("Sending End Simulation instruction");
                    await serverClient.Send(new ArraySegment<byte>(msg));
                }
  
                _clusterTimer.Tick();
                break;
            }

            default:
            {
                Log.E($"Unhandled message type {message}");
                break;
            }
        }
    }
}

#endif
