using System;
using System.IO;
using NUnit.Framework;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    
    [TestFixture]
    class StartLiveLinkWindowTests
    {
        string m_TestDirectoryPath;
        VisualElement m_Container;
        StartLiveLinkWindow.StartLiveLinkView m_View;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_TestDirectoryPath = "Assets/" + Path.GetRandomFileName();
            Directory.CreateDirectory(m_TestDirectoryPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(m_TestDirectoryPath);
        }

        [SetUp]
        public void Setup()
        {
            m_Container = new VisualElement();
            m_View = new StartLiveLinkWindow.StartLiveLinkView();
            m_View.Initialize(m_Container);
        }

        [Test]
        public void ShowEmptyMessage_WhenNoBuildConfigurationAvailable()
        {
            m_View.ResetConfigurationList(Array.Empty<string>());

            Assert.That(m_Container.GetItem<ListView>().style.display.value, Is.EqualTo(DisplayStyle.None));
            var emptyMessage = m_Container.GetItem("start-live-link__body__empty-message");
            Assert.That(emptyMessage.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(emptyMessage.Q<Label>().text, Is.EqualTo("Create a new Build Configuration to get started with Live Link"));
        }

        [Test]
        public void ConfigurationListView_ShowGivenConfigurations()
        {
            m_View.ResetConfigurationList(new[] { CreateBuildConfiguration() });

            Assert.That(m_Container.GetItem<ListView>().style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_Container.GetItem<ListView>().itemsSource.Count, Is.EqualTo(1));
            Assert.That(m_Container.GetItem("start-live-link__body__empty-message").style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ConfigurationListView_Filter()
        {
            var listView = m_Container.GetItem<ListView>();
            m_View.ResetConfigurationList(new[] { CreateBuildConfiguration("Win-Config"), CreateBuildConfiguration("Android-Config") });

            m_Container.GetItem<ToolbarSearchField>().value = "win";
            m_View.FilterConfigurations();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(1));
            Assert.That(((StartLiveLinkWindow.BuildConfigurationViewModel)listView.itemsSource[0]).Name, Is.EqualTo("Win-Config"));

            m_Container.GetItem<ToolbarSearchField>().value = "andr";
            m_View.FilterConfigurations();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(1));
            Assert.That(((StartLiveLinkWindow.BuildConfigurationViewModel)listView.itemsSource[0]).Name, Is.EqualTo("Android-Config"));

            m_Container.GetItem<ToolbarSearchField>().value = "conf";
            m_View.FilterConfigurations();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(2));

            m_Container.GetItem<ToolbarSearchField>().value = "";
            m_View.FilterConfigurations();
            Assert.That(listView.itemsSource.Count, Is.EqualTo(2));
        }

#if !UNITY_BUILD_CLASS_BASED_PIPELINES
        [Test]
        public void ConfigurationViewModel_DetectWhenNotLiveLinkCompatible()
        {
            var liveLinkPipeline = Build.BuildPipeline.LoadAsset(StartLiveLinkWindow.kDefaultLiveLinkBuildPipelineAssetPath);
            var liveLinkMetaPipeline = Build.BuildPipeline.LoadAsset(StartLiveLinkWindow.kWinLiveLinkBuildPipelineAssetPath);
            var nonLiveLinkPipeline = Build.BuildPipeline.LoadAsset("Packages/com.unity.entities/Unity.Entities.Hybrid/Assets/Hybrid.buildpipeline");
            var liveLink = new StartLiveLinkWindow.BuildConfigurationViewModel(CreateBuildConfiguration("LiveLink", configuration =>
            {
                configuration.SetComponent(new GeneralSettings());
                configuration.SetComponent(new SceneList { BuildCurrentScene = true });
                configuration.SetComponent(new ClassicBuildProfile { Pipeline = liveLinkPipeline, Target = BuildTarget.Android });
                configuration.SetComponent(new OutputBuildDirectory { OutputDirectory = "Builds" });
                configuration.SetComponent(new ClassicScriptingSettings());
            }));
            var metaLiveLink = new StartLiveLinkWindow.BuildConfigurationViewModel(CreateBuildConfiguration("LiveLink", configuration =>
            {
                configuration.SetComponent(new GeneralSettings());
                configuration.SetComponent(new SceneList { BuildCurrentScene = true });
                configuration.SetComponent(new ClassicBuildProfile { Pipeline = liveLinkMetaPipeline, Target = BuildTarget.Android });
                configuration.SetComponent(new OutputBuildDirectory { OutputDirectory = "Builds" });
                configuration.SetComponent(new ClassicScriptingSettings());
            }));
            var nonLiveLink = new StartLiveLinkWindow.BuildConfigurationViewModel(CreateBuildConfiguration("NonLiveLink", configuration =>
            {
                configuration.SetComponent(new GeneralSettings());
                configuration.SetComponent(new SceneList { BuildCurrentScene = true });
                configuration.SetComponent(new ClassicBuildProfile { Pipeline = nonLiveLinkPipeline, Target = BuildTarget.Android });
                configuration.SetComponent(new OutputBuildDirectory { OutputDirectory = "Builds" });
                configuration.SetComponent(new ClassicScriptingSettings());
            }));

            Assert.That(liveLink.IsLiveLinkCompatible, Is.True);
            Assert.That(metaLiveLink.IsLiveLinkCompatible, Is.True);
            Assert.That(nonLiveLink.IsLiveLinkCompatible, Is.False);
        }

        [Test]
        public void ConfigurationViewModel_CheckIfActionIsAllowed()
        {
            var viewModel = new StartLiveLinkWindow.BuildConfigurationViewModel(CreateBuildConfiguration("Config", configuration =>
            {
                configuration.SetComponent(new GeneralSettings());
                configuration.SetComponent(new SceneList { BuildCurrentScene = true });
                configuration.SetComponent(new ClassicBuildProfile { Pipeline = Build.BuildPipeline.LoadAsset(StartLiveLinkWindow.kDefaultLiveLinkBuildPipelineAssetPath), Target = BuildTarget.Android });
                configuration.SetComponent(new OutputBuildDirectory { OutputDirectory = "TestConfigurationBuilds" });
                configuration.SetComponent(new ClassicScriptingSettings());
            }));

            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.Build, out _), Is.True);
            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.BuildAndRun, out _), Is.True);
            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.RunLatestBuild, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("No previous build has been found for this configuration."));

            viewModel = new StartLiveLinkWindow.BuildConfigurationViewModel(CreateBuildConfiguration("Config", configuration =>
            {
                configuration.SetComponent(new GeneralSettings());
                configuration.SetComponent(new SceneList { BuildCurrentScene = true });
            }));

            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.Build, out _), Is.False);
            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.BuildAndRun, out _), Is.False);
            Assert.That(viewModel.IsActionAllowed(StartLiveLinkWindow.StartMode.RunLatestBuild, out _), Is.False);
        }
#endif
        
        string CreateBuildConfiguration(string assetName = "TestConfiguration", Action<BuildConfiguration> mutator = null)
        {
            var cfg = BuildConfiguration.CreateAsset(Path.Combine(m_TestDirectoryPath, assetName + BuildConfiguration.AssetExtension), mutator);
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(cfg));
        }
    }
}