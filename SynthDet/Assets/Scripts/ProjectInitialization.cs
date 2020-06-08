using System;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Simulation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using System.IO;

public class ProjectInitialization : MonoBehaviour
{
    static readonly Guid k_AppParamsMetricGuid = new Guid("3F06BCEC-1F23-4387-A1FD-5AF54EE29C16");
    
    // Defaults are shared between the ProjectInitialization inspector GUI and the USim execution window, so they
    // defined one here statically to be used in both places
    public static readonly AppParams AppParamDefaults = new AppParams()
    {
        ScaleFactors = new[] { 1.0f, .5f},
        MaxFrames = 5000,
        MaxForegroundObjectsPerFrame = 500,
        NumBackgroundFillPasses = 1,
        BackgroundObjectDensity = 3,
        ScalingMin = 0.2f,
        ScalingSize = 0.1f,
        LightColorMin = 0.1f,
        LightRotationMax = 90f,
        BackgroundHueMaxOffset = 180,
        OccludingHueMaxOffset = 180f,
        BackgroundObjectInForegroundChance = .2f,
        NoiseStrengthMax = 0.02f,
        BlurKernelSizeMax = 0.01f,
        BlurStandardDeviationMax = 0.5f
    };
    public string ForegroundObjectResourcesDirectory = "Foreground";
    public string BackgroundObjectResourcesDirectory = "Background";
    public string BackgroundImageResourcesDirectory = "GroceryStoreDataset";

    public AppParams AppParameters = AppParamDefaults;
    public bool EnableProfileLog;
    public PerceptionCamera PerceptionCamera;
    Entity m_ResourceDirectoriesEntity;
    Entity m_CurriculumStateEntity;
    string m_ProfileLogPath;
    PlacementStatics m_PlacementStatics;

    void Start()
    {
        m_ResourceDirectoriesEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(ResourceDirectories));
        World.DefaultGameObjectInjectionWorld.EntityManager.SetComponentData(m_ResourceDirectoriesEntity, new ResourceDirectories
        {
            ForegroundResourcePath = ForegroundObjectResourcesDirectory,
            BackgroundResourcePath = BackgroundObjectResourcesDirectory
        });
        var foregroundObjects = Resources.LoadAll<GameObject>(ForegroundObjectResourcesDirectory);
        var backgroundObjects = Resources.LoadAll<GameObject>(BackgroundObjectResourcesDirectory);
        var backgroundImages = Resources.LoadAll<Texture2D>(BackgroundImageResourcesDirectory);

        if (foregroundObjects.Length == 0)
        {
            Debug.LogError($"No Prefabs of FBX files found in foreground object directory \"{ForegroundObjectResourcesDirectory}\".");
            return;
        }
        if (backgroundObjects.Length == 0)
        {
            Debug.LogError($"No Prefabs of FBX files found in background object directory \"{BackgroundObjectResourcesDirectory}\".");
            return;
        }
        //TODO: Fill in CurriculumState from app params
        if (TryGetAppParamPathFromCommandLine(out string appParamPath))
        {
            var AppParamsJson = File.ReadAllText(appParamPath);
            AppParameters = JsonUtility.FromJson<AppParams>(AppParamsJson);
        }
        else if (!String.IsNullOrEmpty(Configuration.Instance.SimulationConfig.app_param_uri))
        {
            AppParameters = Configuration.Instance.GetAppParams<AppParams>();
        }
        
        Debug.Log($"{nameof(ProjectInitialization)}: Starting up. MaxFrames: {AppParameters.MaxFrames}, " +
            $"scale factors {{{string.Join(", ", AppParameters.ScaleFactors)}}}");
        
        m_PlacementStatics = new PlacementStatics(
            AppParameters.MaxFrames, 
            AppParameters.MaxForegroundObjectsPerFrame, 
            AppParameters.ScalingMin, 
            AppParameters.ScalingSize, 
            AppParameters.OccludingHueMaxOffset, 
            AppParameters.BackgroundObjectInForegroundChance,
            foregroundObjects, 
            backgroundObjects, 
            backgroundImages,
            ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Persistent), 
            ObjectPlacementUtilities.GenerateOutOfPlaneRotationCurriculum(Allocator.Persistent), 
            new NativeArray<float>(AppParameters.ScaleFactors, Allocator.Persistent));
        var appParamsMetricDefinition = SimulationManager.RegisterMetricDefinition(
            "app-params", description:"The values from the app-params used in the simulation. Only triggered once per simulation.", id: k_AppParamsMetricGuid);
        SimulationManager.ReportMetric(appParamsMetricDefinition, new[] {AppParameters});
        m_CurriculumStateEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentData(
            m_CurriculumStateEntity, new CurriculumState());
        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentObject(
            m_CurriculumStateEntity, m_PlacementStatics);

        ValidateForegroundLabeling(foregroundObjects, PerceptionCamera);
        
#if !UNITY_EDITOR
        if (Debug.isDebugBuild && EnableProfileLog)
        {
            Debug.Log($"Enabling profile capture");
            m_ProfileLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "profileLog.raw");
            if (System.IO.File.Exists(m_ProfileLogPath))
                System.IO.File.Delete(m_ProfileLogPath);

            UnityEngine.Profiling.Profiler.logFile = m_ProfileLogPath;
            UnityEngine.Profiling.Profiler.enabled = true;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;

        }
#endif
        Manager.Instance.ShutdownNotification += CleanupState;
        
        PerceptionCamera.renderedObjectInfosCalculated += OnRenderedObjectInfosCalculated;
    }

    static bool TryGetAppParamPathFromCommandLine(out string appParamPath)
    {
        appParamPath = null;
        var appParamArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--app-param"));
        if (appParamArg == null)
            return false;

        var strings = appParamArg.Split('=');
        if (strings.Length < 2)
            return false;

        appParamPath = strings[1].Trim('"');
        return true;
    }

    void OnRenderedObjectInfosCalculated(int frameCount, NativeArray<RenderedObjectInfo> renderedObjectinfos)
    {
        foreach (var info in renderedObjectinfos)
        {
            if (info.pixelCount < 50)
            {
                Debug.Log($"Found small bounding box {info} in frame {frameCount}");
            }
        }
    }

    void ValidateForegroundLabeling(GameObject[] foregroundObjects, PerceptionCamera perceptionCamera)
    {
        if (perceptionCamera.LabelingConfiguration == null)
        {
            Debug.LogError("PerceptionCamera does not have a labeling configuration. This will likely cause the program to fail.");
            return;
        }

        var labelingConfiguration = perceptionCamera.LabelingConfiguration;
        var regex = new Regex(".*_[0-9][0-9]");
        var foregroundNames = foregroundObjects.Select(f =>
        {
            var foregroundName = f.name;
            if (regex.IsMatch(foregroundName))
                foregroundName = foregroundName.Substring(0, foregroundName.Length - 3);
            return foregroundName;
        }).ToList();
        var foregroundObjectsMissingFromConfig = foregroundNames.Where(f => labelingConfiguration.LabelEntries.All(l => l.label != f)).ToList();
        var configurationsMissingModel = labelingConfiguration.LabelEntries.Skip(1).Select(l => l.label).Where(l => !foregroundNames.Any(f => f == l)).ToList();

        if (foregroundObjectsMissingFromConfig.Count > 0)
        {
            Debug.LogError($"The following foreground models are not present in the LabelingConfiguration: {string.Join(", ", foregroundObjectsMissingFromConfig)}");
        }
        if (configurationsMissingModel.Count > 0)
        {
            Debug.LogError($"The following LabelingConfiguration entries do not correspond to any foreground object model: {string.Join(", ", configurationsMissingModel)}");
        }
    }

    void CleanupState()
    {
#if !UNITY_EDITOR
        if (Debug.isDebugBuild && EnableProfileLog)
        {
            Debug.Log($"Producing profile capture.");
            UnityEngine.Profiling.Profiler.enabled = false;
            var targetPath = Path.Combine(Manager.Instance.GetDirectoryFor("Profiling"), "profileLog.raw");
            File.Copy(m_ProfileLogPath, targetPath);
            Manager.Instance.ConsumerFileProduced(targetPath);
        }
#endif

        m_PlacementStatics.ScaleFactors.Dispose();
        m_PlacementStatics.InPlaneRotations.Dispose();
        m_PlacementStatics.OutOfPlaneRotations.Dispose();
        World.DefaultGameObjectInjectionWorld?.EntityManager?.DestroyEntity(m_ResourceDirectoriesEntity);
        World.DefaultGameObjectInjectionWorld?.EntityManager?.DestroyEntity(m_CurriculumStateEntity);
    }
}
