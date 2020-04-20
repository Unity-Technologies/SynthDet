using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{
    public static class GraphicsUtilities
    {
        /// <summary>
        /// Get the GraphicsFormat for the input  number of depth bits per pixel.
        /// </summary>
        /// <param name="depthBpp">Depth: Number of bits per pixel.</param>
        /// <returns>Graphics format for the corresponding number of depth bits per pixel.</returns>
        /// <exception cref="NotSupportedException"></exception>
        public static GraphicsFormat DepthFormatForDepth(int depthBpp)
        {
            switch (depthBpp)
            {
                case 16:
                    return GraphicsFormat.R16_UNorm;
                case 24:
                    return GraphicsFormat.R32_SFloat;
                case 32:
                    return GraphicsFormat.R32_SFloat;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Check if the given GraphicsFormat is supported by the current platform.
        /// </summary>
        /// <param name="format">Input Graphics format.</param>
        /// <returns>Boolean indicating if the graphics format is supported.</returns>
        public static bool SupportsRenderTextureFormat(GraphicsFormat format)
        {
            return SystemInfo.SupportsRenderTextureFormat(GraphicsFormatUtility.GetRenderTextureFormat(format));
        }

        /// <summary>
        /// Check if the AsyncReadback is supported by the current Graphics API.
        /// </summary>
        /// <returns>Returns a bool indicating if the asyncreadback is supported.</returns>
        public static bool SupportsAsyncReadback()
        {
            return CaptureOptions.useAsyncReadbackIfSupported && SystemInfo.supportsAsyncGPUReadback;
        }

        /// <summary>
        /// Perform the readback from the provided Render texture using ReadPixels API.
        /// </summary>
        /// <param name="renderTexture">Input source Render texture for the readback.</param>
        /// <returns>Returns a byte array of the RGB data retrieved from the readback.</returns>
        /// <exception cref="NotSupportedException"></exception>
        public static byte[] GetPixelsSlow(RenderTexture renderTexture)
        {
            var graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(renderTexture.format, false);
            var pixelSize = GraphicsFormatUtility.GetBlockSize(graphicsFormat);
            var channels = GraphicsFormatUtility.GetComponentCount(graphicsFormat);
            var channelSize = pixelSize / channels;
            var rect = new Rect(0, 0, renderTexture.width, renderTexture.height);

            // for RGB(A) we can just return the raw data.
            if (channels >= 3 && channels <= 4)
            {
                var texture = new Texture2D(renderTexture.width, renderTexture.height, graphicsFormat, TextureCreationFlags.None);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(rect, 0, 0);
                RenderTexture.active = null;
                var data = texture.GetRawTextureData();
                UnityEngine.Object.Destroy(texture);
                return data;
            }
            else
            {
                Debug.Assert(channels == 1, "Can only handle a single channel RT.");

                // Read pixels must be one of RGBA32, ARGB32, RGB24, RGBAFloat or RGBAHalf.
                // So R16 and RFloat will be converted to RGBAFloat.
                var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);

                RenderTexture.active = renderTexture;
                texture.ReadPixels(rect, 0, 0);
                RenderTexture.active = null;

                var length = renderTexture.width * renderTexture.height;
                var input  = ArrayUtilities.Cast<float>(texture.GetRawTextureData());
                UnityEngine.Object.Destroy(texture);

                Array output = null;

                int index = 0;
                switch (channelSize)
                {
                    case 2:
                        short[] shorts = ArrayUtilities.Allocate<short>(length);
                        var si = 0;
                        var numerator = (1<<16)-1;
                        while (index < length)
                        {
                            shorts[index++] = (short)(numerator * input[si]);
                            si += 4;
                        }
                        output = shorts;
                        break;
                    case 4:
                        float[] floats = ArrayUtilities.Allocate<float>(length);
                        var fi = 0;
                        while (index < length)
                        {
                            floats[index++] = input[fi];
                            fi += 4;
                        }
                        output = floats;
                        break;
                    default:
                        throw new NotSupportedException();
                }

                return ArrayUtilities.Cast<byte>(output);
            }
        }
    }
}