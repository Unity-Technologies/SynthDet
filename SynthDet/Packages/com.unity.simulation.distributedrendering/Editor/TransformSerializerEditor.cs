using UnityEngine;
using UnityEditor;

namespace Unity.Simulation.DistributedRendering.Render
{
    /// <summary>
    /// Add some buttons for creating IDs, testing serialization & deserialization, etc.
    /// </summary>
    [CustomEditor(typeof(TransformSerializerBehavior))]
	public class TransformSerializerEditor : Editor
	{
        public override void OnInspectorGUI()
        {
			DrawDefaultInspector();

			var ts = (TransformSerializerBehavior)target;
            if (null == ts)
            {
                return;
            }

            if (GUILayout.Button ("Remove Ids"))
            {
                ts.RemoveIDs();
            }
            if (GUILayout.Button("Reset IDs"))
            {
                ts.ResetIDs();
            }
            if (GUILayout.Button("Print IDs"))
            {
                ts.PrintIDs();
            }

            EditorUtility.SetDirty(ts.gameObject);
        }
    }
}


