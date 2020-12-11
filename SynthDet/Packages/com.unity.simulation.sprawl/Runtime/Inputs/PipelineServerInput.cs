using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Sprawl.Proto;
using UnityEngine;

namespace Sprawl {
    public class PipelineServerInput : INodeInput {

        private class PipelineTask {
            public TaskCompletionSource<CallPipelineResponse> ResponsePromise { get; set; } = null;
            public Message Payload { get; set; } = null;

            public PipelineTask(Message payload, TaskCompletionSource<CallPipelineResponse> response_promise) {
                Payload = payload;
                ResponsePromise = response_promise;
            }
        }

        private class PipelineServiceImpl : Proto.PipelineService.PipelineServiceBase {
            private PipelineServerInput parent_ = null;
            private IMessageMarshaller marshaller_impl_ = null;

            public PipelineServiceImpl(PipelineServerInput parent, IMessageMarshaller marshaller) {
                parent_ = parent;
                marshaller_impl_ = marshaller;
            }

            public override Task<CallPipelineResponse> CallPipeline(CallPipelineRequest request, ServerCallContext context) {
                Message message = marshaller_impl_.Deserialize(request.Payload.ToByteArray());

                if (request.Blocking) {
                    TaskCompletionSource<CallPipelineResponse> response_task_promise = new TaskCompletionSource<CallPipelineResponse>();
                    parent_.Enqueue(message, response_task_promise);
                    return response_task_promise.Task;

                } else {
                    parent_.Enqueue(message);
                    return Task.FromResult<CallPipelineResponse>(new CallPipelineResponse());
                }
            }
        }

        public int Port { get; set; } = 0;
        public MarshallerType Marshaller { get; set; } = MarshallerType.PROTOBUF;
        public int MaxPendingRequests { get; set; } = 0;

        private PipelineServiceImpl pipeline_service_impl_ = null;
        private Server server_ = null;
        private MaxSizeConcurrentQueue<PipelineTask> queue_ = null;
        private TaskCompletionSource<CallPipelineResponse> response_promise_ = null;
        private IMessageMarshaller marshaller_impl_ = null;

        public PipelineServerInput(int port, MarshallerType type, int max_pending_requests) {
            Port = port;
            Marshaller = type;
            MaxPendingRequests = max_pending_requests;
            queue_ = new MaxSizeConcurrentQueue<PipelineTask>(max_pending_requests);
        }

        void Enqueue(Message message, TaskCompletionSource<CallPipelineResponse> response_promise = null) {
            queue_.Push(new PipelineTask(message, response_promise));
        }

        public bool Initialize(Pipeline.PipelineContext context) {
            marshaller_impl_ = MarshallerFactory.CreateMarshaller(Marshaller);
            pipeline_service_impl_ = new PipelineServiceImpl(this, marshaller_impl_);
            server_ = new Server(
            new[] {
                new ChannelOption(ChannelOptions.SoReuseport, 0),
                new ChannelOption(ChannelOptions.MaxConcurrentStreams, MaxPendingRequests)
            }) {
                Services = { Proto.PipelineService.BindService(pipeline_service_impl_) },
                Ports = { new ServerPort("[::]", Port, ServerCredentials.Insecure) }
            };

            try {
                server_.Start();
            } catch (IOException e) {
                context.LogError(string.Format("Failed to start pipeline server on port: {0} - {1}.", Port, e.Message));
                return false;
            }

            return true;
        }

        public void Acknowledge(Pipeline.NodeExecutionContext context, Message response) {
            if (response_promise_ != null) {
                CallPipelineResponse response_proto = new CallPipelineResponse();
                response_proto.Payload = ByteString.CopyFrom(marshaller_impl_.Serialize(response));
                response_promise_.SetResult(response_proto);
            }
        }

        private Message HandleTask(Pipeline.NodeExecutionContext context, PipelineTask task) {
            if (task.ResponsePromise != null) {
                if (response_promise_ != null) {
                    context.LogWarning("Last message was not acknowledged.");
                }
                response_promise_ = task.ResponsePromise;
            }
            return task.Payload;
        }

        public Message Get(Pipeline.NodeExecutionContext context) {
            return HandleTask(context, queue_.Get());
        }

        public Message Get(Pipeline.NodeExecutionContext context, float timeout) {
            if (queue_.Get(out PipelineTask task, timeout)) {
                return HandleTask(context, task);
            } else {
                return null;
            }
        }

        public Message TryGet(Pipeline.NodeExecutionContext context) {
            if (queue_.TryGet(out PipelineTask task)) {
                return HandleTask(context, task);
            } else {
                return null;
            }
        }
    }
}