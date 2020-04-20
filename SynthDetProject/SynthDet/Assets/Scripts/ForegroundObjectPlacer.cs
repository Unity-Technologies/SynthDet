using System;
using System.Collections.Generic;
using System.Linq;
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
using UnityEngine.Perception;
using UnityEngine.Perception.Sensors;
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
    public const string k_ObjectInfoMetricGuid = "061E08CC-4428-4926-9933-A6732524B52B";
    const int k_OccludingLayerDistance = 4;

    static Unity.Profiling.ProfilerMarker s_ClearGameObjects = new Unity.Profiling.ProfilerMarker("ClearGameObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceObjects = new Unity.Profiling.ProfilerMarker("PlaceObjects");
    static Unity.Profiling.ProfilerMarker s_PlaceOccludingObjects = new Unity.Profiling.ProfilerMarker("PlaceOccludingObjects");
    static Unity.Profiling.ProfilerMarker s_CollectBounds = new Unity.Profiling.ProfilerMarker("CollectBounds");
    static Unity.Profiling.ProfilerMarker s_ComputePlacements = new Unity.Profiling.ProfilerMarker("ComputePlacements");
    static Unity.Profiling.ProfilerMarker s_SetupObjects = new Unity.Profiling.ProfilerMarker("SetupObjects");
    
    Random m_Rand;
    Rect m_ImageCoordinates;
    int m_ForegroundLayer;
    GameObject m_ParentForegound;
    GameObject m_ParentOccluding;
    int m_OccludingLayer;
    EntityQuery m_CurriculumQuery;
    EntityQuery m_ResourceDirectoriesQuery;

    // Initialize your operator here
    protected override void OnCreate()
    {
        m_Rand = new Random(1);
        m_ForegroundLayer = LayerMask.NameToLayer("Foreground");
        m_ParentForegound = new GameObject("ForegroundContainer");
        m_OccludingLayer = LayerMask.NameToLayer("Occluding");
        m_ParentOccluding = new GameObject("OccludingContainer");
        m_ParentOccluding.transform.SetPositionAndRotation(Vector3.back * k_OccludingLayerDistance, Quaternion.identity);
        
        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
        m_ResourceDirectoriesQuery = EntityManager.CreateEntityQuery(typeof(ResourceDirectories));
        
        m_CurriculumQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
        m_ObjectInfoDefinition = SimulationManager.RegisterMetricDefinition("object info", id: new Guid(k_ObjectInfoMetricGuid));
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
        var cameraObject = GameObject.Find("MainCamera");
        if (cameraObject == null)
            return inputDeps;
        
        Camera camera = cameraObject.GetComponent<Camera>();
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
        m_ImageCoordinates = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);

        using (s_ClearGameObjects.Auto())
        {
            for (int i = 0; i < m_ParentForegound.transform.childCount; i++)
            {
                var gameObject = m_ParentForegound.transform.GetChild(i).gameObject;
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

        var perceptionCamera = cameraObject.GetComponent<PerceptionCamera>();
        if (perceptionCamera != null)
            perceptionCamera.SetPersistentSensorData("scale", statics.ScaleFactors[curriculumState.ScaleIndex]);
        
        NativeList<PlacedObject> placedObjectBoundingBoxes;
        using (s_PlaceObjects.Auto())
            placedObjectBoundingBoxes = PlaceObjects(statics, ref curriculumState);

        ReportObjectStats(placedObjectBoundingBoxes, cameraObject);
        
        var occludingObjects = statics.BackgroundPrefabs;
        using (s_PlaceOccludingObjects.Auto())
            PlaceOccludingObjects(occludingObjects, statics.BackgroundImages, placedObjectBoundingBoxes);
        EntityManager.SetComponentData(singletonEntity, curriculumState);

        placedObjectBoundingBoxes.Dispose();
        
        return inputDeps;
    }

    struct ObjectState
    {
        [UsedImplicitly]
        public int label_id;
        [UsedImplicitly]
        public Vector3 rotation;
    }
    void ReportObjectStats(NativeList<PlacedObject> placedObjectBoundingBoxes, GameObject cameraObject)
    {
        var perceptionCamera = cameraObject.GetComponent<PerceptionCamera>();
        var objectStates = new JArray();
        for (int i = 0; i < placedObjectBoundingBoxes.Length; i++)
        {
            var placedObject = placedObjectBoundingBoxes[i];
            var labeling = m_ParentForegound.transform.GetChild(placedObject.PrefabIndex).gameObject.GetComponent<Labeling>();
            if (!perceptionCamera.LabelingConfiguration.TryGetMatchingConfigurationIndex(labeling, out int label_id))
                label_id = -1;

            var jObject = new JObject();
            jObject["label_id"] = label_id;
            var rotationEulerAngles = (float3)placedObject.Rotation.eulerAngles;
            jObject["rotation"] = $"[{rotationEulerAngles.x}, {rotationEulerAngles.y}, {rotationEulerAngles.z}]";
            objectStates.Add(jObject);
        }
        SimulationManager.ReportMetric(m_ObjectInfoDefinition, objectStates.ToString(Formatting.Indented));
    }

    struct PlacedObject : IEquatable<PlacedObject>
    {
        public int PrefabIndex;
        public float Scale;
        public Vector3 Position;
        public Rect BoundingBox;
        public Quaternion Rotation;
        public float ProjectedArea;

        public bool Equals(PlacedObject other)
        {
            return PrefabIndex == other.PrefabIndex && Scale.Equals(other.Scale) && Position.Equals(other.Position) && BoundingBox.Equals(other.BoundingBox) && Rotation.Equals(other.Rotation) && ProjectedArea.Equals(other.ProjectedArea);
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
                return hashCode;
            }
        }
    }

    struct NativePlacementStatics
    {
        public int ForegroundPrefabCount; 
        public NativeArray<Quaternion> InPlaneRotations;
        public NativeArray<Quaternion> OutOfPlaneRotations;
        public NativeArray<float> ScaleFactors;
    }

    [BurstCompile]
    unsafe struct ComputePlacementsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public CurriculumState* CurriculumStatePtr;
        [NativeDisableUnsafePtrRestriction]
        public Random* RandomPtr;
        [ReadOnly]
        public NativePlacementStatics NativePlacementStatics;
        [ReadOnly]
        public NativeArray<Bounds> ObjectBounds;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<PlacedObject> PlaceObjects;

        public Rect ImageCoordinates;
        public void Execute()
        {
            bool placedSuccessfully;
            do
            {
                var curriculumState = *CurriculumStatePtr;
                var bounds = ObjectBounds[curriculumState.PrefabIndex];
                var scale = 
                    ObjectPlacementUtilities.ComputeForegroundScaling(bounds, NativePlacementStatics.ScaleFactors[curriculumState.ScaleIndex]);
                var rotation = ObjectPlacementUtilities.ComposeForegroundRotation(curriculumState,
                    NativePlacementStatics.OutOfPlaneRotations, NativePlacementStatics.InPlaneRotations);
                var projectedArea = ObjectPlacementUtilities.ComputeProjectedArea(rotation, bounds, scale);
                var placedObject = new PlacedObject
                {
                    Scale = scale, 
                    Rotation = rotation, 
                    PrefabIndex = curriculumState.PrefabIndex,
                    ProjectedArea = projectedArea
                };
                placedSuccessfully = false;
                int count = 0;
                for (int i = 0; i < 100; i++)
                {
                    placedObject.Position = new Vector3(RandomPtr->NextFloat(ImageCoordinates.xMin, ImageCoordinates.xMax),
                        RandomPtr->NextFloat(ImageCoordinates.yMin, ImageCoordinates.yMax), 0f);

                    placedObject.BoundingBox = GetBoundingBox(bounds, placedObject.Scale, placedObject.Position, placedObject.Rotation);
                    var cropping = CalculateCropping(placedObject.BoundingBox, ImageCoordinates);
                    var passedOverlap = ValidateOverlap(placedObject.BoundingBox, PlaceObjects);
                    if ((cropping <= .5 && passedOverlap))
                    {
                        placedSuccessfully = true;
                        PlaceObjects.Add(placedObject);
                        *CurriculumStatePtr = NextCurriculumState(curriculumState, NativePlacementStatics);

                        break;
                    }
                }

                //If it can’t be placed within the scene due to violations of the cropping or overlap
                //constraints we stop processing the current foreground scene
                //and start with the next one.
            } while (placedSuccessfully);
        }
    }
    
    unsafe NativeList<PlacedObject> PlaceObjects(PlacementStatics statics, ref CurriculumState curriculumState)
    {
        var placedObjectBoundingBoxes = new NativeList<PlacedObject>(500, Allocator.TempJob);
        var objectBounds = ComputeObjectBounds(statics.ForegroundPrefabs);

        var localCurriculumState = curriculumState;
        var curriculumStatePtr = (CurriculumState*) UnsafeUtility.AddressOf(ref localCurriculumState);
        var localRandom = m_Rand;
        var randomPtr = (Random*) UnsafeUtility.AddressOf(ref localRandom);
        
        using (s_ComputePlacements.Auto())
        {
            var computePlacementsJob = new ComputePlacementsJob()
            {
                CurriculumStatePtr = curriculumStatePtr,
                ImageCoordinates = m_ImageCoordinates,
                ObjectBounds = objectBounds,
                PlaceObjects = placedObjectBoundingBoxes,
                RandomPtr = randomPtr,
                NativePlacementStatics = new NativePlacementStatics
                {   
                    ForegroundPrefabCount = statics.ForegroundPrefabs.Length,
                    InPlaneRotations = statics.InPlaneRotations,
                    OutOfPlaneRotations = statics.OutOfPlaneRotations,
                    ScaleFactors = statics.ScaleFactors
                }
            };
            computePlacementsJob.Run();
            curriculumState = *computePlacementsJob.CurriculumStatePtr;
            m_Rand = *computePlacementsJob.RandomPtr;
        }

        using (s_SetupObjects.Auto())
        {
            int objectGroupIndex = 0;
            foreach (var placedObject in placedObjectBoundingBoxes)
            {
                EnsureObjectGroupsExist(statics, objectGroupIndex);
                var gameObject = m_ParentForegound.transform.GetChild(placedObject.PrefabIndex + objectGroupIndex * statics.ForegroundPrefabs.Length).gameObject;

                gameObject.transform.localRotation = placedObject.Rotation;
                    
                gameObject.transform.localScale =
                    Vector3.one * placedObject.Scale;
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
                ObjectPlacementUtilities.GetMeshAndMaterial(prefabs[i], out _, out Mesh objectMesh);
                objectBounds[i] = objectMesh.bounds;
            }
        }

        return objectBounds;
    }

    static Regex s_NameRegex = new Regex(".*_[0-9][0-9]");

    void EnsureObjectGroupsExist(PlacementStatics statics, int objectGroupIndex)
    {
        while (m_ParentForegound.transform.childCount < statics.ForegroundPrefabs.Length * (objectGroupIndex + 1))
        {
            foreach (var foregroundPrefab in statics.ForegroundPrefabs)
            {
                var gameObject = GameObject.Instantiate(foregroundPrefab, m_ParentForegound.transform);
                gameObject.layer = m_ForegroundLayer;
                var labeling = gameObject.AddComponent<Labeling>();
                var name = foregroundPrefab.name;
                if (s_NameRegex.IsMatch(name))
                    name = name.Substring(0, name.Length - 3);
                
                labeling.classes.Add(name);
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

    public static float ComputeOverlap(Rect rect, Rect other)
    {
        float xMin = Math.Max(other.xMin, rect.xMin);
        float xMax = Math.Min(other.xMax, rect.xMax);
        float yMin = Math.Max(other.yMin, rect.yMin);
        float yMax = Math.Min(other.yMax, rect.yMax);
        float totalArea = rect.width * rect.height;
        float inFrameArea = math.max(0, xMax - xMin) * math.max(0, yMax - yMin);

        float cropping = inFrameArea / totalArea;
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
    
    GameObjectOneWayCache occludingObjectCache;
    MetricDefinition m_ObjectInfoDefinition;

    void PlaceOccludingObjects(GameObject[] objectsToPlace, Texture2D[] textures, NativeList<PlacedObject> placedObjects)
    {
        if (occludingObjectCache == null)
            occludingObjectCache = new GameObjectOneWayCache(m_ParentOccluding.transform, objectsToPlace);
        
        occludingObjectCache.ResetAllObjects();
        var occludingObjectBounds = ComputeObjectBounds(objectsToPlace);
        
        var materialPropertyBlock = new MaterialPropertyBlock();
        var placedOccludingObjects = new NativeArray<PlacedObject>(placedObjects.Length, Allocator.TempJob);
        using (s_ComputePlacements.Auto())
        {
            var job = new ComputeOccludingObjectPlacements()
            {
                OccludingObjectBounds = occludingObjectBounds,
                PlacedForegoundObjects = placedObjects,
                RandomSeed = m_Rand.NextUInt(),
                PlacedOccludingObjects = placedOccludingObjects
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
                var objectToPlace = occludingObjectCache.GetOrInstantiate(prefab);
                objectToPlace.layer = m_OccludingLayer;
                ObjectPlacementUtilities.GetMeshAndMaterial(objectToPlace, out Material material, out Mesh objectMesh);
                
                var meshRenderer = objectToPlace.GetComponentInChildren<MeshRenderer>();
                meshRenderer.GetPropertyBlock(materialPropertyBlock);
                ObjectPlacementUtilities.CreateRandomizedHue(materialPropertyBlock, ref m_Rand);
                materialPropertyBlock.SetTexture("_BaseMap", textures[m_Rand.NextInt(textures.Length)]);
                meshRenderer.SetPropertyBlock(materialPropertyBlock);

                objectToPlace.transform.localPosition = placedOccludingObject.Position;
                objectToPlace.transform.localRotation = placedOccludingObject.Rotation;
                objectToPlace.transform.localScale = Vector3.one * placedOccludingObject.Scale;
            }
        }
        placedOccludingObjects.Dispose();
        occludingObjectBounds.Dispose();
    }

    [BurstCompile]
    struct ComputeOccludingObjectPlacements : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        public NativeArray<Bounds> OccludingObjectBounds;
        [ReadOnly]
        public NativeArray<PlacedObject> PlacedForegoundObjects;
        public uint RandomSeed;
        public NativeArray<PlacedObject> PlacedOccludingObjects;

        public void Execute(int index)
        {
            var foregroundObjectBox = PlacedForegoundObjects[index];
            // See comment regarding Random in jobs in BackgroundGenerator.PlaceObjectsJob
            var rand = new Random(RandomSeed + (uint)index * ObjectPlacementUtilities.LargePrimeNumber);
            
            int prefabIndex = rand.NextInt(OccludingObjectBounds.Length);
            var bounds = OccludingObjectBounds[prefabIndex];
            //TODO: Use more accurate area computation
            var foregroundObjectBoundingBox = foregroundObjectBox.BoundingBox;
                    
            //place over a foreground object such that overlap is between 10%-30%
            int numTries = 0;
            PlacedOccludingObjects[index] = new PlacedObject() { PrefabIndex = -1 };
            while (numTries < 1000)
            {
                numTries++;
                var rotation = rand.NextQuaternionRotation();
                var scale = ObjectPlacementUtilities.ComputeScaleToMatchArea(rotation, bounds,
                    rand.NextFloat(0.2f, 0.3f) * foregroundObjectBox.ProjectedArea);
                var position = new Vector3(rand.NextFloat(foregroundObjectBoundingBox.xMin, foregroundObjectBoundingBox.xMax),
                    rand.NextFloat(foregroundObjectBoundingBox.yMin, foregroundObjectBoundingBox.yMax), 0f);
                var placedObject = new PlacedObject()
                {
                    Scale = scale,
                    Rotation = rotation,
                    Position = position,
                    PrefabIndex = prefabIndex
                };
                Rect occludingObjectBox = GetBoundingBox(bounds, scale, position, rotation);
                float cropping = ComputeOverlap(foregroundObjectBoundingBox, occludingObjectBox);
                if (cropping >= 0.10 && cropping <= 0.30)
                {
                    PlacedOccludingObjects[index] = placedObject;
                    return;
                }
            }
        }
    }
}
