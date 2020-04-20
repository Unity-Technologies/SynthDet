﻿using System;
using System.Linq;
using Unity.Collections;
using Unity.Profiling;
using Unity.Simulation;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Perception.Sensors
{
    /// <summary>
    /// RenderTextureReader reads a RenderTexture from the GPU each frame and passes the data back through a provided callback.
    /// </summary>
    public class RenderTextureReader<T> : IDisposable where T : struct
    {
        RenderTexture m_Source;
        Action<int, NativeArray<T>, RenderTexture> m_ImageReadCallback;

        int m_NextFrameToCapture;

        Texture2D m_CpuTexture;
        Camera m_CameraRenderingToSource;

        ProfilerMarker m_WaitingForCompletionMarker = new ProfilerMarker("RenderTextureReader_WaitingForCompletion");

        public RenderTextureReader(RenderTexture source, Camera cameraRenderingToSource, Action<int, NativeArray<T>, RenderTexture> imageReadCallback)
        {
            this.m_Source = source;
            this.m_ImageReadCallback = imageReadCallback;
            this.m_CameraRenderingToSource = cameraRenderingToSource;
            m_NextFrameToCapture = Time.frameCount;

            if (!GraphicsUtilities.SupportsAsyncReadback())
                m_CpuTexture = new Texture2D(m_Source.width, m_Source.height, m_Source.graphicsFormat, TextureCreationFlags.None);

            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
        }

        void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                return;
#endif
            if (!cameras.Contains(m_CameraRenderingToSource))
                return;

            if (m_NextFrameToCapture > Time.frameCount)
                return;

            m_NextFrameToCapture = Time.frameCount + 1;

            if (!GraphicsUtilities.SupportsAsyncReadback())
            {
                RenderTexture.active = m_Source;
                m_CpuTexture.ReadPixels(new Rect(
                    Vector2.zero,
                    new Vector2(m_Source.width, m_Source.height)),
                    0, 0);
                RenderTexture.active = null;
                var data = m_CpuTexture.GetRawTextureData<T>();
                m_ImageReadCallback(Time.frameCount, data, m_Source);
                return;
            }

            var commandBuffer = CommandBufferPool.Get("RenderTextureReader");
            var frameCount = Time.frameCount;
            commandBuffer.RequestAsyncReadback(m_Source, r => OnGpuReadback(r, frameCount));
            context.ExecuteCommandBuffer(commandBuffer);
            context.Submit();
            CommandBufferPool.Release(commandBuffer);
        }

        void OnGpuReadback(AsyncGPUReadbackRequest request, int frameCount)
        {
            if (request.hasError)
            {
                Debug.LogError("Error reading segmentation image from GPU");
            }
            else if (request.done && m_ImageReadCallback != null)
            {
                m_ImageReadCallback(frameCount, request.GetData<T>(), m_Source);
            }
        }

        public void WaitForAllImages()
        {
            AsyncGPUReadback.WaitAllRequests();
        }

        public void Dispose()
        {
            WaitForAllImages();

            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
            if (m_CpuTexture != null)
            {
                Object.Destroy(m_CpuTexture);
                m_CpuTexture = null;
            }
        }
    }
}
