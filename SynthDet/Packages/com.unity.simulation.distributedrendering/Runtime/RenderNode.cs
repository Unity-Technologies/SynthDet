using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unclassified.Net;
using Unity.Runtime.PlayerLoop;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Object = UnityEngine.Object;

namespace Unity.Simulation.DistributedRendering.Render
{
    public class RenderNode : MonoBehaviour
    {
        private AsyncTcpClient _client;
        private bool _isConnected;
        private ConcurrentQueue<byte[]> _frameData = new ConcurrentQueue<byte[]>();
        protected Dictionary<string, GameObjectId> _objectsToSync;

        public Text statusText;
        public Vector2 screenResolution = new Vector2(300, 300);
        
        public NodeOptions Options;

        /// <summary>
        /// most recent frame number received from the server
        /// </summary>
        private int _lastFrameNum;
        public int NumFramesSaved { get; set; }

        public PlayerLoopAsset spl;

        private Dictionary<string, GameObjectId> _currentFrameObjects = new Dictionary<string, GameObjectId>();

        private static class DistributedRendering
        {
            public struct RenderNodePreLateUpdate { };
        }
        private PlayerLoopSystem customPlayerLoopSystem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void LoadPlayerLoop()
        {
            if (DistributedRenderingOptions.mode == Mode.Render)
            {
                var renderNode = FindObjectOfType<RenderNode>();
                Physics.autoSimulation = false;
                FrameManager.Instance.DataConsumer.Initialize(renderNode.Options);
                renderNode.spl.Write();
                var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
                renderNode.customPlayerLoopSystem = Util.InjectSubsystemToThePlayerLoop(ref playerLoop, 
                    typeof(PreLateUpdate), 
                    typeof(DistributedRendering.RenderNodePreLateUpdate),
                    () =>
                {
                    var framedata = FrameManager.Instance.DataConsumer.RequestFrame();
                    if (framedata != null)
                    {
                        renderNode._frameCount++;
                        renderNode.ParseAndApplyFrame(framedata);
                    }
                });

                Manager.Instance.ShutdownNotification += () =>
                {
                    var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
                    Util.RemoveSubsystem(ref currentPlayerLoop, typeof(PreLateUpdate),
                        renderNode.customPlayerLoopSystem);
                };
            }
        }

        void OnApplicationQuit()
        {
            FrameManager.Instance.DataConsumer?.OnShutdown();
        }

        void Awake()
        {
            _objectsToSync = FindSyncableObjects();
        }

        private int _frameCount = 0;

        protected static Dictionary<string, GameObjectId> FindSyncableObjects()
        {
            var objectsToSync = new Dictionary<string, GameObjectId>();
            var ids = FindObjectsOfType<GameObjectId>();

            for (int i = 0; i < ids.Length; ++i)
            {
                var id = ids[i];
                objectsToSync[id.uniqueId] = id;
            }

            return objectsToSync;
        }

        // TODO: Investigate why this mutex is needed.
        private Mutex _mutex = new Mutex();
        private void ParseAndApplyFrame(byte[] bytes)
        {
            _mutex.WaitOne();
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    using (var serializer = new StreamSerializer(ms))
                    {
                        var messageLen = serializer.ReadLong();
                        var msg = serializer.ReadMessageType();

                        if (MessageType.EndSimulation == msg)
                        {
                            Log.I($"CLIENT: saved {NumFramesSaved} frames.");
                            Util.QuitApplication();
                            return;
                        }

                        Debug.Assert(MessageType.StartFrame == msg,
                            $"Expected message StartFrame, received {msg} ({messageLen} byte(s))");

                        int frameNum = serializer.ReadInt();

                        if (statusText != null)
                        {
                            var clusterFPS = (frameNum - _lastFrameNum) / Time.deltaTime;
                            statusText.text = $"Frame {frameNum}";
                            _lastFrameNum = frameNum;
                        }

                        while (ms.Position < ms.Length)
                        {
                            msg = serializer.ReadMessageType();
                            string prefabPath;
                            switch (msg)
                            {
                                case MessageType.PrefabPath:
                                {
                                    prefabPath = serializer.ReadString();
                                    var id = serializer.ReadString();
                                    if (_objectsToSync.ContainsKey(id))
                                        break;

                                    var goId = (InstantiateGameObject(prefabPath, id) as GameObject)
                                        .GetComponent<GameObjectId>();
                                    Debug.Assert(goId != null, "GameObject instantiation failed!");
                                    TrackGameobjectChildren(goId.GetComponentsInChildren<Transform>(), goId);
                                    break;
                                }
                                case MessageType.SetTransform:
                                {
                                    var id = serializer.ReadString();
                                    GameObjectId idObj;
                                    if (_objectsToSync.TryGetValue(id, out idObj))
                                    {
                                        try
                                        {
                                            var isActive = serializer.ReadBool();
                                            idObj.gameObject.SetActive(isActive);
                                            var t = serializer.ReadTransformProxy();
                                            t.Apply(idObj.transform);
                                            _currentFrameObjects[id] = idObj;
                                        }
                                        catch (Exception e)
                                        {
                                            Log.E("Could not apply transform for gameobject: " +
                                                           idObj.gameObject.name + "Exception : " + e.StackTrace);
                                        }
                                    }
                                    else
                                    {
                                        Log.E($"ParseAndApply No GameObjectID for ID {id}");
                                    }

                                    var proxies = gameObject.GetComponents<MonoBehaviour>().OfType<IComponentSerializerProxy>();
                                    foreach (var go in proxies)
                                    {
                                        go.ReadAndApply(idObj.gameObject, serializer);
                                    }

                                    break;
                                }

                                case MessageType.EndFrame:
                                {
                                    //Log.I("Received End Frame! Saving : " + $"~/clusterOutput/frame_{frameNum}.png");
                                    // technically captures the previous frame.
                                    // TODO: fix this with WaitForEndOfFrame coroutine or camera grab SDK
                                    // ScreenCapture.CaptureScreenshot($"~/clusterOutput/frame_{frameNum}.png");
                                    ++NumFramesSaved;
                                    return;
                                }

                                default:
                                    Log.E($"ParseAndApply Unhandled message {msg} in LoadFrame");
                                    return;

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.E("Failed to ParseAnyApply: " + e.StackTrace);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        
        private void TrackGameobjectChildren(Transform[] components, GameObjectId root, int count = 0)
        {
            if (components.Length == 0)
                return;
            
            foreach (var comp in components)
            {
                if (comp.gameObject.GetComponent<GameObjectId>() == null)
                {
                    var go = comp.gameObject.AddComponent<GameObjectId>();
                    go.uniqueId = root.uniqueId + "_" + (count++);
                    _objectsToSync.Add(go.uniqueId, go);
                }
            }
        }

        private Object InstantiateGameObject(string prefabPath, string id)
        {
            Log.I("Instantiating Prefab: " + prefabPath);
            var prefab = Resources.Load(prefabPath);
            Debug.Assert(prefab!= null, "Prefab not found at: "+ prefabPath);

            var go = Instantiate(prefab);
            var goId = (go as GameObject).GetComponent<GameObjectId>();
            goId.uniqueId = id;

            _currentFrameObjects[id] = goId;
            _objectsToSync.Add(id, goId);

            return go;
        }
    }
}
