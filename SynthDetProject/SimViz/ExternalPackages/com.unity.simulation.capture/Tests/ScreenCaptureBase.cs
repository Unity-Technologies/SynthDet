using System;
using System.IO;
using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;

public class ScreenCaptureBase
{
    public const int kNumFramesToRender = 60;
    public const int kWidth  = 1024;
    public const int kHeight = 1024;
    
    const float kScaleFactor = 50;

    DateTime _startTime;

    GameObject _go;
    protected Camera[] _cameras;

    class RotatingCubes : MonoBehaviour
    {
        public GameObject[] _cubes;
        public void Setup(int numCubes)
        {
            _cubes = new GameObject[numCubes];
            for (var i = 0; i < numCubes; ++i)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = UnityEngine.Random.insideUnitSphere * kScaleFactor;
                cube.GetComponent<Renderer>().sharedMaterial.color = Color.red;
                _cubes[i] = cube;
            }
        }
    }

    public void SetupTest(int numCubes, int numCameras, DepthTextureMode depthTextureMode = DepthTextureMode.Depth)
    {
        Application.targetFrameRate = 10000;
        QualitySettings.vSyncCount = 0;
        
        _go = new GameObject("ScreenCaptureTest");
        
        _cameras = new Camera[numCameras];
        for (var i = 0; i < numCameras; ++i)
        {
            var go = new GameObject("Camera" + i);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = UnityEngine.Random.insideUnitSphere * 2 * kScaleFactor;
            camera.transform.LookAt(Vector3.zero);
            camera.targetTexture = new RenderTexture(kWidth, kHeight, 24, GraphicsFormat.R8G8B8A8_UNorm);
            camera.depthTextureMode = depthTextureMode;
            camera.renderingPath = RenderingPath.Forward;
            camera.nearClipPlane = 8;
            _cameras[i] = camera;
        }

        var script = _go.AddComponent<RotatingCubes>();
        script.Setup(numCubes);

        _startTime = DateTime.Now;
    }

    public double Elapsed()
    {
        TimeSpan elapsed = DateTime.Now - _startTime;
        return elapsed.TotalMilliseconds;
    }
}
