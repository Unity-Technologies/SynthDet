using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SimViz;
using UnityEngine.SimViz.Sensors;
using UnityEngine.TestTools;

namespace GroundTruthTests
{
    [TestFixture]
    public class BoundingBox2DTests : PassTestBase
    {
        public class ProducesCorrectBoundingBoxesData
        {
            public uint[] m_ClassCountsExpected;
            public RenderedObjectInfo[] m_BoundingBoxesExpected;
            public uint[] m_Data;
            public int m_Stride;
            public string m_Name;
            public ProducesCorrectBoundingBoxesData(uint[] data, RenderedObjectInfo[] boundingBoxesExpected, uint[] classCountsExpected, int stride, string name)
            {
                m_Data = data;
                m_BoundingBoxesExpected = boundingBoxesExpected;
                m_ClassCountsExpected = classCountsExpected;
                m_Stride = stride;
                m_Name = name;
            }

            public override string ToString()
            {
                return m_Name;
            }
        }
        public static IEnumerable ProducesCorrectBoundingBoxesTestCases()
        {
            yield return new ProducesCorrectBoundingBoxesData(
                new uint[]
                {
                    1, 1,
                    1, 1
                }, new[]
                {
                    new RenderedObjectInfo()
                    {
                        boundingBox = new Rect(0, 0, 2, 2),
                        instanceId = 1,
                        labelId = 0,
                        pixelCount = 4
                    }
                }, new uint[]
                {
                    1,
					0
                },
                2,
                "SimpleBox");
            yield return new ProducesCorrectBoundingBoxesData(
                new uint[]
                {
                    1, 0, 2,
                    1, 0, 0
                }, new[]
                {
                    new RenderedObjectInfo()
                    {
                        boundingBox = new Rect(0, 0, 1, 2),
                        instanceId = 1,
                        labelId = 0,
                        pixelCount = 2
                    },
                    new RenderedObjectInfo()
                    {
                        boundingBox = new Rect(2, 0, 1, 1),
                        instanceId = 2,
                        labelId = 1,
                        pixelCount = 1
                    }
                }, new uint[]
                {
                    1,
                    1
                },
                3,
                "WithGaps");
            yield return new ProducesCorrectBoundingBoxesData(
                new uint[]
                {
                    1, 2, 1,
                    1, 2, 1
                }, new[]
                {
                    new RenderedObjectInfo()
                    {
                        boundingBox = new Rect(0, 0, 3, 2),
                        instanceId = 1,
                        labelId = 0,
                        pixelCount = 4
                    },
                    new RenderedObjectInfo()
                    {
                        boundingBox = new Rect(1, 0, 1, 2),
                        instanceId = 2,
                        labelId = 1,
                        pixelCount = 2
                    }
                }, new uint[]
                {
                    1,
                    1
                },
                3,
                "Interleaved");
        }

        [UnityTest]
        public IEnumerator ProducesCorrectBoundingBoxes([ValueSource(nameof(ProducesCorrectBoundingBoxesTestCases))]ProducesCorrectBoundingBoxesData producesCorrectBoundingBoxesData)
        {
            var label = "label";
            var label2 = "label2";
            var labelingConfiguration = ScriptableObject.CreateInstance<LabelingConfiguration>();

            labelingConfiguration.LabelingConfigurations = new List<LabelingConfigurationEntry>
            {
                new LabelingConfigurationEntry
                {
                    label = label,
                    value = 500
                },
                new LabelingConfigurationEntry
                {
                    label = label2,
                    value = 500
                }
            };

            var labelHistogramPass = new CpuLabelingObjectInfoPass(labelingConfiguration);
            labelHistogramPass.EnsureActivated();

            //Put a plane in front of the camera
            AddTestObjectForCleanup(TestHelper.CreateLabeledPlane(.1f, label));
            AddTestObjectForCleanup(TestHelper.CreateLabeledPlane(.1f, label2));
            yield return null;

            var dataNativeArray = new NativeArray<uint>(producesCorrectBoundingBoxesData.m_Data, Allocator.Persistent);

            labelHistogramPass.Compute(dataNativeArray, producesCorrectBoundingBoxesData.m_Stride, BoundingBoxOrigin.BottomLeft, out var boundingBoxes, out var classCounts);

            CollectionAssert.AreEqual(producesCorrectBoundingBoxesData.m_BoundingBoxesExpected, boundingBoxes.ToArray());
            CollectionAssert.AreEqual(producesCorrectBoundingBoxesData.m_ClassCountsExpected, classCounts.ToArray());

            dataNativeArray.Dispose();
            boundingBoxes.Dispose();
            classCounts.Dispose();
            labelHistogramPass.Cleanup();
            labelHistogramPass.Dispose();
        }
    }
}
