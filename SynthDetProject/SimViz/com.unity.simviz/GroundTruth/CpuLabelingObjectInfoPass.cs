using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SimViz.Sensors;

namespace UnityEngine.SimViz
{
    public class CpuLabelingObjectInfoPass : GroundTruthCrossPipelinePass, IDisposable
    {
        static ProfilerMarker s_LabelJobs = new ProfilerMarker("Label Jobs");
        static ProfilerMarker s_LabelMerge = new ProfilerMarker("Label Merge");

        struct Object1DSpan
        {
            public int instanceId;
            public int row;
            public int left;
            public int right;
        }
        [BurstCompile]
        struct ComputeHistogramPerRowJob : IJob
        {
            [ReadOnly]
            public NativeSlice<uint> segmentationImageData;
            public int width;
            public int rows;
            public int rowStart;
            [NativeDisableContainerSafetyRestriction]
            public NativeList<Object1DSpan> boundingBoxes;

            public void Execute()
            {
                for (int row = 0; row < rows; row++)
                {
                    var rowSlice = new NativeSlice<uint>(segmentationImageData, width * row, width);
                    var currentBB = new Object1DSpan
                    {
                        instanceId = -1,
                        row = row + rowStart
                    };
                    for (int i = 0; i < rowSlice.Length; i++)
                    {
                        uint value = rowSlice[i];

                        if (value != currentBB.instanceId)
                        {
                            if (currentBB.instanceId > 0)
                            {
                                //save off currentBB
                                currentBB.right = i - 1;
                                boundingBoxes.Add(currentBB);
                            }

                            currentBB = new Object1DSpan
                            {
                                instanceId = (int)value,
                                left = i,
                                row = row + rowStart
                            };
                        }
                    }

                    if (currentBB.instanceId > 0)
                    {
                        //save off currentBB
                        currentBB.right = width - 1;
                        boundingBoxes.Add(currentBB);
                    }
                }
            }
        }

        const int k_StartingObjectCount = 1 << 8;
        public NativeList<int> InstanceIdToClassIdLookup;
        LabelingConfiguration m_LabelingConfiguration;

        public CpuLabelingObjectInfoPass(LabelingConfiguration labelingConfiguration)
            :base(null)
        {
            m_LabelingConfiguration = labelingConfiguration;
            InstanceIdToClassIdLookup = new NativeList<int>(k_StartingObjectCount, Allocator.Persistent);
        }

        protected override void ExecutePass(ScriptableRenderContext renderContext, CommandBuffer cmd, Camera camera, CullingResults cullingResult)
        {
            throw new NotSupportedException();
        }

        public override void SetupMaterialProperties(MaterialPropertyBlock mpb, MeshRenderer meshRenderer, Labeling labeling, uint instanceId)
        {
            if (m_LabelingConfiguration.TryGetMatchingConfigurationIndex(labeling, out var index))
            {
                if (InstanceIdToClassIdLookup.Length <= instanceId)
                {
                    InstanceIdToClassIdLookup.Resize((int)instanceId + 1, NativeArrayOptions.ClearMemory);
                }

                InstanceIdToClassIdLookup[(int)instanceId] = index;
            }
        }

        public void Compute(NativeArray<uint> data, int stride, BoundingBoxOrigin boundingBoxOrigin, out NativeArray<RenderedObjectInfo> boundingBoxes, out NativeArray<uint> classCounts)
        {
            const int k_JobCount = 24;
            int height = data.Length / stride;
            //special math to round up
            int rowsPerJob = height / k_JobCount;
            int rowRemainder = height % k_JobCount;
            var handles = new NativeArray<JobHandle>(k_JobCount, Allocator.Temp);
            var jobBoundingBoxLists = new NativeList<Object1DSpan>[k_JobCount];
            using (s_LabelJobs.Auto())
            {
                //var instanceIdExists = new NativeArray<bool>(m_InstanceIdToClassIdLookup.Length, Allocator.TempJob);
                for (int row = 0, jobIndex = 0; row < height; row += rowsPerJob, jobIndex++)
                {
                    jobBoundingBoxLists[jobIndex] = new NativeList<Object1DSpan>(10, Allocator.TempJob);
                    var rowsThisJob = math.min(height - row, rowsPerJob);
                    if (jobIndex < rowRemainder)
                        rowsThisJob++;

                    handles[jobIndex] = new ComputeHistogramPerRowJob
                    {
                        segmentationImageData = new NativeSlice<uint>(data, row * stride, stride * rowsThisJob),
                        width = stride,
                        rowStart = row,
                        rows = rowsThisJob,
                        boundingBoxes = jobBoundingBoxLists[jobIndex]
                    }.Schedule();

                    if (jobIndex < rowRemainder)
                        row++;
                }

                JobHandle.CompleteAll(handles);
            }

            classCounts = new NativeArray<uint>(m_LabelingConfiguration.LabelingConfigurations.Count, Allocator.Temp);
            NativeHashMap<int, RenderedObjectInfo> boundingBoxMap = new NativeHashMap<int, RenderedObjectInfo>(100, Allocator.Temp);
            using (s_LabelMerge.Auto())
            {
                foreach (var boundingBoxList in jobBoundingBoxLists)
                {
                    if (!boundingBoxList.IsCreated)
                        continue;

                    foreach (var info1D in boundingBoxList)
                    {
                        var objectInfo = new RenderedObjectInfo
                        {
                            boundingBox = new Rect(info1D.left, info1D.row, info1D.right - info1D.left + 1, 1),
                            instanceId = info1D.instanceId,
                            pixelCount = info1D.right - info1D.left + 1
                        };

                        if (boundingBoxMap.TryGetValue(info1D.instanceId, out RenderedObjectInfo info))
                        {
                            objectInfo.boundingBox = Rect.MinMaxRect(
                                math.min(info.boundingBox.xMin, objectInfo.boundingBox.xMin),
                                math.min(info.boundingBox.yMin, objectInfo.boundingBox.yMin),
                                math.max(info.boundingBox.xMax, objectInfo.boundingBox.xMax),
                                math.max(info.boundingBox.yMax, objectInfo.boundingBox.yMax));
                            objectInfo.pixelCount += info.pixelCount;
                        }

                        boundingBoxMap[info1D.instanceId] = objectInfo;
                    }
                }

                var keyValueArrays = boundingBoxMap.GetKeyValueArrays(Allocator.Temp);
                boundingBoxes = new NativeArray<RenderedObjectInfo>(keyValueArrays.Keys.Length, Allocator.Temp);
                for (var i = 0; i < keyValueArrays.Keys.Length; i++)
                {
                    var instanceId = keyValueArrays.Keys[i];
                    if (InstanceIdToClassIdLookup.Length <= instanceId)
                        continue;

                    var classId = InstanceIdToClassIdLookup[instanceId];
                    classCounts[classId]++;
                    var renderedObjectInfo = keyValueArrays.Values[i];
                    var boundingBox = renderedObjectInfo.boundingBox;
                    if (boundingBoxOrigin == BoundingBoxOrigin.TopLeft)
                    {
                        var y = height - boundingBox.yMax;
                        boundingBox = new Rect(boundingBox.x, y, boundingBox.width, boundingBox.height);
                    }
                    boundingBoxes[i] = new RenderedObjectInfo
                    {
                        instanceId = instanceId,
                        labelId = classId,
                        boundingBox = boundingBox,
                        pixelCount = renderedObjectInfo.pixelCount
                    };
                }
                keyValueArrays.Dispose();
            }

            //instanceIdExists.Dispose();
            boundingBoxMap.Dispose();
            foreach (var rowBoundingBox in jobBoundingBoxLists)
            {
                if (rowBoundingBox.IsCreated)
                    rowBoundingBox.Dispose();
            }

            handles.Dispose();
        }

        public void Dispose()
        {
            InstanceIdToClassIdLookup.Dispose();
        }
    }
}
