using System;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{
    public static class CaptureGPUBuffer
    {
        /// <summary>
        /// Perform async read back from the provided compute buffer
        /// </summary>
        /// <param name="src">Compute buffer source to be used for the read back.</param>
        /// <param name="functor">Functor that will be invoked after the async read back request is complete.</param>
        /// <typeparam name="T">Type for the destination data buffer.</typeparam>
        /// <returns>Returns an AsyncRequest</returns>
        public static AsyncRequest<object> Capture<T>(ComputeBuffer src, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

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

        /// <summary>
        /// Perform async read back from the provided compute buffer with size and offset.
        /// </summary>
        /// <param name="src">Compute buffer source to be used for the read back.</param>
        /// <param name="size">Size in bytes of the data to be retrieved from the ComputeBuffer.</param>
        /// <param name="offset">Offset in bytes in the ComputeBuffer.</param>
        /// <param name="functor">Functor that will be invoked after the async read back request is complete.</param>
        /// <typeparam name="T">Type for the destination data buffer.</typeparam>
        /// <returns>Returns an AsyncRequest</returns>
        public static AsyncRequest<object> Capture<T>(ComputeBuffer src, int size, int offset, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

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

        /// <summary>
        /// Perform async read back from the provided source texture.
        /// </summary>
        /// <param name="src">Texture source to be used for the read back.</param>
        /// <param name="mipIndex">Index of the mipmap to be fetched.</param>
        /// <param name="functor">Functor that will be invoked after the async read back request is complete.</param>
        /// <typeparam name="T">Type for the destination data buffer.</typeparam>
        /// <returns>Returns an AsyncRequest</returns>
        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex = 0, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

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

        /// <summary>
        /// Perform async read back from the provided source texture.
        /// </summary>
        /// <param name="src">Texture source to be used for the read back.</param>
        /// <param name="mipIndex">Index of the mipmap to be fetched.</param>
        /// <param name="dstFormat">Target TextureFormat of the data. If the target format is different from the format stored on the GPU, the conversion is automatic.</param>
        /// <param name="functor">Functor that will be invoked after the async read back request is complete.</param>
        /// <typeparam name="T">Type for the destination data buffer.</typeparam>
        /// <returns>Returns an AsynRequest</returns>
        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex, GraphicsFormat dstFormat, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

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

        /// <summary>
        /// Perform async read back from the provided source texture.
        /// </summary>
        /// <param name="src">Texture source to be used for the read back.</param>
        /// <param name="mipIndex">Index of the mipmap to be fetched.</param>
        /// <param name="x">Starting X coordinate in pixels of the Texture data to be fetched.</param>
        /// <param name="width">Width in pixels of the Texture data to be fetched.</param>
        /// <param name="y">Starting Y coordinate in pixels of the Texture data to be fetched.</param>
        /// <param name="height">Height in pixels of the Texture data to be fetched.</param>
        /// <param name="z">Start Z coordinate in pixels for the Texture3D being fetched. Index of Start layer for TextureCube, Texture2DArray and TextureCubeArray being fetched.</param>
        /// <param name="depth">Depth in pixels for Texture3D being fetched. Number of layers for TextureCube, TextureArray and TextureCubeArray.</param>
        /// <param name="functor">Functor that will be invoked after the async read back request is complete.</param>
        /// <typeparam name="T">Type for the destination data buffer.</typeparam>
        /// <returns></returns>
        public static AsyncRequest<object> Capture<T>(Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null) where T : struct
        {
            var req = Manager.Instance.CreateRequest<AsyncRequest<object>>();

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