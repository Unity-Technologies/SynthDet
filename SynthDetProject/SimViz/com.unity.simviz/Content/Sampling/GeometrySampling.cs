using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Pipeline;

namespace UnityEngine.SimViz.Content.Sampling
{
    public enum Side { Center, Left, Right }

    internal struct SegmentOutline
    {
        internal SampleType sampleType;
        internal NativeList<PointSampleGlobal> samples;

        internal SegmentOutline(SampleType sampleType, int numSamples, Allocator allocator = Allocator.Temp)
        {
            this.sampleType = sampleType;
            samples = new NativeList<PointSampleGlobal>(numSamples, allocator);
        }
    }

    internal struct SimpsonsRuleValues
    {
        // accumulated arc length along the polynomial being sampled
        internal float s;
        internal float l;
        internal float p;

        public SimpsonsRuleValues(float s, float l, float p)
        {
            this.s = s;
            this.l = l;
            this.p = p;
        }

        internal void SetAll(float s, float l, float p)
        {
            this.s = s;
            this.l = l;
            this.p = p;
        }
    }

    internal struct GeometrySamplingParameters
    {
        // We will likely need to iterate on the component that computes how many samples are needed per road
        // segment, so to stay flexible we'll allow use of an arbitrary lambda function
        internal Func<Geometry, float> samplingFrequencyComputer;
        internal RoadNetworkDescription roadNetwork;
    }

    public static class GeometrySampling
    {

        internal static NativeArray<PointSampleGlobal> BuildSamplesFromGeometry(
            Geometry geometry,
            float samplesPerMeter,
            Allocator allocator = Allocator.Temp)
        {
            // Add one to count because we start sampling at zero
            var sampleCount =   1 + (int)(geometry.length * samplesPerMeter);

            //Add a sample at the end of the curve if the last
            var sampleAtEnd = !Mathf.Approximately(geometry.length, sampleCount * samplesPerMeter);

            var totalSampleCount = sampleAtEnd ? sampleCount + 1 : sampleCount;
            var state = new SamplingStateGeometry(geometry);

            NativeArray<PointSampleGlobal> samples = new NativeArray<PointSampleGlobal>(totalSampleCount, allocator);
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = geometry.Sample(i / samplesPerMeter + geometry.sRoad, ref state);
                samples[i] = sample;
            }

            if (sampleAtEnd)
                samples[totalSampleCount - 1] = geometry.Sample(geometry.length + geometry.sRoad, ref state);
            return samples;
        }

        internal static void BuildSamplesInsideLaneSection(
            EcsRoadData road, int laneId, int numSamplesSection, int outputIdx, TraversalDirection samplingDirection,
            ref SamplingStateEcsRoad samplingState, ref NativeArray<PointSampleGlobal> samplesOut)
        {
            var numSamplesOut = samplesOut.Length;
            var numEdgesToSample = math.abs(laneId);
            var numSamplesEdge = numEdgesToSample == 0 ? numSamplesOut : numSamplesOut * numEdgesToSample;
            // We will need to sample the center line as well, if we are not between two lane edges
            var shouldSampleCenter = numEdgesToSample <= 1;
            var samplesEdges = new NativeArray<PointSampleGlobal>(numSamplesEdge, Allocator.Temp);
            var side = ToSide(laneId);
            var sectionStart = samplingDirection == TraversalDirection.Forward ? outputIdx : numSamplesSection + outputIdx - 1;
            for (var sampleNum = 0; sampleNum < numSamplesSection; sampleNum++)
            {
                var sampleIdx = sectionStart + sampleNum * (int)samplingDirection;
                var sRoad = samplingState.sRoadLastComputed;
                SampleLanesOneSide(road, samplingState, side, sRoad, sampleIdx, ref samplesEdges,
                    numEdgesToSample);
                // Lane index is lane ID - 1 because ID's start at 1, not 0
                var laneSampleIdx = ComputeLaneSampleIdx(numEdgesToSample - 1, numSamplesOut, sampleIdx);
                var poseOuterEdge = samplesEdges[laneSampleIdx].pose;
                RigidTransform poseInnerEdge;
                if (shouldSampleCenter)
                {
                    poseInnerEdge = SampleCenter(road, samplingState, sRoad).pose;
                }
                else
                {
                    var innerEdgeIdx = ComputeLaneSampleIdx(numEdgesToSample - 2, numSamplesOut, sampleIdx);
                    poseInnerEdge = samplesEdges[innerEdgeIdx].pose;
                }
                var positionMean = math.lerp(poseInnerEdge.pos, poseOuterEdge.pos, 0.5f);
                var rotationMean = math.nlerp(poseInnerEdge.rot, poseOuterEdge.rot, 0.5f);
                samplesOut[sampleIdx] = new PointSampleGlobal(rotationMean, positionMean);

                samplingState.Step(road);
            }

            samplesEdges.Dispose();
        }

        internal static void BuildSamplesFromRoadOutline(EcsRoadData road, float samplesPerMeter, NativeSlice<PointSampleGlobal> allSamples)
        {
            if (road.ecsGeometries.Length == 0)
                throw new Exception($"The road {road} has no reference line.");

            var numTotalSamples = ComputeSampleCountForRoadOutline(road.ecsRoad, samplesPerMeter);
            if (numTotalSamples > allSamples.Length)
                throw new ArgumentException("sample NativeSlice is not large enough to hold all the samples. Use ComputeSampleCountForRoadOutline to determine the proper number of samples.", nameof(allSamples));

            var samplingState = new SamplingStateEcsRoad(road, samplesPerMeter);
            //var numSectionsLeft = Max(road.ecsLaneSections, s => s.LeftLaneCount);
            //var numSectionsRight = Max(road.ecsLaneSections, s => s.RightLaneCount);
            int maxValue = int.MinValue;
            foreach (var laneSection in road.ecsLaneSections)
            {
                maxValue = math.max(maxValue, laneSection.LeftLaneCount);
            }

            var numSectionsLeft = maxValue;
            int maxValue1 = int.MinValue;
            foreach (var laneSection1 in road.ecsLaneSections)
            {
                maxValue1 = math.max(maxValue1, laneSection1.RightLaneCount);
            }

            var numSectionsRight = maxValue1;
            var leftSamplesTemp = new NativeArray<PointSampleGlobal>(numSectionsLeft, Allocator.Temp);
            var rightSamplesTemp = new NativeArray<PointSampleGlobal>(numSectionsRight, Allocator.Temp);
            for (var sampleIdx = 0; sampleIdx < numTotalSamples / 2; sampleIdx++)
            {
                var sRoad = samplingState.sRoadLastComputed;
                // We intentionally do not pass state as a reference here, as each sampling pass will cover the same
                // segment of road. We would need to do some refactoring to sample all lanes for a given s-coordinate
                // in one call in order to use sampling state more effectively
                SampleLanesOneSide(road, samplingState, Side.Left, sRoad, 0, ref leftSamplesTemp);
                SampleLanesOneSide(road, samplingState, Side.Right, sRoad, 0, ref rightSamplesTemp);
                var currentSection = road.ecsLaneSections[samplingState.laneSectionIdx];
                allSamples[sampleIdx] = currentSection.RightLaneCount > 0
                    ? rightSamplesTemp[currentSection.RightLaneCount - 1]
                    : SampleCenter(road, samplingState, sRoad);
                // allSamples must be ordered to form a counter-clockwise outline, so left samples get added
                // in reverse order to the end of the array
                allSamples[numTotalSamples - 1 - sampleIdx] = currentSection.LeftLaneCount > 0
                    ? leftSamplesTemp[currentSection.LeftLaneCount - 1]
                    : SampleCenter(road, samplingState, sRoad);
                var stepSucceeded = samplingState.Step(road);

                // We expect stepping to fail at the end of the current segment, but otherwise this would be a problem
                if (!stepSucceeded && sampleIdx * 2 + 2 != numTotalSamples)
                {
                    Debug.LogWarning($"Reached end of road {road.ecsRoad.name} early. Still have " +
                                     $"{numTotalSamples / 2 - sampleIdx - 1} samples to do.");
                }
            }

            leftSamplesTemp.Dispose();
            rightSamplesTemp.Dispose();
        }

        internal static int ComputeSampleCountForRoadOutline(EcsRoad road, float samplesPerMeter)
        {
            return ComputeNumSamplesForRoadLength(road.length, samplesPerMeter) * 2;
        }

        static int Max(DynamicBuffer<EcsLaneSection> roadEcsLaneSections, Func<EcsLaneSection, int> getValue)
        {
            int maxValue = int.MinValue;
            foreach (var laneSection in roadEcsLaneSections)
            {
                maxValue = math.max(maxValue, getValue(laneSection));
            }

            return maxValue;
        }

        internal static void BuildSamplesInsideLaneSection(
            Road road, int laneId, int numSamplesSection, int outputIdx, TraversalDirection samplingDirection,
            ref SamplingStateRoad samplingState, ref NativeArray<PointSampleGlobal> samplesOut)
        {
            var numSamplesOut = samplesOut.Length;
            var numEdgesToSample = math.abs(laneId);
            var numSamplesEdge = numEdgesToSample == 0 ? numSamplesOut : numSamplesOut * numEdgesToSample;
            // We will need to sample the center line as well, if we are not between two lane edges
            var shouldSampleCenter = numEdgesToSample <= 1;
            var samplesEdges = new NativeArray<PointSampleGlobal>(numSamplesEdge, Allocator.Temp);
            var side = ToSide(laneId);
            var sectionStart = samplingDirection == TraversalDirection.Forward ? outputIdx : numSamplesSection + outputIdx - 1;
            for (var sampleNum = 0; sampleNum < numSamplesSection; sampleNum++)
            {
                var sampleIdx = sectionStart + sampleNum * (int)samplingDirection;
                var sRoad = samplingState.sRoadLastComputed;
                SampleLanesOneSide(road, samplingState, side, sRoad, sampleIdx, ref samplesEdges,
                    numEdgesToSample);
                // Lane index is lane ID - 1 because ID's start at 1, not 0
                var laneSampleIdx = ComputeLaneSampleIdx(numEdgesToSample - 1, numSamplesOut, sampleIdx);
                var poseOuterEdge = samplesEdges[laneSampleIdx].pose;
                RigidTransform poseInnerEdge;
                if (shouldSampleCenter)
                {
                    poseInnerEdge = SampleCenter(road, samplingState, sRoad).pose;
                }
                else
                {
                    var innerEdgeIdx = ComputeLaneSampleIdx(numEdgesToSample - 2, numSamplesOut, sampleIdx);
                    poseInnerEdge = samplesEdges[innerEdgeIdx].pose;
                }
                var positionMean = math.lerp(poseInnerEdge.pos, poseOuterEdge.pos, 0.5f);
                var rotationMean = math.nlerp(poseInnerEdge.rot, poseOuterEdge.rot, 0.5f);
                samplesOut[sampleIdx] = new PointSampleGlobal(rotationMean, positionMean);

                samplingState.Step(road);
            }

            samplesEdges.Dispose();
        }

        internal static NativeArray<PointSampleGlobal> BuildSamplesFromRoadOutline(Road road, float samplesPerMeter)
        {
            if (road.geometry.Count == 0)
                throw new Exception($"The road {road} has no reference line.");
            var numTotalSamples = ComputeNumSamplesForRoadLength(road.length, samplesPerMeter) * 2;
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var numSectionsLeft = road.laneSections.Max(s => s.leftLanes.Count());
            var numSectionsRight = road.laneSections.Max(s => s.rightLanes.Count());
            var leftSamplesTemp = new NativeArray<PointSampleGlobal>(numSectionsLeft, Allocator.Temp);
            var rightSamplesTemp = new NativeArray<PointSampleGlobal>(numSectionsRight, Allocator.Temp);
            var allSamples = new NativeArray<PointSampleGlobal>(numTotalSamples, Allocator.Temp);
            for (var sampleIdx = 0; sampleIdx < numTotalSamples / 2; sampleIdx++)
            {
                var sRoad = samplingState.sRoadLastComputed;
                // We intentionally do not pass state as a reference here, as each sampling pass will cover the same
                // segment of road. We would need to do some refactoring to sample all lanes for a given s-coordinate
                // in one call in order to use sampling state more effectively
                SampleLanesOneSide(road, samplingState, Side.Left, sRoad, 0, ref leftSamplesTemp);
                SampleLanesOneSide(road, samplingState, Side.Right, sRoad, 0, ref rightSamplesTemp);
                var currentSection = road.laneSections[samplingState.laneSectionIdx];
                allSamples[sampleIdx] = currentSection.rightLanes.Any()
                    ? rightSamplesTemp[currentSection.rightLanes.Count() - 1]
                    : SampleCenter(road, samplingState, sRoad);
                // allSamples must be ordered to form a counter-clockwise outline, so left samples get added
                // in reverse order to the end of the array
                allSamples[numTotalSamples - 1 - sampleIdx] = currentSection.leftLanes.Any()
                    ? leftSamplesTemp[currentSection.leftLanes.Count() - 1]
                    : SampleCenter(road, samplingState, sRoad);
                var stepSucceeded = samplingState.Step(road);

                // We expect stepping to fail at the end of the current segment, but otherwise this would be a problem
                if (!stepSucceeded && sampleIdx * 2 + 2 != numTotalSamples)
                {
                    Debug.LogWarning($"Reached end of road {road.roadId} early. Still have " +
                                     $"{numTotalSamples / 2 - sampleIdx - 1} samples to do.");
                }
            }

            leftSamplesTemp.Dispose();
            rightSamplesTemp.Dispose();
            return allSamples;
        }

        internal static void BuildSamplesWithContextualGrouping(
            GeometrySamplingParameters parameters,
            DynamicBuffer<RoadId> roadElements,
            out NativeArray<PointSampleGlobal> samples,
            out NativeArray<int> offsets,
            out NativeArray<SampleType> types)
        {
            // Basic flow --
            // initialize segment outline tracking <-- still need to determine exactly what this data representation is
            // foreach roadId in input list:
            //   get road
            //   foreach geometry in road:
            //     1. Determine number of samples necessary to accurately represent this Geometry's shape
            //     2. In a for loop:
            //         a. Get list of sample points from left to right for given s coordinate
            //         b. Identify points that belong to a particular segment outline and add them as appropriate
            //            >>> The strategy for identifying what is/is not an outline is likely to be very tricky
            //         c. Identify outlines that are no longer being added to and remove them from "active segment" tracking
            //         d. Identify points that may be part of a new outline
            //     3. Iterate through all generated outlines and copy into single out array with corresponding offsets and
            //         types in their respective arrays

            samples = new NativeArray<PointSampleGlobal>();
            offsets = new NativeArray<int>();
            types = new NativeArray<SampleType>();
        }

        /// <summary>
        /// Generates point samples from a road description extended by a specified distance
        /// </summary>
        /// <param name="extensionDistance">Offset applied to the beginning and end of road segment polygons. Increases
        /// the likelihood of polygon overlap to encourage the creation of one contiguous road network polygon.</param>
        public static NativeArray<PointSampleGlobal> BuildSamplesFromRoadOutlineWithExtensionDistance(
            Road road, float samplesPerMeter, float extensionDistance)
        {
            var polygon = BuildSamplesFromRoadOutline(road, samplesPerMeter);
            UpdateWithExtensionDistance(extensionDistance, polygon);

            return polygon;
        }

        /// <summary>
        /// Generates point samples from a road description extended by a specified distance
        /// </summary>
        /// <param name="extensionDistance">Offset applied to the beginning and end of road segment polygons. Increases
        /// the likelihood of polygon overlap to encourage the creation of one contiguous road network polygon.</param>
        public static NativeArray<PointSampleGlobal> BuildSamplesFromRoadOutlineWithExtensionDistance(
            EcsRoadData road, float samplesPerMeter, float extensionDistance)
        {
            var numTotalSamples = ComputeSampleCountForRoadOutline(road.ecsRoad, samplesPerMeter);
            var allSamples = new NativeArray<PointSampleGlobal>(numTotalSamples, Allocator.Temp);
            BuildSamplesFromRoadOutline(road, samplesPerMeter, allSamples);
            UpdateWithExtensionDistance(extensionDistance, allSamples);

            return allSamples;
        }

        /// <summary>
        /// Generates point samples from a road description extended by a specified distance
        /// </summary>
        /// <param name="extensionDistance">Offset applied to the beginning and end of road segment polygons. Increases
        /// the likelihood of polygon overlap to encourage the creation of one contiguous road network polygon.</param>
        public static void BuildSamplesFromRoadOutlineWithExtensionDistance(
            EcsRoadData road, float samplesPerMeter, float extensionDistance, NativeSlice<PointSampleGlobal> samplesOut)
        {
            BuildSamplesFromRoadOutline(road, samplesPerMeter, samplesOut);
            UpdateWithExtensionDistance(extensionDistance, samplesOut);
        }

        static void UpdateWithExtensionDistance(float extensionDistance, NativeSlice<PointSampleGlobal> polygon)
        {
            var pose = polygon[0].pose;
            polygon[0] = new PointSampleGlobal(new RigidTransform
            {
                pos = pose.pos + math.rotate(pose.rot, new float3(0, 0, -extensionDistance)),
                rot = pose.rot
            });

            pose = polygon[polygon.Length - 1].pose;
            polygon[polygon.Length - 1] = new PointSampleGlobal(new RigidTransform
            {
                pos = pose.pos + math.rotate(pose.rot, new float3(0, 0, -extensionDistance)),
                rot = pose.rot
            });

            var middleIndex = polygon.Length / 2;
            pose = polygon[middleIndex - 1].pose;
            polygon[middleIndex - 1] = new PointSampleGlobal(new RigidTransform
            {
                pos = pose.pos + math.rotate(pose.rot, new float3(0, 0, extensionDistance)),
                rot = pose.rot
            });

            pose = polygon[middleIndex].pose;
            polygon[middleIndex] = new PointSampleGlobal(new RigidTransform
            {
                pos = pose.pos + math.rotate(pose.rot, new float3(0, 0, extensionDistance)),
                rot = pose.rot
            });
        }

        internal static NativeArray<PointSampleGlobal> BuildSamplesForLaneSectionSide(Road road, SamplingStateRoad state,
            Side side)
        {
            var laneSection = road.laneSections[state.laneSectionIdx];
            var sideLanes = GetLaneSectionSide(laneSection, side);
            if (sideLanes == null)
            {
                // NOTE: This should be un-reachable from the function that calls this one
                Debug.LogWarning(String.Format("Can't sample {0} lanes for road {1} because there are none",
                    side.ToString(), road.roadId));
                return new NativeArray<PointSampleGlobal>(0, Allocator.Temp);
            }

            var laneLength = state.laneSectionEndS - laneSection.sRoad;
            var samplesPerMeter = state.samplesPerMeter;
            // Convert the outside points (throwing out y at this point)
            var sampleCount = 1 + (int) (laneLength * samplesPerMeter);

            //Add a sample at the end of the curve if the last
            var sampleAtEnd = !Mathf.Approximately(laneLength, (sampleCount - 1) / samplesPerMeter);

            var totalSampleCount = sampleAtEnd ? sampleCount + 1 : sampleCount;

            // samples for each lane are in contiguous blocks [s0l0, s1l0, s2l0, s0l1, s1l1, s1l2, s0l2...]
            var samples =
                new NativeArray<PointSampleGlobal>(totalSampleCount * sideLanes.Count(), Allocator.Temp);
            for (var sampleIdx = 0; sampleIdx < sampleCount; sampleIdx++)
            {
                var sSampleAbsolute = sampleIdx / samplesPerMeter + laneSection.sRoad;
                SampleLanesOneSide(road, state, side, sSampleAbsolute, sampleIdx, ref samples);
            }

            if (sampleAtEnd)
            {
                var sEnd = laneSection.sRoad + laneLength;
                SampleLanesOneSide(road, state, side, sEnd, totalSampleCount - 1, ref samples);
            }

            return samples;
        }

        internal static NativeArray<PointSampleGlobal> BuildSamplesForLaneSectionSide(EcsRoadData roadData, SamplingStateEcsRoad state, Side side)
        {
            var laneSection = roadData.ecsLaneSections[state.laneSectionIdx];
            var sideLanes = GetLaneSectionSide(roadData, laneSection, side);
            if (sideLanes.Length == 0)
            {
                // NOTE: This should be un-reachable from the function that calls this one
                Debug.LogWarning(String.Format("Can't sample {0} lanes for road {1} because there are none",
                    side.ToString(), roadData.ecsRoad.name));
                return new NativeArray<PointSampleGlobal>(0, Allocator.Temp);
            }

            var laneLength = state.laneSectionEndS - laneSection.sRoad;
            var samplesPerMeter = state.samplesPerMeter;
            // Convert the outside points (throwing out y at this point)
            var sampleCount = 1 + (int) (laneLength * samplesPerMeter);

            //Add a sample at the end of the curve if the last
            var sampleAtEnd = !Mathf.Approximately(laneLength, (sampleCount - 1) / samplesPerMeter);

            var totalSampleCount = sampleAtEnd ? sampleCount + 1 : sampleCount;

            // samples for each lane are in contiguous blocks [s0l0, s1l0, s2l0, s0l1, s1l1, s1l2, s0l2...]
            var samples =
                new NativeArray<PointSampleGlobal>(totalSampleCount * sideLanes.Count(), Allocator.Temp);
            for (var sampleIdx = 0; sampleIdx < sampleCount; sampleIdx++)
            {
                var sSampleAbsolute = sampleIdx / samplesPerMeter + laneSection.sRoad;
                SampleLanesOneSide(roadData, state, side, sSampleAbsolute, sampleIdx, ref samples);
            }

            if (sampleAtEnd)
            {
                var sEnd = laneSection.sRoad + laneLength;
                SampleLanesOneSide(roadData, state, side, sEnd, totalSampleCount - 1, ref samples);
            }

            return samples;
        }

        // TODO: Many of our internal sampling paths could be optimized by calling the stateful sample method instead of
        //       this one.
        /// <summary>
        /// Sample the geometry at the given s position without tracking progress along the sampled segment
        /// </summary>
        /// <param name="sRoad">S position to sample. Must be between s and s + length.</param>
        public static PointSampleGlobal Sample(this Geometry geometry, float sRoad)
        {
            var transientState = new SamplingStateGeometry(geometry);
            return Sample(geometry, sRoad, ref transientState);
        }

        internal static PointSampleGlobal Sample(this Geometry geometry, float sRoad, ref SamplingStateGeometry state)
        {
            switch (geometry.geometryKind)
            {
                case GeometryKind.Line:
                    return SampleLine(geometry, sRoad);
                case GeometryKind.Arc:
                    return SampleArc(geometry, sRoad);
                case GeometryKind.Spiral:
                    return SampleSpiral(geometry, sRoad);
                case GeometryKind.Poly3:
                case GeometryKind.ParamPoly3:
                    var sLocal = sRoad - geometry.sRoad;
                    var sampleLocal = SamplePoly3(ref state.poly3, sLocal);
                    return FromLocalToGlobal(geometry.startPose, sampleLocal);
                case GeometryKind.Unknown:
                default:
                    throw new NotImplementedException($"GeometryKind {geometry.geometryKind} not supported");
            }
        }

        // TODO: Width records are still searched every time - this could be optimized by tracking the current record
        /// <summary>
        /// Sample the laneSection at the given s position.
        /// </summary>
        /// <param name="sDelta">s position to sample. Must be between laneSection and s + length.</param>
        internal static PointSampleLocal BuildLaneSample(EcsRoadData roadData, EcsLane lane, float laneOffset, float sSection)
        {
            if (!TryFindWidthRecord(roadData, lane, sSection, out var widthRecord))
            {
                if (lane.laneType == LaneType.Border)
                {
                    return new PointSampleLocal(0.0f, 0.0f, 0.0f);
                }

                throw new Exception("Couldn't find width record for lane... xodr file may be malformed.'");
            }

            var sign = lane.id > 0 ? 1f : -1f;
            var sDelta = sSection - widthRecord.sSectionOffset;
            var width = sign * Poly3ComputeV(widthRecord.poly3, sDelta);
            var heading = sign * Poly3ComputeHeading(widthRecord.poly3, sDelta);
            return new PointSampleLocal(new Vector2(0, width + laneOffset), heading);
        }

        // TODO: Width records are still searched every time - this could be optimized by tracking the current record
        /// <summary>
        /// Sample the laneSection at the given s position.
        /// </summary>
        /// <param name="sDelta">s position to sample. Must be between laneSection and s + length.</param>
        internal static PointSampleLocal BuildLaneSample(Lane lane, float laneOffset, float sSection)
        {
            if (!TryFindWidthRecord(lane, sSection, out var widthRecord))
            {
                if (lane.laneType == LaneType.Border)
                {
                    return new PointSampleLocal(0.0f, 0.0f, 0.0f);
                }

                throw new Exception("Couldn't find width record for lane... xodr file may be malformed.'");
            }

            var sign = lane.id > 0 ? 1f : -1f;
            var sDelta = sSection - widthRecord.sSectionOffset;
            var width = sign * Poly3ComputeV(widthRecord.poly3, sDelta);
            var heading = sign * Poly3ComputeHeading(widthRecord.poly3, sDelta);
            return new PointSampleLocal(new Vector2(0, width + laneOffset), heading);
        }

        /// <summary>
        /// Converts a point sample from inertial coordinates to global (Unity) coordinates
        /// </summary>
        /// <param name="poseLocalInGlobal">Transform defining the conversion from local to global coordinates.
        /// Equivalent to the geometry segments pose in global space</param>
        /// <param name="pointLocal"></param>
        /// <returns></returns>
        internal static PointSampleGlobal FromLocalToGlobal(
            RigidTransform poseLocalInGlobal, PointSampleLocal pointLocal)
        {
            // NOTE: In converting the forward vector from right-handed inertial with u forward to left-handed
            //       3D space with z forward, we must negate the inertial y axis
            var positionLocal = new float3(-pointLocal.position.y, 0, pointLocal.position.x);
            var positionGlobal = math.transform(poseLocalInGlobal, positionLocal);
            var yawLocal = quaternion.RotateY(-pointLocal.headingRadians);
            var rotationGlobal = math.mul(poseLocalInGlobal.rot, yawLocal);
            return new PointSampleGlobal(new RigidTransform(rotationGlobal,
                new float3(positionGlobal.x, positionGlobal.y, positionGlobal.z)));
        }

        internal static PointSampleLocal FromGlobalToLocal(RigidTransform poseLocalInGlobal,
            PointSampleGlobal pointGlobal)
        {
            var tfFromGlobalToLocal = math.inverse(poseLocalInGlobal);
            var poseLocal3D = math.mul(tfFromGlobalToLocal, pointGlobal.pose);
            // In 3D coordinates, z is forward and x is right; in 2D coordinates, x is forward and y is left
            var positionLocal2D = new float2(poseLocal3D.pos.z, -poseLocal3D.pos.x);
            var headingLocalRadians = GlobalOrientationToLocalHeadingRadians(poseLocal3D.rot);

            return new PointSampleLocal(positionLocal2D, headingLocalRadians);
        }

        internal static float GlobalOrientationToLocalHeadingRadians(quaternion orientation)
        {
            var forward = math.mul(orientation, new float3(0, 0, 1));
            var headingUnity = math.atan2(forward.x, forward.z);
            return -headingUnity;
        }

        internal static void SampleLanesOneSide(EcsRoadData road, SamplingStateEcsRoad state, Side side, float sRoad,
            int outputIdx, ref NativeArray<PointSampleGlobal> samples, int stopBeforeIdx = int.MaxValue)
        {
            var laneEdgeOffsetLocal = state.GetLaneOffset(road, sRoad);

            // TODO: Direct access to the Idx should be revoked and replaced with Getter methods
            var laneSection = state.GetLaneSection(road, sRoad);
            var lanes = GetLaneSectionSide(road, laneSection, side);

            if (lanes.Length == 0)
                return;

            var numLanes = lanes.Length;
            var laneIter = lanes.GetEnumerator();
            var numSamplesPerLane = samples.Length / math.min(numLanes, stopBeforeIdx);
            var sLaneSection = sRoad - laneSection.sRoad;
            var geometrySample = state.GetGeometrySample(road, sRoad);
            for (var laneIdx = 0; laneIdx < numLanes && laneIdx < stopBeforeIdx; ++laneIdx)
            {
                // For some reason, in IEnumerables, the 'current' field actually returns the value at index-1 rather
                // than the actual current value, so we need to move the iterator first to set 'current' to the value
                // we'd expect
                laneIter.MoveNext();
                var lane = laneIter.Current;
                var sampleLocal = BuildLaneSample(road, lane, laneEdgeOffsetLocal, sLaneSection);
                laneEdgeOffsetLocal = sampleLocal.position.y;

                var currentIdx = ComputeLaneSampleIdx(laneIdx, numSamplesPerLane, outputIdx);
                if (currentIdx >= samples.Length)
                {
                    throw new Exception("Computed an out of range index.");
                }

                // The offset for the lane edge is measured along the normal of the reference line
                samples[currentIdx] =
                    FromLocalToGlobal(geometrySample.pose, sampleLocal);
            }
        }

        internal static void SampleLanesOneSide(Road road, SamplingStateRoad state, Side side, float sRoad,
            int outputIdx, ref NativeArray<PointSampleGlobal> samples, int stopBeforeIdx = int.MaxValue)
        {
            var laneEdgeOffsetLocal = state.GetLaneOffset(road, sRoad);

            // TODO: Direct access to the Idx should be revoked and replaced with Getter methods
            var laneSection = state.GetLaneSection(road, sRoad);
            var lanes = GetLaneSectionSide(laneSection, side);

            if (!lanes.Any())
            {
                return;
            }

            var numLanes = lanes.Count();
            var laneIter = lanes.GetEnumerator();
            var numSamplesPerLane = samples.Length / math.min(numLanes, stopBeforeIdx);
            var sLaneSection = sRoad - laneSection.sRoad;
            var geometrySample = state.GetGeometrySample(road, sRoad);
            for (var laneIdx = 0; laneIdx < numLanes && laneIdx < stopBeforeIdx; ++laneIdx)
            {
                // For some reason, in IEnumerables, the 'current' field actually returns the value at index-1 rather
                // than the actual current value, so we need to move the iterator first to set 'current' to the value
                // we'd expect
                laneIter.MoveNext();
                var lane = laneIter.Current;
                var sampleLocal = BuildLaneSample(lane, laneEdgeOffsetLocal, sLaneSection);
                laneEdgeOffsetLocal = sampleLocal.position.y;

                var currentIdx = ComputeLaneSampleIdx(laneIdx, numSamplesPerLane, outputIdx);
                if (currentIdx >= samples.Length)
                {
                    throw new Exception("Computed an out of range index.");
                }

                // The offset for the lane edge is measured along the normal of the reference line
                samples[currentIdx] =
                    FromLocalToGlobal(geometrySample.pose, sampleLocal);
            }
        }

        internal static PointSampleGlobal SampleCenter(EcsRoadData road, SamplingStateEcsRoad state, float sRoad)
        {
            var offset = state.GetLaneOffset(road, sRoad);
            var geometrySample = state.GetGeometrySample(road, sRoad);
            var sampleLocal = new PointSampleLocal(0, offset, 0);
            return FromLocalToGlobal(geometrySample.pose, sampleLocal);
        }

        internal static PointSampleGlobal SampleCenter(Road road, SamplingStateRoad state, float sRoad)
        {
            var offset = state.GetLaneOffset(road, sRoad);
            var geometrySample = state.GetGeometrySample(road, sRoad);
            var sampleLocal = new PointSampleLocal(0, offset, 0);
            return FromLocalToGlobal(geometrySample.pose, sampleLocal);
        }

        internal static PointSampleLocal BuildCircleSample(float s, float curvature)
        {
            // Parametric equation for the circles starting at 0, 0 and curving away from the 'x-direction:'
            // u = abs(1/c) * cos(theta - pi/2)  --   v = 1/c * (sin(theta-pi/2) + 1)
            // du/dtheta = -abs(1/c) * sin(theta - pi/2)   --   dv/dtheta = 1/c * sin(theta - pi/2)
            var radiusSigned = 1f / curvature;
            var radiusUnsigned = math.abs(radiusSigned);
            var thetaRotated = s / radiusUnsigned - math.PI / 2f;
            var cosResult = math.cos(thetaRotated);
            var sinResult = math.sin(thetaRotated);
            var u = radiusUnsigned * cosResult;
            // NOTE: 1/curvature is different than radius because it retains its direction; radius is a magnitude
            var v = radiusSigned * (sinResult + 1);
            var headingRadians = s / radiusSigned;
            return new PointSampleLocal(u, v, headingRadians);
        }

        internal static IEnumerable<Lane> GetLaneSectionSide(LaneSection laneSection, Side side)
        {
            var lanes = side == Side.Left ? laneSection.leftLanes : laneSection.rightLanes;
            if (lanes == null)
            {
                throw new InvalidOperationException("Attempted to fetch an un-initialized lanes list.");
            }

            return lanes;
        }

        internal static NativeSlice<EcsLane> GetLaneSectionSide(EcsRoadData roadData, EcsLaneSection laneSection, Side side)
        {
            var lanes = side == Side.Left ? laneSection.GetLeftLanes(roadData.ecsLanes) : laneSection.GetRightLanes(roadData.ecsLanes);
            if (lanes == null)
            {
                throw new InvalidOperationException("Attempted to fetch an un-initialized lanes list.");
            }

            return lanes;
        }

        private static void ThrowLaneOutsideRoad(string name, string id)
        {
            var errStr = "Invalid RoadNetworkDescription. " +
                         $"The road {name} (id {id}) contains a lane that starts outside the reference line.";
            throw new Exception(errStr);
        }

        private static void ThrowLengthNotMatching(string name, string id, float sOffset)
        {
            var errStr = "Invalid RoadNetworkDescription. " +
                         $"The road {name} (id {id}) has a geometry at s={sOffset} whose length doesn't match the " +
                         "distance between itself and the next geometry element";
            throw new Exception(errStr);
        }

        private static bool TryFindWidthRecord(Lane lane, float sLocal,
            out LaneWidthRecord laneWidthRecord)
        {
            var widthRecordIdx = 0;
            var foundWidthRecord = false;
            laneWidthRecord = default;
            if (lane.widthRecords == null)
            {
                Debug.LogWarning("WidthRecords was null, but we'd expect it to be an array of length zero if '" +
                                 "there were no width records.");
                return false;
            }

            for (; widthRecordIdx < lane.widthRecords.Length; widthRecordIdx++)
            {
                var widthRecordNext = lane.widthRecords[widthRecordIdx];
                var sOffsetNext = widthRecordNext.sSectionOffset;
                if (sOffsetNext > sLocal)
                {
                    break;
                }

                laneWidthRecord = widthRecordNext;
                foundWidthRecord = true;
            }

            return foundWidthRecord;
        }

        private static bool TryFindWidthRecord(EcsRoadData roadData, EcsLane lane, float sLocal,
            out LaneWidthRecord laneWidthRecord)
        {
            var widthRecordIdx = 0;
            var foundWidthRecord = false;
            laneWidthRecord = default;

            var laneWidthRecords = lane.GetLaneWidthRecords(roadData.laneWidthRecords);
            for (; widthRecordIdx < laneWidthRecords.Length; widthRecordIdx++)
            {
                var widthRecordNext = laneWidthRecords[widthRecordIdx];
                var sOffsetNext = widthRecordNext.sSectionOffset;
                if (sOffsetNext > sLocal)
                {
                    break;
                }

                laneWidthRecord = widthRecordNext;
                foundWidthRecord = true;
            }

            return foundWidthRecord;
        }

        internal static PointSampleLocal SamplePoly3(Poly3Data poly3, float sLocal)
        {
            // To keep our code paths consistent, we'll just create state for stateful sampling, then discard it
            var samplingState = new SamplingStatePoly3(poly3);

            return SamplePoly3(ref samplingState, sLocal);
        }

        internal static PointSampleLocal SamplePoly3(ref SamplingStatePoly3 state, float sLocal)
        {
            while (state.NextValues.s < sLocal)
            {
                state.SetNextValues(
                    Poly3ComputeSimpsonStep(state.m_poly3, state.CurrentValues, state.m_dsStepForApproximation));
            }

            return Poly3ConstructSample(state.m_poly3, sLocal, state.CurrentValues, state.NextValues);
        }

        internal static float Poly3ComputeL(Poly3Data poly3, float u)
        {
            //l = sqrt(1 + (dv/du)^2)
            // dv/du is the slope
            var slope = Poly3ComputeDerivative(poly3, u);
            return math.sqrt(1 + slope * slope);
        }

        internal static float Poly3ComputeL(ParamPoly3Data poly3, float p)
        {
            //l = sqrt((du/dp)^2 + (dv/dp)^2)
            // dv/du is the slope
            var (dvDp, duDp) = Poly3ComputeDerivative(poly3, p);
            return math.sqrt(duDp * duDp + dvDp * dvDp);
        }

        internal static SimpsonsRuleValues Poly3ComputeSimpsonStep(ParamPoly3Data poly3, SimpsonsRuleValues current,
            float samplingDelta)
        {
            //use the arc length rule (https://en.wikipedia.org/wiki/Arc_length#Finding_arc_lengths_by_integrating) and
            //Simpson's Rule (https://en.wikipedia.org/wiki/Simpson's_rule) to estimate the distance along the polynomial
            var (dvdp, dudp) = Poly3ComputeDerivative(poly3, current.p);
            var stepSize = samplingDelta / math.sqrt(dvdp * dvdp + dudp * dudp);
            var pMid = current.p + stepSize;
            var lMid = Poly3ComputeL(poly3, pMid);

            var pNext = current.p + stepSize * 2;
            var lNext = Poly3ComputeL(poly3, pNext);

            var ds = (current.l + 4 * lMid + lNext) * stepSize / 3;

            var sNext = current.s + ds;

            if (ds <= 0)
                throw new Exception(
                    $"ds was {ds:0.0000} but should always be positive and non-zero while stepping through a Poly3.\n"
                     + $"Error caused by sampling with step size {samplingDelta:0.0000}: {poly3}");

            return new SimpsonsRuleValues(sNext, lNext, pNext);
        }

        internal static PointSampleLocal Poly3ConstructSample(ParamPoly3Data poly3, float sLocal,
            SimpsonsRuleValues current, SimpsonsRuleValues next)
        {
            var sPercent = (sLocal - current.s) / (next.s - current.s);
            var pFinal = current.p + (next.p - current.p) * sPercent;
            var uFinal = Poly3ComputeU(poly3, pFinal);
            var vFinal = Poly3ComputeV(poly3, pFinal);
            var headingLocal = Poly3ComputeHeading(poly3, pFinal);

            return new PointSampleLocal(uFinal, vFinal, headingLocal);
        }

        internal static float Poly3ComputeHeading(Poly3Data poly3, float x)
        {
            var slope = Poly3ComputeDerivative(poly3, x);
            // Because poly3 is a cubic polynomial, its heading vector will always point towards positive x
            // So, since heading will always be in (-pi/2, pi/2), we are safe to use atan here
            return math.atan(slope);
        }

        internal static float Poly3ComputeHeading(ParamPoly3Data poly3, float t)
        {
            var (dv, du) = Poly3ComputeDerivative(poly3, t);
            return math.atan2(dv, du);
        }

        internal static float Poly3ComputeU(ParamPoly3Data poly3, float t)
        {
            var tValues = Poly3ZeroOrder(t);
            return math.dot(tValues, poly3.u);
        }

        internal static float Poly3ComputeV(ParamPoly3Data poly3, float t)
        {
            var tValues = Poly3ZeroOrder(t);
            return math.dot(tValues, poly3.v);
        }

        internal static float Poly3ComputeV(Poly3Data poly3, float u)
        {
            var uValues = Poly3ZeroOrder(u);
            return math.dot(uValues, poly3.v);
        }

        internal static float Poly3ComputeDerivative(Poly3Data poly3, float u)
        {
            var dvDu = Poly3FirstOrder(u);
            return math.dot(dvDu, poly3.v);
        }

        internal static (float, float) Poly3ComputeDerivative(ParamPoly3Data poly3, float t)
        {
            var ddt = Poly3FirstOrder(t);
            var dudt = math.dot(ddt, poly3.u);
            var dvdt = math.dot(ddt, poly3.v);
            return (dvdt , dudt);
        }

        // Computes the index of a given lane sample in the stacked 1-D array of lane samples
        private static int ComputeLaneSampleIdx(int laneIdx, int numSamplesPerLane, int outputIdx)
        {
            return laneIdx * numSamplesPerLane + outputIdx;
        }

        internal static int ComputeNumSamplesForLaneSection(Road road, int sectionIdx, float samplesPerMeter)
        {
            var sStart = road.laneSections[sectionIdx].sRoad;
            var sEnd = road.laneSections.Count > sectionIdx + 1 ? road.laneSections[sectionIdx + 1].sRoad : road.length;
            var sLength = sEnd - sStart;
            // Since this is measuring by section, we don't attempt to cap the sampling on the end by adding an extra
            return (int) (1 + sLength * samplesPerMeter);
        }

        internal static int ComputeNumSamplesForRoadLength(float roadLength, float samplesPerMeter)
        {
            var numTotalSamples = (int) (1 + roadLength * samplesPerMeter);
            // If sampling step doesn't divide evenly into road length, add additional samples at the end of road
            if (roadLength % (1 / samplesPerMeter) > .0001f)
            {
                numTotalSamples++;
            }

            return numTotalSamples;
        }

        private static float4 Poly3ZeroOrder(float x)
        {
            return new float4(1f, x, x * x, x * x * x);
        }

        private static float4 Poly3FirstOrder(float x)
        {
            return new float4(0, 1, 2 * x, 3 * x * x);
        }

        static PointSampleGlobal SampleLine(Geometry geo, float sRoad)
        {
            var sLocal = sRoad - geo.sRoad;
            var direction = math.mul(geo.startPose.rot, new float3(0,0,1));
            var position = geo.startPose.pos + direction * sLocal;
            var pointPose = new RigidTransform(geo.startPose.rot, position);
            return new PointSampleGlobal(pointPose);
        }

        static PointSampleGlobal SampleArc(Geometry geo, float s)
        {
            if (Mathf.Approximately(geo.arcData.curvature, 0))
            {
                return SampleLine(geo, s);
            }

            var sLocal = s - geo.sRoad;
            var sampleLocal = BuildCircleSample(sLocal, geo.arcData.curvature);
            return FromLocalToGlobal(geo.startPose, sampleLocal);
        }

        static PointSampleGlobal SampleSpiral(Geometry geo, float s)
        {
            //Spirals with very similar start and end curvatures should be treated as arcs, since Spiral.Compute does not work with differences in curvature close to zero
            if (Math.Abs(geo.spiralData.curvatureStart - geo.spiralData.curvatureEnd) < .00001)
            {
                geo.arcData.curvature = geo.spiralData.curvatureStart;
                return SampleArc(geo, s);
            }
            var sLocal = s - geo.sRoad;
            var sample = Spiral.Compute(sLocal, geo.spiralData.curvatureStart, geo.spiralData.curvatureEnd, geo.length);
            return FromLocalToGlobal(geo.startPose, sample);
        }

        internal static bool IsApproximatelyLessThan(float a, float b)
        {
            return (!Mathf.Approximately(a, b) && a < b);
        }

        static Side ToSide(int laneId)
        {
            if (laneId > 0)
            {
                return Side.Left;
            }

            if (laneId < 0)
            {
                return Side.Right;
            }

            return Side.Center;
        }
    }
}
