using System;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
{
    public class DataCaptureHandler
    {
        public static void ScreenCaptureAsync<T>(Camera sourceCamera, GraphicsFormat renderTextureFormat, string path, CaptureImageEncoder.ImageFormat format = CaptureImageEncoder.ImageFormat.Raw) where T : struct
        {
            Debug.Assert((sourceCamera != null),"Source Camera cannot be null");
            Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(renderTextureFormat));

            Func<AsyncRequest<CaptureCamera.CaptureState>, AsyncRequest<CaptureCamera.CaptureState>.Result> functor = (AsyncRequest<CaptureCamera.CaptureState> r) =>
            {
                r.data.colorBuffer = CaptureImageEncoder.Encode(r.data.colorBuffer as Array, sourceCamera.pixelWidth, sourceCamera.pixelHeight, GraphicsFormat.R8G8B8A8_UNorm, format);
                var result = DXFile.Write(path, r.data.colorBuffer as Array);
                return result ? AsyncRequest<CaptureCamera.CaptureState>.Result.Completed : AsyncRequest<CaptureCamera.CaptureState>.Result.Error;
            };
            CaptureCamera.Capture(sourceCamera, functor);
        }
    }
}

