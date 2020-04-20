using System;

using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace Unity.AI.Simulation
{
    public static class CaptureEncodeJPG
    {
        public static Array Encode(Array data, int width, int height, GraphicsFormat format, bool flipY = true)
        {
#if DATA_CAPTURE_PROFILING
            Profiler.BeginSample("DataCapture.CaptureEncodeJPG");
#endif

            var encoded = JpegEncoder.Encode(ArrayUtilities.Cast<byte>(data), width, height, GraphicsUtilities.GetBlockSize(format), format, 75, flipY ? JpegEncoder.Flags.TJ_BOTTOMUP : JpegEncoder.Flags.NONE);

#if DATA_CAPTURE_PROFILING
            Profiler.EndSample();
#endif

            return encoded;
        }

        public static byte[] Decode(byte[] data, ref int width, ref int height)
        {
#if DATA_CAPTURE_PROFILING
            Profiler.BeginSample("DataCapture.CaptureEncodeJPG");
#endif

            var decoded = JpegEncoder.Decode(data, ref width, ref height);

#if DATA_CAPTURE_PROFILING
            Profiler.EndSample();
#endif

            return decoded;
        }
    }
}