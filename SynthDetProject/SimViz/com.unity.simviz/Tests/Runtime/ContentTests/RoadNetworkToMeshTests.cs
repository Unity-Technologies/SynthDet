using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;
using UnityEngine.TestTools.Utils;
using Unity.Mathematics;

namespace UnityEngine.SimViz.Content.ContentTests
{
    public class RoadNetworkToMeshTests
    {
        [Test]
        public void GenerateMesh_OnLine_ReturnsCorrectVertices()
        {
            var geometry = new Geometry()
            {
                geometryKind = GeometryKind.Line,
                startPose = new RigidTransform(quaternion.identity, float3.zero),
                length = 0.4f,
                sRoad = 0f
            };

            var wd2 = RoadNetworkMesher.RoadWidth * .5f;
            // NOTE: The expected values here vary with respect to the const values in RoadNetworkMesher
            var verticesExpected = new[]
            {
                new Vector3(-wd2, 0, 0),
                new Vector3(wd2, 0, 0),
                new Vector3(-wd2, 0, 0.3f),
                new Vector3(wd2, 0,0.3f),
                new Vector3(-wd2, 0, 0.4f),
                new Vector3(wd2, 0, 0.4f)
            };

            var mesh = RoadNetworkMesher.BuildMeshForGeometry(geometry, 3);
            var verticesActual = mesh.vertices;
            Assert.AreEqual(verticesExpected.Length, verticesActual.Length);
            // It is ok for the generated vertices to be slightly off from the exact values we expect
            var laxVectorComparer = new Vector3EqualityComparer(0.1f);
            foreach (var vertex in verticesExpected)
                Assert.IsTrue(verticesActual.Contains(vertex, laxVectorComparer));
        }

        [Test]
        public void RoadNetworkMesher_TransformsAreInverses()
        {
            var pointOriginal = new Vector3(1, 0, -2);
            var pointPolygon = RoadNetworkMesher.ToPolygonPoint(pointOriginal);
            var pointTransformed = RoadNetworkMesher.ToVector3(pointPolygon);
            Assert.AreEqual(pointOriginal, pointTransformed);
        }
    }
}
