using Newtonsoft.Json.Linq;

namespace Sprawl {
    interface INodeImplementationConfigurator : INodeImplementation {
        bool Configure(JObject node_config);
    }
}
