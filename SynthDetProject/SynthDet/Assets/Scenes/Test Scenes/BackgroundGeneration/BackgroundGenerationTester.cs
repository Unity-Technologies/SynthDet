using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Object = System.Object;
using Random = Unity.Mathematics.Random;

public class BackgroundGenerationTester : MonoBehaviour
{
    public int NumFramesPerTest = 50;
    public int numEmptyPixelsAllowed = 10;
    public float ErrorThresholdPrimitive = 0.01f;
    public float ErrorThresholdAssets = 0.05f;
    public int RandSeed = 1;
    public Material TestMaterial;
    public bool RunBackgroundFillTest = true;
    public bool RunPrimitiveScaleMatchTest = true;
    public bool RunAssetScaleMatchTest = true;
    
    private BackgroundGenerator m_backgroundGenerator;
    private BackgroundGeneratorParameters m_generatorParams;
    private EntityQuery m_CurriculumQuery;
    private int m_testStage;
    private int m_numTestsFailedInStage;
    private int m_numFramesTestedInStage;
    private int m_numFramesTestedAllStages;
    private Mesh m_cubeMesh;
    private Material m_emissiveMaterial;
    private Random m_rand;
    private Camera m_camera;
    private Rect m_textureRegion;
    private Texture2D m_cpuTexture;
    private int m_layer => m_backgroundGenerator.m_BackgroundLayer;
    private string m_frame => $"[rgb_{m_numFramesTestedAllStages + 2}]";

    // Start is called before the first m_frame update
    void Start()
    {
        m_rand = new Random((uint)RandSeed);
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<ForegroundObjectPlacer>().Enabled = false;
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<LightingRandomizerSystem>().Enabled = false;
        m_backgroundGenerator = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BackgroundGenerator>();
        m_CurriculumQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(CurriculumState));
        m_generatorParams = GetComponent<BackgroundGeneratorParameters>();
        if (m_backgroundGenerator == null)
        {
            throw new Exception("Background generator is missing - can't perform test.");
        }

        m_camera = GameObject.Find("DebugCamera").GetComponent<Camera>();
        m_camera.targetTexture = new RenderTexture(m_camera.pixelWidth, m_camera.pixelHeight, 0);
        m_textureRegion = new Rect(0, 0, m_camera.pixelWidth, m_camera.pixelHeight);
        m_cpuTexture = new Texture2D(m_camera.pixelWidth, m_camera.pixelHeight, TextureFormat.RGBA32, false);
        GameObject.Find("Canvas").GetComponent<RawImage>().texture = m_cpuTexture;

        m_testStage = RunBackgroundFillTest ? 1 : RunPrimitiveScaleMatchTest ? 2 : RunAssetScaleMatchTest ? 3 : 4;
        m_numFramesTestedInStage = 0;
        m_numTestsFailedInStage = 0;
        
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ObjectPlacementUtilities.GetMeshAndMaterial(go, out _, out m_cubeMesh);
        
        Destroy(go);
    }

    void DrawMesh(Mesh mesh, Material material, MaterialPropertyBlock properties, 
        Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var tf = Matrix4x4.TRS(position, rotation, scale);
        Graphics.DrawMesh(mesh, tf, material, m_layer, m_camera, 0, properties);
    }

    void DisableAutomaticBackgroundGeneration()
    {
        m_generatorParams.SystemEnabled = false;
        m_backgroundGenerator.m_framesWaited = 0;
        m_backgroundGenerator.m_objectCache.ResetAllObjects();
    }

    // Update is called once per m_frame
    void Update()
    {
        if (m_backgroundGenerator == null || m_testStage > 3 || !m_backgroundGenerator.m_initialized || 
            m_backgroundGenerator.m_framesWaited > 0)
            return;

        m_backgroundGenerator.m_camera = m_camera;
        m_backgroundGenerator.m_placementOrigin = new Vector3(-m_camera.aspect * m_camera.orthographicSize, -m_camera.orthographicSize, 40f);

        // For the rest of the testing stages, we will manually trigger the rendering
        if (m_testStage > 1 && m_generatorParams.SystemEnabled)
            DisableAutomaticBackgroundGeneration();
            
        
        switch (m_testStage)
        {
            //  1) Continuously regenerate backgrounds using the normal means and count the number of
            //     pixels that don't get covered
            case 1:
                if (!TestBackgroundCoverage())
                    ++m_numTestsFailedInStage;
                break;
            // 2) Generate a small set of primitive "foreground" and "background" shapes with arbitrary rotations
            //    and no occlusions or clipping. Verify that their pixels sizes are nearly identical
            case 2:
                if (!TestBackgroundScalingPrimitive())
                    ++m_numTestsFailedInStage;
                break;
            // 3) Generate a small set of arbitrarily rotated objects from the foreground/background curriculum
            //    with no occlusions or clipping and validate that they are approximately the same pixel size
            case 3:
                if (!TestBackgroundScalingAssets())
                    ++m_numTestsFailedInStage;
                break;
        }


        ++m_numFramesTestedAllStages;
        ++m_numFramesTestedInStage;
        if (m_numFramesTestedInStage >= NumFramesPerTest)
        {
            if (m_numTestsFailedInStage > 0)
            {
                Debug.LogError(
                    $"Testing stage {m_testStage} failed {m_numTestsFailedInStage} out of {m_numFramesTestedInStage} tests.");
            }

            m_numFramesTestedInStage = 0;
            m_numTestsFailedInStage = 0;
            ++m_testStage;
            if (m_testStage == 2)
            {
                for(var i = 0; i < m_backgroundGenerator.m_container.transform.childCount; ++i)
                {
                    var backgroundObject = m_backgroundGenerator.m_container.transform.GetChild(i).gameObject;
                    ObjectPlacementUtilities.GetMeshAndMaterial(backgroundObject, out var material, out var mesh);
                    if (material.shader.name != "Shader Graphs/HueShift" && mesh.name != "transparent")
                    {
                        Debug.LogWarning($"Mesh '{mesh.name}' has {material.shader.name} instead of HueShift shader.");
                    }
                }
            }

            if (m_testStage == 2 && !RunPrimitiveScaleMatchTest)
                ++m_testStage;

            if (m_testStage == 3 && !RunAssetScaleMatchTest)
                ++m_testStage;
        }
    }

    int CountPixelsWithValue(int offset, int valueExpected)
    {
        RenderTexture.active = m_backgroundGenerator.m_camera.activeTexture;
        var numMatchingPixels = 0;
        m_cpuTexture.ReadPixels(m_textureRegion, 0, 0);
        m_cpuTexture.Apply();
        var newPixelData = m_cpuTexture.GetRawTextureData();
        for (var valueIdx = offset; valueIdx < newPixelData.Length; valueIdx += 4)
        {
            if (newPixelData[valueIdx] == valueExpected)
            {
                ++numMatchingPixels;
            }
        }

        return numMatchingPixels;
    }

    void CountOpaquePixelsOnEachHalf(out int numOpaqueLeft, out int numOpaqueRight)
    {
        RenderTexture.active = m_backgroundGenerator.m_camera.activeTexture;
        numOpaqueLeft = 0;
        numOpaqueRight = 0;
        var width = m_textureRegion.width;
        m_cpuTexture.ReadPixels(m_textureRegion, 0, 0);
        m_cpuTexture.Apply();
        var newPixelData = m_cpuTexture.GetRawTextureData();
        for (var alphaIdx = 3; alphaIdx < newPixelData.Length; alphaIdx += 4)
        {
            if (newPixelData[alphaIdx] != 0)
            {
                var xValue = (alphaIdx / 4) % width;
                if (xValue < width / 2)
                {
                    ++numOpaqueLeft;
                }
                else
                {
                    ++numOpaqueRight;
                }
            }
        }
    }

    bool TestBackgroundCoverage()
    {
        var numEmptyPixels = CountPixelsWithValue(3, 0);

        Graphics.SetRenderTarget(null);
        if (numEmptyPixels > numEmptyPixelsAllowed)
        {
            Debug.LogWarning($"{m_frame} Found {numEmptyPixels} empty pixels.");
            return false;
        }
        return true;
    }

    bool TestBackgroundScalingPrimitive()
    {
        var pixelEdgeToMeters = 2 * m_backgroundGenerator.m_camera.orthographicSize /
                             m_backgroundGenerator.m_camera.pixelHeight;
        var pixelsToMetersSq = pixelEdgeToMeters * pixelEdgeToMeters;
        var cameraTransform = m_backgroundGenerator.m_camera.transform;
        var xHalfStep = m_camera.orthographicSize * m_camera.aspect / 2f;
        var leftPos = new Vector3(cameraTransform.position.x - xHalfStep, cameraTransform.position.y, 0f);
        var leftRot = m_rand.NextQuaternionRotation();
        var leftScale = new Vector3(m_rand.NextFloat(0.2f, 2f), m_rand.NextFloat(0.2f, 2f), m_rand.NextFloat(0.2f, 2f));
        var leftAreaMetersExpected =
            ObjectPlacementUtilities.ComputeProjectedArea(leftRot, m_cubeMesh.bounds, leftScale);
        var rightPos = leftPos + new Vector3(xHalfStep * 2f, 0f, 0f);
        var rightRot = m_rand.NextQuaternionRotation();
        var rightScale = Vector3.one * 
            ObjectPlacementUtilities.ComputeScaleToMatchArea(rightRot, m_cubeMesh.bounds, leftAreaMetersExpected);
        var leftColor = new MaterialPropertyBlock();
        leftColor.SetColor("_BaseColor", Color.blue);
        var rightColor = new MaterialPropertyBlock();
        rightColor.SetColor("_BaseColor", Color.red);
        DrawMesh(m_cubeMesh, TestMaterial, leftColor, leftPos, leftRot, leftScale);
        DrawMesh(m_cubeMesh, TestMaterial, rightColor, rightPos, rightRot, rightScale);
        m_backgroundGenerator.m_camera.Render();

        CountOpaquePixelsOnEachHalf(out var numBlue, out var numRed);
        if (numBlue == 0 || numRed == 0)
        {
            Debug.LogWarning($"{m_frame} Zero pixels counted (blue: {numBlue}, red: {numRed})");
            return false;
        }

        var pixelDisparityAbsolute = math.abs(numBlue - numRed);
        var pixelDisparityRatio = pixelDisparityAbsolute / numBlue;
        var testHasPassed = true;
        var areaLeft = numBlue * pixelsToMetersSq;
        var areaRight = numRed * pixelsToMetersSq;
        if (pixelDisparityRatio > ErrorThresholdPrimitive)
        {
            Debug.LogWarning($"{m_frame} Pixel Disparity! {pixelDisparityRatio} difference, or {pixelDisparityAbsolute} pixels - " +
                             $"Red has {numRed} pixels, Blue has {numBlue} pixels at scale {leftScale}");
            testHasPassed = false;
        }
        if (math.abs(leftAreaMetersExpected - areaLeft) / leftAreaMetersExpected > ErrorThresholdPrimitive)
        {
            Debug.LogWarning($"{m_frame} Predicted area {leftAreaMetersExpected} did not match measured area {areaLeft}");
            testHasPassed = false;
        }
        if (math.abs(areaRight - leftAreaMetersExpected) / leftAreaMetersExpected > ErrorThresholdPrimitive)
        {
            Debug.LogWarning($"{m_frame} Left area was correct ({leftAreaMetersExpected}), " +
                             $"but right area was not correctly scaled to match ({areaRight})");
            testHasPassed = false;
        }

        return testHasPassed;
    }
    
    bool TestBackgroundScalingAssets()
    {
        var entity = m_CurriculumQuery.GetSingletonEntity();
        var statics = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<PlacementStatics>(entity);
        var arbitraryScale = m_rand.NextFloat(0.9f, 1.5f);
        var cameraTransform = m_backgroundGenerator.m_camera.transform;
        var xHalfStep = m_camera.orthographicSize * m_camera.aspect / 2f;
        var leftCenter = new Vector3(cameraTransform.position.x - xHalfStep, cameraTransform.position.y, 0f);
        var leftRotate = m_rand.NextQuaternionRotation();
        var rightCenter = leftCenter + new Vector3(xHalfStep * 2, 0f, 0f);
        var rightRotate = m_rand.NextQuaternionRotation();
        var numObjects = statics.BackgroundPrefabs.Length;
        var leftObject = statics.BackgroundPrefabs[m_rand.NextInt(numObjects)];
        ObjectPlacementUtilities.GetMeshAndMaterial(leftObject, out var leftMat, out var leftMesh);
        var rightObject = statics.BackgroundPrefabs[m_rand.NextInt(numObjects)];
        ObjectPlacementUtilities.GetMeshAndMaterial(rightObject, out var rightMat, out var rightMesh);
        var leftArea = ObjectPlacementUtilities.ComputeProjectedArea(leftRotate, leftMesh.bounds, arbitraryScale);
        var rightScale = ObjectPlacementUtilities.ComputeScaleToMatchArea(rightRotate, rightMesh.bounds, leftArea);
        DrawMesh(leftMesh, leftMat, default, leftCenter, leftRotate, arbitraryScale * Vector3.one);
        DrawMesh(rightMesh, rightMat, default, rightCenter, rightRotate, rightScale * Vector3.one);
        m_backgroundGenerator.m_camera.Render();
        CountOpaquePixelsOnEachHalf(out var numLeft, out var numRight);
        var pixelDisparityAbsolute = math.abs(numRight - numLeft);
        var pixelDisparityRatio = 2f * pixelDisparityAbsolute / (numRight + numLeft);
        if (pixelDisparityRatio > ErrorThresholdAssets)
        {
            Debug.LogWarning($"{m_frame} Pixel Disparity! {pixelDisparityRatio} difference, or {pixelDisparityAbsolute} pixels - " +
                             $"Right has {numRight} pixels, Left has {numLeft} pixels at scale {arbitraryScale}");
            return false;
        }

        return true;
    }
}
