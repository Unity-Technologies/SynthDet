using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
{
    public static class GraphicsUtilities
    {
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

        public static bool SupportsRenderTextureFormat(GraphicsFormat format)
        {
            return SystemInfo.SupportsRenderTextureFormat(GraphicsFormatUtility.GetRenderTextureFormat(format));
        }

        public static bool SupportsAsyncReadback()
        {
            return CaptureOptions.useAsyncReadbackIfSupported && SystemInfo.supportsAsyncGPUReadback;
        }

        static Dictionary<GraphicsFormat, int> _blockSizeMap;

        [RuntimeInitializeOnLoadMethod]
        static void SetupAlternateGetBlockSize()
        {
            if (!CaptureOptions.useGetBlockSizeFromAnyThread)
            {
                _blockSizeMap = new Dictionary<GraphicsFormat, int>();
                foreach (GraphicsFormat format in Enum.GetValues(typeof(GraphicsFormat)))
                    _blockSizeMap[format] = (int)GraphicsFormatUtility.GetBlockSize(format);
            }
        }

        public static int GetBlockSize(GraphicsFormat format)
        {
            if (CaptureOptions.useGetBlockSizeFromAnyThread)
            {
                return (int)GraphicsFormatUtility.GetBlockSize(format);
            }
            else
            {
                if (!_blockSizeMap.ContainsKey(format))
                    throw new NotSupportedException("BlockSizeMap doesn't contain key for format");
                return _blockSizeMap[format];
            }
        }

        public static byte[] GetPixelsSlow(RenderTexture renderTexture)
        {
            var graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(renderTexture.format, false);
            var pixelSize = GraphicsUtilities.GetBlockSize(graphicsFormat);
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
                        var nominator = (1<<16)-1;
                        while (index < length)
                        {
                            shorts[index++] = (short)(nominator * input[si]);
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