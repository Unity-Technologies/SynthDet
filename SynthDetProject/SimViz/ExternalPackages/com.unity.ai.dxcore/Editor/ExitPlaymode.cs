using UnityEditor;

namespace Unity.AI.Simulation
{
    [InitializeOnLoad]
    public static class ExitPlaymode
    {
        static ExitPlaymode()
        {
            EditorApplication.playModeStateChanged += (PlayModeStateChange change) =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode)
                {
                    DXManager.Instance.Shutdown();
                    DXManager.Instance.Update(0);
                }
            };
        }
    }
}