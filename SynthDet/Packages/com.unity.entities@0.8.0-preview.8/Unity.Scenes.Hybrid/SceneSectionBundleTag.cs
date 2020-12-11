using System;
using Unity.Entities;

namespace Unity.Scenes
{
    [Serializable]
    internal struct SceneSectionBundle : ISharedComponentData, IEquatable<SceneSectionBundle>, IRefCounted
    {
        private SceneBundleHandle _sceneBundleHandle;

        public SceneSectionBundle(SceneBundleHandle bundle)
        {
            _sceneBundleHandle = bundle;
        }

        public void Release()
        {
            _sceneBundleHandle?.Release();
            _sceneBundleHandle = null;
        }

        public void Retain()
        {
            _sceneBundleHandle?.Retain();
        }

        public bool Equals(SceneSectionBundle other)
        {
            return _sceneBundleHandle == other._sceneBundleHandle;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (!ReferenceEquals(_sceneBundleHandle, null)) hash ^= _sceneBundleHandle.GetHashCode();
            return hash;
        }
    }
}