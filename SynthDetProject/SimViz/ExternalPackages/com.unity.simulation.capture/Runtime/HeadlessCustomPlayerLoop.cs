
#if UNITY_2020_1_OR_NEWER

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using System.Linq;
using System;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.IO;

public class HeadlessCustomPlayerLoop
{
    public static class HeadlessServerLoopType
    {
        public struct HeadlessServerLoopTypePostLateUpdate { };
        public struct HeadlessServerLoopTypePostLateUpdateTwo { };
    }

    public static RenderTexture headlessTexture;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
#if UNITY_SERVER
        if (headlessTexture == null)
        {
            headlessTexture = new RenderTexture(640, 480, 1);
            if (headlessTexture.Create())
            {
                Graphics.SetDefaultBackbufferSurface(headlessTexture);
            }
            else
            {
                Debug.LogError("Failed to create a render texture for default backbuffer surface");
            }
        }
        var loopSystem = GenerateCustomLoop();
        PlayerLoop.SetPlayerLoop(loopSystem);
#endif
    }

    static void Insert(ref PlayerLoopSystem playerLoopSystem, Type playerLoopType, Func<List<PlayerLoopSystem>, bool> function)
    {
        for (int i = 0; i < playerLoopSystem.subSystemList.Length; i++)
        {
            var mainSystem = playerLoopSystem.subSystemList[i];
            if (mainSystem.type == playerLoopType)
            {
                var subSystemList = new List<PlayerLoopSystem>(mainSystem.subSystemList);
                if (function(subSystemList))
                {
                    mainSystem.subSystemList = subSystemList.ToArray();
                    playerLoopSystem.subSystemList[i] = mainSystem;
                    PlayerLoop.SetPlayerLoop(playerLoopSystem);
                    return;
                }
            }
        }
    }

    private static PlayerLoopSystem GenerateCustomLoop()
    {
        var playerLoop = PlayerLoop.GetDefaultPlayerLoop();

        Insert(ref playerLoop, typeof(PostLateUpdate), (subSystemList) =>
        {
            var headlessRenderCamera = new PlayerLoopSystem();
            headlessRenderCamera.type = typeof(HeadlessServerLoopType.HeadlessServerLoopTypePostLateUpdate);
            headlessRenderCamera.updateDelegate += () =>
            {
                {
                    GL.sRGBWrite = (QualitySettings.activeColorSpace == ColorSpace.Linear);

                    var cams = Camera.allCameras;
                    var offscreen = cams.Where(x => x.targetTexture != null);
                    var nonOffcreens = cams.Where(y => y.targetTexture == null);

                    foreach (var item in offscreen)
                    {
                        if (!item.enabled) continue;

                        item.Render();
                    }

                    Graphics.SetRenderTarget(null);

                    foreach (var item in nonOffcreens)
                    {
                        if (!item.enabled) continue;
                        item.Render();
                    }

                    Graphics.SetRenderTarget(null);
                }
            };

            for (int j = 0; j < subSystemList.Count; j++)
            {
                if (subSystemList[j].type == typeof(PostLateUpdate.FinishFrameRendering))
                {
                    subSystemList.Insert(j + 1, headlessRenderCamera);
                    return true;
                }
            }

            return true;
        });

        Insert(ref playerLoop, typeof(PostLateUpdate), (subSystemList) =>
        {
            var headlessRenderCamera = new PlayerLoopSystem();
            headlessRenderCamera.type = typeof(HeadlessServerLoopType.HeadlessServerLoopTypePostLateUpdateTwo);
            headlessRenderCamera.updateDelegate += () =>
            {

            };

            for (int j = 0; j < subSystemList.Count; j++)
            {
                if (subSystemList[j].type == typeof(PostLateUpdate.BatchModeUpdate))
                {
                    subSystemList.Insert(j + 1, headlessRenderCamera);
                    return true;
                }
            }

            return true;
        });

        return playerLoop;
    }
}
#endif