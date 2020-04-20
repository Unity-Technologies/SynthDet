using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Cloud agnostic consumer for uploading data.
    /// </summary>
    public class SignedURLDataConsumer : BaseDataConsumer<bool>, IDataProduced
    {
        // Public Members

        [RuntimeInitializeOnLoadMethod]
        static void RegisterSelfAsConsumer()
        {
            Manager.Instance.StartNotification += () =>
            {
                if (Configuration.Instance.SimulationConfig.signlynx_host != null)
                {
                    Manager.Instance.RegisterDataConsumer(new SignedURLDataConsumer());
                }
            };
        }

        struct Response
        {
            public string url;
        }

        /// <summary>
        /// Synchronously uploads data to the cloud.
        /// </summary>
        /// <param name="localPath">The path to the file on the local file system.</param>
        /// <param name="objectPath">The path to the file on the remote cloud system..</param>
        public override bool Upload(string localPath, string objectPath)
        {
            FileStream file = null;

            try
            {
                file = File.OpenRead(localPath);
            }
            catch(Exception e)
            {
                Log.E($"Failed to open the file {localPath} for synchronous upload. Exception: {e.Message}", kUseConsoleLog);
                return false;
            }

            try
            {
                var buffer = new byte[file.Length];
                file.Read(buffer, 0, buffer.Length);

                var json = _GetSignedURL(objectPath);
                if (string.IsNullOrEmpty(json))
                {
                    throw new Exception("_GetSignedURL failed to return a valid json object.");
                }
                var rsp = JsonUtility.FromJson<Response>(json);
                if (string.IsNullOrEmpty(rsp.url))
                {
                    throw new Exception("_GetSignedURL returned an invalid url.");
                }
                _UploadAsync(rsp.url, buffer);
                return true;
            }
            catch (Exception e)
            {
                Log.E(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Asynchronously uploads data to the cloud.
        /// </summary>
        /// <param name="source">Open stream representing the data to upload.</param>
        /// <param name="objectPath">The path to the file on the remote cloud system..</param>
        public override Task<bool> UploadAsync(Stream source, string objectPath)
        {
            return Task<bool>.Run(() =>
            {
                try
                {
                    var buffer = new byte[source.Length];
                    source.Read(buffer, 0, buffer.Length);

                    var json = _GetSignedURL(objectPath);
                    if (string.IsNullOrEmpty(json))
                    {
                        throw new Exception("_GetSignedURL failed to return a valid json object.");
                    }
                    var rsp = JsonUtility.FromJson<Response>(json);
                    if (string.IsNullOrEmpty(rsp.url))
                    {
                        throw new Exception("_GetSignedURL returned an invalid url.");
                    }
                    _UploadAsync(rsp.url, buffer);
                    return true;
                }
                catch (Exception e)
                {
                    Log.E(e.ToString());
                    return false;
                }
            });
        }

        /// <summary>
        /// Converts a local file system path into a remote cloud path.
        /// </summary>
        /// <param name="localPath">The path to the file on the local file system.</param>
        /// <returns> The remote cloud system path. </returns>
        public override string LocalPathToObjectPath(string localPath)
        {
            return Path.Combine(Path.GetFileName(Path.GetDirectoryName(localPath)), Path.GetFileName(localPath));
        }

        // IDataProduced

        /// <summary>
        /// Initializes the signed URL uploader
        /// If not running in the cloud, or no signed URL endpoint is specified, then this consumer is disabled.
        /// </summary>
        public bool Initialize()
        {
            return Configuration.Instance.IsSimulationRunningInCloud() && !string.IsNullOrEmpty(Configuration.Instance.SimulationConfig.signlynx_host) && Configuration.Instance.SimulationConfig.signlynx_port != 0;
        }

        // Non-Public Members

        string _GetSignedURL(string objectPath)
        {
            objectPath = Uri.EscapeDataString($"{Configuration.Instance.SimulationConfig.app_param_id}/instance:{Configuration.Instance.SimulationConfig.instance_id}/attempt:{Configuration.Instance.SimulationConfig.current_attempt}/{objectPath}");
            var host = $"http://{Configuration.Instance.SimulationConfig.signlynx_host}:{Configuration.Instance.SimulationConfig.signlynx_port}";
            var url  = $"{host}/api/v1/pre-signed-url-put/{objectPath}";
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                _SetAuthHeaders(message.Headers, ContentType.ForPath(objectPath));
                var response = _client.SendAsync(message); response.Wait();
                var result   = response.Result.Content.ReadAsStringAsync(); result.Wait();
                return result.Result;
            }
        }

        void _UploadAsync(string url, byte[] data)
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/octet-stream");
            var response = _client.PutAsync(url, content); response.Wait();
            if (!response.Result.IsSuccessStatusCode)
            {
                var responseText = response.Result.Content.ReadAsStringAsync(); responseText.Wait();
                throw new Exception($"_UploadAsync upload to {url} failed with status code {response.Result.StatusCode} and reason {response.Result.ReasonPhrase} content {responseText.Result}");
            }
        }

        static void _SetAuthHeaders(HttpRequestHeaders headers, string contentType = null, string accessToken = null)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            }

            var token = Configuration.Instance.SimulationConfig.bearer_token;
            if (!string.IsNullOrEmpty(token))
            {
                headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        HttpClient _client = new HttpClient();
    }
}
