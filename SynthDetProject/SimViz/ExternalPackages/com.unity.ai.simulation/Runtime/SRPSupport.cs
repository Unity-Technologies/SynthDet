using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_2019_3_OR_NEWER

using Unity.AI.Simulation;

namespace Unity.AI.Simulation
{
    public class SRPSupport : ICaptureScriptableRenderPipeline
    { 
        static SRPSupport _instance = null;

        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            _instance = new SRPSupport();

            if (_instance.UsingCustomRenderPipeline())
            {
                CaptureCamera.SRPSupport = _instance;

                DXManager.Instance.StartNotification += () =>
                {
                    RenderPipelineManager.endCameraRendering += (ScriptableRenderContext context, Camera camera) =>
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
                    };
                };
            }
        }

        Dictionary<Camera, List<AsyncRequest<CaptureCamera.CaptureState>>> _pendingCameraRequests = new Dictionary<Camera, List<AsyncRequest<CaptureCamera.CaptureState>>>();

        public bool UsingCustomRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline != null;
        }

        public void QueueCameraRequest(Camera camera, AsyncRequest<CaptureCamera.CaptureState> request)
        {
            if (!_instance._pendingCameraRequests.ContainsKey(camera))
                _instance._pendingCameraRequests.Add(camera, new List<AsyncRequest<CaptureCamera.CaptureState>>());
            _instance._pendingCameraRequests[camera].Add(request);
        }
    }
}

#endif
