
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sprawl {

    public interface IOutput {
        void Push(Message message);
        Message PushAcknowledged(Message message);
        bool PushAcknowledged(Message message, float timeout, out Message response);
    }

    public interface IInput {
        Message Get();
        Message Get(float timeout);
        Message TryGet();
        void Acknowledge(Message response);
    }

    public interface ISharedEntity {
        Message Get();
        void Update(Message message);
    }

    public interface IReduceFunctor<T> {
        T Reduce(T[] values);
    }

    public interface ICommunicationChannel {
        int Rank();
        int Size();

        Message Scatter(int root_rank, Message scatter_message);
        Tensor Scatter(int root_rank, Tensor scatter_tensor);
        T Scatter<T>(int root_rank, T scatter_value);

        Message[] Gather(int root_rank, Message gather_message);
        Tensor[] Gather(int root_rank, Tensor gather_tensor);
        T[] Gather<T>(int root_rank, T gather_value);

        Message[] AllGather(int root_rank, Message gather_message);
        Tensor[] AllGather(int root_rank, Tensor gather_tensor);
        T[] AllGather<T>(int root_rank, T gather_value);

        Message AllReduce(int root_rank, Message gather_message, IReduceFunctor<Message> message_reduce_functor);
        Tensor AllReduce(int root_rank, Tensor gather_tensor, IReduceFunctor<Tensor> tensor_reduce_functor);
        T AllReduce<T>(int root_rank, T gather_value, IReduceFunctor<T> value_reduce_functor);

        Message Ring(Message ring_message);
        Tensor Ring(Tensor ring_tensor);
        T Ring<T>(T ring_value);

        Message Permute(int next, Message permute_message);
        Tensor Permute(int next, Tensor permute_tensor);
        T Permute<T>(int next, T permute_value);
    }

    public interface ISimulationContext {
        JObject GetConfig();

        int InputsCount();
        bool HasInput(string name);
        IInput GetInput(int index);
        IInput GetInput(string name);
        IList<KeyValuePair<string, IInput>> GetInputs();

        int OutputsCount();
        bool HasOutput(string name);
        IOutput GetOutput(int index);
        IOutput GetOutput(string name);
        IList<KeyValuePair<string, IOutput>> GetOutputs();

        int SharedEntitiesCount();
        bool HasSharedEntity(string name);
        ISharedEntity GetSharedEntity(int index);
        ISharedEntity GetSharedEntity(string name);
        IList<KeyValuePair<string, ISharedEntity>> GetSharedEntities();

        int CommunicationChannelsCount();
        bool HasCommunicationChannel(string name);
        ICommunicationChannel GetCommunicationChannel(int index);
        ICommunicationChannel GetCommunicationChannel(string name);
        IList<KeyValuePair<string, ICommunicationChannel>> GetCommunicationChannels();

        void LogDebug(string format, params object[] objects);
        void LogInfo(string format, params object[] objects);
        void LogWarning(string format, params object[] objects);
        void LogError(string format, params object[] objects);
    }
}