using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public interface IFrameDataConsumer
    {
        /// <summary>
        /// Initialize the data consumer for the renderer
        /// </summary>
        void Initialize(NodeOptions options);

        /// <summary>
        /// React to application shutdown.
        /// </summary>
        void OnShutdown();

        /// <summary>
        /// Implementation for the FrameData request
        /// </summary>
        /// <returns>byte array of the data payload</returns>
        byte[] RequestFrame();
    }
}
