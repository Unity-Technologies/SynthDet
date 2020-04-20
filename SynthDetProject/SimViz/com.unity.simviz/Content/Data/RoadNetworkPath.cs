using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.SimViz.Content;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Scenarios
{
    public enum RoadForwardSide
    {
        Right,
        Left
    }

    [RequireComponent(typeof(WaypointPath))]
    public class RoadNetworkPath : MonoBehaviour
    {

        // XXX: May want to modify the serialization behavior for this reference
        public RoadNetworkDescription RoadNetwork;
        // Invalid values will get randomized on path generation
        public string RoadId = "";
        public int LaneId = 0;
        // NOTE: There is guaranteed to be at least one waypoint per lane section currently
        [Range(10f, 100f)]
        public float DistanceBetweenPoints = 25.0f;
        // If a randomized path is generated below this value - will attempt to find a new one
        [Range(1, 100)]
        public int MinimumNumberWaypoints = 10;
        public uint Seed = 1;
        // The side of the road that a vehicle should be on when driving forward
        public RoadForwardSide forwardSide = RoadForwardSide.Right;

        // Put an upper limit on how many times we attempt to generate random paths
        private const int MaxPathGenerationAttempts = 5000;

        [SerializeField, HideInInspector]
        private NativeString64 _roadId;
        [SerializeField, HideInInspector]
        private WaypointPath _waypointPath;
        [SerializeField, HideInInspector]
        private uint _currentRandSeed;
        [SerializeField, HideInInspector]
        private string _currentRoadNetwork;
        [SerializeField, HideInInspector]
        private Unity.Mathematics.Random _rand;

        public void Awake()
        {
            InitializeInternals();
        }

        public void Reset()
        {
            InitializeInternals();
        }

        public void OnValidate()
        {
            UpdateInternals();
        }

        internal void GenerateNewWaypointPath()
        {
            var roadIds = new RoadSet(RoadNetwork);
            if (!roadIds.Contains(_roadId))
            {
                _roadId = roadIds.GetNotTraversed(Math.Abs(_rand.NextInt()));
                if (_roadId.Equals(default))
                {
                    if (roadIds.Count == 0)
                    {
                        Debug.LogError($"There were no RoadIds in {nameof(roadIds)}.");
                        return;
                    }

                    // It would be very mysterious if this happened...
                    System.Diagnostics.Debug.Fail($"Failed to get a _roadId from {nameof(roadIds)} even though there are {roadIds.Count}");
                    return;
                }
            }
            // Mark this initial road as traversed in case it's not a valid starting point and we need to get a new one
            roadIds.Traverse(_roadId);

            var road = RoadNetwork.GetRoadById(_roadId);
            while (!RoadHasAnyLanes(road))
            {
                _roadId = roadIds.GetNotTraversed(_rand.NextInt());
                if (_roadId.Equals(default))
                {
                    Debug.LogError($"Failed to find any roads with lanes in {RoadNetwork.name}");
                    return;
                }

                road = RoadNetwork.GetRoadById(_roadId);
                roadIds.Traverse(_roadId);
            }

            if (LaneId == 0 || !RoadHasLane(road, LaneId))
            {
                LaneId = GetRandomLaneId(road);
            }

            // Reset record so only our starting road has been touched

            // Ensure what's displayed in the inspector is consistent with this value
            RoadId = _roadId.ToString();
            var pathPoints = new NativeList<PointSampleGlobal>(Allocator.Temp);
            // NOTE: In road coordinates, "forward" is an arbitrary direction, so the path generated may not follow
            //       regional driving laws for side of road
            var startIdx = GetRoadStartSectionIdx(road);
            var startDirection = GetRoadStartDirection();
            var traversalState = new TraversalState(RoadNetwork, _roadId, startIdx, LaneId, startDirection);
            var samplesPerMeter = 1f / DistanceBetweenPoints;
            var roadIdCurrent = _roadId.ToString();
            var samplingState = new SamplingStateRoad(road, samplesPerMeter);
            var numRoadsTraversed = 1;
            do
            {
                if (roadIdCurrent != traversalState.RoadId.ToString())
                {
                    numRoadsTraversed++;
                    roadIdCurrent = traversalState.RoadId.ToString();
                    //Debug.Log($"Moving to road {roadIdCurrent}, lane {traversalState.LaneId}");
                    road = RoadNetwork.GetRoadById(roadIdCurrent);
                    samplingState = new SamplingStateRoad(road, samplesPerMeter);
                }

                var numSamplesSection = GeometrySampling.ComputeNumSamplesForLaneSection(road, traversalState.LaneSectionIdx,
                    samplesPerMeter);
                var newSamples = new NativeArray<PointSampleGlobal>(numSamplesSection, Allocator.Temp);
                GeometrySampling.BuildSamplesInsideLaneSection(
                    road, traversalState.LaneId, numSamplesSection, 0, traversalState.Direction,
                    ref samplingState, ref newSamples);
                pathPoints.AddRange(newSamples);
                newSamples.Dispose();
            } while (RoadNetworkTraversal.TryAdvanceOneLaneSection(RoadNetwork, traversalState));

            _waypointPath.RemoveAllSegments();
            _waypointPath.AddSegment();
            var pointIdx = 0;
            foreach (var sample in pathPoints)
            {
                _waypointPath.AddNewPointAt(0, pointIdx, sample.Position);
                pointIdx++;
            }

            Debug.Log($"Path generated: {pathPoints.Length} points over {numRoadsTraversed} road segments.");
            pathPoints.Dispose();
        }

        public void GeneratePathFromInspectorValues()
        {
            GenerateNewWaypointPath();
            var numPoints = _waypointPath.GetPointCount(0);
            if (numPoints < MinimumNumberWaypoints)
            {
                Debug.LogWarning(
                    $"Generated path has only {numPoints} - you may want to randomize it to get a larger one.");
            }
        }

        public void GenerateRandomizedPath()
        {
            int numPointsPath; 
            var numAttempts = 0;
            do
            {
                RoadId = "";
                LaneId = 0;
                UpdateInternals();
                GenerateNewWaypointPath();
                numAttempts++;
                numPointsPath = _waypointPath.GetPointCount(0);
            } while (numPointsPath < MinimumNumberWaypoints && numAttempts < MaxPathGenerationAttempts);

            if (numAttempts == MaxPathGenerationAttempts)
            {
                Debug.LogError("Failed to generate a random path with an adequate number of points.");
            }
        }

        private int GetRandomLaneId(Road road)
        {
            var startIdx = GetRoadStartSectionIdx(road);
            var start = road.laneSections[startIdx];
            var lanes = start.edgeLanes.Select(r => r.id);
            return lanes.ElementAt(Random.Range(0, lanes.Count()));
        }

        private void InitializeInternals()
        {
            _waypointPath = GetComponent<WaypointPath>();
            if (RoadNetwork == null)
            {
                RoadNetwork = GetComponent<RoadNetworkReference>()?.RoadNetwork;
            }

            _currentRandSeed = Seed;
            _currentRoadNetwork = RoadNetwork != null ? RoadNetwork.name : "";
            InitializeRand(Seed);
        }

        private void InitializeRand(uint seed)
        {
            _currentRandSeed = seed;
            _rand = new Unity.Mathematics.Random(seed);
        }

        private int GetRoadStartSectionIdx(Road road)
        {
            return forwardSide == RoadForwardSide.Right ? 0 : road.laneSections.Count - 1;
        }

        private TraversalDirection GetRoadStartDirection()
        {
            return forwardSide == RoadForwardSide.Right ? TraversalDirection.Forward : TraversalDirection.Backward;
        }

        private bool RoadHasAnyLanes(Road road)
        {
            return road.laneSections != null && road.laneSections.Any(section => section.edgeLanes.Any());
        }

        private bool RoadHasLane(Road road, int laneId)
        {
            foreach (var section in road.laneSections)
            {
                if (section.HasLane(laneId))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateInternals()
        {
            if (_currentRandSeed != Seed)
            {
                InitializeRand(Seed);
            }

            if (RoadNetwork == null)
            {
                _currentRoadNetwork = "";
            }
            else if (_currentRoadNetwork != RoadNetwork.name)
            {
                _currentRoadNetwork = RoadNetwork.name;
                _roadId = default;
                LaneId = 0;
            }

            if (_waypointPath == null)
            {
                _waypointPath = GetComponent<WaypointPath>();
                if (_waypointPath == null)
                {
                    Debug.LogError("No WaypointPath attached - won't be able to generate a path.");
                }
            }

            if (RoadNetwork?.HasRoad(RoadId) ?? false)
            {
                _roadId = new NativeString64(RoadId);
            }
            else
            {
                _roadId = default;
            }
        }
    }
}
