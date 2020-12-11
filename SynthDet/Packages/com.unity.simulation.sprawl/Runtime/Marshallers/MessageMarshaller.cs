namespace Sprawl {

    public interface IMessageMarshaller {
        Message Deserialize(byte[] bytes);
        byte[] Serialize(Message message);
    }
}
