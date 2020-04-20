using UnityEngine;

namespace Unity.AI.Simulation
{
    public interface ICaptureScriptableRenderPipeline
    {
        bool UsingCustomRenderPipeline();
        void QueueCameraRequest(Camera camera, AsyncRequest<CaptureCamera.CaptureState> request);
    }
}