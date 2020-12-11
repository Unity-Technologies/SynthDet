using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

using Unity.Simulation.DistributedRendering;
using UnityEngine.Assertions;

public class HelloWorld : MonoBehaviour
{
    public ClusterOptions _clusterOptions = new ClusterOptions((ulong)"HelloWorldSecret".GetHashCode(), kExpectedNumberOfNodes);
    ClusterManager        _cluster;

    // This is the total number of nodes we expect to be in the cluster.
    // This is inclusive of self, and the discovery will stop when it reaches this number of nodes.
    const int kExpectedNumberOfNodes = 2;
 
    uint kTestMessageId = (uint)"HelloWorld".GetHashCode();

    ClusterNode[] _nodes;

    double _elapsedTime;

    // Start is called before the first frame update
    void Start()
    {
        // For default options, you can just create options with a secret and the number of nodes you expect.
        //_clusterOptions 

        _cluster = new ClusterManager(_clusterOptions, (ClusterState state) =>
        {
            switch (state)
            {
                case ClusterState.Discovering:
                    Debug.Log("Cluster Discovery has begun.");
                    break;

                case ClusterState.Ready:
                    // This call is expensive, so cache it.
                    // There is a delegate for when nodes change if you need to know this.
                    _nodes = _cluster.Nodes;

                    Debug.Log("Cluster ready...");
                    foreach (var node in _nodes)
                        Debug.Log(node.ToString());
                    break;

                case ClusterState.TimedOut:
                    Debug.Log("Cluster Discovery timed out.");
                    break;

                case ClusterState.Disposed:
                    Debug.Log("Cluster has been disposed.");
                    break;

                default:
                    throw new InvalidEnumArgumentException($"Missing enum case for: {state.ToString()}");
            }
        });

        _cluster.RegisterHandler(kTestMessageId, true, (Message message) =>
        {
            Debug.Log("Got HelloWorld message");
            _cluster.SendMessage(_nodes[0], kTestMessageId);
        });
    }

    void Update()
    {
        if (_cluster.ClusterState == ClusterState.Ready)
        {
            _elapsedTime += Time.unscaledDeltaTime;

            if (_elapsedTime > 1)
            {
                _elapsedTime -= 1;

                _cluster.SendMessage(_nodes[0], kTestMessageId);
            }
        }
    }
}
