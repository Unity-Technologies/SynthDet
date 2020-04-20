using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    public class Run
    {
        // Public Members

        /// <summary>
        /// Returns the run definition id.
        /// </summary>
        public string definitionId { get; protected set; }

        /// <summary>
        /// Returns the run execution id.
        /// </summary>
        public string executionId { get; protected set; }

        /// <summary>
        /// Set/Get the build location for this run.
        /// </summary>
        public string buildLocation { get; set; }

        /// <summary>
        /// Returns the total number of instances for this run.
        /// </summary>
        public int instances { get; protected set; }

        /// <summary>
        /// Returns true when all instances have completed.
        /// Note that completed and success are not the same thing. 
        /// </summary>
        public bool completed
        {
            get
            {
                var summary = API.Summarize(executionId);
                var completedInstances = summary.num_failures + summary.num_not_run + summary.num_success;
                return completedInstances == instances;
            }
        }

        /// <summary>
        /// Map of currently uploaded app parameters for this run.
        /// </summary>
        public Dictionary<string, AppParam> appParameters = new Dictionary<string, AppParam>();

        /// <summary>
        /// Create a new run definition.
        /// </summary>
        /// <param name="name"> Name for this run. </param>
        /// <param name="description"> Description text for this run. </param>
        public static Run Create(string name = null, string description = null)
        {
            var run = new Run();
            run._definition.name = name;
            run._definition.description = description;
            return run;
        }

        /// <summary>
        /// Create a run definition instance from a previously uploaded run definition.
        /// </summary>
        /// <param name="definitionId"> The run definition id returned from a previous upload. </param>
        public static Run CreateFromDefinitionId(string definitionId)
        {
            var run = new Run();
            run.definitionId = definitionId;
            run._definition = API.DownloadRunDefinition(definitionId);
            return run;
        }

        /// <summary>
        /// Create a run definition instance from a previously uploaded run execution.
        /// </summary>
        /// <param name="executionId"> The run execution id returned from a previous run. </param>
        public static Run CreateFromExecutionId(string executionId)
        {
            var run = new Run();
            var description  = API.Describe(executionId);
            run._definition  = _DescriptionToDefinition(description);

            run.executionId  = executionId;
            run.definitionId = description.definition_id;
            return run;
        }

        /// <summary>
        /// Set the sys parameter to be used for this run.
        /// </summary>
        /// <param name="sysParam"> The sys param selected from GetSysParam. </param>
        public void SetSysParam(SysParamDefinition sysParam)
        {
            _sysParam = sysParam;
        }

        /// <summary>
        /// Sets the build location that will be uploaded.
        /// </summary>
        /// <param name="path"> The path to the zipped up build to be uploaded. </param>
        public void SetBuildLocation(string path)
        {
            buildLocation = path;
        }

        /// <summary>
        /// Add an app param to be uploaded. Struct T will be converted to JSON.
        /// </summary>
        /// <param name="name"> Name for the app param. </param>
        /// <param name="param"> Struct value to be converted to JSON and uploaded. </param>
        /// <param name="numInstances"> The number of instances to use this app param. </param>
        public string SetAppParam<T>(string name, T param, int numInstances) where T : struct
        {
            AppParam appParam;
            appParam.id            = API.UploadAppParam<T>(name, param);
            appParam.name          = name;
            appParam.num_instances = numInstances;

            if (!appParameters.ContainsKey(name))
            {
                instances += numInstances;
                appParameters.Add(name, appParam);
            }
            else
            {
                appParameters[name] = appParam;
            }

            Debug.Log($"AppParam {appParam.id} numInstances {appParam.num_instances}");

            return appParam.id;
        }

        /// <summary>
        /// Get a previously added app param.
        /// </summary>
        /// <param name="name"> Name of previously added app param. </param>
        public T GetAppParam<T>(string name) where T : struct
        {
            if (appParameters.ContainsKey(name))
            {
                var appParam = appParameters[name];
                return _GetAppParam<T>(appParam.id);
            }
            return default(T);
        }

        /// <summary>
        /// Executes this run definition.
        /// </summary>
        public void Execute()
        {
            if (string.IsNullOrEmpty(Project.activeProjectId))
            {
                if (string.IsNullOrEmpty(CloudProjectSettings.projectId))
                {
                    Debug.LogWarning("No project is active, and project id is not set. Perhaps you want to login?");
                    return;
                }
                Project.Activate(CloudProjectSettings.projectId);
            }

            // Upload Build
            if (_definition.build_id == null)
            {

                _definition.build_id = API.UploadBuild(_definition.name, buildLocation);
                Debug.Log("Build id " + _definition.build_id);
            }

            if (definitionId == null)
            {
                // Upload Run Definition
                _definition.sys_param_id = _sysParam.id;
                _definition.app_params   = new List<AppParam>(appParameters.Values).ToArray();
                definitionId = API.UploadRunDefinition(_definition);
                Debug.Log("Definition Id " + definitionId);
            }

            // Execute
            var runUrl = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs";
            using (var webrx = new UnityWebRequest(runUrl, UnityWebRequest.kHttpVerbPOST))
            {
                RunDefinitionId rdid;
                rdid.definition_id = definitionId;

                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);
                
                webrx.timeout = Config.timeoutSecs;
                webrx.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(rdid)));
                webrx.downloadHandler = new DownloadHandlerBuffer();
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("Failed to execute run _definition");

                var response = JsonUtility.FromJson<RunExecutionId>(webrx.downloadHandler.text);
                executionId = response.execution_id;

                Debug.Log("Execution Id " + executionId);
            }
        }

        /// <summary>
        /// Retrieves the player log for a specific instance.
        /// </summary>
        /// <param name="instance"> The instance whose player log you wish to retrieve. Defaults to 1.</param>
        public string[] GetPlayerLog(int instance = 1)
        {
            var manifest = API.GetManifest(executionId);
            foreach (var kv in manifest)
            {
                if (kv.Value.instanceId == instance && kv.Value.fileName == "Logs/Player.log")
                {
                    return Encoding.UTF8.GetString(_GetManifestEntry(kv.Value)).Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                }
            }
            return null;
        }

        // Protected / Private Members

        byte[] _GetManifestEntry(ManifestEntry entry)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Get, entry.downloadUri))
            {
                Transaction.SetAuthHeaders(message.Headers);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("_GetManifestEntry: failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsByteArrayAsync();
                payload.Wait();

                return payload.Result;
            }
        }

        static T _GetAppParam<T>(string id)
        {
            var url  = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params";
            var data = Transaction.Download(url, id);
            return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(data));
        }

        static RunDefinition _DescriptionToDefinition(RunDescription description)
        {
            RunDefinition definition;
            definition.name         = description.name;
            definition.description  = description.description;
            definition.build_id     = description.build_id;
            definition.sys_param_id = description.sys_param_id;
            definition.app_params   = description.app_params;
            return definition;
        }

        SysParamDefinition _sysParam;
        RunDefinition      _definition;
    }
}
