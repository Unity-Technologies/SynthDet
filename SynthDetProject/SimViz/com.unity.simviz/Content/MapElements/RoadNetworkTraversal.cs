using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.SimViz.Content.MapElements
{
    public enum TraversalDirection
    {
        Backward = -1,
        Forward = 1
    }

    public struct RoadGroup : IComponentData
    {
        // Direction along road to travel to get to second road in group
        // NOTE: If traversal is backwards, you must start traversal at s=length
        public readonly TraversalDirection startingDirection;
        // First roadId in this group
        public readonly NativeString64 startingId;
        // Number of roads in this group
        public readonly int numRoads;

        public RoadGroup(TraversalDirection startingDirection, NativeString64 startingId, int numRoads)
        {
            this.startingDirection = startingDirection;
            this.startingId = startingId;
            this.numRoads = numRoads;
        }
    }

    // Two HashSets which enable us to keep track of which roads have been traversed and which have not
    internal struct RoadSet
    {
        private HashSet<NativeString64> _idsNotTraversed;
        private HashSet<NativeString64> _idsTraversed;
        internal int Count => _idsNotTraversed == null ? 0 :
            _idsNotTraversed.Count + _idsTraversed.Count;

        internal RoadSet(RoadNetworkDescription roadNetwork)
        {
            _idsNotTraversed = roadNetwork == null ? new HashSet<NativeString64>() :
                new HashSet<NativeString64>(roadNetwork.AllRoads.Select(r => new NativeString64(r.roadId)));
            _idsTraversed = new HashSet<NativeString64>();
        }

        internal bool AnyNotTraversed()
        {
            return _idsNotTraversed.Any();
        }

        internal bool Contains(NativeString64 roadId)
        {
            return _idsTraversed.Contains(roadId) || _idsNotTraversed.Contains(roadId);
        }

        internal NativeString64 GetNotTraversed(int val = 0)
        {
            return _idsNotTraversed.Any()
                ? _idsNotTraversed.ElementAt(math.abs(val % _idsNotTraversed.Count))
                : default;
        }

        internal bool HasBeenTraversed(NativeString64 roadId)
        {
            if (!Contains(roadId))
            {
                throw new ArgumentException($"Road Id {roadId.ToString()} does not exist in this network.");
            }

            return _idsTraversed.Contains(roadId);
        }

        internal void Reset()
        {
            _idsNotTraversed.UnionWith(_idsTraversed);
            _idsTraversed.Clear();
        }

        internal void Traverse(NativeString64 roadId)
        {
            _idsNotTraversed.Remove(roadId);
            _idsTraversed.Add(roadId);
        }
    }

    internal struct TraversalParameters
    {
        internal readonly RoadNetworkDescription roadNetwork;
        internal readonly bool shouldStopAtJunctions;

        public TraversalParameters(RoadNetworkDescription roadNetwork, bool shouldStopAtJunctions)
        {
            this.roadNetwork = roadNetwork;
            this.shouldStopAtJunctions = shouldStopAtJunctions;
        }
    }

    internal class TraversalState
    {
        internal NativeString64 RoadId { get; private set; }
        internal int LaneSectionIdx { get; private set; }
        internal int LaneId { get; private set; }
        internal TraversalDirection Direction { get; private set; }
        // The tracking set of roads in the road network that this traversal is operating on
        internal RoadSet AllRoadIds { get; }

        internal TraversalState(RoadNetworkDescription roadNetwork, NativeString64 roadId, int laneSectionIdx, int laneId, TraversalDirection direction)
            : this(roadNetwork)
        {
            SetNewLocation(roadId, laneSectionIdx, laneId, direction);
        }

        internal TraversalState(RoadNetworkDescription roadNetwork)
        {
            RoadId = default;
            LaneSectionIdx = -1;
            LaneId = 0;
            Direction = default;
            AllRoadIds = new RoadSet(roadNetwork);
        }

        internal void SetNewLocation(NativeString64 roadId, int laneSectionIdx, int laneId,
            TraversalDirection direction)
        {
            RoadId = roadId;
            LaneSectionIdx = laneSectionIdx;
            LaneId = laneId;
            Direction = direction;
            if (!AllRoadIds.HasBeenTraversed(roadId))
            {
                AllRoadIds.Traverse(roadId);
            }
        }

        internal void ReverseDirection()
        {
            Direction = (TraversalDirection) (-(int) Direction);
        }
    }

    internal static class RoadNetworkTraversal
    {

        // TODO: This should be modified to take a TraversalParams struct instead of taking the network directly
        internal static bool TryAdvanceOneLaneSection(RoadNetworkDescription network, TraversalState state,
            bool allowLooping = false)
        {
            var roadCurrent = network.GetRoadById(state.RoadId);
            var laneCurrent = roadCurrent.laneSections[state.LaneSectionIdx].GetLane(state.LaneId);
            var laneIdNext = GetNextLaneId(laneCurrent, state.Direction);

            // If there are more lane sections, we haven't reached the end of the road
            if (HasMoreLaneSectionsInDirection(roadCurrent, state))
            {
                // Check that the current lane exists in this next road section
                if (laneIdNext == 0)
                {
                    Debug.Log($"Lane {state.LaneId} on Road {state.RoadId} ends after section {state.LaneSectionIdx}");
                    return false;
                }

                var sectionIdxNext = state.LaneSectionIdx + (int)state.Direction;
                // Since we're staying in the same road, we preserve the traverse travelDirection
                state = new TraversalState(network, state.RoadId, sectionIdxNext, laneIdNext, state.Direction);
                if (!roadCurrent.laneSections[state.LaneSectionIdx].HasLane(state.LaneId))
                    throw new Exception($"Expected road {state.RoadId} to have lane {state.LaneId}.");
                return true;
            }

            var roadLink = GetLinkToNextRoad(roadCurrent, state.Direction);

            // Check to see if road terminates
            if (roadLink.linkType == RoadLinkType.None)
            {
                Debug.Log($"Road {state.RoadId} ends in travelDirection {state.Direction}");
                return false;
            }

            if (roadLink.linkType == RoadLinkType.Road)
            {
                // If the road has a link, it should be guaranteed to have a valid LinkContactPoint as well
                Debug.Assert(roadLink.contactPoint == LinkContactPoint.Start ||
                             roadLink.contactPoint == LinkContactPoint.End,
                    $"Road {state.RoadId} road link contact point was not set correctly.'");
                if (laneIdNext == 0)
                {
                    Debug.Log($"Lane {state.LaneId} on Road {state.RoadId} ends in travelDirection {state.Direction}");
                    return false;
                }

                var roadIdNext = roadLink.nodeId;
                var roadNext = network.GetRoadById(roadIdNext);
                var directionNext = roadLink.contactPoint == LinkContactPoint.Start ? TraversalDirection.Forward : TraversalDirection.Backward;
                var sectionIdxNext = directionNext == TraversalDirection.Forward ? 0 : roadNext.laneSections.Count -1;

                var laneSectionNext = roadNext.laneSections[sectionIdxNext];
                if (!laneSectionNext.HasLane(laneIdNext))
                    throw new Exception($"Expected {roadNext.roadId} to have lane {laneIdNext}.");

                if (state.AllRoadIds.HasBeenTraversed(new NativeString64(roadIdNext)) && !allowLooping)
                {
                    return false;
                }

                state.SetNewLocation(new NativeString64(roadIdNext), sectionIdxNext, laneIdNext, directionNext);
            }
            else if (roadLink.linkType == RoadLinkType.Junction)
            {
                var junction = network.GetJunctionById(roadLink.nodeId);
                if (!TryAdvanceThroughJunction(junction, network, state, allowLooping))
                {
                    Debug.Log($"Found no connected lanes for lane {state.LaneId} on road {state.RoadId} in junction {junction.junctionId}");
                    return false;
                }
            }

            return true;
        }

        // Each non-junction road is effectively a segment of a graph edge - this function identifies which roads
        // belong on the same edge as the input graph edge and return the information necessary to query this edge
        internal static RoadGroup IdentifyGraphEdgeGroup(RoadNetworkDescription roadNetwork,
            TraversalState state, NativeString64 roadId)
        {
            if (roadNetwork.GetRoadById(roadId).junction != "-1")
            {
                throw new ArgumentException(
                    "Cannot collect graph edge group for a road inside a junction - roads inside junctions" +
                    "are part of a graph node.");
            }

            // Because we want to limit traversal to this graph edge, we will always stop at junctions (nodes)
            const bool stopAtJunctions = true;
            var param = new TraversalParameters(roadNetwork, stopAtJunctions);
            // Count how many Road elements are in this group, including the starting road
            var numRoads = 1;
            state.SetNewLocation(roadId, 0, 0, TraversalDirection.Backward);
            while (TryAdvanceOneRoad(param, state))
            {
                numRoads++;
            }
            // We've moved all the way to the "front" of this collection of roads - store it as the starting location
            var startingRoadId = state.RoadId;
            // To get from the first Road to the second, we need to travel in the opposite direction we traversed to
            // reach the first Road
            var startDirection = (TraversalDirection)(-(int) state.Direction);
            // Traverse forward to ensure we mark all the roads in this group as traversed
            state.SetNewLocation(roadId, 0, 0, TraversalDirection.Forward);
            while (TryAdvanceOneRoad(param, state))
            {
                numRoads++;
            }

            return new RoadGroup(startDirection, startingRoadId, numRoads);
        }

        internal static void AdvanceOneRoad(TraversalParameters traversalParam, TraversalState traversalState)
        {
            if (!TryAdvanceOneRoad(traversalParam, traversalState))
            {
                throw new InvalidOperationException(
                    "Failed to advance along road network - you may want TryAdvanceOneRoad instead.");
            }
        }

        // NOTE: Because this method advances by ROAD, any LANE information will be lost on traversal (reset to 0)
        //       Use TryAdvanceOneLaneSection to move along a selected lane
        internal static bool TryAdvanceOneRoad(TraversalParameters traversalParam, TraversalState traversalState)
        {
            var roadCurrent = traversalParam.roadNetwork.GetRoadById(traversalState.RoadId);
            var link = GetLinkToNextRoad(roadCurrent, traversalState.Direction);


            if (link.linkType == RoadLinkType.None || traversalParam.shouldStopAtJunctions &&
                !IsLinkOnGraphEdge(traversalParam.roadNetwork, link))
            {
                return false;
            }

            var directionNext = DetermineNewDirection(link.contactPoint);
            traversalState.SetNewLocation(new NativeString64(link.nodeId), 0, 0, directionNext);
            return true;
        }

        private static TraversalDirection DetermineNewDirection(LinkContactPoint contactPoint)
        {
            switch (contactPoint)
            {
                case LinkContactPoint.Start:
                    return TraversalDirection.Forward;
                case LinkContactPoint.End:
                    return TraversalDirection.Backward;
                default:
                    throw new ArgumentException($"Cannot determine traversal direction for {contactPoint}.");
            }
        }

        private static RoadLink GetLinkToNextRoad(Road road, TraversalDirection direction)
        {
            return direction == TraversalDirection.Forward ? road.successor : road.predecessor;
        }

        private static int GetNextLaneId(Lane laneCurrent, TraversalDirection direction)
        {
            var links = direction == TraversalDirection.Forward ? laneCurrent.link.successors : laneCurrent.link.predecessors;
            // Check to see if current lane terminates
            if (!links.Any())
            {
                // Zero is guaranteed to never be a valid lane value as the center is not a drivable lane
                return 0;
            }

            // TODO: We need to deal with the fact that sometimes there will be multiple connections
            return links.First();
        }

        private static bool HasMoreLaneSectionsInDirection(Road road, TraversalState state)
        {
            var nextIndex = state.LaneSectionIdx + (int)state.Direction;
            return 0 <= nextIndex && nextIndex < road.laneSections.Count;
        }

        // A Road shares a graph edge with another linked element if that element is a Road which is not part of
        // a set of junction roads
        private static bool IsLinkOnGraphEdge(RoadNetworkDescription roadNetwork, RoadLink link)
        {
            return !(link.linkType == RoadLinkType.None || link.linkType == RoadLinkType.Junction ||
                                    roadNetwork.GetRoadById(link.nodeId).junction != "-1");
        }

        private static bool TryAdvanceThroughJunction(Junction junction, RoadNetworkDescription network, TraversalState state,
            bool allowLooping = false)
        {
            var roadId = state.RoadId.ToString();
            // TODO: This search currently terminates on the first found linked road/lane, but we may want to find
            //       all of them first and then select one based on some criteria
            foreach (var connection in junction.connections)
            {
                if (roadId == connection.incomingRoadId)
                {
                    if (!allowLooping && state.AllRoadIds.HasBeenTraversed(new NativeString64(connection.incomingRoadId)))
                    {
                        return false;
                    }

                    foreach (var link in connection.laneLinks)
                    {
                        if (link.laneIdFrom != state.LaneId)
                            continue;
                        var direction = DetermineNewDirection(connection.contactPoint);
                        var idx = direction == TraversalDirection.Forward
                            ? 0
                            : network.GetRoadById(connection.connectingRoadId).laneSections.Count - 1;
                        state.SetNewLocation(new NativeString64(connection.connectingRoadId), idx, link.laneIdTo, direction);
                        return true;
                    }
                }
                else if (roadId == connection.connectingRoadId)
                {
                    if (!allowLooping && state.AllRoadIds.HasBeenTraversed(new NativeString64(connection.connectingRoadId)))
                    {
                        return false;
                    }

                    foreach (var link in connection.laneLinks)
                    {
                        if (link.laneIdTo != state.LaneId)
                            continue;
                        var nextRoad = network.GetRoadById(connection.incomingRoadId);
                        var sectionIdx = nextRoad.laneSections.Count - 1;
                        state.SetNewLocation(new NativeString64(connection.incomingRoadId), sectionIdx, link.laneIdFrom, TraversalDirection.Backward);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
