using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {
    class PipelineFactory {

        private interface INodeBuilder {
            INodeImplementation CreateNodeImplementationFromConfig(JObject config);
        }

        private interface INodeInputBuilder {
            INodeInput CreateNodeInputFromConfig(JObject config);
        }

        private class DefaultNodeBuilder<T> : INodeBuilder where T : INodeImplementationConfigurator, new() {
            public INodeImplementation CreateNodeImplementationFromConfig(JObject config) {
                T node = new T();
                if (!node.Configure(config)) {
                    Debug.LogError(string.Format("Cannot configure node: {0}.", typeof(T).FullName));
                    return null;
                }
                return node;
            }
        }

        private class DefaultNodeInputBuilder<T> : INodeInputBuilder where T : INodeInputConfigurator, new() {
            public INodeInput CreateNodeInputFromConfig(JObject config) {
                T input = new T();
                if (!input.Configure(config)) {
                    Debug.LogError(string.Format("Cannot configure input: {0},", typeof(T).FullName));
                    return null;
                }
                return input;
            }
        }

        private static Dictionary<string, INodeBuilder> DefaultNodeImplBuilders() {
            Dictionary<string, INodeBuilder> builders = new Dictionary<string, INodeBuilder>();
            builders.Add("UnityEntrypoint", new DefaultNodeBuilder<UnityEntrypoint>());
            builders.Add("PushToQueue", new DefaultNodeBuilder<PushToQueue>());
            builders.Add("CallPipeline", new DefaultNodeBuilder<CallPipeline>());
            return builders;
        }

        private static Dictionary<string, INodeInputBuilder> DefaultNodeInputBuilders() {
            Dictionary<string, INodeInputBuilder> builders = new Dictionary<string, INodeInputBuilder>();
            builders.Add("PullFromQueue", new DefaultNodeInputBuilder<PullFromQueue>());
            return builders;
        }

        private static readonly Dictionary<string, INodeBuilder> node_impl_builders_ = DefaultNodeImplBuilders();
        private static readonly Dictionary<string, INodeInputBuilder> node_input_builders_ = DefaultNodeInputBuilders();

        public static INodeImplementation CreateNodeImplementationFromConfig(string node_type, JObject node_config) {
            if (!node_impl_builders_.ContainsKey(node_type)) {
                Debug.LogError(string.Format("Unsupported node type: {0}.", node_type));
                return null;
            }
            return node_impl_builders_[node_type].CreateNodeImplementationFromConfig(node_config);
        }

        public static INodeInput CreateNodeInputFromConfig(string input_type, JObject input_config) {
            if (!node_input_builders_.ContainsKey(input_type)) {
                Debug.LogError(string.Format("Unsupported input type: {0}.", input_type));
                return null;
            }
            return node_input_builders_[input_type].CreateNodeInputFromConfig(input_config);
        }
    }
}