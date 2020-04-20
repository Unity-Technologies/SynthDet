using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Syncity
{
    [Serializable]
    public class UnityEventVector3Array : UnityEvent<NativeArray<Vector3>> {}
}