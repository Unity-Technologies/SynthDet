using System;
using UnityEngine;

namespace UnityEngine.SimViz
{
    public class EgoMarker : MonoBehaviour
    {
        public string Description;
        Ego m_Ego;

        public Ego Ego
        {
            get
            {
                EnsureEgoInitialized();
                return m_Ego;
            }
        }

        void Start()
        {
            EnsureEgoInitialized();
        }

        void EnsureEgoInitialized()
        {
            if (m_Ego == default)
                m_Ego = SimulationManager.RegisterEgo(Description);
        }
    }
}
