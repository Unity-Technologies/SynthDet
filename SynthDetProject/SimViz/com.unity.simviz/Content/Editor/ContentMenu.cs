using System;
using UnityEditor;
using UnityEngine.SimViz.Content.MapElements;
using UnityEngine.SimViz.Content.Sampling;
using UnityEngine.SimViz.Scenarios;
namespace UnityEngine.SimViz.Content.Editor
{
    public class ContentMenu : ScriptableObject
    {
        [MenuItem("SimViz/Debug Rendering/RoadNetwork/Reference Line Mesh")]
        public static void RoadNetworkToMesh()
        {
            var roadNetworkDescription = GetSelectedRoadNetwork();
            if (roadNetworkDescription == null)
                return;
            
            var go = RoadNetworkMesher.GenerateMesh(roadNetworkDescription);
            AddRoadNetworkReference(go, roadNetworkDescription);
        }

        [MenuItem("SimViz/Debug Rendering/RoadNetwork/Reference Line")]
        public static void RoadNetworkToLineRenderer()
        {
            var roadNetworkDescription = GetSelectedRoadNetwork();
            if (roadNetworkDescription == null)
                return;
            
            var go = RoadNetworkMesher.GenerateLineRenderer(roadNetworkDescription);
            AddRoadNetworkReference(go, roadNetworkDescription);
        }

        [MenuItem("SimViz/Debug Rendering/RoadNetwork/Lane Edges")]
        public static void RoadNetworkLanesToMesh()
        {
            var roadNetworkDescription = GetSelectedRoadNetwork();
            if (roadNetworkDescription == null)
            {
                Debug.LogWarning("No RoadNetworkDescription selected.");
                return;
            }
            
            var go = RoadNetworkMesher.GenerateMeshWithLanes(roadNetworkDescription);
            AddRoadNetworkReference(go, roadNetworkDescription);
        }
        [MenuItem("SimViz/Scenario Manager/Generate AppParams")]
        public static void GenerateAppParams()
        {
            if (FindObjectOfType<ScenarioManager>())
            {
                var scenarioManager = FindObjectOfType<ScenarioManager>();
                scenarioManager.GenerateAppParamsFiles();

            }
        }

        public static RoadNetworkDescription GetSelectedRoadNetwork()
        {
            var obj = Selection.activeObject;
            if (AssetDatabase.Contains(obj))
            {
                // We're in the project browser, load the RoadNetwork as an asset
                return GetRoadNetworkFromAssetPath(obj);
            }
            // Something in the scene hierarchy is selected, try to find the RoadNetworkReference
            return GetRoadNetworkFromGameObject(obj as GameObject);
        }

        private static RoadNetworkDescription GetRoadNetworkFromAssetPath(Object obj)
        {
            // XXX: This may not be necessary... is the object returned by selection always the main asset?
            var roadNetwork = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(obj)) as RoadNetworkDescription;
            return roadNetwork;
        }

        private static RoadNetworkDescription GetRoadNetworkFromGameObject(GameObject go)
        {
            var reference = go.GetComponentInParent(typeof(RoadNetworkReference)) as RoadNetworkReference;
            if (reference == null)
            {
                Debug.LogWarning($"Unable to find a Road Network associated with {go.name}.");
                return null;
            }

            return reference.RoadNetwork;
        }

        // We don't want to keep the whole RoadNetworkDescription in memory because it is large, so we just hold
        // a reference that fetches via the asset path when we need to access this information
        public static void AddRoadNetworkReference(GameObject gameObject, RoadNetworkDescription roadNetwork)
        {
            var reference = gameObject.AddComponent<RoadNetworkReference>();
            if (reference == null)
            {
                throw new Exception($"Failed to add {nameof(RoadNetworkReference)} component to {gameObject.name}");
            }

            reference.RoadNetwork = roadNetwork;
        }
    }
}
