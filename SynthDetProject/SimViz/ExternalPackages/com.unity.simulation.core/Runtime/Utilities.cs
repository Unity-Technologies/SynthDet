using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Utility class that determines if the app is running under the Test Runner.
    /// </summary>
    public static class TestUtility
    {
        public static bool IsAutomatedTestRun()
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "-runTests") >= 0;
        }
    }

    /// <summary>
    /// Union class for converting Array types from one type to another without copying.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ArrayCast
    {
        [FieldOffset(0)]
        Array _a;

        [FieldOffset(0)]
        byte[] _byte;
        public byte[] ToByte(Array a) { _a = a; return _byte; }

        [FieldOffset(0)]
        short[] _short;
        public short[] ToShort(Array a) { _a = a; return _short; }

        [FieldOffset(0)]
        int[] _int;
        public int[] ToInt(Array a) { _a = a; return _int; }

        [FieldOffset(0)]
        float[] _float;
        public float[] ToFloat(Array a) { _a = a; return _float; }

        [FieldOffset(0)]
        Color[] _color;
        public Color[] ToColor(Array a) { _a = a; return _color; }

        [FieldOffset(0)]
        Color32[] _color32;
        public Color32[] ToColor32(Array a) { _a = a; return _color32; }

        [FieldOffset(0)]
        Vector2[] _vector2;
        public Vector2[] ToVector2(Array a) { _a = a; return _vector2; }

        [FieldOffset(0)]
        Vector3[] _vector3;
        public Vector3[] ToVector3(Array a) { _a = a; return _vector3; }

        [FieldOffset(0)]
        Vector4[] _vector4;
        public Vector4[] ToVector4(Array a) { _a = a; return _vector4; }
    }

    /// <summary>
    /// Utility class for converting Array types from one type to another without copying.
    /// </summary>
    public static class ArrayUtilities
    {
        delegate T[] CastDelegate<T>(Array a);

        static readonly Dictionary<Type, Delegate> _castMap = new Dictionary<Type, Delegate>()
        {
            { typeof(byte),    new CastDelegate<byte>   (Caster.ToByte)    },
            { typeof(short),   new CastDelegate<short>  (Caster.ToShort)   },
            { typeof(int),     new CastDelegate<int>    (Caster.ToInt)     },
            { typeof(float),   new CastDelegate<float>  (Caster.ToFloat)   },
            { typeof(Color),   new CastDelegate<Color>  (Caster.ToColor)   },
            { typeof(Color32), new CastDelegate<Color32>(Caster.ToColor32) },
            { typeof(Vector2), new CastDelegate<Vector2>(Caster.ToVector2) },
            { typeof(Vector3), new CastDelegate<Vector3>(Caster.ToVector3) },
            { typeof(Vector4), new CastDelegate<Vector4>(Caster.ToVector4) },
        };

        static ArrayCast Caster = new ArrayCast();

        /// <summary>
        /// Casts an input array to type T if supported by the cast map.
        /// </summary>
        /// <param name="array">Input array to be casted.</param>
        /// <typeparam name="T">Cast type.</typeparam>
        /// <returns>Array of type T.</returns>
        public static T[] Cast<T>(Array array)
        {
            Debug.Assert(_castMap.ContainsKey(typeof(T)), "Array cast map doesn't contain a caster for type " + typeof(T).ToString());
            var d = _castMap[typeof(T)] as CastDelegate<T>;
            return d(array);
        }

        /// <summary>
        /// Returns actual count of a casted array.
        /// </summary>
        /// <param name="array">Input casted array</param>
        /// <typeparam name="T">Cast type.</typeparam>
        /// <returns>Actual count of a casted array</returns>
        public static int Count<T>(T[] array)
        {
            var orgEleSize = Marshal.SizeOf(array.GetType().GetElementType());
            var newEleSize = Marshal.SizeOf(typeof(T));
            return orgEleSize > newEleSize ? array.Length * (orgEleSize / newEleSize) : array.Length / (newEleSize / orgEleSize);
        }

        // 
        // reason being that the length will be enforced when casted, so best to always use byte[]
        /// <summary>
        /// Helper method to ensure you always allocate arrays as byte[]
        /// </summary>
        /// <param name="length">Length of the byte array.</param>
        /// <typeparam name="T">Cast type.</typeparam>
        /// <returns>Casted array.</returns>
        public static T[] Allocate<T>(int length)
        {
            return Cast<T>(new byte[length * Marshal.SizeOf(typeof(T))]);
        }
    }
}
