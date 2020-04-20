using System;
using System.Diagnostics;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;

using ZipUtility;

using Debug = UnityEngine.Debug;
#endif

namespace Unity.Simulation.Client
{
    /// <summary>
    /// Class for retrieving project, scenes, and building a project for uploading.
    /// </summary>
    public static class Project
    {
        // Public Members

        /// <summary>
        /// Retrieves a list of the projects you have created.
        /// </summary>
        /// <returns> An array of ProjectInfo structs. </returns>
        public static ProjectInfo[] GetProjects()
        {
            var url   = Config.apiEndpoint + "/v1/projects";

            using (var webrx = UnityWebRequest.Get(url))
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.timeout = Config.timeoutSecs;
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("Unable to get projects.");

                var array = JsonUtility.FromJson<ProjectArray>(webrx.downloadHandler.text);
                return array.projects;
            }
        }

        /// <summary>
        /// Sets the active project id to the value specified.
        /// If no project id is specified, then the current project Cloud Project Id is used.
        /// </summary>
        public static void Activate(string projectId = null)
        {
            if (string.IsNullOrEmpty(projectId))
                projectId = CloudProjectSettings.projectId;
            _activeProjectId = projectId;
        }

        /// <summary>
        /// Deactivates the currently active project id.
        /// </summary>
        public static void Deactivate()
        {
            _activeProjectId = null;
        }

        /// <summary>
        /// Returns the currently active project id.
        /// </summary>
        public static string activeProjectId
        {
            get 
            {
                if (string.IsNullOrEmpty(_activeProjectId))
                    _activeProjectId = CloudProjectSettings.projectId;
                Debug.Assert(!string.IsNullOrEmpty(_activeProjectId), "activeProjectId is invalid. Make sure you either activate a project, or you have created/linked a cloud project id in the services window.");
                return _activeProjectId;
            }
        }

        /// <summary>
        /// Retrieves a list of the scenes you currently have open in the editor.
        /// </summary>
        public static string[] GetOpenScenes()
        {
            var countLoaded = SceneManager.sceneCount;
            var loadedScenes = new string[countLoaded];
            for (int i = 0; i < countLoaded; i++)
                loadedScenes[i] = SceneManager.GetSceneAt(i).path;
            return loadedScenes;
        }

        /// <summary>
        /// Retrieves a list of the scenes that are currently added to the build in build settings.
        /// </summary>
        public static string[] GetBuildSettingScenes()
        {
            var countLoaded = SceneManager.sceneCountInBuildSettings;
            var loadedScenes = new string[countLoaded];
            for (int i = 0; i < countLoaded; i++)
                loadedScenes[i] = SceneManager.GetSceneByBuildIndex(i).path;
            return loadedScenes;
        }

        /// <summary>
        /// Builds the current project.
        /// </summary>
        /// <param name="savePath"> The location where the build should be saved. Note that a new directory will be created at this location. </param>
        /// <param name="name"> Name for the build directory and executable. </param>
        /// <param name="scenes"> Array of scenes to be included in the build. </param>
        /// <param name="target"> The build target to build for. Defaults to StandaloneLinux64 </param>
        /// <param name="compress"> Flag for whether or not to compress the build executable and data directory into a zip file. </param>
        /// <param name="launch"> Flag for whether or not to launch the build. </param>
        public static void BuildProject(string savePath, string name, string[] scenes = null, BuildTarget target = BuildTarget.StandaloneLinux64, bool compress = true, bool launch = false)
        {
            Directory.CreateDirectory(savePath);

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.locationPathName = Path.Combine(savePath, name + ".x86_64");
            buildPlayerOptions.target           = target;
            buildPlayerOptions.options          = BuildOptions.None;
            buildPlayerOptions.scenes           = scenes;

            BuildReport  report  = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Build failed");
                return;
            }

            if (launch)
            {
                var exe = Path.Combine(Application.dataPath, "..", savePath + ".app");
                Debug.Log("Executing " + exe);
                Process.Start(exe);
            }

            if (compress)
                Zip.DirectoryContents(savePath, name);
        }

        // Protected / Private Members

        [RuntimeInitializeOnLoadMethod]
        public static void InitializeProjectId()
        {
            _activeProjectId = CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(_activeProjectId))
            {
                Debug.LogWarning($"CloudProjectSettings.projectId is invalid. Please create/link a project id in the services window.");
            }
        }

        static string _activeProjectId;
    }
}
