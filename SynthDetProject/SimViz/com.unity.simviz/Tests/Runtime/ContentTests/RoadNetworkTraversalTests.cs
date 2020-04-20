using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.ContentTests
{
    [TestFixture]
    public class RoadNetworkTraversalTests
    {
#if UNITY_EDITOR
        private RoadNetworkDescription LoadTestFile(string name)
        {
            // XXX: How do we remove the need to construct an importer here? We need to be able to serialize a
            // RoadNetworkDescription object without keeping the accompanying xodr file
            var testFilePath = "";
            foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (assetPath.Contains("Test") && assetPath.Contains("XodrFiles") &&
                    assetPath.Contains(name))
                {
                    testFilePath = assetPath;
                    break;
                }
            }
            Assert.AreNotEqual("", testFilePath, $"Failed to find {name} test file.");
            var factory = new OpenDriveMapElementFactory();
            Assert.IsTrue(factory.TryCreateRoadNetworkDescription(XDocument.Load(testFilePath), out var roadNetwork),
                $"Failed to create test road network from {testFilePath}");
            return roadNetwork;
        }

        // RoadNetworkTraversal is still in a larval stage - we're not yet sure what it's final form will take
        // so we are just smoke testing to ensure we have more than zero coverage
        [Test]
        public void RoadNetworkTraversal_LaneSectionTraversalSmokeTest()
        {
            var roadNetwork = LoadTestFile("CircleCourse");
            var firstRoad = roadNetwork.AllRoads.First();
            var firstRoadId = new NativeString64(firstRoad.roadId);
            var numLaneSections = 3;
            var numLoops = 10;
            var traversalState = new TraversalState(roadNetwork,firstRoad.roadId, 0, -1, TraversalDirection.Forward);
            for (var step = 0; step < numLaneSections - 1; ++step)
            {
                Assert.IsTrue(RoadNetworkTraversal.TryAdvanceOneLaneSection(roadNetwork, traversalState, false),
                    $"Failed to advance to step #{step} - {numLaneSections} total");
                Assert.AreEqual(-1, traversalState.LaneId);
            }
            // Without allowing looping, we shouldn't be allowed to go to the next lane section
            Assert.IsFalse(RoadNetworkTraversal.TryAdvanceOneLaneSection(roadNetwork, traversalState, false));
            Assert.IsTrue(RoadNetworkTraversal.TryAdvanceOneLaneSection(roadNetwork, traversalState, true));
            Assert.AreEqual(firstRoadId, traversalState.RoadId);
            for (var step = 0; step < numLaneSections * numLoops; ++step)
            {
                Assert.IsTrue(RoadNetworkTraversal.TryAdvanceOneLaneSection(roadNetwork, traversalState, true),
                    $"Failed to advance to step #{step} - {numLaneSections} total");
                Assert.AreEqual(-1, traversalState.LaneId);
            }
            Assert.AreEqual(firstRoadId, traversalState.RoadId);
        }

        [Test]
        public void RoadNetworkTraversal_RoadGroupingSmokeTest()
        {
            var roadNetwork = LoadTestFile("Crossing8Course");
            var traversalState = new TraversalState(roadNetwork);
            var numGroups = 0;
            while (traversalState.AllRoadIds.AnyNotTraversed())
            {
                var roadId = traversalState.AllRoadIds.GetNotTraversed();
                if (roadNetwork.GetRoadById(roadId).junction != "-1")
                {
                    traversalState.AllRoadIds.Traverse(roadId);
                    continue;
                }

                var roadGroup = RoadNetworkTraversal.IdentifyGraphEdgeGroup(roadNetwork,
                    traversalState, roadId);
                numGroups++;
                Assert.AreEqual(3, roadGroup.numRoads);
                Assert.AreEqual(TraversalDirection.Forward, roadGroup.startingDirection);
            }
            Assert.AreEqual(2, numGroups);
        }


#endif
    }
}
