using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Simulation.DistributedRendering
{
    public static class Utils
    {
        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            MainThread = Thread.CurrentThread;
        }

        public static Thread MainThread;

        public static bool IsMainThread()
        {
            return Thread.CurrentThread == MainThread;
        }

        public static string InstanceToString(ulong instanceId)
        {
            uint uniqueId = (uint)(instanceId >> 32);
            return string.Format("{0}:{1}", uniqueId.ToString("X8"), (int)(instanceId & 0xffffffff));
        }

        public static uint UniqueIdFromInstanceId(ulong instanceId)
        {
            return (uint)(instanceId >> 32);
        }

        public static uint FourCC(string code)
        {
            Debug.Assert(code.Length == 4);
            return (uint)(code[0] | code[1] << 8 | code[2] << 16 | code[3] << 24);
        }

        public static string FourCCToString(uint value, string context = null)
        {
            return Encoding.UTF8.GetString(ToByteArray(ref value)) + (context != null ? context : "");
        }

        public static IPAddress LocalIPAddress()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address;
            }
        }

        public static byte[] ToByteArray<T>(ref T value) where T : struct
        {
            var size   = UnsafeUtility.SizeOf<T>();
            var array  = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, array, 0, size);
            Marshal.FreeHGlobal(ptr);
            return array;
        }

        public static byte[] ToByteArray<T>(T[] values) where T : struct
        {
            if (values.Length == 0)
                return null;
            var size   = UnsafeUtility.SizeOf<T>();
            var array  = new byte[size * values.Length];
            var offset = 0;
            for (var i = 0; i < values.Length; ++i)
            {
                Array.Copy(ToByteArray<T>(ref values[i]), 0, array, offset, size);
                offset += size;
            }
            return array;
        }

        public static void ByteArrayTo<T>(byte[] array, int offset, out T value) where T : struct
        {
            var size   = UnsafeUtility.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(array, offset, ptr, size);
            value = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);
        }

        unsafe public static void Write<T>(byte[] dest, int offset, ref T value) where T : struct
        {
            var size = UnsafeUtility.SizeOf<T>();
            Debug.Assert(offset >= 0 && offset <= dest.Length - size);
            var source = ToByteArray<T>(ref value);
            Array.Copy(source, 0, dest, offset, size);
        }

        unsafe public static void Write<T>(byte[] dest, int offset, T[] values) where T : struct
        {
            if (values.Length > 0)
            {
                var size = UnsafeUtility.SizeOf<T>();
                Debug.Assert(offset >= 0 && offset <= dest.Length - (values.Length * size));
                for (var i = 0; i < values.Length; ++i)
                {
                    var source = ToByteArray<T>(ref values[i]);
                    Array.Copy(source, 0, dest, offset, size);
                    offset += size;
                }
            }
        }

        public static void Read<T>(byte[] source, int offset, out T value) where T : struct
        {
            var size = UnsafeUtility.SizeOf<T>();
            Debug.Assert(offset >= 0 && offset <= source.Length - size);
            ByteArrayTo<T>(source, offset, out value);
        }

        public static void Read<T>(byte[] source, int offset, T[] values) where T : struct
        {
            if (values.Length > 0)
            {
                var size   = UnsafeUtility.SizeOf<T>();
                var length = values.Length;
                Debug.Assert(offset >= 0 && offset <= source.Length - size);
                Debug.Assert(length >= 0 && (source.Length - offset) / size >= length);
                for (var i = 0; i < length; ++i)
                {
                    ByteArrayTo<T>(source, offset, out values[i]);
                    offset += size;
                }
            }
        }

        public static string HexDump(byte[] data, int length, int columns = 8)
        {
            int index = 0;
            string line = "\n";
            string d = "";
            string v = "";
            while (index < length)
            {
                var b = data[index++];
                d += b.ToString("X2") + " ";
                v += (Char.IsLetterOrDigit((char)b) ? (char)b : '.') + " ";
                if (index % columns == 0)
                {
                    line += (d + "   " + v + "\n");
                    d = ""; v = "";
                }
            }
            return line;
        }

        public class Forward : MonoBehaviour
        {
            public delegate void ForwardDelegate();

            public ForwardDelegate startDelegate;
            public ForwardDelegate updateDelegate;
            public ForwardDelegate shutdownDelegate;

            void Awake()
            {
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
                EditorApplication.playModeStateChanged += (PlayModeStateChange state) =>
                {
                    if (state == PlayModeStateChange.ExitingPlayMode)
                    {
                        shutdownDelegate?.Invoke();
                    }
                };
#else
                Application.quitting += () =>
                {
                    shutdownDelegate?.Invoke();
                };
#endif
            }
            void Start()
            {
                startDelegate?.Invoke();
            }
            void LateUpdate()
            {
                updateDelegate?.Invoke();
            }
        }

        public static Forward CreateForward(string name, bool hidden = true)
        {
            return new GameObject(name).AddComponent<Forward>();
        }
    }
}
