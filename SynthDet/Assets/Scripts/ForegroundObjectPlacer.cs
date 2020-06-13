using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Random = Unity.Mathematics.Random;
using Vector3 = UnityEngine.Vector3;


public struct ResourceDirectories : IComponentData
{
    public NativeString64 ForegroundResourcePath;
    public NativeString64 BackgroundResourcePath;
}

unsafe public class ForegroundObjectPlacer : JobComponentSystem
{
    public const string k_ForegroundPlacementInfoMetricGuid = "061E08CC-4428-4926-9933-A6732524B52B";
    internal const int k_OccludingLayerDistance = 8;
    internal const int k_ForegroundLayerDistance = 10;

    static Unity.Profiling.ProfilerMarker s_ClearGameObjects = new Unity.Profiling.ProfilerMarker("ClearGameObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceObjects = new Unity.Profiling.ProfilerMarker("PlaceObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceOccludingObjects = new Unity.Profiling.ProfilerMarker("PlaceOccludingObjects");
    static Unity.Profiling.ProfilerMarker s_CollectBounds = new Unity.Profiling.ProfilerMarker("CollectBounds");
    static Unity.Profiling.ProfilerMarker s_ComputePlacements = new Unity.Profiling.ProfilerMarker("ComputePlacements");
    static Unity.Profiling.ProfilerMarker s_SetupObjects = new Unity.Profiling.ProfilerMarker("SetupObjects");

    Random m_Rand;
    GameObject m_CameraContainer;
    int m_ForegroundLayer;
    GameObject m_ParentForeground;
    GameObject m_ParentOccluding;
    GameObject m_ParentBackgroundInForeground;
    int m_OccludingLayer;
    EntityQuery m_CurriculumQuery;
    EntityQuery m_ResourceDirectoriesQuery;
    
    GameObjectOneWayCache m_OccludingObjectCache;
    GameObjectOneWayCache m_BackgroundInForegroundObjectCache;
    MetricDefinition m_ForegroundPlacementInfoDefinition;

    // Initialize your operator here
    protected override void OnCreate()
    {
        m_Rand = new Random(1);
        m_ForegroundLayer = LayerMask.NameToLayer("Foreground");
        m_ParentForeground = new GameObject("ForegroundContainer");
        m_ParentBackgroundInForeground = new GameObject("BackgroundInForegroundContainer");
        m_OccludingLayer = LayerMask.NameToLayer("Occluding");
        m_ParentOccluding = new GameObject("OccludingContainer");

        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
        m_ResourceDirectoriesQuery = EntityManager.CreateEntityQuery(typeof(ResourceDirectories));

        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
        m_ForegroundPlacementInfoDefinition = SimulationManager.RegisterMetricDefinition("foreground placement info", description: "Info about each object placed in the foreground layer. Currently only includes label and orientation.",id: new Guid(k_ForegroundPlacementInfoMetricGuid));
    }
#if UNITY_EDITOR
    public void LoadAndStartRenderDocCapture(out UnityEditor.EditorWindow gameView)
    {
        UnityEditorInternal.RenderDoc.Load();
        System.Reflection.Assembly assembly = typeof(UnityEditor.EditorWindow).Assembly;
        Type type = assembly.GetType("UnityEditor.GameView");
        gameView = UnityEditor.EditorWindow.GetWindow(type);
        UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(gameView);
    }
#endif


    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (m_CameraContainer == null)
        {
            m_CameraContainer = GameObject.Find("MainCamera");
            if (m_CameraContainer == null)
                return inputDeps;
            var tfCamera = m_CameraContainer.transform;
            m_ParentForeground.transform.SetPositionAndRotation(
                tfCamera.position + tfCamera.forward * k_ForegroundLayerDistance, Quaternion.identity);
            m_ParentBackgroundInForeground.transform.SetPositionAndRotation(
                tfCamera.position + tfCamera.forward * k_ForegroundLayerDistance, Quaternion.identity);
            m_ParentOccluding.transform.SetPositionAndRotation(
                tfCamera.position + tfCamera.forward * k_OccludingLayerDistance, Quaternion.identity);
        }

        using (s_ClearGameObjects.Auto())
        {
            for (var i = 0; i < m_ParentForeground.transform.childCount; i++)
            {
                var gameObject = m_ParentForeground.transform.GetChild(i).gameObject;
                ObjectPlacementUtilities.SetMeshRenderersEnabledRecursive(gameObject, false);
            }
        }

        if (m_ResourceDirectoriesQuery.CalculateEntityCount() != 1)
            return inputDeps;

        var singletonEntity = m_CurriculumQuery.GetSingletonEntity();
        var curriculumState = EntityManager.GetComponentData<CurriculumState>(singletonEntity);
        var statics = EntityManager.GetComponentObject<PlacementStatics>(singletonEntity);

        if (statics.ForegroundPrefabs == null || statics.ForegroundPrefabs.Length == 0)
            return inputDeps;
        if (statics.BackgroundPrefabs == null || statics.BackgroundPrefabs.Length == 0)
            return inputDeps;

        if (curriculumState.ScaleIndex >= statics.ScaleFactors.Length)
            return inputDeps;

        var perceptionCamera = m_CameraContainer.GetComponent<PerceptionCamera>();
        if (perceptionCamera != null)
            perceptionCamera.SetPersistentSensorData("scale", statics.ScaleFactors[curriculumState.ScaleIndex]);

        var camera = m_CameraContainer.GetComponent<Camera>();
        NativeList<PlacedObject> placedObjectBoundingBoxes;
        var occludingObjects = statics.BackgroundPrefabs;
        var occludingObjectBounds = ComputeObjectBounds(occludingObjects);
        
        if (m_OccludingObjectCache == null)
            m_OccludingObjectCache = new GameObjectOneWayCache(m_ParentOccluding.transform, occludingObjects);
        if (m_BackgroundInForegroundObjectCache == null)
            m_BackgroundInForegroundObjectCache = new GameObjectOneWayCache(m_ParentBackgroundInForeground.transform, occludingObjects);

        m_OccludingObjectCache.ResetAllObjects();
        m_BackgroundInForegroundObjectCache.ResetAllObjects();
        
        using (s_PlaceObjects.Auto())
            placedObjectBoundingBoxes = PlaceObjects(camera, statics, occludingObjectBounds, ref curriculumState);

        ReportObjectStats(placedObjectBoundingBoxes, m_CameraContainer);
        
        using (s_PlaceOccludingObjects.Auto())
        {
            PlaceOccludingObjects(occludingObjects, occludingObjectBounds, camera, statics, placedObjectBoundingBoxes);
        }

        EntityManager.SetComponentData(singletonEntity, curriculumState);

        placedObjectBoundingBoxes.Dispose();

        return inputDeps;
    }
    
    void ReportObjectStats(NativeList<PlacedObject> placedObjectBoundingBoxes, GameObject cameraObject)
    {
        var perceptionCamera = cameraObject.GetComponent<PerceptionCamera>();
        var objectStates = new JArray();
        for (int i = 0; i < placedObjectBoundingBoxes.Length; i++)
        {
            var placedObject = placedObjectBoundingBoxes[i];
            var labeling = m_ParentForeground.transform.GetChild(placedObject.PrefabIndex).gameObject.GetComponent<Labeling>();
            int labelId;
            if (perceptionCamera.LabelingConfiguration.TryGetMatchingConfigurationEntry(labeling, out LabelEntry labelEntry))
                labelId = labelEntry.id;
            else
                labelId = -1;

            var jObject = new JObject();
            jObject["label_id"] = labelId;
            var rotationEulerAngles = (float3)placedObject.Rotation.eulerAngles;
            jObject["rotation"] = new JRaw($"[{rotationEulerAngles.x}, {rotationEulerAngles.y}, {rotationEulerAngles.z}]");
            objectStates.Add(jObject);
        }
        SimulationManager.ReportMetric(m_ForegroundPlacementInfoDefinition, objectStates.ToString(Formatting.Indented));
    }

    struct PlacedObject : IEquatable<PlacedObject>
    {
        public int PrefabIndex;
        public float Scale;
        public Vector3 Position;
        public Rect BoundingBox;
        public Quaternion Rotation;
        public float ProjectedArea;
        public bool IsOccluding;

        public bool Equals(PlacedObject other)
        {
            return PrefabIndex == other.PrefabIndex && Scale.Equals(other.Scale) && Position.Equals(other.Position) && BoundingBox.Equals(other.BoundingBox) && Rotation.Equals(other.Rotation) && ProjectedArea.Equals(other.ProjectedArea) && IsOccluding == other.IsOccluding;
        }

        public override bool Equals(object obj)
        {
            return obj is PlacedObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = PrefabIndex;
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ Position.GetHashCode();
                hashCode = (hashCode * 397) ^ BoundingBox.GetHashCode();
                hashCode = (hashCode * 397) ^ Rotation.GetHashCode();
                hashCode = (hashCode * 397) ^ ProjectedArea.GetHashCode();
                hashCode = (hashCode * 397) ^ IsOccluding.GetHashCode();
                return hashCode;
            }
        }
    }

    struct NativePlacementStatics
    {
        public int ForegroundPrefabCount;
        public int MaxForegroundObjects;
        public NativeArray<Quaternion> InPlaneRotations;
        public NativeArray<Quaternion> OutOfPlaneRotations;
        public NativeArray<float> ScaleFactors;
        public float BackgroundObjectInForegroundChance;
    }

    [BurstCompile]
    struct ComputePlacementsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public CurriculumState* CurriculumStatePtr;
        [NativeDisableUnsafePtrRestriction]
        public Random* RandomPtr;
        [ReadOnly]
        public NativePlacementStatics NativePlacementStatics;
        [ReadOnly]
        public NativeArray<Bounds> ObjectBounds;
        [ReadOnly]
        public NativeArray<Bounds> OccludingObjectBounds;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PlacedObject> PlaceObjects;

        public WorldToScreenTransformer Transformer;
        public Rect ImageCoordinates;
        public void Execute()
        {
            bool placedSuccessfully;
            do
            {
                var curriculumState = *CurriculumStatePtr;
                var placeOccluding = RandomPtr->NextFloat() < NativePlacementStatics.BackgroundObjectInForegroundChance;
                
                Bounds bounds;
                int prefabIndex;
                if (placeOccluding)
                {
                    prefabIndex = RandomPtr->NextInt(0, OccludingObjectBounds.Length - 1);
                    bounds = OccludingObjectBounds[prefabIndex];
                }
                else
                {
                    prefabIndex = curriculumState.PrefabIndex;
                    bounds = ObjectBounds[prefabIndex];
                }

                var scale =
                    ObjectPlacementUtilities.ComputeForegroundScaling(bounds, NativePlacementStatics.ScaleFactors[curriculumState.ScaleIndex]);
                var rotation = ObjectPlacementUtilities.ComposeForegroundRotation(curriculumState,
                    NativePlacementStatics.OutOfPlaneRotations, NativePlacementStatics.InPlaneRotations);
                var placedObject = new PlacedObject
                {
                    Scale = scale,
                    Rotation = rotation,
                    PrefabIndex = prefabIndex,
                    IsOccluding = placeOccluding
                };
                placedSuccessfully = false;
                for (var i = 0; i < 100; i++)
                {
                    placedObject.Position = new Vector3(RandomPtr->NextFloat(ImageCoordinates.xMin, ImageCoordinates.xMax),
                        RandomPtr->NextFloat(ImageCoordinates.yMin, ImageCoordinates.yMax), 0f);
                    placedObject.ProjectedArea = ObjectPlacementUtilities.ComputeProjectedArea(
                        Transformer, placedObject.Position, rotation, bounds, scale);

                    placedObject.BoundingBox = GetBoundingBox(bounds, placedObject.Scale, placedObject.Position, placedObject.Rotation);
                    var cropping = CalculateCropping(placedObject.BoundingBox, ImageCoordinates);
                    var passedOverlap = ValidateOverlap(placedObject.BoundingBox, PlaceObjects);
                    if ((cropping <= .5 && passedOverlap))
                    {
                        placedSuccessfully = true;
                        PlaceObjects.Add(placedObject);
                        if (!placeOccluding)
                            *CurriculumStatePtr = NextCurriculumState(curriculumState, NativePlacementStatics);

                        break;
                    }
                }

                // If it can’t be placed within the scene due to violations of the cropping or overlap
                // constraints we stop processing the current foreground scene
                // and start with the next one.
            } while (placedSuccessfully && PlaceObjects.Length < NativePlacementStatics.MaxForegroundObjects);
        }
    }
    
    NativeList<PlacedObject> PlaceObjects(Camera camera, PlacementStatics statics, NativeArray<Bounds> occludingObjectBounds, ref CurriculumState curriculumState)
    {
        var placedObjectBoundingBoxes = new NativeList<PlacedObject>(500, Allocator.TempJob);
        var objectBounds = ComputeObjectBounds(statics.ForegroundPrefabs);

        var localCurriculumState = curriculumState;
        var curriculumStatePtr = (CurriculumState*) UnsafeUtility.AddressOf(ref localCurriculumState);
        var localRandom = m_Rand;
        var randomPtr = (Random*) UnsafeUtility.AddressOf(ref localRandom);
        var placementRegion = ObjectPlacementUtilities.ComputePlacementRegion(camera, k_ForegroundLayerDistance);
        
        using (s_ComputePlacements.Auto())
        {
            var computePlacementsJob = new ComputePlacementsJob()
            {
                CurriculumStatePtr = curriculumStatePtr,
                Transformer = new WorldToScreenTransformer(camera),
                ImageCoordinates = placementRegion,
                ObjectBounds = objectBounds,
                OccludingObjectBounds = occludingObjectBounds,
                PlaceObjects = placedObjectBoundingBoxes,
                RandomPtr = randomPtr,
                NativePlacementStatics = new NativePlacementStatics
                {
                    ForegroundPrefabCount = statics.ForegroundPrefabs.Length,
                    MaxForegroundObjects = statics.MaxForegroundObjectsPerFrame,
                    InPlaneRotations = statics.InPlaneRotations,
                    OutOfPlaneRotations = statics.OutOfPlaneRotations,
                    ScaleFactors = statics.ScaleFactors,
                    BackgroundObjectInForegroundChance = statics.BackgroundObjectInForegroundChance,
                }
            };
            computePlacementsJob.Run();
            curriculumState = *computePlacementsJob.CurriculumStatePtr;
            m_Rand = *computePlacementsJob.RandomPtr;
        }

        using (s_SetupObjects.Auto())
        {
            var materialPropertyBlock = new MaterialPropertyBlock();
            int objectGroupIndex = 0;
            foreach (var placedObject in placedObjectBoundingBoxes)
            {
                GameObject gameObject;
                if (placedObject.IsOccluding)
                {
                    gameObject = m_BackgroundInForegroundObjectCache.GetOrInstantiate(statics.BackgroundPrefabs[placedObject.PrefabIndex]);
                    
                    var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
                    meshRenderer.GetPropertyBlock(materialPropertyBlock);
                    ObjectPlacementUtilities.CreateRandomizedHue(materialPropertyBlock, statics.OccludingHueMaxOffset, ref m_Rand);
                    materialPropertyBlock.SetTexture("_BaseMap", statics.BackgroundImages[m_Rand.NextInt(statics.BackgroundImages.Length)]);
                    meshRenderer.SetPropertyBlock(materialPropertyBlock);
                }
                else
                {
                    EnsureObjectGroupsExist(statics, objectGroupIndex);
                    gameObject = m_ParentForeground.transform.GetChild(placedObject.PrefabIndex + objectGroupIndex * statics.ForegroundPrefabs.Length).gameObject;
                }

                gameObject.transform.localRotation = placedObject.Rotation;
                gameObject.transform.localScale = Vector3.one * placedObject.Scale;
                gameObject.transform.localPosition = placedObject.Position;

                ObjectPlacementUtilities.SetMeshRenderersEnabledRecursive(gameObject, true);

                if (placedObject.PrefabIndex == statics.ForegroundPrefabs.Length - 1)
                    objectGroupIndex++;
            }
        }

        objectBounds.Dispose();

        return placedObjectBoundingBoxes;
    }

    static NativeArray<Bounds> ComputeObjectBounds(GameObject[] prefabs)
    {
        var objectBounds = new NativeArray<Bounds>(prefabs.Length, Allocator.TempJob);
        using (s_CollectBounds.Auto())
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                var bounds = ObjectPlacementUtilities.ComputeBounds(prefabs[i]);
                objectBounds[i] = bounds;
            }
        }

        return objectBounds;
    }

    static Regex s_NameRegex = new Regex(".*_[0-9][0-9]");

    void EnsureObjectGroupsExist(PlacementStatics statics, int objectGroupIndex)
    {
        while (m_ParentForeground.transform.childCount < statics.ForegroundPrefabs.Length * (objectGroupIndex + 1))
        {
            foreach (var foregroundPrefab in statics.ForegroundPrefabs)
            {
                var gameObject = Object.Instantiate(foregroundPrefab, m_ParentForeground.transform);
                gameObject.layer = m_ForegroundLayer;
                ObjectPlacementUtilities.SetMeshRenderersEnabledRecursive(gameObject, false);
                var labeling = gameObject.AddComponent<Labeling>();
                var name = foregroundPrefab.name;
                if (s_NameRegex.IsMatch(name))
                    name = name.Substring(0, name.Length - 3);

                labeling.labels.Add(name);
            }
        }
    }

    static CurriculumState NextCurriculumState(CurriculumState curriculumState, NativePlacementStatics statics)
    {
        curriculumState.PrefabIndex++;
        if (curriculumState.PrefabIndex < statics.ForegroundPrefabCount)
            return curriculumState;

        curriculumState.PrefabIndex = 0;
        curriculumState.OutOfPlaneRotationIndex++;
        if (curriculumState.OutOfPlaneRotationIndex < statics.OutOfPlaneRotations.Length)
            return curriculumState;

        curriculumState.OutOfPlaneRotationIndex = 0;
        curriculumState.InPlaneRotationIndex++;
        if (curriculumState.InPlaneRotationIndex < statics.InPlaneRotations.Length)
            return curriculumState;

        curriculumState.InPlaneRotationIndex = 0;
        curriculumState.ScaleIndex++;

        return curriculumState;
    }

    [BurstCompile]
    static bool ValidateOverlap(Rect boundingBox, NativeList<PlacedObject> placedObjects)
    {
        for (var i = 0; i < placedObjects.Length; i++)
        {
            var placedObject = placedObjects[i];
            var overlap = ComputeOverlap(boundingBox, placedObject.BoundingBox);
            if (overlap > .3f)
                return false;
        }

        return true;
    }

    static float CalculateCropping(Rect boundingBox, Rect imageCoordinates)
    {
        var cropping = 1 - ComputeOverlap(boundingBox, imageCoordinates);

        return  cropping;
    }

    internal static float ComputeOverlap(Rect rect, Rect other)
    {
        float xMin = Math.Max(other.xMin, rect.xMin);
        float xMax = Math.Min(other.xMax, rect.xMax);
        float yMin = Math.Max(other.yMin, rect.yMin);
        float yMax = Math.Min(other.yMax, rect.yMax);
        float inFrameArea = math.max(0, xMax - xMin) * math.max(0, yMax - yMin);

        float totalAreaRect = rect.width * rect.height;
        float totalAreaOther = other.width * other.height;
        float cropping = inFrameArea / Math.Min(totalAreaRect, totalAreaOther);
        return cropping;
    }

    public static Rect GetBoundingBox(Bounds bounds, float scale, Vector3 position, Quaternion rotation)
    {
        var aabb = bounds.ToAABB();

        aabb = AABB.Transform(float4x4.TRS(position, rotation, scale), aabb);
        float xObjectMax = aabb.Max.x;
        float xObjectMin = aabb.Min.x;
        float yObjectMax = aabb.Max.y;
        float yObjectMin = aabb.Min.y;
        var objectRect = Rect.MinMaxRect(xObjectMin, yObjectMin, xObjectMax, yObjectMax);
        return objectRect;
    }

    void PlaceOccludingObjects(
        GameObject[] objectsToPlace, NativeArray<Bounds> objectBounds, Camera camera, PlacementStatics statics, NativeList<PlacedObject> placedObjects)
    {
        var textures = statics.BackgroundImages;

        var materialPropertyBlock = new MaterialPropertyBlock();
        var placedOccludingObjects = new NativeArray<PlacedObject>(placedObjects.Length, Allocator.TempJob);
        var placementRegion = ObjectPlacementUtilities.ComputePlacementRegion(camera, k_OccludingLayerDistance);
        
        using (s_ComputePlacements.Auto())
        {
            var job = new ComputeOccludingObjectPlacements()
            {
                OccludingObjectBounds = objectBounds,
                ImageCoordinates = placementRegion,
                Transformer = new WorldToScreenTransformer(camera),
                PlacedForegroundObjects = placedObjects,
                RandomSeed = m_Rand.NextUInt(),
                PlacedOccludingObjects = placedOccludingObjects,
                ScalingMin = statics.ScalingMin,
                ScalingSize = statics.ScalingSize
            };
            job.Schedule(placedObjects.Length, 10).Complete();
        }

        using (s_SetupObjects.Auto())
        {
            foreach (var placedOccludingObject in placedOccludingObjects)
            {
                if (placedOccludingObject.PrefabIndex < 0)
                    continue;

                var prefab = objectsToPlace[placedOccludingObject.PrefabIndex];
                var objectToPlace = m_OccludingObjectCache.GetOrInstantiate(prefab);
                objectToPlace.layer = m_OccludingLayer;

                var meshRenderer = objectToPlace.GetComponentInChildren<MeshRenderer>();
                meshRenderer.GetPropertyBlock(materialPropertyBlock);
                ObjectPlacementUtilities.CreateRandomizedHue(materialPropertyBlock, statics.OccludingHueMaxOffset, ref m_Rand);
                materialPropertyBlock.SetTexture("_BaseMap", textures[m_Rand.NextInt(textures.Length)]);
                meshRenderer.SetPropertyBlock(materialPropertyBlock);

                objectToPlace.transform.localPosition = placedOccludingObject.Position;
                objectToPlace.transform.localRotation = placedOccludingObject.Rotation;
                objectToPlace.transform.localScale = Vector3.one * placedOccludingObject.Scale;
            }
        }
        placedOccludingObjects.Dispose();
        objectBounds.Dispose();
    }

    [BurstCompile]
    struct ComputeOccludingObjectPlacements : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        public NativeArray<Bounds> OccludingObjectBounds;
        [ReadOnly]
        public NativeArray<PlacedObject> PlacedForegroundObjects;
        public Rect ImageCoordinates;
        public WorldToScreenTransformer Transformer;
        public uint RandomSeed;
        public NativeArray<PlacedObject> PlacedOccludingObjects;
        public float ScalingMin;
        public float ScalingSize;

        public void Execute(int index)
        {
            var foregroundObjectBox = PlacedForegroundObjects[index];
            // See comment regarding Random in jobs in BackgroundGenerator.PlaceObjectsJob
            var rand = new Random(RandomSeed + (uint)index * ObjectPlacementUtilities.LargePrimeNumber);
            
            var prefabIndex = rand.NextInt(OccludingObjectBounds.Length);
            var bounds = OccludingObjectBounds[prefabIndex];
            var foregroundObjectBoundingBox = ObjectPlacementUtilities.IntersectRect(
                foregroundObjectBox.BoundingBox, ImageCoordinates);

            //place over a foreground object such that overlap is between 10%-30%
            var numTries = 0;
            PlacedOccludingObjects[index] = new PlacedObject() { PrefabIndex = -1 };
            while (numTries < 1000)
            {
                numTries++;
                var rotation = rand.NextQuaternionRotation();
                var position = new Vector3(rand.NextFloat(foregroundObjectBoundingBox.xMin, foregroundObjectBoundingBox.xMax),
                    rand.NextFloat(foregroundObjectBoundingBox.yMin, foregroundObjectBoundingBox.yMax), 0f);
                var scale = ObjectPlacementUtilities.ComputeScaleToMatchArea(Transformer, position, rotation, bounds,
                    rand.NextFloat(ScalingMin, ScalingMin+ScalingSize) * foregroundObjectBox.ProjectedArea);
                var placedObject = new PlacedObject()
                {
                    Scale = scale,
                    Rotation = rotation,
                    Position = position,
                    PrefabIndex = prefabIndex
                };
                // NOTE: This computation is done with orthographic projection and will be slightly inaccurate
                //       when rendering with perspective projection
                var occludingObjectBox = GetBoundingBox(bounds, scale, position, rotation);
                var cropping = ComputeOverlap(foregroundObjectBoundingBox, occludingObjectBox);
                if (cropping >= 0.10 && cropping <= 0.30)
                {
                    PlacedOccludingObjects[index] = placedObject;
                    return;
                }
            }
        }
    }

    public PlacementStatics Parameters { get; set; }
}
