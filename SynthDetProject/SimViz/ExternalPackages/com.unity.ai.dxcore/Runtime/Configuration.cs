using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Unity.AI.Simulation
{
    public class Configuration
    {
        private static Configuration _instance = new Configuration();

        private static readonly Dictionary<string, string> _argv = new Dictionary<string, string>()
        {
            {"--simulation-config-file", null},
        };

        [Serializable]
        public struct SimulationConfiguration
        {
            public string storage_uri_prefix;
            public string app_param_uri;
            public int    current_attempt;
            public int    chunk_size_bytes;
            public int    chunk_timeout_ms;
            public string instance_id;
            public float  time_logging_timeout_sec;
            public float  heartbeat_timeout_sec;
            public string app_param_id;
            public string signlynx_host;
            public ushort signlynx_port;
            public string bearer_token;
            public string execution_id;
            public string definition_id;

            public static SimulationConfiguration GetConfig(string config)
            {
                return JsonUtility.FromJson<SimulationConfiguration>(config);
            }

            private string _bucketName;
            private string _storagePath;

            private static readonly string kProtocolPrefix = "gs://";

            public string bucketName
            {
                get
                {
                    if (string.IsNullOrEmpty(storage_uri_prefix))
                        return null;

                    if (string.IsNullOrEmpty(_bucketName))
                    {
                        var temp = storage_uri_prefix;
                        if (temp.StartsWith(kProtocolPrefix))
                            temp = temp.Remove(0, kProtocolPrefix.Length);
                        _bucketName = temp.Split('/')[0];
                    }
                    return _bucketName;
                }
            }

            public string storagePath
            {
                get
                {
                    if (string.IsNullOrEmpty(_storagePath))
                    {
                        var temp = storage_uri_prefix;
                        if (temp.StartsWith(kProtocolPrefix))
                            temp = temp.Remove(0, kProtocolPrefix.Length);
                        var bname = bucketName + "/";
                        if (temp.StartsWith(bname))
                            temp = temp.Remove(0, bname.Length);
                        _storagePath = temp;
                    }
                    return _storagePath;
                }
            }
        }

        public SimulationConfiguration SimulationConfig;

        public static Configuration Instance => _instance;

        private string _currentSession = Guid.NewGuid().ToString();
        private readonly static string _persistentDataPath = Application.persistentDataPath;

        private bool _runningInCloud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadCommandlineArgs()
        {
            var args = Environment.GetCommandLineArgs();

            for (int i = 1; i < args.Length; i++)
            {
                string[] arg = args[i].Split('=');
                if (arg.Length == 2)
                {
                    if (_argv.ContainsKey(arg[0]))
                    {
                        _argv[arg[0]] = args[1];
                        Instance.LoadSimulationConfiguration(arg[1]);
                    }
                }
            }
        }

        private void LoadSimulationConfiguration(string path)
        {
            Debug.Assert(File.Exists(path), "Config file not found at path " + path);

            string simulationConfig = File.ReadAllText(path);
            try
            {
                SimulationConfig = SimulationConfiguration.GetConfig(simulationConfig);
                _runningInCloud = true;
            }
            catch (Exception e)
            {
                Log.E("Failed to parse the configuration: " + simulationConfig + " Exception: " + e.Message);
            }
        }

        public T GetAppParams<T>()
        {
            T config = default(T);

            if (SimulationConfig.app_param_uri == String.Empty)
            {
                Log.E("App config is not provided. Falling back to the default config");
            }

            try
            {
                string filePath = new Uri(SimulationConfig.app_param_uri).AbsolutePath;
                if (File.Exists(filePath))
                {
                    string appConfig = File.ReadAllText(filePath);
                    config = JsonUtility.FromJson<T>(appConfig);
                }
                else
                {
                    Log.E("Path to the app params is not valid: " + SimulationConfig.app_param_uri);
                }
            }
            catch (Exception e)
            {
                Log.E("Failed to parse the user config. Exception : " + e.Message);
            }

            return config;
        }

        public string GetAttemptId()
        {
            return  IsSimulationRunningInCloud() ? ("attempt:" + SimulationConfig.current_attempt.ToString()) : _currentSession;
        }

        public string GetInstanceId()
        {
            return IsSimulationRunningInCloud() ? SimulationConfig.instance_id : "";
        }

        public string GetStorageBasePath()
        {
            return IsSimulationRunningInCloud() ? SimulationConfig.storagePath : _persistentDataPath;
        }

        public string GetStoragePath()
        {
            return Path.Combine(GetStorageBasePath(), GetAttemptId());
        }

        public bool IsSimulationRunningInCloud()
        {
            return _runningInCloud;
        }
    }
}
