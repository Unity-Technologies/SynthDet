using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Simulation.Client;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using UnityEditor.Build.Reporting;
using ZipUtility;
using Random = UnityEngine.Random;

public class AppParamsGeneratorWindow : EditorWindow
{
    [MenuItem("Window/Run in USim...")]
    static void ShowWindow()
    {
        var window = GetWindow<AppParamsGeneratorWindow>();
        window.titleContent = new GUIContent("Run in Unity Simulation");
        window.Show();
    }

    Toggle m_UseExistingBuildToggle;
    TextField m_ExistingBuildId;
    TextField m_NameField;

    PopupField<SysParamOption> m_SysParamPopup;
    IntegerField m_StepsField;
    IntegerField m_StepsPerJobField;
    CurveField m_ScaleFactorCurve;
    IntegerField m_SeedField;
    IntegerField m_MaxFramesField;
    IntegerField m_MaxForegroundObjectsPerFrame;
    IntegerField m_BackgroundObjectDensityField;
    IntegerField m_NumBackgroundFillPassesField;
    TextElement m_EstimateElement;

    // ADR parameters
    FloatField m_ScalingMinField;
    FloatField m_ScalingSizeField;
    FloatField m_LightColorMinField;
    FloatField m_LightRotationMaxField;
    FloatField m_BackgroundHueMaxOffsetField;
    FloatField m_OccludingHueMaxOffsetField;
    FloatField m_BackgroundInForegroundChanceField;

    FloatField m_NoiseStrengthMaxField;
    FloatField m_BlurKernelSizeMaxField;
    FloatField m_BlurStandardDeviationMaxField;

    class SysParamOption
    {
        public string id;
        public string description;

        public override string ToString() => description;
    };
    
    public void OnEnable()
    {
        Project.clientReadyStateChanged += OnClientReadyStateChanged;
        Project.Activate();
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        RefreshUI();
    }

    public void OnDestroy()
    {
        Project.projectIdChanged -= OnClientReadyStateChanged;
        if (m_CancellationTokenSource != null)
        {
            m_CancellationTokenSource.Cancel();
            EditorUtility.ClearProgressBar();
        }
    }

    void RefreshUI()
    {
        rootVisualElement.Clear();
        if (Project.projectIdState == Project.State.Pending)
        {
            rootVisualElement.Add(new TextElement()
            {
                text = "Waiting for connection to Unity Cloud."
            });
            return;
        }
        else if (Project.projectIdState == Project.State.Invalid)
        {
            rootVisualElement.Add(new TextElement()
            {
                text = "Project must be associated with a valid Unity Cloud project to run in Unity Simulation."
            });
            return;
        }

        m_NameField = new TextField("Run Name")
        {
            viewDataKey = "Run name",
        };
        rootVisualElement.Add(m_NameField);

        m_UseExistingBuildToggle = new Toggle("Use Existing Build")
        {
            viewDataKey = "Use Existing Build"
        };
        m_UseExistingBuildToggle.RegisterValueChangedCallback(e =>
        {
            m_ExistingBuildId.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });
        rootVisualElement.Add(m_UseExistingBuildToggle);

        m_ExistingBuildId = new TextField("Build ID")
        {
            viewDataKey = "Build ID"
        };
        m_ExistingBuildId.style.display = DisplayStyle.None;
        rootVisualElement.Add(m_ExistingBuildId);
        
        m_ScaleFactorCurve = new CurveField("Scale Factor Range")
        {
            viewDataKey = "Scale factor range",
        };
        m_ScaleFactorCurve.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_ScaleFactorCurve);
        m_StepsField = new IntegerField("Scale Factor Steps")
        {
            viewDataKey = "Scale factor steps",
            maxLength = 4
        };
        m_StepsField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_StepsField);
        m_StepsPerJobField = new IntegerField("Steps Per Instance")
        {
            viewDataKey = "Steps per instance",
            maxLength = 3
        };
        m_StepsPerJobField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_StepsPerJobField);
        m_MaxFramesField = new IntegerField("Max Frames Per Instance")
        {
            viewDataKey = "Max frames"
        };
        m_SeedField = new IntegerField("Random Seed")
        {
            viewDataKey = "Seed"
        };
        rootVisualElement.Add(m_SeedField);
        m_MaxFramesField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_MaxFramesField);

        m_MaxForegroundObjectsPerFrame = new IntegerField("Max Objects Per Frame")
        {
            maxLength = 3,
            tooltip = "Max foreground objects per frame",
            viewDataKey = "Max objects per frame"
        };
        m_MaxForegroundObjectsPerFrame.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_MaxForegroundObjectsPerFrame);

        m_BackgroundObjectDensityField = new IntegerField("Background Object Density")
        {
            viewDataKey = "Background object density",
            tooltip = "Number of objects per square meter to distribute in a background fill pass."
        };
        rootVisualElement.Add(m_BackgroundObjectDensityField);
        m_NumBackgroundFillPassesField = new IntegerField("Num Background Passes")
        {
            viewDataKey = "Num background passes",
            tooltip =
                "Number of times the background generator will generate a collection of objects to fill background",
        };
        rootVisualElement.Add(m_NumBackgroundFillPassesField);
        
        // ADR parameters
        m_ScalingMinField = new FloatField("Occluding Object Scaling Range Minimum")
        {
            viewDataKey = "Occluding object scaling range minimum"
        };
        rootVisualElement.Add(m_ScalingMinField);
        m_ScalingSizeField = new FloatField("Occluding Object Scaling Range Size")
        {
            viewDataKey = "Occluding object scaling range size"
        };
        rootVisualElement.Add(m_ScalingSizeField);
        m_LightColorMinField = new FloatField("Light Color Minimum")
        {
            viewDataKey = "Light color minimum"
        };
        rootVisualElement.Add(m_LightColorMinField);
        m_LightRotationMaxField = new FloatField("Light Rotation Maximum")
        {
            viewDataKey = "Light rotation maximum"
        };
        rootVisualElement.Add(m_LightRotationMaxField);
        m_BackgroundHueMaxOffsetField = new FloatField("Background Hue Maximum Offset")
        {
            viewDataKey = "Background hue maximum offset"
        };
        rootVisualElement.Add(m_BackgroundHueMaxOffsetField);
        m_OccludingHueMaxOffsetField = new FloatField("Occluding Hue Maximum Offset")
        {
            viewDataKey = "Occluding hue maximum offset"
        };
        rootVisualElement.Add(m_OccludingHueMaxOffsetField);
        m_BackgroundInForegroundChanceField = new FloatField("Background In Foreground Chance")
        {
            viewDataKey = "Background In Foreground Chance"
        };
        m_BackgroundInForegroundChanceField.RegisterValueChangedCallback(a => UpdateEstimate());
        rootVisualElement.Add(m_BackgroundInForegroundChanceField);
        m_NoiseStrengthMaxField = new FloatField("White Noise Maximum Strength")
        {
            viewDataKey = "White noise maximum strength",
            tooltip = "Maximum limit for range within which the white noise can be randomized per frame."
        };
        rootVisualElement.Add(m_NoiseStrengthMaxField);
        m_BlurKernelSizeMaxField = new FloatField("Blur Kernel Maximum Size")
        {
            viewDataKey = "Blur kernel maximum size",
            tooltip = "Maximum size in uv coordinates the gaussian blur kernel will be computed over for a given frag."
        };
        rootVisualElement.Add(m_BlurKernelSizeMaxField);
        m_BlurStandardDeviationMaxField = new FloatField("Blur Standard Deviation Maximum")
        {
            viewDataKey = "Blur standard deviation maximum",
            tooltip = "Maximum limit for the percentage of the kernel size which represents one standard deviation's " +
                "distance from the center of the kernel."
        };
        rootVisualElement.Add(m_BlurStandardDeviationMaxField);

        rootVisualElement.Add(new Button(SetDefaultFieldValues){ text = "Reset to Defaults"});
        m_EstimateElement = new TextElement();
        rootVisualElement.Add(m_EstimateElement);
        SetDefaultFieldValues();

        var sysParamDefinitions = API.GetSysParams();

        var sysParamOptions = sysParamDefinitions.Where(s => s.allowed)
            .Select(s => new SysParamOption()
            {
                id = s.id,
                description = s.description
            }).ToList();
        m_SysParamPopup = new PopupField<SysParamOption>("SysParam", sysParamOptions, sysParamOptions[0])
        {
            viewDataKey = "SysParam"
        };
        rootVisualElement.Add(m_SysParamPopup);
        rootVisualElement.Add(new Button(OnBeginRunButtonClicked) { text = "Execute on Unity Simulation" });
    }
    
    void OnClientReadyStateChanged(Project.State state)
    {
        RefreshUI();
    }

    void OnBeforeAssemblyReload()
    {
        if (m_ExecuteTask != null)
            EditorUtility.ClearProgressBar();
    }

    void SetDefaultFieldValues()
    {
        m_ScaleFactorCurve.value = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
        //The number required to get approx. 400k frames
        m_StepsField.value = 377;
        m_StepsPerJobField.value = 1;
        
        var defaults = ProjectInitialization.AppParamDefaults;
        m_SeedField.value = (int)(Random.value * int.MaxValue);
        m_MaxFramesField.value = defaults.MaxFrames;
        m_MaxForegroundObjectsPerFrame.value = defaults.MaxForegroundObjectsPerFrame;
        m_BackgroundObjectDensityField.value = defaults.BackgroundObjectDensity;
        m_NumBackgroundFillPassesField.value = defaults.NumBackgroundFillPasses;
        m_ScalingMinField.value = defaults.ScalingMin;
        m_ScalingSizeField.value = defaults.ScalingSize;
        m_LightColorMinField.value = defaults.LightColorMin;
        m_LightRotationMaxField.value = defaults.LightRotationMax;
        m_BackgroundHueMaxOffsetField.value = defaults.BackgroundHueMaxOffset;
        m_OccludingHueMaxOffsetField.value = defaults.OccludingHueMaxOffset;
        m_BackgroundInForegroundChanceField.value = defaults.BackgroundObjectInForegroundChance;
        m_NoiseStrengthMaxField.value = defaults.NoiseStrengthMax;
        m_BlurKernelSizeMaxField.value = defaults.BlurKernelSizeMax;
        m_BlurStandardDeviationMaxField.value = defaults.BlurStandardDeviationMax;
        UpdateEstimate();
    }

    void UpdateEstimate()
    {
        var estimateTotalFrames = EstimateTotalFrames();
        
        m_EstimateElement.text = $"Estimated total frames: {(estimateTotalFrames == 0 ? "--" : estimateTotalFrames.ToString())}";
    }

    // A hand-tuned value that approximately corresponds to the frames per scale factor per orientation with 64 foreground objects.
    const float k_EstimatedFramesPerCurriculumStepAtOneScale = 4.2f;
    
    int EstimateTotalFrames()
    {
        // var scaleFactorRange = m_ScaleFactorCurve.value;
        var steps = m_StepsField.value;
        var stepsPerJob = m_StepsPerJobField.value;
        var maxFramesPerJob = m_MaxFramesField.value;
        var maxForegroundObjectsPerFrame = m_MaxForegroundObjectsPerFrame.value;

        if (steps == 0 || maxFramesPerJob == 0 || stepsPerJob == 0 || maxForegroundObjectsPerFrame == 0)
            return 0;

        // var inPlaneRotations = ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Temp);
        // var outOfPlaneRotations = ObjectPlacementUtilities.GenerateOutOfPlaneRotationCurriculum(Allocator.Temp);
        //
        // var framesPerScaleFactorMaxObjects = inPlaneRotations.Length * outOfPlaneRotations.Length * 64 / maxForegroundObjectsPerFrame;
        // var scaleAccountingForBackgroundInForeground = 1/(1-m_BackgroundInForegroundChanceField.value);
        // var framesPerScaleFactorAtOneScale = k_EstimatedFramesPerCurriculumStepAtOneScale * scaleAccountingForBackgroundInForeground * inPlaneRotations.Length * outOfPlaneRotations.Length;
        //
        // inPlaneRotations.Dispose();
        // outOfPlaneRotations.Dispose();
        //
        // var stepSize = 1f / (steps + 1);
        // var time = 0f;
        // var estimate = 0;
        //
        // for (int i = 0; i < steps; i++)
        // {
        //     var scaleFactor = scaleFactorRange.Evaluate(time);
        //     var stepEstimate = (int)(framesPerScaleFactorAtOneScale * (scaleFactor * scaleFactor));
        //     stepEstimate = Math.Max(stepEstimate, framesPerScaleFactorMaxObjects);
        //     estimate += stepEstimate;
        //     time += stepSize;
        // }

        var maxFrames = (m_StepsField.value / stepsPerJob) * maxFramesPerJob;

        // return math.min(maxFrames, estimate);
        return maxFrames;
    }

    float m_Progress;
    string m_RunStatus;
    Task m_ExecuteTask;
    CancellationTokenSource m_CancellationTokenSource;
    bool m_ProgressBarDirty;

    void OnBeginRunButtonClicked()
    {
        var scaleFactorRange = m_ScaleFactorCurve.value;
        var steps = m_StepsField.value;
        var stepsPerJob = m_StepsPerJobField.value;
        var appParams = new AppParams()
        {
            // NOTE: ScaleFactors intentionally not populated here because it varies by USim instance
            Seed = (uint)m_SeedField.value,
            MaxFrames = m_MaxFramesField.value,
            MaxForegroundObjectsPerFrame = m_MaxForegroundObjectsPerFrame.value,
            BackgroundObjectDensity = m_BackgroundObjectDensityField.value,
            NumBackgroundFillPasses = m_NumBackgroundFillPassesField.value,
            ScalingMin = m_ScalingMinField.value,
            ScalingSize = m_ScalingSizeField.value,
            LightColorMin = m_LightColorMinField.value,
            LightRotationMax = m_LightRotationMaxField.value,
            BackgroundHueMaxOffset = m_BackgroundHueMaxOffsetField.value,
            OccludingHueMaxOffset = m_OccludingHueMaxOffsetField.value,
            BackgroundObjectInForegroundChance = m_BackgroundInForegroundChanceField.value,
            NoiseStrengthMax = m_NoiseStrengthMaxField.value,
            BlurKernelSizeMax = m_BlurKernelSizeMaxField.value,
            BlurStandardDeviationMax = m_BlurStandardDeviationMaxField.value
        };

        // Build and zip
        if (m_NameField.value == null)
            m_NameField.value = "SynthDet";

        Task<Run> runTask;
        
        UpdateProgress(0f, "");
        m_CancellationTokenSource = new CancellationTokenSource();

        if (m_UseExistingBuildToggle.value)
        {
            if (string.IsNullOrWhiteSpace(m_ExistingBuildId.value))
            {
                Debug.LogError("Build ID is not valid");
                return;
            }
            runTask = new Task<Run>(() =>
                ExecuteRun(m_NameField.value,
                    m_SysParamPopup.value.id,
                    scaleFactorRange,
                    steps,
                    stepsPerJob,
                    appParams,
                    m_CancellationTokenSource.Token,
                    m_ExistingBuildId.value,
                    0f));
            runTask.Start();
        }
        else
        {
            runTask = BuildAndStartUSimRun(
                m_NameField.value, 
                m_SysParamPopup.value.id,
                scaleFactorRange, 
                steps, 
                stepsPerJob, 
                appParams);
        }
        
        runTask?.ContinueWith(task => Debug.Log("USim run started. Execution ID " + task.Result.executionId));
        m_ExecuteTask = runTask;
    }
    
    
    bool CreateLinuxBuildAndZip(string buildName, out string zipPath)
    {
        string pathToZip;
        var pathToProjectBuild = Application.dataPath + "/../" + "Build/";
        if (!Directory.Exists(pathToProjectBuild) ||
             Directory.Exists(pathToProjectBuild) && !Directory.Exists(pathToProjectBuild + buildName)
            )
        {
            Directory.CreateDirectory((pathToProjectBuild + buildName));
        }

        pathToProjectBuild = pathToProjectBuild + buildName + "/";

        // Create Linux build
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/MainScene.unity" };
        buildPlayerOptions.locationPathName = Path.Combine(pathToProjectBuild, buildName + ".x86_64");
        buildPlayerOptions.target = BuildTarget.StandaloneLinux64;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded."); 
            EditorUtility.DisplayProgressBar("Compress Project", "Compressing Project Build...", 0);
            ulong totalSize = summary.totalSize;

            // Zip the build
            pathToZip = Application.dataPath + "/../" + "Build/" + buildName;
            Zip.DirectoryContents(pathToZip, buildName);
            
            EditorUtility.ClearProgressBar();
            
            // Return path to .zip file
            string[] st = Directory.GetFiles(pathToZip + "/../", buildName + ".zip");
            if (st.Length != 0)
                pathToZip = Path.GetFullPath(st[0]);
            else
            {
                zipPath = null;
                return false;
            }
        }
        else
        {
            zipPath = null;
            return false;
        }
        zipPath = pathToZip;
        
        return true;
    }

    public void Update()
    {
        if (m_ExecuteTask == null)
            return;

        if (m_ExecuteTask.IsCompleted)
        {
            EditorUtility.ClearProgressBar();
            m_ExecuteTask = null;
            return;
        }

        if (m_ProgressBarDirty && EditorUtility.DisplayCancelableProgressBar(s_ProgressHeader, m_RunStatus, m_Progress))
        {
            m_ProgressBarDirty = false;
            m_CancellationTokenSource?.Cancel();
            m_CancellationTokenSource = null;
        }
        Repaint();
    }

    const string s_ProgressHeader = "Run in Unity Simulation";
    const float k_UploadProgressPercentage = .9f;

    public Task<Run> BuildAndStartUSimRun(
        string runName, 
        string sysParamId, 
        AnimationCurve scaleFactorRange, 
        int steps, 
        int stepsPerJob, 
        AppParams appParams)
    {
        var token = m_CancellationTokenSource.Token;
        
        var buildSuccess = CreateLinuxBuildAndZip(m_NameField.value, out string buildZipPath);
        if (!buildSuccess)
            return null;
            
        var taskRun = API.UploadBuildAsync(runName, buildZipPath, cancellationTokenSource: m_CancellationTokenSource, 
            progress: (progress) =>
            {
                UpdateProgress(progress * k_UploadProgressPercentage, "Uploading build") ;
            });
        
        var runTask = taskRun.ContinueWith(( finishedTask) =>
        {
            if (finishedTask.IsCanceled)
                return null;

            if (finishedTask.IsFaulted)
            {
                Debug.Log($"Upload failed. {finishedTask.Exception}");
                return null;
            }
            
            Debug.Log($"Upload complete: build id {finishedTask.Result}");
            
            return ExecuteRun(runName, sysParamId, scaleFactorRange, steps, stepsPerJob, appParams, token, finishedTask.Result, k_UploadProgressPercentage);
        }, token);
        return runTask;
    }

    Run ExecuteRun(string runName, string sysParamId, AnimationCurve scaleFactorRange, int steps, int stepsPerJob, AppParams appParams, CancellationToken token, string buildId, float progressThusFar)
    {
        var appParamList = GenerateAppParamIds(runName, scaleFactorRange, steps, stepsPerJob, appParams, token, progressThusFar);
        if (appParamList == null || token.IsCancellationRequested)
        {
            Debug.Log($"Operation canceled");
            return null;
        }

        UpdateProgress(1f, "Starting run execution");
        var runDefinitionId = API.UploadRunDefinition(new RunDefinition
        {
            app_params = appParamList.ToArray(),
            name = runName,
            sys_param_id = sysParamId,
            build_id = buildId
        });
        var run = Run.CreateFromDefinitionId(runDefinitionId);
        run.Execute();
        m_CancellationTokenSource.Dispose();
        return run;
    }

    void UpdateProgress(float progress, string runStatus)
    {
        m_ProgressBarDirty = true;
        m_RunStatus = runStatus;
        m_Progress = progress;
    }

    List<AppParam> GenerateAppParamIds(string runName, AnimationCurve scaleFactorRange, int steps, int stepsPerJob, AppParams appParams, CancellationToken token, float progressThusFar)
    {
        float stepSize = 1f / (steps + 1);
        float time = 0f;
        int stepsThisJob = 0;
        int jobIndex = 0;
        List<float> scaleFactors = new List<float>(stepsPerJob);
        var appParamIds = new List<AppParam>();
        for (int i = 0; i < steps; i++)
        {
            stepsThisJob++;
            if (stepsThisJob == stepsPerJob)
            {
                stepsThisJob = 0;
                var appParamName = $"{runName}_{jobIndex}";
                
                UpdateProgress(progressThusFar + (1 - progressThusFar) * jobIndex / steps, $"Uploading app param {appParamName}");
                if (token.IsCancellationRequested)
                    return null;
                
                // appParams.ScaleFactorMin = scaleFactorRange.Evaluate(((float)i) / steps);
                // appParams.ScaleFactorMax = scaleFactorRange.Evaluate(((float)i + 1) / steps);
                appParams.ScaleFactorMin = scaleFactorRange.Evaluate(0);
                appParams.ScaleFactorMax = scaleFactorRange.Evaluate(1);

                appParams.Seed = appParams.Seed + (uint)i * ObjectPlacementUtilities.LargePrimeNumber;
                var appParamId = API.UploadAppParam(appParamName, appParams);
                appParamIds.Add(new AppParam()
                {
                    id = appParamId,
                    name = appParamName,
                    num_instances = 1
                });
                jobIndex++;
                scaleFactors.Clear();
            }

            time += stepSize;
        }

        return appParamIds;
    }
}
