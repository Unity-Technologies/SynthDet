using UnityEngine;

namespace Sprawl {
    public class MarshallerFactory {
        public static IMessageMarshaller CreateMarshaller(MarshallerType type) {
            if (MarshallerType.PROTOBUF.Equals(type)) {
                return new ProtocolBufferMarshaller();
            } else {
                Debug.LogError(string.Format("Unsupported marshaller: {0}.", type));
                return null;
            }
        }
    }
}
