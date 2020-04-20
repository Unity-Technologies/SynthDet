using System;
using System.Configuration;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Perception;
using UnityEngine.UI;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(LightingRandomizerSystem))]
public class BackgroundGenerator : JobComponentSystem
{
    private float m_cameraOrthographicSize => m_camera.orthographicSize;
    private float m_objectDensity => m_parameters.ObjectDensity;
    private int m_numFillPasses => m_parameters.NumFillPasses;

    static Unity.Profiling.ProfilerMarker s_ResetBackgroundObjects = new Unity.Profiling.ProfilerMarker("ResetBackgroundObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceBackgroundObjects = new Unity.Profiling.ProfilerMarker("PlaceBackgroundObjects");
    static Unity.Profiling.ProfilerMarker s_DrawMeshes = new Unity.Profiling.ProfilerMarker("DrawBackgroundObjects");
    Random m_Rand;
    internal bool m_initialized = false;
    private bool m_enabled => m_parameters != null && m_parameters.SystemEnabled;
    internal int m_framesWaited = 0;
    private BackgroundGeneratorParameters m_parameters;
    private EntityQuery m_CurriculumQuery;
    internal Camera m_camera;
    internal GameObject m_container;
    internal GameObjectOneWayCache m_objectCache;
    internal Vector3 m_placementOrigin;
    internal int m_BackgroundLayer = LayerMask.NameToLayer("Background");
    MetricDefinition m_ScaleRangeMetric;
    static readonly Guid m_BackgroundScaleMetricId = Guid.Parse("4A55E55C-76E8-47C4-8A39-813E6833B04F");

    private float m_aspectRatio => m_camera.aspect;
    
    protected void Initialize()
    {
        const string globalInitParamsName = "Management";
        var globalInitParamsContainer = GameObject.Find(globalInitParamsName);
        if (globalInitParamsContainer == null)
        {
            Debug.Log($"Cannot find a {globalInitParamsName} object to init parameters from.");
            return;
        }

        var paperInit = globalInitParamsContainer.GetComponent<ProjectInitialization>();
        var perceptionCamera = paperInit.PerceptionCamera;
        m_camera = perceptionCamera.GetComponentInParent<Camera>();
        // Compute the bottom left corner of the view frustum
        // IMPORTANT: We're assuming the camera is facing along the positive z-axis
        m_placementOrigin = new Vector3(-m_aspectRatio * m_cameraOrthographicSize, -m_cameraOrthographicSize, 40f);
        m_container = new GameObject("BackgroundContainer");
        m_container.transform.SetPositionAndRotation(m_camera.transform.position - Vector3.forward + Vector3.up * 100f,
            Quaternion.identity);
        var statics = EntityManager.GetComponentObject<PlacementStatics>(m_CurriculumQuery.GetSingletonEntity());
        m_objectCache = new GameObjectOneWayCache(m_container.transform, statics.BackgroundPrefabs);
        
        const string parametersName = "BackgroundGeneratorParameters";
        var paramContainer = GameObject.Find(parametersName);
        if (paramContainer == null)
        {
            Debug.Log($"Cannot find a {parametersName} object to read parameters from.");
            return;
        }
        m_parameters = paramContainer.GetComponent<BackgroundGeneratorParameters>();

        if (!m_enabled)
        {
            Debug.LogWarning("Background generator is disabled.");
            m_initialized = true;
            return;
        }
        m_framesWaited = m_parameters.PauseBetweenFrames;
        m_initialized = true;

        m_ScaleRangeMetric = SimulationManager.RegisterMetricDefinition("background scale range", "The range of scale factors used to place background objects each frame", m_BackgroundScaleMetricId);
    }

    protected override void OnCreate()
    {
        m_Rand = new Random(2);
        
        m_initialized = false; 
        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
    }
    
    [BurstCompile]
    struct PlaceObjectsJob : IJobParallelFor
    {
        public Vector3 PlacementOrigin;
        public int NumCellsHorizontal;
        public int NumCellsVertical;
        public float HorizontalStep;
        public float VerticalStep;
        public float ForegroundSize;
        public float MinScale;
        public float MaxScale;
        public uint Seed;
        [NativeDisableParallelForRestriction]
        public NativeArray<MeshDrawInfo> MeshDrawInfos;
        [NativeDisableParallelForRestriction]
        public NativeArray<MeshInfo> MeshInfos;
        public int TextureCount;

        public void Execute(int passIndex)
        {
            // Best practice for deterministic random numbers in parallel jobs is to provide a base seed and add the index multiplied by a large prime.
            // The large prime multiplication is necessary because the algorithm implemented by Random does not have a good distribution between neighboring seeds.
            var rand = new Random(Seed + (uint)passIndex * ObjectPlacementUtilities.LargePrimeNumber);
            
            // Partition view into cells and place one object in each cell 
            for (var i = 0; i < NumCellsHorizontal; i++)
            {
                for (var j = 0; j < NumCellsVertical; j++)
                {
                    // IMPORTANT: Assumes the camera object is pointing in the positive z-direction in global space
                    //            To generalize to an arbitrary camera position we would need m_camera's transform
                    var x = HorizontalStep * (i + rand.NextFloat());
                    var y = VerticalStep * (j + rand.NextFloat());
                    var position = new Vector3(x, y, 0) + PlacementOrigin;
                    MeshDrawInfos[passIndex * NumCellsHorizontal * NumCellsVertical + i * NumCellsVertical + j] = 
                        PlaceBackgroundObject(MeshInfos, ForegroundSize, position, ref rand, MinScale, MaxScale, TextureCount);
                }
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!m_initialized)
        {
            Initialize();
        }

        if (!m_initialized)
            return inputDeps;

        if (m_framesWaited < m_parameters.PauseBetweenFrames)
        {
            m_framesWaited++;
            return inputDeps;
        }
        else
        {
            m_framesWaited = 0;
        }
        
        if (!m_enabled)
        {
            return inputDeps;
        }

        if (m_CurriculumQuery.CalculateEntityCount() != 1)
            return inputDeps;

        using (s_ResetBackgroundObjects.Auto())
        {
            m_objectCache.ResetAllObjects();
        }

        var entity = m_CurriculumQuery.GetSingletonEntity();
        var curriculumState = EntityManager.GetComponentData<CurriculumState>(entity);
        var statics = EntityManager.GetComponentObject<PlacementStatics>(entity);

        if (curriculumState.ScaleIndex >= statics.ScaleFactors.Length)
            return inputDeps;

        var meshInfos = new NativeArray<MeshInfo>(statics.BackgroundPrefabs.Length, Allocator.TempJob);
        for (int i = 0; i < statics.BackgroundPrefabs.Length; i++)
        {
            ObjectPlacementUtilities.GetMeshAndMaterial(statics.BackgroundPrefabs[i], out var material, out var meshToDraw);
            meshInfos[i] = new MeshInfo()
            {
                Bounds = meshToDraw.bounds
            };
        }

        var foregroundObject = statics.ForegroundPrefabs[curriculumState.PrefabIndex];
        ObjectPlacementUtilities.GetMeshAndMaterial(foregroundObject, out _, out var foregroundMesh);
        var foregroundRotation = ObjectPlacementUtilities.ComposeForegroundRotation(curriculumState, statics.OutOfPlaneRotations, statics.InPlaneRotations);
        var foregroundScale = ObjectPlacementUtilities.ComputeForegroundScaling(
                foregroundMesh.bounds, statics.ScaleFactors[curriculumState.ScaleIndex]);
        var foregroundSize = ObjectPlacementUtilities.ComputeProjectedArea(foregroundRotation, foregroundMesh.bounds, foregroundScale);
        
        var cameraViewArea = (m_cameraOrthographicSize * 2) * (m_cameraOrthographicSize * 2) * m_aspectRatio;
        // Lazy approximation of how many subdivisions we need to achieve the target density
        var numCellsSqrt = math.sqrt( m_objectDensity * cameraViewArea / foregroundSize);
        var numCellsHorizontal = (int)math.round(numCellsSqrt * m_aspectRatio);
        var numCellsVertical = (int) math.round(numCellsSqrt / m_aspectRatio);
        var cameraViewHeight = m_camera.orthographicSize * 2;
        // Assuming we've placed the camera such that the bottom left bounds are at 0, 0
        var verticalStep = cameraViewHeight / numCellsVertical;
        var placementBoundsWidth = cameraViewHeight * m_aspectRatio;
        var horizontalStep = placementBoundsWidth / numCellsHorizontal;
        var scale0 = m_Rand.NextFloat(0.9f, 1.5f);
        var scale1 = m_Rand.NextFloat(0.9f, 1.5f);
        var scaleMin = math.min(scale0, scale1);
        var scaleMax = math.max(scale0, scale1);
        
        SimulationManager.ReportMetric(m_ScaleRangeMetric, $@"[ {{ ""scale_min"": {scaleMin}, ""scale_max"": {scaleMax} }}]");

        var meshesToDraw = new NativeArray<MeshDrawInfo>(numCellsHorizontal * numCellsVertical * m_numFillPasses, Allocator.TempJob);
        using (s_PlaceBackgroundObjects.Auto())
        {
            // XXX: Rather than placing a large collection and then looking for gaps, we simply assume that a sufficiently
            //      dense background will not have gaps - rendering way more objects than necessary is still substantially
            //      faster than trying to read the render texture multiple times per frame
            new PlaceObjectsJob()
            {
                PlacementOrigin = m_placementOrigin,
                NumCellsHorizontal = numCellsHorizontal,
                NumCellsVertical = numCellsVertical,
                HorizontalStep = horizontalStep,
                VerticalStep = verticalStep,
                ForegroundSize = foregroundSize,
                MeshInfos = meshInfos,
                MeshDrawInfos = meshesToDraw,
                TextureCount = statics.BackgroundImages.Length,
                Seed = m_Rand.NextUInt(),
                MinScale = scaleMin,
                MaxScale = scaleMax
            }.Schedule(m_numFillPasses, 1, inputDeps).Complete();
        }

        using (s_DrawMeshes.Auto())
        {
            var properties = new MaterialPropertyBlock();
            foreach (var meshDrawInfo in meshesToDraw)
            {
                var prefab = statics.BackgroundPrefabs[meshDrawInfo.MeshIndex];
                var sceneObject = m_objectCache.GetOrInstantiate(prefab);
                ObjectPlacementUtilities.CreateRandomizedHue(properties, ref m_Rand);
                properties.SetTexture("_BaseMap", statics.BackgroundImages[meshDrawInfo.TextureIndex]);
                sceneObject.GetComponentInChildren<MeshRenderer>().SetPropertyBlock(properties);
                sceneObject.transform.SetPositionAndRotation(meshDrawInfo.Position, meshDrawInfo.Rotation);
                sceneObject.transform.localScale = meshDrawInfo.Scale;
            }
        }
        
        // We have finished drawing the meshes in the camera view, but the engine itself will call Render()
        // at the end of the frame
        meshInfos.Dispose();
        meshesToDraw.Dispose();

        var numObjectsExpected = numCellsHorizontal * numCellsVertical * m_numFillPasses;
        if (numObjectsExpected != m_objectCache.NumObjectsActive)
        {
            Debug.LogWarning($"BackgroundGenerator should have placed {numObjectsExpected} but is only using " +
                             $"{m_objectCache.NumObjectsActive} from the cache.");
        }

        return inputDeps;
    }
    
    static MeshDrawInfo PlaceBackgroundObject(NativeArray<MeshInfo> meshInfos, float foregroundObjectSize, Vector3 position,
        ref Random rand, float scaleMin, float scaleMax, int textureCount)
    {
        var meshIndex = rand.NextInt(0, meshInfos.Length);
        var meshInfo = meshInfos[meshIndex];
        
        // Rotate/resize object
        var rotation = rand.NextQuaternionRotation();
        var scaleRandom = rand.NextFloat(scaleMin, scaleMax);
        var scale = ObjectPlacementUtilities.ComputeScaleToMatchArea(rotation, meshInfo.Bounds,
            scaleRandom * foregroundObjectSize);
        
        return new MeshDrawInfo()
        {
            MeshIndex = meshIndex,
            Position = position,
            Rotation = rotation,
            Scale = scale * Vector3.one,
            TextureIndex = rand.NextInt(textureCount)
        };
    }
    public struct MeshInfo
    {
        public Bounds Bounds;
    }

    public struct MeshDrawInfo
    {
        public int MeshIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public int TextureIndex;
    }
}

