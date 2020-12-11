using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [CustomEditor(typeof(SubScene))]
    [CanEditMultipleObjects]
    class SubSceneInspector : UnityEditor.Editor
    {
        Dictionary<Hash128, bool> m_ConversionLogLoaded = new Dictionary<Hash128, bool>();
        string m_ConversionLog = "";

        SceneAsset[] m_PreviousSceneAssets;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            // The referenced Scene Asset can have changed when undo/redo happens so we ensure to 
            // reload the Hierarchy which depends on the current SubScene state. 
            SceneHierarchyHooks.ReloadAllSceneHierarchies();
        }

        void CachePreviousSceneAssetReferences()
        {
            var numTargets = targets.Length;
            if (m_PreviousSceneAssets == null || m_PreviousSceneAssets.Length != numTargets)
            {
                m_PreviousSceneAssets = new SceneAsset[numTargets];
            }
            for (int i = 0; i < numTargets; ++i)
            {
                var subScene = (SubScene)targets[i];
                m_PreviousSceneAssets[i] = subScene.SceneAsset;
            }
        }

        void HandleChangedSceneAssetReferences()
        {
            bool needsHierarchyReload = false;
            var numTargets = targets.Length;
            for (int i = 0; i < numTargets; ++i)
            {
                var subScene = (SubScene)targets[i];
                var prevSceneAsset = m_PreviousSceneAssets[i];
                if (prevSceneAsset != subScene.SceneAsset)
                {
                    if (!needsHierarchyReload)
                    {
                        // First time we see there's a change in Scene Asset,
                        // check if new scene is already loaded but not as a Sub Scene.
                        var scene = subScene.EditingScene;
                        if (scene.IsValid() && !scene.isSubScene)
                        {
                            if (EditorUtility.DisplayDialog("Convert to Sub Scene?", "The Scene is already loaded as a root Scene. Do you want to convert it to a Sub Scene?", "Convert", "Cancel"))
                            {
                                // Make loaded scene a Sub Scene. Only needs to be done once,
                                // since even with multi-editing, user can only have assigned one Scene.
                                scene.isSubScene = true;
                            }
                            else
                            {
                                // Cancel assigning new Scene Asset (after the fact).
                                Undo.PerformUndo();
                                break;
                            }
                        }
                    }

                    needsHierarchyReload = true;
                    if (prevSceneAsset != null)
                    {
                        Scene prevScene = SceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(prevSceneAsset));
                        if (prevScene.isLoaded && prevScene.isSubScene)
                        {
                            if (prevScene.isDirty)
                                EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { prevScene });

                            // We need to close the Scene if it is loaded to prevent having scenes loaded
                            // that are not visualized in the Hierarhcy.
                            EditorSceneManager.CloseScene(prevScene, true);
                        }
                    }
                }
            }
            if (needsHierarchyReload)
                SceneHierarchyHooks.ReloadAllSceneHierarchies();
        }

        public override void OnInspectorGUI()
        {
            var subScene = target as SubScene;

            if (!subScene.IsInMainStage())
            {
                // In Prefab Mode and when selecting a Prefab Asset in the Project Browser we only show the inspector of data of the 
                // SubScene, and not the load/unload/edit/close buttons.
                base.OnInspectorGUI();

                EditorGUILayout.HelpBox($"Only Sub Scenes in the Main Stage can be loaded and unloaded.", MessageType.Info, true);
                EditorGUILayout.Space();
                return;
            }

            var prevColor = subScene.HierarchyColor;
            CachePreviousSceneAssetReferences();

            base.OnInspectorGUI();

            HandleChangedSceneAssetReferences();

            if (subScene.HierarchyColor != prevColor)
                SceneHierarchyHooks.ReloadAllSceneHierarchies();

            var targetsArray = targets;
            var subscenes = new SubScene[targetsArray.Length];
            targetsArray.CopyTo(subscenes, 0);

            GUILayout.BeginHorizontal();
            if (!SubSceneInspectorUtility.IsEditingAll(subscenes))
            {
                GUI.enabled = SubSceneInspectorUtility.CanEditScene(subscenes);
                if (GUILayout.Button("Edit"))
                {
                    SubSceneInspectorUtility.EditScene(subscenes);
                }
            }
            else
            {
                GUI.enabled = true;
                if (GUILayout.Button("Close"))
                {
                    SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(subscenes);
                }
            }
    
            GUI.enabled = SubSceneInspectorUtility.IsDirty(subscenes);
            if (GUILayout.Button("Save"))
            {
                SubSceneInspectorUtility.SaveScene(subscenes);
            }
            GUI.enabled = true;
    
            GUILayout.EndHorizontal();
            
            var scenes = SubSceneInspectorUtility.GetLoadableScenes(subscenes);

            GUILayout.Space(10);
            
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                foreach (var scene in scenes)
                {
                    if (!entityManager.HasComponent<RequestSceneLoaded>(scene.Scene))
                    {
                        if (GUILayout.Button($"Load '{scene.Name}'"))
                        {
                            entityManager.AddComponentData(scene.Scene, new RequestSceneLoaded());
                            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button($"Unload '{scene.Name}'"))
                        {
                            entityManager.RemoveComponent<RequestSceneLoaded>(scene.Scene);
                            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                        }
                    }
                }
            }

    #if false
            // @TODO: TEMP for debugging
            if (GUILayout.Button("ClearWorld"))
            {
                World.DisposeAllWorlds();
                DefaultWorldInitialization.Initialize("Default World", !Application.isPlaying);
    
                var scenes = FindObjectsOfType<SubScene>();
                foreach (var scene in scenes)
                {
                    var oldEnabled = scene.enabled; 
                    scene.enabled = false;
                    scene.enabled = oldEnabled;
                }
                
                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
            }
    #endif

            bool hasDuplicates = subScene.SceneAsset != null && (SubScene.AllSubScenes.Count(s => (s.SceneAsset == subScene.SceneAsset)) > 1);
            if (hasDuplicates)
            {
                EditorGUILayout.HelpBox($"The Scene Asset '{subScene.EditableScenePath}' is used mutiple times and this is not supported. Clear the reference.", MessageType.Warning, true);
                if (GUILayout.Button("Clear"))
                {
                    subScene.SceneAsset = null;
                    SceneHierarchyHooks.ReloadAllSceneHierarchies();
                }
                EditorGUILayout.Space();
            }

            var uncleanHierarchyObject = SubSceneInspectorUtility.GetUncleanHierarchyObject(subscenes);
            if (uncleanHierarchyObject != null)
            {
                EditorGUILayout.HelpBox($"Scene transform values are not applied to scenes child transforms. But {uncleanHierarchyObject.name} has an offset Transform.", MessageType.Warning, true);
                if (GUILayout.Button("Clear"))
                {
                    foreach (var scene in subscenes)
                    {
                        scene.transform.localPosition = Vector3.zero;
                        scene.transform.localRotation = Quaternion.identity;
                        scene.transform.localScale = Vector3.one;
                    }
                }
                EditorGUILayout.Space();
            }
            if (SubSceneInspectorUtility.HasChildren(subscenes))
            {
                EditorGUILayout.HelpBox($"SubScenes can not have child game objects. Close the scene and delete the child game objects.", MessageType.Warning, true);
            }

            if (CheckConversionLog(subScene))
            {
                GUILayout.Space(10);
                GUILayout.Label("Importing...");
                Repaint();
            }
            if (m_ConversionLog.Length != 0)
            {
                GUILayout.Space(10);

                GUILayout.Label("Conversion Log");
                GUILayout.TextArea(m_ConversionLog);
            }
        }

        // Invoked by Unity magically for FrameSelect command.
        // Frames the whole sub scene in scene view
        bool HasFrameBounds()
        {
            return !SubSceneInspectorUtility.GetActiveWorldMinMax(World.DefaultGameObjectInjectionWorld, targets).Equals(MinMaxAABB.Empty);
        }

        Bounds OnGetFrameBounds()
        {
            AABB aabb = SubSceneInspectorUtility.GetActiveWorldMinMax(World.DefaultGameObjectInjectionWorld, targets); 
            return new Bounds(aabb.Center, aabb.Size);
        }
        
        // Visualize SubScene using bounding volume when it is selected.
        [DrawGizmo(GizmoType.Selected)]
        static void DrawSubsceneBounds(SubScene scene, GizmoType gizmoType)
        {
            SubSceneInspectorUtility.DrawSubsceneBounds(scene);
        }
        
        bool CheckConversionLog(SubScene subScene)
        {
            var pendingWork = false;

            foreach (var world in World.All)
            {
                var sceneSystem = world.GetExistingSystem<SceneSystem>();
                if (sceneSystem is null)
                    continue;

                if (!m_ConversionLogLoaded.TryGetValue(sceneSystem.BuildConfigurationGUID, out var loaded))
                    m_ConversionLogLoaded.Add(sceneSystem.BuildConfigurationGUID, false);
                else if (loaded)
                    continue;

                var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, sceneSystem.BuildConfigurationGUID, UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode.Queue);
                if (!hash.IsValid)
                {
                    pendingWork = true;
                    continue;
                }

                m_ConversionLogLoaded[sceneSystem.BuildConfigurationGUID] = true;

                UnityEditor.Experimental.AssetDatabaseExperimental.GetArtifactPaths(hash, out var paths);
                var logPath = EntityScenesPaths.GetLoadPathFromArtifactPaths(paths, EntityScenesPaths.PathType.EntitiesConversionLog);
                if (logPath == null)
                    continue;

                var log = File.ReadAllText(logPath);
                if (log.Trim().Length != 0)
                {
                    if (m_ConversionLog.Length != 0)
                        m_ConversionLog += "\n\n";
                    m_ConversionLog += log;
                }
            }

            return pendingWork;
        }
    }
}
