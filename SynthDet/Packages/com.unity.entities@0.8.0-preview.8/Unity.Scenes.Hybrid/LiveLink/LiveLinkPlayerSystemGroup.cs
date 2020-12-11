using System;
using System.IO;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Networking;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    
    //@TODO: #ifdefs massively increase iteration time right now when building players (Should be fixed in 20.1)
    //       Until then always have the live link code present.
#if UNITY_EDITOR
    [DisableAutoCreation]
#endif
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SceneSystemGroup))]
    class LiveLinkRuntimeSystemGroup : ComponentSystemGroup
    {
        public const string k_BootstrapFileName = "livelink-bootstrap.txt";
        public static long LiveLinkSessionId { get; private set; }

        internal static string GetBootStrapPath()
        {
            return Path.Combine(Application.streamingAssetsPath, k_BootstrapFileName);
        }
        
        protected override void OnCreate()
        {
#if UNITY_ANDROID
            var uwrFile = new UnityWebRequest(SceneSystem.GetBootStrapPath());
            uwrFile.SendWebRequest();
            while(!uwrFile.isDone) {}

            if (uwrFile.isNetworkError || uwrFile.isHttpError)
            {
                Enabled = false;
            }
            else
            {
                Enabled = true;
            }
#else
            var bootstrapFilePath = GetBootStrapPath();
            Enabled = File.Exists(bootstrapFilePath);
#endif
            if (Enabled)
            {
                if (!UnityEngine.Networking.PlayerConnection.PlayerConnection.instance.isConnected)
                    Debug.LogError("Failed to connect to the Editor.\nAn Editor connection is required for LiveLink to work.");
                
                using (var rdr = File.OpenText(bootstrapFilePath))
                {
                    var buildConfigurationGUID = new Hash128(rdr.ReadLine());
                    LiveLinkSessionId = long.Parse(rdr.ReadLine() ?? throw new Exception("Expected line in bootstrap containing session id!"));
                    World.GetOrCreateSystem<SceneSystem>().BuildConfigurationGUID = buildConfigurationGUID;
                }
            }
        }
    }
}