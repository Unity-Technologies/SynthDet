namespace Unity.Simulation.DistributedRendering
{

    /// <summary>
    /// Enumeration for the state of the cluster.
    /// This is used to represent state internally, as well as in callbacks to the developer.
    /// </summary>
    public enum ClusterState
    {
        /// <summary>
        /// None indicates that the cluster state has not be specified.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the cluster is currently in discovery mode.
        /// </summary>
        Discovering,

        /// <summary>
        /// Indicates that the cluster is ready and available for communications.
        /// </summary>
        Ready,

        /// <summary>
        /// Indicates that the cluster timed out while trying to discover available nodes.
        /// </summary>
        TimedOut,

        /// <summary>
        /// Indicates that the cluster has been disposed.
        /// </summary>
        Disposed,
    }
}
