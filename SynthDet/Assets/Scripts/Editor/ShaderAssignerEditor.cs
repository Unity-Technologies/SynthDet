using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu]
public class ShaderAssigner : ScriptableObject
{
    public Shader ShaderOpaque;
    public Shader ShaderTransparent;
    public List<string> FolderNames;

    public void OnEnable()
    {
        ShaderOpaque = Shader.Find("Shader Graphs/HueShiftOpaque");
        ShaderTransparent = Shader.Find("Shader Graphs/HueShiftTransparent");
        FolderNames = new List<string>
        {
            "foreground"
        };
    }

    public void AssignShaders()
    {
        foreach (var folder in FolderNames)
        {
            var materials = Resources.LoadAll<Material>(folder);
            Debug.Log($"Found {materials.Length} materials in {folder} - re-assigning shader...");
            foreach (var mat in materials)
            {
                if (mat.name.Contains("transparent"))
                {
                    Debug.Log($"Assigning {ShaderTransparent.name} to {mat.name}");
                    mat.shader = ShaderTransparent;
                }
                else
                {
                    Debug.Log($"Assigning {ShaderOpaque.name} to {mat.name}");
                    mat.shader = ShaderOpaque;
                }

            }
        }
    }
}

[CustomEditor(typeof(ShaderAssigner))]
public class ShaderAssignerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        if (GUILayout.Button("Assign"))
        {
            ((ShaderAssigner)target).AssignShaders();
        }
    }
}

