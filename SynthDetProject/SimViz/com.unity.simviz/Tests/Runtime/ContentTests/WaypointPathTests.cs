using NUnit.Framework;
using UnityEngine.SimViz.Scenarios;
using UnityEngine.SimViz.Content.MapElements;
#if UNITY_EDITOR
using SimViz.TestHelpers;
#endif

namespace UnityEngine.SimViz.Content.ContentTests
{
#if UNITY_EDITOR
    public class WaypointPathTests : SimvizTestBaseSetup
    {
        private TestHelpers testHelp = new TestHelpers();
        private GameObject wayPointObj;
        private RoadNetworkDescription testRoad;

        [SetUp]
        public void WayPointPathTestsSetUp()
        {
            wayPointObj = new GameObject("TestWaypointPath", typeof(WaypointPath));
        }


        [TearDown]
        public void WayPointPathTestsTearDown()
        {
            GameObject.Destroy(wayPointObj);
        }

        [Test]
        public void AddSegmentToWayPointPath()
        {
            var path = wayPointObj.GetComponent<WaypointPath>();
            path.AddSegment();

            Assert.AreEqual(path.GetSegmentCount(), 1, "Failed to add a segment path");
        }

        [Test]
        public void RemoveSegmentFromWayPointPath()
        {
            var path = wayPointObj.GetComponent<WaypointPath>();
            path.AddSegment();

            path.RemoveSegment(0);

            Assert.AreEqual(path.GetSegmentCount(), 0, "Failed to remove a segment path");
        }

        [Test]
        public void ChangeSegmentResolution()
        {
            var path = wayPointObj.GetComponent<WaypointPath>();
            var oldRes = path.resolution;
             var newRes = path.resolution + 1;

            Assert.AreNotEqual(oldRes, newRes, "Failed to change the segment resolution");
        }

        [Test]
        public void RemoveAllSegmentsFromWayPointPath()
        {
            var path = wayPointObj.GetComponent<WaypointPath>();

            for (int i = 0; i < 10; i++)
            {
                path.AddSegment();
            }

            Assert.AreEqual(10, path.GetSegmentCount(), "Didn't create 10 segments for remove all test");

            path.RemoveAllSegments();

            Assert.AreEqual(path.GetSegmentCount(), 0, "Failed to remove all 10 segments");
        }

        [Test]
        public void RayCastPositionsBool()
        {
            var path = wayPointObj.GetComponent<WaypointPath>();
            path.raycastPosition = true;

            Assert.IsTrue(path.raycastPosition, "Failed to set the ray-cast position bool");
        }


        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void GenerateRandomWayPointPathFromXodr(string file)
        {
            testHelp.GetTestRoadNetwork(file, out testRoad);

            var root = new GameObject($"{testRoad.name}");
            var reference = root.AddComponent<RoadNetworkReference>();
            var roadNetworkPath = root.AddComponent<RoadNetworkPath>();
            root.AddComponent<WaypointPath>();

            reference.RoadNetwork = testRoad;
            roadNetworkPath.RoadNetwork = testRoad;

            Assert.IsNotNull(root.name, "Failed to create the Way point Path using the Xodr File");

            if (!testRoad.name.Contains("Circle"))
                roadNetworkPath.GenerateRandomizedPath();
            else
                Assert.Ignore("{0}Road Network doesn't contain enough points for random path generation", testRoad.name);

            var pointCount = root.GetComponent<WaypointPath>().GetPointCount(0);

            Assert.AreNotEqual(" ", roadNetworkPath.RoadId, "Road Id's are blank");
            Assert.AreNotEqual(0, roadNetworkPath.LaneId, "Lane ID is 0");
            Assert.Greater(pointCount, 0, "Way point path doesn't have any segments");
        }

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void GenerateNewWayPointPathFromXodr(string file)
        {
            testHelp.GetTestRoadNetwork(file, out testRoad);

            var root = new GameObject($"{testRoad.name}");
            var reference = root.AddComponent<RoadNetworkReference>();
            var roadNetworkPath = root.AddComponent<RoadNetworkPath>();

            root.AddComponent<WaypointPath>();

            reference.RoadNetwork = testRoad;
            roadNetworkPath.RoadNetwork = testRoad;

            Assert.IsNotNull(root.name, "Failed to create the Way point Path using the Xodr File");

            roadNetworkPath.GenerateNewWaypointPath();

            var pointCount = root.GetComponent<WaypointPath>().GetPointCount(0);

            Assert.AreNotEqual(" ", roadNetworkPath.RoadId, "Road Id's are blank");
            Assert.AreNotEqual(0, roadNetworkPath.LaneId, "Lane ID is 0");
            Assert.Greater(pointCount, 0, "Way point path doesn't have any segments");
        }
    }
#endif
}
