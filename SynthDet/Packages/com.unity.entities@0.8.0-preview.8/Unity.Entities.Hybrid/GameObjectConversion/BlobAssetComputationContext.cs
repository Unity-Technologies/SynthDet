using System;
using Unity.Collections;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities {
    /// <summary>
    /// The BlobAssetComputationContext must be used during Authoring to ECS conversion process to detect which BlobAsset should be computed and to declare their association with a UnityObject
    /// </summary>
    /// <typeparam name="TS">The type of the setting struct to be used to generate the BlobAsset</typeparam>
    /// <typeparam name="TB">The type of the BlobAsset to generate</typeparam>
    /// <remarks>
    /// The context must typically be used in a three stages conversion process, for given type of BlobAsset to process.
    /// Multiple context can be used if multiple BlobAsset types are generated.
    /// Stages:
    ///  1) Each Authoring component to convert are evaluated>
    ///     The user calls <see cref="AssociateBlobAssetWithUnityObject"/> to declare the association between the UnityObject owning the Authoring component and the BlobAsset being processed.
    ///     Then <see cref="NeedToComputeBlobAsset"/> is called to determine if the BlobAsset needs to be computed or if it's already in the store (or registered for computation).
    ///     The user creates the setting object that contains the necessary information to create the BlobAsset later on and calls <see cref="AddBlobAssetToCompute"/>.
    ///  2) The user creates a job to compute all BlobAsset and calls <see cref="GetSettings"/> to feed the job with the settings of each BlobAsset to compute.
    ///     During the job execution, the BlobAsset will be created and typically stored in a result array.
    ///     After the job is done, the user must call <see cref="AddComputedBlobAsset"/> to add the newly created BlobAsset to the context (and the Store)
    ///  3) The user create ECS Components and attaches the BlobAsset by calling<see cref="GetBlobAsset"/>.
    /// When the context will be disposed (typically after the conversion process is done), the store will be updated with the new associations between the BlobAsset and the UnityObject(s) that use them.
    /// If a BlobAsset is no longer used by any UnityObject, it will be disposed.
    /// Thread-safety: main thread only.
    /// </remarks>
    public struct BlobAssetComputationContext<TS, TB> : IDisposable where TS : struct where TB : struct
    {
        public BlobAssetComputationContext(BlobAssetStore blobAssetStore, int initialCapacity, Allocator allocator)
        {
            m_BlobAssetStore = blobAssetStore ?? throw new ArgumentNullException(nameof(blobAssetStore), "A valid BlobAssetStore must be passed to construct a BlobAssetComputationContext");
            m_ToCompute = new NativeHashMap<Hash128, TS>(initialCapacity, allocator);
            m_Computed = new NativeHashMap<Hash128, BlobAssetReference<TB>>(initialCapacity, allocator);
            m_BlobPerUnityObject = new NativeMultiHashMap<int, Hash128>(initialCapacity, allocator);
        }

        public bool IsCreated => m_ToCompute.IsCreated;

        BlobAssetStore m_BlobAssetStore;
        NativeHashMap<Hash128, TS> m_ToCompute;
        NativeHashMap<Hash128, BlobAssetReference<TB>> m_Computed;
        NativeMultiHashMap<int, Hash128> m_BlobPerUnityObject;

        public NativeArray<TS> GetSettings(Allocator allocator) => m_ToCompute.GetValueArray(allocator);

        /// <summary>
        /// Dispose the Computation context, update the BlobAssetStore with the new BlobAsset/UnityObject associations
        /// </summary>
        /// <remarks>
        /// This method will calls <see cref="UpdateBlobStore"/> to ensure the store is up to date.
        /// </remarks>
        public void Dispose()
        {
            if (!m_ToCompute.IsCreated)
            {
                return;
            }

            UpdateBlobStore();
            
            m_ToCompute.Dispose();
            m_Computed.Dispose();
            m_BlobPerUnityObject.Dispose();
        }

    #if UNITY_SKIP_UPDATES_WITH_VALIDATION_SUITE
        [Obsolete("BlobAssetComputationContext<TS, TB>.AssociateBlobAssetWithGameObject(Hash128, GameObject) is deprecated, use BlobAssetComputationContext<TS, TB>.AssociateBlobAssetWithUnityObject(Hash128, UnityObject) instead. (RemovedAfter 2020-04-08)")]
    #else
        [Obsolete("BlobAssetComputationContext<TS, TB>.AssociateBlobAssetWithGameObject(Hash128, GameObject) is deprecated, use BlobAssetComputationContext<TS, TB>.AssociateBlobAssetWithUnityObject(Hash128, UnityObject) instead. (RemovedAfter 2020-04-08) (UnityUpgradable) -> AssociateBlobAssetWithUnityObject(*)")]
    #endif
        public void AssociateBlobAssetWithGameObject(Hash128 hash, GameObject gameObject)
        {
            AssociateBlobAssetWithUnityObject(hash, gameObject);
        }
        
        /// <summary>
        /// Declare the BlobAsset being associated with the given UnityObject
        /// </summary>
        /// <param name="hash">The hash associated to the BlobAsset</param>
        /// <param name="unityObject">The UnityObject associated with the BlobAsset</param>
        /// <remarks>
        /// One of the role of the <see cref="BlobAssetComputationContext{TS,TB}"/> is to track the new association between Authoring UnityObject and BlobAsset and report them to the <see cref="BlobAssetStore"/> to automatically track the life-time of the <see cref="BlobAssetReference{T}"/> and release the instances that are no longer used.
        /// </remarks>
        public void AssociateBlobAssetWithUnityObject(Hash128 hash, UnityObject unityObject)
        {
            m_BlobPerUnityObject.Add(unityObject.GetInstanceID(), hash);
        }
        
        /// <summary>
        /// During the conversion process, the user must call this method for each BlobAsset being processed, to determine if it requires to be computed
        /// </summary>
        /// <param name="hash">The hash associated to the BlobAsset</param>
        /// <returns>true if the BlobAsset must be computed, false if it's already in the store or the computing queue</returns>
        public bool NeedToComputeBlobAsset(Hash128 hash)
        {
            return !m_ToCompute.ContainsKey(hash) && !m_BlobAssetStore.Contains<TB>(hash);
        }

        /// <summary>
        /// Call this method to record a setting object that will be used to compute a BlobAsset
        /// </summary>
        /// <param name="hash">The hash associated with the BlobAsset</param>
        /// <param name="settings">The setting object to store</param>
        public void AddBlobAssetToCompute(Hash128 hash, TS settings)
        {
            if (!m_ToCompute.TryAdd(hash, settings))
            {
                throw new ArgumentException($"The hash: {hash} already as a setting object. You shouldn't add a setting object more than once.");
            }
        }

        /// <summary>
        /// Add a newly created BlobAsset in the context and its Store.
        /// </summary>
        /// <param name="hash">The hash associated to the BlobAsset</param>
        /// <param name="blob">The BlobAsset to add</param>
        public void AddComputedBlobAsset(Hash128 hash, BlobAssetReference<TB> blob)
        {
            if (!m_Computed.TryAdd(hash, blob) || !m_BlobAssetStore.TryAdd(hash, blob))
            {
                throw new ArgumentException($"There is already a BlobAsset with the hash: {hash} in the Store or the Computed list. You should add a newly computed BlobAsset only once.");
            }
        }

        /// <summary>
        /// Get the blob asset for the corresponding hash
        /// </summary>
        /// <param name="hash">The hash associated with the BlobAsset</param>
        /// <param name="blob">The BlobAsset corresponding to the given Hash</param>
        /// <returns>true if the blob asset was found, false otherwise</returns>
        public bool GetBlobAsset(Hash128 hash, out BlobAssetReference<TB> blob)
        {
            return m_Computed.TryGetValue(hash, out blob) || m_BlobAssetStore.TryGet(hash, out blob);
        }
        
        /// <summary>
        /// Update the store with the recorded BlobAsset/UnityObject associations.
        /// </summary>
        /// <remarks>
        /// User don't have to call this method because <see cref="Dispose"/> will do it.
        /// This method can be called multiple times, on the first one will matter.
        /// </remarks>
        public void UpdateBlobStore()
        {
            var keys = m_BlobPerUnityObject.GetUniqueKeyArray(Allocator.Temp);
            using (keys.Item1)
            {
                for (var k = 0; k < keys.Item2; ++k)
                {
                    var key = keys.Item1[k];
                    var valueCount = m_BlobPerUnityObject.CountValuesForKey(key);
                    var valueArray = new NativeArray<Hash128>(valueCount, Allocator.Temp);
                    var i = 0;
                    if (m_BlobPerUnityObject.TryGetFirstValue(key, out var value, out var iterator))
                    {
                        do
                        {
                            valueArray[i++] = value;
                        } while (m_BlobPerUnityObject.TryGetNextValue(out value, ref iterator));

                        valueArray.Sort();
                    }

                    m_BlobAssetStore.UpdateBlobAssetForUnityObject<TB>(key, valueArray);
                    valueArray.Dispose();
                }
            }

            m_BlobPerUnityObject.Clear();
        }
    }
}