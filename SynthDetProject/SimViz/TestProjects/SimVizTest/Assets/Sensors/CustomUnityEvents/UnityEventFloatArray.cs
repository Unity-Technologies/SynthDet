using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Syncity
{
    [Serializable]
    public class UnityEventFloatArray : UnityEvent<NativeArray<float>> {}
}