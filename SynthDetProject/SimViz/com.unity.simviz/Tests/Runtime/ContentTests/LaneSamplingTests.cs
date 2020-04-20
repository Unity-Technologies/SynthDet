using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Pipeline;
using UnityEngine.SimViz.Content.Sampling;
using UnityEngine.XR;

namespace UnityEngine.SimViz.Content.ContentTests
{

    [TestFixture]
    public class LaneSamplingTests
    {
#if UNITY_EDITOR
        private const float k_Delta = 0.001f;

        static void AssertSamplesEqual(PointSampleGlobal sampleExpected, PointSampleGlobal sampleActual,
            float positionTolerance = k_Delta, float rotationTolerance = k_Delta)
        {
            var posDelta = sampleExpected.pose.pos - sampleActual.pose.pos;
            var posDeltaMagnitude = math.sqrt(math.dot(posDelta, posDelta));
            Assert.AreEqual(0f, posDeltaMagnitude, positionTolerance,
                "Sample positions are different. Expected " +
                $"{sampleExpected.pose.pos.x:E}, {sampleExpected.pose.pos.y:E}, {sampleExpected.pose.pos.z:E}, " +
                $"was {sampleActual.pose.pos.x:E}, {sampleActual.pose.pos.y:E}, {sampleActual.pose.pos.z:E}");
            var rotProduct = math.dot(sampleExpected.pose.rot, sampleActual.pose.rot);
            // NOTE: The closer the dot product is to 1, the more similar two quaternions are
            Assert.AreEqual(1f, math.abs(rotProduct), rotationTolerance,
                "Sample quaternions were insufficiently similar.");
        }

        static void AssertSamplesEqual(PointSampleLocal sampleExpected, PointSampleLocal sampleActual,
            float positionTolerance = k_Delta, float rotationTolerance = k_Delta)
        {
            var posDelta = sampleExpected.position - sampleActual.position;
            var posDeltaMagnitude = math.sqrt(math.dot(posDelta, posDelta));
            Assert.AreEqual(0f, posDeltaMagnitude, positionTolerance,
                "Sample positions are different. Expected " +
                $"{sampleExpected.position.x:E}, {sampleExpected.position.y:E}, " +
                $"was {sampleActual.position.x:E}, {sampleActual.position.y:E}");
            Assert.AreEqual(sampleExpected.headingRadians, sampleActual.headingRadians, rotationTolerance);
        }

        static Geometry ConstructGeometry(
            GeometryKind geometryKind, RigidTransform pose, float length, float sAbsolute,
            ArcData arcData = default, SpiralData spiralData = default,
            Poly3Data poly3Data = default, ParamPoly3Data paramPoly3Data = default)
        {
            return new Geometry()
            {
                length = length,
                sRoad = sAbsolute,
                startPose = pose,
                geometryKind = geometryKind,
                arcData = arcData,
                spiralData = spiralData,
                poly3Data = poly3Data,
                paramPoly3Data = paramPoly3Data
            };
        }

        static PointSampleGlobal ConstructPointSampleGlobal(quaternion orientation = new quaternion(),
            float3 position = new float3())
        {
            return new PointSampleGlobal(ConstructRigidTransform(orientation, position));
        }

        static RigidTransform ConstructRigidTransform(quaternion orientation = new quaternion(),
            float3 position = new float3())
        {
            // The identity quaternion can't be set as default because it isn't compile-time const, and the empty
            // constructor assigns zeros to all values, so we have to check for default construction and set properly
            if (math.all(orientation.value == float4.zero))
            {
                orientation = quaternion.identity;
            }
            return new RigidTransform(orientation, position);
        }

        // Because all of these tests were written assuming open drive coordinates, but our geometry is in
        // Unity coordinates, rather than re-write all the test cases, we just convert from OpenDRIVE to Unity here
        static Geometry ConstructGeometryFromOpenDriveCoordinates(
            GeometryKind geometryKind, float headingDegrees, float length, float sAbsolute, float x, float z,
            ArcData arcData = default, SpiralData spiralData = default,
            Poly3Data poly3Data = default, ParamPoly3Data paramPoly3Data = default)
        {
            var pose = OpenDriveMapElementFactory.FromOpenDriveGlobalToUnity(math.radians(headingDegrees), x, z);
            return ConstructGeometry(geometryKind, pose, length, sAbsolute,
                arcData, spiralData, poly3Data, paramPoly3Data);
        }

        static PointSampleGlobal ConstructPointSampleFromOpenDriveCoordinates(Vector2 position, float heading)
        {
            var poseUnity = OpenDriveMapElementFactory.FromOpenDriveGlobalToUnity(
                math.radians(heading), position.x, position.y);
            return new PointSampleGlobal(poseUnity);
        }

        static void AssertLaneSamplesEqual(PointSampleLocal sampleExpected, PointSampleLocal sampleActual)
        {
            Assert.IsTrue(Mathf.Approximately(0, sampleActual.position.x),
                $"Lane samples should always have an x-value of zero, but got {sampleActual.position.x}");
            Assert.IsTrue(Mathf.Approximately(sampleExpected.position.y, sampleActual.position.y) &&
                          Mathf.Approximately(sampleExpected.slope, sampleActual.slope),
                $"Samples are different. Expected {sampleExpected}, got {sampleActual}");
        }

        [Test]
        [TestCase(1, 0f, 0f, 0f, 0f, /*sample*/ 0f, /*slopeExpected:g*/ 0f, /*widthExpected*/ 0f)]
        [TestCase(1, 0f, 0f, 0f, 0f, /*sample*/ 100f, /*slopeExpected:g*/ 0f, /*widthExpected*/ 0f)]
        [TestCase(1, 0f, 1f, 0f, 0f, /*sample*/ 10f, /*slopeExpected:g*/ 1f, /*widthExpected*/ 10f)]
        [TestCase(-1, 0f, 1f, 0f, 0f, /*sample*/ 10f, /*slopeExpected:g*/ -1f, /*widthExpected*/ -10f)]
        [TestCase(1, 0f, 0f, 1f, 0f, /*sample*/ 10f, /*slopeExpected:g*/ 20f, /*widthExpected*/ 100f)]
        [TestCase(1, 0f, 0f, 0f, 1f, /*sample*/ 10f, /*slopeExpected:g*/ 300f, /*widthExpected*/ 1000f)]
        public void SampleLane_ReturnsCorrectLaneSample(
            int id, float a, float b, float c, float d, float s, float slopeExpected, float widthExpected)
        {
            var lane = new Lane(id, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, a, b, c, d),});
            var laneSample = GeometrySampling.BuildLaneSample(lane, 0, s);

            var laneSampleExpected =
                new PointSampleLocal(0, widthExpected, math.atan(slopeExpected));

            AssertLaneSamplesEqual(laneSampleExpected, laneSample);
        }

        public static IEnumerable<Tuple<Road, float, Side>> LaneSamplingTestCaseInputs()
        {
            var samplesPerMeter = 0.5f;
            var line = ConstructGeometryFromOpenDriveCoordinates(
                geometryKind: GeometryKind.Line,
                headingDegrees: 0,
                length: 3.5f,
                sAbsolute: 0f,
                x: 0f,
                z: 0f);

            // NOTE: We expect the test to ignore this second line since it is its own lane section
            var line2 = ConstructGeometryFromOpenDriveCoordinates(

                geometryKind : GeometryKind.Line,
                headingDegrees : 0,
                length : 3.5f,
                sAbsolute : 3.5f,
                x : 3.5f,
                z : 0f
            );

            var centerLane = new Lane(0, LaneType.None, default);
            var rightLane1 = new Lane(-1, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, 0, 1, 0, 0),});
            var laneSectionOneRightLane = new LaneSection(0, new Lane[0], centerLane, new[] {rightLane1});

            var roadLineOneRightLane = new Road("", 7f, "",
                new List<Geometry>(){line, line2},
                new List<LaneSection>(){laneSectionOneRightLane},
                new List<RoadElevationProfile>(),
                new List<LaneOffset>(),
                default, default);

            yield return Tuple.Create(roadLineOneRightLane, samplesPerMeter, Side.Right);


            var rightLane2 = new Lane(-2, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, .5f, 1, 0, 0),});
            var laneSectionTwoRightLanes = new LaneSection(0, new Lane[0], centerLane,
                new[] {rightLane1, rightLane2});

            var roadLineTwoRightLanes = new Road("", 3.5f, "",
                new List<Geometry>(){line},
                new List<LaneSection>(){laneSectionTwoRightLanes},
                new List<RoadElevationProfile>(),
                new List<LaneOffset>(),
                default, default);

            yield return Tuple.Create(roadLineTwoRightLanes, samplesPerMeter, Side.Right);


            var leftLane1 = new Lane(1, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, .5f, .5f, 0, 0),});
            var leftLane2 = new Lane(2, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, 0, 1, 0, 0),});
            var laneSectionTwoLeftLanes = new LaneSection(0, new[] {leftLane1, leftLane2}, centerLane, new Lane[0]);

            var roadLineTwoLeftLanes = new Road("", 3.5f, "",
                new List<Geometry>(){line},
                new List<LaneSection>(){laneSectionTwoLeftLanes},
                new List<RoadElevationProfile>(),
                new List<LaneOffset>(),
                default, default);

            yield return Tuple.Create(roadLineTwoLeftLanes, samplesPerMeter, Side.Left);


            var arc = ConstructGeometryFromOpenDriveCoordinates(
                geometryKind : GeometryKind.Arc,
                headingDegrees : 0,
                length : 2 * Mathf.PI,
                sAbsolute : 0f,
                x : 0f,
                z : 0f,
                arcData : new ArcData(1)
            );
            var rightLaneConst = new Lane(-1, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, .5f, 0, 0, 0),});
            var laneSectionOneConstRightLane = new LaneSection(0, new Lane[0], centerLane, new[] {rightLaneConst});

            var roadArcOneRightLane = new Road("", 2 * math.PI, "",
                new List<Geometry>(){arc},
                new List<LaneSection>(){laneSectionOneConstRightLane},
                new List<RoadElevationProfile>(),
                new List<LaneOffset>(),
                default, default);

            yield return Tuple.Create(roadArcOneRightLane, 1 / math.PI, Side.Right);
        }

        public static IEnumerable<PointSampleGlobal[]> LaneEdgeSamplingOutputs()
        {
            var samplesLineOneRightLane = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, 0), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, -2), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(4, -4), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(6, -6), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(7, -7), -45)
            };
            yield return samplesLineOneRightLane;

            var samplesLineTwoRightLanes = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, 0), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, -2), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, -3.5f), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -.5f), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, -4.5f), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, -7.5f), -45),
            };
            yield return samplesLineTwoRightLanes;

            var samplesLineTwoLeftLanes = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, .5f), 45/2),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, 1.5f), 45/2),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, 2.25f), 45/2),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, .5f), 45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, 3.5f), 45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, 5.75f), 45)
            };
            yield return samplesLineTwoLeftLanes;

            var samplesArcOneRightLane = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -.5f), 0),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, 2.5f), 180),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -0.5f), 0)
            };
            yield return samplesArcOneRightLane;
        }

        public static IEnumerable<TestCaseData> LaneEdgeSamplingTestCases()
        {
            return LaneSamplingTestCaseInputs().Zip(LaneEdgeSamplingOutputs(),
                (ins, outs) => new TestCaseData(ins.Item1, ins.Item2, ins.Item3, outs));
        }

        [Test]
        [TestCaseSource(nameof(LaneEdgeSamplingTestCases))]
        public void SampleLanes_ReturnsCorrectValues(Road road, float samplesPerMeter, Side side,
            PointSampleGlobal[] samplesExpected)
        {
            var numSamplesExpected = samplesExpected.Length;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var samplesActual = GeometrySampling.BuildSamplesForLaneSectionSide(road, samplingState, side);

            Assert.AreEqual(numSamplesExpected, samplesActual.Length);
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            samplesActual.Dispose();
        }

        [Test]
        [TestCaseSource(nameof(LaneEdgeSamplingTestCases))]
        public void SampleLanesEcs_ReturnsCorrectValues(Road road, float samplesPerMeter, Side side,
            PointSampleGlobal[] samplesExpected)
        {
            var roadData = ConvertToEcs(road, out var pipeline);

            var numSamplesExpected = samplesExpected.Length;
            var samplingState = new SamplingStateEcsRoad(roadData, samplesPerMeter);
            var samplesActual = GeometrySampling.BuildSamplesForLaneSectionSide(roadData, samplingState, side);

            Assert.AreEqual(numSamplesExpected, samplesActual.Length);
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            pipeline.Dispose();
            samplesActual.Dispose();
        }

        static EcsRoadData ConvertToEcs(Road road, out ContentPipeline pipeline)
        {
            var roadNetworkDescription = ScriptableObject.CreateInstance<RoadNetworkDescription>();
            roadNetworkDescription.SetRoadsAndJunctions(new List<Road>() { road }, new List<Junction>());

            pipeline = new ContentPipeline();
            pipeline.RunGenerator<RoadNetworkDescriptionToEcsSystem, RoadNetworkDescriptionToEcsSystemParameters>(
                new RoadNetworkDescriptionToEcsSystemParameters
                {
                    roadNetworkDescription = roadNetworkDescription
                });

            var roads = pipeline.World.EntityManager.CreateEntityQuery(typeof(EcsRoad)).ToEntityArray(Allocator.TempJob);
            Assert.AreEqual(1, roads.Length);

            var entity = roads[0];
            var roadData = EcsRoadData.Create(pipeline.World.EntityManager, entity);
            roads.Dispose();
            return roadData;
        }

        public static IEnumerable<PointSampleGlobal[]> LaneInsideSamplingOutputs()
        {
            var samplesLineOneRightLane = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, 0), -22.5f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, -1), -22.5f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(4, -2), -22.5f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(6, -3), -22.5f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(7, -3.5f), -22.5f)
            };
            yield return samplesLineOneRightLane;

            var samplesLineTwoRightLanes = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -0.25f), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, -3.25f), -45),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, -5.5f), -45),
            };
            yield return samplesLineTwoRightLanes;

            var samplesLineTwoLeftLanes = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, .5f), 33.75f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(2, 2.5f), 33.75f),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(3.5f, 4), 33.75f)
            };
            yield return samplesLineTwoLeftLanes;

            var samplesArcOneRightLane = new[]
            {
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -.25f), 0),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, 2.25f), 180),
                ConstructPointSampleFromOpenDriveCoordinates(new Vector2(0, -0.25f), 0)
            };
            yield return samplesArcOneRightLane;
        }

        public static IEnumerable<TestCaseData> LaneInsideSamplingTestCases()
        {
            return LaneSamplingTestCaseInputs().Zip(LaneInsideSamplingOutputs(),
                (ins, outs) => new TestCaseData(ins.Item1, ins.Item2, ins.Item3, outs));
        }

        [Test]
        [TestCaseSource(nameof(LaneInsideSamplingTestCases))]
        public void SampleInsideLanes_ReturnsCorrectValues(Road road, float samplesPerMeter, Side side,
            PointSampleGlobal[] samplesExpected)
        {
            int idToSampleUntil;
            idToSampleUntil = side == Side.Left ?
                road.laneSections[0].leftLanes.Last().id : road.laneSections[0].rightLanes.Last().id;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var numSamples = GeometrySampling.ComputeNumSamplesForRoadLength(road.length, samplesPerMeter);
            var numSamplesPerSection = numSamples / road.laneSections.Count;
            var samplesActual = new NativeArray<PointSampleGlobal>(numSamples, Allocator.Temp);
            var outputIdx = 0;
            for (var sectionIdx = 0; sectionIdx < road.laneSections.Count; ++sectionIdx)
            {
                // Adjust the final section's sample count in case there was a truncation error
                if (sectionIdx == road.laneSections.Count - 1)
                {
                    numSamplesPerSection = numSamples - outputIdx;
                }

                if (road.laneSections[sectionIdx].HasLane(idToSampleUntil))
                {
                    GeometrySampling.BuildSamplesInsideLaneSection(road, idToSampleUntil, numSamplesPerSection, outputIdx, TraversalDirection.Forward,
                        ref samplingState, ref samplesActual);
                }
                else
                {
                    Debug.LogWarning($"Lane {idToSampleUntil} does exist in the entirety of Road {road.roadId} " +
                                     "Some samples for this road will be down the center-line.");
                    GeometrySampling.BuildSamplesInsideLaneSection(road, 0, numSamplesPerSection, outputIdx, TraversalDirection.Forward,
                        ref samplingState, ref samplesActual);
                }

                outputIdx += numSamplesPerSection;
            }
            Assert.AreEqual(samplesExpected.Length, samplesActual.Length, "Number of samples returned was wrong.");
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            samplesActual.Dispose();
        }

        [Test]
        [TestCaseSource(nameof(LaneInsideSamplingTestCases))]
        public void SampleInsideLanes_Ecs_ReturnsCorrectValues(Road road, float samplesPerMeter, Side side,
            PointSampleGlobal[] samplesExpected)
        {
            var roadData = ConvertToEcs(road, out var pipeline);

            int idToSampleUntil;
            idToSampleUntil = side == Side.Left ?
                road.laneSections[0].leftLanes.Last().id : road.laneSections[0].rightLanes.Last().id;
            var samplingState = new SamplingStateEcsRoad(roadData, samplesPerMeter);
            var numSamples = GeometrySampling.ComputeNumSamplesForRoadLength(roadData.ecsRoad.length, samplesPerMeter);
            var numSamplesPerSection = numSamples / roadData.ecsLaneSections.Length;
            var samplesActual = new NativeArray<PointSampleGlobal>(numSamples, Allocator.Temp);
            var outputIdx = 0;
            for (var sectionIdx = 0; sectionIdx < road.laneSections.Count; ++sectionIdx)
            {
                // Adjust the final section's sample count in case there was a truncation error
                if (sectionIdx == road.laneSections.Count - 1)
                {
                    numSamplesPerSection = numSamples - outputIdx;
                }

                if (road.laneSections[sectionIdx].HasLane(idToSampleUntil))
                {
                    GeometrySampling.BuildSamplesInsideLaneSection(roadData, idToSampleUntil, numSamplesPerSection, outputIdx, TraversalDirection.Forward,
                        ref samplingState, ref samplesActual);
                }
                else
                {
                    Debug.LogWarning($"Lane {idToSampleUntil} does exist in the entirety of Road {road.roadId} " +
                                     "Some samples for this road will be down the center-line.");
                    GeometrySampling.BuildSamplesInsideLaneSection(roadData, 0, numSamplesPerSection, outputIdx, TraversalDirection.Forward,
                        ref samplingState, ref samplesActual);
                }

                outputIdx += numSamplesPerSection;
            }
            Assert.AreEqual(samplesExpected.Length, samplesActual.Length, "Number of samples returned was wrong.");
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            pipeline.Dispose();
            samplesActual.Dispose();
        }

        [Test]
        public void SampleLanes_WithLaneOffsets_ReturnsCorrectValues()
        {
            var laneOffsets = new List<LaneOffset>()
            {
                new LaneOffset(0,new Vector4(1, .3f, .2f, .1f)),
            };
            var line = ConstructGeometryFromOpenDriveCoordinates(
                geometryKind : GeometryKind.Line,
                headingDegrees : 0,
                length : 2f,
                sAbsolute : 0f,
                x : 0f,
                z : 0f
            );
            var laneSectionOuter = new Lane(1, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, 1, 0, 0, 0),});
            var laneSection = new LaneSection(0, new[] {laneSectionOuter},
                new Lane(0, default, default), new Lane[0]);

            Road road = new Road("", 2f, "",
                new List<Geometry> {line},
                new List<LaneSection> {laneSection},
                new List<RoadElevationProfile>(),
                laneOffsets,
                default, default);

            var samplesExpected = new[]
            {
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(-2f, 0, 0))),
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(-4.2f, 0, 2))),
            };

            var numSamplesExpected = samplesExpected.Length;
            var samplesPerMeter = 0.5f;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var samplesActual = GeometrySampling.BuildSamplesForLaneSectionSide(road, samplingState, Side.Left);

            Assert.AreEqual(numSamplesExpected, samplesActual.Length);
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            samplesActual.Dispose();
        }

        [Test]
        public void SampleLanes_Ecs_WithLaneOffsets_ReturnsCorrectValues()
        {
            var line = ConstructGeometryFromOpenDriveCoordinates(
                geometryKind : GeometryKind.Line,
                headingDegrees : 0,
                length : 2f,
                sAbsolute : 0f,
                x : 0f,
                z : 0f
            );

            var world = new World(nameof(SampleLanes_Ecs_WithLaneOffsets_ReturnsCorrectValues));
            var roadEntity = world.EntityManager.CreateEntity(EcsRoadData.CreateRoadArchetype(world.EntityManager));
            world.EntityManager.SetComponentData(roadEntity, new EcsRoad()
            {
                length = 2f,
                name = ""
            });
            world.EntityManager.GetBuffer<Geometry>(roadEntity).Add(line);
            var lanes = world.EntityManager.GetBuffer<EcsLane>(roadEntity);
            var laneWidthRecords = world.EntityManager.GetBuffer<LaneWidthRecord>(roadEntity);
            laneWidthRecords.Add(new LaneWidthRecord(false, 0, 1, 0, 0, 0));
            lanes.Add(new EcsLane()
            {
                id = 1,
                firstLaneWidthRecordIndex = 0,
                laneWidthRecordCount = 1
            });
            world.EntityManager.GetBuffer<EcsLaneSection>(roadEntity).Add(new EcsLaneSection()
            {
                firstLaneIndex = 0,
                centerLaneIndex = 1,
                laneCount = 3,
                sRoad = 0
            });

            var laneOffsets = new List<LaneOffset>()
            {
                new LaneOffset(0,new Vector4(1, .3f, .2f, .1f)),
            };
            var laneSectionOuter = new Lane(1, LaneType.None, default,
                new[] {new LaneWidthRecord(false, 0, 1, 0, 0, 0),});
            var laneSection = new LaneSection(0, new[] {laneSectionOuter},
                new Lane(0, default, default), new Lane[0]);

            Road road = new Road("", 2f, "",
                new List<Geometry> {line},
                new List<LaneSection> {laneSection},
                new List<RoadElevationProfile>(),
                laneOffsets,
                default, default);

            var samplesExpected = new[]
            {
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(-2f, 0, 0))),
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(-4.2f, 0, 2))),
            };

            var numSamplesExpected = samplesExpected.Length;
            var samplesPerMeter = 0.5f;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var samplesActual = GeometrySampling.BuildSamplesForLaneSectionSide(road, samplingState, Side.Left);

            Assert.AreEqual(numSamplesExpected, samplesActual.Length);
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }

            world.Dispose();
            samplesActual.Dispose();
        }

        static internal IEnumerable<TestCaseData> RoadOutlineTestCases()
        {
            var emptyPose = ConstructRigidTransform();
            var centerLane = new Lane(0, default, default);
            var simpleLine = ConstructGeometry(GeometryKind.Line, emptyPose, 1, 0);
            var simpleWidthRecord = new LaneWidthRecord(false, 0, 1, 0, 0, 0);
            var simpleLeftLane = new Lane(1, LaneType.Border, default, new[] {simpleWidthRecord});
            var simpleRightLane = new Lane(-1, LaneType.Border, default, new[] {simpleWidthRecord});
            var simpleLaneSection = new LaneSection(0, new[] {simpleLeftLane}, centerLane, new[] {simpleRightLane});
            var twoSimpleLanes = new List<LaneSection>(new[] {simpleLaneSection});
            var oneSimpleLineGeometry = new List<Geometry>(new [] {simpleLine});
            var simpleRoad = new Road("1", 1, "", oneSimpleLineGeometry, twoSimpleLanes,
                new List<RoadElevationProfile>(), new List<LaneOffset>(), default, default);
            var simpleSamples = new List<PointSampleGlobal>
            {
                ConstructPointSampleGlobal(position: new float3(1, 0, 0)),
                ConstructPointSampleGlobal(position: new float3(1, 0, 1)),
                ConstructPointSampleGlobal(position: new float3(-1, 0, 1)),
                ConstructPointSampleGlobal(position: new float3(-1, 0, 0)),
            };
            yield return new TestCaseData(simpleRoad, 1.0f, simpleSamples).SetDescription(
                "One line two lanes.");

            var secondSimpleLineGeometry = ConstructGeometry(GeometryKind.Line,
                ConstructRigidTransform(position: new float3(0, 0, 1)), 1, 1);
            var twoSimpleLineGeometries = oneSimpleLineGeometry.Append(secondSimpleLineGeometry).ToList();
            var twoSimpleLaneSections = twoSimpleLanes.Append(simpleLaneSection).ToList();
            var secondSimpleRoad = new Road("2", 2, "", twoSimpleLineGeometries, twoSimpleLaneSections,
                new List<RoadElevationProfile>(), new List<LaneOffset>(), default, default);
            var moreSimpleSamples = new List<PointSampleGlobal>(simpleSamples);
            moreSimpleSamples.InsertRange(2, new[]
            {
                ConstructPointSampleGlobal(position: new float3(1, 0, 2)),
                ConstructPointSampleGlobal(position: new float3(-1, 0, 2))
            });
            yield return new TestCaseData(secondSimpleRoad, 1.0f, moreSimpleSamples).SetDescription(
                "Two lines two lanes.");

            var simpleLaneOffset = new List<LaneOffset> {new LaneOffset(0, new float4(1, 0, 0, 0))};
            var oneLeftLane = new List<LaneSection>
            {
                new LaneSection(0, new[] {simpleLeftLane}, centerLane, new Lane[0])
            };
            var offsetSimpleRoad = new Road("1", 1, "", oneSimpleLineGeometry, oneLeftLane,
                new List<RoadElevationProfile>(), simpleLaneOffset, default, default);
            var offsetSamples = new List<PointSampleGlobal>(new[]
            {
                ConstructPointSampleGlobal(position: new float3(-1, 0, 0)),
                ConstructPointSampleGlobal(position: new float3(-1, 0, 1)),
                ConstructPointSampleGlobal(position: new float3(-2, 0, 1)),
                ConstructPointSampleGlobal(position: new float3(-2, 0, 0)),
            });
            yield return new TestCaseData(offsetSimpleRoad, 1.0f, offsetSamples).SetDescription(
                "One offset line, one left lane");
        }

        [Test]
        [TestCaseSource(nameof(RoadOutlineTestCases))]
        public void SampleRoadOutline_ReturnsCorrectValues(Road roadIn, float samplesPerMeter,
            List<PointSampleGlobal> samplesExpected)
        {
            var samplesNativeArray = GeometrySampling.BuildSamplesFromRoadOutline(roadIn, samplesPerMeter);
            var samplesActual = samplesNativeArray.ToList();
            Assert.AreEqual(samplesExpected.Count, samplesActual.Count);
            foreach (var (expected, actual) in samplesExpected.Zip(samplesActual, Tuple.Create))
            {
                AssertSamplesEqual(expected, actual);
            }
            samplesNativeArray.Dispose();
        }
#endif
    }
}
