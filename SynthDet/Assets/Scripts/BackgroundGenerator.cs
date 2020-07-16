using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(LightingRandomizerSystem))]
public class BackgroundGenerator : JobComponentSystem
{
    AppParams m_AppParams;
    internal const float k_PlacementDistance = 12f;

    static Unity.Profiling.ProfilerMarker s_ResetBackgroundObjects = new Unity.Profiling.ProfilerMarker("ResetBackgroundObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceBackgroundObjects = new Unity.Profiling.ProfilerMarker("PlaceBackgroundObjects");
    static Unity.Profiling.ProfilerMarker s_DrawMeshes = new Unity.Profiling.ProfilerMarker("DrawBackgroundObjects");
    EntityQuery m_CurriculumQuery;
    Random m_Rand;
    internal bool initialized = false;
    internal int framesWaited = 0;
    internal float backgroundHueMaxOffset = 180f; // ADR parameter for maximum hue offset
    internal Camera camera;
    internal GameObject container;
    internal GameObjectOneWayCache objectCache;
    Vector3 m_ForegroundCenter;
    MetricDefinition m_ScaleRangeMetric;
    static readonly Guid k_BackgroundScaleMetricId = Guid.Parse("4A55E55C-76E8-47C4-8A39-813E6833B04F");

    Vector3 m_CameraForward => camera.transform.forward;
    float m_objectDensity => m_AppParams.BackgroundObjectDensity;
    int numFillPasses => m_AppParams.NumBackgroundFillPasses;
    float m_aspectRatio => camera.aspect;

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
    
    [BurstCompile]
    struct PlaceBackgroundObjectsJob : IJobParallelFor
    {
        public Vector3 PlacementOrigin;
        public WorldToScreenTransformer Transformer;
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
                        PlaceBackgroundObject(MeshInfos, Transformer, ForegroundSize, position, ref rand, MinScale, MaxScale, TextureCount);
                }
            }
        }
    }
    
    protected void Initialize()
    {
        m_Rand = new Random(2);
        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
        
        const string globalInitParamsName = "Management";
        var globalInitParamsContainer = GameObject.Find(globalInitParamsName);
        if (globalInitParamsContainer == null)
        {
            Debug.Log($"Cannot find a {globalInitParamsName} object to init parameters from.");
            return;
        }

        var init = globalInitParamsContainer.GetComponent<ProjectInitialization>();
        m_AppParams = init.AppParameters;
        var perceptionCamera = init.PerceptionCamera;
        camera = perceptionCamera.GetComponentInParent<Camera>();
        m_ForegroundCenter = 
            camera.transform.position + m_CameraForward * ForegroundObjectPlacer.k_ForegroundLayerDistance;
        // Compute the bottom left corner of the view frustum
        // IMPORTANT: We're assuming the camera is facing along the positive z-axis
        container = new GameObject("BackgroundContainer");
        container.transform.SetPositionAndRotation(
            m_CameraForward * k_PlacementDistance + camera.transform.position,
            Quaternion.identity);
        var statics = EntityManager.GetComponentObject<PlacementStatics>(m_CurriculumQuery.GetSingletonEntity());
        objectCache = new GameObjectOneWayCache(container.transform, statics.BackgroundPrefabs);

        backgroundHueMaxOffset = init.AppParameters.BackgroundHueMaxOffset;
        initialized = true;

        m_ScaleRangeMetric = DatasetCapture.RegisterMetricDefinition("background scale range", "The range of scale factors used to place background objects each frame", k_BackgroundScaleMetricId);
    }

    protected override void OnCreate()
    {
        initialized = false; 
    }


    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!initialized)
            Initialize();

        if (!initialized)
            return inputDeps;

        if (m_CurriculumQuery.CalculateEntityCount() != 1)
            return inputDeps;

        using (s_ResetBackgroundObjects.Auto())
        {
            objectCache.ResetAllObjects();
        }

        var entity = m_CurriculumQuery.GetSingletonEntity();
        var curriculumState = EntityManager.GetComponentData<CurriculumState>(entity);
        var statics = EntityManager.GetComponentObject<PlacementStatics>(entity);

        if (curriculumState.ScaleIndex >= statics.ScaleFactors.Length)
            return inputDeps;

        var meshInfos = new NativeArray<MeshInfo>(statics.BackgroundPrefabs.Length, Allocator.TempJob);
        for (int i = 0; i < statics.BackgroundPrefabs.Length; i++)
        {
            // ReSharper disable once UnusedVariable
            ObjectPlacementUtilities.GetMeshAndMaterial(statics.BackgroundPrefabs[i], out var material, out var meshToDraw);
            meshInfos[i] = new MeshInfo()
            {
                Bounds = meshToDraw.bounds
            };
        }

        var foregroundObject = statics.ForegroundPrefabs[curriculumState.PrefabIndex];
        var foregroundBounds = ObjectPlacementUtilities.ComputeBounds(foregroundObject);
        var foregroundRotation = 
            ObjectPlacementUtilities.ComposeForegroundRotation(curriculumState, statics.OutOfPlaneRotations, statics.InPlaneRotations);
        var foregroundScale = ObjectPlacementUtilities.ComputeForegroundScaling(
            foregroundBounds, statics.ScaleFactors[curriculumState.ScaleIndex]);
        var transformer = new WorldToScreenTransformer(camera);
        // NOTE: For perspective projection, size will depend on position within the viewport, but we're approximating
        //       by computing size for the center
        var foregroundSizePixels = ObjectPlacementUtilities.ComputeProjectedArea(
            transformer, m_ForegroundCenter, foregroundRotation, foregroundBounds, foregroundScale);
        
        var placementRegion = ObjectPlacementUtilities.ComputePlacementRegion(camera, k_PlacementDistance);
        var areaPlacementRegion = placementRegion.width * placementRegion.height;
        var cameraSquarePixels = camera.pixelHeight * camera.pixelWidth;
        var foregroundSizeUnits = foregroundSizePixels * areaPlacementRegion / cameraSquarePixels;
        // Lazy approximation of how many subdivisions we need to achieve the target density
        var numCellsSqrt = math.sqrt( m_objectDensity * areaPlacementRegion / foregroundSizeUnits);
        var numCellsHorizontal = (int)math.round(numCellsSqrt * m_aspectRatio);
        var numCellsVertical = (int) math.round(numCellsSqrt / m_aspectRatio);
        var verticalStep = placementRegion.height / numCellsVertical;
        var horizontalStep = placementRegion.width / numCellsHorizontal;
        var scale0 = m_Rand.NextFloat(0.9f, 1.5f);
        var scale1 = m_Rand.NextFloat(0.9f, 1.5f);
        var scaleMin = math.min(scale0, scale1);
        var scaleMax = math.max(scale0, scale1);
        var cameraTf = camera.transform;
        var placementOrigin = new Vector3(placementRegion.x, placementRegion.y, 
            k_PlacementDistance + cameraTf.position.z);
        
        DatasetCapture.ReportMetric(m_ScaleRangeMetric, $@"[ {{ ""scaleMin"": {scaleMin}, ""scaleMax"": {scaleMax} }}]");

        var meshesToDraw = new NativeArray<MeshDrawInfo>(numCellsHorizontal * numCellsVertical * numFillPasses, Allocator.TempJob);
        using (s_PlaceBackgroundObjects.Auto())
        {
            // XXX: Rather than placing a large collection and then looking for gaps, we simply assume that a sufficiently
            //      dense background will not have gaps - rendering way more objects than necessary is still substantially
            //      faster than trying to read the render texture multiple times per frame
            new PlaceBackgroundObjectsJob()
            {
                PlacementOrigin = placementOrigin,
                Transformer = new WorldToScreenTransformer(camera),
                NumCellsHorizontal = numCellsHorizontal,
                NumCellsVertical = numCellsVertical,
                HorizontalStep = horizontalStep,
                VerticalStep = verticalStep,
                ForegroundSize = foregroundSizePixels,
                MeshInfos = meshInfos,
                MeshDrawInfos = meshesToDraw,
                TextureCount = statics.BackgroundImages.Length,
                Seed = m_Rand.NextUInt(),
                MinScale = scaleMin,
                MaxScale = scaleMax
            }.Schedule(numFillPasses, 1, inputDeps).Complete();
        }

        using (s_DrawMeshes.Auto())
        {
            var properties = new MaterialPropertyBlock();
            foreach (var meshDrawInfo in meshesToDraw)
            {
                var prefab = statics.BackgroundPrefabs[meshDrawInfo.MeshIndex];
                var sceneObject = objectCache.GetOrInstantiate(prefab);
                ObjectPlacementUtilities.CreateRandomizedHue(properties, backgroundHueMaxOffset, ref m_Rand);
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
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

        var numObjectsExpected = numCellsHorizontal * numCellsVertical * numFillPasses;
        if (numObjectsExpected != objectCache.NumObjectsActive)
        {
            Debug.LogWarning($"BackgroundGenerator should have placed {numObjectsExpected} but is only using " +
                             $"{objectCache.NumObjectsActive} from the cache.");
        }

        return inputDeps;
    }
    
    static MeshDrawInfo PlaceBackgroundObject(NativeArray<MeshInfo> meshInfos, WorldToScreenTransformer transformer,
        float foregroundObjectSize, Vector3 position,
        ref Random rand, float scaleMin, float scaleMax, int textureCount)
    {
        var meshIndex = rand.NextInt(0, meshInfos.Length);
        var meshInfo = meshInfos[meshIndex];

        // Rotate/resize object
        var rotation = rand.NextQuaternionRotation();
        var scaleRandom = rand.NextFloat(scaleMin, scaleMax);
        var scale = ObjectPlacementUtilities.ComputeScaleToMatchArea(transformer, position, rotation, meshInfo.Bounds,
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
}
