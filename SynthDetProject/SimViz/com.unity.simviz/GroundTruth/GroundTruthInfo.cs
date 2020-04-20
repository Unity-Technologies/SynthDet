using Unity.Entities;

namespace UnityEngine.SimViz.Sensors
{
    public struct GroundTruthInfo : IComponentData
    {
        public uint instanceId;
    }
}
