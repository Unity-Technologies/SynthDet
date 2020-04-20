using System;
using System.IO;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    /// <summary>
    /// Configuration class that provides for various helper properties.
    /// </summary>
    public static class Config
    {
        // Public Members

        /// <summary>
        /// The default API endpoint for production Unity Simulation service.
        /// </summary>
        public const string kDefaultAPIHost = "api.simulation.unity3d.com";

        /// <summary>
        /// The default API timeout in seconds.
        /// </summary>
        public const int kDefaultAPITimeout = 1000;

        /// <summary>
        /// The default API protocol to use.
        /// </summary>
        public const string kDefaultAPIProtocol = "https";

        /// <summary>
        /// The default port to listen to for the auth redirect response.
        /// </summary>
        public const ushort kDefaultRedirectUriPort = 57242;

        /// <summary>
        /// </summary>

        /// <summary>
        /// The API host to use communicating with Unity Simulation service.
        /// </summary>
        public static string apiHost
        {
            get
            {
#if UNITY_EDITOR
                return kDefaultAPIHost;
#else
                return _config["host"];
#endif
            }
        }

        /// <summary>
        /// The API protocol to use communication with Unity Simulation service.
        /// </summary>
        public static string apiProtocol
        {
            get
            {
#if UNITY_EDITOR
                return kDefaultAPIProtocol;
#else
                return _config["proto"];
#endif
            }
        }

        /// <summary>
        /// The endpoint to use communicating with the Unity Simulation service.
        /// </summary>
        public static string apiEndpoint
        {
            get { return $"{apiProtocol}://{apiHost}"; }
        }

        /// <summary>
        /// The timeout in seconds for Unity Simulation service requests.
        /// </summary>
        public static int timeoutSecs
        {
            get
            {
#if UNITY_EDITOR
                return kDefaultAPITimeout;
#else
                return int.Parse(_config["timeout_secs"]);
#endif
            }
        }

        /// <summary>
        /// The home directory for the current user.
        /// </summary>
        public static string homeDir
        {
            get
            {
#if UNITY_EDITOR_WIN
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
                return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
#endif
            }
        }

        /// <summary>
        /// The configuration directory for the current user.
        /// </summary>
        public static string confDir
        {
            get { return Path.Combine(homeDir, "." + kProductNameLowerCase); }
        }

        /// <summary>
        /// The configuration file for the Unity Simulation service.
        /// </summary>
        public static string configurationFile
        {
            get { return Path.Combine(confDir, kProductNameLowerCase + ".ini"); }
        }

#if !UNITY_EDITOR
        /// <summary>
        /// The token file used to persist the auth and refresh tokens.
        /// </summary>
        public static string tokenFile
        {
            get { return Path.Combine(confDir, "token.json"); }
        }

        /// <summary>
        /// Returns the instantiated token from the token file.
        /// </summary>
        public static Token token
        {
            get { return _token = Token.Load(tokenFile); }
        }

        /// <summary>
        /// The file used to persist the active project id.
        /// </summary>
        public static string projectFile
        {
            get { return Path.Combine(confDir, "project_id.txt"); }
        }

        /// <summary>
        /// If Unity Simulation has never run, you can use this to generate the first time data needed to run.
        /// </summary>
        public static void Bootstrap()
        {
            var dir = Config.confDir;
            if (Directory.Exists(dir))
                return;

            Directory.CreateDirectory(dir);

            var lines = new List<string>();
            lines.Add("[API]");
            lines.Add("host = "         + Config.kDefaultAPIHost);
            lines.Add("timeout_secs = " + Config.kDefaultAPITimeout);
            lines.Add("proto = "        + Config.kDefaultAPIProtocol);
            File.WriteAllLines(Config.configurationFile, lines);
        }

        /// <summary>
        /// Refreshes the auth token.
        /// </summary>
        public static void Refresh()
        {
            _config = _ParseConfigSection("API", File.ReadAllLines(configurationFile));
        }
#endif//!UNITY_EDITOR

        // Protected / Private Members

        const string kProductNameLowerCase = "usim";

#if !UNITY_EDITOR
        static Token _token;

        static Config()
        {
            Refresh();
        }

        static Dictionary<string, string> _config;

        static Dictionary<string, string> _ParseConfigSection(string sectionName, string[] config)
        {
            if (config == null || config.Length == 0)
            {
                throw new Exception("_ParseConfigSection config is null or empty");
            }

            var tag = $"[{sectionName}]";
            Dictionary<string, string> dict = new Dictionary<string, string>();

            for (var i = 0; i < config.Length; ++i)
            {
                if (config[i].StartsWith(tag))
                {
                    for (var j = i + 1; j < config.Length; ++j)
                    {
                        var kv = config[j];
                        if (string.IsNullOrEmpty(kv) || kv.StartsWith("["))
                            return dict;
                        var parts = kv.Split('=');
                        UnityEngine.Debug.Assert(parts.Length == 2);
                        dict[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            return dict;
        }
#endif//UNITY_EDITOR
    }
}
