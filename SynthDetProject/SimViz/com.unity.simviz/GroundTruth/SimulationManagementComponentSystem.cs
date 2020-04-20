using System;
using Unity.Entities;

namespace UnityEngine.SimViz
{
    class SimulationManagementComponentSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            SimulationManager.SimulationState?.Update();
        }
    }
}
