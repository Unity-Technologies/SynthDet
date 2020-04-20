using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;

using NUnit.Framework;

using Unity.Simulation;

class PlayerTest : MonoBehaviour
{

#if !UNITY_EDITOR && UNITY_STANDALONE
    ScreenCaptureTestNew _new = new ScreenCaptureTestNew();
    ScreenCaptureTestOld _old = new ScreenCaptureTestOld();
    CaptureTests _cap = new CaptureTests();
    IEnumerator Start()
    {
        yield return StartCoroutine(_cap.CaptureTest_CaptureColorAsColor32_AndDepthAs16bitShort());
        yield return StartCoroutine(_cap.CaptureTest_CaptureColorAsColor32_AndDepthAs32bitFloat());
        yield return StartCoroutine(_new.CaptureScreenshotsNew_ColorOnly());
        yield return StartCoroutine(_new.CaptureScreenshotsNew_NewColorAndDepth());
        yield return StartCoroutine(_old.CaptureScreenshotsOld());
        Application.Quit();

    }
#endif
}
