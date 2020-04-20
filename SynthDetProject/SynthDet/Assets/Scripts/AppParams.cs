using System;

[Serializable]
public struct AppParams
{
    public float[] ScaleFactors;
    public int MaxFrames;
    public AppParams(float[] scaleFactors, int maxFrames)
    {
        ScaleFactors = scaleFactors;
        MaxFrames = maxFrames;
    }
}
