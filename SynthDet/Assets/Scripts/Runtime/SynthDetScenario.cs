using System.Collections.Generic;
using System.Linq;
using SynthDet.Randomizers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Perception.Randomization.Scenarios;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SynthDet.Scenarios
{
    [AddComponentMenu("SynthDet/SynthDet Scenario")]
    public class SynthDetScenario : FixedLengthScenario
    {
        bool m_LoadedAddressableAssets;
        AsyncOperationHandle<IList<GameObject>> m_AddressableForegroundPrefabs;
        
        /// <inheritdoc/>
        protected override bool isScenarioReadyToStart
        {
            get
            {
                if (m_LoadedAddressableAssets)
                    return true;
                
                if (m_AddressableForegroundPrefabs.Status != AsyncOperationStatus.Succeeded)
                    return false;
                
                var randomizer = GetRandomizer<ForegroundObjectPlacementRandomizer>();
                randomizer.prefabs = m_AddressableForegroundPrefabs.Result.ToArray();
                m_LoadedAddressableAssets = true;
                return true;
            }
        }

        /// <inheritdoc/>
        protected override void OnAwake()
        {
            base.OnAwake();
            m_AddressableForegroundPrefabs = Addressables.LoadAssetsAsync<GameObject>("foreground", null);
        }
    }
}
