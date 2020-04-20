using UnityEngine;
using UnityEngine.SimViz.Content.Pipeline;

#if UNITY_EDITOR
namespace UnityEditor.SimViz.Content.Pipeline
{
    [CustomEditor(typeof(PolygonSoupPlacementPipeline))]
    public class PolygonSoupPlacementPipelineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var parameters = (PolygonSoupPlacementPipeline) target;
            DrawDefaultInspector();
            if (GUILayout.Button("Run Pipeline"))
            {
                var output = parameters.RunPipeline();
                if (output != null)
                    Undo.RegisterCreatedObjectUndo(output, "placement");
            }
        }
    }
}
#endif