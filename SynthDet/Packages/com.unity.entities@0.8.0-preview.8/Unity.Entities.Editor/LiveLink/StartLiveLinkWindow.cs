using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Build;
using Unity.Build.Common;
using Unity.Build.Classic;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using BuildPipeline = Unity.Build.BuildPipeline;
using Unity.Scenes.Editor;

namespace Unity.Entities.Editor
{
    public class StartLiveLinkWindow : EditorWindow
    {
        internal const string kDefaultLiveLinkBuildPipelineAssetPath = "Packages/com.unity.entities/Unity.Entities.Hybrid/Assets/HybridLiveLink.buildpipeline";
        internal const string kWinLiveLinkBuildPipelineAssetPath = "Packages/com.unity.entities/Unity.Entities.Hybrid/Assets/WindowsHybridLiveLink.buildpipeline";

        static ProfilerMarker s_OpenStartLiveLinkWindowMarker = new ProfilerMarker(nameof(StartLiveLinkWindow) + "." + nameof(OpenWindow));

        StartLiveLinkView m_View;
        static bool s_IsWindowVisible;

        public static void OpenWindow()
        {
            using (s_OpenStartLiveLinkWindowMarker.Auto())
            {
                var wnd = GetWindow<StartLiveLinkWindow>(true, "Start Live Link", true);
                if (s_IsWindowVisible)
                    return;

                wnd.maxSize = new Vector2(800, 800);
                wnd.minSize = new Vector2(300, 255);
                wnd.Show();
                s_IsWindowVisible = true;
            }
        }

        void OnEnable()
        {
            m_View = new StartLiveLinkView();
            m_View.Start += OnStart;
            m_View.EditBuildConfiguration += OnEditBuildConfiguration;
            m_View.RevealBuildInFinder += OnRevealBuildInFinder;

            m_View.Initialize(rootVisualElement);
            ResetConfigurationListInView();

            BuildSettingsAssetPostProcessor.ScanAssetDatabaseForBuildConfigurations += ResetConfigurationListInView;
        }

        void OnStart(StartMode startMode, BuildConfigurationViewModel buildConfiguration)
        {
            switch (startMode)
            {
                case StartMode.Build:
                case StartMode.BuildAndRun:
                    m_View.SetEnableBuildingState(true);

                    EditorApplication.delayCall += () =>
                    {
                        var buildResult = buildConfiguration.Asset.Build();
                        buildResult.LogResult();
                        m_View.OnBuildFinished(buildConfiguration, buildResult.Succeeded);

                        m_View.SetEnableBuildingState(false);

                        if (startMode == StartMode.Build && buildResult.Succeeded)
                            OnRevealBuildInFinder(buildConfiguration);
                        else if (startMode == StartMode.BuildAndRun)
                            RunAndCloseWindowIfSuccessful(buildConfiguration);
                    };
                    break;
                case StartMode.RunLatestBuild:
                    RunAndCloseWindowIfSuccessful(buildConfiguration);
                    break;
            }
        }

        void RunAndCloseWindowIfSuccessful(BuildConfigurationViewModel buildConfiguration)
        {
            var r = buildConfiguration.Asset.Run();
            r.LogResult();
            if (r.Succeeded)
                Close();
        }

        void OnEditBuildConfiguration(BuildConfigurationViewModel buildConfiguration)
        {
            Selection.activeObject = buildConfiguration.Asset;
            EditorGUIUtility.PingObject(buildConfiguration.Asset);
        }

        void OnRevealBuildInFinder(BuildConfigurationViewModel buildConfiguration)
            => EditorUtility.RevealInFinder(buildConfiguration.OutputBuildDirectory);

        void OnDisable()
        {
            if (m_View == null)
                return;
            s_IsWindowVisible = false;

            m_View.Start -= OnStart;
            m_View.EditBuildConfiguration -= OnEditBuildConfiguration;
            m_View.RevealBuildInFinder -= OnRevealBuildInFinder;
            BuildSettingsAssetPostProcessor.ScanAssetDatabaseForBuildConfigurations -= ResetConfigurationListInView;
            m_View = null;
        }

        void ResetConfigurationListInView()
        {
            var configurations = AssetDatabase.FindAssets($"t:{typeof(BuildConfiguration).FullName}");
            m_View.ResetConfigurationList(configurations);
        }

        class BuildSettingsAssetPostProcessor : AssetPostprocessor
        {
            public static event Action ScanAssetDatabaseForBuildConfigurations = delegate { };

            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                foreach (var asset in importedAssets.Concat(deletedAssets))
                {
                    if (!asset.EndsWith(BuildConfiguration.AssetExtension, true, System.Globalization.CultureInfo.InvariantCulture))
                        continue;

                    ScanAssetDatabaseForBuildConfigurations();
                    return;
                }
            }
        }

        internal class StartLiveLinkView
        {
            const string kBasePath = "Packages/com.unity.entities/Editor/LiveLink";
            const string kNamePrefix = "start-live-link__";

            static readonly List<StartMode> sStartModes = new List<StartMode> { StartMode.RunLatestBuild, StartMode.BuildAndRun, StartMode.Build };

            readonly List<BuildConfigurationViewModel> m_BuildConfigurationViewModels = new List<BuildConfigurationViewModel>();
            readonly List<BuildConfigurationViewModel> m_FilteredBuildConfigurationViewModels = new List<BuildConfigurationViewModel>();
            readonly UIElementHelpers.VisualElementTemplate m_BuildConfigurationTemplate;

            VisualElement m_Root;
            ToolbarSearchField m_SearchField;
            VisualElement m_EmptyMessage;
            VisualElement m_ActionButtons;
            Button m_StartButton;
            VisualElement m_BuildMessage;
            PopupField<StartMode> m_StartModeDropdown;
            ListView m_ConfigurationsListView;

            BuildConfigurationViewModel m_LastSelectedConfiguration;
            BuildConfigurationViewModel m_SelectedConfiguration;

            VisualElement m_FooterMessage;
            bool m_DiscardSelectionChanged;
            Button m_NewBuildNameSubmit;

            public StartLiveLinkView()
            {
                m_BuildConfigurationTemplate = UIElementHelpers.LoadClonableTemplate(kBasePath, "StartLiveLinkWindow.ListViewItemTemplate");
            }

            public void Initialize(VisualElement rootVisualElement)
            {
                m_Root = UIElementHelpers.LoadTemplate(kBasePath, "StartLiveLinkWindow");
                m_Root.style.flexGrow = 1;

                m_SearchField = m_Root.Q<ToolbarSearchField>(kNamePrefix + "body__search");
                m_SearchField.RegisterValueChangedCallback(evt => FilterConfigurations());

                m_EmptyMessage = m_Root.Q<VisualElement>(kNamePrefix + "body__empty-message");

                m_ConfigurationsListView = m_Root.Q<ListView>(kNamePrefix + "body__configurations-list");
                m_ConfigurationsListView.makeItem = CreateBuildConfigurationItemVisualElement;
                m_ConfigurationsListView.bindItem = BindBuildConfigurationItem;
                m_ConfigurationsListView.itemsSource = m_FilteredBuildConfigurationViewModels;
                m_ConfigurationsListView.selectionType = SelectionType.Single;


#if UNITY_2020_1_OR_NEWER
                m_ConfigurationsListView.onSelectionChange += OnConfigurationListViewSelectionChange;
                m_ConfigurationsListView.onItemsChosen += chosenConfigurations => EditBuildConfiguration((BuildConfigurationViewModel) chosenConfigurations.First());
#else
                m_ConfigurationsListView.onSelectionChanged += OnConfigurationListViewSelectionChanged;
                m_ConfigurationsListView.onItemChosen += chosenConfiguration => EditBuildConfiguration((BuildConfigurationViewModel) chosenConfiguration);
#endif

                m_BuildMessage = m_Root.Q<VisualElement>(kNamePrefix + "build-message");
                m_BuildMessage.Hide();
                m_ActionButtons = m_Root.Q<VisualElement>(kNamePrefix + "footer");
                m_StartModeDropdown = new PopupField<StartMode>(sStartModes, StartMode.RunLatestBuild, FormatStartMode, FormatStartMode) { style = { flexGrow = 1 } };
                m_StartModeDropdown.RegisterValueChangedCallback(OnSelectedStartModeChanged);
                m_ActionButtons.Q<VisualElement>(kNamePrefix + "footer__build-mode-container").Add(m_StartModeDropdown);

                m_StartButton = m_ActionButtons.Q<Button>(kNamePrefix + "footer__start-button");
                m_StartButton.clicked += () =>
                {
                    m_FooterMessage.Hide();
                    Start(m_StartModeDropdown.value, m_SelectedConfiguration);
                };

                m_FooterMessage = m_Root.Q<VisualElement>(kNamePrefix + "message");
                m_FooterMessage.Hide();

                rootVisualElement.Add(m_Root);
            }

            public event Action<StartMode, BuildConfigurationViewModel> Start = delegate { };
            public event Action<BuildConfigurationViewModel> EditBuildConfiguration = delegate { };
            public event Action<BuildConfigurationViewModel> RevealBuildInFinder = delegate { };

            public void ResetConfigurationList(IEnumerable<string> configurationAssetGuids)
            {
                m_BuildConfigurationViewModels.Clear();
                m_BuildConfigurationViewModels.AddRange(configurationAssetGuids.Select(a => new BuildConfigurationViewModel(a))
                                                            .OrderBy(b => b.Name, StringComparer.Ordinal));

                if (m_BuildConfigurationViewModels.Count == 0)
                {
                    m_ConfigurationsListView.selectedIndex = -1;
                    m_ConfigurationsListView.Hide();
                    m_EmptyMessage.Show();
                }
                else
                {
                    m_ConfigurationsListView.Show();
                    m_EmptyMessage.Hide();
                }

                FilterConfigurations();
            }

            public void SetEnableBuildingState(bool isBuilding)
            {
                var body = m_Root.Q<VisualElement>(kNamePrefix + "body");
                body.SetEnabled(!isBuilding);
                m_ActionButtons.ToggleVisibility(!isBuilding);
                m_BuildMessage.ToggleVisibility(isBuilding);
            }

            public void OnBuildFinished(BuildConfigurationViewModel buildConfigurationViewModel, bool isSuccessful)
            {
                if (!isSuccessful)
                {
                    m_FooterMessage.Q<Label>().text = $"Build failed for {buildConfigurationViewModel.Name}, see Console for details";
                    m_FooterMessage.Show();
                }
                else
                {
                    m_FooterMessage.Hide();
                }

                if(buildConfigurationViewModel == m_SelectedConfiguration)
                    UpdateActionButtonsState();
            }

            internal void FilterConfigurations()
            {
                var searchTerm = m_SearchField.value.TrimEnd();
                m_DiscardSelectionChanged = true;
                m_FilteredBuildConfigurationViewModels.Clear();
                m_FilteredBuildConfigurationViewModels.AddRange(string.IsNullOrEmpty(searchTerm) ? m_BuildConfigurationViewModels : m_BuildConfigurationViewModels.Where(b => b.MatchFilter(searchTerm)));

                m_ConfigurationsListView.Refresh();

                if (m_SelectedConfiguration != null)
                    m_ConfigurationsListView.selectedIndex = m_FilteredBuildConfigurationViewModels.IndexOf(m_SelectedConfiguration);

                m_DiscardSelectionChanged = false;
                UpdateActionButtonsState();
            }

#if UNITY_2020_1_OR_NEWER
            void OnConfigurationListViewSelectionChange(IEnumerable<object> obj)
#else
            void OnConfigurationListViewSelectionChanged(List<object> obj)
#endif
            {
                if (m_DiscardSelectionChanged)
                    return;

                m_SelectedConfiguration = (BuildConfigurationViewModel) obj.FirstOrDefault();
                m_StartModeDropdown.index = m_SelectedConfiguration != null ? sStartModes.IndexOf(m_SelectedConfiguration.SelectedStartMode) : 0;

                UpdateActionButtonsState();
            }

            void UpdateActionButtonsState()
            {
                var isPanelEnabled = m_SelectedConfiguration != null
                                    && m_FilteredBuildConfigurationViewModels.Contains(m_SelectedConfiguration)
                                    && m_SelectedConfiguration.IsLiveLinkCompatible;

                if (isPanelEnabled)
                {
                    m_ActionButtons.SetEnabled(true);
                    var isEnabled = m_SelectedConfiguration.IsActionAllowed(m_StartModeDropdown.value, out var reason);
                    m_StartButton.SetEnabled(isEnabled);
                    m_StartButton.parent.tooltip = isEnabled ? string.Empty : reason;
                }
                else
                    m_ActionButtons.SetEnabled(false);
            }

            static string FormatStartMode(StartMode mode)
            {
                switch (mode)
                {
                    case StartMode.RunLatestBuild:
                        return "Run latest build";
                    case StartMode.BuildAndRun:
                        return "Build and run";
                    case StartMode.Build:
                        return "Build";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }

            void OnSelectedStartModeChanged(ChangeEvent<StartMode> evt)
            {
                m_StartButton.text = evt.newValue == StartMode.Build ? "Start Build" : "Start Live Link";

                if (m_SelectedConfiguration != null)
                    m_SelectedConfiguration.SelectedStartMode = evt.newValue;

                UpdateActionButtonsState();
            }

            VisualElement CreateBuildConfigurationItemVisualElement()
            {
                var e = m_BuildConfigurationTemplate.GetNewInstance();
                e.AddManipulator(new ContextualMenuManipulator(null));
                e.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                {
                    var configurationViewModel = (BuildConfigurationViewModel) ((VisualElement) evt.target).userData;
                    evt.menu.AppendAction("Edit Build Configuration", a => EditBuildConfiguration(configurationViewModel));
                    evt.menu.AppendAction(GetRevealInFinderMenuTitle(), a => RevealBuildInFinder(configurationViewModel), configurationViewModel.HasALatestBuild ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                });

                return e;
            }

            static string GetRevealInFinderMenuTitle()
                => Application.platform == RuntimePlatform.WindowsEditor ? "Show latest build in Explorer" : "Reveal latest build in Finder";

            void BindBuildConfigurationItem(VisualElement visualElement, int index)
            {
                var configurationViewModel = m_FilteredBuildConfigurationViewModels[index];
                visualElement.userData = configurationViewModel;

                visualElement.EnableInClassList("unity-disabled", !configurationViewModel.IsLiveLinkCompatible);
                visualElement.tooltip = !configurationViewModel.IsLiveLinkCompatible ? "This Build Configuration is not compatible with Live Link. Please edit it to use a compatible build pipeline." : string.Empty;

                var icon = visualElement.Q<Image>(className: kNamePrefix + "item-template__device-icon");
                icon.ClearClassList();
                icon.AddToClassList(kNamePrefix + "item-template__device-icon");
                icon.AddToClassList(GetUssClass(configurationViewModel.Target));
                icon.tooltip = $"Target: {configurationViewModel.Target}";

                var errorIcon = visualElement.Q<Image>(className: kNamePrefix + "item-template__error-icon");
                var canBuild = configurationViewModel.Asset.CanBuild(out var reason);
                errorIcon.ToggleVisibility(!canBuild);
                errorIcon.tooltip = canBuild ? string.Empty : $"This Build Configuration as an error preventing it from being built: {Environment.NewLine}{StripAnyHtmlTag(reason)}";

                visualElement.Q<Label>().text = configurationViewModel.Name;
            }

            static string GetUssClass(BuildTarget buildTarget)
            {
                switch (buildTarget)
                {
                    case BuildTarget.NoTarget:
                        return "start-live-link__item-template__icon-noTarget";
                    case BuildTarget.StandaloneWindows64:
                    case BuildTarget.WSAPlayer:
                    case BuildTarget.StandaloneWindows:
                        return "start-live-link__item-template__icon-windows";
                    case BuildTarget.StandaloneLinux64:
                    case BuildTarget.StandaloneOSX:
                        return "start-live-link__item-template__icon-standalone";
                    case BuildTarget.XboxOne:
                        return "start-live-link__item-template__icon-xboxOne";
                    case BuildTarget.iOS:
                        return "start-live-link__item-template__icon-iOS";
                    case BuildTarget.Android:
                        return "start-live-link__item-template__icon-android";
                    case BuildTarget.WebGL:
                        return "start-live-link__item-template__icon-webGL";
                    case BuildTarget.PS4:
                        return "start-live-link__item-template__icon-ps4";
                    case BuildTarget.tvOS:
                        return "start-live-link__item-template__icon-tvOS";
                    case BuildTarget.Switch:
                        return "start-live-link__item-template__icon-switch";
                    case BuildTarget.Lumin:
                        return "start-live-link__item-template__icon-lumin";
                    case BuildTarget.Stadia:
                        return "start-live-link__item-template__icon-stadia";
                }

                return null;
            }

            static string StripAnyHtmlTag(string input) => Regex.Replace(input, "<.*?>", string.Empty);
        }

        internal enum StartMode
        {
            RunLatestBuild,
            BuildAndRun,
            Build
        }

        internal class BuildConfigurationViewModel
        {
            public readonly string AssetGuid;

            public readonly string Name;
            public readonly BuildConfiguration Asset;
            public readonly BuildTarget Target;
            public readonly string OutputBuildDirectory;
            public readonly bool IsLiveLinkCompatible;
            public StartMode SelectedStartMode;

            public BuildConfigurationViewModel(string assetGuid)
            {
                AssetGuid = assetGuid;
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                Name = Path.GetFileNameWithoutExtension(assetPath);

                Asset = BuildConfiguration.LoadAsset(assetPath);
                Asset.OnEnable();

                Target = Asset.TryGetComponent(out ClassicBuildProfile classicBuildProfile) ? classicBuildProfile.Target : BuildTarget.NoTarget;
                OutputBuildDirectory = Asset.GetOutputBuildDirectory();

                IsLiveLinkCompatible = DetermineLivelinkCompatibility();

                SelectedStartMode = IsActionAllowed(StartMode.RunLatestBuild, out _) ? StartMode.RunLatestBuild : StartMode.BuildAndRun;
            }

            public bool DetermineLivelinkCompatibility()
            {
#if !UNITY_BUILD_CLASS_BASED_PIPELINES
                IEnumerable<BuildStep> EnumerateBuildSteps(BuildPipeline buildPipeline)
                {
                    foreach (var step in buildPipeline.BuildSteps)
                    {
                        if (step is BuildPipeline subPipeline)
                        {
                            foreach (var nestedStep in EnumerateBuildSteps(subPipeline))
                            {
                                yield return nestedStep;
                            }
                        }
                        else
                        {
                            yield return step as BuildStep;
                        }
                    }
                }

                var pipeline = Asset.GetBuildPipeline();
                if (pipeline != null)
                {
                    pipeline.OnEnable();
                    return EnumerateBuildSteps(pipeline).Any(s => s is BuildStepBuildClassicLiveLink);
                }
                else
                    return false;
#else
                return true;
#endif                
            }

            public bool HasALatestBuild => OutputBuildDirectory != null && Directory.Exists(OutputBuildDirectory);

            public bool MatchFilter(string searchTerm) => Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0;

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return AssetGuid == ((BuildConfigurationViewModel) obj).AssetGuid;
            }

            public override int GetHashCode() => AssetGuid != null ? AssetGuid.GetHashCode() : 0;

            public bool IsActionAllowed(StartMode mode, out string reason)
            {
                reason = string.Empty;

                switch (mode)
                {
                    case StartMode.RunLatestBuild:
                        if (!HasALatestBuild)
                        {
                            reason = "No previous build has been found for this configuration.";
                            return false;
                        }
                        else if (!Asset.CanRun(out var canRunReason))
                        {
                            reason = $"Impossible to run latest build: {canRunReason}";
                            return false;
                        }
                        return true;
                    case StartMode.BuildAndRun:
                    case StartMode.Build:
                        if (Asset.CanBuild(out var canBuildReason))
                            return true;

                        reason = $"Impossible to build this configuration: {canBuildReason}";
                        return false;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }
        }
    }
}