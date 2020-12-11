using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEngine.GameObject;
using Object = UnityEngine.Object;


namespace Unity.Simulation.DistributedRendering
{
    public class FrameManager
    {
        private FrameManager() { }
        
        private static FrameManager               _instance;
        private SceneServer                       _sceneServer;
        private Dictionary<string, GameObjectId>  _objectsToSync;


        public IFrameDataProducer DataProducer { get; private set; }
        public IFrameDataConsumer DataConsumer { get; private set; }

        public Action<IMessageSerializer> CustomMessageFunctor { get; set; }
        
        /// <summary>
        /// Sets the node option at on initialize.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void SetNodeOption()
        {
            // For debugging purpose in the editor, set to force a DR operations mode.
#if UNITY_EDITOR && UNITY_SIMULATION_DR_SCENE_SERVER
            DistributedRenderingOptions.mode = Mode.Physics;
#elif UNITY_EDITOR && UNITY_SIMULATION_DR_RENDER
            DistributedRenderingOptions.mode = Mode.Render;
#elif !UNITY_SIMULATION_SPRAWL
            Debug.Log("DR Mode: " + DistributedRenderingOptions.mode);
            if (DistributedRenderingOptions.mode == Mode.None)
            {
                var cmdLineOption = Environment.GetCommandLineArgs();
                if (cmdLineOption.Contains("--render"))
                {
                    Log.I("Setting render mode");
                    DistributedRenderingOptions.mode = Mode.Render;
                }
                else
                {
                    Log.I("Setting Physics mode");
                    DistributedRenderingOptions.mode = Mode.Physics;
                }
            }
#endif
        }

        /// <summary>
        /// Returns an instance of the FrameManager.
        /// </summary>
        public static FrameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FrameManager();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Register the frame data producer.
        /// </summary>
        /// <param name="frameDataProducer">Instance of a producer implementing IFrameDataProducer interface.</param>
        public void RegisterSceneServerDataProducer(IFrameDataProducer frameDataProducer)
        {
            if (DataProducer != null)
            {
                Log.E($"Frame Data Provider is already registered: {DataProducer.GetType().Name}");
                return;
            }

            DataProducer = frameDataProducer;
        }

        /// <summary>
        /// Register the frame data consumer.
        /// </summary>
        /// <param name="frameDataConsumer">Instance of a consumer implementing IFrameDataConsumer interface.</param>
        public void RegisterRenderNodeDataConsumer(IFrameDataConsumer frameDataConsumer)
        {
            if (DataConsumer != null)
            {
                Log.E($"Frame Data Consumer is already registered: {DataConsumer.GetType().Name}");
                return;
            }
            
            DataConsumer = frameDataConsumer;
        }

        /// <summary>
        /// Returns the list of gameobjects that are currently being tracked for distributed rendering.
        /// </summary>
        public Dictionary<string, GameObjectId> ObjectsToSync
        {
            get
            {
                if (_objectsToSync == null)
                {
                    _objectsToSync = FindSyncableObjects();
                }

                return _objectsToSync;
            }
        }

        /// <summary>
        /// Returns a dictionary of uniqueId : GameObjectID. Populates the objects to sync if it is being
        /// called for the first time.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string,GameObjectId> FindSyncableObjects()
        {
            var objects = new Dictionary<string, GameObjectId>();
            var ids = Object.FindObjectsOfType<GameObjectId>();

            for (int i = 0; i < ids.Length; ++i)
            {
                var id = ids[i];
                objects[id.uniqueId] = id;
            }

            return objects;

        }

        /// <summary>
        /// Instantiates a gameobject at runtime and marks it for tracking for Distributed rendering.
        /// The prefab being instantiated is expected to be in the Resources directory.
        /// </summary>
        /// <param name="resourcePathToPrefab">Path to the prefab relative to the Resources directory.</param>
        /// <returns>Reference to the gameobject instantiated.</returns>
        public Object Instantiate(string resourcePathToPrefab, GameObject parent = null)
        {
            var prefab = Resources.Load(resourcePathToPrefab);
            Debug.Assert(prefab != null, $"Prefab does not exists in Resources: {resourcePathToPrefab}");

            if (_objectsToSync == null)
            {
                _objectsToSync = FindSyncableObjects();
            }

            var gameObject = Object.Instantiate(prefab) as GameObject;
            if (gameObject != null)
            {
                var gameObjectId = gameObject.GetComponent<GameObjectId>();
                
                //Debug.Assert(gameObjectId != null, "GameObjectID is not associated with this prefab!");

                if (gameObjectId == null)
                    gameObjectId = gameObject.AddComponent<GameObjectId>();

                gameObjectId.uniqueId = Guid.NewGuid().ToString();
                gameObjectId.prefabPath = resourcePathToPrefab;
                _objectsToSync[gameObjectId.uniqueId] = gameObjectId;

                TrackGameobjectChildren(gameObject.GetComponentsInChildren<Transform>(), gameObjectId);

                // if (parent != null)
                // {
                //     var parentGameObjectId = parent.GetComponent<GameObjectId>();
                //     //Debug.Assert(parentGameObjectId != null, "The Parent gameobeject is not tracked.");
                //
                //     gameObjectId.parentObjectUniqueId = parentGameObjectId.uniqueId;
                // }
            }

            return gameObject;
        }

        public void StartTrackingGameObject(GameObject go)
        {
            Debug.Assert(go!= null, "GameObject is null");
            var goID = go.AddComponent<GameObjectId>();
            goID.uniqueId = Guid.NewGuid().ToString();
            TrackGameobjectChildren(go.GetComponentsInChildren<Transform>(), goID);
        }
        
        private void TrackGameobjectChildren(Transform[] components, GameObjectId root, int count = 0)
        {
            if (components.Length == 0)
            {
                return;
            }

            foreach (var childTransform in components)
            {
                if (childTransform.gameObject.GetComponent<GameObjectId>() == null)
                {
                    var objId = childTransform.gameObject.AddComponent<GameObjectId>();
                    objId.uniqueId = root.uniqueId + "_" + (count++);
                    _objectsToSync[objId.uniqueId] = objId;
                }
            }
        }

        /// <summary>
        /// Destroys the object from the scene at runtime and also removes it from the list of objects being tracked for
        /// distributed rendering.
        /// </summary>
        /// <param name="gameObject"></param>
        public void Destroy(GameObject gameObject)
        {
            var gameObjectId = gameObject.GetComponent<GameObjectId>();
            Debug.Assert(gameObjectId != null, "The object you are trying to destroy is not being tracked for distributed rendering");
            _objectsToSync.Remove(gameObjectId.uniqueId);
            Destroy(gameObject);
        }
    }
}
