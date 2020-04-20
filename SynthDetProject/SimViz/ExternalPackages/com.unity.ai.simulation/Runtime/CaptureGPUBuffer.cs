using System;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
{
    public static class CaptureGPUBuffer
    {
        public static AsyncRequest<object> Capture<T>(ComputeBuffer src, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, (AsyncGPUReadbackRequest request) =>
                {
                    req.error = request.hasError;
                    if (!request.hasError)
                    {
                        req.data = request.GetData<T>().ToArray(); 
                        req.Start(functor);
                    }
                });
            }
            else
            {
                T[] dst = new T[src.count];
                src.GetData(dst, 0, 0, src.count);
                
                req.data = dst;
                req.Start(functor);
            }

            return req;
        }

        public static AsyncRequest<object> Capture<T>(ComputeBuffer src, int size, int offset, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, size, offset, (AsyncGPUReadbackRequest request) =>
                {
                    req.error = request.hasError;
                    if (!request.hasError)
                    {
                        req.data = request.GetData<T>().ToArray(); 
                        req.Start(functor);
                    }
                });
            }
            else
            {
                T[] dst = new T[size];
                src.GetData(dst, offset, offset, size);
                
                req.data = dst;
                req.Start(functor);
            }

            return req;
        }

        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex = 0, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, mipIndex, (AsyncGPUReadbackRequest request) =>
                {
                    req.error = request.hasError;
                    if (!request.hasError)
                    {
                        req.data = request.GetData<T>().ToArray(); 
                        req.Start(functor);
                    }
                });
            }
            else
            {
                req.data = GraphicsUtilities.GetPixelsSlow(src as RenderTexture);
                req.Start(functor);
            }

            return req;
        }

        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex, GraphicsFormat dstFormat, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, mipIndex, GraphicsFormatUtility.GetTextureFormat(dstFormat), (AsyncGPUReadbackRequest request) =>
                {
                    req.error = request.hasError;
                    if (!request.hasError)
                    {
                        req.data = request.GetData<T>().ToArray(); 
                        req.Start(functor);
                    }
                });
            }
            else
            {
                req.data = GraphicsUtilities.GetPixelsSlow(src as RenderTexture);
                req.Start(functor);
            }

            return req;
        }

        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();

            if (GraphicsUtilities.SupportsAsyncReadback())
            {
                AsyncGPUReadback.Request(src, mipIndex, x, width, y, height, z, depth, (AsyncGPUReadbackRequest request) =>
                {
                    req.error = request.hasError;
                    if (!request.hasError)
                    {
                        req.data = request.GetData<T>().ToArray(); 
                        req.Start(functor);
                    }
                });
            }
            else
            {
                req.data = GraphicsUtilities.GetPixelsSlow(src as RenderTexture);
                req.Start(functor);
            }

            return req;
        }
    }
}