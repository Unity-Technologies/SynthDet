using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;

public class CaptureTests
{
    readonly Color32 kTestColor = Color.blue;

    [UnityTest]
    public IEnumerator CaptureTest_ComputeBuffer()
    {
        const int kNumberOfFloats = 8000;

        Debug.Assert(SystemInfo.supportsComputeShaders, "Compute shaders are not supported.");

        var computeShader = Resources.Load("CaptureTestComputeBuffer") as ComputeShader;
        Debug.Assert(computeShader != null);

        var kernel = computeShader.FindKernel("CSMain");

        var input  = new ComputeBuffer(kNumberOfFloats, sizeof(float), ComputeBufferType.Default);
        computeShader.SetBuffer(kernel, "inputBuffer", input);

        var output = new ComputeBuffer(kNumberOfFloats, sizeof(float), ComputeBufferType.Default);
        computeShader.SetBuffer(kernel, "outputBuffer", output);

        var rvalue = UnityEngine.Random.Range(0, 100000);

        var floats = new float[kNumberOfFloats];
        for (var i = 0; i < floats.Length; ++i)
            floats[i] = rvalue;

        input.SetData(floats);

        computeShader.Dispatch(kernel, kNumberOfFloats/8, 1, 1);

        var request = CaptureGPUBuffer.Capture<float>(output);

        while (!request.completed)
            yield return null;

        var results = request.data as float[];

        int count = 0;
        for (var i = 0; i < results.Length; ++i)
            if (results[i] - 1 + rvalue <= Mathf.Epsilon)
                ++count;

        Debug.Assert(count == 0, "Output values are not the expected value.");   

        request.Dispose();
        input.Dispose();
        output.Dispose();
    }

    [UnityTest]
    public IEnumerator CaptureTest_RenderTexture()
    {
        const int kDimension = 4;
        const int kLength = kDimension * kDimension;

        var color = new Color32((byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), 255);

        var data = new Color32[kLength];
        for (var i = 0; i < data.Length; ++i)
            data[i] = color;

        var texture = new Texture2D(kDimension, kDimension, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.SetPixels32(data);
        texture.Apply();

        var rt = new RenderTexture(kDimension, kDimension, 0, GraphicsFormat.R8G8B8A8_UNorm);

        Graphics.Blit(texture, rt);

        var request = CaptureRenderTexture.Capture(rt);

        while (!request.completed)
            yield return null;

        var results = ArrayUtilities.Cast<Color32>(request.data as Array);

        int count = 0;
        for (var i = 0; i < kLength; ++i)
            if (!results[i].Equals(color))
                ++count;
        
        Debug.Assert(count == 0, string.Format("Output values are not the expected value. Results[0]: {0}, Color {1} " , results[0].ToString(), color.ToString()));   

        request.Dispose();
    }


    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAsColor32_AndDepthAs16bitShort()
    {
        return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                var colorBuffer = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                var depthBuffer = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);

                Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");
                Debug.Assert(EnsureDepthCloseTo(13102, depthBuffer, 100) == 0, "depthBuffer differs from expected output");   
                
            }
        );
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAsColor32_AndDepthAs32bitFloat()
    {
        return CaptureTest_CaptureColorAndDepthParametric
        (
            24, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                var colorBuffer = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                var depthBuffer = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);

                Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");
                Debug.Assert(EnsureDepthCloseTo(0.2f, depthBuffer, 0.02f) == 0, "depthBuffer differs from expected output");
            }
        );
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndDepth16_FastAndSlow_CheckConsistency()
    {
        byte[] colorBufferFast = null;
        short[] depthBufferFast = null;

        byte[] colorBufferSlow = null;
        short[] depthBufferSlow = null;

        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferFast = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferFast = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferSlow = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferSlow = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);
            }
        );

        int count = 0;
        for (var i = 0; i < colorBufferFast.Length; ++i)
        {
            if (colorBufferFast[i] != colorBufferSlow[i])
                ++count;
        }

        Debug.Assert(count == 0, "color buffers differ by " + count);

        count = 0;
        for (var i = 0; i < ArrayUtilities.Count<short>(depthBufferFast); ++i)
        {
            if (Math.Abs(depthBufferFast[i] - depthBufferSlow[i]) > 0)
                ++count;
        }

        Debug.Assert(count == 0, "depth buffers differ by " + count);
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndDepth32_FastAndSlow_CheckConsistency()
    {
        byte[] colorBufferFast = null;
        float[] depthBufferFast = null;

        byte[] colorBufferSlow = null;
        float[] depthBufferSlow = null;

        CaptureOptions.useAsyncReadbackIfSupported = true;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            32, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferFast = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferFast = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            32, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferSlow = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferSlow = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);
            }
        );

        int count = 0;
        for (var i = 0; i < colorBufferFast.Length; ++i)
        {
            if (colorBufferFast[i] != colorBufferSlow[i])
                ++count;
        }

        Debug.Assert(count == 0, "color buffers differ by " + count);

        count = 0;
        for (var i = 0; i < ArrayUtilities.Count<float>(depthBufferFast); ++i)
        {
            if (Math.Abs(depthBufferFast[i] - depthBufferSlow[i]) > 1e-6f)
                ++count;
        }

        Debug.Assert(count == 0, "depth buffers differ by " + count);
    }

    Camera SetupCameraWithRenderTexture(int width, int height, GraphicsFormat renderTextureFormat, float near = 0.1f, float far = 1000, float fov = 60)
    {
        var go = new GameObject("DataCaptureTestsCamera");
        
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = Vector3.zero;
        camera.transform.rotation = Quaternion.identity;
        camera.nearClipPlane = near;
        camera.farClipPlane = far;
        camera.fieldOfView = 45;
        camera.depthTextureMode = DepthTextureMode.Depth;
        camera.targetTexture = new RenderTexture(width, height, 0);

        return camera;
    }

    Camera SetupCameraTest(int depthBpp, GraphicsFormat renderTextureFormat, Vector3 gopos, float near = 0.1f, float far = 1000)
    {
        var camera = SetupCameraWithRenderTexture(32, 32, renderTextureFormat, near, far, 45);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = gopos;

        var renderer = cube.GetComponent<Renderer>();
        Debug.Assert(renderer != null);

        var shader = Shader.Find("Hidden/DataCaptureTestsUnlitShader");
        Debug.Assert(shader != null);

        renderer.sharedMaterial.shader = shader;
        renderer.sharedMaterial.color = kTestColor;

        return camera;
    }

    public IEnumerator CaptureTest_CaptureColorAndDepthParametric(int depthBpp, GraphicsFormat renderTextureFormat, Action<AsyncRequest<CaptureCamera.CaptureState>> validator)
    {
        Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(renderTextureFormat), "GraphicsFormat not supported");

        var camera = SetupCameraTest(depthBpp, renderTextureFormat, new Vector3(0, 0, 1.0f));

        var request = CaptureCamera.Capture(camera, colorFunctor: AsyncRequest<CaptureCamera.CaptureState>.DontCare, depthFunctor: AsyncRequest<CaptureCamera.CaptureState>.DontCare, depthFormat: GraphicsUtilities.DepthFormatForDepth(depthBpp));

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");

        validator.Invoke(request);
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorUsingNonAsyncMethod()
    {
        const int kDimension = 64;
        const int kLength = kDimension * kDimension;

        var color = kTestColor;
        
        var data = new Color32[kLength];
        for (var i = 0; i < data.Length; ++i)
            data[i] = color;

        var texture = new Texture2D(kDimension, kDimension, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.SetPixels32(data);
        texture.Apply();

        var rt = new RenderTexture(kDimension, kDimension, 0, GraphicsFormat.R8G8B8A8_UNorm);
        rt.useMipMap = false;
        rt.autoGenerateMips = false;

        Graphics.Blit(texture, rt);

        var request = Manager.Instance.CreateRequest<AsyncRequest<object>>();

        Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = (AsyncRequest<object> r) =>
        {
            var colorBuffer = GraphicsUtilities.GetPixelsSlow(rt as RenderTexture);

            Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");

            var encoded = CaptureImageEncoder.EncodeArray(colorBuffer, kDimension, kDimension, GraphicsFormatUtility.GetGraphicsFormat(rt.format, false), CaptureImageEncoder.ImageFormat.Jpg);

            //int w = 0, h = 0;
            var decoded = encoded; //CaptureImageEncoder.Decode(encoded as byte[], ref w, ref h);

            return AsyncRequest<object>.Result.Completed;
        };

        request.Start(functor, AsyncRequest<object>.ExecutionContext.EndOfFrame);

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Request had an error");
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureDepth32ToFile()
    {
        var camera = SetupCameraTest(32, GraphicsFormat.R8G8B8A8_UNorm, new Vector3(0, 0, 500.5f), 0.1f, 1000);

        var request = CaptureCamera.CaptureDepthToFile(camera, GraphicsFormat.R32_SFloat, "depth32.tga");

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureDepth16ToFile()
    {
        var camera = SetupCameraTest(16, GraphicsFormat.R8G8B8A8_UNorm, new Vector3(0, 0, 500.5f), 0.1f, 1000);

        var request = CaptureCamera.CaptureDepthToFile(camera, GraphicsFormat.R16_UNorm, "depth16.tga");

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");
    }
    
    int EnsureColorsCloseTo(Color32 exemplar, byte[] inputs, int deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; i += 4)
        {
            Color32 c;
            c.r = inputs[i+0];
            c.g = inputs[i+1];
            c.b = inputs[i+2];
            c.a = inputs[i+3];
            int rd = Math.Abs((int)exemplar.r - (int)c.r);
            int gd = Math.Abs((int)exemplar.g - (int)c.g);
            int bd = Math.Abs((int)exemplar.b - (int)c.b);
            int ad = Math.Abs((int)exemplar.a - (int)c.a);
            if (rd > deviation || gd > deviation || bd > deviation || ad > deviation)
                ++count;
        }
        return count;
    }

    int EnsureDepthCloseTo(short exemplar, short[] inputs, short deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; ++i)
        {
            var s = inputs[i];
            var d = Math.Abs(exemplar - s);
            if (d > deviation)
                ++count;
        }
        return count;
    }

    int EnsureDepthCloseTo(float exemplar, float[] inputs, float deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; ++i)
        {
            var f = inputs[i];
            var d = Mathf.Abs(exemplar - f);
            if (d > deviation)
                ++count;
        }
        return count;
    }

    bool CompareColors(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) < 1e-6f && Math.Abs(a.g - b.g) < 1e-6f && Math.Abs(a.b - b.b) < 1e-6f;
    }

    IEnumerator CaptureColorAndEnsureUpright(bool fastPath)
    {
        var camera = SetupCameraWithRenderTexture(2, 2, GraphicsFormat.R8G8B8A8_UNorm);
        camera.clearFlags = CameraClearFlags.Nothing;

        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        texture.SetPixel(0, 0, Color.black);
        texture.SetPixel(1, 0, Color.black);
        texture.SetPixel(0, 1, Color.red);
        texture.SetPixel(1, 1, Color.red);
        texture.Apply();

        Graphics.Blit(texture, camera.targetTexture);

        var imagePath = Path.Combine(Application.persistentDataPath, "upright.png");

        var useAsyncReadbackIfSupported = CaptureOptions.useAsyncReadbackIfSupported;
        CaptureOptions.useAsyncReadbackIfSupported = fastPath;

        var request = CaptureCamera.CaptureColorToFile(camera, GraphicsFormat.R8G8B8A8_UNorm, imagePath, CaptureImageEncoder.ImageFormat.Png);

        camera.Render();

        CaptureOptions.useAsyncReadbackIfSupported = useAsyncReadbackIfSupported;

        while (!request.completed)
            yield return null;

        Assert.True(request.error == false);

        texture.LoadImage(File.ReadAllBytes(imagePath));

        Assert.True(CompareColors(texture.GetPixel(0, 0), Color.black));
        Assert.True(CompareColors(texture.GetPixel(1, 0), Color.black));
        Assert.True(CompareColors(texture.GetPixel(0, 1), Color.red));
        Assert.True(CompareColors(texture.GetPixel(1, 1), Color.red));
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndEnsureUpright_FastPath()
    {
        yield return CaptureColorAndEnsureUpright(true);
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndEnsureUpright_SlowPath()
    {
        yield return CaptureColorAndEnsureUpright(false);
    }
}
