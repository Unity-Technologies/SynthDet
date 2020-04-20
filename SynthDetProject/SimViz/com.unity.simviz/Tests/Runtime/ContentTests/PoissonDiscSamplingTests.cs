using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.ContentTests
{
    [TestFixture]
    public class PoissonDiscSamplingTests
    {
#if UNITY_EDITOR
        private static IEnumerable<TestCaseData> InvalidInputTestCases()
        {
            yield return new TestCaseData(-1f, 10f, 1f, 12345U, 30);
            yield return new TestCaseData(10f, -1f, 1f, 12345U, 30);
            yield return new TestCaseData(10f, 10f, -1f, 12345U, 30);
            yield return new TestCaseData(10f, 10f, 1f, 12345U, -1);
            yield return new TestCaseData(10f, 10f, 1f, 12345U, 0);
            yield return new TestCaseData(10f, 10f, 1f, 0U, 30);
        }

        [Test]
        [TestCaseSource(nameof(InvalidInputTestCases))]
        public static void InvalidArguments(
            float width, float height, float minRadius, uint seed, int samplingResolution)
        {
            Assert.Throws<ArgumentException>(delegate
            {
                PoissonDiscSampling.BuildSamples(width, height, minRadius, seed, samplingResolution);
            });
        }

        private static IEnumerable<TestCaseData> SpacingTestCases()
        {
            yield return new TestCaseData(0f, 0f, 1f);
            yield return new TestCaseData(1f, 1f, 1f);
            yield return new TestCaseData(10f, 10f, 100f);
            yield return new TestCaseData(10f, 10f, 1f);
            yield return new TestCaseData(10f, 100f, 0.3f);
            yield return new TestCaseData(100f, 10f, 4f);
        }
        
        [Test]
        [TestCaseSource(nameof(SpacingTestCases))]
        public static void GeneratesMinimumSpacedPoints(float width, float height, float minRadius)
        {
            var points = PoissonDiscSampling.BuildSamples(width, height, minRadius);
            var rSqr = minRadius * minRadius;
            var smallestDist = float.PositiveInfinity;
            var point1 = new float2(0f, 0f);
            var point2 = new float2(0f, 0f);
            for (var i = 0; i < points.Length; i++)
            {
                for (var j = i + 1; j < points.Length; j++)
                {
                    var dist = math.distancesq(points[i], points[j]);
                    if (dist >= smallestDist)
                        continue;
                    smallestDist = dist;
                    point1 = points[i];
                    point2 = points[j];
                }
            }
            points.Dispose();
            Assert.GreaterOrEqual(smallestDist, rSqr,
                $"The points { point1 } and { point2 } are spaced less than the minimum square distance allowed");
        }
#endif
    }
}
