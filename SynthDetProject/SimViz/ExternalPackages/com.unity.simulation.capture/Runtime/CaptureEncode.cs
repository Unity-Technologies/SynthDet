using System;
using System.IO;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{
    public static class CaptureImageEncoder
    {
        /// <summary>
        /// An enum describing the image format
        /// </summary>
        public enum ImageFormat
        {
            Jpg,
            Tga,
            Raw,
            Png,
            Exr
        }

        /// <summary>
        /// Appends the provided file path with the imageFormat
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="imageFormat">Image format to be appended at the end of the file path</param>
        /// <returns>A string of filePath with extension.</returns>
        public static string EnforceFileExtension(string path, ImageFormat imageFormat)
        {
            var extension = "." + Enum.GetName(typeof(ImageFormat), imageFormat).ToLower();
            return Path.ChangeExtension(path, extension);
        }

        /// <summary>
        /// Encode the data with provided image format.
        /// </summary>
        /// <param name="data">Array of data to be encoded</param>
        /// <param name="width">Texture/Image width</param>
        /// <param name="height">Texture/Image height</param>
        /// <param name="format">Graphics Format used for the render texture</param>
        /// <param name="imageFormat">Format in which the data is to be encoded</param>
        /// <param name="flipY">Boolean flag indicating if the image needs to be flipped</param>
        /// <param name="additionalParam">Additional flags to be provided for image conversion (optional)</param>
        /// <returns></returns>
        [Obsolete("Encode supporting flipY has been deprecated. Use EncodeArray instead.")]
        public static Array Encode(Array data, int width, int height, GraphicsFormat format, ImageFormat imageFormat, bool flipY = true, int additionalParam = 0)
        {
            return EncodeArray(data, width, height, format, imageFormat, additionalParam);
        }

        [System.Obsolete("Decoding is no longer supported.")]
        public static byte[] Decode(byte[] data, ref int width, ref int height, ImageFormat imageFormat)
        {
            throw new NotSupportedException("Image decoding is not supported");
        }

        /// <summary>
        /// Encode the input data as per provided image format.
        /// </summary>
        /// <param name="data">An array of data to be encoded.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="format">Graphics format used by the render texture.</param>
        /// <param name="imageFormat">Format for encoding the data.</param>
        /// <param name="additionalParam">Additional flags to be passed for the encoding.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static Array EncodeArray(Array data, int width, int height, GraphicsFormat format, ImageFormat imageFormat, int additionalParam = 0)
        {
            switch (imageFormat)
            {
                case ImageFormat.Raw:
                    return data;
                case ImageFormat.Png:
                    return ImageConversion.EncodeArrayToPNG(data, format, (uint)width, (uint)height, 0);
                case ImageFormat.Exr:
                    return ImageConversion.EncodeArrayToEXR(data, format, (uint)width, (uint)height, 0, /*EXRFlags*/(Texture2D.EXRFlags)additionalParam);
                case ImageFormat.Jpg:
#if USIM_USE_BUILTIN_JPG_ENCODER
                    return ImageConversion.EncodeArrayToJPG(data, format, (uint)width, (uint)height, 0, /*quality*/additionalParam > 0 ? (int)additionalParam : 75);
#else
                    return JpegEncoder.Encode(ArrayUtilities.Cast<byte>(data), width, height, (int)GraphicsFormatUtility.GetBlockSize(format), format, /*quality*/additionalParam > 0 ? (int)additionalParam : 75);
#endif
                case ImageFormat.Tga:
                    return ImageConversion.EncodeArrayToTGA(data, format, (uint)width, (uint)height, 0);
                default:
                    throw new NotSupportedException("ImageFormat is not supported");
            }
        }
    }
}
