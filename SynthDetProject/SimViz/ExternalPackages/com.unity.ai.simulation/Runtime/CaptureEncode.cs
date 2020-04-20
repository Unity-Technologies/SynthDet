using System;
using System.IO;

using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
{
    public static class CaptureImageEncoder
    {
        public enum ImageFormat
        {
            Jpg,
            Tga,
            Raw,
        }

        public static string EnforceFileExtension(string path, ImageFormat imageFormat)
        {
            var extension = "." + Enum.GetName(typeof(ImageFormat), imageFormat).ToLower();
            return Path.ChangeExtension(path, extension);
        }

        public static Array Encode(Array data, int width, int height, GraphicsFormat format, ImageFormat imageFormat, bool flipY = true)
        {
            switch (imageFormat)
            {
                case ImageFormat.Raw:
                    return data;
                case ImageFormat.Jpg:
                    return CaptureEncodeJPG.Encode(data, width, height, format, flipY);
                case ImageFormat.Tga:
                    return CaptureEncodeTGA.Encode(data, width, height, format, flipY);
                default:
                    throw new NotSupportedException("ImageFormat is not supported");
            }
        }

        public static byte[] Decode(byte[] data, ref int width, ref int height, ImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case ImageFormat.Raw:
                    return data;
                case ImageFormat.Jpg:
                    return CaptureEncodeJPG.Decode(data, ref width, ref height);
                case ImageFormat.Tga:
                    return CaptureEncodeTGA.Decode(data, ref width, ref height);
                default:
                    throw new NotSupportedException("ImageFormat is not supported");
            }
        }
    }
}