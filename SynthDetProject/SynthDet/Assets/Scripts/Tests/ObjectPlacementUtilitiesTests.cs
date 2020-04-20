using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;
using Random = Unity.Mathematics.Random;

namespace Tests
{
    public class ObjectPlacementUtilitiesTests
    {
        private Random _random;
        private Dictionary<PrimitiveType, Mesh> _meshPrimitives;

        private Mesh GetMeshForPrimitive(PrimitiveType primitiveType)
        {
            if (_meshPrimitives.ContainsKey(primitiveType))
                return _meshPrimitives[primitiveType];
            
            var go = GameObject.CreatePrimitive(primitiveType);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            GameObject.Destroy(go);
            _meshPrimitives.Add(primitiveType, mesh);
            return mesh;
        }
        
        [SetUp]
        public void Setup()
        {
            _random = new Random(1);
            _meshPrimitives = new Dictionary<PrimitiveType, Mesh>();
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
            var look45Corner =
                Quaternion.Slerp(
                Quaternion.Slerp(Quaternion.identity, Quaternion.LookRotation(Vector3.right), 0.5f),
                Quaternion.LookRotation(Vector3.up), 0.5f);
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.identity);
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.LookRotation(Vector3.right));
            yield return new TestCaseData(1f, PrimitiveType.Cube, Quaternion.LookRotation(Vector3.up));
            yield return new TestCaseData(math.sqrt(2), PrimitiveType.Cube, look45Up);
            yield return new TestCaseData(1f, PrimitiveType.Sphere, Quaternion.identity);
            yield return new TestCaseData(math.sqrt(2), PrimitiveType.Sphere, look45Up);
        }


        // A Test behaves as an ordinary method
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
                triangle3d[i] = (Vector3) triangle[i] + _random.NextFloat(-10f, 10f) * Vector3.forward;
            }

            Assert.AreApproximatelyEqual(area, 
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle3d[0], triangle3d[1], triangle3d[2]));

        }

        [Test]
        [TestCaseSource(nameof(AreasTriangles))]
        public void ComputeAreaOfTriangle_IgnoresTranslations(float area, Vector2[] triangle)
        {
            var translation = _random.NextFloat2();
            for (var i = 0; i < 3; i++)
            {
                triangle[i] = (Vector2)translation + triangle[i];
            }
            Assert.AreApproximatelyEqual(area,
                ObjectPlacementUtilities.ComputeAreaOfTriangle(triangle[0], triangle[1], triangle[2]));
        }

        [Test]
        [TestCaseSource(nameof(AreasPrimitivesRotations))]
        public void ComputeProjectedArea_ReturnsCorrectValues(float area, PrimitiveType primitive, Quaternion rotation)
        {
            var mesh = GetMeshForPrimitive(primitive);
            var projectedArea = ObjectPlacementUtilities.ComputeProjectedArea(rotation, mesh.bounds);
            Assert.AreApproximatelyEqual(area, projectedArea);
            var scale = 1.5f;
            var scaledArea = ObjectPlacementUtilities.ComputeProjectedArea(rotation, mesh.bounds, scale);
            Assert.AreApproximatelyEqual(area * scale * scale, scaledArea);
        }
    }
}
