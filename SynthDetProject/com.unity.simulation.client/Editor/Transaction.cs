using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    internal static class Transaction
    {
        public static HttpClient client { get; private set; }

        static Transaction()
        {
            client = new HttpClient();
        }

        internal static void SetAuthHeaders(HttpRequestHeaders headers, string accessToken = null)
        {
            headers.Accept   .Add(new MediaTypeWithQualityHeaderValue("application/json" ));
            headers.UserAgent.Add(new ProductInfoHeaderValue         ("usim-cli", "0.0.0"));
            if (string.IsNullOrEmpty(accessToken))
            {
#if UNITY_EDITOR
                accessToken = CloudProjectSettings.accessToken;
#else
                accessToken = Config.token.accessToken;
#endif
            }
            headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        internal static string Upload(string url, string name, string inFile, bool useTransferUrls = true)
        {
            return Upload(url, name, File.ReadAllBytes(inFile), useTransferUrls);
        }

        internal static string Upload(string url, string name, byte[] data, bool useTransferUrls = true)
        {
            string entityId = null;

            Action<UnityWebRequest> action = (UnityWebRequest webrx) =>
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.timeout = Config.timeoutSecs;
                webrx.uploadHandler = new UploadHandlerRaw(data);
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("Failed to upload");

                if (!string.IsNullOrEmpty(webrx.downloadHandler.text))
                {
                    Debug.Assert(false, "Need to pull id from response");
                    // set entity return id here
                }
            };

            if (useTransferUrls)
            {
                var tuple = GetUploadURL(url, name);
                entityId  = tuple.Item2;
                using (var webrx = UnityWebRequest.Put(tuple.Item1, data))
                {
                    action(webrx);
                }
            }
            else
            {
                using (var webrx = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    action(webrx);
                }
            }

            Debug.Assert(!string.IsNullOrEmpty(entityId));

            return entityId;
        }

        internal static void Download(string url, string id, string outFile, bool useTransferUrls = true)
        {
            if (!Directory.Exists(outFile))
                throw new Exception("Download location must exist " + outFile);

            var data = Transaction.Download(url, id, useTransferUrls);
            if (data != null)
                File.WriteAllBytes(outFile, data);
        }

        internal static string GetDownloadUrl(string url, string id, bool useTransferUrls = true)
        {
            return useTransferUrls ? GetDownloadDetails(url, id).Item1 : $"{url}/{id}";
        }

        internal static byte[] Download(string url, string id, bool useTransferUrls = true)
        {
            url = useTransferUrls ? GetDownloadDetails(url, id).Item1 : $"{url}/{id}";

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
                    throw new Exception("Download failed " + id);

                return webrx.downloadHandler.data;
            }
        }

        // first = upload_uri, second = entity_id
        internal static Tuple<string, string> GetUploadURL(string url, string path)
        {
            var payload = JsonUtility.ToJson(new UploadInfo(Path.GetFileName(path), "Placeholder description"));

            using (var webrx = UnityWebRequest.Post(url, payload))
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                webrx.timeout = Config.timeoutSecs;
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("_GetUploadURL failed.");

                var data = JsonUtility.FromJson<UploadUrlData>(webrx.downloadHandler.text);
                return new Tuple<string, string>(data.upload_uri, data.id);
            }
        }

        // first = download_url, second = entity name
        internal static Tuple<string, string> GetDownloadDetails(string url, string id)
        {
            using (var webrx = UnityWebRequest.Get($"{url}/{id}"))
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.timeout = Config.timeoutSecs;
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("_GetDownloadDetails failed.");

                var data = JsonUtility.FromJson<DownloadDetails>(webrx.downloadHandler.text);
                return new Tuple<string, string>(data.download_uri, data.name);
            }
        }

        internal static void Delete(string url, string id, string entityType)
        {
            using (var webrx = UnityWebRequest.Delete($"{url}/{id}"))
            {
                var headers = Utils.GetAuthHeader();
                foreach (var k in headers)
                    webrx.SetRequestHeader(k.Key, k.Value);

                webrx.timeout = Config.timeoutSecs;
                webrx.SendWebRequest();
                while (!webrx.isDone)
                    ;

                if (webrx.isNetworkError || webrx.isHttpError)
                    throw new Exception("Delete failed " + id);
            }
        }
    }
}
