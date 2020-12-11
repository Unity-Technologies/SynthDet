using System;
using System.Collections;
using System.Diagnostics;

using UnityEditor;
using UnityEngine;

using NUnit.Framework;
using UnityEngine.TestTools;

namespace Unity.Simulation.DistributedRendering
{
    public class Tests
    {
        /*
            Test Ideas
            2 clusters with different secrets don't talk to eachother and vice versa.
            Broadcast works
            Sending to Node works
            Sending packet larger than 1024 fails
            Cluster ready works.
        */

        [UnityTest]
        public IEnumerator Test_ClusterTimesOutAfterTimeOut()
        {
            Log.level = Log.Level.All;

            var options = new ClusterOptions((ulong)UnityEngine.Random.Range(0, int.MaxValue), 2);
            options.identifyClusterTimeoutSec = 0.5f;

            bool timedOut = false;
            ClusterManager.StateChangedDelegate stateChangedHandler = (ClusterState state) =>
            {
                Assert.IsTrue(state == ClusterState.Discovering || state == ClusterState.TimedOut, $"Expected cluster state Discovering or TimedOut but got {state.ToString()}");
                if (state == ClusterState.TimedOut)
                    timedOut = true;
            };

            var cluster = new ClusterManager(options, stateChangedHandler);

            while (!timedOut)
                yield return null;

            cluster.StateChanged -= stateChangedHandler;
            cluster.Dispose();

            Assert.IsTrue(timedOut);
        }

        [UnityTest]
        public IEnumerator Test_ClusterReadyWorks()
        {
            const float kTimeoutInSeconds = 5.0f;

            Log.level = Log.Level.All;

            var options = new ClusterOptions((ulong)UnityEngine.Random.Range(0, int.MaxValue), 2);
            options.identifyClusterTimeoutSec = kTimeoutInSeconds;

            const int kNumberOfNodes = 5;

            var clusterReady         = new bool[kNumberOfNodes];
            var clusters             = new ClusterManager[kNumberOfNodes];
            var stateChangedHandlers = new ClusterManager.StateChangedDelegate[kNumberOfNodes];

            for (var i = 0; i < kNumberOfNodes; ++i)
            {
                var instance = i;
                clusterReady[i] = false;
                stateChangedHandlers[i] = (ClusterState state) =>
                {
                    Assert.True(instance < kNumberOfNodes, $"Expected less than {kNumberOfNodes} but got {instance}");
                    if (state == ClusterState.Ready)
                        clusterReady[instance] = true;
                };
                clusters[i] = new ClusterManager(options, stateChangedHandlers[i]);
            }

            Func<bool> allClustersReady = () =>
            {
                int count = 0;
                for (var i = 0; i < kNumberOfNodes; ++i)
                {
                    if (clusterReady[i])
                        ++count;
                }
                return count == kNumberOfNodes;
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stopwatch.Elapsed.TotalSeconds < kTimeoutInSeconds && !allClustersReady())
                yield return null;

            int readyCount = 0;
            for (var i = 0; i < kNumberOfNodes; ++i)
            {
                clusters[i].StateChanged -= stateChangedHandlers[i];
                clusters[i].Dispose();

                if (clusterReady[i])
                    ++readyCount;
            }

            Assert.True(readyCount == kNumberOfNodes, $"Expected {kNumberOfNodes} ready nodes, but got {readyCount}");
        }
    }
}
