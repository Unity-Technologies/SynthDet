using System;
using UnityEngine;

[Serializable]
public struct AppParams
{
    public float[] ScaleFactors;
    public int MaxFrames;
    public int MaxForegroundObjectsPerFrame;

    [Header("Background Generation")]
    // Number of cells to create per foreground object for placing background objects
    [Range(1, 9)]
    public int BackgroundObjectDensity;
    // The number of times the background generator will place an object in each "cell"
    [Range(0, 5)]
    public int NumBackgroundFillPasses;

    [Header("Randomization Parameters")]
    public float ScalingMin;
    public float ScalingSize;
    public float LightColorMin;
    public float LightRotationMax;
    public float BackgroundHueMaxOffset;
    public float OccludingHueMaxOffset;
    [Range(0f, .8f)]
    public float BackgroundObjectInForegroundChance;
    [Header("Post Processing Randomization")]
    // Maximum for how much a white noise value gets blended into its corresponding pixel
    [Range(0f, 1f)] 
    public float NoiseStrengthMax;
    // Max value, in UV coordinates, the blur kernel size can be randomized to
    [Range(0f, 1f)] 
    public float BlurKernelSizeMax;
    // Max value, as a fraction of the blur size, that represents one standard deviatian of the gaussian distribution
    [Range(0f, 1f)] 
    public float BlurStandardDeviationMax;
}
