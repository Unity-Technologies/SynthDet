using UnityEngine;

public class SceneReferenceTest : MonoBehaviour
{
    private void OnGUI()
    {
        DisplayLevel(exampleNull);
        DisplayLevel(exampleMissing);
        DisplayLevel(exampleDisabled);
        DisplayLevel(exampleEnabled);
    }

    public void DisplayLevel(SceneReference scene)
    {
        GUILayout.Label(new GUIContent("Scene name Path: " + scene));
        if (GUILayout.Button("Load " + scene))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
    }

    public SceneReference exampleNull;
    public SceneReference exampleMissing;
    public SceneReference exampleDisabled;
    public SceneReference exampleEnabled;
    
}
