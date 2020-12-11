
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {
    public class PushToQueue : INodeImplementationConfigurator, INodeImplementation {

        public List<QueueEndpoint> Endpoints { get; } = new List<QueueEndpoint>();
        public int SizeLimit { get; set; } = 0;
        public EndpointAlgorithm Algorithm { get; set; } = EndpointAlgorithm.ROUND_ROBIN;

        private int count_ = 0;

        private EndpointAlgorithm ParseEndpointAlgorithm(string algorithm) {
            if ("EndpointAlgorithm.ROUND_ROBIN".Equals(algorithm)) {
                return EndpointAlgorithm.ROUND_ROBIN;
            } else if ("EndpointAlgorithm.RANDOM".Equals(algorithm)) {
                return EndpointAlgorithm.RANDOM;
            } else {
                Debug.LogError(string.Format("Unknown endpoint algorithm: {0}.", algorithm));
                return EndpointAlgorithm.ROUND_ROBIN;
            }
        }

        public bool Configure(JObject node_config) {
            SizeLimit = node_config["size_limit"].ToObject<int>();
            Algorithm = ParseEndpointAlgorithm(node_config["algorithm"].ToString());
            foreach (JObject endpoint_config in node_config["queue_endpoints"]) {
                Endpoints.Add(new QueueEndpoint(
                    endpoint_config["hostname"].ToString(),
                    endpoint_config["port"].ToObject<int>(),
                    MarshallerUtils.ParseMarshallerType(endpoint_config["marshaller"].ToString())
                ));
            }
            return true;
        }

        private QueueEndpoint GetEndpoint() {
            if (Algorithm == EndpointAlgorithm.ROUND_ROBIN) {
                return Endpoints[count_++ % Endpoints.Count];
            } else {
                return Endpoints[Random.Range(0, Endpoints.Count)];
            }
        }

        public Message Execute(Pipeline.NodeExecutionContext context) {
            while (true) {
                QueueEndpoint endpoint = GetEndpoint();
                if (SizeLimit > 0 && endpoint.QueueElementsCount(context) > SizeLimit) {
                    context.LogDebug("Queue full, waiting.");
                    Thread.Sleep(1000);
                } else {
                    context.LogDebug("Pushing to queue.");
                    endpoint.Push(context.Payload, context);
                    return null;
                }
            }
        }

        public bool Initialize(Pipeline.NodeExecutionContext context) {
            context.LogInfo("Initializing: PushToQueue.");
            foreach (QueueEndpoint queue_endpoint in Endpoints) {
                if (!queue_endpoint.Initialize()) {
                    context.LogError(string.Format("Cannot initialize queue endpoint: {0}:{1}.", queue_endpoint.Hostname, queue_endpoint.Port));
                }
            }
            return true;
        }

        public IExecutionFuture ExecuteAsync(Pipeline.NodeExecutionContext context) {
            throw new System.NotImplementedException();
        }
    }
}
