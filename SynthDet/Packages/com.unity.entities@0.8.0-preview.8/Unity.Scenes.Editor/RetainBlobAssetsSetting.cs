using Unity.Build;

class RetainBlobAssetsSetting : IBuildComponent
{
    public int FramesToRetainBlobAssets;

    public string Name => "RetainBlobAssetsSetting";

    public static int GetFramesToRetainBlobAssets(BuildConfiguration config)
    {
        int framesToRetainBlobAssets = 1;
        if (config != null && config.TryGetComponent(out RetainBlobAssetsSetting retainSetting))
            framesToRetainBlobAssets = retainSetting.FramesToRetainBlobAssets;
        return framesToRetainBlobAssets;
    }
}
