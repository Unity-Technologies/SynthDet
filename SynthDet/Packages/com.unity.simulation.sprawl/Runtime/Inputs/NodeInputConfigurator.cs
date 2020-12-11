using Newtonsoft.Json.Linq;

namespace Sprawl {
    interface INodeInputConfigurator : INodeInput {
        bool Configure(JObject input_config);
    }
}
