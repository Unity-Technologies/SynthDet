using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using File = System.IO.File;
using Hash128 = Unity.Entities.Hash128;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
#endif
#pragma warning disable 649


namespace Unity.Scenes
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SubScene : MonoBehaviour
    {
#if UNITY_EDITOR
        [FormerlySerializedAs("sceneAsset")]
        [SerializeField] SceneAsset _SceneAsset;
        [SerializeField] Color _HierarchyColor = Color.gray;
    
        static List<SubScene> m_AllSubScenes = new List<SubScene>();
        public static IReadOnlyCollection<SubScene> AllSubScenes { get { return m_AllSubScenes; } }
#endif

        public bool AutoLoadScene = true;

        [SerializeField]
        [HideInInspector]
        Hash128 _SceneGUID;

        [NonSerialized]
        Hash128 _AddedSceneGUID;

#if UNITY_EDITOR

        [NonSerialized]
        bool _IsAddedToListOfAllSubScenes;
        
        public SceneAsset SceneAsset
        {
            get { return _SceneAsset; }
            set
            {
                if (_SceneAsset == value)
                    return;

                // If the SceneAsset has been loaded we need to close it before changing to a new SceneAsset reference so we 
                // don't end up with loaded scenes which are not visible in the Hierarchy.
                if (_SceneAsset != null)
                {
                    Scene scene = EditingScene;
                    if (scene.isLoaded && scene.isSubScene)
                        EditorSceneManager.CloseScene(scene, true);
                }

                _SceneAsset = value;
                OnValidate();
            }
        }

        public string SceneName
        {
            get { return SceneAsset.name; }
        }

        public Color HierarchyColor
        {
            get { return _HierarchyColor; }
            set { _HierarchyColor = value; }
        }
   
        public string EditableScenePath
        {
            get 
            { 
                return _SceneAsset != null ? AssetDatabase.GetAssetPath(_SceneAsset) : "";    
            }
        }
        
        public Scene EditingScene
        {
            get
            {
                if (_SceneAsset == null)
                    return default(Scene);
                
                return EditorSceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(_SceneAsset));
            }
        }
        
        public bool IsLoaded
        {
            get { return EditingScene.isLoaded; }
        }

        void WarnIfNeeded()
        {
            if (!IsInMainStage())
                return;

            if (SceneAsset != null)
            {
                foreach (var subscene in m_AllSubScenes)
                {
                    if (!subscene.IsInMainStage())
                        continue;

                    if (subscene.SceneAsset == SceneAsset)
                    {
                        UnityEngine.Debug.LogWarning($"Sub Scenes can not reference the same scene ('{EditableScenePath}') multiple times.", this);
                        return;
                    }
                }
            }
        }
        
        void OnValidate()
        {
            _SceneGUID = new GUID(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_SceneAsset)));
            
            if (_IsAddedToListOfAllSubScenes && IsInMainStage())
            {
                if (_SceneGUID != _AddedSceneGUID)
                {
                    RemoveSceneEntities();
                    if (_SceneGUID != default)
                        AddSceneEntities();
                }
            }
        }

        internal bool CanBeLoaded()
        {
            if (SceneAsset == null)
                return false;

            if (!_IsAddedToListOfAllSubScenes)
                return false;

            if (!IsInMainStage())
                return false;

            if (!isActiveAndEnabled)
                return false;

            return true;
        }

        internal bool IsInMainStage()
        {
            return !EditorUtility.IsPersistent(gameObject) && StageUtility.GetStageHandle(gameObject) == StageUtility.GetMainStageHandle();
        }
#endif

        public Hash128 SceneGUID => _SceneGUID;

        void OnEnable()
        {
#if UNITY_EDITOR
            WarnIfNeeded();
            
            _IsAddedToListOfAllSubScenes = true;
            m_AllSubScenes.Add(this);

            if (_SceneGUID == default(Hash128))
                return;

            if (!IsInMainStage())
                return;
#endif

            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            AddSceneEntities();
        }
        
        void OnDisable()
        {
#if UNITY_EDITOR
            _IsAddedToListOfAllSubScenes = false;
            m_AllSubScenes.Remove(this);
#endif
            
            RemoveSceneEntities();
        }

        void AddSceneEntities()
        {
            Assert.IsTrue(_AddedSceneGUID == default);
            Assert.IsFalse(_SceneGUID == default);

            var flags = AutoLoadScene ? 0 : SceneLoadFlags.DisableAutoLoad;
#if UNITY_EDITOR
            flags |= EditorApplication.isPlaying ? SceneLoadFlags.BlockOnImport : 0;
#else
            flags |= SceneLoadFlags.BlockOnImport;
#endif
            foreach (var world in World.All)
            {
                var sceneSystem = world.GetExistingSystem<SceneSystem>();
                if (sceneSystem != null)
                {
                    
                    var loadParams = new SceneSystem.LoadParameters
                    {
                        Flags = flags
                    };
                    
                    var sceneEntity = sceneSystem.LoadSceneAsync(_SceneGUID, loadParams);
                    sceneSystem.EntityManager.AddComponentObject(sceneEntity, this);
                    _AddedSceneGUID = _SceneGUID;
                }
            }
        }
        void RemoveSceneEntities()
        {
            if (_AddedSceneGUID != default)
            {
                foreach (var world in World.All)
                {
                    var sceneSystem = world.GetExistingSystem<SceneSystem>();
                    if (sceneSystem != null)
                        sceneSystem.UnloadScene(_AddedSceneGUID, SceneSystem.UnloadParameters.DestroySceneProxyEntity | SceneSystem.UnloadParameters.DestroySectionProxyEntities);
                }
                
                _AddedSceneGUID = default;
            }
        }

        //@TODO: Move this into SceneManager
        void UnloadScene()
        {
        //@TODO: ask to save scene first???
#if UNITY_EDITOR
            var scene = EditingScene;
            if (scene.IsValid())
            {
                // If there is only one scene left in the editor, we create a new empty scene
                // before unloading this sub scene
                if (EditorSceneManager.loadedSceneCount == 1)
                {
                    Debug.Log("Creating new scene");
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                }
    
                EditorSceneManager.UnloadSceneAsync(scene);    
            }
#endif
        }
      
        private void OnDestroy()
        {
            UnloadScene();
        }

        /* @TODO: Add conversion. How do we prevent duplicate from OnEnable / OnDisable
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new SceneReference { SceneGUID = SceneGUID});
            if (AutoLoadScene)
                dstManager.AddComponent<RequestSceneLoaded>(entity);
        }
        */
    }
}
