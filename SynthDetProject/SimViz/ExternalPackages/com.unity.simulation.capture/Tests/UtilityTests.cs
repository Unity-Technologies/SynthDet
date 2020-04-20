using System.Runtime.InteropServices;

using UnityEngine;

using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;

public class UtilityTests
{
    [Test]
    public void ArrayCast_CastingToOtherArrayTypesDoesNotCopy()
    {
        foo();
    }

    void foo()
    {
        const int kElementCount = 20000;

        var byteArray    = new byte[kElementCount];
        var intArray     = ArrayUtilities.Cast<int>(byteArray);
        var floatArray   = ArrayUtilities.Cast<float>(byteArray);
        var colorArray   = ArrayUtilities.Cast<Color>(byteArray);
        var color32Array = ArrayUtilities.Cast<Color32>(byteArray);
        var vector2Array = ArrayUtilities.Cast<Vector2>(byteArray);
        var vector3Array = ArrayUtilities.Cast<Vector3>(byteArray);
        var vector4Array = ArrayUtilities.Cast<Vector4>(byteArray);

        Debug.Assert(Object.ReferenceEquals(byteArray, intArray));
        Debug.Assert(Object.ReferenceEquals(intArray, colorArray));
        Debug.Assert(Object.ReferenceEquals(colorArray, color32Array));
        Debug.Assert(Object.ReferenceEquals(color32Array, vector2Array));
        Debug.Assert(Object.ReferenceEquals(vector2Array, vector3Array));
        Debug.Assert(Object.ReferenceEquals(vector3Array, vector4Array));

        Debug.Assert(ArrayUtilities.Count<float>(floatArray)     == kElementCount / sizeof(float));
        Debug.Assert(ArrayUtilities.Count<int>(intArray)         == kElementCount / sizeof(int));
        Debug.Assert(ArrayUtilities.Count<Color>(colorArray)     == kElementCount / Marshal.SizeOf(typeof(Color)));
        Debug.Assert(ArrayUtilities.Count<Color32>(color32Array) == kElementCount / Marshal.SizeOf(typeof(Color32)));
        Debug.Assert(ArrayUtilities.Count<Vector2>(vector2Array) == kElementCount / Marshal.SizeOf(typeof(Vector2)));
        Debug.Assert(ArrayUtilities.Count<Vector3>(vector3Array) == kElementCount / Marshal.SizeOf(typeof(Vector3)));
        Debug.Assert(ArrayUtilities.Count<Vector4>(vector4Array) == kElementCount / Marshal.SizeOf(typeof(Vector4)));
    }
}
