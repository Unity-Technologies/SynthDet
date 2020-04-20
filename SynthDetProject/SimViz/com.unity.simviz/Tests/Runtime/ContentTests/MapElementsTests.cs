using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using System.Xml.Linq;
using Unity.Mathematics;
using UnityEngine.TestTools;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content.ContentTests
{
    [TestFixture]
    public class MapElementsTests
    {
#if UNITY_EDITOR
        private OpenDriveMapElementFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new OpenDriveMapElementFactory();
        }

        private static XDocument MakeMockXDocument(string filename)
        {
            var rootDir = Directory.GetCurrentDirectory();
            string contentDir;
            
            if (rootDir.EndsWith("simviz"))
            {
                // We're in the root of the SimViz package...
                contentDir = Path.Combine(new string[] {rootDir, "Tests"});
            }
            else
            {
                // We're in the simviz-av-template project, navigate to simviz in the package directory...
                contentDir = Path.Combine(new string[] {rootDir, "Packages", "com.unity.simviz", "Tests"});
            }
            
            if (!Directory.Exists(contentDir))
            {
                throw new FileNotFoundException("Failed to find the Content directory at " + contentDir);
            }

            var xmlDir =  Path.Combine(new string[] {contentDir, @"Runtime\ContentTests", "MockXml"});
            var xmlPath = Path.Combine(new string[] {xmlDir, filename});
            return XDocument.Load(xmlPath);
        }

        public static IEnumerable<TestCaseData> FromOpenDriveToUnityTestCases()
        {
            // Identity transforms
            yield return new TestCaseData(0f, 0f, 0f, RigidTransform.identity);
            yield return new TestCaseData(2*math.PI, 0f, 0f, RigidTransform.identity);
            yield return new TestCaseData(-2*math.PI, 0f, 0f, RigidTransform.identity);
            // Rotation only
            yield return new TestCaseData(math.PI / 4f, 0f, 0f, 
                new RigidTransform(quaternion.RotateY(-math.PI / 4f), float3.zero));
            // Translation only
            yield return new TestCaseData(0f, 1f, -2f, 
                new RigidTransform(quaternion.identity, new float3(2, 0, 1)));
            // Rotation and translation
            yield return new TestCaseData(math.PI / 2f, 1f, -1f,
                new RigidTransform(quaternion.RotateY(-math.PI / 2f), new float3(1, 0, 1f)));
        }

        [Test]
        [TestCaseSource(nameof(FromOpenDriveToUnityTestCases))]
        public void TestFromOpenDriveToUnity(float headingIn, float xIn, float yIn, RigidTransform poseExpected)
        {
            var poseActual = OpenDriveMapElementFactory.FromOpenDriveGlobalToUnity(headingIn, xIn, yIn);
            var posDelta = poseActual.pos - poseExpected.pos;
            var magnitudePosDelta = math.sqrt(math.dot(posDelta, posDelta));
            Assert.IsTrue(Mathf.Approximately(0.0f, magnitudePosDelta));
            var rotationProduct = math.dot(poseExpected.rot, poseActual.rot);
            Assert.IsTrue(Mathf.Approximately(1f, math.abs(rotationProduct)));
        }

        [Test]
        public void OpenDriveFactoryTestRoadNetworkConstruction()
        {
            // Since this is a basic smoke test, we want an xml file with a little bit of everything...
            var doc = MakeMockXDocument("SimpleOpenDrive.xml");
            Debug.Log("Attempting to parse " + doc + "...");
            Assert.IsTrue(_factory.TryCreateRoadNetworkDescription(doc, out var network));
        }

        [Test]
        public void OpenDriveFactoryTestElevationProfileParsing()
        {
            var doc = MakeMockXDocument("SimpleElevationProfileOD.xml");
            var profileElements = doc.Root.Elements().ToList();
            var profile0 = _factory.ConstructRoadElevationProfile(profileElements[0]);
            Assert.AreEqual(0, _factory.NumErrorsLogged);
            Assert.AreEqual(0f, profile0.s);
            Assert.AreEqual(1.0f, profile0.a);
            Assert.AreEqual(2.0f, profile0.b);
            Assert.AreEqual(-1.5f, profile0.c);
            Assert.AreEqual(0.0f, profile0.d);
            // Second profile contains an invalid value...
            LogAssert.Expect(LogType.Error, new Regex(".* is invalid .*"));
            _factory.ConstructRoadElevationProfile(profileElements[1]);
            Assert.AreEqual(1, _factory.NumErrorsLogged);
        }

        [Test]
        public void OpenDriveFactoryTestJunctionParsing()
        {
            var doc = MakeMockXDocument("SimpleJunctionOD.xml");
            var junction = _factory.ConstructJunction(doc.Root);
            Assert.AreEqual("mrjunction", junction.name);
            Assert.AreEqual("1", junction.junctionId);
            Assert.AreEqual(2, junction.connections.Count);
            var connection0 = junction.connections[0];
            Assert.AreEqual("0", connection0.connectionId);
            Assert.AreEqual("502", connection0.incomingRoadId);
            Assert.AreEqual("500", connection0.connectingRoadId);
            Assert.AreEqual(LinkContactPoint.Start, connection0.contactPoint);
            Assert.AreEqual(2, connection0.laneLinks.Count);
            var laneLink00 = connection0.laneLinks[0];
            Assert.AreEqual(1, laneLink00.laneIdFrom);
            Assert.AreEqual(-1, laneLink00.laneIdTo);
            var laneLink01 = connection0.laneLinks[1];
            Assert.AreEqual(2, laneLink01.laneIdFrom);
            Assert.AreEqual(-2, laneLink01.laneIdTo);
            var connection1 = junction.connections[1];
            Assert.AreEqual("1", connection1.connectionId);
            Assert.AreEqual("502", connection1.incomingRoadId);
            Assert.AreEqual("510", connection1.connectingRoadId);
            Assert.AreEqual(LinkContactPoint.End, connection1.contactPoint);
            Assert.AreEqual(1, connection1.laneLinks.Count);
            var laneLink10 = connection1.laneLinks[0];
            Assert.AreEqual(1, laneLink10.laneIdFrom);
            Assert.AreEqual(-1, laneLink10.laneIdTo);
        }

        [Test]
        public void OpenDriveFactoryTestLaneParsing()
        {
            var doc = MakeMockXDocument("SimpleLaneOD.xml");
            var laneSection = _factory.ConstructLaneSection(doc.Root);
            // NOTE: Left lane sections are sorted into ascending order during openDRIVE ingestion - this is contrary
            //       to the xodr spec, but better for our internal representation
            var left = laneSection.leftLanes;
            Assert.AreEqual(2, left.Count());
            Assert.AreEqual(2, left.ElementAt(1).id);
            Assert.AreEqual(LaneType.Border, left.ElementAt(1).laneType);
            Assert.AreEqual(-2, left.ElementAt(1).link.successors[0]);
            Assert.IsEmpty(left.ElementAt(0).link.predecessors);
            Assert.AreEqual(0.0f, left.ElementAt(1).widthRecords[0].sSectionOffset);
            Assert.AreEqual(0.35f, left.ElementAt(1).widthRecords[0].poly3.v.x);
            Assert.AreEqual(0.0f, left.ElementAt(1).widthRecords[0].poly3.v.y);
            var center = laneSection.centerLane;
            Assert.AreEqual(0, center.id);
            Assert.AreEqual(LaneType.Driving, center.laneType);
            Assert.IsEmpty(center.link.predecessors);
            Assert.IsEmpty(center.link.successors);
            var right = laneSection.rightLanes;
            Assert.AreEqual(-1, right.ElementAt(0).id);
            Assert.AreEqual(LaneType.Driving, right.ElementAt(0).laneType);
            Assert.AreEqual(1, right.ElementAt(0).link.successors[0]);
            Assert.AreEqual(-2, right.ElementAt(1).id);
        }

        [Test]
        public void OpenDriveFactoryTestRoadParsing()
        {
            var doc = MakeMockXDocument("EmptyRoadOD.xml");
            var roadElements = doc.Root;
            var parsedRoad = _factory.ConstructRoad(roadElements);
            Assert.AreEqual(2.0, parsedRoad.length);
            Assert.AreEqual("500", parsedRoad.roadId);
            Assert.AreEqual("2", parsedRoad.junction);
            Assert.AreEqual("mock_road", parsedRoad.name);
            
        }

        [Test]
        public void RoadDescriptionTestRoadLinking()
        {
            var doc = MakeMockXDocument("LinkedRoadsOD.xml");
            Assert.IsTrue(_factory.TryCreateRoadNetworkDescription(doc, out var roadNetwork));
            const int numLinkableElements = 2;
            Assert.AreEqual(numLinkableElements, roadNetwork.AllRoads.Length);
            var road1 = roadNetwork.GetRoadById("500");
            var road2 = roadNetwork.GetRoadById("501");
            Assert.NotNull(road1);
            Assert.NotNull(road2);
            Assert.AreEqual(RoadLinkType.Road,road1.successor.linkType);
            Assert.AreEqual(RoadLinkType.Road,road1.predecessor.linkType);
            Assert.AreEqual(RoadLinkType.Road,road2.successor.linkType);
            Assert.AreEqual(RoadLinkType.Road,road2.predecessor.linkType);
            Assert.AreEqual(road2.roadId, road1.predecessor.nodeId);
            Assert.AreEqual(road2.roadId, road1.successor.nodeId);
            Assert.AreEqual(road1.roadId, road2.predecessor.nodeId);
            Assert.AreEqual(road1.roadId, road2.successor.nodeId);
        }
        
        [Test]
        public void ConstructLane()
        {
            var laneOffsetsExpected = new[]
            {
                new LaneOffset(0, new Vector4(1, 2, 3, 4)),
                new LaneOffset(5, new Vector4(6, 7, 8, 9))
            };
            
            var doc = MakeMockXDocument("LanesWithOffsets.xml");
            var road = _factory.ConstructRoad(doc.Root);
            
            CollectionAssert.AreEqual(laneOffsetsExpected, road.laneOffsets);
            Assert.AreEqual(1, road.laneSections.Count);
        }
#endif
    }
}
