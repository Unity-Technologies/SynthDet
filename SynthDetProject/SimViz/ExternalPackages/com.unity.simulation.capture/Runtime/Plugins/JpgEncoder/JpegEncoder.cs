using System;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class JpegEncoder 
{
	public enum SubSample 
	{
		TJ_444=0, 
		TJ_422, 
		TJ_420, 
		TJ_GRAYSCALE
	};

	[Flags]
	public enum Flags
	{
		NONE = 0,

		TJ_BGR = 1,
		/* The components of each pixel in the source/destination bitmap are stored
			in B,G,R order, not R,G,B */
		TJ_BOTTOMUP = 2,
		/* The source/destination bitmap is stored in bottom-up (Windows, OpenGL)
			order, not top-down (X11) order */
		TJ_FORCEMMX = 8,
		/* Turn off CPU auto-detection and force TurboJPEG to use MMX code
			(IPP and 32-bit libjpeg-turbo versions only) */
		TJ_FORCESSE = 16,
		/* Turn off CPU auto-detection and force TurboJPEG to use SSE code
			(32-bit IPP and 32-bit libjpeg-turbo versions only) */
		TJ_FORCESSE2 = 32,
		/* Turn off CPU auto-detection and force TurboJPEG to use SSE2 code
			(32-bit IPP and 32-bit libjpeg-turbo versions only) */
		TJ_ALPHAFIRST = 64,
		/* If the source/destination bitmap is 32 bpp, assume that each pixel is
			ARGB/XRGB (or ABGR/XBGR if TJ_BGR is also specified) */
		TJ_FORCESSE3 = 128,
		/* Turn off CPU auto-detection and force TurboJPEG to use SSE3 code
			(64-bit IPP version only) */
		TJ_FASTUPSAMPLE = 256,
		TJFLAG_NOREALLOC = 1024
	}

	[Flags]
	public enum PixelFormat
	{
		TJPF_INVALID = -1,
		/**
		* RGB pixel format.  The red, green, and blue components in the image are
		* stored in 3-byte pixels in the order R, G, B from lowest to highest byte
		* address within each pixel.
		*/
		TJPF_RGB = 0,
		/**
		* BGR pixel format.  The red, green, and blue components in the image are
		* stored in 3-byte pixels in the order B, G, R from lowest to highest byte
		* address within each pixel.
		*/
		TJPF_BGR,
		/**
		* RGBX pixel format.  The red, green, and blue components in the image are
		* stored in 4-byte pixels in the order R, G, B from lowest to highest byte
		* address within each pixel.  The X component is ignored when compressing
		* and undefined when decompressing.
		*/
		TJPF_RGBX,
		/**
		* BGRX pixel format.  The red, green, and blue components in the image are
		* stored in 4-byte pixels in the order B, G, R from lowest to highest byte
		* address within each pixel.  The X component is ignored when compressing
		* and undefined when decompressing.
		*/
		TJPF_BGRX,
		/**
		* XBGR pixel format.  The red, green, and blue components in the image are
		* stored in 4-byte pixels in the order R, G, B from highest to lowest byte
		* address within each pixel.  The X component is ignored when compressing
		* and undefined when decompressing.
		*/
		TJPF_XBGR,
		/**
		* XRGB pixel format.  The red, green, and blue components in the image are
		* stored in 4-byte pixels in the order B, G, R from highest to lowest byte
		* address within each pixel.  The X component is ignored when compressing
		* and undefined when decompressing.
		*/
		TJPF_XRGB,
		/**
		* Grayscale pixel format.  Each 1-byte pixel represents a luminance
		* (brightness) level from 0 to 255.
		*/
		TJPF_GRAY,
		/**
		* RGBA pixel format.  This is the same as @ref TJPF_RGBX, except that when
		* decompressing, the X component is guaranteed to be 0xFF, which can be
		* interpreted as an opaque alpha channel.
		*/
		TJPF_RGBA,
		/**
		* BGRA pixel format.  This is the same as @ref TJPF_BGRX, except that when
		* decompressing, the X component is guaranteed to be 0xFF, which can be
		* interpreted as an opaque alpha channel.
		*/
		TJPF_BGRA,
		/**
		* ABGR pixel format.  This is the same as @ref TJPF_XBGR, except that when
		* decompressing, the X component is guaranteed to be 0xFF, which can be
		* interpreted as an opaque alpha channel.
		*/
		TJPF_ABGR,
		/**
		* ARGB pixel format.  This is the same as @ref TJPF_XRGB, except that when
		* decompressing, the X component is guaranteed to be 0xFF, which can be
		* interpreted as an opaque alpha channel.
		*/
		TJPF_ARGB,
		/**
		* CMYK pixel format.  Unlike RGB, which is an additive color model used
		* primarily for display, CMYK (Cyan/Magenta/Yellow/Key) is a subtractive
		* color model used primarily for printing.  In the CMYK color model, the
		* value of each color component typically corresponds to an amount of cyan,
		* magenta, yellow, or black ink that is applied to a white background.  In
		* order to convert between CMYK and RGB, it is necessary to use a color
		* management system (CMS.)  A CMS will attempt to map colors within the
		* printer's gamut to perceptually similar colors in the display's gamut and
		* vice versa, but the mapping is typically not 1:1 or reversible, nor can it
		* be defined with a simple formula.  Thus, such a conversion is out of scope
		* for a codec library.  However, the TurboJPEG API allows for compressing
		* CMYK pixels into a YCCK JPEG image (see #TJCS_YCCK) and decompressing YCCK
		* JPEG images into CMYK pixels.
		*/
		TJPF_CMYK,
		/**
		* Unknown pixel format.  Currently this is only used by #tjLoadImage().
		*/
		TJPF_UNKNOWN = -1
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport("turbojpeg")]
    private static extern IntPtr tjInitCompress();

    [DllImport("turbojpeg")]
    private static extern IntPtr tjInitDecompress();

    [DllImport("turbojpeg")]
    private static extern int tjCompress(IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelSize, byte[] dstBuf, ref int compressedSize, int jpegSubsamp, int jpegQual, int flags);

    [DllImport("turbojpeg")]
    private static extern int tjCompress2(IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelFormat, byte[] dstBuf, ref int compressedSize, int jpegSubsamp, int jpegQual, int flags);

    [DllImport("turbojpeg")]
    private static extern int tjDecompressHeader3(IntPtr handle, byte[] jpegBuf, int jpegSize, ref int width, ref int height, ref int jpegSubsamp, ref int jpegColorspace);

    [DllImport("turbojpeg")]
    private static extern int tjDecompress2(IntPtr handle, byte[] jpegBuf, int jpegSize, byte[] dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

    [DllImport("turbojpeg")]
    private static extern int tjDestroy(IntPtr handle);
#else

    [DllImport("libturbojpeg")]
	private static extern IntPtr tjInitCompress();

	[DllImport("libturbojpeg")]
	private static extern IntPtr tjInitDecompress();

	[DllImport("libturbojpeg")]
	private static extern int tjCompress(IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelSize, byte[] dstBuf, ref int compressedSize, int jpegSubsamp, int jpegQual, int flags);

	[DllImport("libturbojpeg")]
	private static extern int tjCompress2(IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelFormat, byte[] dstBuf, ref int compressedSize, int jpegSubsamp, int jpegQual, int flags);

	[DllImport("libturbojpeg")]
	private static extern int tjDecompressHeader3(IntPtr handle, byte[] jpegBuf, int jpegSize, ref int width, ref int height, ref int jpegSubsamp, ref int jpegColorspace);

	[DllImport("libturbojpeg")]
	private static extern int tjDecompress2(IntPtr handle, byte[] jpegBuf, int jpegSize, byte[] dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

	[DllImport("libturbojpeg")]
	private static extern int tjDestroy(IntPtr handle);

#endif

	static PixelFormat GetPixelFormat(int pixelSize, GraphicsFormat format, ref Flags flags)
	{
		if (pixelSize == 1) 
			return PixelFormat.TJPF_GRAY;
		if (pixelSize == 3) 
			return PixelFormat.TJPF_RGB;
		if (pixelSize == 4)
		{
			switch (format)
			{
				case GraphicsFormat.R8G8B8A8_UNorm:
					return PixelFormat.TJPF_RGBX;
				case GraphicsFormat.B8G8R8A8_UNorm:
					flags |= Flags.TJ_BGR;
					return PixelFormat.TJPF_BGRX;
			}
		}
		return PixelFormat.TJPF_INVALID;
	}

	public static byte[] Encode(byte[] raw, int width, int height, int pixelSize, GraphicsFormat textureFormat, int quality = 75, Flags flags = Flags.TJ_BOTTOMUP)
	{
		Debug.Assert(raw != null, "Input array cannot be null");
		Debug.Assert(raw.Length != 0, "Array cannot be empty");
		
		if (pixelSize == 0)
		{
			throw new ArgumentException("Pixel size is not supported.");
		}

		var pixelFormat = GetPixelFormat(pixelSize, textureFormat, ref flags);
		if (pixelFormat == PixelFormat.TJPF_INVALID)
		{
			throw new ArgumentException("Pixel format is not supported.");
		}
		
		var encoder = tjInitCompress();

		int size = 0;

		var elementSize = Marshal.SizeOf(raw.GetValue(0).GetType());
		var jpgData = new byte[raw.Length * elementSize];

		var result = tjCompress(encoder, raw, width, 0, height, pixelSize, jpgData, ref size, 0, quality, (int)flags);

		tjDestroy(encoder);

		if (result < 0)
			return null;

		Array.Resize(ref jpgData, size);

		return jpgData;
	}

	public static byte[] Decode(byte[] jpg, ref int width, ref int height)
	{
		var decoder = tjInitDecompress();

		int jpegSubsamp = 0, jpegColorspace = 0;
		tjDecompressHeader3(decoder, jpg, jpg.Length, ref width, ref height, ref jpegSubsamp, ref jpegColorspace);

		var buffer = new byte[width * height * 4];

		tjDecompress2(decoder, jpg, jpg.Length, buffer, width, 0/*pitch*/, height, (int)PixelFormat.TJPF_RGBA, 0);

		tjDestroy(decoder);

		return buffer;
	}
}