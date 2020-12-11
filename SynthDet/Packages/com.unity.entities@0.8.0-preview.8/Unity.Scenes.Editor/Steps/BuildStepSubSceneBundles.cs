using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Platforms;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.Build.Internals;
using UnityEditor;
using UnityEngine;
using BuildContext = Unity.Build.BuildContext;


namespace Unity.Scenes.Editor
{
#if !UNITY_BUILD_CLASS_BASED_PIPELINES
    [BuildStep(Name = "Build SubScene Bundles", Description = "Building SubScene Bundles", Category = "Hybrid")]
    sealed class BuildStepSubSceneBundles : BuildStep
    {
        TemporaryFileTracker m_TemporaryFileTracker;

        public override Type[] RequiredComponents => new[]
        {
            typeof(ClassicBuildProfile),
            typeof(SceneList)
        };
        
        public override BuildStepResult RunBuildStep(BuildContext context)
        {
            m_TemporaryFileTracker = new TemporaryFileTracker();
            
            // Delete SubScenes build folder defensively (Eg. if unity crashes during build)
            var streamingAssetsSubscenes = "Assets/StreamingAssets/SubScenes";
            FileUtil.DeleteFileOrDirectory(streamingAssetsSubscenes);
            
            m_TemporaryFileTracker.CreateDirectory(streamingAssetsSubscenes);

            List<(string sourceFile, string destinationFile)> filesToCopy = new List<(string sourceFile, string destinationFile)>();

            void RegisterFileCopy(string sourceFile, string destinationFile)
            {
                filesToCopy.Add((sourceFile,destinationFile));
            }
            
            SubSceneBuildCode.PrepareAdditionalFiles( BuildContextInternals.GetBuildConfigurationGUID(context), type=>GetRequiredComponent(context,type), RegisterFileCopy, Application.streamingAssetsPath, $"Library/SubsceneBundles");
            
            foreach (var (sourceFile, targetFile) in filesToCopy)
            {
                m_TemporaryFileTracker.TrackFile(targetFile);
                File.Copy(sourceFile, targetFile, true);
            }

            return Success();
        }

        public override BuildStepResult CleanupBuildStep(BuildContext context)
        {
            m_TemporaryFileTracker.Dispose();
            return Success();
        }
    }
#else
    class SubsceneFilesProvider : AdditionalFilesProviderBase
    {
        public override Type[] UsedComponents { get; } = {typeof(SceneList), typeof(ClassicBuildProfile)};

        protected override void OnPrepareAdditionalAssetsBeforeBuild()
        {
            SubSceneBuildCode.PrepareAdditionalFiles(BuildConfigurationGuid, GetComponent, AddFile, StreamingAssetsDirectory, $"Library/SubsceneBundles");
        }
    }
#endif
}