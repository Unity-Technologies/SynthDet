using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;
using Random = Unity.Mathematics.Random;

namespace Tests
{

    public class ObjectPlacementUtilitiesTests
    {
        Random m_Rand;
        Dictionary<PrimitiveType, Mesh> m_MeshPrimitives;

        Mesh GetMeshForPrimitive(PrimitiveType primitiveType)
        {
            if (m_MeshPrimitives.ContainsKey(primitiveType))
                return m_MeshPrimitives[primitiveType];
            
            var go = GameObject.CreatePrimitive(primitiveType);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            m_MeshPrimitives.Add(primitiveType, mesh);
            return mesh;
        }
        
        [SetUp]
        public void Setup()
        {
            m_Rand = new Random(1);
            m_MeshPrimitives = new Dictionary<PrimitiveType, Mesh>();
        }

        public static IEnumerable<TestCaseData> AreasTriangles()
        {
            yield return new TestCaseData(0f, new [] {Vector2.zero, Vector2.zero, Vector2.zero});
            yield return new TestCaseData(0f, new [] {Vector2.zero, Vector2.zero, Vector2.one});
            yield return new TestCaseData(0.5f, new[] {Vector2.zero, Vector2.right, Vector2.up});
            yield return new TestCaseData(0.5f, new[] {Vector2.up, Vector2.right, Vector2.one});
            yield return new TestCaseData(3f, new [] {
                new Vector2(0, 0), new Vector2(2, 0), new Vector2(0, 3)});
        }

        public static IEnumerable<TestCaseData> AreasPrimitivesRotations()
        {
            var look45Up = 
                Quaternion.Slerp(Quaternion.identity, Quaternion.LookRotation(Vector3.up), 0.5f);
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.identity);
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.LookRotation(Vector3.right));
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.LookRotation(Vector3.up));
            yield return new TestCaseData(math.sqrt(2), PrimitiveType.Cube, look45Up);
            yield return new TestCaseData(1f, PrimitiveType.Sphere, Quaternion.identity);
            yield return new TestCaseData(math.sqrt(2), PrimitiveType.Sphere, look45Up);
        }


        [Test]
        [TestCaseSource(nameof(AreasTriangles))]
        public void ComputeAreaOfTriangle_IsOrderInvariant(float area, Vector2[] triangle)
        {
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[0], triangle[1], triangle[2]));
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[0], triangle[2], triangle[1]));
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[1], triangle[0], triangle[2]));
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[1], triangle[2], triangle[0]));
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[2], triangle[1], triangle[0]));
            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[2], triangle[0], triangle[1]));
        }

        [Test]
        [TestCaseSource(nameof(AreasTriangles))]
        public void ComputeAreaOfTriangle_IgnoresZAxis(float area, Vector2[] triangle)
        {
            var triangle3d = new Vector3[3];
            for (var i = 0; i < 3; i++)
            {
                triangle3d[i] = (Vector3) triangle[i] + m_Rand.NextFloat(-10f, 10f) * Vector3.forward;
            }

            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle3d[0], triangle3d[1], triangle3d[2]));

        }

        [Test]
        [TestCaseSource(nameof(AreasTriangles))]
        public void ComputeAreaOfTriangle_IgnoresTranslations(float area, Vector2[] triangle)
        {
            var translation = m_Rand.NextFloat2();
            for (var i = 0; i < 3; i++)
            {
                triangle[i] = (Vector2)translation + triangle[i];
            }
            Assert.AreApproximatelyEqual(area,
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[0], triangle[1], triangle[2]));
        }

        [UnityTest]
        [TestCaseSource(nameof(AreasPrimitivesRotations))]
        public void ComputeProjectedArea_ReturnsCorrectValues(float area, PrimitiveType primitive, Quaternion rotation)
        {
            var cameraGo = new GameObject("camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            var cameraViewAreaMeters = camera.orthographicSize * camera.orthographicSize * camera.aspect * 4;
            var cameraViewAreaPixels = camera.pixelHeight * camera.pixelWidth;
            var pixelsToMeters = cameraViewAreaMeters / cameraViewAreaPixels;
            var transformer = new WorldToScreenTransformer(camera);
            var mesh = GetMeshForPrimitive(primitive);
            var projectedArea = ObjectPlacementUtilities.ComputeProjectedArea(
                transformer, Vector3.zero, rotation, mesh.bounds) * pixelsToMeters;
            Assert.AreApproximatelyEqual(area, projectedArea);
            var scale = 1.5f;
            var scaledArea = ObjectPlacementUtilities.ComputeProjectedArea(
                transformer, Vector3.zero, rotation, mesh.bounds, scale) * pixelsToMeters;
            Assert.AreApproximatelyEqual(area * scale * scale, scaledArea);
            GameObject.Destroy(cameraGo);
        }

        public static IEnumerable<TestCaseData> RectTests()
        {
            // Simple corner intersection
            yield return new TestCaseData(
                Rect.MinMaxRect(0, 0, 2, 2),
                Rect.MinMaxRect(1, 1, 3, 3), 
                Rect.MinMaxRect(1, 1, 2, 2));
            // Second Rect encapsulates first
            yield return new TestCaseData(
                Rect.MinMaxRect(-5, -3, 10, 0),
                Rect.MinMaxRect(-6, -4, 15, 2),
                Rect.MinMaxRect(-5, -3, 10, 0));
        }
        
        [Test]
        [TestCaseSource(nameof(RectTests))]
        public void IntersectRect_ReturnsCorrectValues(Rect a, Rect b, Rect expected)
        {
            // Test identities
            Assert.AreEqual(a, ObjectPlacementUtilities.IntersectRect(a, a));
            Assert.AreEqual(b, ObjectPlacementUtilities.IntersectRect(b, b));
            // Test operation
            Assert.AreEqual(expected, ObjectPlacementUtilities.IntersectRect(a, b));
            // Test for symmetry
            Assert.AreEqual(expected, ObjectPlacementUtilities.IntersectRect(b, a));
        }
    }
}
