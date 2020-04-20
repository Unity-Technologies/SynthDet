using System;
using System.Collections.Generic;
using Unity.AI.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
 
namespace Unity.Simulation
{
    /// <summary>
    /// Capture class for cameras. Supports Color, Depth, MotionVectors.
    /// Captures supported channels to file system and notifies Manager.
    /// </summary>
    public static class CaptureCamera
    {
        /// <summary>
        /// Enumeration for the supported channels.
        /// </summary>
        public enum Channel
        {
            /// <summary>
            /// Enumeration value specifying the color channel.
            /// </summary>
            Color,
            /// <summary>
            /// Enumeration value specifying the depth channel.
            /// </summary>
            Depth,
            /// <summary>
            /// Enumeration value specifying the motion vectors channel.
            /// </summary>
            Motion
        }

        /// <summary>
        /// Capture state when asynchronously capturing a camera render.
        /// </summary>
        public struct CaptureState
        {
            /// <summary>
            /// While in flight, references the source buffer to read from.
            /// When completed, references the captured data in Array form.
            /// </summary>
            public object colorBuffer;

            /// <summary>
            /// While in flight, references the source buffer to read from.
            /// When completed, references the captured data in Array form.
            /// </summary>
            public object depthBuffer;

            /// <summary>
            /// While in flight, references the source buffer to read from.
            /// When completed, references the captured data in Array form.
            /// </summary>
            public object motionVectorsBuffer;

            /// <summary>
            /// Completion function for handling capture results. Invoked once the capture data is ready.
            /// The handler is responsible for persisting the data. Once invoked, the data is discarded.
            /// </summary>
            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor;

            /// <summary>
            /// Completion function for handling capture results. Invoked once the capture data is ready.
            /// The handler is responsible for persisting the data. Once invoked, the data is discarded.
            /// </summary>
            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor;

            /// <summary>
            /// Completion function for handling capture results. Invoked once the capture data is ready.
            /// The handler is responsible for persisting the data. Once invoked, the data is discarded.
            /// </summary>
            public Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> motionVectorsFunctor;

            /// <summary>
            /// Helper method to set the buffer for a specified channel.
            /// </summary>
            /// <param name="channel">Enumeration value for which channel you are specifying.</param>
            /// <param name="buffer">Object specifying the buffer you are setting for the specified channel.</param>
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

            /// <summary>
            /// Helper method to set the completion functor for a specified channel.
            /// </summary>
            /// <param name="channel">Enumeration value for which channel you are specifying.</param>
            /// <param name="functor">Completion functor for handling the captured data when available.</param>
            /// <returns>The previous completion functor.</returns>
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

        /// <summary>
        /// Support for Scriptable Render Pipeline.
        /// SRP works a little differently, this abstraction allows for custom capture options when using SRP.
        /// The default implementation queues camera captures, and dispatches on RenderPipelineManager.endFrameRendering.
        /// You can provide your own implementation of QueueCameraRequest, and dispatch that when appropriate.
        /// </summary>
        public static SRPSupport SRPSupport;

        static Material _depthCopyMaterial;

        static Dictionary<Camera, Dictionary<CameraEvent, CommandBuffer>> _buffers = new Dictionary<Camera, Dictionary<CameraEvent, CommandBuffer>>();

        /// <summary>
        /// Stop tracking a camera and remove any command buffers associated with it.
        /// </summary>
        /// <param name="camera">Camera that you wish to remove any active command buffers from.</param>
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

        /// <summary>
        /// Captures a camera render and writes out the color channel to a file.
        /// </summary>
        /// <param name="camera"> The Camera to capture data from. </param>
        /// <param name="colorFormat"> The color pixel format to capture in. </param>
        /// <param name="colorPath"> The location of the file to write out. </param>
        /// <param name="colorImageFormat"> The image format to write the data out in. </param>
        /// <returns>AsyncRequest&lt;CaptureState&gt;</returns>
        public static AsyncRequest<CaptureState> CaptureColorToFile(Camera camera, GraphicsFormat colorFormat, string colorPath, CaptureImageEncoder.ImageFormat colorImageFormat = CaptureImageEncoder.ImageFormat.Jpg)
        {
            return CaptureColorAndDepthToFile(camera, colorFormat, colorPath, colorImageFormat);
        }

        /// <summary>
        /// Captures a camera render and writes out the depth channel to a file.
        /// </summary>
        /// <param name="camera"> The Camera to capture data from. </param>
        /// <param name="depthFormat"> The pixel format to capture in. </param>
        /// <param name="depthPath"> The location of the file to write out. </param>
        /// <param name="depthImageFormat"> The image format to write the data out in. </param>
        /// <returns>AsyncRequest&lt;CaptureState&gt;</returns>
        public static AsyncRequest<CaptureState> CaptureDepthToFile(Camera camera, GraphicsFormat depthFormat, string depthPath, CaptureImageEncoder.ImageFormat depthImageFormat = CaptureImageEncoder.ImageFormat.Raw)
        {
            return CaptureColorAndDepthToFile(camera, depthFormat: depthFormat, depthPath: depthPath, depthImageFormat: depthImageFormat);
        }

        /// <summary>
        /// Captures a camera render and writes out the color and depth channels to a file.
        /// </summary>
        /// <param name="camera"> The Camera to capture data from. </param>
        /// <param name="colorFormat"> The pixel format to capture in. </param>
        /// <param name="colorPath"> The location of the file to write out. </param>
        /// <param name="colorImageFormat"> The image format to write the data out in. </param>
        /// <param name="depthFormat"> The pixel format to capture in. </param>
        /// <param name="depthPath"> The location of the file to write out. </param>
        /// <param name="depthImageFormat"> The image format to write the data out in. </param>
        /// <returns>AsyncRequest&lt;CaptureState&gt;</returns>
        public static AsyncRequest<CaptureState> CaptureColorAndDepthToFile
        (
            Camera camera,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_UNorm, 
            string colorPath = null, 
            CaptureImageEncoder.ImageFormat colorImageFormat = CaptureImageEncoder.ImageFormat.Jpg,
            GraphicsFormat depthFormat = GraphicsFormat.R16_UNorm, 
            string depthPath = null,
            CaptureImageEncoder.ImageFormat depthImageFormat = CaptureImageEncoder.ImageFormat.Raw
        )
        {
            Debug.Assert(camera != null, "CaptureColorAndDepthToFile camera cannot be null");

            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor = null;
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor = null;

            var width  = camera.pixelWidth;
            var height = camera.pixelHeight;

            bool flipY = camera.targetTexture == null || SystemInfo.graphicsUVStartsAtTop;
            
            if (colorPath != null)
            {
                colorFunctor = (AsyncRequest<CaptureState> r) =>
                {
                    colorPath = CaptureImageEncoder.EnforceFileExtension(colorPath, colorImageFormat);
                    var result = FileProducer.Write(colorPath, CaptureImageEncoder.Encode(r.data.colorBuffer as Array, width, height, colorFormat, colorImageFormat));
                    return result ? AsyncRequest<CaptureState>.Result.Completed : AsyncRequest<CaptureState>.Result.Error;
                };
            }

            if (depthPath != null)
            {
                depthFunctor = (AsyncRequest<CaptureState> r) =>
                {
                    depthPath = CaptureImageEncoder.EnforceFileExtension(depthPath, depthImageFormat);
                    var result = FileProducer.Write(depthPath, CaptureImageEncoder.Encode(r.data.depthBuffer as Array, width, height, depthFormat, depthImageFormat));
                    return result ? AsyncRequest<CaptureState>.Result.Completed : AsyncRequest<CaptureState>.Result.Error;
                };
            }

            return Capture(camera, colorFunctor, colorFormat, depthFunctor, depthFormat, flipY: flipY);
        }

        /// <summary>
        /// Main Capture entrypoint. 
        /// </summary>
        /// <param name="camera"> The Camera to capture data from. </param>
        /// <param name="colorFunctor"> Completion functor for the color channel. </param>
        /// <param name="colorFormat"> The pixel format to capture in. </param>
        /// <param name="depthFunctor"> Completion functor for the depth channel. </param>
        /// <param name="depthFormat"> The pixel format to capture in. </param>
        /// <param name="motionVectorsFunctor"> Completion functor for the motion vectors channel. </param>
        /// <param name="motionFormat"> The pixel format to capture in. </param>
        /// <param name="flipY"> Whether or not to flip the image vertically. </param>
        /// <returns>AsyncRequest&lt;CaptureState&gt;</returns>
        public static AsyncRequest<CaptureState> Capture
        (
            Camera camera,
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> colorFunctor = null,
            GraphicsFormat colorFormat = GraphicsFormat.R8G8B8A8_UNorm, 
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> depthFunctor = null,
            GraphicsFormat depthFormat = GraphicsFormat.R16_UNorm,
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> motionVectorsFunctor = null,
            GraphicsFormat motionFormat = GraphicsFormat.R16_UNorm,
            bool flipY = false
        )
        {
#if UNITY_EDITOR
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
#endif // UNITY_EDITOR
            var req = Manager.Instance.CreateRequest<AsyncRequest<CaptureState>>();

            SetupCaptureRequest(req, Channel.Color,  camera, CameraEvent.AfterEverything,    BuiltinRenderTextureType.CameraTarget,  colorFormat,  colorFunctor,         flipY);
            SetupCaptureRequest(req, Channel.Depth,  camera, CameraEvent.AfterDepthTexture,  BuiltinRenderTextureType.Depth,         depthFormat,  depthFunctor,         flipY);
            SetupCaptureRequest(req, Channel.Motion, camera, CameraEvent.BeforeImageEffects, BuiltinRenderTextureType.MotionVectors, motionFormat, motionVectorsFunctor, flipY);
            
            SRPSupport?.QueueCameraRequest(camera, req);

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
            Func<AsyncRequest<CaptureState>, AsyncRequest<CaptureState>.Result> functor,
            bool flipY
        )
        {
            if (functor != null)
            {
                // declared for possible capture, to avoid use from other threads.
                var cameraTargetTexture = camera.targetTexture;

                RenderTexture target1 = null;
                RenderTexture target2 = null;

                Action ReleaseTargets = () =>
                {
                    if (target1 != null && target1 != cameraTargetTexture)
                    {
                        RenderTexture.ReleaseTemporary(target1);
                        target1 = null;
                    }
                    if (target2 != null && target2 != cameraTargetTexture)
                    {
                        RenderTexture.ReleaseTemporary(target2);
                        target2 = null;
                    }
                };

                Material depthMaterial = null;
                if (source == BuiltinRenderTextureType.Depth)
                {
                    // Lazily initialize _depthCopyMaterial. This could be done somewhere else.
                    if (_depthCopyMaterial == null)
                        _depthCopyMaterial = new Material(Shader.Find("usim/BlitCopyDepth")); 
                    depthMaterial = _depthCopyMaterial;
                }

                if (SRPSupport != null && SRPSupport.UsingCustomRenderPipeline())
                {
                    req.data.SetFunctor(channel, (AsyncRequest<CaptureState> r) =>
                    {
                        var target = SetupRenderTargets(ref target1, ref target2, camera, null, format, cameraTargetTexture, depthMaterial, flipY);

                        if (GraphicsUtilities.SupportsAsyncReadback())
                        {
                            AsyncGPUReadback.Request(target, 0, (AsyncGPUReadbackRequest request) =>
                            {
                                ReleaseTargets();
                                if (request.hasError)
                                    req.error = true;
                                else
                                {
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
                            r.data.SetBuffer(channel, GraphicsUtilities.GetPixelsSlow(target));
                            ReleaseTargets();
                            req.Start(functor);
                        }
                        return AsyncRequest.Result.None;
                    });
                }
                else
                {
                    req.data.SetFunctor(channel, functor);

                    CommandBuffer commandBuffer = GetCommandBufferForCamera(cameraEvent, camera);
                    commandBuffer.name = $"CaptureCamera.{channel.ToString()}";

                    var target = SetupRenderTargets(ref target1, ref target2, camera, commandBuffer, format, cameraTargetTexture, depthMaterial, flipY);

                    if (GraphicsUtilities.SupportsAsyncReadback())
                    {
                        commandBuffer.RequestAsyncReadback(target, (AsyncGPUReadbackRequest request) =>
                        {
                            commandBuffer.Clear();
                            ReleaseTargets();
                            if (request.hasError)
                                req.error = true;
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
                            r.data.SetBuffer(channel, GraphicsUtilities.GetPixelsSlow(target));
                            ReleaseTargets();
                            return functor(r);
                        };
                        req.Start(wrapper, AsyncRequest.ExecutionContext.EndOfFrame);
                    }
                }
            }
        }

        static RenderTexture SetupRenderTargets
        (
            ref RenderTexture target1,
            ref RenderTexture target2,
            Camera camera,
            CommandBuffer commandBuffer,
            GraphicsFormat format,
            RenderTexture cameraTargetTexture,
            Material depthMaterial,
            bool flipY
        )
        {
            if (cameraTargetTexture == null)
            {
                target1 = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, GraphicsFormatUtility.GetRenderTextureFormat(format));
                Blit(null, null, target1, depthMaterial);
            }

            if (flipY)
            {
                target2 = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, GraphicsFormatUtility.GetRenderTextureFormat(format));
                Blit(null, target1, target2, new Vector2(1, -1), Vector2.up);
            }

            // order is target2 > targetTexture > target1
            var target = target2 == null ? (cameraTargetTexture == null ? target1 : cameraTargetTexture) : target2;

            return target;
        }

        static void Blit(CommandBuffer commandBuffer, Texture texture, RenderTexture renderTexture, Material material)
        {
            if (commandBuffer != null)
            {
                if (material != null)
                    commandBuffer.Blit(texture, renderTexture, material);
                else
                    commandBuffer.Blit(texture, renderTexture);
            }
            else
            {
                if (material != null)
                    Graphics.Blit(texture, renderTexture, material);
                else
                    Graphics.Blit(texture, renderTexture);
            }
        }

        static void Blit(CommandBuffer commandBuffer, BuiltinRenderTextureType source, BuiltinRenderTextureType dest, Material material)
        {
            if (material != null)
                commandBuffer.Blit(source, dest, material);
            else
                commandBuffer.Blit(source, dest);
        }

        static void Blit(CommandBuffer commandBuffer, Texture texture, RenderTexture renderTexture, Vector2 scale, Vector2 offset)
        {
            if (commandBuffer != null)
            {
                commandBuffer.Blit(texture, renderTexture, scale, offset);
            }
            else
            {
                Graphics.Blit(texture, renderTexture, scale, offset);
            }
        }
    }
}
