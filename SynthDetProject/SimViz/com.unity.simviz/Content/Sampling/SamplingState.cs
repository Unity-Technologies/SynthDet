using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine.Assertions;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Pipeline;

// XXX: These aren't pure state objects yet - all the logic and update functions would need to be moved to statics if
//      we wanted to aggressively enforce a functional paradigm

namespace UnityEngine.SimViz.Content.Sampling
{
    public struct SamplingStatePoly3
    {
        internal ParamPoly3Data m_poly3 { get; }

        internal SimpsonsRuleValues NextValues => m_values[1 - m_currentIdx];

        internal SimpsonsRuleValues CurrentValues => m_values[m_currentIdx];

        internal void SetNextValues(SimpsonsRuleValues newValues)
        {
            m_values[m_currentIdx].SetAll(newValues.s, newValues.l, newValues.p);
            m_currentIdx = 1 - m_currentIdx;
        }

        internal readonly float m_dsStepForApproximation;
        // Index which toggles between 0 and 1 to track which slot in the values array holds the "current" values
        private int m_currentIdx;
        // A tiny circular buffer (size 2) to hold the most recently computed simpson's rule steps
        private SimpsonsRuleValues[] m_values;

        internal SamplingStatePoly3(Geometry geometry, float dsStepForApproximation = 0.1f)
        {
            if (geometry.geometryKind == GeometryKind.Poly3)
            {
                m_poly3 = new ParamPoly3Data(geometry.poly3Data);
            }
            else
            {
                m_poly3 = geometry.paramPoly3Data;
            }

            m_dsStepForApproximation = dsStepForApproximation;

            m_currentIdx = 0;
            var initialValues = new SimpsonsRuleValues(0, GeometrySampling.Poly3ComputeL(m_poly3, 0), 0);
            m_values = new[]
            {
                initialValues,
                GeometrySampling.Poly3ComputeSimpsonStep(m_poly3, initialValues, m_dsStepForApproximation)
            };
        }

        internal SamplingStatePoly3(Poly3Data poly3, float dsStepForApproximation = 0.1f)
            : this(new ParamPoly3Data(poly3), dsStepForApproximation)
        {
        }

        internal SamplingStatePoly3(ParamPoly3Data poly3, float dsStepForApproximation = 0.1f)
        {
            m_poly3 = poly3;
            m_dsStepForApproximation = dsStepForApproximation;

            m_currentIdx = 0;
            var initialValues = new SimpsonsRuleValues(0, GeometrySampling.Poly3ComputeL(m_poly3, 0), 0);
            m_values = new[]
            {
                initialValues,
                GeometrySampling.Poly3ComputeSimpsonStep(m_poly3, initialValues, dsStepForApproximation)
            };
        }
    }

    public struct SamplingStateGeometry
    {
        internal SamplingStatePoly3 poly3;

        public SamplingStateGeometry(Geometry geometry)
        {
            if (geometry.geometryKind == GeometryKind.Poly3 || geometry.geometryKind == GeometryKind.ParamPoly3)
            {
                poly3 = new SamplingStatePoly3(geometry);
            }
            else
            {
                poly3 = default;
            }
        }

    }

    public struct SamplingStateEcsRoad
    {
        internal SamplingStateGeometry geometryState;

        public int geometryIdx { get; private set; }
        private float geometryEndS;
        private PointSampleGlobal geometrySample;
        public int laneOffsetIdx { get; private set; }
        private float laneOffsetEndS;
        private float laneOffset;
        public int laneSectionIdx { get; private set; }
        public float laneSectionEndS { get; private set; }
        public float sRoadLastComputed { get; private set; }

        public float samplesPerMeter => 1.0f / sRoadStep;
        private float sRoadCurrent;
        private float sRoadStep;

        // When we're traversing along specific lane sections, ensure the search stays locked to that section
        private bool canMoveToNextLaneSection;

        public SamplingStateEcsRoad(EcsRoadData road, float samplesPerMeter)
        {
            canMoveToNextLaneSection = true;
            geometryIdx = 0;
            geometryEndS = road.ecsGeometries[0].length;
            laneSectionIdx = 0;
            laneSectionEndS = road.ecsLaneSections.Length > 1 ? road.ecsLaneSections[1].sRoad : road.ecsRoad.length;
            if (road.laneOffsets.Length > 0)
            {
                laneOffsetIdx = 0;
                laneOffsetEndS = road.laneOffsets.Length > 1 ? road.laneOffsets[1].sRoad : road.ecsRoad.length;
            }
            else
            {
                laneOffsetIdx = -1;
                laneOffsetEndS = float.MaxValue;
            }

            sRoadCurrent = 0.0f;
            sRoadStep = 1.0f / samplesPerMeter;
            sRoadLastComputed = -1f;
            geometrySample = default;
            laneOffset = 0f;
            geometryState = new SamplingStateGeometry(road.ecsGeometries[0]);
            ComputeSamples(road);
        }

        public PointSampleGlobal GetGeometrySample(EcsRoadData road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return geometrySample;
        }

        public float GetLaneOffset(EcsRoadData road, float sRoad)
        {
            if (road.laneOffsets.Length == 0)
            {
                return 0f;
            }

            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return laneOffset;
        }

        public EcsLaneSection GetLaneSection(EcsRoadData road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return road.ecsLaneSections[laneSectionIdx];
        }

        // Locks traversal to a specific lane section
        public void SelectLaneSection(EcsRoadData road, int newLaneSectionIdx)
        {
            if (newLaneSectionIdx >= road.ecsLaneSections.Length)
            {
                throw new Exception($"Requested lane section idx {newLaneSectionIdx} is larger than road " +
                    $"{road.ecsRoad.name}'s list of lane sections ({road.ecsLaneSections.Length})'");
            }

            sRoadCurrent = road.ecsLaneSections[newLaneSectionIdx].sRoad;
            canMoveToNextLaneSection = true;
            CheckStateAndUpdate(road);
            canMoveToNextLaneSection = false;
            if (newLaneSectionIdx != laneSectionIdx)
            {
                throw new Exception($"Requested lane section {newLaneSectionIdx} but search returned {laneSectionIdx}");
            }
        }

        public void MoveToSCoordinate(EcsRoadData road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
        }

        public bool Step(EcsRoadData road)
        {
            if (Mathf.Approximately(sRoadLastComputed, road.ecsRoad.length))
            {
                return false;
            }

            if (GeometrySampling.IsApproximatelyLessThan(sRoadCurrent + sRoadStep, road.ecsRoad.length))
            {
                sRoadCurrent += sRoadStep;
            }
            else
            {
                sRoadCurrent = road.ecsRoad.length;
            }

            CheckStateAndUpdate(road);
            return true;
        }

        private void ComputeSamples(EcsRoadData road)
        {
            if (Mathf.Approximately(sRoadCurrent, sRoadLastComputed))
            {
                Debug.LogWarning("Compute samples called for the same s-value twice.");
            }

            sRoadLastComputed = sRoadCurrent;

            var geometry = road.ecsGeometries[geometryIdx];

            //var sGeometry = sRoadCurrent - geometry.sRoad;
            geometrySample = geometry.Sample(sRoadCurrent);
            if (road.laneOffsets.Length > 0)
            {
                var laneOffsetRecord = road.laneOffsets[laneOffsetIdx];
                var sLaneOffset = sRoadCurrent - laneOffsetRecord.sRoad;
                laneOffset = GeometrySampling.Poly3ComputeV(laneOffsetRecord.poly3, sLaneOffset);
            }
        }

        private void CheckStateAndUpdate(EcsRoadData road)
        {
            if (!IsStale())
            {
                return;
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, geometryEndS))
            {
                (geometryIdx, geometryEndS) =
                    FindNextAlongLength(road.ecsGeometries, sRoadCurrent, geometryIdx, road.ecsRoad.length);
                geometryState = new SamplingStateGeometry(road.ecsGeometries[geometryIdx]);
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, laneOffsetEndS))
            {
                (laneOffsetIdx, laneOffsetEndS) =
                    FindNextAlongLength(road.laneOffsets, sRoadCurrent, laneOffsetIdx, road.ecsRoad.length);
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, laneSectionEndS))
            {
                if (canMoveToNextLaneSection)
                {
                    (laneSectionIdx, laneSectionEndS) =
                        FindNextAlongLength(road.ecsLaneSections, sRoadCurrent, laneSectionIdx, road.ecsRoad.length);
                }
                else if (!Mathf.Approximately(sRoadCurrent, laneSectionEndS))
                {
                    throw new Exception("Attempted to traverse past end of lane section, but " +
                        $"{nameof(canMoveToNextLaneSection)} is {canMoveToNextLaneSection}");
                }
            }

            ComputeSamples(road);
        }

        internal static (int, float) FindNextAlongLength<TElement>(
            DynamicBuffer<TElement> elements, float sQuery, int idxCurrent, float sEnd) where TElement : struct, IHasS
        {
            if (GeometrySampling.IsApproximatelyLessThan(sQuery, elements[idxCurrent].GetS()))
            {
                throw new Exception($"sTarget ({sQuery}) is less than next GetS() from list.");
            }

            for (; idxCurrent < elements.Length; ++idxCurrent)
            {
                var sCurrent = elements[idxCurrent].GetS();
                if (idxCurrent + 1 == elements.Length)
                {
                    // We've reached the end of the elements list...
                    if (!GeometrySampling.IsApproximatelyLessThan(sQuery, sCurrent))
                    {
                        return (idxCurrent, sEnd);
                    }

                    throw new Exception("Length of items in list did not add up to length target.");
                }

                var sNext = elements[idxCurrent + 1].GetS();
                if (GeometrySampling.IsApproximatelyLessThan(sQuery, sNext))
                {
                    return (idxCurrent, sNext);
                }
            }

            throw new Exception("Should have returned or thrown an exception before this point.");
        }

        internal bool IsStale()
        {
            return !Mathf.Approximately(sRoadCurrent, sRoadLastComputed);
        }
    }

    public struct SamplingStateRoad
    {
        internal SamplingStateGeometry geometryState;

        public int geometryIdx { get; private set; }
        private float geometryEndS;
        private PointSampleGlobal geometrySample;
        public int laneOffsetIdx { get; private set; }
        private float laneOffsetEndS;
        private float laneOffset;
        public int laneSectionIdx { get; private set; }
        public float laneSectionEndS { get; private set; }
        public float sRoadLastComputed { get; private set; }

        public float samplesPerMeter => 1.0f / sRoadStep;
        private float sRoadCurrent;
        private float sRoadStep;

        // When we're traversing along specific lane sections, ensure the search stays locked to that section
        private bool canMoveToNextLaneSection;

        public SamplingStateRoad(Road road, float samplesPerMeter)
        {
            canMoveToNextLaneSection = true;
            geometryIdx = 0;
            geometryEndS = road.geometry[0].length;
            laneSectionIdx = 0;
            laneSectionEndS = road.laneSections.Count > 1 ? road.laneSections[1].sRoad : road.length;
            if (road.laneOffsets.Any())
            {
                laneOffsetIdx = 0;
                laneOffsetEndS = road.laneOffsets.Count > 1 ? road.laneOffsets[1].sRoad : road.length;
            }
            else
            {
                laneOffsetIdx = -1;
                laneOffsetEndS = float.MaxValue;
            }

            sRoadCurrent = 0.0f;
            sRoadStep = 1.0f / samplesPerMeter;
            sRoadLastComputed = -1f;
            geometrySample = default;
            laneOffset = 0f;
            geometryState = new SamplingStateGeometry(road.geometry[0]);
            ComputeSamples(road);
        }

        public PointSampleGlobal GetGeometrySample(Road road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return geometrySample;
        }

        public float GetLaneOffset(Road road, float sRoad)
        {
            if (!road.laneOffsets.Any())
            {
                return 0f;
            }

            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return laneOffset;
        }

        public LaneSection GetLaneSection(Road road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
            return road.laneSections[laneSectionIdx];
        }

        // Locks traversal to a specific lane section
        public void SelectLaneSection(Road road, int newLaneSectionIdx)
        {
            if (newLaneSectionIdx >= road.laneSections.Count)
            {
                throw new Exception($"Requested lane section idx {newLaneSectionIdx} is larger than road " +
                                    $"{road.roadId}'s list of lane sections ({road.laneSections.Count})'");
            }

            sRoadCurrent = road.laneSections[newLaneSectionIdx].sRoad;
            canMoveToNextLaneSection = true;
            CheckStateAndUpdate(road);
            canMoveToNextLaneSection = false;
            if (newLaneSectionIdx != laneSectionIdx)
            {
                throw new Exception($"Requested lane section {newLaneSectionIdx} but search returned {laneSectionIdx}");
            }
        }

        public void MoveToSCoordinate(Road road, float sRoad)
        {
            sRoadCurrent = sRoad;
            CheckStateAndUpdate(road);
        }

        public bool Step(Road road)
        {
            if (Mathf.Approximately(sRoadLastComputed, road.length))
            {
                return false;
            }

            if (GeometrySampling.IsApproximatelyLessThan(sRoadCurrent + sRoadStep, road.length))
            {
                sRoadCurrent += sRoadStep;
            }
            else
            {
                sRoadCurrent = road.length;
            }

            CheckStateAndUpdate(road);
            return true;
        }

        private void ComputeSamples(Road road)
        {
            if (Mathf.Approximately(sRoadCurrent, sRoadLastComputed))
            {
                Debug.LogWarning("Compute samples called for the same s-value twice.");
            }

            sRoadLastComputed = sRoadCurrent;

            var geometry = road.geometry[geometryIdx];
            //var sGeometry = sRoadCurrent - geometry.sRoad;
            geometrySample = geometry.Sample(sRoadCurrent);
            if (road.laneOffsets.Any())
            {
                var laneOffsetRecord = road.laneOffsets[laneOffsetIdx];
                var sLaneOffset = sRoadCurrent - laneOffsetRecord.sRoad;
                laneOffset = GeometrySampling.Poly3ComputeV(laneOffsetRecord.poly3, sLaneOffset);
            }
        }

        private void CheckStateAndUpdate(Road road)
        {
            if (!IsStale())
            {
                return;
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, geometryEndS))
            {
                (geometryIdx, geometryEndS) =
                    FindNextAlongLength(road.geometry, sRoadCurrent, geometryIdx, road.length);
                geometryState = new SamplingStateGeometry(road.geometry[geometryIdx]);
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, laneOffsetEndS))
            {
                (laneOffsetIdx, laneOffsetEndS) =
                    FindNextAlongLength(road.laneOffsets, sRoadCurrent, laneOffsetIdx, road.length);
            }

            if (!GeometrySampling.IsApproximatelyLessThan(sRoadCurrent, laneSectionEndS))
            {
                if (canMoveToNextLaneSection)
                {
                    (laneSectionIdx, laneSectionEndS) =
                        FindNextAlongLength(road.laneSections, sRoadCurrent, laneSectionIdx, road.length);
                }
                else if (!Mathf.Approximately(sRoadCurrent, laneSectionEndS))
                {
                    throw new Exception("Attempted to traverse past end of lane section, but " +
                                        $"{nameof(canMoveToNextLaneSection)} is {canMoveToNextLaneSection}");
                }
            }

            ComputeSamples(road);
        }

        internal static (int, float) FindNextAlongLength<TElement>(
            List<TElement> elements, float sQuery, int idxCurrent, float sEnd) where TElement : IHasS
        {
            if (GeometrySampling.IsApproximatelyLessThan(sQuery, elements[idxCurrent].GetS()))
            {
                throw new Exception($"sTarget ({sQuery}) is less than next GetS() from list.");
            }

            for (; idxCurrent < elements.Count; ++idxCurrent)
            {
                var sCurrent = elements[idxCurrent].GetS();
                if (idxCurrent + 1 == elements.Count)
                {
                    // We've reached the end of the elements list...
                    if (!GeometrySampling.IsApproximatelyLessThan(sQuery, sCurrent))
                    {
                        return (idxCurrent, sEnd);
                    }
                    throw new Exception("Length of items in list did not add up to length target.");
                }

                var sNext = elements[idxCurrent + 1].GetS();
                if (GeometrySampling.IsApproximatelyLessThan(sQuery, sNext))
                {
                    return (idxCurrent, sNext);
                }
            }
            throw new Exception("Should have returned or thrown an exception before this point.");
        }

        internal bool IsStale()
        {
            return !Mathf.Approximately(sRoadCurrent, sRoadLastComputed);
        }
    }
}
