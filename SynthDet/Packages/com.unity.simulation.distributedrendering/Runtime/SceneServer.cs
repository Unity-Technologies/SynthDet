using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Unclassified.Net;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using Unity.Runtime.PlayerLoop;
using Unity.Simulation.DistributedRendering.Render;
using UnityEditor;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Unity.Simulation.DistributedRendering.Render
{
    public struct SimulationStats
    {
        public float clusterFPS;
        public float timeElapsed;
        public int framesProcessed;
        public float simulationTime;
    }
    
    public static class DistributedRendering
    {
        public struct PhysicsNodePreLateUpdate { };
        public struct ApplicationQuitConditionPreLateUpdate { };
    }

    public class SceneServer: MonoBehaviour
    {
        [HideInInspector] public bool _runInServerMode;
        [HideInInspector] public bool _runInRender;

        public int             maxFramesToRender = 0;
        public bool            disableCameras = true;
        public PlayerLoopAsset scriptablePlayerLoop;
        public NodeOptions     options;

        private ClusterTimer Timer { get; set; }
        private float ClusterFps { get; set; }
        private int                                _printStatsState;
        private MemoryStream                       _stream = new MemoryStream();
        private List<AsyncTcpListener>             _listener = new List<AsyncTcpListener>();
        private IMessageSerializer                 _serializer;
        private static float                       _accumulatedTime = 0.0f;
        private SimulationStats                    _stats;
        private int                                _enqueuedFrameCount;
        private float                              _elapsedTime;
        private bool                               _doneProducingFrames;
        private PlayerLoopSystem                   _customPhysicsPlayerLoopSystem;

        /// <summary>
        /// Initializes the Server with a custom player loop with all subsystems except physics turned off.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void StartTightPhysicsLoop()
        {
            if (DistributedRenderingOptions.mode != Mode.Physics)
                return;

            var server = FindObjectOfType<SceneServer>();
            Debug.Log("SceneServer found: " + (server != null));
            DiffAndUpdatePlayerLoopAsset(ref server.scriptablePlayerLoop);
            server.scriptablePlayerLoop.Write();
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            
            // Initialize the data producer.
            FrameManager.Instance.DataProducer?.Initialize(server.options);
            
            Physics.autoSimulation = false;
            server.SetupServer();

            server._customPhysicsPlayerLoopSystem = Util.InjectSubsystemToThePlayerLoop(ref playerLoop,
            typeof(PreLateUpdate),
            typeof(DistributedRendering.PhysicsNodePreLateUpdate),
            () =>
            {
                Physics.Simulate(1.0f / 60.0f);
                server.EnqueueFrame(FrameManager.Instance.ObjectsToSync);
            });
            
            Manager.Instance.ShutdownNotification += () =>
            {
                var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
                Util.RemoveSubsystem(ref currentPlayerLoop, typeof(DistributedRendering.PhysicsNodePreLateUpdate),
                    server._customPhysicsPlayerLoopSystem);
            };
        }

        public class PlayerLoopComparator : IEqualityComparer<PlayerLoopSystem>
        {
            public bool Equals(PlayerLoopSystem x, PlayerLoopSystem y)
            {
                return x.type == y.type;
            }

            public int GetHashCode(PlayerLoopSystem obj)
            {
                return obj.GetHashCode();
            }
        }

        private static void DiffAndUpdatePlayerLoopAsset(ref PlayerLoopAsset splAsset)
        {
            var phases = splAsset.phases;
            var defaultPlayerLoop = PlayerLoop.GetDefaultPlayerLoop();
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var map = new Dictionary<int, PlayerLoopSystem>();
            //Find the added subsystem by doing a diff between default playerloop and currentplayer loop
            for (int i = 0; i < currentPlayerLoop.subSystemList.Length; i++)
            {
                var defaultSubsystemsList = defaultPlayerLoop.subSystemList[i].subSystemList.ToArray();
                var currentPLSubsystemsList = currentPlayerLoop.subSystemList[i].subSystemList.ToArray();

                var diff = currentPLSubsystemsList.Except(defaultSubsystemsList, new PlayerLoopComparator());
                var playerLoopSystems = diff.ToList();
                if (playerLoopSystems.Any())
                {
                    foreach (var entry in playerLoopSystems)
                    {
                        map.Add(i, entry);
                    }
                }
            }

            foreach (var entry in map)
            {
                splAsset.Insert(entry.Key, phases[entry.Key].calls.Count-1, new PlayerLoopAsset.Call()
                {
                    type = entry.Value.type.AssemblyQualifiedName,
                    i = phases[entry.Key].calls.Count-1,
                    j = 0
                });
            }
            //Save that in the dictionary.
            //Iterate over dictionary and add it to the phases
            
        }

        public void SetupServer()
        {
            Timer = FindObjectOfType<ClusterTimer>();
            _serializer = new StreamSerializer(_stream);

            if (disableCameras)
            {
                var cameras = FindObjectsOfType<Camera>();
                for (var i = 0; i < cameras.Length; ++i)
                {
                    cameras[i].enabled = false;
                }
            }
        }

        void OnApplicationQuit()
        {
            _stats.framesProcessed = _enqueuedFrameCount;
            _stats.timeElapsed = Time.realtimeSinceStartup;
            _stats.simulationTime = _elapsedTime;
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            if (!Util.RemoveSubsystem(ref currentPlayerLoop, typeof(DistributedRendering.PhysicsNodePreLateUpdate), _customPhysicsPlayerLoopSystem))
            {
                Log.E("Unable to remove the custom player loop.");
            }
                
            FrameManager.Instance.DataProducer?.OnShutdown(_stats);
        }
        
        void Update()
        {
            _elapsedTime += Time.deltaTime;

            if (maxFramesToRender > 0 && _enqueuedFrameCount >= maxFramesToRender && !_doneProducingFrames)
            {
                _doneProducingFrames = true;
                Log.I("Done Producing Frames!!");
                var currentPlayerLoop = Util.GetEmptyPlayerLoopSystem();
                _customPhysicsPlayerLoopSystem = Util.InjectSubsystemToThePlayerLoop(ref currentPlayerLoop, 
                    typeof(PreLateUpdate),
                    typeof(DistributedRendering.ApplicationQuitConditionPreLateUpdate), 
                    () =>
                    {
                        if (FrameManager.Instance.DataProducer.ReadyToQuitAfterFramesProduced())
                        {
#if UNITY_EDITOR
                            EditorApplication.isPlaying = false;
#endif
                            Application.Quit();
                        }
                    });
            }
        }

        /*
         * Frame format:
         * NumBytes
         * StartFrame
         * CurrentFrameNum
         * SetTransform
         * TransformProxy
         * SetTransform
         * TransformProxy
         * SetTransform
         * ...
         * EndFrame
         */

        private void EnqueueFrame(Dictionary<string,GameObjectId> objectsToSync)
        {
            _stream.SetLength(0);
            _serializer.Write(0L);
            _serializer.Write((UInt32) MessageType.StartFrame);
            _serializer.Write(++_enqueuedFrameCount);
            
            foreach (var kvp in objectsToSync)
            {
                var id = kvp.Key;
                var obj = kvp.Value;

                // TODO:
                // When adding different message types, it'd be better to
                // write a "MessageType.SelectObject" followed by ID
                // before any object-specific things like SetTransform.
                // That would allow more per-object commands to be streamed.

                if (!string.IsNullOrEmpty(obj.prefabPath))
                {
                    Log.I("Enqueuing Prefab!");
                    _serializer.Write(MessageType.PrefabPath);
                    _serializer.Write(obj.prefabPath);
                    _serializer.Write(id);
                }

                _serializer.Write(MessageType.SetTransform);
                _serializer.Write(id);
                _serializer.Write(obj.gameObject.activeInHierarchy);
                _serializer.Write(TransformProxy.FromTransform(obj.transform));

                var additionalFrameData = FindObjectsOfType<MonoBehaviour>().OfType<IComponentSerializerProxy>();
                foreach (var go in additionalFrameData)
                    go.WriteCustom(_serializer);

            }

            _serializer.Write(MessageType.EndFrame);

            // write the total length in the stream header
            _stream.Seek(0, SeekOrigin.Begin);
            _serializer.Write(_stream.Length);
            _stream.Seek(0, SeekOrigin.Begin);

            var l = BitConverter.GetBytes(_stream.Length);
            var bytes = new byte[_stream.Length];
            _stream.Read(bytes, 0, bytes.Length);

            FrameManager.Instance.DataProducer?.Consume(bytes);
        }
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(SceneServer))]
public class SceneServer_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SceneServer sceneServer = (SceneServer) target;
        sceneServer._runInServerMode = EditorGUILayout.Toggle("ServerMode", sceneServer._runInServerMode);
        sceneServer._runInRender = EditorGUILayout.Toggle("RenderMode", sceneServer._runInRender);
        
        if (sceneServer._runInServerMode)
        {
            sceneServer._runInRender = false;
            var cameras = FindObjectsOfType<Camera>();
            var camSet = new HashSet<string>();
            foreach (var camera in cameras)
            {
                if (camera.gameObject.activeSelf && camera.enabled)
                {
                    camSet.Add(camera.gameObject.name);
                }
                camera.gameObject.SetActive(false);
            }

            var camerasEnabled = String.Join(",", camSet);
            EditorPrefs.SetString("active_cameras", camerasEnabled);
        }
        else
        {
            var activeCameras = EditorPrefs.GetString("active_cameras");
            if (!String.IsNullOrEmpty(activeCameras))
            {
                var cams = activeCameras.Split(',');
                var camerasInScene = GameObject.FindObjectsOfType<Camera>();
                foreach (var cam in cams)
                {
                    var cameras = camerasInScene.Where((c) => c.gameObject.name.Equals(cam));
                    foreach (var camera in cameras)
                    {
                        camera.enabled = true;
                        camera.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
}
#endif
