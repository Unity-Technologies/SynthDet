using System;
using SynthDet.Randomizers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers.SampleRandomizers;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.SceneManagement;
using BackgroundObjectPlacementRandomizer = SynthDet.Randomizers.BackgroundObjectPlacementRandomizer;
using ForegroundObjectPlacementRandomizer = SynthDet.Randomizers.ForegroundObjectPlacementRandomizer;

namespace SynthDet
{
    public static class MenuItems
    {
        [MenuItem("SynthDet/Open SynthDet Scene")]
        static void OpenSynthDetScene()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SynthDet.unity");
            }
            catch (Exception e)
            {
                Debug.LogError("Could not open the SynthDet Scene. Make sure the file Assets/Scenes/SynthDet.unity exists and is a valid Scene.");
                Debug.LogException(e);
            }
        }               
    }
}
