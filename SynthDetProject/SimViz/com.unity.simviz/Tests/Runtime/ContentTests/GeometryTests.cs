using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;

namespace UnityEngine.SimViz.Content.ContentTests
{
    public class GeometryTests
    {
#if UNITY_EDITOR
        const float k_Delta = 0.0001f;
        
        static void AssertSamplesEqual(PointSampleGlobal sampleExpected, PointSampleGlobal sampleActual,
            float positionTolerance = k_Delta, float rotationTolerance = k_Delta)
        {
            var posDelta = sampleExpected.pose.pos - sampleActual.pose.pos;
            var posDeltaMagnitude = math.sqrt(math.dot(posDelta, posDelta));
            Assert.AreEqual(0f, posDeltaMagnitude, positionTolerance,
                "Sample positions are different. Expected " +
                $"{sampleExpected.pose.pos.x:E}, {sampleExpected.pose.pos.y:E}, {sampleExpected.pose.pos.z:E}, " +
                $"was {sampleActual.pose.pos.x:E}, {sampleActual.pose.pos.y:E}, {sampleActual.pose.pos.z:E}");
            var rotProduct = math.dot(sampleExpected.pose.rot, sampleActual.pose.rot);
            // NOTE: The closer the dot product is to 1, the more similar two quaternions are
            Assert.AreEqual(1f, math.abs(rotProduct), rotationTolerance,
                "Sample quaternions were insufficiently similar.");
        }
        
        static void AssertSamplesEqual(PointSampleLocal sampleExpected, PointSampleLocal sampleActual,
            float positionTolerance = k_Delta, float rotationTolerance = k_Delta)
        {
            var posDelta = sampleExpected.position - sampleActual.position;
            var posDeltaMagnitude = math.sqrt(math.dot(posDelta, posDelta));
            Assert.AreEqual(0f, posDeltaMagnitude, positionTolerance,
                "Sample positions are different. Expected " +
                $"{sampleExpected.position.x:E}, {sampleExpected.position.y:E}, " +
                $"was {sampleActual.position.x:E}, {sampleActual.position.y:E}");
            Assert.AreEqual(sampleExpected.headingRadians, sampleActual.headingRadians, rotationTolerance);
        }
        
        // Because all of these tests were written assuming open drive coordinates, but our geometry is in 
        // Unity coordinates, rather than re-write all the test cases, we just convert from OpenDRIVE to Unity here
        static Geometry ConstructGeometryFromOpenDriveCoordinates(
            float headingDegrees, float length, float sAbsolute, float x, float z, 
            GeometryKind geometryKind, ArcData arcData = default, SpiralData spiralData = default, 
            Poly3Data poly3Data = default, ParamPoly3Data paramPoly3Data = default)
        {
            var pose = OpenDriveMapElementFactory.FromOpenDriveGlobalToUnity(math.radians(headingDegrees), x, z);
            return new Geometry()
            {
                length = length,
                sRoad = sAbsolute,
                startPose = pose,
                geometryKind = geometryKind,
                arcData = arcData,
                spiralData = spiralData,
                poly3Data = poly3Data,
                paramPoly3Data = paramPoly3Data
            };
        }

        static PointSampleGlobal ConstructPointSampleFromOpenDriveCoordinates(float x, float y, float heading)
        {
            var translation = new float3(-y, 0, x);
            var rotation = quaternion.RotateY(-math.radians(heading));
            var pose = new RigidTransform(rotation, translation);
            return new PointSampleGlobal(pose);
        }

        static IEnumerable<TestCaseData> CoordinateTransformTests()
        {
            yield return new TestCaseData(
                RigidTransform.identity, 
                new PointSampleLocal(), 
                new PointSampleGlobal(RigidTransform.identity));
            yield return new TestCaseData(
                RigidTransform.identity, 
                new PointSampleLocal(1, 0, 0),
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(0, 0, 1))));
            yield return new TestCaseData(
                RigidTransform.identity, 
                new PointSampleLocal(0, 1, 0),
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(-1, 0, 0))));
            yield return new TestCaseData(
                new RigidTransform(quaternion.RotateY(math.PI/2f), new float3(-1, 1, 1)),
                new PointSampleLocal(1, -1, -math.PI/2f),
                new PointSampleGlobal(new RigidTransform(quaternion.RotateY(math.PI), new float3(0, 1, 0))));
            yield return new TestCaseData(
                new RigidTransform(quaternion.RotateY(math.PI/2f), new float3(2, 0, 1)),
                new PointSampleLocal(3, 1, math.PI/2f),
                new PointSampleGlobal(new RigidTransform(quaternion.identity, new float3(5, 0, 2))));
        }

        [Test]
        [TestCaseSource(nameof(CoordinateTransformTests))]
        public void TestFromLocalToGlobal(RigidTransform geometryPose, PointSampleLocal pointIn,
            PointSampleGlobal pointExpected)
        {
            var pointActual = GeometrySampling.FromLocalToGlobal(geometryPose, pointIn);
            AssertSamplesEqual(pointExpected, pointActual);
        }

        [Test]
        [TestCaseSource(nameof(CoordinateTransformTests))]
        public void TestFromGlobalToLocal(RigidTransform geometryPose, PointSampleLocal pointExpected,
            PointSampleGlobal pointIn)
        {
            var pointActual = GeometrySampling.FromGlobalToLocal(geometryPose, pointIn);
            AssertSamplesEqual(pointExpected, pointActual);
        }

        static IEnumerable<TestCaseData> ComputeCircleTestCases()
        {
            yield return new TestCaseData(0, 1, new PointSampleLocal(0, 0, 0));
            yield return new TestCaseData(math.PI / 4f, 1, 
                new PointSampleLocal(math.sqrt(2)/2, 1 - math.sqrt(2)/2, math.PI/4f));
            yield return new TestCaseData(math.PI / 2f, 1, new PointSampleLocal(1, 1, math.PI/2f));
            yield return new TestCaseData(math.PI / 2f, -1, new PointSampleLocal(1, -1, -math.PI/2f));
            yield return new TestCaseData(math.PI, 1, new PointSampleLocal(0, 2, math.PI));
        }

        [Test]
        [TestCaseSource(nameof(ComputeCircleTestCases))]
        public void TestComputeCircleSample(float s, float curvature, PointSampleLocal pointExpected)
        {
            var pointActual = GeometrySampling.BuildCircleSample(s, curvature);
            AssertSamplesEqual(pointExpected, pointActual);
        }

        static IEnumerable<TestCaseData> SampleArcTestCases()
        {
            yield return new TestCaseData(0, 0, 0, 0, 0);
            yield return new TestCaseData(1, 0, 0, 0, 0);
            yield return new TestCaseData(-1, 0, 0, 0, 0);
            yield return new TestCaseData(1, (float)Math.PI, 0, 2, 180);
            yield return new TestCaseData(-1, (float)Math.PI, 0, -2, 180);
            yield return new TestCaseData(2, (float)Math.PI, 0, 0, 0);
            yield return new TestCaseData(.5f, (float)Math.PI, 2, 2, 90);
        }


        [Test]
        [TestCaseSource(nameof(SampleArcTestCases))]
        public void SampleArc_ReturnsValuesAlongCurve(
            float curvature, float s, float xExpected, float yExpected, float headingExpectedDegrees)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 3f * math.PI,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Arc,
                arcData : new ArcData(curvature)
            );
            var sampleActual = geometry.Sample(s);
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(xExpected, yExpected, headingExpectedDegrees);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }
        
        [Test]
        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 3, 0, 3, 0)]
        [TestCase(0, 1, 0, 1, 0)]
        [TestCase(90, 1, 0, 0, 1)]
        [TestCase(-90, 1, 1, 0, 0)]
        [TestCase(180, 1, 0, -1, 0)]
        public void SampleLine_WithHeading_ReturnsCorrectValues(
            float headingOpenDriveDegrees, float s, float xExpected, float zExpected, float sOffset)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : headingOpenDriveDegrees,
                length : 3,
                sAbsolute : sOffset,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Line
                );
            var sampleActual = geometry.Sample(s);
            var sampleExpected = new PointSampleGlobal(new RigidTransform(
                quaternion.RotateY(math.radians(-headingOpenDriveDegrees)),
                new float3(xExpected, 0, zExpected)));
            AssertSamplesEqual(sampleExpected, sampleActual);
            // Since we start the segment at the origin, the length should be equal to the L2 norm for this point
            var lineLength = math.length(sampleActual.pose.pos);
            Assert.IsTrue(Mathf.Approximately(s - sOffset, lineLength),
                $"Expected line length to equal s ({s}) but was {lineLength}");
        }
        
        [Test]
        [TestCase(0, 0, 0, 0)]
        [TestCase(1, 1, 0, 0)]
        [TestCase(2, 2, 0, 0)]
        public void SampleSpiral_WithZeroCurvature_ReturnsSameAsLine(
            float s, float xExpected, float yExpected, float headingExpectedDegrees)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 3,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(0f, 0f)
            );
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(xExpected, yExpected, headingExpectedDegrees);
            var sampleActual = geometry.Sample(s);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }
        
        [Test]
        [TestCaseSource(nameof(SampleArcTestCases))]
        public void SampleSpiral_WithConstantCurvature_ReturnsSameAsArc(
            float curvature, float s, float xExpected, float yExpected, float headingExpectedDegrees)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 3,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(curvature, curvature));
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(xExpected, yExpected, headingExpectedDegrees);
            var sampleActual = geometry.Sample(s);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }
        
        [Test]
        public void SampleSpiral_WithInitialHeading_ReturnsCorrectValues()
        {
            var geometryHeading = 180f;
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : geometryHeading,
                length : 2,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(0, 1));
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(-1.8090484f, -0.6205365f,
                1 * Mathf.Rad2Deg + geometryHeading);
            var sampleActual = geometry.Sample(2);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }
        
        [Test]
        public void SampleSpiral_WithNegativeCurvature_ReturnsCorrectValues()
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 2,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(-.1f, -.3f));
            //The negative curvature should put the sample in the top-left quadrant
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(1.958977f, -.3293728f, -22.91831f);
            var sampleActual = geometry.Sample(2);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }

        [Test]
        public void SampleSpiral_WithNegativeToPositiveCurvature_ReturnsCorrectValues()
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 90,
                length : 2,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(-.05f, .05f));
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(.03333106f, 1.999666f, 90);
            var sampleActual = geometry.Sample(2);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }
        
        [Test]
        [TestCase(0, 0, 0, 0, Description = "S=zero should be at the origin")]
        [TestCase(0.1f, 0.1f, 0.000966f, 1.08861f, Description = "S close to zero should have positive curvature and be headingOpenDriveDegrees roughly towards +x")]
        [TestCase(1, .997335f, .06659051f, 5.7296f, Description = "S at midpoint should be roughly at (1, -.05) and headingOpenDriveDegrees right and slightly downward")]
        [TestCase(2, 1.99467f, .133181f, 0, Description = "S at end should be roughly at (.1, 2) and headingOpenDriveDegrees towards +y")]
        public void SampleSpiral_WithPositiveToNegativeCurvature_ReturnsCorrectValues(float s, float xExpected, float yExpected, float headingExpectedDegrees)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 2,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Spiral,
                spiralData : new SpiralData(.2f, -.2f));
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(xExpected, yExpected, headingExpectedDegrees);
            var sampleActual = geometry.Sample(s);
            
            AssertSamplesEqual(sampleExpected, sampleActual);
        }

        public static IEnumerable<TestCaseData> LineLikeSamplePoly3Data()
        {
            yield return new TestCaseData(
                new Poly3Data(Vector4.zero),
                0f, new Vector2(0f, 0f), 0f);
            yield return new TestCaseData(
                new Poly3Data(Vector4.zero),
                .5f, new Vector2(.5f, 0f), 0f);
            yield return new TestCaseData(
                new Poly3Data(Vector4.zero),
                1, new Vector2(1, 0f), 0f);
        }

        public static IEnumerable<TestCaseData> SamplePoly3Data()
        {
            yield return new TestCaseData(
                new Poly3Data(new Vector4(1f, 0f)), 0f, new Vector2(0f, 1f), 0f)
                .SetDescription("Constant factor test #1");
            
            yield return new TestCaseData(
                new Poly3Data(new Vector4(-1f, 0f)), 0f, new Vector2(0f, -1f), 0f)
                .SetDescription("Constant factor test #2");

            yield return new TestCaseData(
                    new Poly3Data(new Vector4(-1f, 0f)), 10f, new Vector2(10f, -1f), 0f)
                .SetDescription("Constant factor test #3");

            //Correct solution for x coordinate:
            //https://www.wolframalpha.com/input/?source=nav&i=integrate+sqrt(1%2B(2*.2*x)%5E2)+x%3D0...+.975687
            //Correct solution for headingOpenDriveDegrees:
            //https://www.wolframalpha.com/input/?source=nav&i=-atan(2*.2*.975687)+radians+in+degrees
            yield return new TestCaseData(
                new Poly3Data(new Vector4(0f, 0f, .2f, 0f)), 1, new Vector2(.975687f, .19039f), 21.319464f)
                .SetDescription(".2x^2 @ 1");
            
            //Correct solution for x coordinate is .9438:
            //Correct solution for headingOpenDriveDegrees:
            //https://www.wolframalpha.com/input/?source=nav&i=-atan(3*.3*.942668%5E2)+radians+in+degrees
            yield return new TestCaseData(
                    new Poly3Data(new Vector4(0f, 0f, 0f, .3f)), 1, new Vector2(.942668f, .251301f), 38.65137f)
                .SetDescription(".3x^3 @ 1");
            
            // Solution for arc length (s):
            // https://www.wolframalpha.com/input/?i=for+y%3D2x%5E3+arc+length+from+0+to+3
            // Heading is atan(2 * 3 * (3)^2)
            yield return new TestCaseData(
                    new Poly3Data(new Vector4(0f, 0f, 0f, 2f)), 54.47684f, new Vector2(3, 54), 88.9390883f)
                .SetDescription("2x^3 @ 3");
        }

        [Test]
        [TestCaseSource(nameof(LineLikeSamplePoly3Data))]
        [TestCaseSource(nameof(SamplePoly3Data))]
        public void SamplePoly3_ReturnsCorrectValues(Poly3Data poly3Data, float s, Vector2 samplePositionExpected, float headingExpectedDegrees)
        {
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : 0,
                length : 2,
                sAbsolute : 0,
                x : 0,
                z : 0,
                geometryKind : GeometryKind.Poly3,
                poly3Data : poly3Data);
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(
                samplePositionExpected.x, samplePositionExpected.y, headingExpectedDegrees);
            var sampleActual = geometry.Sample(s);
            
            AssertSamplesEqual(sampleExpected, sampleActual, rotationTolerance: 0.001f);
        }

        [Test]
        [TestCaseSource(nameof(LineLikeSamplePoly3Data))]
        [TestCaseSource(nameof(SamplePoly3Data))]
        public void SamplePoly3_WithInitialPose_ReturnsCorrectValues(Poly3Data poly3Data, float s, Vector2 samplePositionExpected, float headingExpectedDegrees)
        {
            var initialPosition = new Vector2(12, -5);
            var geometryHeading = 10;
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : geometryHeading,
                length : 2,
                sAbsolute : 5,
                x : initialPosition.x,
                z : initialPosition.y,
                geometryKind : GeometryKind.Poly3,
                poly3Data : poly3Data);
            var positionExpected =
                (Vector2) (Quaternion.AngleAxis(-geometryHeading, Vector3.back) * samplePositionExpected) + initialPosition;
            var sampleExpected = ConstructPointSampleFromOpenDriveCoordinates(
                positionExpected.x, positionExpected.y, headingExpectedDegrees + geometryHeading);
            var sampleActual = geometry.Sample(s + 5);
            
            AssertSamplesEqual(sampleExpected, sampleActual, rotationTolerance: 0.001f);
        }

        [Test]
        [TestCaseSource(nameof(LineLikeSamplePoly3Data))]
        [TestCaseSource(nameof(SamplePoly3Data))]
        public void SampleParamPoly3_ReturnsCorrectValuesForPoly3Inputs(
            Poly3Data poly3Data, float s, Vector2 samplePositionExpected, float headingExpectedDegrees)
        {
            var pPoly3 = new ParamPoly3Data(poly3Data);
            var initialPosition = new Vector2(12, -5);
            var geometryHeading = 10;
            var geometry = ConstructGeometryFromOpenDriveCoordinates(
                headingDegrees : geometryHeading,
                length : 2 + s,
                sAbsolute : 0,
                x : initialPosition.x,
                z : initialPosition.y,
                geometryKind : GeometryKind.ParamPoly3,
                paramPoly3Data : pPoly3);
            var positionExpected =
                (Vector2) (Quaternion.AngleAxis(-geometryHeading, Vector3.back) * samplePositionExpected) + initialPosition;
            var sampleExpectedGlobal = ConstructPointSampleFromOpenDriveCoordinates(
                positionExpected.x, positionExpected.y, headingExpectedDegrees + geometryHeading);
            var sampleExpected = GeometrySampling.FromGlobalToLocal(geometry.pose, sampleExpectedGlobal);
            var poly3SampleState = new SamplingStatePoly3(geometry.paramPoly3Data, 0.01f);
            var sampleActual = GeometrySampling.SamplePoly3(ref poly3SampleState, s);
           
            AssertSamplesEqual(sampleExpected, sampleActual, positionTolerance: 0.01f, rotationTolerance: 0.01f);
        }
        
        public static IEnumerable<TestCaseData> SampleParamPoly3Data()
        {
            yield return new TestCaseData(new ParamPoly3Data(
                new float4(0, 9, 9, 0),
                new float4(0, 0, 9, 3f),
                Poly3PRange.Normalized), 
                new PointSampleLocal(0, 0, 0),
                new PointSampleLocal(18, 12, math.PI / 4f),
                // From Wolfram Alpha: parametric plot (9 * t  + 9* t^2,9*t^2 + 3 * t^3) from 0 to 1
                21.996722303010f);
        }

        [Test]
        [TestCaseSource(nameof(SampleParamPoly3Data))]
        public void SampleParamPoly3_ReturnsCorrectStartAndEndPoints(
            ParamPoly3Data poly3, PointSampleLocal startExpected, PointSampleLocal endExpected, float sEnd)
        {
            var samplingState = new SamplingStatePoly3(poly3);
            var startActual = GeometrySampling.SamplePoly3(ref samplingState, 0);
            AssertSamplesEqual(startExpected, startActual);
            var endActual = GeometrySampling.SamplePoly3(ref samplingState, sEnd);
            // Check state object first to see if traversal worked
            Assert.IsTrue(samplingState.NextValues.s >= sEnd, "Sampling didn't traverse far enough along the line. " + 
                          $"Expected to get to s={sEnd} but only got to s={samplingState.NextValues.s}");
            Assert.IsTrue(samplingState.CurrentValues.s <= sEnd, "Sampling traversed past end of line. +" +
                          $"Should have stopped at s={sEnd} but went to s={samplingState.CurrentValues.s}");
            // Because we're treating these polynomials as normalized, p will always be 1.0f when s = sEnd
            Assert.IsTrue(samplingState.NextValues.p >= 1.0f, "Sampling didn't traverse far enough along the line. " + 
                          $"Expected to get to p=1.0 but only got to p={samplingState.NextValues.p}");
            Assert.IsTrue(samplingState.CurrentValues.p <= 1.0f, "Sampling traversed past end of line. +" +
                          $"Should have stopped at p=1.0 but went to p={samplingState.CurrentValues.p}");
            AssertSamplesEqual(endExpected, endActual, positionTolerance: 0.001f);
        }
#endif
    }
}
