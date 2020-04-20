using UnityEngine;
using UnityEngine.SimViz.Scenarios;

namespace UnityEditor.SimViz.Scenarios
{
    [CustomEditor(typeof(RoadNetworkPath))]
    public class RoadNetworkPathEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var rnp = (RoadNetworkPath)target;
            if(GUILayout.Button("Generate Waypoint Path"))
            {
                rnp.GeneratePathFromInspectorValues();
            }

            if (GUILayout.Button("Randomize Waypoint Path"))
            {
                rnp.GenerateRandomizedPath();
            }
        }
    }
}