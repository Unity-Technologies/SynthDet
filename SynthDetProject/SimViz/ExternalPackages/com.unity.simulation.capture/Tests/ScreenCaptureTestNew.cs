using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Simulation;
using Unity.Collections;

using UnityEngine.TestTools;
using NUnit.Framework;

public class ScreenCaptureTestNew : ScreenCaptureBase
{
    
#if !UNITY_EDITOR && UNITY_STANDALONE
    [UnityTest]
    public IEnumerator CaptureScreenshotsNew_ColorOnly() 
    {        
	    var requests = new List<AsyncRequest<CaptureCamera.CaptureState>>();

        SetupTest(1000, 3);

        yield return null;

        for (int i = 0; i < kNumFramesToRender; ++i)
        {
            for (int c = 0; c < _cameras.Length; ++c)
            {
                var camera = _cameras[c];

                var r = CaptureCamera.CaptureColorToFile(
                    camera,
                    GraphicsFormat.R8G8B8A8_UNorm, 
                    string.Format("test_capture_{0}_camera_{1}_color.jpg", i, c));
                    
                requests.Add(r);

                camera.Render();
            }

            yield return null;
        }

        Debug.Log("Finally, wait for any remaining requests to complete.");

        if (requests.Exists(r => r.completed == false))
            yield return null;
                
        for (var i = 0; i < requests.Count; ++i)
            if (requests[i].error)
                Debug.Log(string.Format("Request {0} returned error.", i));

        Debug.Assert(!requests.Exists(r => r.error == true));

        Debug.Log("CaptureScreenshotsNew_ColorOnly elapsed " + Elapsed());
    }


    [UnityTest]
    public IEnumerator CaptureScreenshotsNew_NewColorAndDepth() 
    {        
	    var requests = new List<AsyncRequest<CaptureCamera.CaptureState>>();

        SetupTest(1000, 3);

        yield return null;

        for (int i = 0; i < kNumFramesToRender; ++i)
        {
            for (int c = 0; c < _cameras.Length; ++c)
            {
                var camera = _cameras[c];

                var r = CaptureCamera.CaptureColorAndDepthToFile(
                    camera,
                    GraphicsFormat.R8G8B8A8_UNorm, 
                    string.Format("test_capture_{0}_camera_{1}_color.jpg", i, c),
                    default(CaptureImageEncoder.ImageFormat),
                    GraphicsFormat.R32_SFloat,
                    string.Format("test_capture_{0}_camera_{1}_depth.jpg", i, c));

                requests.Add(r);

                camera.Render();
            }

            yield return null;
        }

        Debug.Log("Finally, wait for any remaining requests to complete.");

        if (requests.Exists(r => r.completed == false))
            yield return null;
        
        Debug.Assert(!requests.Exists(r => r.error == true), "one or more requests returned an error");
        Debug.Log("CaptureScreenshotsNew_NewColorAndDepth elapsed " + Elapsed());
    }
#endif
}
