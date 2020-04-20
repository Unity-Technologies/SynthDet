using System;

using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AI.Simulation
{
    public static class CaptureRenderTexture
    {
        public static AsyncRequest<object> Capture(RenderTexture src, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null)
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

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