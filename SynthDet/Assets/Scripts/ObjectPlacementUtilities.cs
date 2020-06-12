using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;

public struct CurriculumState : IComponentData
{
    public int ScaleIndex;
    public int OutOfPlaneRotationIndex;
    public int InPlaneRotationIndex;
    public int PrefabIndex;
}

public class PlacementStatics : Component
{
    public readonly int MaxFrames;
    public readonly float ScalingMin;
    public readonly float ScalingSize;
    public readonly float OccludingHueMaxOffset;
    public readonly int MaxForegroundObjectsPerFrame;
    public readonly GameObject[] ForegroundPrefabs;
    public readonly GameObject[] BackgroundPrefabs;
    public readonly Texture2D[] BackgroundImages;
    public readonly NativeArray<Quaternion> InPlaneRotations;
    public readonly NativeArray<Quaternion> OutOfPlaneRotations;
    public readonly NativeArray<float> ScaleFactors;

    public PlacementStatics(int maxFrames, int maxForegroundObjectsPerFrame, float scalingMin, float scalingSize, float occludingHueMaxOffset, GameObject[] foreground, GameObject[] backgroundPrefabs, Texture2D[] backgroundImages, NativeArray<Quaternion> inPlaneRot, NativeArray<Quaternion> outPlaneRot, NativeArray<float> scaleFactors)
    {
        MaxFrames = maxFrames;
        ForegroundPrefabs = foreground;
        BackgroundPrefabs = backgroundPrefabs;
        InPlaneRotations = inPlaneRot;
        OutOfPlaneRotations = outPlaneRot;
        ScaleFactors = scaleFactors;
        ScalingMin = scalingMin;
        ScalingSize = scalingSize;
        OccludingHueMaxOffset = occludingHueMaxOffset;
        MaxForegroundObjectsPerFrame = maxForegroundObjectsPerFrame;
        BackgroundImages = backgroundImages;
    }
}

public struct WorldToScreenTransformer
{
    Matrix4x4 m_FromWorldToClip;
    Vector2 m_ScreenResolution;

    public WorldToScreenTransformer(Camera camera)
    {
        m_FromWorldToClip = camera.projectionMatrix * camera.worldToCameraMatrix;
        m_ScreenResolution = new Vector2(Screen.width, Screen.height);
    }

    public Vector2 TransformPoint(Vector3 point)
    {
        var positionClip = m_FromWorldToClip.MultiplyPoint(point);
        var positionNdc = ((Vector2)positionClip + Vector2.one) / 2f;
        return positionNdc * m_ScreenResolution;
    }
}

public static class ObjectPlacementUtilities
{
    /// <summary>
    /// A large prime number used to mix random seeds
    /// </summary>
    public const uint LargePrimeNumber = 0x9F6ABC1;

    public static void GetMeshAndMaterial(GameObject objectToRender, out Material material, out Mesh meshToDraw)
    {
        material = objectToRender.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        meshToDraw = objectToRender.GetComponentInChildren<MeshFilter>().sharedMesh;
    }

    public static float ComputeAreaOfTriangle(Vector2 a, Vector2 b, Vector2 c)
    {
        return math.abs((a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y)) / 2);

    }

    public static float ComputeForegroundScaling(Bounds bounds, float curriculumScale)
    {
        return curriculumScale / bounds.extents.magnitude;
    }

    [BurstCompile]
    public static float ComputeProjectedArea(
        WorldToScreenTransformer transformer, Vector3 position, Quaternion rotation, Bounds meshBounds, float scale = 1f)
    {
        return ComputeProjectedArea(transformer, position, rotation, scale * Vector3.one, meshBounds);
    }


    [BurstCompile]
    unsafe public static float ComputeProjectedArea(
        WorldToScreenTransformer transformer, Vector3 position, Quaternion rotation, Vector3 scale, Bounds meshBounds)
    {
        var zMin = Single.MaxValue;
        var closestIdx = -1;
        var projectedVertices = stackalloc Vector2[8];
        projectedVertices[0] = ProjectBoundsVertex(transformer, new Vector3(-1, -1, -1), 
            position, rotation, meshBounds, scale, 0, ref zMin, ref closestIdx);
        projectedVertices[1] = ProjectBoundsVertex(transformer, new Vector3(-1, -1, 1),  
            position, rotation, meshBounds, scale, 1, ref zMin, ref closestIdx);
        projectedVertices[2] = ProjectBoundsVertex(transformer, new Vector3(-1, 1, -1),  
            position, rotation, meshBounds, scale, 2, ref zMin, ref closestIdx);
        projectedVertices[3] = ProjectBoundsVertex(transformer, new Vector3(-1, 1, 1),   
            position, rotation, meshBounds, scale, 3, ref zMin, ref closestIdx);
        projectedVertices[4] = ProjectBoundsVertex(transformer, new Vector3(1, -1, -1),  
            position, rotation, meshBounds, scale, 4, ref zMin, ref closestIdx);
        projectedVertices[5] = ProjectBoundsVertex(transformer, new Vector3(1, -1, 1),   
            position, rotation, meshBounds, scale, 5, ref zMin, ref closestIdx);
        projectedVertices[6] = ProjectBoundsVertex(transformer, new Vector3(1, 1, -1),   
            position, rotation, meshBounds, scale, 6, ref zMin, ref closestIdx);
        projectedVertices[7] = ProjectBoundsVertex(transformer, new Vector3(1, 1, 1),    
            position, rotation, meshBounds, scale, 7, ref zMin, ref closestIdx);
        if(closestIdx == -1)
            Assert.AreNotEqual(-1, closestIdx);

        var neighborMap = stackalloc int[]
        {
            // 0
            1, 2, 4,
            // 1
            0, 3, 5,
            // 2
            0, 3, 6,
            // 3
            1, 2, 7,
            // 4
            0, 5, 6,
            // 5
            1, 4, 7,
            // 6
            2, 4, 7,
            // 7
            3, 5, 6
        };

        //  Compute the projected surface area of each bounds tri facing the camera
        var closestPoint = projectedVertices[closestIdx];
        var neighborStartIdx = closestIdx * 3;
        var totalArea = 0f;
        var trisAdded = 0;
        for (var i = 0; i < 2; i++)
        {
            var neighborA = projectedVertices[neighborMap[neighborStartIdx + i]];
            for (var j = i + 1; j < 3; j++)
            {
                var neighborB = projectedVertices[neighborMap[neighborStartIdx + j]];
                totalArea += ComputeAreaOfTriangle(closestPoint, neighborA, neighborB) * 2;
                trisAdded += 2;
            }
        }

        Assert.AreEqual(6, trisAdded);
        return totalArea;
    }

    static Vector2 ProjectBoundsVertex(
        WorldToScreenTransformer transformer, Vector3 direction, Vector3 position, Quaternion rotation, Bounds meshBounds,
        Vector3 scale, int idx, ref float zMin, ref int closestIdx)
    {
        var boundsVertex = Vector3.Scale(Vector3.Scale(direction, scale), meshBounds.extents);
        var vertex = (rotation * boundsVertex);
        if (vertex.z < zMin)
        {
            zMin = vertex.z;
            closestIdx = idx;
        }

        return transformer.TransformPoint(vertex + position);
    }

    internal static float ComputeScaleToMatchArea(
        WorldToScreenTransformer transformer, Vector3 position, Quaternion rotation, Bounds bounds, float projectedAreaTarget)
    {
        return math.sqrt(projectedAreaTarget / ComputeProjectedArea(transformer, position, rotation, bounds));
    }

    public static void CreateRandomizedHue(MaterialPropertyBlock materialPropertyBlock, float maxRandomization, ref Random random)
    {
        var amount = random.NextFloat(min:-maxRandomization, max:maxRandomization);
        materialPropertyBlock.SetFloat("_HueOffset", value: amount);
    }

    public static NativeArray<Quaternion> GenerateInPlaneRotationCurriculum(Allocator allocator)
    {
        NativeArray<Quaternion> inRotations = new NativeArray<Quaternion>(36, allocator);
        for (int i = 0; i < 36; i++)
        {
            inRotations[i] = Quaternion.AngleAxis(i * 10, Vector3.forward);
        }
        return inRotations;
    }

    // This returns all rotations for the vertices of an Icosahedron, where the center of each edge is an added vertex. See https://www.labri.fr/perso/vlepetit/pubs/hinterstoisser_bmvc08.pdf
    public static NativeArray<Quaternion> GenerateOutOfPlaneRotationCurriculum(Allocator allocator)
    {
        return GenerateIcosahedronRotations(allocator);
    }
    
    // This returns all rotations for the vertices of an Icosaheron https://en.wikipedia.org/wiki/Icosahedron
    static NativeArray<Quaternion> GenerateIcosahedronRotations(Allocator allocator)
    {
        var outRotations = new NativeArray<Quaternion>(12, allocator);
        outRotations[0] = Quaternion.Euler(0, 0, 0f);
        for (int y = 0; y < 10; y++)
            outRotations[y + 1] = Quaternion.Euler(y % 2 == 0 ? 60 : 120, y * 36, 0f);
        
        outRotations[11] = Quaternion.Euler(180, 0, 0f);
        
        return outRotations;
    }

    // ReSharper disable once UnusedMember.Local
    static NativeArray<Quaternion> GenerateSubdividedIcosahedronRotations(Allocator allocator)
    {
        var outRotations = new NativeArray<Quaternion>(42, allocator);
        int y = 0;

        //two rows of vertices, one at 60 deg from top and one from 120 deg from top, each with 10 vertices each
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(60, s * 36, 0f);
            outRotations[y + 1] = Quaternion.Euler(120, s * 36, 0f);
            y += 2;
        }

        // Center rows of vertices, half way between rows
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(90, s * 36 + 18, 0f);
            y++;
        }

        // Top and bottom rows of vertices, half way between Icosahedron vertices on 60 and 120 rows and the top/bottom vertices, respectively
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(s % 2 == 0 ? 30 : 150, y * 36, 0f);
            y++;
        }

        Debug.Assert(y == 40);

        // Top two vertices
        outRotations[40] = Quaternion.Euler(0, 0, 0f);
        outRotations[41] = Quaternion.Euler(180, 0, 0f);

        return outRotations;
    }

    public static Quaternion ComposeForegroundRotation(CurriculumState state, NativeArray<Quaternion> outOfPlaneRotations, NativeArray<Quaternion> inPlaneRotations)
    {
        var outRotate = outOfPlaneRotations[state.OutOfPlaneRotationIndex];
        var inRotate = inPlaneRotations[state.InPlaneRotationIndex];
        return inRotate * outRotate;
    }
    
    // Computes 2-D bounding region in which objects can be placed given a specified distance from the camera
    // NOTE: Assumes the camera to be positioned on the Z axis with orientation == Quaternion.Identity
    public static Rect ComputePlacementRegion(Camera camera, float placementDistance)
    {
        var position = camera.transform.position;
        var bottomLeftRay = camera.ViewportPointToRay(Vector2.zero);
        var topRightRay = camera.ViewportPointToRay(Vector2.one);
        // Solve in global space by assuming the camera is on the z-axis pointing forward
        var placementPlane = new Plane(Vector3.forward, position + placementDistance * Vector3.forward);
        placementPlane.Raycast(bottomLeftRay, out var bottomLeftDistance);
        placementPlane.Raycast(topRightRay, out var topRightDistance);
        var bottomLeftPoint = bottomLeftRay.GetPoint(bottomLeftDistance);
        var topRightPoint = topRightRay.GetPoint(topRightDistance);
        return Rect.MinMaxRect(bottomLeftPoint.x, bottomLeftPoint.y, topRightPoint.x, topRightPoint.y);
    }

    public static void SetMeshRenderersEnabledRecursive(GameObject gameObject, bool enabled)
    {
        if (gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.enabled = enabled;

        for (var i = 0; i < gameObject.transform.childCount; i++)
            SetMeshRenderersEnabledRecursive(gameObject.transform.GetChild(i).gameObject, enabled);
    }

    public static Bounds ComputeBounds(GameObject gameObject)
    {
        var bounds = ComputeBoundsUnchecked(gameObject);
        if (bounds.IsEmpty)
            throw new ArgumentException($"GameObject {gameObject.name} must have a MeshFilter in its hierarchy.");

        var result = new Bounds();
        result.SetMinMax(bounds.Min, bounds.Max);
        return result;
    }

    public static Rect IntersectRect(Rect a, Rect b)
    {
        return Rect.MinMaxRect(Math.Max(a.xMin, b.xMin), Math.Max(a.yMin, b.yMin),
            Math.Min(a.xMax, b.xMax), Math.Min(a.yMax, b.yMax));
    }

    static MinMaxAABB ComputeBoundsUnchecked(GameObject gameObject)
    {
        MinMaxAABB aabb = MinMaxAABB.Empty;
        var meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
            aabb = meshFilter.sharedMesh.bounds.ToAABB();
        else
        {
            var skinnedMesh = gameObject.GetComponent<SkinnedMeshRenderer>();
            if ( skinnedMesh != null)
            {
                aabb = skinnedMesh.sharedMesh.bounds.ToAABB();
            }
        }

        var transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var childAabb = ComputeBoundsUnchecked(transform.GetChild(i).gameObject);
            aabb.Encapsulate(childAabb);
        }

        aabb = AABB.Transform(float4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale), aabb);
        return aabb;
    }
}
