using System;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Simulation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Perception;

#if !UNITY_EDITOR
using System.IO;
#endif

public class ProjectInitialization : MonoBehaviour
{
    public string ForegroundObjectResourcesDirectory = "Foreground";
    public string BackgroundObjectResourcesDirectory = "Background";
    public string BackgroundImageResourcesDirectory = "GroceryStoreDataset";
    
    public float[] ScaleFactors = new[] { 1.0f, .5f};
    public int MaxFrames = 5000;
    public bool EnableProfileLog;
    public PerceptionCamera PerceptionCamera;
    public PostProcessRandomizationParams PostProcessingParams = new PostProcessRandomizationParams
    {
        // Defaults determined through manual testing and visual inspection
        NoiseStrengthMax = 0.02f,
        BlurKernelSizeMax = 0.01f,
        BlurStandardDeviationMax = 0.5f
    };
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
        AppParams appParams;
        if (!String.IsNullOrEmpty(Configuration.Instance.SimulationConfig.app_param_uri))
        {
            appParams = Configuration.Instance.GetAppParams<AppParams>();
            MaxFrames = appParams.MaxFrames;
        }
        else
        {
            appParams = new AppParams(ScaleFactors, MaxFrames);
        }
        
        Debug.Log($"{nameof(ProjectInitialization)}: Starting up. MaxFrames: {appParams.MaxFrames}, scale factors {{{string.Join(", ", appParams.ScaleFactors)}}}");
        
        m_PlacementStatics = new PlacementStatics(appParams.MaxFrames, foregroundObjects, backgroundObjects, backgroundImages,
            ObjectPlacementUtilities.GenerateInPlaneRotationCurriculum(Allocator.Persistent), 
            ObjectPlacementUtilities.GenerateOutOfPlaneRotationCurriculum(Allocator.Persistent), 
            new NativeArray<float>(appParams.ScaleFactors, Allocator.Persistent));
        m_CurriculumStateEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentData(m_CurriculumStateEntity, new CurriculumState());
        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentObject(m_CurriculumStateEntity, m_PlacementStatics);

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
            var name = f.name;
            if (regex.IsMatch(name))
                name = name.Substring(0, name.Length - 3);
            return name;
        }).ToList();
        var foregroundObjectsMissingFromConfig = foregroundNames.Where(f => labelingConfiguration.LabelingConfigurations.All(l => l.label != f)).ToList();
        var configurationsMissingModel = labelingConfiguration.LabelingConfigurations.Skip(1).Select(l => l.label).Where(l => !foregroundNames.Any(f => f == l)).ToList();

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
