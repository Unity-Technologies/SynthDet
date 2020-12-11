using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Unity.Entities
{
    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class BeginInitializationEntityCommandBufferSystem : EntityCommandBufferSystem {}

    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class EndInitializationEntityCommandBufferSystem : EntityCommandBufferSystem {}

    public class InitializationSystemGroup : ComponentSystemGroup
    {
        [Preserve] public InitializationSystemGroup() {}

        public override void SortSystemUpdateList()
        {
            // Extract list of systems to sort (excluding built-in systems that are inserted at fixed points)
            var toSort = new List<ComponentSystemBase>(m_systemsToUpdate.Count);
            BeginInitializationEntityCommandBufferSystem beginEcbSys = null;
            EndInitializationEntityCommandBufferSystem endEcbSys = null;
            foreach (var s in m_systemsToUpdate)
            {
                if (s is BeginInitializationEntityCommandBufferSystem) {
                    beginEcbSys = (BeginInitializationEntityCommandBufferSystem)s;
                } else if (s is EndInitializationEntityCommandBufferSystem) {
                    endEcbSys = (EndInitializationEntityCommandBufferSystem)s;
                } else {
                    toSort.Add(s);
                }
            }
            m_systemsToUpdate = toSort;
            base.SortSystemUpdateList();
            // Re-insert built-in systems to construct the final list
            var finalSystemList = new List<ComponentSystemBase>(toSort.Count);
            if (beginEcbSys != null)
                finalSystemList.Add(beginEcbSys);
            foreach (var s in m_systemsToUpdate)
                finalSystemList.Add(s);

            if (endEcbSys != null)
                finalSystemList.Add(endEcbSys);
            m_systemsToUpdate = finalSystemList;
        }
    }

    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class BeginSimulationEntityCommandBufferSystem : EntityCommandBufferSystem {}

    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class EndSimulationEntityCommandBufferSystem : EntityCommandBufferSystem {}

    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class LateSimulationSystemGroup : ComponentSystemGroup {}

    public class SimulationSystemGroup : ComponentSystemGroup
    {
        [Preserve] public SimulationSystemGroup() {}

        public override void SortSystemUpdateList()
        {
            // Extract list of systems to sort (excluding built-in systems that are inserted at fixed points)
            var toSort = new List<ComponentSystemBase>(m_systemsToUpdate.Count);
            BeginSimulationEntityCommandBufferSystem beginEcbSys = null;
            LateSimulationSystemGroup lateSysGroup = null;
            EndSimulationEntityCommandBufferSystem endEcbSys = null;
            foreach (var s in m_systemsToUpdate) {
                if (s is BeginSimulationEntityCommandBufferSystem) {
                    beginEcbSys = (BeginSimulationEntityCommandBufferSystem)s;
                } else if (s is LateSimulationSystemGroup) {
                    lateSysGroup = (LateSimulationSystemGroup)s;
                    lateSysGroup.SortSystemUpdateList(); // not handled by base-class sort call below
                } else if (s is EndSimulationEntityCommandBufferSystem) {
                    endEcbSys = (EndSimulationEntityCommandBufferSystem)s;
                } else {
                    toSort.Add(s);
                }
            }
            m_systemsToUpdate = toSort;
            base.SortSystemUpdateList();
            // Re-insert built-in systems to construct the final list
            var finalSystemList = new List<ComponentSystemBase>(toSort.Count);
            if (beginEcbSys != null)
                finalSystemList.Add(beginEcbSys);
            foreach (var s in m_systemsToUpdate)
                finalSystemList.Add(s);
            if (lateSysGroup != null)
                finalSystemList.Add(lateSysGroup);
            if (endEcbSys != null)
                finalSystemList.Add(endEcbSys);
            m_systemsToUpdate = finalSystemList;
        }
    }

    [UnityEngine.ExecuteAlways]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class BeginPresentationEntityCommandBufferSystem : EntityCommandBufferSystem {}

    public class PresentationSystemGroup : ComponentSystemGroup
    {
        [Preserve] public PresentationSystemGroup() {}

        public override void SortSystemUpdateList()
        {
            // Extract list of systems to sort (excluding built-in systems that are inserted at fixed points)
            var toSort = new List<ComponentSystemBase>(m_systemsToUpdate.Count);
            BeginPresentationEntityCommandBufferSystem beginEcbSys = null;
#pragma warning disable 0618
#pragma warning restore 0618
            foreach (var s in m_systemsToUpdate)
            {
                if (s is BeginPresentationEntityCommandBufferSystem)
                {
                    beginEcbSys = (BeginPresentationEntityCommandBufferSystem)s;
                } else {
                    toSort.Add(s);
                }
            }
            m_systemsToUpdate = toSort;
            base.SortSystemUpdateList();
            // Re-insert built-in systems to construct the final list
            var finalSystemList = new List<ComponentSystemBase>(toSort.Count);
            if (beginEcbSys != null)
                finalSystemList.Add(beginEcbSys);
            foreach (var s in m_systemsToUpdate)
                finalSystemList.Add(s);
            m_systemsToUpdate = finalSystemList;
        }
    }
}
