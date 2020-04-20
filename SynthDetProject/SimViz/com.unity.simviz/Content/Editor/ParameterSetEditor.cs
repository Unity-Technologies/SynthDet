using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SimViz.Scenarios;

[CustomPropertyDrawer(typeof(ParameterSet), true)]
public class ParameterSetEditor : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (new ExtendedScriptableObjectDrawer()).GetPropertyHeight(property, label);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var parameterSet = property.objectReferenceValue as ParameterSet;
        (new ExtendedScriptableObjectDrawer()).OnGUI(position, property, label);

        // Check if any values have changed on the type
        if (parameterSet.hasChanged)
        {
            parameterSet.ApplyParameters();
            parameterSet.hasChanged = false;
        }
    }
}
