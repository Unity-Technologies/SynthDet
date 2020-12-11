using Sprawl;

public interface IExecutionFuture {
    Message GetResponse(Pipeline.NodeExecutionContext context);
}
