namespace Sprawl {
    public interface INodeImplementation {
        bool Initialize(Pipeline.NodeExecutionContext context);
        Message Execute(Pipeline.NodeExecutionContext context);
        IExecutionFuture ExecuteAsync(Pipeline.NodeExecutionContext context);
    }
}