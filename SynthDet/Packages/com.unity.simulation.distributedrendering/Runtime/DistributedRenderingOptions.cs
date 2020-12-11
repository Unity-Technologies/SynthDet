using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public enum Mode
    {
        Physics,
        Render,
        None
    }
    public class DistributedRenderingOptions
    {
        public static Mode mode = Mode.None;
    }
}
