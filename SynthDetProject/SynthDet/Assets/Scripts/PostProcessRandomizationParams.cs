using System;
using UnityEngine;

[Serializable]
public struct PostProcessRandomizationParams
{
    // Maximum for how much a white noise value gets blended into its corresponding pixel
    [Range(0f, 1f)] public float NoiseStrengthMax;
    
    // Max value, in UV coordinates, the blur kernel size can be randomized to
    [Range(0f, 1f)] public float BlurKernelSizeMax;
    // Max value, as a fraction of the blur size, that represents one standard deviatian of the gaussian distribution
    [Range(0f, 1f)] public float BlurStandardDeviationMax;
}
