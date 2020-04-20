using System;
using System.Collections.Generic;
using Unity.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_2019_3_OR_NEWER

using Unity.AI.Simulation;

namespace Unity.AI.Simulation
{
    /// <summary>
    /// </summary>
    /// <param name=""></param>
    /// <returns></returns>
    public class SRPSupport
    { 
        static SRPSupport _instance = null;

        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            _instance = new SRPSupport();

            if (_instance.UsingCustomRenderPipeline())
            {
                CaptureCamera.SRPSupport = _instance;

                Manager.Instance.StartNotification += () =>
                {
                    RenderPipelineManager.endFrameRendering += (ScriptableRenderContext context, Camera[] cameras) =>
                    {
                        foreach (var camera in cameras)
                        {
                            if (Application.isPlaying && _instance._pendingCameraRequests.ContainsKey(camera))
                            {
                                var pendingRequests = _instance._pendingCameraRequests[camera].ToArray();
                                _instance._pendingCameraRequests[camera].Clear();

                                foreach (var r in pendingRequests)
                                {
                                    r.data.colorFunctor?.Invoke(r);
                                    r.data.depthFunctor?.Invoke(r);
                                    r.data.motionVectorsFunctor?.Invoke(r);
                                }
                            }
                        }
                    };
                };
            }
        }

        Dictionary<Camera, List<AsyncRequest<CaptureCamera.CaptureState>>> _pendingCameraRequests = new Dictionary<Camera, List<AsyncRequest<CaptureCamera.CaptureState>>>();

        /// <summary>
        /// Returns true if using a custom render pipeline or false otherwise.
        /// </summary>
        /// <returns>bool</returns>
        public bool UsingCustomRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline != null;
        }

        /// <summary>
        /// With different rendering pipelines, the moment when you need to capture a camera migh be different.
        /// This method will allow for the CaptureCamera class to operate as normal, while allowing the author
        /// of the render pipeline to decide when the work get dispatched.
        /// </summary>
        /// <param name="camera">The camera that you wish to queue a request for.</param>
        /// <param name="request">The request you are queueing for this camera.</param>
        public void QueueCameraRequest(Camera camera, AsyncRequest<CaptureCamera.CaptureState> request)
        {
            if (!_instance._pendingCameraRequests.ContainsKey(camera))
                _instance._pendingCameraRequests.Add(camera, new List<AsyncRequest<CaptureCamera.CaptureState>>());
            _instance._pendingCameraRequests[camera].Add(request);
        }
    }
}

#endif
