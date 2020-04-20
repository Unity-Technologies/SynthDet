using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Simulation;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif
using UnityEngine.SimViz.Sensors;

namespace UnityEngine.SimViz
{
    /// <summary>
    /// SimvizCamera schedules frames and reports metadata about the associated Camera for the current simulation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PerceptionCamera : MonoBehaviour
    {
        const string k_SemanticSegmentationDirectory = "SemanticSegmentation";
        //TODO: Remove the Guid path when we have proper dataset merging in USim/Thea
        static string k_RgbDirectory = $"RGB{Guid.NewGuid()}";
        static string s_RgbFilePrefix = "rgb_";
        const string k_SegmentationFilePrefix = "segmentation_";

        /// <summary>
        /// A human-readable description of the camera.
        /// </summary>
        public string description;
        /// <summary>
        /// The period in seconds that the Camera should render
        /// </summary>
        public float period = .0166f;
        /// <summary>
        /// The start time in seconds of the first frame in the simulation.
        /// </summary>
        public float startTime;
        /// <summary>
        /// Whether camera output should be captured to disk
        /// </summary>
        public bool captureRgbImages = true;
        /// <summary>
        /// Whether semantic segmentation images should be generated
        /// </summary>
        public bool produceSegmentationImages = true;
        /// <summary>
        /// Whether object counts should be computed
        /// </summary>
        public bool produceObjectCountAnnotations = true;
        /// <summary>
        /// The ID to use for object count annotations in the resulting dataset
        /// </summary>
        public string m_ObjectCountID = "51DA3C27-369D-4929-AEA6-D01614635CE2";
        /// <summary>
        /// Whether object bounding boxes should be computed
        /// </summary>
        public bool produceBoundingBoxAnnotations = true;
        /// <summary>
        /// The ID to use for bounding box annotations in the resulting dataset
        /// </summary>
        public string m_BoundingBoxID = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";
        /// <summary>
        /// Whether visible pixels should be computed for each labeled object
        /// </summary>
        public bool produceVisiblePixelsMetric = true;
        /// <summary>
        /// The ID to use for visible pixels metrics in the resulting dataset
        /// </summary>
        public string m_VisiblePixelsID = "5BA92024-B3B7-41A7-9D3F-C03A6A8DDD01";
        /// <summary>
        /// The corner of the image to use as the origin for bounding boxs.
        /// </summary>
        public BoundingBoxOrigin boundingBoxOrigin = BoundingBoxOrigin.TopLeft;
        /// <summary>
        /// The LabelingConfiguration to use for segmentation and object count.
        /// </summary>
        public LabelingConfiguration LabelingConfiguration;

        /// <summary>
        /// Invoked when RenderedObjectInfos (bounding boxes) are calculated. The first parameter is the Time.frameCount at which the objects were rendered. This may be called many frames after the frame in which the objects were rendered.
        /// </summary>
        public event Action<int, NativeArray<RenderedObjectInfo>> renderedObjectInfosCalculated;

        [NonSerialized]
        internal RenderTexture m_LabelingTexture;
        [NonSerialized]
        internal RenderTexture m_SegmentationTexture;

        RenderTextureReader<short> m_ClassLabelingTextureReader;
        RenderTextureReader<uint> m_SegmentationReader;
        CpuLabelingObjectInfoPass m_CpuLabelingObjectInfoPass;
        Dictionary<string, object> m_PersistentSensorData = new Dictionary<string, object>();

        [NonSerialized]
        internal SegmentationUrpPass m_SegmentationUrpPass;
        [NonSerialized]
        internal LabelingUrpPass m_LabelingUrpPass;

        bool m_CapturedLastFrame;

        EgoMarker m_EgoMarker;

        /// <summary>
        /// The <see cref="SensorHandle"/> associated with this camera. Use this to report additional annotations and metrics at runtime.
        /// </summary>
        public SensorHandle SensorHandle { get; private set; }

        /// <summary>
        /// Add a data object which will be added to the dataset with each capture. Overrides existing sensor data associated with the given key.
        /// </summary>
        /// <param name="key">The key to associate with the data.</param>
        /// <param name="data">An object containing the data. Will be serialized into json.</param>
        public void SetPersistentSensorData(string key, object data)
        {
            m_PersistentSensorData[key] = data;
        }

        /// <summary>
        /// Removes a persistent sensor data object.
        /// </summary>
        /// <param name="key">The key of the object to remove.</param>
        /// <returns>True if a data object was removed. False if it was not set.</returns>
        public bool RemovePersistentSensorData(string key)
        {
            return m_PersistentSensorData.Remove(key);
        }

        struct AsyncCaptureInfo
        {
            public int FrameCount;
            public AsyncAnnotation SegmentationAsyncAnnotation;
            public AsyncMetric ClassCountAsyncMetric;
            public AsyncMetric VisiblePixelsAsyncMetric;
            public AsyncAnnotation BoundingBoxAsyncMetric;
        }

        List<AsyncCaptureInfo> m_AsyncCaptureInfos = new List<AsyncCaptureInfo>();

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct ClassCountValue
        {
            public int label_id;
            public string label_name;
            public uint count;
        }


        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct BoundingBoxValue
        {
            public int label_id;
            public string label_name;
            public int instance_id;
            public float x;
            public float y;
            public float width;
            public float height;
        }

        ClassCountValue[] m_ClassCountValues;
        BoundingBoxValue[] m_BoundingBoxValues;
        VisiblePixelsValue[] m_VisiblePixelsValues;

#if HDRP_PRESENT
        SegmentationPass m_SegmentationPass;
        LabelingPass m_LabelingPass;
        LabelHistogramPass m_LabelHistogramPass;
#endif
        MetricDefinition m_ObjectCountMetricDefinition;
        AnnotationDefinition m_BoundingBoxAnnotationDefinition;
        AnnotationDefinition m_SegmentationAnnotationDefinition;
        MetricDefinition m_VisiblePixelsMetricDefinition;

        static ProfilerMarker s_WriteFrame = new ProfilerMarker("Write Frame (PerceptionCamera)");
        static ProfilerMarker s_FlipY = new ProfilerMarker("Flip Y (PerceptionCamera)");
        static ProfilerMarker s_EncodeAndSave = new ProfilerMarker("Encode and save (PerceptionCamera)");
        static ProfilerMarker s_ClassCountCallback = new ProfilerMarker("OnClassLabelsReceived");
        static ProfilerMarker s_RenderedObjectInfosCalculatedEvent = new ProfilerMarker("renderedObjectInfosCalculated event");
        static ProfilerMarker s_BoundingBoxCallback = new ProfilerMarker("OnBoundingBoxesReceived");
        static ProfilerMarker s_ProduceVisiblePixelsMetric = new ProfilerMarker("ProduceVisiblePixelsMetric");

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        struct SemanticSegmentationSpec
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public string label_name;
            [UsedImplicitly]
            public int pixel_value;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        struct ObjectCountSpec
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public string label_name;
        }

        // Start is called before the first frame update
        void Awake()
        {
            //CaptureOptions.useAsyncReadbackIfSupported = false;

            m_EgoMarker = this.GetComponentInParent<EgoMarker>();
            var ego = m_EgoMarker == null ? SimulationManager.RegisterEgo("") : m_EgoMarker.Ego;
            SensorHandle = SimulationManager.RegisterSensor(ego, "camera", description, period, startTime);

            var myCamera = GetComponent<Camera>();
            var width = myCamera.pixelWidth;
            var height = myCamera.pixelHeight;

            if ((produceSegmentationImages || produceObjectCountAnnotations || produceBoundingBoxAnnotations) && LabelingConfiguration == null)
            {
                Debug.LogError("LabelingConfiguration must be set if producing ground truth data");
                produceSegmentationImages = false;
                produceObjectCountAnnotations = false;
                produceBoundingBoxAnnotations = false;
            }

            m_SegmentationTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            m_SegmentationTexture.name = "Segmentation";
            m_SegmentationUrpPass = new SegmentationUrpPass(myCamera, m_SegmentationTexture);
            m_LabelingTexture = new RenderTexture(new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 8));
            m_LabelingTexture.name = "Labeling";
            m_LabelingUrpPass = new LabelingUrpPass(myCamera, m_LabelingTexture, LabelingConfiguration);

#if HDRP_PRESENT
            var customPassVolume = this.GetComponent<CustomPassVolume>() ?? gameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            customPassVolume.isGlobal = true;
            m_SegmentationPass = new SegmentationPass(myCamera, segmentationTexture)
            {
                name = "Segmentation Pass"
            };
            m_LabelingPass = new LabelingPass(myCamera, labelingTexture)
            {
                name = "Labeling Pass",
                labelingConfiguration = LabelingConfiguration
            };

            m_LabelHistogramPass = new LabelHistogramPass(myCamera)
            {
                name = "Label Histogram Pass",
                SegmentationTexture = segmentationTexture,
                LabelingConfiguration = LabelingConfiguration,
                WriteToLog = false
            };

            m_LabelHistogramPass.ClassCountsReceived += OnClassCountsReceived;

            SetupPasses(customPassVolume);
#endif

            if (produceSegmentationImages)
            {
                var specs = LabelingConfiguration.LabelingConfigurations.Select((l, index) => new SemanticSegmentationSpec()
                {
                    label_id = index,
                    label_name = l.label,
                    pixel_value = l.value
                }).ToArray();

                m_SegmentationAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("semantic segmentation", specs, "pixel-wise semantic segmentation label", "PNG");
            }

            if (produceObjectCountAnnotations || produceBoundingBoxAnnotations)
            {
                var labelingMetricSpec = LabelingConfiguration.LabelingConfigurations.Select((l, index) => new ObjectCountSpec()
                {
                    label_id = index,
                    label_name = l.label,
                }).ToArray();

                if (produceObjectCountAnnotations)
                {
                    m_ObjectCountMetricDefinition = SimulationManager.RegisterMetricDefinition("object count", labelingMetricSpec, "Counts of objects for each label in the sensor's view", id: new Guid(m_ObjectCountID));
                }

                if (produceBoundingBoxAnnotations)
                {
                    m_BoundingBoxAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("bounding box", labelingMetricSpec, "Bounding boxe for each labeled object visible to the sensor", id: new Guid(m_BoundingBoxID));
                }

                if (produceVisiblePixelsMetric)
                    m_VisiblePixelsMetricDefinition = SimulationManager.RegisterMetricDefinition("visible pixels", labelingMetricSpec, "Visible pixels for each visible object", id: new Guid(m_VisiblePixelsID));

                m_ClassLabelingTextureReader = new RenderTextureReader<short>(m_LabelingTexture, myCamera,
                    (frameCount, data, tex) => OnSemanticSegmentationImageRead(frameCount, data));
                RenderPipelineManager.endFrameRendering += OnEndFrameRendering;

                m_CpuLabelingObjectInfoPass = new CpuLabelingObjectInfoPass(LabelingConfiguration);
                m_CpuLabelingObjectInfoPass.EnsureActivated();

                m_SegmentationReader = new RenderTextureReader<uint>(m_SegmentationTexture, myCamera, (frameCount, data, tex) =>
                {
                    if (OnSegmentationImageReceived != null)
                        OnSegmentationImageReceived(frameCount, data);

                    m_CpuLabelingObjectInfoPass.Compute(data, tex.width, boundingBoxOrigin, out var renderedObjectInfos, out var classCounts);

                    using (s_RenderedObjectInfosCalculatedEvent.Auto())
                        renderedObjectInfosCalculated?.Invoke(frameCount, renderedObjectInfos);

                    if (produceObjectCountAnnotations)
                        OnObjectCountsReceived(classCounts, LabelingConfiguration.LabelingConfigurations, frameCount);

                    if (produceBoundingBoxAnnotations)
                        ProduceBoundingBoxesAnnotation(renderedObjectInfos, LabelingConfiguration.LabelingConfigurations, frameCount);

                    if (produceVisiblePixelsMetric)
                        ProduceVisiblePixelsMetric(renderedObjectInfos, frameCount);
                });
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            SimulationManager.SimulationEnding += OnSimulationEnding;
        }

        struct VisiblePixelsValue
        {
            public int label_id;
            public int instance_id;
            public int visible_pixels;
        }

        void ProduceVisiblePixelsMetric(NativeArray<RenderedObjectInfo> renderedObjectInfos, int frameCount)
        {
            using (s_ProduceVisiblePixelsMetric.Auto())
            {
                var findResult = FindAsyncCaptureInfo(frameCount);
                if (findResult.index == -1)
                    return;
                var asyncCaptureInfo = findResult.asyncCaptureInfo;
                var visiblePixelsAsyncMetric = asyncCaptureInfo.VisiblePixelsAsyncMetric;
                if (!visiblePixelsAsyncMetric.IsValid)
                    return;

                if (m_VisiblePixelsValues == null || m_VisiblePixelsValues.Length != renderedObjectInfos.Length)
                    m_VisiblePixelsValues = new VisiblePixelsValue[renderedObjectInfos.Length];

                for (int i = 0; i < renderedObjectInfos.Length; i++)
                {
                    var objectInfo = renderedObjectInfos[i];
                    if (!TryGetClassIdFromInstanceId(objectInfo.instanceId, out var labelId))
                        continue;

                    m_VisiblePixelsValues[i] = new VisiblePixelsValue
                    {
                        label_id = labelId,
                        instance_id = objectInfo.instanceId,
                        visible_pixels = objectInfo.pixelCount
                    };
                }

                visiblePixelsAsyncMetric.ReportValues(m_VisiblePixelsValues);
            }
        }

        void OnEndFrameRendering(ScriptableRenderContext arg1, Camera[] arg2)
        {
            var myCamera = GetComponent<Camera>();
            if (!arg2.Contains(myCamera))
                return;

            ReportAsyncAnnotations();
        }

#if HDRP_PRESENT
        void SetupPasses(CustomPassVolume customPassVolume)
        {
            customPassVolume.customPasses.Remove(m_SegmentationPass);
            customPassVolume.customPasses.Remove(m_LabelingPass);
            customPassVolume.customPasses.Remove(m_LabelHistogramPass);

            if (produceSegmentationImages || produceObjectCountAnnotations)
                customPassVolume.customPasses.Add(m_SegmentationPass);

            if (produceSegmentationImages)
                customPassVolume.customPasses.Add(m_LabelingPass);

            if (produceObjectCountAnnotations)
            {
                customPassVolume.customPasses.Add(m_LabelHistogramPass);
            }
        }
#endif

        void ProduceBoundingBoxesAnnotation(NativeArray<RenderedObjectInfo> renderedObjectInfos, List<LabelingConfigurationEntry> labelingConfigurations, int frameCount)
        {
            using (s_BoundingBoxCallback.Auto())
            {
                var findResult = FindAsyncCaptureInfo(frameCount);
                if (findResult.index == -1)
                    return;
                var asyncCaptureInfo = findResult.asyncCaptureInfo;
                var boundingBoxAsyncAnnotation = asyncCaptureInfo.BoundingBoxAsyncMetric;
                if (!boundingBoxAsyncAnnotation.IsValid)
                    return;

                if (m_BoundingBoxValues == null || m_BoundingBoxValues.Length != renderedObjectInfos.Length)
                    m_BoundingBoxValues = new BoundingBoxValue[renderedObjectInfos.Length];

                for (int i = 0; i < renderedObjectInfos.Length; i++)
                {
                    var objectInfo = renderedObjectInfos[i];
                    if (!TryGetClassIdFromInstanceId(objectInfo.instanceId, out var labelId))
                        continue;

                    m_BoundingBoxValues[i] = new BoundingBoxValue
                    {
                        label_id = labelId,
                        label_name = labelingConfigurations[labelId].label,
                        instance_id = objectInfo.instanceId,
                        x = objectInfo.boundingBox.x,
                        y = objectInfo.boundingBox.yMax,
                        width = objectInfo.boundingBox.width,
                        height = objectInfo.boundingBox.height,
                    };
                }

                boundingBoxAsyncAnnotation.ReportValues(m_BoundingBoxValues);
            }
        }

        public bool TryGetClassIdFromInstanceId(int instanceId, out int labelId)
        {
            if (m_CpuLabelingObjectInfoPass == null)
                throw new InvalidOperationException($"{nameof(TryGetClassIdFromInstanceId)} can only be used when bounding box capture is enabled");

            labelId = 0;
            if (m_CpuLabelingObjectInfoPass.InstanceIdToClassIdLookup.Length <= instanceId)
                return false;

            labelId = m_CpuLabelingObjectInfoPass.InstanceIdToClassIdLookup[instanceId];
            return true;
        }

        void OnObjectCountsReceived(NativeSlice<uint> counts, IReadOnlyList<LabelingConfigurationEntry> entries, int frameCount)
        {
            using (s_ClassCountCallback.Auto())
            {
                OnClassCountsReceived?.Invoke(counts, entries, frameCount);

                var findResult = FindAsyncCaptureInfo(frameCount);
                if (findResult.index == -1)
                    return;

                var asyncCaptureInfo = findResult.asyncCaptureInfo;
                var classCountAsyncMetric = asyncCaptureInfo.ClassCountAsyncMetric;
                if (!classCountAsyncMetric.IsValid)
                    return;

                if (m_ClassCountValues == null || m_ClassCountValues.Length != entries.Count)
                    m_ClassCountValues = new ClassCountValue[entries.Count];

                for (int i = 0; i < entries.Count; i++)
                {
                    m_ClassCountValues[i] = new ClassCountValue()
                    {
                        label_id = i,
                        label_name = entries[i].label,
                        count = counts[i]
                    };
                }

                classCountAsyncMetric.ReportValues(m_ClassCountValues);
            }
        }

        (int index, AsyncCaptureInfo asyncCaptureInfo) FindAsyncCaptureInfo(int frameCount)
        {
            for (int i = 0; i < m_AsyncCaptureInfos.Count; i++)
            {
                var captureInfo = m_AsyncCaptureInfos[i];
                if (captureInfo.FrameCount == frameCount)
                {
                    return (i, captureInfo);
                }
            }

            return (-1, default);
        }

        // Update is called once per frame
        void Update()
        {
            if (!SensorHandle.IsValid)
                return;

            var camera = this.GetComponent<Camera>();
            camera.enabled = SensorHandle.ShouldCaptureThisFrame;

            m_AsyncCaptureInfos.RemoveSwapBack(i =>
                !i.SegmentationAsyncAnnotation.IsPending &&
                !i.BoundingBoxAsyncMetric.IsPending &&
                !i.VisiblePixelsAsyncMetric.IsPending &&
                !i.ClassCountAsyncMetric.IsPending);
        }

        void ReportAsyncAnnotations()
        {
            if (produceSegmentationImages || produceObjectCountAnnotations || produceBoundingBoxAnnotations)
            {
                var captureInfo = new AsyncCaptureInfo()
                {
                    FrameCount = Time.frameCount
                };
                if (produceSegmentationImages)
                    captureInfo.SegmentationAsyncAnnotation = SensorHandle.ReportAnnotationAsync(m_SegmentationAnnotationDefinition);

                if (produceObjectCountAnnotations)
                    captureInfo.ClassCountAsyncMetric = SensorHandle.ReportMetricAsync(m_ObjectCountMetricDefinition);

                if (produceBoundingBoxAnnotations)
                    captureInfo.BoundingBoxAsyncMetric = SensorHandle.ReportAnnotationAsync(m_BoundingBoxAnnotationDefinition);

                if (produceVisiblePixelsMetric)
                    captureInfo.VisiblePixelsAsyncMetric = SensorHandle.ReportMetricAsync(m_VisiblePixelsMetricDefinition);

                m_AsyncCaptureInfos.Add(captureInfo);
            }
        }

        void CaptureRgbData(Camera camera)
        {
            Profiler.BeginSample("CaptureDataFromLastFrame");
            if (!captureRgbImages)
                return;

            var captureFilename = Path.Combine(Manager.Instance.GetDirectoryFor(k_RgbDirectory), $"{s_RgbFilePrefix}{Time.frameCount}.png");
            var dxRootPath = Path.Combine(k_RgbDirectory, $"{s_RgbFilePrefix}{Time.frameCount}.png");
            SensorHandle.ReportCapture(dxRootPath, SensorSpatialData.FromGameObjects(m_EgoMarker == null ? null : m_EgoMarker.gameObject, gameObject), m_PersistentSensorData.Select(kvp => (kvp.Key, kvp.Value)).ToArray());

            Func<AsyncRequest<CaptureCamera.CaptureState>, AsyncRequest.Result> colorFunctor = null;
            int width = camera.pixelWidth;
            int height = camera.pixelHeight;
            var flipY = ShouldFlipY(camera);

            colorFunctor = r =>
            {
                using (s_WriteFrame.Auto())
                {
                    var dataColorBuffer = (byte[])r.data.colorBuffer;
                    if (flipY)
                        FlipImageY(dataColorBuffer, height);

                    byte[] encodedData;
                    using (s_EncodeAndSave.Auto())
                    {
                        encodedData = ImageConversion.EncodeArrayToPNG(dataColorBuffer, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height);
                    }

                    return !FileProducer.Write(captureFilename, encodedData) ? AsyncRequest.Result.Error : AsyncRequest.Result.Completed;
                }
            };

            CaptureCamera.Capture(camera, colorFunctor);

            //CaptureCamera.CaptureColorToFile(camera, GraphicsFormat.R8G8B8_SNorm, captureFilename, CaptureImageEncoder.ImageFormat.Jpg);
            //ScreenCapture.CaptureScreenshot(captureFilename);

            Profiler.EndSample();
        }

        bool ShouldFlipY(Camera camera)
        {
#if HDRP_PRESENT
            var hdAdditionalCameraData = GetComponent<HDAdditionalCameraData>();

            //Based on logic in HDRenderPipeline.PrepareFinalBlitParameters
            bool flipY = camera.targetTexture != null || hdAdditionalCameraData.flipYMode == HDAdditionalCameraData.FlipYMode.ForceFlipY || camera.cameraType == CameraType.Game;
#endif
#if URP_PRESENT
    #if UNITY_EDITOR
            bool flipY = camera.targetTexture != null || camera.cameraType == CameraType.Game;
    #else
            bool flipY = false;
    #endif
#endif
            return flipY;
        }

        static unsafe void FlipImageY(byte[] dataColorBuffer, int height)
        {
            using (s_FlipY.Auto())
            {
                int stride = dataColorBuffer.Length / height;
                NativeArray<byte> buffer = new NativeArray<byte>(stride, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                fixed (byte* colorBufferPtr = &dataColorBuffer[0])
                {
                    byte* unsafePtr = (byte*)buffer.GetUnsafePtr();
                    for (int row = 0; row < height / 2; row++)
                    {
                        var nearRowStartPtr = colorBufferPtr + stride * row;
                        var oppositeRowStartPtr = colorBufferPtr + stride * (height - row - 1);
                        UnsafeUtility.MemCpy(unsafePtr, oppositeRowStartPtr, stride);
                        UnsafeUtility.MemCpy(oppositeRowStartPtr, nearRowStartPtr, stride);
                        UnsafeUtility.MemCpy(nearRowStartPtr, unsafePtr, stride);
                    }
                }

                buffer.Dispose();
            }
        }

        void OnSimulationEnding()
        {
#if HDRP_PRESENT
            m_LabelHistogramPass.WaitForAllRequests();
#endif
            m_ClassLabelingTextureReader?.WaitForAllImages();

            m_SegmentationReader?.WaitForAllImages();
            m_SegmentationReader?.Dispose();
            m_SegmentationReader = null;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
        {
            if (camera != GetComponent<Camera>())
                return;
            CaptureRgbData(camera);
        }

        void OnDisable()
        {
            SimulationManager.SimulationEnding -= OnSimulationEnding;

            OnSimulationEnding();

            m_ClassLabelingTextureReader?.Dispose();
            m_ClassLabelingTextureReader = null;
            if (m_SegmentationTexture != null)
                m_SegmentationTexture.Release();

            m_SegmentationTexture = null;
            if (m_LabelingTexture != null)
                m_LabelingTexture.Release();

            m_CpuLabelingObjectInfoPass?.Cleanup();
            m_CpuLabelingObjectInfoPass?.Dispose();
            m_CpuLabelingObjectInfoPass = null;

            if (SensorHandle.IsValid)
                SensorHandle.Dispose();

            SensorHandle = default;

            m_LabelingTexture = null;
        }

        struct AsyncSemanticSegmentationWrite
        {
            public short[] dataArray;
            public int width;
            public int height;
            public string path;
        }

        void OnSemanticSegmentationImageRead(int frameCount, NativeArray<short> data)
        {
            var findResult = FindAsyncCaptureInfo(frameCount);
            var asyncCaptureInfo = findResult.asyncCaptureInfo;

            var dxLocalPath = Path.Combine(k_SemanticSegmentationDirectory, k_SegmentationFilePrefix) + frameCount + ".png";
            var path = Path.Combine(Manager.Instance.GetDirectoryFor(k_SemanticSegmentationDirectory), k_SegmentationFilePrefix) + frameCount + ".png";
            var annotation = asyncCaptureInfo.SegmentationAsyncAnnotation;
            if (!annotation.IsValid)
                return;

            annotation.ReportFile(dxLocalPath);

            var asyncRequest = Manager.Instance.CreateRequest<AsyncRequest<AsyncSemanticSegmentationWrite>>();
            asyncRequest.data = new AsyncSemanticSegmentationWrite()
            {
                dataArray = data.ToArray(),
                width = m_LabelingTexture.width,
                height = m_LabelingTexture.height,
                path = path
            };
            asyncRequest.Start((r) =>
            {
                Profiler.EndSample();
                Profiler.BeginSample("Encode");
                var pngBytes = ImageConversion.EncodeArrayToPNG(r.data.dataArray, GraphicsFormat.R8G8B8A8_UNorm, (uint)r.data.width, (uint)r.data.height);
                Profiler.EndSample();
                Profiler.BeginSample("WritePng");
                File.WriteAllBytes(r.data.path, pngBytes);
                Manager.Instance.ConsumerFileProduced(r.data.path);
                Profiler.EndSample();
                return AsyncRequest.Result.Completed;
            });
        }

        internal event Action<int,NativeArray<uint>> OnSegmentationImageReceived;

        internal event Action<NativeSlice<uint>, IReadOnlyList<LabelingConfigurationEntry>, int> OnClassCountsReceived;
    }
}
