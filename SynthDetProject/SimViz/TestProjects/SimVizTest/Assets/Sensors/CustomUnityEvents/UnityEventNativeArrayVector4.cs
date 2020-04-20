using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Syncity
{
    [Serializable]
    public class UnityEventVector4Array : UnityEvent<NativeArray<Vector4>> {}
}