using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.IO.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace Unity.Scenes
{
    struct AsyncLoadSceneData
    {
        public EntityManager EntityManager;
        public int ExpectedObjectReferenceCount;
        public int SceneSize;
        public string ResourcesPathObjRefs;
        public string ScenePath;
        public bool BlockUntilFullyLoaded;
    }

    unsafe class AsyncLoadSceneOperation
    {
        public enum LoadingStatus
        {
            Completed,
            NotStarted,
            WaitingForAssetBundleLoad,
            WaitingForAssetLoad,
            WaitingForResourcesLoad,
            WaitingForEntitiesLoad,
            WaitingForSceneDeserialization
        }

        public override string ToString()
        {
            return $"AsyncLoadSceneJob({_ScenePath})";
        }

        unsafe struct FreeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public void* ptr;
            public Allocator allocator;
            public void Execute()
            {
                UnsafeUtility.Free(ptr, allocator);
            }
        }

        public void Dispose()
        {
            if (_LoadingStatus == LoadingStatus.Completed)
            {
                new FreeJob { ptr = _FileContent, allocator = Allocator.Persistent }.Schedule();
            }
            else if (_LoadingStatus == LoadingStatus.WaitingForResourcesLoad || _LoadingStatus == LoadingStatus.WaitingForEntitiesLoad)
            {
                new FreeJob { ptr = _FileContent, allocator = Allocator.Persistent }.Schedule(_ReadHandle.JobHandle);
            }
            else if (_LoadingStatus == LoadingStatus.WaitingForSceneDeserialization)
            {
                _EntityManager.ExclusiveEntityTransactionDependency.Complete();
                new FreeJob { ptr = _FileContent, allocator = Allocator.Persistent }.Schedule();
            }

            _SceneBundleHandle?.Release();
        }

        struct AsyncLoadSceneJob : IJob
        {
            public GCHandle                     LoadingOperationHandle;
            public GCHandle                     ObjectReferencesHandle;
            public ExclusiveEntityTransaction   Transaction;
            [NativeDisableUnsafePtrRestriction]
            public byte*                        FileContent;

            static readonly ProfilerMarker k_ProfileDeserializeWorld = new ProfilerMarker("AsyncLoadSceneJob.DeserializeWorld");

            public void Execute()
            {
                var loadingOperation = (AsyncLoadSceneOperation)LoadingOperationHandle.Target;
                LoadingOperationHandle.Free();

                var objectReferences = (UnityEngine.Object[]) ObjectReferencesHandle.Target;
                ObjectReferencesHandle.Free();

                try
                {
                    using (var reader = new MemoryBinaryReader(FileContent))
                    {
                        k_ProfileDeserializeWorld.Begin();
                        SerializeUtility.DeserializeWorld(Transaction, reader, objectReferences);
                        k_ProfileDeserializeWorld.End();
                    }
                }
                catch (Exception exc)
                {
                    loadingOperation._LoadingFailure = exc.ToString();
                }
            }
        }

        AsyncLoadSceneData      _Data;
        string                  _ScenePath => _Data.ScenePath;
        int                     _SceneSize => _Data.SceneSize;
        int                     _ExpectedObjectReferenceCount => _Data.ExpectedObjectReferenceCount;
        string                  _ResourcesPathObjRefs => _Data.ResourcesPathObjRefs;
        EntityManager           _EntityManager => _Data.EntityManager;
        bool                    _BlockUntilFullyLoaded => _Data.BlockUntilFullyLoaded;

        ReferencedUnityObjects  _ResourceObjRefs;

        SceneBundleHandle       _SceneBundleHandle;
        AssetBundleRequest      _AssetRequest;

        LoadingStatus           _LoadingStatus;
        string                  _LoadingFailure;

        byte*                    _FileContent;
        ReadHandle               _ReadHandle;

        private double _StartTime;
        
        public AsyncLoadSceneOperation(AsyncLoadSceneData asyncLoadSceneData)
        {
            _Data = asyncLoadSceneData;
            _LoadingStatus = LoadingStatus.NotStarted;
        }

        public bool IsCompleted
        {
            get
            {
                return _LoadingStatus == LoadingStatus.Completed;
            }
        }

        public string ErrorStatus
        {
            get
            {
                if (_LoadingStatus == LoadingStatus.Completed)
                    return _LoadingFailure;
                else
                    return null;
            }
        }

        public SceneBundleHandle StealBundle()
        {
            SceneBundleHandle sceneBundleHandle = _SceneBundleHandle;
            _SceneBundleHandle = null;
            return sceneBundleHandle;
        }

        private void UpdateBlocking()
        {
            if (_LoadingStatus == LoadingStatus.Completed)
                return;
            if (_SceneSize == 0)
                return;

            try
            {
                _StartTime = Time.realtimeSinceStartup;

                _FileContent = (byte*)UnsafeUtility.Malloc(_SceneSize, 16, Allocator.Persistent);

                ReadCommand cmd;
                cmd.Buffer = _FileContent;
                cmd.Offset = 0;
                cmd.Size = _SceneSize;
                _ReadHandle = AsyncReadManager.Read(_ScenePath, &cmd, 1);

                if (_ExpectedObjectReferenceCount != 0)
                {
#if UNITY_EDITOR
                    var resourceRequests = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(_ResourcesPathObjRefs);
                    _ResourceObjRefs = (ReferencedUnityObjects)resourceRequests[0];
#else
                    _SceneBundleHandle = SceneBundleHandle.CreateOrRetainBundle(_ResourcesPathObjRefs);
                    _ResourceObjRefs = _SceneBundleHandle.AssetBundle.LoadAsset<ReferencedUnityObjects>(Path.GetFileName(_ResourcesPathObjRefs));
#endif
                }
                
                ScheduleSceneRead(_ResourceObjRefs);
                _EntityManager.ExclusiveEntityTransactionDependency.Complete();
            }
            catch (Exception e)
            {
                _LoadingFailure = e.Message;
            }
            _LoadingStatus = LoadingStatus.Completed;
        }

        private void UpdateAsync()
        {
            //@TODO: Try to overlap Resources load and entities scene load

            // Begin Async resource load
            if (_LoadingStatus == LoadingStatus.NotStarted)
            {
                if (_SceneSize == 0)
                    return;

                try
                {
                    _StartTime = Time.realtimeSinceStartup;

                    _FileContent = (byte*)UnsafeUtility.Malloc(_SceneSize, 16, Allocator.Persistent);

                    ReadCommand cmd;
                    cmd.Buffer = _FileContent;
                    cmd.Offset = 0;
                    cmd.Size = _SceneSize;
                    _ReadHandle = AsyncReadManager.Read(_ScenePath, &cmd, 1);

                    if (_ExpectedObjectReferenceCount != 0)
                    {
#if UNITY_EDITOR
                        var resourceRequests = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(_ResourcesPathObjRefs);
                        _ResourceObjRefs = (ReferencedUnityObjects)resourceRequests[0];

                        _LoadingStatus = LoadingStatus.WaitingForResourcesLoad;
#else
                        _SceneBundleHandle = SceneBundleHandle.CreateOrRetainBundle(_ResourcesPathObjRefs);
                        _LoadingStatus = LoadingStatus.WaitingForAssetBundleLoad;
#endif
                    }
                    else
                    {
                        _LoadingStatus = LoadingStatus.WaitingForEntitiesLoad;
                    }
                }
                catch (Exception e)
                {
                    _LoadingFailure = e.Message;
                    _LoadingStatus = LoadingStatus.Completed;
                }
            }

            // Once async asset bundle load is done, we can read the asset
            if (_LoadingStatus == LoadingStatus.WaitingForAssetBundleLoad)
            {
                if (!_SceneBundleHandle.IsReady())
                    return;

                if (!_SceneBundleHandle.AssetBundle)
                {
                    _LoadingFailure = $"Failed to load Asset Bundle '{_ResourcesPathObjRefs}'";
                    _LoadingStatus = LoadingStatus.Completed;
                    return;
                }

                var fileName = Path.GetFileName(_ResourcesPathObjRefs);
                
                _AssetRequest = _SceneBundleHandle.AssetBundle.LoadAssetAsync(fileName);
                _LoadingStatus = LoadingStatus.WaitingForAssetLoad;
            }

            // Once async asset bundle load is done, we can read the asset
            if (_LoadingStatus == LoadingStatus.WaitingForAssetLoad)
            {
                if (!_AssetRequest.isDone)
                    return;

                if (!_AssetRequest.asset)
                {
                    _LoadingFailure = $"Failed to load Asset '{Path.GetFileName(_ResourcesPathObjRefs)}'";
                    _LoadingStatus = LoadingStatus.Completed;
                    return;
                }

                _ResourceObjRefs = _AssetRequest.asset as ReferencedUnityObjects;

                if (_ResourceObjRefs == null)
                {
                    _LoadingFailure = $"Failed to load object references resource '{_ResourcesPathObjRefs}'";
                    _LoadingStatus = LoadingStatus.Completed;
                    return;
                }

                _LoadingStatus = LoadingStatus.WaitingForEntitiesLoad;
            }

            // Once async resource load is done, we can async read the entity scene data
            if (_LoadingStatus == LoadingStatus.WaitingForResourcesLoad)
            {
                if (_ResourceObjRefs == null)
                {
                    _LoadingFailure = $"Failed to load object references resource '{_ResourcesPathObjRefs}'";
                    _LoadingStatus = LoadingStatus.Completed;
                    return;
                }

                _LoadingStatus = LoadingStatus.WaitingForEntitiesLoad;
            }

            if (_LoadingStatus == LoadingStatus.WaitingForEntitiesLoad)
            {
                try
                {
                    _LoadingStatus = LoadingStatus.WaitingForSceneDeserialization;
                    ScheduleSceneRead(_ResourceObjRefs);

                    if (_BlockUntilFullyLoaded)
                    {
                        _EntityManager.ExclusiveEntityTransactionDependency.Complete();
                    }
                }
                catch (Exception e)
                {
                    _LoadingFailure = e.Message;
                    _LoadingStatus = LoadingStatus.Completed;
                }
            }

            // Complete Loading status
            if (_LoadingStatus == LoadingStatus.WaitingForSceneDeserialization)
            {
                if (_EntityManager.ExclusiveEntityTransactionDependency.IsCompleted)
                {
                    _EntityManager.ExclusiveEntityTransactionDependency.Complete();

                    _LoadingStatus = LoadingStatus.Completed;
                    var currentTime = Time.realtimeSinceStartup;
                    var totalTime = currentTime - _StartTime;
                    System.Console.WriteLine($"Streamed scene with {totalTime * 1000,3:f0}ms latency from {_ScenePath}");
                }
            }
        }
        

        public void Update()
        {
            if (_BlockUntilFullyLoaded)
            {
                UpdateBlocking();
            }
            else
            {
                UpdateAsync();
            }
        }

        void ScheduleSceneRead(ReferencedUnityObjects objRefs)
        {
            var transaction = _EntityManager.BeginExclusiveEntityTransaction();
            SerializeUtilityHybrid.DeserializeObjectReferences(_EntityManager, objRefs, _ScenePath, out var objectReferences);

            var loadJob = new AsyncLoadSceneJob
            {
                Transaction = transaction,
                LoadingOperationHandle = GCHandle.Alloc(this),
                ObjectReferencesHandle = GCHandle.Alloc(objectReferences),
                FileContent = _FileContent
            };

            _EntityManager.ExclusiveEntityTransactionDependency = loadJob.Schedule(JobHandle.CombineDependencies(_EntityManager.ExclusiveEntityTransactionDependency, _ReadHandle.JobHandle));
        }
    }

}
