using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Scenes.Editor
{
    [InitializeOnLoad]
    class SubSceneTransformHierarchyHook
    {
        static SubSceneTransformHierarchyHook( )
        {
            SceneHierarchyHooks.provideSubScenes = ProvideSubScenes;
            SceneHierarchyHooks.provideSubSceneName = ProvideSubSceneName;
            SceneHierarchyHooks.addItemsToGameObjectContextMenu += SubSceneContextMenu.AddExtraGameObjectContextMenuItems;
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += SubSceneContextMenu.AddExtraSceneHeaderContextMenuItems;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyOverlay.HierarchyWindowItemOnGUI;
        }

        static string GetSceneName(SceneAsset sceneAsset, Scene scene)
        {
            if (sceneAsset == null)
                return "Missing SubScene";
                
            var name = sceneAsset.name;
            
            if (scene.isDirty)
                name += "*";
            
            return name;
        }

        static SceneHierarchyHooks.SubSceneInfo[] ProvideSubScenes()
        {
            var scenes = new SceneHierarchyHooks.SubSceneInfo[SubScene.AllSubScenes.Count()];
            var sceneAssets = new HashSet<SceneAsset>();

            int index = 0;
            foreach (var subScene in SubScene.AllSubScenes)
            {
                var isSubSceneInMainStage = subScene.IsInMainStage();
                var duplicateSceneAsset = subScene.SceneAsset != null && isSubSceneInMainStage && !sceneAssets.Add(subScene.SceneAsset);
                var transform = subScene.transform;
                if (duplicateSceneAsset)
                {
                    scenes[index].sceneName = $"{subScene.SceneAsset.name}  (Duplicate Scene)";
                }
 
                var loadedScene = default(Scene);
                if (isSubSceneInMainStage && !duplicateSceneAsset)
                {
                    var candidateScene = subScene.EditingScene;
                    if (candidateScene.IsValid() && candidateScene.isSubScene)
                    {
                        loadedScene = candidateScene;
                    }
                }

                scenes[index].transform = subScene.transform;
                scenes[index].scene = loadedScene;
                scenes[index].sceneAsset = subScene.SceneAsset;
                scenes[index].color = subScene.HierarchyColor;
                index++;
            }

            return scenes;
        }

        static string ProvideSubSceneName(SceneHierarchyHooks.SubSceneInfo subSceneInfo)
        {
            // Use override scene name if set
            if (!string.IsNullOrEmpty(subSceneInfo.sceneName))
                return subSceneInfo.sceneName;

            return GetSceneName(subSceneInfo.sceneAsset, subSceneInfo.scene);
        }
    }
}
