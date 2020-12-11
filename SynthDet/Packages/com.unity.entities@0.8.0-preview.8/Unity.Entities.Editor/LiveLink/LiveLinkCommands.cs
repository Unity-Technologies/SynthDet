using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;

namespace Unity.Entities.Editor
{
    static class LiveLinkCommands
    {
        const string k_LiveLinkEditorMenu = "DOTS/Live Link Mode/";

        const string kEnableInEditMode = k_LiveLinkEditorMenu  + "Live Conversion in Edit Mode";
        const string kAuthoring = k_LiveLinkEditorMenu   + "SceneView: Editing State";
        const string kGameState = k_LiveLinkEditorMenu + "SceneView: Live Game State";


        [MenuItem(kEnableInEditMode, false, 0)]
        static void ToggleInEditMode()
        {
            SubSceneInspectorUtility.LiveLinkEnabledInEditMode = !SubSceneInspectorUtility.LiveLinkEnabledInEditMode;
        }

        [MenuItem(kEnableInEditMode, true)]
        static bool ValidateToggleInEditMode()
        {
            Menu.SetChecked(kEnableInEditMode, SubSceneInspectorUtility.LiveLinkEnabledInEditMode);
            return true;
        }

        [MenuItem(kAuthoring, false, 11)]
        static void LiveAuthoring()
            => SubSceneInspectorUtility.LiveLinkShowGameStateInSceneView = false;

        [MenuItem(kAuthoring, true)]
        static bool ValidateLiveConvertAuthoring()
        {
            Menu.SetChecked(kAuthoring, !SubSceneInspectorUtility.LiveLinkShowGameStateInSceneView);
            return true;
        }

        [MenuItem(kGameState, false, 11)]
        static void LiveConvertGameState() => SubSceneInspectorUtility.LiveLinkShowGameStateInSceneView = true;

        [MenuItem(kGameState, true)]
        static bool ValidateLiveConvertGameState()
        {
            Menu.SetChecked(kGameState, SubSceneInspectorUtility.LiveLinkShowGameStateInSceneView);
            return true;
        }

    }
}