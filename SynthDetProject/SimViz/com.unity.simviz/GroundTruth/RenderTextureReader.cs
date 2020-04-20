﻿using System;
using System.Linq;
using Unity.AI.Simulation;
using Unity.Collections;
using Unity.Profiling;
using Unity.Simulation;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.SimViz.Sensors
{
    /// <summary>
    /// RenderTextureReader reads a RenderTexture from the GPU each frame and passes the data back through a provided callback.
    /// </summary>
    public class RenderTextureReader<T> : IDisposable where T : struct
    {
        RenderTexture m_Source;
        int m_NumSwapFrames = 2;

        Action<int, NativeArray<T>, RenderTexture> m_ImageReadCallback;

        struct ReadbackRequest
        {
            public AsyncGPUReadbackRequest asyncGpuReadbackRequest;
            public int frameCount;
        }

        // this array is 1:1 with the swap chain array and stores the readback requests for each texture
        ReadbackRequest?[] m_ReadbackRequests;

        Action<AsyncGPUReadbackRequest>[] m_ReadbackRequestDelegates;
        int m_NextSwapChainIndex;
        int m_NextFrameToCapture;

        Texture2D m_CpuTexture;
        Camera m_CameraRenderingToSource;

        static ProfilerMarker s_WaitingForCompletionMarker = new ProfilerMarker("RenderTextureReader_WaitingForCompletion");

        public RenderTextureReader(RenderTexture source, Camera cameraRenderingToSource, Action<int, NativeArray<T>, RenderTexture> imageReadCallback)
        {
            this.m_Source = source;
            this.m_ImageReadCallback = imageReadCallback;
            this.m_CameraRenderingToSource = cameraRenderingToSource;
            m_NextFrameToCapture = Time.frameCount;

            if (!GraphicsUtilities.SupportsAsyncReadback())
            {
                m_CpuTexture = new Texture2D(m_Source.width, m_Source.height, m_Source.graphicsFormat, TextureCreationFlags.None);
                m_NumSwapFrames = 0;
            }
            else
            {
                m_ReadbackRequests = new ReadbackRequest?[m_NumSwapFrames];
                m_ReadbackRequestDelegates = new Action<AsyncGPUReadbackRequest>[m_NumSwapFrames];
                for (var i = 0; i < m_NumSwapFrames; i++)
                {
                    var iForDelegate = i;
                    m_ReadbackRequestDelegates[i] = request => OnGpuReadback(request, iForDelegate);
                }
            }

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

            ProcessReadbackRequests(m_NextSwapChainIndex);
            Assert.IsFalse(m_ReadbackRequests[m_NextSwapChainIndex].HasValue);

            m_ReadbackRequests[m_NextSwapChainIndex] = new ReadbackRequest
            {
                asyncGpuReadbackRequest = AsyncGPUReadback.Request(m_Source, 0, m_Source.graphicsFormat, m_ReadbackRequestDelegates[m_NextSwapChainIndex]),
                frameCount = Time.frameCount
            };

            m_NextSwapChainIndex = (m_NextSwapChainIndex + 1) % m_NumSwapFrames;
        }

        void OnGpuReadback(AsyncGPUReadbackRequest request, int swapChainIndex)
        {
            //TODO: add equality operators to AsyncGPUReadbackRequest
            if (request.hasError)
            {
                Debug.LogError("Error reading segmentation image from GPU");
            }
            else if (request.done && m_ImageReadCallback != null)
            {
                m_ImageReadCallback(m_ReadbackRequests[swapChainIndex].Value.frameCount, request.GetData<T>(), m_Source);
            }
            m_ReadbackRequests[swapChainIndex] = null;
        }

        void ProcessReadbackRequests( int swapChainIndexToForce = -1, bool forceAll = false)
        {
            for (var i = 0; i < m_NumSwapFrames; i++)
            {
                var currentReadbackRequest = m_ReadbackRequests[i];
                if (currentReadbackRequest.HasValue)
                {
                    var readbackRequest = currentReadbackRequest.Value.asyncGpuReadbackRequest;

                    if (swapChainIndexToForce == i || forceAll)
                    {
                        using (s_WaitingForCompletionMarker.Auto())
                            readbackRequest.WaitForCompletion();

                        m_ReadbackRequests[i] = null;
                    }
                }
            }
        }

        public void WaitForAllImages()
        {
            ProcessReadbackRequests(forceAll: true);
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
