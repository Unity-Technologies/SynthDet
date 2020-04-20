using System;
using System.IO;
using System.Collections;

using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;

public class ScreenCaptureTestOld : ScreenCaptureBase
{
	private string UniqueFilename(int counter, int width, int height) 
    {
		string timeStamp = System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff");
		string combinedPath = Path.Combine(Configuration.Instance.GetStoragePath() + "/Tests/");
		if (!Directory.Exists(combinedPath))
			Directory.CreateDirectory(combinedPath);
		var filename = string.Format("{0}/screen_{1}_{2}x{3}_{4}.jpg", combinedPath, timeStamp, width, height, counter);
		return filename;
	}

	
#if !UNITY_EDITOR && UNITY_STANDALONE
	[Timeout(60000)]
	[UnityTest]
    public IEnumerator CaptureScreenshotsOld() 
    {
	    Debug.Log("Starting the screenCapture test");

		var screenShot = new Texture2D(kWidth, kHeight, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        int counter = 0;

        SetupTest(1000, 3);

	    for (int i = 0; i < kNumFramesToRender; ++i)
	    {
		    foreach (var camera in _cameras)
		    {
			    camera.Render();

			    // read pixels will read from the currently active render texture so make our offscreen
			    // render texture active and then read the pixels
			    yield return new WaitForEndOfFrame();
			    
			    RenderTexture.active = camera.targetTexture;
			    screenShot.ReadPixels(new Rect(0, 0, kWidth, kHeight), 0, 0);

			    // get our unique filename
			    string filename = UniqueFilename(counter++, kWidth, kHeight);

			    // pull in our file header/data bytes for the specified image format (has to be done from main thread)
			    byte[] fileData = screenShot.EncodeToJPG();

			    var f = System.IO.File.Create(filename);
			    f.Write(fileData, 0, fileData.Length);
			    f.Close();
		    }
		    yield return null;
	    }

	    Debug.Log("CaptureScreenshotsOld elapsed " + Elapsed());
    }
#endif
}
