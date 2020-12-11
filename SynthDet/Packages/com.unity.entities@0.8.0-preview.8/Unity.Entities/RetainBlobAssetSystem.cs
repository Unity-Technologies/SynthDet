using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Unity.Entities
{
#if UNITY_EDITOR
    // Workaround for Entities.ForEach not working in JobComponentSystems with [ExecuteAlways]
    // TODO: Remove this once the above is fixed
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    class RetainBlobAssetSystem : ComponentSystem
    {
        protected override unsafe void OnUpdate()
        {
            Entities.WithNone<RetainBlobAssetBatchPtr>().ForEach((Entity e, BlobAssetOwner blobOwner, ref RetainBlobAssets retain) =>
            {
                BlobAssetBatch.Retain(blobOwner.BlobAssetBatchPtr);
                EntityManager.AddComponentData(e, new RetainBlobAssetBatchPtr{ BlobAssetBatchPtr = blobOwner.BlobAssetBatchPtr});
            });

            Entities.WithNone<BlobAssetOwner>().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetBatchPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- == 0)
                {
                    BlobAssetBatch.Release(retainPtr.BlobAssetBatchPtr);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetBatchPtr>(e);
                }
            });

            Entities.WithNone<BlobAssetOwner>().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- == 0)
                {
                    retainPtr.BlobAsset->Invalidate();
                    UnsafeUtility.Free(retainPtr.BlobAsset, Allocator.Persistent);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetPtr>(e);
                }
            });
        }
    }

#else

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    class RetainBlobAssetSystem : JobComponentSystem
    {
        protected override unsafe JobHandle OnUpdate(JobHandle inputDeps)
        {
            Entities.WithNone<RetainBlobAssetBatchPtr>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, BlobAssetOwner blobOwner, ref RetainBlobAssets retain) =>
            {
                BlobAssetBatch.Retain(blobOwner.BlobAssetBatchPtr);
                EntityManager.AddComponentData(e, new RetainBlobAssetBatchPtr{ BlobAssetBatchPtr = blobOwner.BlobAssetBatchPtr});
            }).Run();

            Entities.WithNone<BlobAssetOwner>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetBatchPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- == 0)
                {
                    BlobAssetBatch.Release(retainPtr.BlobAssetBatchPtr);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetBatchPtr>(e);
                }
            }).Run();

            Entities.WithNone<BlobAssetOwner>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- == 0)
                {
                    retainPtr.BlobAsset->Invalidate();
                    UnsafeUtility.Free(retainPtr.BlobAsset, Allocator.Persistent);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetPtr>(e);
                }
            }).Run();
            
            return inputDeps;
        }
    }
#endif
}