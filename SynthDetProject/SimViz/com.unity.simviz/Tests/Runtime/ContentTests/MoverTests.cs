using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.SimViz.Scenarios;

#if UNITY_EDITOR
using SimViz.TestHelpers;
#endif

namespace UnityEngine.SimViz.Content.ContentTests
{
#if UNITY_EDITOR
    public class MoverTests : SimvizTestBaseSetup
    {
        private TestHelpers testHelp = new TestHelpers();
        private GameObject cube;
        private GameObject wheelObj;
        private Vector3 currentPoint;
        private Quaternion wheelRot;
        private Quaternion oldWheelRot;
        private ControlPoint endPoint;
        private bool stillMoving = true;
        private GameObject trailerObj;

        [TearDown]
        public void MoverTearDown()
        {
            stillMoving = true;
            GameObject.Destroy(cube);
            GameObject.Destroy(wheelObj);
            GameObject.Destroy(GameObject.Find("Crossing8Course"));
            GameObject.Destroy(GameObject.Find("LakeMarcel"));
            GameObject.Destroy(GameObject.Find("CircleCourse"));
        }

        [UnityTest]
        public IEnumerator MoveAroundCircleCourse()
        {
            cube = SetupMoverObjects(5f, "Assets/CircleCourse.xodr");

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundLakeMarcel()
        {
            cube = SetupMoverObjects(80f, "Assets/LakeMarcel.xodr");

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundCrossing8Course()
        {
            cube = SetupMoverObjects(200f, "Assets/Crossing8Course.xodr", true);

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundCrossing8CoursePathLogging()
        {
            cube = SetupMoverObjects(200f, "Assets/Crossing8Course.xodr", true);

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundCrossing8CourseRaycastPosition()
        {
            cube = SetupMoverObjects(200f, "Assets/Crossing8Course.xodr", true, false, true);

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundCrossing8CourseCheckWheel()
        {
            cube = SetupMoverObjects(200f, "Assets/Crossing8Course.xodr", true);
            SetupWheel();

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                wheelRot = wheelObj.transform.rotation;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreNotEqual(oldWheelRot, wheelRot, "Wheel is not rotating");
            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        [UnityTest]
        public IEnumerator MoveAroundCrossing8CourseCheckTrailer()
        {
            cube = SetupMoverObjects(200f, "Assets/Crossing8Course.xodr", true);
            SetupTrailer();

            while (stillMoving == true)
            {
                currentPoint = cube.transform.position;
                if (currentPoint == endPoint.Point)
                {
                    stillMoving = false;
                }

                yield return null;
            }

            Assert.AreEqual(trailerObj.transform.position, endPoint.Point, "Trailer didn't make it to the end of the course");
            Assert.AreEqual(currentPoint, endPoint.Point, "Didn't make it to the end of the road");
        }

        public GameObject SetupMoverObjects(float speed, string xodrFilePath, bool genRandomWayPoint = false, bool pathLogging = false, bool raycastPosition = false)
        {
            if(!genRandomWayPoint)
                testHelp.GenerateWayPointPathFromXodr(xodrFilePath);
            else
                testHelp.GenerateRandomWayPointPathFromXodr(xodrFilePath);

            cube = new GameObject("Mover");
            cube.AddComponent<Camera>();
            cube.AddComponent<Mover>();

            var xodrFile = xodrFilePath.Remove(0, xodrFilePath.LastIndexOf("/") + 1);
            var waypointName = xodrFile.Substring(0, xodrFile.LastIndexOf("."));
            var waypointPath = GameObject.Find(waypointName).GetComponent<WaypointPath>();
            var pointCount = waypointPath.GetPointCount(0) - 1;
            var segmentCount = waypointPath.GetSegmentCount() - 1;

            var mover = cube.GetComponent<Mover>();
            mover.waypointPath = waypointPath;
            mover.debug = true;
            mover.speed = speed;
            mover.raycastPosition = raycastPosition;
            mover.pathLogging = pathLogging;

            endPoint = waypointPath.GetPoint(segmentCount, pointCount);

            return cube;
        }

        public void SetupWheel()
        {
            var mover = cube.GetComponent<Mover>();
            wheelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            oldWheelRot = wheelObj.transform.rotation;
            wheelObj.transform.position = new Vector3(0f, 0f, 0f);
            wheelObj.transform.parent = cube.transform;
            mover.wheels = new Transform[1] { wheelObj.transform };
        }

        public void SetupTrailer()
        {
            var mover = cube.GetComponent<Mover>();
            trailerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trailerObj.transform.position = new Vector3(0f, 0f, 0f);
            trailerObj.transform.parent = cube.transform;
            mover.trailer = trailerObj.transform;
        }
    }
#endif
}
