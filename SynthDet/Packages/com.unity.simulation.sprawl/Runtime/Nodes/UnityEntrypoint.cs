using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {
    public class UnityEntrypoint : INodeImplementationConfigurator, INodeImplementation {

        public JObject Config { get; set; } = null;
        public ISimulationContext SimulationContext { get; set; } = null;

        public bool Configure(JObject node_config) {
            Config = node_config;
            return true;
        }

        public bool Initialize(Pipeline.NodeExecutionContext context) {
            context.LogInfo("Initializing: UnityEntrypoint.");
            SimulationContext = context;
            return true;
        }

        public Message Execute(Pipeline.NodeExecutionContext context) {
            throw new System.NotImplementedException();
        }

        public IExecutionFuture ExecuteAsync(Pipeline.NodeExecutionContext context) {
            throw new System.NotImplementedException();
        }
    }
}
