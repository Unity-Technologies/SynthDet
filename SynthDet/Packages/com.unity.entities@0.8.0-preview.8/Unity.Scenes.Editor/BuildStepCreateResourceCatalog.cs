using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Build.Common;
using Unity.Build.Classic;
using Unity.Build;
using Unity.Properties;
using Unity.Platforms;

namespace Unity.Scenes.Editor
{
#if !UNITY_BUILD_CLASS_BASED_PIPELINES    
    [BuildStep(Name = "Build Resource Catalog", Description = "Build Resource Catalog", Category = "Classic")]
    sealed class BuildStepCreateResourceCatalog : BuildStep
    {
        string SceneInfoPath => $"Assets/StreamingAssets/{SceneSystem.k_SceneInfoFileName}";

        TemporaryFileTracker m_TemporaryFileTracker;

        public override Type[] RequiredComponents => new[]
        {
            typeof(SceneList)
        };

        public override BuildStepResult RunBuildStep(BuildContext context)
        {
            m_TemporaryFileTracker = new TemporaryFileTracker();
            
            var sceneList = GetRequiredComponent<SceneList>(context);
            ResourceCatalogBuildCode.WriteCatalogFile(sceneList, m_TemporaryFileTracker.TrackFile(SceneInfoPath));

            return Success();
        }

        public override BuildStepResult CleanupBuildStep(BuildContext context)
        {
            m_TemporaryFileTracker.Dispose();
            return Success();
        }
    }
#else
    class ResourceCatalogFilesProvider : AdditionalFilesProviderBase
    {
        public override Type[] UsedComponents { get; } = {typeof(SceneList), typeof(ClassicBuildProfile)};

        protected override void OnPrepareAdditionalAssetsBeforeBuild()
        {
            var sceneList = (SceneList)GetComponent(typeof(SceneList));
            var tempFile = WorkingDirectory+"/SceneSystem.k_SceneInfoFileName";
            ResourceCatalogBuildCode.WriteCatalogFile(sceneList, tempFile);
            AddFile(tempFile, $"{StreamingAssetsDirectory}/{SceneSystem.k_SceneInfoFileName}");
        }
    }
#endif
    
    static class ResourceCatalogBuildCode
    {
        public static void WriteCatalogFile(SceneList sceneList, string sceneInfoPath)
        {
            var sceneInfos = sceneList.GetSceneInfosForBuild();
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceCatalogData>();
            var metas = builder.Allocate(ref root.resources, sceneInfos.Length);
            for (int i = 0; i < sceneInfos.Length; i++)
            {
                metas[i] = new ResourceMetaData()
                {
                    ResourceId = sceneInfos[i].Scene.assetGUID,
                    ResourceFlags = sceneInfos[i].AutoLoad ? ResourceMetaData.Flags.AutoLoad : ResourceMetaData.Flags.None,
                    ResourceType = ResourceMetaData.Type.Scene,
                };
            }

            var strings = builder.Allocate(ref root.paths, sceneInfos.Length);
            for (int i = 0; i < sceneInfos.Length; i++)
                builder.AllocateString(ref strings[i], sceneInfos[i].Path);

            BlobAssetReference<ResourceCatalogData>.Write(builder, sceneInfoPath, ResourceCatalogData.CurrentFileFormatVersion);
            builder.Dispose();
        }
    }
}
