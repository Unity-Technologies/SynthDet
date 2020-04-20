using System;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{
    public class DataCaptureHandler
    {
        /// <summary>
        /// Perform asynchronous capture for the provided camera source.
        /// </summary>
        /// <param name="sourceCamera">Camera for which the capture is to be performed.</param>
        /// <param name="renderTextureFormat">Graphics format to be used for the read back.</param>
        /// <param name="path">File path on the local file system where the image is to be saved.</param>
        /// <param name="format">Image format in which the image is to be saved.</param>
        public static void ScreenCaptureAsync(Camera sourceCamera, GraphicsFormat renderTextureFormat, string path, CaptureImageEncoder.ImageFormat format = CaptureImageEncoder.ImageFormat.Raw)
        {
            Debug.Assert((sourceCamera != null),"Source Camera cannot be null");
            Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(renderTextureFormat));

            Func<AsyncRequest<CaptureCamera.CaptureState>, AsyncRequest<CaptureCamera.CaptureState>.Result> functor = (AsyncRequest<CaptureCamera.CaptureState> r) =>
            {
                r.data.colorBuffer = CaptureImageEncoder.Encode(r.data.colorBuffer as Array, sourceCamera.pixelWidth, sourceCamera.pixelHeight, GraphicsFormat.R8G8B8A8_UNorm, format);
                var result = FileProducer.Write(path, r.data.colorBuffer as Array);
                return result ? AsyncRequest<CaptureCamera.CaptureState>.Result.Completed : AsyncRequest<CaptureCamera.CaptureState>.Result.Error;
            };
            CaptureCamera.Capture(sourceCamera, functor);
        }
    }
}

