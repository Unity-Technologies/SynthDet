

using System;
#if UNITY_EDITOR
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;

using Unity.Simulation;
using Unity.Simulation.Client;

using Debug = UnityEngine.Debug;

namespace Unity.Simulation.Client.Tests
{
    public static class TestUtility
    {
        public static bool IsAutomatedTestRun()
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "-runTests") >= 0;
        }
    }
    
    public struct TestAppParam
    {
        public int value;
        public TestAppParam(int value)
        {
            this.value = value;
        }
    }

    public class ClientTests
    {
        public static string projectName
        {
            get { return "TestBuild"; }
        }

        public static string projectPath
        {
            get { return Path.Combine(Application.dataPath, "..", "ClientTestBuild"); }
        }

        public static string zipPath
        {
            get { return Path.Combine(Directory.GetParent(projectPath).FullName, projectName + ".zip"); }
        }

        [Test]
        public void ClientTests_GetSysParamSucceeds()
        {
            if (!TestUtility.IsAutomatedTestRun())
            {
                var sysParams = API.GetSysParams();
                Assert.True(sysParams.Length != 0);
            }
        }

        [MenuItem("Simulation/Cloud/Build Test Project")]
        public static void BuildTestProject()
        {
            if (!TestUtility.IsAutomatedTestRun())
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                Assert.False(File.Exists(zipPath));
                var scenes = new string[]
                {
                    "Packages/com.unity.simulation.client/Tests/Editor/TestScene.unity",
                };
                Project.BuildProject(projectPath, projectName, scenes);
                Assert.True(File.Exists(zipPath));
            }
        }

       [UnityTest]
       [Timeout(600000)]
       public IEnumerator ClientTests_ExecuteRun()
       {
           if (!TestUtility.IsAutomatedTestRun())
           {
               var run = Run.Create("test", "test run");
               var sysParam = API.GetSysParams()[0];
               run.SetSysParam(sysParam);
               run.SetBuildLocation(zipPath);
               run.SetAppParam("test", new TestAppParam(1), 1);
               run.Execute();

               var stopwatch = new Stopwatch();
               stopwatch.Start();

               var timeoutSecs = 600; // 10 mins
               while (stopwatch.Elapsed.TotalSeconds < timeoutSecs && !run.completed)
                   yield return null;

               Debug.Log("Run completed.");

               var summary = API.Summarize(run.executionId);
               Assert.True(summary.num_success == 1);
           }
       }
    }
}

#endif // UNITY_EDITOR
