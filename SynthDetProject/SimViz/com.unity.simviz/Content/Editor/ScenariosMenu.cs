using UnityEditor;
using UnityEngine.SimViz.Content.Editor;

namespace UnityEngine.SimViz.Scenarios.Editor
{
    public class ScenariosMenu
    {
        [MenuItem("SimViz/Content Pipeline/Generate WaypointPath")]
        public static void RoadNetworkToWaypointPath()
        {
            var roadNetworkDescription = ContentMenu.GetSelectedRoadNetwork();
            var root = new GameObject($"{roadNetworkDescription.name}");
            ContentMenu.AddRoadNetworkReference(root, roadNetworkDescription);
            root.AddComponent(typeof(RoadNetworkPath));
        }

    }
}