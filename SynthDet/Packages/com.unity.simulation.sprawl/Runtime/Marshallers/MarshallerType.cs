using UnityEngine;

namespace Sprawl {

    public enum MarshallerType {
        PROTOBUF,
        JSON
    }

    public class MarshallerUtils {
        public static MarshallerType ParseMarshallerType(string marshaller) {
            if ("MarshallerType.PROTOBUF".Equals(marshaller)) {
                return MarshallerType.PROTOBUF;
            } else if ("MarshallerType.JSON".Equals(marshaller)) {
                return MarshallerType.JSON;
            } else {
                Debug.LogError(string.Format("Unknown marshaller type: {0}.", marshaller));
                return MarshallerType.PROTOBUF;
            }
        }
    }
}
