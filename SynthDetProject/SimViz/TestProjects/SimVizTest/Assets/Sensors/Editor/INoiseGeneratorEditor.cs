using System.IO;
using UnityEditor;
using UnityEngine;

namespace Syncity.Sensors
{
    public static class INoiseGeneratorEditor
    {
        public static INoiseGenerator<T> Field<T>(string label, INoiseGenerator<T> value)
        {
            var accelerationNoiseObject = EditorGUILayout.ObjectField(label, value as ScriptableObject, typeof(INoiseGenerator<T>), true);
            return accelerationNoiseObject as INoiseGenerator<T>;
        }

    }
}
