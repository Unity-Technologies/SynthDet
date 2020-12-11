using Newtonsoft.Json.Linq;
using Sprawl;

namespace Unity.Simulation.DistributedRendering
{
    public abstract class SprawlSharedData : IWorkerSharedData
    {
        protected ISimulationContext SprawlContext { get; set; }
        protected JObject SprawlConfig { get; set; }

        public virtual void Dispose()
        {
            // TODO: Figure out some way of shutting down sprawl.
        }
    }
}
