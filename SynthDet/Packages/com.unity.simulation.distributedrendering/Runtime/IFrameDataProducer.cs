using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unclassified.Net;
using Unity.Simulation.DistributedRendering;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

public interface IFrameDataProducer
{
    /// <summary>
    /// Initialize the data producer with initial connections or other required setup. Call during RuntimeInitializeOnLoad.
    /// </summary>
    void Initialize(NodeOptions options);

    /// <summary>
    /// React to application shutdown.
    /// </summary>
    void OnShutdown(SimulationStats stats);
    
    /// <summary>
    /// Consume the data provided.
    /// </summary>
    /// <param name="data"></param>
    void Consume(byte[] data);

    /// <summary>
    /// Provide condition to quit after the scene server is done producing frames.
    /// </summary>
    /// <returns></returns>
    bool ReadyToQuitAfterFramesProduced();
}
