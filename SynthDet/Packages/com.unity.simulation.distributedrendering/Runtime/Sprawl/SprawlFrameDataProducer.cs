#if SIMULATION_SPRAWL_PRESENT
using System;
using System.Collections.Generic;
using Sprawl;
using Unity.Simulation.DistributedRendering;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

using Message = Sprawl.Message;

public class SprawlFrameDataProducer : SprawlSharedData, IFrameDataProducer
{
    private IOutput _renderers;

#if UNITY_SIMULATION_SPRAWL
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void SetNodeOption()
    {
        if (!SimulationContext.GetInstance().Initialize()) {
            Debug.LogError("Cannot Initialize the SimulationContext.");
        }

        var context = SimulationContext.GetInstance();
        var config = context.GetConfig();
        var nodeType = config?["config"]?["renderType"]?.ToString() ?? "";

        Debug.Log("Setting the render option..");
        switch (nodeType)
        {
            case "Physics":
                Log.I("Setting RenderNodeOption to Physics...");
                DistributedRenderingOptions.mode = Mode.Physics;
                break;
            case "Render":
                Log.I("Setting RenderNodeOption to Render...");
                DistributedRenderingOptions.mode = Mode.Render;
                break;
            default:
                DistributedRenderingOptions.mode = Mode.None;
                break;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void RegisterWithFrameManager()
    {
        if (DistributedRenderingOptions.mode == Mode.Physics)
        {
            FrameManager.Instance.RegisterSceneServerDataProducer(new SprawlFrameDataProducer());
        }
    }
#endif
    
    public void Initialize(NodeOptions options)
    {
        SprawlContext = SimulationContext.GetInstance();
        SprawlConfig = SprawlContext.GetConfig();
        _renderers = SprawlContext.GetOutput(0);
    }

    public void Consume(byte[] data)
    {
        Message message = new Message();
        message.StringValues["frame_data"] = Util.EncodeData(data);
        message.IntValues["frame_number"] = Time.frameCount;
        _renderers.Push(message);
    }

    public bool ReadyToQuitAfterFramesProduced()
    {
        return true;
    }

    public void OnShutdown(SimulationStats stats)
    {
        Log.I("SceneServer Stats: "  + JsonUtility.ToJson(stats));
        Log.I("Done Pushing frames to Message queue..");
        Message message = new Message();
        var b0 = BitConverter.GetBytes((long) sizeof(MessageType));
        var b1 = BitConverter.GetBytes((UInt32) MessageType.EndSimulation);
        var bytes = new List<byte>();
        bytes.AddRange(b0);
        bytes.AddRange(b1);
        message.StringValues["frame_data"] = Util.EncodeData(bytes.ToArray());
        message.IntValues["frame_number"] = Time.frameCount;
        _renderers.Push(message);
        Dispose();
    }
}

#endif
