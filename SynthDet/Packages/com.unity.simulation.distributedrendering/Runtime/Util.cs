using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Unity.Simulation.DistributedRendering
{
    public static class Util
    {
        public static string EncodeData(byte[] rawData)
        {
            // NOTE: Compression could be used on the input data, but it will only provide a benefit if the
            //       data is larger than 1KB.  Anything smaller will result in output that is either the same
            //       size as the input data or larger, keeping in mind that the output of this function is a
            //       base64 representation of the data, which by itself will end up being roughly 25% larger
            //       than whatever it is encoding.  We can evaluate the performance vs memory metrics once we
            //       have a larger dataset to work with.
            return Convert.ToBase64String(rawData);
        }
        
        
        /// <summary>
        /// Decode the base64 encoded string.
        /// </summary>
        /// <param name="encodedData">base64 encoded string.</param>
        /// <returns>byte array of decoded data.</returns>
        public static byte[] DecodeData(string encodedData)
        {
            return Convert.FromBase64String(encodedData);
        }
        

        /// <summary>
        /// Get base-encoded payload to be transferred over the network. The structure is: Length of the msg
        /// followed by base64 encoded message.
        /// </summary>
        /// <param name="data">Raw bytes that needs to be base64 encoded.</param>
        /// <returns>Base64 encoded payload to be transferred over the network.</returns>

        public static byte[] GetBase64EncodedPayload(byte[] data)
        {
            var encodedData = Encoding.UTF8.GetBytes(EncodeData(data));
            var encodedDataLengthBytes = BitConverter.GetBytes(encodedData.Length);
            var payload = new byte[encodedData.Length + encodedDataLengthBytes.Length];
            int index = 0;
            for (int i = 0; i < encodedDataLengthBytes.Length; i++)
                payload[index++] = encodedDataLengthBytes[i];
            for (int i = 0; i < encodedData.Length; i++)
                payload[index++] = encodedData[i];

            return payload;
        }

        public static void QuitApplication()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static class DistributedRendering
        {
            public struct RenderNodePreLateUpdate { };
        }
        
        /// <summary>
        /// Inject a custom playerloopsystem in the provided playerloop. 
        /// </summary>
        /// <param name="playerLoopSystem">PlayerLoop in which the system is to be injected.</param>
        /// <param name="injectToPlayerloopType">PlayerLoop type where the system is to be injected.</param>
        /// <param name="playerloopType">Custom Player Loop type.</param>
        /// <param name="updateDeletage">Update delegate to be called in the custom playerloop system.</param>
        /// <returns></returns>
        public static PlayerLoopSystem InjectSubsystemToThePlayerLoop(ref PlayerLoopSystem playerLoopSystem,
            Type injectToPlayerloopType,
            Type playerloopType,
            PlayerLoopSystem.UpdateFunction updateDeletage)
        {
            var subsystem = new PlayerLoopSystem();
            Insert(ref playerLoopSystem, injectToPlayerloopType, list =>
            {
                subsystem.type = playerloopType;
                subsystem.updateDelegate += updateDeletage;
                list.Insert(0, subsystem);
                return true;
            });
            return subsystem;
        }
        
        static void Insert(ref PlayerLoopSystem playerLoopSystem, Type playerLoopType,
            Func<List<PlayerLoopSystem>, bool> function)
        {
            for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
            {
                var mainSystem = playerLoopSystem.subSystemList[i];
                if (mainSystem.type == playerLoopType)
                {
                    var subSystemList = new List<PlayerLoopSystem>(mainSystem.subSystemList);
                    if (function(subSystemList))
                    {
                        mainSystem.subSystemList = subSystemList.ToArray();
                        playerLoopSystem.subSystemList[i] = mainSystem;
                        PlayerLoop.SetPlayerLoop(playerLoopSystem);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Returns an empty default player loop's susbsytemslist.
        /// </summary>
        /// <returns></returns>
        public static  PlayerLoopSystem GetEmptyPlayerLoopSystem()
        {
            PlayerLoopSystem cpl = PlayerLoop.GetDefaultPlayerLoop();
            for (int i = 0; i < cpl.subSystemList.Length; i++)
            {
                var mainSystem = cpl.subSystemList[i];
                mainSystem.subSystemList = new List<PlayerLoopSystem>().ToArray();
            }

            return cpl;
        }

        /// <summary>
        /// Remove player loop system from the provided playerloop
        /// </summary>
        /// <param name="playerLoopSystem">Playerloop from which the system is to be removed.</param>
        /// <param name="playerloopType">Player loop type after which the system can be found.</param>
        /// <param name="subSystem">Refernece to subsystem to be removed.</param>
        /// <returns>Returns a bool indicating if the subsystem was removed successfully.</returns>
        public static bool RemoveSubsystem(ref PlayerLoopSystem playerLoopSystem, Type playerloopType, PlayerLoopSystem subSystem)
        {
            for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
            {
                var mainSystem = playerLoopSystem.subSystemList[i];
                for (int j = 0; j < mainSystem.subSystemList.Length; j++)
                {
                    if (mainSystem.subSystemList[j].type == playerloopType)
                    {
                        var subSystemList = new List<PlayerLoopSystem>(mainSystem.subSystemList);
                        return subSystemList.Remove(subSystem);    
                    }
                }
            }

            return false;
        }
    }
}
