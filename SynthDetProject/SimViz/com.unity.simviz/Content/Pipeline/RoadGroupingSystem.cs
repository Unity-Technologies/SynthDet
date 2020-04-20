using Unity.Entities;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public struct RoadPartitioningParameters
    {
        public readonly RoadNetworkDescription roadNetworkDescription;
    }

    // TODO: This is a placeholder prototype and will be replaced with the system which generates entities containing
    //       the road outlines
    public class RoadPartitioningSystem : ComponentSystem, IGeneratorSystem<RoadPartitioningParameters>
    {
        public RoadPartitioningParameters Parameters
        {
            get => _parameters;
            set
            {
                _paramsWereProcessed = false;
                _parameters = value;
            }
        }

        public EntityArchetype RoadGroupArchetype;
        private RoadPartitioningParameters _parameters;
        private bool _paramsWereProcessed = true;

        protected override void OnCreate()
        {
            // A "Road Group" consists of a collection of road Ids and the direction along the first Road object
            // one needs to travel to reach the next Road
            RoadGroupArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<RoadGroup>());
        }

        protected override void OnUpdate()
        {
            if (_paramsWereProcessed)
                return;

            // Set this flag in the beginning in case something fails and OnUpdate keeps getting called
            _paramsWereProcessed = true;
            var traversalState = new TraversalState(_parameters.roadNetworkDescription);
            while (traversalState.AllRoadIds.AnyNotTraversed())
            {
                var roadId = traversalState.AllRoadIds.GetNotTraversed();
                // Check that the road is part of a graph edge, not a graph node
                if (_parameters.roadNetworkDescription.GetRoadById(roadId).junction != "-1")
                {
                    traversalState.AllRoadIds.Traverse(roadId);
                    continue;
                }
                var entity = EntityManager.CreateEntity(RoadGroupArchetype);
                var roadGroup = RoadNetworkTraversal.IdentifyGraphEdgeGroup(_parameters.roadNetworkDescription,
                    traversalState, roadId);
                EntityManager.SetComponentData(entity, roadGroup);
            }
        }
    }
}
