using System;

using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Simulation
{
    /// <summary>
    /// Capture class for capturing RenderTexture contents.
    /// </summary>
    public static class CaptureRenderTexture
    {
        /// <summary>
        /// </summary>
        /// <param name="src">RenderTexture to capture.</param>
        /// <param name="functor">Completion functor for handling the captured data. The object passed is a byte[] of the captured data.</param>
        /// <returns>AsyncRequest&lt;object&gt;</returns>
        public static AsyncRequest<object> Capture(RenderTexture src, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null)
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

#if !UNITY_2019_2_OR_NEWER && (PLATFORM_STANDALONE_OSX || UNITY_EDITOR)
            
            req.data = GraphicsUtilities.GetPixelsSlow(src as RenderTexture);
            req.Start(functor);
#else
            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, 0, (AsyncGPUReadbackRequest request) =>
                {
                    if (request.hasError)
                    {
                        req.error = true;
                    }
                    else
                    {
                        req.data = request.GetData<byte>().ToArray();
                        req.Start(functor);
                    }
                });
            }
            else
            {
                req.data = GraphicsUtilities.GetPixelsSlow(src as RenderTexture);
                req.Start(functor);
            }
#endif
            return req;
        }
    }
}