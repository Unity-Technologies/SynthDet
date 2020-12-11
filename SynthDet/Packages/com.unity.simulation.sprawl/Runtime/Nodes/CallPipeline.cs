using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {
    class CallPipeline : INodeImplementationConfigurator, INodeImplementation {
        public enum EndpointAlgorithm {
            ROUND_ROBIN = 0,
            RANDOM = 1
        };

        public class CallFuture : IExecutionFuture {

            private AsyncUnaryCall<Proto.CallPipelineResponse> async_call_ = null;
            private IMessageMarshaller marshaller_ = null;

            public CallFuture(AsyncUnaryCall<Proto.CallPipelineResponse> async_call, IMessageMarshaller marshaller) {
                async_call_ = async_call;
                marshaller_ = marshaller;
            }

            public Message GetResponse(Pipeline.NodeExecutionContext context) {
                Proto.CallPipelineResponse response = async_call_.GetAwaiter().GetResult();
                if (response.Payload != null) {
                    return marshaller_.Deserialize(response.Payload.ToByteArray());
                } else {
                    return null;
                }
            }
        }

        public class PipelineEndpoint {
            public string Hostname { get; set; } = "";
            public int Port { get; set; } = 0;
            public IMessageMarshaller Marshaller { get; set; } = null;

            private Proto.PipelineService.PipelineServiceClient pipeline_service_client_ = null;
            private Channel channel_ = null;

            public PipelineEndpoint(string hostname, int port, MarshallerType marshaller_type) {
                Hostname = hostname.Equals("localhost") ? "127.0.0.1" : hostname;
                Port = port;
                Marshaller = MarshallerFactory.CreateMarshaller(marshaller_type);
            }

            public CallFuture PushAsync(bool blocking, Message payload, Pipeline.NodeExecutionContext context) {
                Proto.CallPipelineRequest request = new Proto.CallPipelineRequest();
                request.Blocking = blocking;
                request.Payload = ByteString.CopyFrom(Marshaller.Serialize(payload));
                return new CallFuture(pipeline_service_client_.CallPipelineAsync(request), Marshaller);
            }

            public Message Push(bool blocking, Message payload, Pipeline.NodeExecutionContext context) {
                Proto.CallPipelineRequest request = new Proto.CallPipelineRequest();
                request.Blocking = blocking;
                request.Payload = ByteString.CopyFrom(Marshaller.Serialize(payload));
                Proto.CallPipelineResponse response = pipeline_service_client_.CallPipeline(request);
                if (response.Payload != null) {
                    return Marshaller.Deserialize(response.Payload.ToByteArray());
                } else {
                    return null;
                }
            }

            public bool Initialize() {
                channel_ = new Channel(string.Format("{0}:{1}", Hostname, Port), ChannelCredentials.Insecure);
                pipeline_service_client_ = new Proto.PipelineService.PipelineServiceClient(channel_);

                channel_.ConnectAsync().Wait();
                Debug.Log(string.Format("Connected to GRPC Pipeline Service: {0}:{1}.", Hostname, Port));

                return true;
            }

        }

        public List<PipelineEndpoint> Endpoints { get; } = new List<PipelineEndpoint>();
        public bool Blocking { get; set; } = false;
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
            Algorithm = ParseEndpointAlgorithm(node_config["algorithm"].ToString());
            Blocking = node_config["blocking"].ToObject<bool>();
            foreach (JObject endpoint_config in node_config["pipeline_endpoints"]) {
                Endpoints.Add(new PipelineEndpoint(
                    endpoint_config["hostname"].ToString(),
                    endpoint_config["port"].ToObject<int>(),
                    MarshallerUtils.ParseMarshallerType(endpoint_config["marshaller"].ToString())
                ));
            }
            return true;
        }

        private PipelineEndpoint GetEndpoint() {
            if (Algorithm == EndpointAlgorithm.ROUND_ROBIN) {
                return Endpoints[count_++ % Endpoints.Count];
            } else {
                return Endpoints[Random.Range(0, Endpoints.Count)];
            }
        }

        public Message Execute(Pipeline.NodeExecutionContext context) {
            PipelineEndpoint endpoint = GetEndpoint();
            return endpoint.Push(Blocking, context.Payload, context);
        }

        public IExecutionFuture ExecuteAsync(Pipeline.NodeExecutionContext context) {
            PipelineEndpoint endpoint = GetEndpoint();
            return endpoint.PushAsync(Blocking, context.Payload, context);
        }

        public bool Initialize(Pipeline.NodeExecutionContext context) {
            context.LogInfo("Initializing: CallPipeline.");
            foreach (PipelineEndpoint queue_endpoint in Endpoints) {
                if (!queue_endpoint.Initialize()) {
                    context.LogError(string.Format("Cannot initialize pipeline endpoint: {0}:{1}.", queue_endpoint.Hostname, queue_endpoint.Port));
                }
            }
            return true;
        }
    }
}