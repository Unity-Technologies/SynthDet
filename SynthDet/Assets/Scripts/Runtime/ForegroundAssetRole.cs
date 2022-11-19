using System;
using SynthDet.Randomizers;
using SynthDet.RandomizerTags;
using UnityEngine;
using UnityEngine.Perception.Randomization;

namespace Runtime.AssetRoles
{
    [Serializable]
    public class ForegroundAssetRole : AssetRole<GameObject>
    {
        public override string label => "foreground";
        public override string description => "Foreground objects for CV training and prediction";

        public override void Preprocess(GameObject asset)
        {
            ConfigureLayerRecursive(asset);
            AddRandomizerTags(asset);
        }
        
        void ConfigureLayerRecursive(GameObject prefab)
        {
            prefab.layer = LayerMask.NameToLayer("Foreground");
            for (var i = 0; i < prefab.transform.childCount; i++)
            {
                var child = prefab.transform.GetChild(i).gameObject;
                ConfigureLayerRecursive(child);
            }
        }

        void AddRandomizerTags(GameObject prefab)
        {
            Utilities.GetOrAddComponent<ForegroundObjectMetricReporterTag>(prefab);
            Utilities.GetOrAddComponent<UnifiedRotationRandomizerTag>(prefab);
            Utilities.GetOrAddComponent<ForegroundScaleRandomizerTag>(prefab);
        }
    }
}
