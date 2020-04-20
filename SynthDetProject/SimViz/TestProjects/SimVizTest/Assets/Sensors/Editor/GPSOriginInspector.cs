using System.IO;
using UnityEditor;
using UnityEngine;

namespace Syncity.Sensors
{
    [CustomEditor(typeof(GPSOrigin))]
    public class GPSOriginInspector : Editor
    {
        [MenuItem("Assets/Create/Syncity/GPS Origin")]
        public static void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<GPSOrigin>();

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (path == "")
            {
                path = "Assets";
            }
            else if (Path.GetExtension(path) != "")
            {
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + typeof(GPSOrigin).ToString() + ".asset");

            AssetDatabase.CreateAsset(asset, assetPathAndName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
