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
    public readonly GameObject[] ForegroundPrefabs;
    public readonly GameObject[] BackgroundPrefabs;
    public readonly Texture2D[] BackgroundImages;
    public readonly NativeArray<Quaternion> InPlaneRotations;
    public readonly NativeArray<Quaternion> OutOfPlaneRotations;
    public readonly NativeArray<float> ScaleFactors;

    public PlacementStatics(int maxFrames, GameObject[] foreground, GameObject[] backgroundPrefabs, Texture2D[] backgroundImages, NativeArray<Quaternion> inPlaneRot, NativeArray<Quaternion> outPlaneRot, NativeArray<float> scaleFactors)
    {
        MaxFrames = maxFrames;
        ForegroundPrefabs = foreground;
        BackgroundPrefabs = backgroundPrefabs;
        InPlaneRotations = inPlaneRot;
        OutOfPlaneRotations = outPlaneRot;
        ScaleFactors = scaleFactors;
        BackgroundImages = backgroundImages;
    }
}

public static class ObjectPlacementUtilities
{
    // Directions from center of the bounding box for each vertex of bounding box
    static readonly Vector3[] k_Directions = 
    {
        new Vector3(-1, -1, -1),   //0
        new Vector3(-1, -1, 1),    //1
        new Vector3(-1, 1, -1),    //2
        new Vector3(-1, 1, 1),     //3
        new Vector3(1, -1, -1),    //4
        new Vector3(1, -1, 1),     //5
        new Vector3(1, 1, -1),     //6
        new Vector3(1, 1, 1)       //7
    };
    // Mapping from each vertex of a bounding box to the vertices that share an edge
    // NOTE: direction A is a neighbor of direction B if dot(A,B) = 1
    static readonly int[] k_NeighborMap =
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
    unsafe public static float ComputeProjectedArea(Quaternion rotation, Bounds meshBounds, float scale = 1f)
    {
        return ComputeProjectedArea(rotation, meshBounds, scale * Vector3.one);
    }

    // XXX: Computes projected area of the faces of the bounds cube that are visible to the camera instead of
    //      counting pixels - it's unclear how exactly the paper did this computation
    [BurstCompile]
    unsafe public static float ComputeProjectedArea(Quaternion rotation, Bounds meshBounds, Vector3 scale)
    {
        var zMin = Single.MaxValue;
        var closestIdx = -1;
        // TODO: this could be statically allocated
        var rotatedBoundsVertices = stackalloc Vector3[8];
        rotatedBoundsVertices[0] = ComputeBoundsVertex(new Vector3(-1, -1, -1), 
            rotation, meshBounds, scale, 0, ref zMin, ref closestIdx);
        rotatedBoundsVertices[1] = ComputeBoundsVertex(new Vector3(-1, -1, 1),  
            rotation, meshBounds, scale, 1, ref zMin, ref closestIdx);
        rotatedBoundsVertices[2] = ComputeBoundsVertex(new Vector3(-1, 1, -1),  
            rotation, meshBounds, scale, 2, ref zMin, ref closestIdx);
        rotatedBoundsVertices[3] = ComputeBoundsVertex(new Vector3(-1, 1, 1),   
            rotation, meshBounds, scale, 3, ref zMin, ref closestIdx);
        rotatedBoundsVertices[4] = ComputeBoundsVertex(new Vector3(1, -1, -1),  
            rotation, meshBounds, scale, 4, ref zMin, ref closestIdx);
        rotatedBoundsVertices[5] = ComputeBoundsVertex(new Vector3(1, -1, 1),   
            rotation, meshBounds, scale, 5, ref zMin, ref closestIdx);
        rotatedBoundsVertices[6] = ComputeBoundsVertex(new Vector3(1, 1, -1),   
            rotation, meshBounds, scale, 6, ref zMin, ref closestIdx);
        rotatedBoundsVertices[7] = ComputeBoundsVertex(new Vector3(1, 1, 1),    
            rotation, meshBounds, scale, 7, ref zMin, ref closestIdx);
        
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
        var closestPoint = rotatedBoundsVertices[closestIdx];
        var neighborStartIdx = closestIdx * 3;
        var totalArea = 0f;
        var trisAdded = 0;
        for (var i = 0; i < 2; i++)
        {
            var neighborA = rotatedBoundsVertices[neighborMap[neighborStartIdx + i]];
            for (var j = i + 1; j < 3; j++)
            {
                var neighborB = rotatedBoundsVertices[neighborMap[neighborStartIdx + j]];
                totalArea += ComputeAreaOfTriangle(closestPoint, neighborA, neighborB) * 2;
                trisAdded += 2;
            }
        }

        Assert.AreEqual(6, trisAdded);
        return totalArea;
    }

    static Vector3 ComputeBoundsVertex(Vector3 direction, Quaternion rotation, Bounds meshBounds, Vector3 scale, int idx, ref float zMin, ref int closestIdx)
    {
        var boundsVertex = Vector3.Scale(Vector3.Scale(direction, scale), meshBounds.extents);
        var vertex = (rotation * boundsVertex);
        if (vertex.z < zMin)
        {
            zMin = vertex.z;
            closestIdx = idx;
        }

        return vertex;
    }

    static internal float ComputeScaleToMatchArea(Quaternion rotation, Bounds bounds, float projectedAreaTarget)
    {
        return math.sqrt(projectedAreaTarget / ComputeProjectedArea(rotation, bounds));
    }

    // XXX: Randomizes the hue of the material's base color rather than the texture (albedo map) - on visual inspection
    //      this seems to create a result that is closer to what the paper did
    public static void CreateRandomizedHue(MaterialPropertyBlock materialPropertyBlock, ref Random random)
    {
        materialPropertyBlock.SetFloat("_HueOffset", random.NextFloat(0, 360));
    }
    
    public static NativeArray<Quaternion> GenerateInPlaneRotationCurriculum(Allocator allocator)
    {
        NativeArray<Quaternion> inRotations = new NativeArray<Quaternion>(36, allocator);
        for (int i = 1; i < 36; i++)
        {
            inRotations[i] = Quaternion.AngleAxis(i * 10, Vector3.forward);
        }
        return inRotations;
    }
    
    // This returns all rotations for the vertices of an Icosahedron, where the center of each edge is an added vertex. See https://www.labri.fr/perso/vlepetit/pubs/hinterstoisser_bmvc08.pdf
    public static NativeArray<Quaternion> GenerateOutOfPlaneRotationCurriculum(Allocator allocator)
    {
        NativeArray<Quaternion> outRotations = new NativeArray<Quaternion>(42, allocator);
        int y = 0;
        //two rows of vertices, one at 60 deg from top and one from 120 deg from top, each with 10 vertices each
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(60, s * 36, 0f);
            outRotations[y + 1] = Quaternion.Euler(120, s * 36, 0f);
            y += 2;
        }
        
        //Center rows of vertices, half way between rows
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(90, s * 36 + 18, 0f);
            y++;
        }
        
        //Top and bottom rows of vertices, half way between Icosahedron vertices on 60 and 120 rows and the top/bottom vertices, respectively
        for (int s = 0; s < 10; s++)
        {
            outRotations[y] = Quaternion.Euler(s % 2 == 0 ? 30 : 150, y * 36, 0f);
            y++;
        }
        
        Debug.Assert(y == 40);
        
        //top two vertices
        outRotations[40] = Quaternion.Euler(0, 0, 0f);
        outRotations[41] = Quaternion.Euler(180, 0, 0f);
        
        return outRotations;
    }

    public static Quaternion ComposeForegroundRotation(CurriculumState state, NativeArray<Quaternion> outOfPlaneRotations, NativeArray<Quaternion> inPlaneRotations)
    {
        var outRotate = outOfPlaneRotations[state.OutOfPlaneRotationIndex];
        var inRotate = inPlaneRotations[state.InPlaneRotationIndex];
        return outRotate * inRotate;
    }

    public static void SetMeshRenderersEnabledRecursive(GameObject gameObject, bool enabled)
    {
        if (gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.enabled = enabled;

        for (var i = 0; i < gameObject.transform.childCount; i++)
            SetMeshRenderersEnabledRecursive(gameObject.transform.GetChild(i).gameObject, enabled);
    }
}