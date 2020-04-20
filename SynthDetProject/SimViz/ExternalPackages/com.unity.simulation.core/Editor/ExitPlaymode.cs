using UnityEditor;

namespace Unity.Simulation
{
    /// <summary>
    /// Utility class that shuts down the SDK when exiting playmode.
    /// </summary>
    [InitializeOnLoad]
    public static class ExitPlaymode
    {
        static ExitPlaymode()
        {
            EditorApplication.playModeStateChanged += (PlayModeStateChange change) =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode)
                {
                    Manager.Instance.Shutdown();
                }
            };
        }
    }
}