#if UNITY_2020_1_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EnableHeadless
{
    [MenuItem("Tools/Set Headless")]
    private static void NewMenuOption()
    {
        var so = PlayerSettings.GetSerializedObject();
        var s = PlayerSettings.FindProperty("allowHeadlessRendering");
        s.boolValue = true;
        so.ApplyModifiedProperties();
    }
}
#endif