using System.Linq;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine;

namespace UnityEditor.UI
{
    internal static class HierarchyOverlay
    {
        static class Styles
        {
            public static float subSceneEditingButtonWidth = 16f;
            public static GUIContent subSceneEditingTooltip = EditorGUIUtility.TrTextContent(string.Empty, "Toggle whether the Sub Scene is open for editing.");
        }

        internal static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject != null)
            {
                SubScene subScene;
                if (gameObject.TryGetComponent(out subScene))
                {
                    if (!subScene.CanBeLoaded())
                        return;

                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(subScene.gameObject))
                        return;

                    var evt = Event.current;
                    Rect buttonRect = selectionRect;
                    buttonRect.x = buttonRect.xMax;
                    buttonRect.width = Styles.subSceneEditingButtonWidth;

                    var loaded = subScene.EditingScene.isLoaded;
                    var wantsLoaded = EditorGUI.Toggle(buttonRect, loaded);
                    if (wantsLoaded != loaded)
                    {
                        SubScene[] subScenes;
                        var selectedSubScenes = Selection.GetFiltered<SubScene>(SelectionMode.TopLevel);
                        if (selectedSubScenes.Contains(subScene))
                            subScenes = selectedSubScenes;
                        else
                            subScenes = new[] { subScene };

                        if (wantsLoaded)
                            SubSceneInspectorUtility.EditScene(subScenes);
                        else
                            SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(subScenes);
                    }

                    if (buttonRect.Contains(evt.mousePosition))
                    {
                        GUI.Label(buttonRect, Styles.subSceneEditingTooltip);
                    }
                }
            }
        }
    }
}