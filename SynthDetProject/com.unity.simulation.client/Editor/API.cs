using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    /// <summary>
    /// The API class encapsulates the REST API used by the Unity Simulation service.
    /// </summary>
    public static class API
    {
        // Public Members

#if !UNITY_EDITOR
        /// <summary>
        /// Authenticates the currently active project with the Unity Simulation service.
        /// </summary>
        /// <returns> void </returns>
        public static void Login()
        {
            Token.Load();

            var redirect_uri   = $"http://127.0.0.1:{Config.kDefaultRedirectUriPort}/v1/auth/login/callback";
            var usim_oauth_url = $"{Config.apiEndpoint}/v1/auth/login?redirect_uri={redirect_uri}";

            var httpListener = new HttpListener();
            httpListener.Prefixes.Add(redirect_uri + "/");
            httpListener.Start();

            Application.OpenURL(usim_oauth_url);

            var context  = httpListener.GetContext();
            var request  = context.Request;
            var token    = new Token(Config.apiEndpoint, request.QueryString);
            token.Save(Config.tokenFile);

            var text = Encoding.UTF8.GetBytes("You have been successfully authenticated to use Unity Simulation services.");
            context.Response.OutputStream.Write(text, 0, text.Length);
            context.Response.Close();
            httpListener.Stop();
        }

        /// <summary>
        /// Refreshes the auth token for the currently active project with the Unity Simulation service.
        /// </summary>
        /// <returns> void </returns>
        public static void Refresh()
        {
            var token = Token.Load(refreshIfExpired: false);
            token.Refresh();
            token.Save(Config.tokenFile);
        }
#endif//UNITY_EDITOR

        /// <summary>
        /// Retrieves the supported SysParams for the Unity Simulation service.
        /// </summary>
        /// <returns> Array of SysParamDefinition </returns>
        public static SysParamDefinition[] GetSysParams()
        {
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/sys_params";
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
                {
                    Debug.Log($"GetSysParams request failed. {webrx.error}");
                    throw new Exception("SysParams.Get failed");
                }

                return JsonUtility.FromJson<SysParamArray>(webrx.downloadHandler.text).sys_params;
            }
        }

        /// <summary>
        /// Uploads a build to the Unity Simulation Service.
        /// Note that the executable name must end with .x86_64, and the entire build must be zipped into a single archive.
        /// </summary>
        /// <param name="name"> Name for the build when uploaded. </param>
        /// <param name="location"> Path to the zipped archive. </param>
        /// <returns> Uploaded build id. </returns>
        public static string UploadBuild(string name, string location)
        {
            return Transaction.Upload($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/builds", name, location);
        }

        /// <summary>
        /// Download a build from Unity Simulation to a specific location.
        /// </summary>
        /// <param name="id"> Build upload id to download. </param>
        /// <param name="location"> Path to where you want the download to be saved. </param>
        /// <returns> Array of SysParamDefinition </returns>
        public static void DownloadBuild(string id, string location)
        {
            Transaction.Download($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/builds", id, location);
        }

        /// <summary>
        /// Serialize a struct and upload the JSON as an app param.
        /// </summary>
        /// <param name="name"> Name for uploaded resource. </param>
        /// <param name="location"> Struct value to be serialized and uploaded. </param>
        /// <returns> Uploaded app param id. </returns>
        public static string UploadAppParam<T>(string name, T param) where T : struct
        {
            return Transaction.Upload($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params", name, Encoding.UTF8.GetBytes(JsonUtility.ToJson(param)));
        }

        /// <summary>
        /// Download an app param from Unity Simulation to a specific location.
        /// </summary>
        /// <param name="id"> App param upload id to download. </param>
        /// <param name="location"> Path to where you want the download to be saved. </param>
        /// <returns> Copy of struct value. </returns>
        public static T DownloadAppParam<T>(string id)
        {
            return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(Transaction.Download($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params", id)));
        }

        /// <summary>
        /// Upload a run definition to Unity Simulation.
        /// </summary>
        /// <param name="definition"> Run definition to be uploaded. </param>
        /// <returns> Uploaded run definition id. </returns>
        public static string UploadRunDefinition(RunDefinition definition)
        {
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/run_definitions";
            using (var webrx = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.timeout = Config.timeoutSecs;
                webrx.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(definition)));
                webrx.downloadHandler = new DownloadHandlerBuffer();
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("Failed to upload run _definition");

                var response = JsonUtility.FromJson<RunDefinitionId>(webrx.downloadHandler.text);
                return response.definition_id;
            }
        }

        /// <summary>
        /// Download a run definition from Unity Simulation.
        /// </summary>
        /// <param name="id"> Run definition upload id to download. </param>
        /// <returns> RunDefinition struct value. </returns>
        public static RunDefinition DownloadRunDefinition(string definitionId)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Get, $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/run_definitions/{definitionId}"))
            {
                Transaction.SetAuthHeaders(message.Headers);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("DownloadRunDefinition failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunDefinition>(payload.Result);
            }
        }

        /// <summary>
        /// Download a summary of a run execution from Unity Simulation.
        /// </summary>
        /// <param name="id"> Execution id to summarize. </param>
        /// <returns> RunSummary struct value. </returns>
        public static RunSummary Summarize(string executionId)
        {
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}/summary";

            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Summarize: failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunSummary>(payload.Result);
            }
        }

        /// <summary>
        /// Download a description of a run exection from Unity Simulation.
        /// </summary>
        /// <param name="id"> Execution id to describe. </param>
        /// <returns> RunDescription struct value. </returns>
        public static RunDescription Describe(string executionId)
        {
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}";

            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Summarize: failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunDescription>(payload.Result);
            }
        }

        /// <summary>
        /// Download the manifest of uploaded artifacts for a run exection.
        /// </summary>
        /// <param name="executionId"> Execution id whose manifest you wish to download. </param>
        /// <returns> Dictionary of entry hash code mapped to ManifestEntry. </returns>
        /// <remarks> You can call this at any time, and multiple time, and the dictionary will contain new items that have been uploaded.</remarks>
        public static Dictionary<int, ManifestEntry> GetManifest(string executionId)
        {
            var entries = new Dictionary<int, ManifestEntry>();

            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}/data";

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
                    throw new Exception("Unable to get manifest.");

                var lines = webrx.downloadHandler.text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                for (var i = 1; i < lines.Length; ++i)
                {
                    var l = lines[i].Split(',');
                    Debug.Assert(l.Length == 6);
                    ManifestEntry e;
                    e.executionId = l[0];
                    e.appParamId  = l[1];
                    e.instanceId  = int.Parse(l[2]);
                    e.attemptId   = int.Parse(l[3]);
                    e.fileName    = l[4];
                    e.downloadUri = l[5];
                    entries.Add(e.GetHashCode(), e);
                }
            }

            return entries;
        }
    }
}
