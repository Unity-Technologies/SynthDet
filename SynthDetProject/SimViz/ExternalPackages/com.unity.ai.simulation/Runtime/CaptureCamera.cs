using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
{
    public static class CaptureCamera
    {
        public enum Channel
        {
            Color,
            Depth,
            Motion
        }

        public struct CaptureState
        {
            public object colorBuffer;
            public object depthBuffer;
            public object motionVectorsBuffer;

            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor;
            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor;
            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> motionVectorsFunctor;

            public void SetBuffer(Channel channel, object buffer)
            {
                switch (channel)
                {
                    case Channel.Color:  colorBuffer = buffer; break;
                    case Channel.Depth:  depthBuffer = buffer; break;
                    case Channel.Motion: motionVectorsBuffer = buffer; break;
                    default: throw new ArgumentException("CaptureState.SetBuffer invalid channel.");
                }
            }

            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> SetFunctor(Channel channel, Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> functor)
            {
                Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> previous = null;
                switch (channel)
                {
                    case Channel.Color:  previous = colorFunctor; colorFunctor = functor; break;
                    case Channel.Depth:  previous = depthFunctor; depthFunctor = functor; break;
                    case Channel.Motion: previous = motionVectorsFunctor; motionVectorsFunctor = functor; break;
                    default: throw new ArgumentException("CaptureState.SetFunctor invalid channel.");
                }
                return previous;
            }
        }

        static Material _depthCopyMaterial;
        public static ICaptureScriptableRenderPipeline SRPSupport { get; set; }

        static Dictionary<Camera, Dictionary<CameraEvent, CommandBuffer>> _buffers = new Dictionary<Camera, Dictionary<CameraEvent, CommandBuffer>>();

        // Stop tracking a camera and remove any command buffers associated with it.
        public static void ForgetCamera(Camera camera)
        {
            if (_buffers.ContainsKey(camera))
            {
                var events = _buffers[camera];
                foreach (var e in events)
                {
                    camera.RemoveCommandBuffer(e.Key, e.Value);
                    e.Value.Dispose();
                }
                _buffers.Remove(camera);
            }
        }

        static CommandBuffer GetCommandBufferForCamera(CameraEvent e, Camera camera)
        {
            Dictionary<CameraEvent, CommandBuffer> events = null;
            if (!_buffers.ContainsKey(camera))
                events = _buffers[camera] = new Dictionary<CameraEvent, CommandBuffer>();
            else
                events = _buffers[camera];
            Debug.Assert(events != null, "GetCommandBufferForCamera failed to get camera events array.");
            CommandBuffer cb = null;
            if (!events.ContainsKey(e))
            {
                cb = events[e] = new CommandBuffer();
                camera.AddCommandBuffer(e, cb);
            }
            else
            {
                cb = events[e];
                cb.Clear();
            }
            return cb;
        }

        public static AsyncRequest<CaptureState> CaptureColorToFile(Camera camera, GraphicsFormat colorFormat, string colorPath, CaptureImageEncoder.ImageFormat colorImageFormat = CaptureImageEncoder.ImageFormat.Jpg)
        {
            return CaptureColorAndDepthToFile(camera, colorFormat, colorPath, colorImageFormat);
        }

        public static AsyncRequest<CaptureState> CaptureDepthToFile(Camera camera, GraphicsFormat depthFormat, string depthPath, CaptureImageEncoder.ImageFormat depthImageFormat = CaptureImageEncoder.ImageFormat.Tga)
        {
            return CaptureColorAndDepthToFile(camera, depthFormat: depthFormat, depthPath: depthPath, depthImageFormat: depthImageFormat);
        }

        public static AsyncRequest<CaptureState> CaptureColorAndDepthToFile
        (
            Camera camera,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_UNorm, 
            string colorPath = null, 
            CaptureImageEncoder.ImageFormat colorImageFormat = CaptureImageEncoder.ImageFormat.Jpg,
            GraphicsFormat depthFormat = GraphicsFormat.R16_UNorm, 
            string depthPath = null,
            CaptureImageEncoder.ImageFormat depthImageFormat = CaptureImageEncoder.ImageFormat.Tga
        )
        {
            Debug.Assert(camera != null, "CaptureColorAndDepthToFile camera cannot be null");

            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor = null;
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor = null;

            var width  = camera.pixelWidth;
            var height = camera.pixelHeight;

            bool flipY = camera.targetTexture != null || !SystemInfo.graphicsUVStartsAtTop;

            if (colorPath != null)
            {
                colorFunctor = (AsyncRequest<CaptureState> r) =>
                {
                    colorPath = CaptureImageEncoder.EnforceFileExtension(colorPath, colorImageFormat);
                    var result = DXFile.Write(colorPath, CaptureImageEncoder.Encode(r.data.colorBuffer as Array, width, height, colorFormat, colorImageFormat, flipY));
                    return result ? AsyncRequest<CaptureState>.Result.Completed : AsyncRequest<CaptureState>.Result.Error;
                };
            }

            if (depthPath != null)
            {
                depthFunctor = (AsyncRequest<CaptureState> r) =>
                {
                    depthPath = CaptureImageEncoder.EnforceFileExtension(depthPath, depthImageFormat);
                    var result = DXFile.Write(depthPath, CaptureImageEncoder.Encode(r.data.depthBuffer as Array, width, height, depthFormat, depthImageFormat, flipY));
                    return result ? AsyncRequest<CaptureState>.Result.Completed : AsyncRequest<CaptureState>.Result.Error;
                };
            }

            return Capture(camera, colorFunctor, colorFormat, depthFunctor, depthFormat);
        }

        public static AsyncRequest<CaptureState> Capture
        (
            Camera camera,
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor = null,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_UNorm, 
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor = null,
            GraphicsFormat depthFormat = GraphicsFormat.R16_UNorm,
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> motionVectorsFunctor = null,
            GraphicsFormat motionFormat = GraphicsFormat.R16_UNorm
        )
        {
#if DEVELOPMENT_BUILD
            Debug.Assert(camera != null, "Capture camera cannot be null.");
            Debug.Assert(colorFunctor != null || depthFunctor != null || motionVectorsFunctor != null, "Capture one functor must be valid.");

            if (colorFunctor != null)
            {
                Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(colorFormat), "GraphicsFormat not supported");
            }

            if (depthFunctor != null)
            {
                Debug.Assert((camera.depthTextureMode & (DepthTextureMode.Depth | DepthTextureMode.DepthNormals)) != 0, "Depth not specified for camera");
                Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(depthFormat), "GraphicsFormat not supported");
            }

            if (motionVectorsFunctor != null)
            {
                Debug.Assert((camera.depthTextureMode & DepthTextureMode.MotionVectors) != 0, "Motion vectors not enabled in depthTextureMode");
                Debug.Assert(SystemInfo.supportsMotionVectors, "Motion vectors are not supported");
                Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(motionFormat), "GraphicsFormat not supported");
            }
#endif
            var req = DXManager.Instance.CreateRequest<AsyncRequest<CaptureState>>();

            SetupCaptureRequest(req, Channel.Color,  camera, CameraEvent.AfterEverything,    BuiltinRenderTextureType.CameraTarget,  colorFormat,  colorFunctor);
            SetupCaptureRequest(req, Channel.Depth,  camera, CameraEvent.AfterDepthTexture,  BuiltinRenderTextureType.Depth,         depthFormat,  depthFunctor);
            SetupCaptureRequest(req, Channel.Motion, camera, CameraEvent.BeforeImageEffects, BuiltinRenderTextureType.MotionVectors, motionFormat, motionVectorsFunctor);

            if (SRPSupport != null && SRPSupport.UsingCustomRenderPipeline())
                SRPSupport.QueueCameraRequest(camera, req);

            return req;
        }

        static void SetupCaptureRequest
        (
            AsyncRequest<CaptureState> req,
            Channel channel,
            Camera camera,
            CameraEvent cameraEvent,
            BuiltinRenderTextureType source,
            GraphicsFormat format,
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> functor
        )
        {
            if (functor != null)
            {
                var buffer = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, GraphicsFormatUtility.GetRenderTextureFormat(format));
                req.data.SetBuffer (channel, buffer);
                if (SRPSupport != null && SRPSupport.UsingCustomRenderPipeline())
                {
                    if (_depthCopyMaterial == null)
                        _depthCopyMaterial = new Material(Shader.Find("usim/BlitCopyDepth"));

                    // for now ignoring motion vectors, but will support shortly.
                    Material material = source == BuiltinRenderTextureType.Depth ? _depthCopyMaterial : null;

                    req.data.SetFunctor(channel, (AsyncRequest<CaptureState> r) =>
                    {
                        if (camera.targetTexture == null)
                        {
                            RenderTexture.active = null;
                            if (material != null)
                                Graphics.Blit(null, buffer, material);
                            else
                                Graphics.Blit(null, buffer);
                        }

                        if (GraphicsUtilities.SupportsAsyncReadback())
                        {
                            AsyncGPUReadback.Request(buffer, 0, (AsyncGPUReadbackRequest request) =>
                            {
                                RenderTexture.ReleaseTemporary(buffer);
                                r.data.SetBuffer(channel, request.GetData<byte>().ToArray());
                                r.Start(functor);
                            });
                        }
                        else
                        {
                            r.data.SetBuffer(channel, GraphicsUtilities.GetPixelsSlow(buffer));
                            RenderTexture.ReleaseTemporary(buffer);
                            req.Start(functor);
                        }
                        return AsyncRequest.Result.None;
                    });
                }
                else
                {
                    req.data.SetFunctor(channel, functor);

                    CommandBuffer commandBuffer = GetCommandBufferForCamera(cameraEvent, camera);
                    commandBuffer.name = "CaptureCamera." + channel.ToString();
                    commandBuffer.Blit(source, buffer);

                    if (GraphicsUtilities.SupportsAsyncReadback())
                    {
                        commandBuffer.RequestAsyncReadback(buffer, (AsyncGPUReadbackRequest request) =>
                        {
                            commandBuffer.Clear();
                            RenderTexture.ReleaseTemporary(buffer);
                            buffer = null;
                            if (request.hasError)
                            {
                                req.error = true;
                            }
                            else
                            {
                                functor = req.data.SetFunctor(channel, null);
                                if (functor != null)
                                {
                                    req.data.SetBuffer(channel, request.GetData<byte>().ToArray());
                                    req.Start(functor);
                                }
                            }
                        });
                    }
                    else
                    {
                        Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> wrapper = (AsyncRequest<CaptureState> r) =>
                        {
                            r.data.SetBuffer(channel, GraphicsUtilities.GetPixelsSlow(buffer));
                            commandBuffer.Clear();
                            RenderTexture.ReleaseTemporary(buffer);
                            return functor(r);
                        };
                        req.Start(wrapper, AsyncRequest.ExecutionContext.EndOfFrame);
                    }
                }
            }
        }
    }
}
