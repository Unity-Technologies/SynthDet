using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.TestTools;
using UnityEngine.SimViz.Content.Sampling;
using UnityEngine.SimViz.Scenarios;
using UnityEngine.SimViz.Content;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace SimViz.TestHelpers
{
    [PrebuildSetup(typeof(SimvizPrebuildSetup))]
    public class SimvizTestBaseSetup
    {
        public class SimvizPrebuildSetup : IPrebuildSetup
        {
            public TestHelpers testHelpers = new TestHelpers();
            [SetUp]
            public void Setup()
            {
#if UNITY_EDITOR
                testHelpers.TestXodrImport();
#endif
            }
            [TearDown]
            public void TearDown()
            {
#if UNITY_EDITOR
                testHelpers.CleanUpXodrFiles();
#endif
            }
        }

        public class XodrTestData
        {
            public static IEnumerable<TestCaseData> TestCases
            {
                get
                {
                    yield return new TestCaseData("Assets/LakeMarcel.xodr");
                    yield return new TestCaseData("Assets/Crossing8Course.xodr");
                    yield return new TestCaseData("Assets/CircleCourse.xodr");
                }
            }
        }

        protected IEnumerator SkipFrame(int frames)
        {
            Debug.LogFormat("Skipping {0} Frames", frames);
            for (int f = 0; f < frames; f++)
            {
                yield return null;
            }
        }
    }

    public class TestHelpers : SimvizTestBaseSetup
    {
        public enum MeshGenerationType
        {
            MeshLanes,
            MeshLineRenderer,
            MeshRoad
        }

        private List<RoadNetworkDescription> roadNetworkList = new List<RoadNetworkDescription>();
        public RoadNetworkDescription testRoad;

        public List<string> PackageXodrList = new List<string>();
        public List<string> ImportedXodrList = new List<string>();

        private bool xodrContainsRoads = false;
        private bool xodrContainsJunctions = false;

        public GameObject wayPointObj;

#if UNITY_EDITOR
        public void TestXodrImport()
        {
            var allpaths = AssetDatabase.GetAllAssetPaths();
            foreach (var targetPath in allpaths)
            {
                if (targetPath.Contains("com.unity.simviz") &&
                    targetPath.Contains("Tests") &&
                    targetPath.Contains("Editor") &&
                    targetPath.Contains("XodrFiles"))
                {
                    if (targetPath.Contains(".xodr"))
                    {
                        PackageXodrList.Add(targetPath);
                        Debug.Log("Found a xodr file for import! : " + targetPath);

                        var removeFilePath = targetPath.Remove(0, targetPath.LastIndexOf("/") + 1);
                        var finalXodrFileName = removeFilePath.Substring(0, removeFilePath.LastIndexOf("."));
                        Debug.Log("Final path of xodr! : " + finalXodrFileName);

                        var copyPath = "Assets/" + finalXodrFileName;
                        ImportedXodrList.Add(copyPath);
                        AssetDatabase.CopyAsset(targetPath, copyPath);
                    }
                }
            }
        }

        public void CleanUpXodrFiles()
        {
            foreach (var xodrPath in ImportedXodrList)
                File.Delete(xodrPath);

            ImportedXodrList.Clear();
        }

        public bool VerifyXodrFile(string file)
        {
            // Get all the paths
            var allpaths = AssetDatabase.GetAllAssetPaths();
            foreach (var targetPath in allpaths)
            {
                if (targetPath == file)
                {
                    if (targetPath.Contains(".xodr"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool VerifyRoadsJunctionsInXodrFile(string file)
        {
            using (StreamReader streamReader = new StreamReader(file))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null
                       && xodrContainsRoads != true
                       && xodrContainsJunctions != true)
                {
                    if (line.Contains("road"))
                        xodrContainsRoads = true;
                    if (line.Contains("junction"))
                        xodrContainsJunctions = true;
                }
            }

            if (xodrContainsJunctions == true && xodrContainsRoads == true)
                return true;
            else
                return false;
        }

        public void GetTestRoadNetwork(string file, out RoadNetworkDescription road)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(file);
            if (!(obj is RoadNetworkDescription))
            {
                Debug.LogErrorFormat("{0} extension unrecognized, please select one .xodr file", file);
            }
            roadNetworkList.Add((RoadNetworkDescription)obj);
            road = (RoadNetworkDescription)obj;
        }

        public void GenerateWayPointPathFromXodr(string file)
        {
            GetTestRoadNetwork(file, out testRoad);

            wayPointObj = new GameObject($"{testRoad.name}");
            var reference = wayPointObj.AddComponent<RoadNetworkReference>();
            var roadNetworkPath = wayPointObj.AddComponent<RoadNetworkPath>();

            reference.RoadNetwork = testRoad;
            roadNetworkPath.RoadNetwork = testRoad;

            roadNetworkPath.GenerateNewWaypointPath();
        }

        public void GenerateRandomWayPointPathFromXodr(string file)
        {
            GetTestRoadNetwork(file, out testRoad);

            wayPointObj = new GameObject($"{testRoad.name}");
            var reference = wayPointObj.AddComponent<RoadNetworkReference>();
            var roadNetworkPath = wayPointObj.AddComponent<RoadNetworkPath>();

            reference.RoadNetwork = testRoad;
            roadNetworkPath.RoadNetwork = testRoad;

            roadNetworkPath.GenerateRandomizedPath();
        }
#endif

        public void GenerateMeshTypeRoads(RoadNetworkDescription road, MeshGenerationType roadMeshGenerationType)
        {
            if (roadMeshGenerationType == MeshGenerationType.MeshRoad)
            {
                RoadNetworkMesher.GenerateMesh(road);
            }
            else if (roadMeshGenerationType == MeshGenerationType.MeshLanes)
            {
                RoadNetworkMesher.GenerateLineRenderer(road);
            }
            else if (roadMeshGenerationType == MeshGenerationType.MeshLineRenderer)
            {
                RoadNetworkMesher.GenerateLineRenderer(road);
            }
        }
    }
}

