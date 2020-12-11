using System;
using UnityEngine;

namespace Unity.Entities
{
#if UNITY_DISABLE_MANAGED_COMPONENTS
    [AttributeUsage(AttributeTargets.Struct)]
#else
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
#endif
    public class GenerateAuthoringComponentAttribute : Attribute
    {
    }

#if !UNITY_DOTSPLAYER
    [AttributeUsage(AttributeTargets.Field)]
    public class RestrictAuthoringInputToAttribute : PropertyAttribute
    {
        public Type Type { get; }

        public RestrictAuthoringInputToAttribute(Type type)
        {
            Type = type;
        }
    }
#endif

}