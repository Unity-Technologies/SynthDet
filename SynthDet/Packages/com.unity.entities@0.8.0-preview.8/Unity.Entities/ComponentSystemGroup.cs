using System;
using System.Collections.Generic;
using Unity.Assertions;
using Unity.Core;
#if !NET_DOTS
using System.Linq;
#endif

namespace Unity.Entities
{

    public abstract class ComponentSystemGroup : ComponentSystem
    {
        private bool m_systemSortDirty = false;
        protected List<ComponentSystemBase> m_systemsToUpdate = new List<ComponentSystemBase>();
        protected List<ComponentSystemBase> m_systemsToRemove = new List<ComponentSystemBase>();

        public virtual IEnumerable<ComponentSystemBase> Systems => m_systemsToUpdate;

        public void AddSystemToUpdateList(ComponentSystemBase sys)
        {
            if (sys != null)
            {
                if (this == sys)
                    throw new ArgumentException($"Can't add {TypeManager.GetSystemName(GetType())} to its own update list");

                // Check for duplicate Systems. Also see issue #1792
                if (m_systemsToUpdate.IndexOf(sys) >= 0)
                    return;

                m_systemsToUpdate.Add(sys);
                m_systemSortDirty = true;
            }
        }

        public void RemoveSystemFromUpdateList(ComponentSystemBase sys)
        {
            m_systemSortDirty = true;
            m_systemsToRemove.Add(sys);
        }

        public virtual void SortSystemUpdateList()
        {
            if (!m_systemSortDirty)
                return;
            m_systemSortDirty = false;

            if (m_systemsToRemove.Count > 0)
            {
                foreach (var sys in m_systemsToRemove)
                    m_systemsToUpdate.Remove(sys);
                m_systemsToRemove.Clear();
            }
            
            foreach (var sys in m_systemsToUpdate)
            {
                if (TypeManager.IsSystemAGroup(sys.GetType()))
                {
                    ((ComponentSystemGroup)sys).SortSystemUpdateList();
                }
            }

            ComponentSystemSorter.Sort(m_systemsToUpdate, x => x.GetType(), this.GetType());
        }

#if UNITY_DOTSPLAYER
        public void RecursiveLogToConsole()
        {
            foreach (var sys in m_systemsToUpdate)
            {
                if (sys is ComponentSystemGroup)
                {
                    (sys as ComponentSystemGroup).RecursiveLogToConsole();
                }

                var name = TypeManager.GetSystemName(sys.GetType());
                Debug.Log(name);
            }
        }

#endif

        protected override void OnStopRunning()
        {
        }

        internal override void OnStopRunningInternal()
        {
            OnStopRunning();

            foreach (var sys in m_systemsToUpdate)
            {
                if ((sys == null) || (!sys.m_PreviouslyEnabled)) continue;

                sys.m_PreviouslyEnabled = false;
                sys.OnStopRunningInternal();
            }
        }

        /// <summary>
        /// An optional callback.  If set, this group's systems will be updated in a loop while
        /// this callback returns true.  This can be used to implement custom processing before/after
        /// update (first call should return true, second should return false), or to run a group's
        /// systems multiple times (return true more than once).
        /// 
        /// The group is passed as the first parameter.
        /// </summary>
        public Func<ComponentSystemGroup, bool> UpdateCallback;
        
        protected override void OnUpdate()
        {
            if (UpdateCallback == null)
            {
                UpdateAllSystems();
            }
            else
            {
                while (UpdateCallback(this))
                {
                    UpdateAllSystems();
                }
            }
        }

        void UpdateAllSystems()
        {
            if (m_systemSortDirty)
                SortSystemUpdateList();

            foreach (var sys in m_systemsToUpdate)
            {
                try
                {
                    sys.Update();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
#if UNITY_DOTSPLAYER
                    // When in a DOTS Runtime build, throw this upstream -- continuing after silently eating an exception
                    // is not what you'll want, except maybe once we have LiveLink.  If you're looking at this code
                    // because your LiveLink dots runtime build is exiting when you don't want it to, feel free
                    // to remove this block, or guard it with something to indicate the player is not for live link.
                    throw;
#endif
                }

                if (World.QuitUpdate)
                    break;
            }
        }
    }
}
