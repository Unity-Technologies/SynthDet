using System;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace Unity.AI.Simulation
{
    public static class CaptureEncodeTGA
    {
        public static Array Encode(Array data, int width, int height, GraphicsFormat format, bool flipY = true)
        {
#if DATA_CAPTURE_PROFILING
            Profiler.BeginSample("DataCapture.CaptureEncodeTGA");
#endif

            var encoded = ConvertBufferToTGABuffer(ArrayUtilities.Cast<byte>(data), width, height, format, flipY);

#if DATA_CAPTURE_PROFILING
            Profiler.EndSample();
#endif

            return encoded;
        }

        public static byte[] Decode(byte[] data, ref int width, ref int height)
        {
#if DATA_CAPTURE_PROFILING
            Profiler.BeginSample("DataCapture.CaptureEncodeTGA");
#endif

            var decoded = ConvertTGABufferToBuffer(data, ref width, ref height);

#if DATA_CAPTURE_PROFILING
            Profiler.EndSample();
#endif

            return decoded;
        }

        // TGA file format image types (https://en.wikipedia.org/wiki/Truevision_TGA#Header)
        enum TgaImageType
        {
            NoImageData,
            UncompressedColourMapped,
            UncompressedTrueColour,
            UncompressedGreyscale,
            RLEColourMapped = 9,
            RLETrueColour,
            RLEGreyscale
        }

#pragma warning disable CS0649
        // Header for TGA files (https://en.wikipedia.org/wiki/Truevision_TGA#Header)
        struct TgaImageHeader
        {
            public byte m_idLength;                   // The number of bytes that the image ID field consists of. The image ID field can contain any information, but it is common for it to contain the date and time the image was created or a serial number.As of version 2.0 of the TGA spec, the date and time the image was created is catered for in the extension area.
            public byte m_colourMapType;              // 0 = no colour map, 1 = colour map (i.e. paletted image)
            public byte m_imageType;                  // TgaImageType

            public byte m_firstColourMapIndexLsb;     // Index of first color map entry that is included in the file
            public byte m_firstColourMapIndexMsb;     // Index of first color map entry that is included in the file
            public byte m_colourMapLengthLsb;         // Number of entries of the color map that are included in the file
            public byte m_colourMapLengthMsb;         // Number of entries of the color map that are included in the file
            public byte m_colorMapEntrySize;          // Number of bits per pixel for colour map entries

            public ushort m_xOrigin;                   // X Absolute coordinate of lower - left corner for displays where origin is at the lower left
            public ushort m_yOrigin;                   // Y Absolute coordinate of lower - left corner for displays where origin is at the lower left
            public ushort m_width;                     // Image width in pixels
            public ushort m_height;                    // Image height in pixels
            public byte m_bpp;                        // Bits per pixel

            public byte m_AlphaDepthAndDirection;
            // struct
            // {
            //     UInt8 m_alphaChannelDepth   : 4;
            //     UInt8 m_direction           : 2;    // Screen destination of first pixel (0 = bottom-left, 1 = bottom-right, 2 = top-left, 3 = top-right)
            // };
        };
#pragma warning restore CS0649

        static byte[] ValueToBytes<T>(T value)
        {
            int size = Marshal.SizeOf(value);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        // TGA pixel format is BGR(A) so swizzle appropriately.
        static int EmitPixel(byte[] source, int sourceIndex, GraphicsFormat format, byte[] dest, int destIndex)
        {
            var pixelSize = GraphicsUtilities.GetBlockSize(format);
            switch (format)
            {
                case GraphicsFormat.R8_UNorm:
                    dest[destIndex] = source[sourceIndex];
                    break;
                    
                case GraphicsFormat.R8G8B8_UNorm:
                    dest[destIndex+0] = source[sourceIndex+2];
                    dest[destIndex+1] = source[sourceIndex+1];
                    dest[destIndex+2] = source[sourceIndex+0];
                    break;

                case GraphicsFormat.R8G8B8A8_UNorm:
                    dest[destIndex+0] = source[sourceIndex+2];
                    dest[destIndex+1] = source[sourceIndex+1];
                    dest[destIndex+2] = source[sourceIndex+0];
                    dest[destIndex+3] = source[sourceIndex+3];
                    break;

                case GraphicsFormat.R16_UNorm:
                case GraphicsFormat.R16G16_UNorm:
                case GraphicsFormat.B8G8R8A8_UNorm:
                case GraphicsFormat.R32_SFloat:
                case GraphicsFormat.R32G32_SFloat:
                case GraphicsFormat.R32G32B32A32_SFloat:
                    Array.Copy(source, sourceIndex, dest, destIndex, pixelSize);
                    break;

                default:
                    throw new NotSupportedException();
            }

            return (int)pixelSize;
        }

        static TgaImageType GetImageType(GraphicsFormat format)
        {
            switch (format)
            {
                case GraphicsFormat.R8_UNorm:
                case GraphicsFormat.R16_UNorm:
                case GraphicsFormat.R32_SFloat:
                    return TgaImageType.UncompressedGreyscale;
                    
                case GraphicsFormat.R8G8B8_UNorm:
                case GraphicsFormat.R8G8B8A8_UNorm:
                case GraphicsFormat.B8G8R8A8_UNorm:
                    return TgaImageType.UncompressedTrueColour;
                    
                default:
                    throw new NotSupportedException();
            }
        }

        static GraphicsFormat GetGraphicsFormat(TgaImageType type, int bpp)
        {
            switch (type)
            {
                case TgaImageType.UncompressedGreyscale:
                    {
                        switch (bpp)
                        {
                            case 8:
                                return GraphicsFormat.R8_UNorm;
                            case 16:
                                return GraphicsFormat.R16_UNorm;
                            case 32:
                                return GraphicsFormat.R32_SFloat;
                            default:
                                throw new NotSupportedException("TGA image greyscale bpp not supported.");
                        }
                    }

                case TgaImageType.UncompressedTrueColour:
                    {
                        switch (bpp)
                        {
                            case 24:
                                return GraphicsFormat.R8G8B8_UNorm;
                            case 32:
                                return GraphicsFormat.R8G8B8A8_UNorm;
                            default:
                                throw new NotSupportedException("TGA image color bpp not supported.");
                        }
                    }

                default:
                    throw new NotSupportedException("TGA image type not supported.");
            }
        }

        public static byte[] ConvertBufferToTGABuffer(byte[] sourceImage, int width, int height, GraphicsFormat format, bool flipY)
        {
            Debug.Assert(sourceImage != null);
            Debug.Assert(width > 0 && height > 0);

            int pixelSize = GraphicsUtilities.GetBlockSize(format);
            int length = width * height * pixelSize;
            int imageLen = ArrayUtilities.Count<byte>(sourceImage);
            Debug.Assert(length == imageLen, "length and imageLen must be equal.");

            TgaImageHeader tgaImageHeader = default(TgaImageHeader);
            
            int headerSize = Marshal.SizeOf(typeof(TgaImageHeader));
            byte[] destImage = new byte[headerSize + width * height * pixelSize];

            tgaImageHeader.m_imageType = (byte)GetImageType(format);
            tgaImageHeader.m_width  = (ushort)width;
            tgaImageHeader.m_height = (ushort)height;
            tgaImageHeader.m_bpp    = (byte)(pixelSize * 8);

            int sourceIndex = 0;
            int destIndex = 0;
            int index = 0;

            int direction = flipY ? -1 : 1;
            if (direction < 0)
                sourceIndex += (length - pixelSize); 

            // Obligatory endianess warning.
            // Note: At this point we may need to deal with endianess issues, 
            // but at the moment, there are no big endian platforms we support.

            Array.Copy(ValueToBytes(tgaImageHeader), 0, destImage, destIndex, headerSize);
            destIndex += headerSize;

            while (index < length)
            {
                var bytes = EmitPixel(sourceImage, sourceIndex, format, destImage, destIndex);
                index += bytes;
                sourceIndex += (direction * bytes);
                destIndex += bytes;
            }

            return destImage;
        }

        public static byte[] ConvertTGABufferToBuffer(byte[] tgaImage, ref int width, ref int height)
        {
            Debug.Assert(tgaImage != null, "Input tgaImage cannot be null");

            int headerSize = Marshal.SizeOf(typeof(TgaImageHeader));

            IntPtr temp = Marshal.AllocHGlobal(headerSize);
            Marshal.Copy(tgaImage, 0, temp, headerSize);
            var tga = Marshal.PtrToStructure<TgaImageHeader>(temp);

            // Obligatory endianess warning.
            // Note: At this point we may need to deal with endianess issues, 
            // but at the moment, there are no big endian platforms we support.

            width = tga.m_width;
            height = tga.m_height;

            var length = width * height * tga.m_bpp / 8;
            Debug.Assert(length + headerSize == tgaImage.Length);

            var data = new byte[length];

            int sourceIndex = headerSize;
            int destIndex = 0;

            while (destIndex < length)
            {
                var bytes = EmitPixel(tgaImage, sourceIndex, GetGraphicsFormat((TgaImageType)tga.m_imageType, tga.m_bpp), data, destIndex);
                sourceIndex += bytes;
                destIndex += bytes;
            }

            return data;
        }
    }
}