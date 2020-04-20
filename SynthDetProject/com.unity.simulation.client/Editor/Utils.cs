using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    internal static class Utils
    {
        internal static Dictionary<string, string> GetAuthHeader()
        {
            var dict = new Dictionary<string, string>();
            AddUserAgent(dict);
            AddContentTypeApplication(dict);
            AddAuth(dict);
            return dict;
        }

        internal static void AddContentTypeApplication(Dictionary<string, string> dict)
        {
            dict["Content-Type"] = "application/json";
        }

        internal static void AddAuth(Dictionary<string, string> dict)
        {
#if UNITY_EDITOR
            var tokenString = CloudProjectSettings.accessToken;
#else
            var tokenString = Config.token.accessToken;
#endif
            dict["Authorization"] = "Bearer " + tokenString;
        }

        internal static void AddUserAgent(Dictionary<string, string> dict)
        {
            dict["User-Agent"] = "usim-cli/0.0.0";
        }
    }
}