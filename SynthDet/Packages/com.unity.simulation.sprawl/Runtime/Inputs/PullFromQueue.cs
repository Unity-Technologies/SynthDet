
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {
    public class PullFromQueue : INodeInputConfigurator {
        public EndpointAlgorithm Algorithm { get; private set; }
        public List<QueueEndpoint> Endpoints { get; } = new List<QueueEndpoint>();
        private int count_ = 0;

        public void Acknowledge(Pipeline.NodeExecutionContext context, Message response) {
            throw new System.NotImplementedException();
        }

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

        public bool Configure(JObject input_config) {
            Algorithm = ParseEndpointAlgorithm(input_config["algorithm"].ToString());
            foreach (JObject endpoint_config in input_config["queue_endpoints"]) {
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

        public Message Get(Pipeline.NodeExecutionContext context) {
            return Get(context, float.PositiveInfinity);
        }

        public Message Get(Pipeline.NodeExecutionContext context, float timeout) {
            float elapsed = 0.0f;
            while (true) {
                QueueEndpoint endpoint = GetEndpoint();

                Message message = endpoint.Get(context);

                if (message != null) {
                    return message;
                } else {
                    if (elapsed >= timeout) {
                        return null;
                    }
                    elapsed += 0.1f;
                    Thread.Sleep(100);
                }
            }
        }

        public bool Initialize(Pipeline.PipelineContext context) {
            context.LogInfo("Initializing: PullFromQueue.");
            foreach (QueueEndpoint queue_endpoint in Endpoints) {
                if (!queue_endpoint.Initialize()) {
                    context.LogError(string.Format("Cannot initialize queue endpoint: {0}:{1}.", queue_endpoint.Hostname, queue_endpoint.Port));
                }
            }
            return true;
        }

        public Message TryGet(Pipeline.NodeExecutionContext context) {
            return GetEndpoint().Get(context);
        }
    }
}