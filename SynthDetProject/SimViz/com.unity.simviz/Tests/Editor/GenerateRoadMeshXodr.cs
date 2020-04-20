using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SimViz.Content.Sampling;
using SimViz.TestHelpers;
using UnityEngine.SimViz.Content.MapElements;

namespace EditorTests.RoadNetworkMeshTests
{
    public class GenerateRoadMeshXodr : SimvizTestBaseSetup
    {
        TestHelpers testHelpers = new TestHelpers();
        private RoadNetworkDescription road;

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void GenerateMeshFromXodr(string file)
        {
            testHelpers.GetTestRoadNetwork(file, out road);
            testHelpers.GenerateMeshTypeRoads(road, TestHelpers.MeshGenerationType.MeshRoad);
            
            var roadGameObj = GameObject.Find($"{road.name} (Road Network Mesh)");

            Assert.IsNotNull(roadGameObj, "OpenDrive GameObject was never created");
            Assert.IsNotEmpty(road.AllJunctions, "OpenDrive mesh doesn't junction contain data");
            Assert.IsNotEmpty(road.AllRoads, "OpenDrive mesh doesn't road contain data");
            LogAssert.NoUnexpectedReceived();
        }

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void GenerateLineRendererFromXodr(string file)
        {
            testHelpers.GetTestRoadNetwork(file, out road);
            testHelpers.GenerateMeshTypeRoads(road, TestHelpers.MeshGenerationType.MeshRoad);

            var roadGameObj = GameObject.Find($"{road.name} (Road Network Mesh)");
            Assert.IsNotNull(roadGameObj, "OpenDrive GameObject was never created");
            Assert.IsNotEmpty(road.AllJunctions, "OpenDrive mesh doesn't junction contain data");
            Assert.IsNotEmpty(road.AllRoads, "OpenDrive mesh doesn't road contain data");
            LogAssert.NoUnexpectedReceived();
        }

        [Test, TestCaseSource(typeof(XodrTestData), "TestCases")]
        public void GenerateOpenDRIVELanesToMesh(string file)
        {
            testHelpers.GetTestRoadNetwork(file, out road);
            testHelpers.GenerateMeshTypeRoads(road, TestHelpers.MeshGenerationType.MeshRoad);

            var meshContainer = RoadNetworkMesher.GenerateMesh(road);
            var laneContainer = RoadNetworkMesher.GenerateMeshWithLanes(road);

            var roadContainer = new GameObject("TestOpenDRIVE");
            meshContainer.name = "Mesh";
            laneContainer.name = "Lanes";
            meshContainer.transform.parent = roadContainer.transform;
            laneContainer.transform.parent = roadContainer.transform;

            Assert.IsNotNull(roadContainer, "TestOpenDrive was never created");
            Assert.IsNotNull(meshContainer, "Mesh container was never created");
            Assert.IsNotNull(laneContainer, "Lane container was never created");
            Assert.IsNotEmpty(road.AllJunctions, "OpenDrive mesh doesn't junction contain data");
            Assert.IsNotEmpty(road.AllRoads, "OpenDrive mesh doesn't road contain data");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
