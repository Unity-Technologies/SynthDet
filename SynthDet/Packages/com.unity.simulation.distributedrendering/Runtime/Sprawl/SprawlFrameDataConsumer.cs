#if SIMULATION_SPRAWL_PRESENT

using Sprawl;
using Unity.Simulation.DistributedRendering;
using Unity.Simulation.DistributedRendering.Render;
using UnityEngine;

using Message = Sprawl.Message;

namespace Unity.Simulation.Sprawl
{
    public class SprawlFrameDataConsumer : SprawlSharedData, IFrameDataConsumer
    {
        private IInput _frames;
        
#if UNITY_SIMULATION_SPRAWL
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterWithFrameManager()
        {
            if (DistributedRenderingOptions.mode == Mode.Render)
            {
                FrameManager.Instance.RegisterRenderNodeDataConsumer(new SprawlFrameDataConsumer());
            }
        }
#endif

        public void Initialize(NodeOptions options)
        {
            var context = SimulationContext.GetInstance();
            if (!context.Initialize())
            {
                Log.E("Cannot Initialize the Sprawl SimulationContext.");
            }

            SprawlContext = context;
            SprawlConfig = SprawlContext.GetConfig();

            _frames = SprawlContext.GetInput(0);
        }

        public void OnShutdown()
        {
            Dispose();

            _frames = null;
        }

        public byte[] RequestFrame()
        {
            Message message = _frames.Get(0.1f);
            if (message == null)
            {
                SprawlContext.LogDebug("Idle.");
                return null;
            }

            SprawlContext.LogInfo("Received frame: {0}.", message.IntValues["frame_number"]);

            var data = message.StringValues["frame_data"];
            
            return Util.DecodeData(data);
        }
    }
}

#endif
