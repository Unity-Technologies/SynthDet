using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.Build.Internals;
using Unity.Properties;
using UnityEditor;

#if !UNITY_BUILD_CLASS_BASED_PIPELINES
namespace Unity.Scenes.Editor
{
    [BuildStep(Name = "Build LiveLink Player", Description = "Build LiveLink Player", Category = "Classic")]
    [FormerlySerializedAs("Unity.Build.Common.BuildStepBuildClassicLiveLink, Unity.Build.Common")]
    sealed class BuildStepBuildClassicLiveLink : BuildStep
    {
        const string k_Description = "Build LiveLink Player";

        const string k_BootstrapPath = "Assets/StreamingAssets/" + LiveLinkRuntimeSystemGroup.k_BootstrapFileName;
        const string k_EmptyScenePath = "Packages/com.unity.entities/Unity.Scenes.Editor/LiveLink/Assets/empty.unity";

        TemporaryFileTracker m_TempFileTracker;

        public override Type[] RequiredComponents => new[]
        {
            typeof(ClassicBuildProfile),
            typeof(SceneList),
            typeof(GeneralSettings)
        };

        public override Type[] OptionalComponents => new[]
        {
            typeof(OutputBuildDirectory),
            typeof(SourceBuildConfiguration)
        };

        private bool UseAutoRunPlayer(BuildContext context)
        {
            var pipeline = GetRequiredComponent<ClassicBuildProfile>(context).Pipeline;
            var runStep = pipeline.RunStep;

            // RunStep is provided no need to use AutoRunPlayer
            if (runStep != null && !runStep.GetType().Name.Contains("RunStepNotImplemented"))
                return false;

            // See dots\Samples\Library\PackageCache\com.unity.build@0.1.0-preview.1\Editor\Unity.Build\BuildSettingsScriptedImporterEditor.cs
            const string k_CurrentActionKey = "BuildAction-CurrentAction";
            if (!EditorPrefs.HasKey(k_CurrentActionKey))
                return false;

            var value = EditorPrefs.GetInt(k_CurrentActionKey);
            return value == 1;
        }

        public override BuildStepResult RunBuildStep(BuildContext context)
        {
            m_TempFileTracker = new TemporaryFileTracker();
            var generalSettings = GetRequiredComponent<GeneralSettings>(context);
            var profile = GetRequiredComponent<ClassicBuildProfile>(context);
            var sceneList = GetRequiredComponent<SceneList>(context);

            if (profile.Target <= 0)
                return BuildStepResult.Failure(this, $"Invalid build target '{profile.Target.ToString()}'.");

            if (profile.Target != EditorUserBuildSettings.activeBuildTarget)
                return BuildStepResult.Failure(this, $"{nameof(EditorUserBuildSettings.activeBuildTarget)} must be switched before {nameof(BuildStepBuildClassicLiveLink)} step.");

            //any initial scenes that cannot be live linked must be added to the scenes list
            var embeddedScenes = new List<string>(sceneList.GetScenePathsToLoad().Where(path => !SceneImporterData.CanLiveLinkScene(path)));

            //if none of the startup scenes are embedded, add empty scene
            if (embeddedScenes.Count == 0)
                embeddedScenes.Add(k_EmptyScenePath);

            //add any additional scenes that cannot be live linked
            foreach (var path in sceneList.GetScenePathsForBuild())
            {
                if (!SceneImporterData.CanLiveLinkScene(path) && !embeddedScenes.Contains(path))
                    embeddedScenes.Add(path);
            }

            var outputPath = this.GetOutputBuildDirectory(context);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = embeddedScenes.ToArray(),
                target = profile.Target,
                locationPathName = Path.Combine(outputPath, generalSettings.ProductName + profile.GetExecutableExtension()),
                targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(profile.Target),
                options = BuildOptions.Development | BuildOptions.ConnectToHost
            };

            var sourceBuild = GetOptionalComponent<SourceBuildConfiguration>(context);
            if (sourceBuild.Enabled)
                buildPlayerOptions.options |= BuildOptions.InstallInBuildFolder;

            if (profile.Configuration == BuildType.Debug)
                buildPlayerOptions.options |= BuildOptions.AllowDebugging;

            if (UseAutoRunPlayer(context))
            {
                UnityEngine.Debug.Log($"Using BuildOptions.AutoRunPlayer, since RunStep is not provided for {profile.Target}");
                buildPlayerOptions.options |= BuildOptions.AutoRunPlayer;
            }

            var settings = BuildContextInternals.GetBuildConfiguration(context);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(settings, out var guid, out long _))
            {
                using (var stream = new StreamWriter(m_TempFileTracker.TrackFile(k_BootstrapPath)))
                {
                    stream.WriteLine(guid);
                    stream.WriteLine(EditorAnalyticsSessionInfo.id);
                }
            }

            var report = UnityEditor.BuildPipeline.BuildPlayer(buildPlayerOptions);
            var result = new BuildStepResult(this, report);
            context.SetValue(report);
            return result;
        }

        public override BuildStepResult CleanupBuildStep(BuildContext context)
        {
            m_TempFileTracker.Dispose();
            return Success();
        }
    }
}
#endif