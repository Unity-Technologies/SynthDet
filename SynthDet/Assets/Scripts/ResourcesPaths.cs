using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct Prefab
{
    public string relativePath;
    public GameObject gameObject;
}
public class ResourcesPaths : ScriptableObject
{
    public List<Prefab> prefabs;
}
