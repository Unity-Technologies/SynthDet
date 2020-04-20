using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReversePointsOrder))]
public class ReversePointsOrderEditor : Editor {

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ReversePointsOrder reversePointsOrder = (ReversePointsOrder)target;

        if (GUILayout.Button("Take and Reverse Points Order"))
        {
            reversePointsOrder.ReverseOrder();
        }

        
    }

}
