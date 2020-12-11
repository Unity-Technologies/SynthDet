using System;

namespace Unity.Simulation.DistributedRendering
{
    /// <summary>
    /// Options class for configuring cluster options.
    /// </summary>
    [Serializable]
    public class ClusterOptions
    {
        /// <summary>
        // The cluster will wait until the expected number of nodes identify themselves before signalling ready.
        // This number is inclusive of the self node, i.e. total nodes you expect including yourself.
        /// </summary>
        public int    expectedNodes;

        /// <summary>
        // Cluster secret to prevent foreign nodes on the same network from interfering. 
        /// </summary>
        public ulong  secret;

        /// <summary>
        /// The port to use when listening for network traffic.
        /// </summary>
        public ushort port;

        /// <summary>
        /// When sending messages, the message will timeout after this time, if not acknowledged.
        /// </summary>
        public float  sendMessageTimeoutSec;

        /// <summary>
        /// The time between retrying a message that has not been acknowledged.
        /// </summary>
        public float  messageAckRetryTimeoutSec;

        /// <summary>
        /// The time to spend waiting for nodes in the cluster to identify themselves before timing out.
        /// </summary>
        public float  identifyClusterTimeoutSec;

        /// <summary>
        /// The amount of time to sleep before waking up to process message retries.
        /// </summary>
        public float  resendThreadSleepTimeSec;

        /// <summary>
        /// Constructor for creating cluster options with a secret and the number of expected nodes.
        /// This is the most common use case, and the other options can be omitted.
        /// </summary>
        public ClusterOptions(ulong secret, int expectedNodes = 0)
            : this()
        {
            this.secret = secret;
            this.expectedNodes = expectedNodes;
        }

        /// <summary>
        /// Base constructor for creating cluster options.
        /// </summary>
        public ClusterOptions()
        {
            secret                      = 1234;
            expectedNodes               = 1;
            port                        = 58085;
            sendMessageTimeoutSec       = .2f;
            messageAckRetryTimeoutSec   = .1f;
            identifyClusterTimeoutSec   = 20;
            resendThreadSleepTimeSec    = .5f;
        }
    }
}