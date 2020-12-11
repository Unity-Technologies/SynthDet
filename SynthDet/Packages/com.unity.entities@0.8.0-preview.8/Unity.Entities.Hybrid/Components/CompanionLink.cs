#if !UNITY_DISABLE_MANAGED_COMPONENTS

using System;
using System.Diagnostics;
using UnityEngine;

namespace Unity.Entities
{
    class CompanionLink : IComponentData, IEquatable<CompanionLink>, IDisposable, ICloneable
    {
        static int s_CompanionNameUniqueId = 0;

        [Conditional("DOTS_HYBRID_COMPONENTS_DEBUG")]
        public static void SetCompanionName(Entity entity, GameObject gameObject)
        {
            gameObject.name = $"Companion of {entity} (UID {s_CompanionNameUniqueId += 1})";
        }

        public const HideFlags CompanionFlags =
#if !DOTS_HYBRID_COMPONENTS_DEBUG
            HideFlags.HideInHierarchy |
#endif
            HideFlags.NotEditable | HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.DontUnloadUnusedAsset;

        public GameObject Companion;

        public bool Equals(CompanionLink other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Companion, other.Companion);
        }

        public override int GetHashCode()
        {
            return ReferenceEquals(Companion, null) ? 0 : Companion.GetHashCode();
        }

        public void Dispose()
        {
            GameObject.DestroyImmediate(Companion);
        }

        public object Clone()
        {
            var cloned = new CompanionLink { Companion = GameObject.Instantiate(Companion) };
            cloned.Companion.hideFlags = CompanionFlags;
            return cloned;
        }
    }
}

#endif
