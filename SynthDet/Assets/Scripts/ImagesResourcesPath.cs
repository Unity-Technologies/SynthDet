using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct TextureResource
{
    public string relativePath;
    public Texture2D texture;
}
public class ImagesResourcesPath : ScriptableObject
{
    public List<TextureResource> textures;
}
