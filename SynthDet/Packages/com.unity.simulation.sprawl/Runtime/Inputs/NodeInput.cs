namespace Sprawl {
    public interface INodeInput {
        bool Initialize(Pipeline.PipelineContext context);
        Message Get(Pipeline.NodeExecutionContext context);
        Message Get(Pipeline.NodeExecutionContext context, float timeout);
        Message TryGet(Pipeline.NodeExecutionContext context);
        void Acknowledge(Pipeline.NodeExecutionContext context, Message response);
    }
}
