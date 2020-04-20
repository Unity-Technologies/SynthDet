using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Unity.Simulation
{
    /// <summary>
    /// Configuration class for application.
    /// Access to app params and other simulation configuration data.
    /// </summary>
    public class Configuration
    {
        private static Configuration _instance = new Configuration();

        private static readonly Dictionary<string, string> _argv = new Dictionary<string, string>()
        {
            {"--simulation-config-file", null},
        };

        [Serializable]
        /// <summary>
        /// The configuration passed to the app from the agent.
        /// </summary>
        public struct SimulationConfiguration
        {
            /// <summary>
            /// Deprecated. The storage URI when using GCS uploading.
            /// </summary>
            public string storage_uri_prefix;

            /// <summary>
            /// Local path of the app param json to read.
            /// </summary>
            public string app_param_uri;

            /// <summary>
            /// The attempt number for this simulation. 0 - 2
            /// </summary>
            public int    current_attempt;

            /// <summary>
            /// The size in bytes for the buffer used to write out logs in chunks.
            /// </summary>
            public int    chunk_size_bytes;

            /// <summary>
            /// The timeout in seconds for flushing the log to disk in chunks.
            /// </summary>
            public int    chunk_timeout_ms;

            /// <summary>
            /// The instance number for this simulation. If you scheduled 10 instances, then 1 - 10
            /// </summary>
            public string instance_id;

            /// <summary>
            /// The logging interval in seconds for the time logger.
            /// Time logger will log simulation time, wall time and FPS at this interval.
            /// </summary>
            public float  time_logging_timeout_sec;

            /// <summary>
            /// The interval at which the heartbeat log is emitted.
            /// </summary>
            public float  heartbeat_timeout_sec;

            /// <summary>
            /// The app param id for this instance.
            /// </summary>
            public string app_param_id;

            /// <summary>
            /// The signed url endpoint to get signed urls for cloud agnostic uploads.
            /// </summary>
            public string signlynx_host;

            /// <summary>
            /// The signed url port to use when getting signed urls for cloud agnostic uploads.
            /// </summary>
            public ushort signlynx_port;

            /// <summary>
            /// Bearer token to use for getting signed url uploads.
            /// </summary>
            public string bearer_token;

            /// <summary>
            /// The execution id for this run.
            /// </summary>
            public string execution_id;

            /// <summary>
            /// The run definitio id for this run.
            /// </summary>
            public string definition_id;

            /// <summary>
            /// Returns the configuration for the current simulation in progress.
            /// </summary>
            /// <param name="config">Simulation config file. (Provided by the USim agent).</param>
            /// <returns>Simulation Configuration for the current run.</returns>
            public static SimulationConfiguration GetConfig(string config)
            {
                return JsonUtility.FromJson<SimulationConfiguration>(config);
            }

            private string _bucketName;
            private string _storagePath;

            private static readonly string kProtocolPrefix = "gs://";

            /// <summary>
            /// Returns the cloud storage bucket name.
            /// </summary>
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
            
            /// <summary>
            /// Returns the full cloud storage path.
            /// </summary>
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

        /// <summary>
        /// Accessor for the simulation configuration.
        /// </summary>
        public SimulationConfiguration SimulationConfig;

        /// <summary>
        /// Singleton accessor.
        /// </summary>
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

        /// <summary>
        /// Returns the AppParams of custom type T
        /// </summary>
        /// <typeparam name="T">The type of AppParam</typeparam>
        /// <returns>AppParam of type T</returns>
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

        /// <summary>
        /// Returns the attemptId of the simulation run.
        /// </summary>
        /// <returns>string of attemptId</returns>
        public string GetAttemptId()
        {
            return  IsSimulationRunningInCloud() ? ("attempt:" + SimulationConfig.current_attempt.ToString()) : _currentSession;
        }

        /// <summary>
        /// Returns the id of the instance on which the simulation is currently running.
        /// </summary>
        /// <returns>string of instanceId</returns>
        public string GetInstanceId()
        {
            return IsSimulationRunningInCloud() ? SimulationConfig.instance_id : "";
        }
        
        /// <summary>
        /// Returns the base storage path for the data storage in the cloud. It defaults to the Application persistent data path.
        /// </summary>
        /// <returns>string of the storagePath</returns>
        public string GetStorageBasePath()
        {
            return IsSimulationRunningInCloud() ? SimulationConfig.storagePath : _persistentDataPath;
        }

        /// <summary>
        /// Returns the full path for the storage which contains base path and the attemptId
        /// </summary>
        /// <returns>string of full storage path</returns>
        public string GetStoragePath()
        {
            return Path.Combine(GetStorageBasePath(), GetAttemptId());
        }

        /// <summary>
        /// Returns if the simulation is running in cloud or not.
        /// </summary>
        /// <returns>boolean indicating if simulation is running in cloud</returns>
        public bool IsSimulationRunningInCloud()
        {
            return _runningInCloud;
        }
    }
}
