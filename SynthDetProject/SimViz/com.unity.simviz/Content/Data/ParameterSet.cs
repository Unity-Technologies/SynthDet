using UnityEngine;
using System;

namespace UnityEngine.SimViz.Scenarios
{
    public abstract class ParameterSet : ExpandableScriptableObject
    {
        [NonSerialized]
        public bool hasChanged = false;

        private void OnValidate()
        {
            hasChanged = true;
        }

        public abstract void ApplyParameters();
    }
}
