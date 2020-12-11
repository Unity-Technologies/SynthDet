using Google.Protobuf;
using System.Collections.Generic;

namespace Sprawl {
    public class ProtocolBufferMarshaller : IMessageMarshaller {

        public static Tensor.TensorType ConvertTensorType(Proto.Tensor.Types.Type type) {
            switch (type) {
            case Proto.Tensor.Types.Type.Int8:
                return Tensor.TensorType.INT8;
            case Proto.Tensor.Types.Type.Int16:
                return Tensor.TensorType.INT16;
            case Proto.Tensor.Types.Type.Int32:
                return Tensor.TensorType.INT32;
            case Proto.Tensor.Types.Type.Int64:
                return Tensor.TensorType.INT64;
            case Proto.Tensor.Types.Type.Uint8:
                return Tensor.TensorType.UINT8;
            case Proto.Tensor.Types.Type.Uint16:
                return Tensor.TensorType.UINT16;
            case Proto.Tensor.Types.Type.Uint32:
                return Tensor.TensorType.UINT32;
            case Proto.Tensor.Types.Type.Uint64:
                return Tensor.TensorType.UINT64;
            case Proto.Tensor.Types.Type.Bool:
                return Tensor.TensorType.BOOL;
            case Proto.Tensor.Types.Type.Float32:
                return Tensor.TensorType.FLOAT;
            case Proto.Tensor.Types.Type.Float64:
                return Tensor.TensorType.DOUBLE;
            case Proto.Tensor.Types.Type.Float16:
            case Proto.Tensor.Types.Type.Bfloat:
                return Tensor.TensorType.UINT16;
            }
            return Tensor.TensorType.INT32;
        }

        public static Proto.Tensor.Types.Type ConvertTensorType(Tensor.TensorType type) {
            switch (type) {
            case Tensor.TensorType.BOOL:
                return Proto.Tensor.Types.Type.Bool;
            case Tensor.TensorType.FLOAT:
                return Proto.Tensor.Types.Type.Float32;
            case Tensor.TensorType.DOUBLE:
                return Proto.Tensor.Types.Type.Float64;
            case Tensor.TensorType.INT8:
                return Proto.Tensor.Types.Type.Int8;
            case Tensor.TensorType.INT16:
                return Proto.Tensor.Types.Type.Int16;
            case Tensor.TensorType.INT32:
                return Proto.Tensor.Types.Type.Int32;
            case Tensor.TensorType.INT64:
                return Proto.Tensor.Types.Type.Int64;
            case Tensor.TensorType.UINT8:
                return Proto.Tensor.Types.Type.Uint8;
            case Tensor.TensorType.UINT16:
                return Proto.Tensor.Types.Type.Uint16;
            case Tensor.TensorType.UINT32:
                return Proto.Tensor.Types.Type.Uint32;
            case Tensor.TensorType.UINT64:
                return Proto.Tensor.Types.Type.Uint64;
            }
            return Proto.Tensor.Types.Type.Int32;
        }

        public Tensor Convert(Proto.Tensor tensor_proto) {
            Tensor tensor = new Tensor();

            int[] dims = new int[tensor_proto.Dims.Count];
            tensor_proto.Dims.CopyTo(dims, 0);

            tensor.Set(ConvertTensorType(tensor_proto.Type), dims, tensor_proto.Data.ToByteArray());
            return tensor;
        }

        public Message Convert(Proto.Message proto_message) {
            Message message = new Message();
            message.AddIntValues(proto_message.Int32Values);
            message.AddLongValues(proto_message.Int64Values);
            message.AddBoolValues(proto_message.BoolValues);
            message.AddFloatValues(proto_message.Float32Values);
            message.AddDoubleValues(proto_message.Float64Values);
            message.AddStringValues(proto_message.StringValues);

            foreach (KeyValuePair<string, Proto.Tensor> tensor in proto_message.TensorValues) {
                message.AddTensorValue(tensor.Key, Convert(tensor.Value));
            }

            foreach (KeyValuePair<string, Proto.Message> submessage in proto_message.MessageValues) {
                message.AddMessageValue(submessage.Key, Convert(submessage.Value));
            }

            return message;
        }

        public Proto.Tensor Convert(Tensor tensor) {
            Proto.Tensor proto_tensor = new Proto.Tensor();
            proto_tensor.Type = ConvertTensorType(tensor.Type);
            proto_tensor.Dims.AddRange(tensor.Dims);
            proto_tensor.Data = ByteString.CopyFrom(tensor.DataToBytes());
            return proto_tensor;
        }

        public Proto.Message Convert(Message message) {
            Proto.Message proto_message = new Proto.Message();

            proto_message.Int32Values.Add(message.IntValues);
            proto_message.Int64Values.Add(message.LongValues);
            proto_message.BoolValues.Add(message.BoolValues);
            proto_message.Float32Values.Add(message.FloatValues);
            proto_message.Float64Values.Add(message.DoubleValues);
            proto_message.StringValues.Add(message.StringValues);

            foreach (KeyValuePair<string, Tensor> tensor in message.TensorValues) {
                proto_message.TensorValues.Add(tensor.Key, Convert(tensor.Value));
            }

            foreach (KeyValuePair<string, Message> submessage in message.MessageValues) {
                proto_message.MessageValues.Add(submessage.Key, Convert(submessage.Value));
            }

            return proto_message;
        }

        public Message Deserialize(byte[] bytes) {
            Proto.Message proto_message = Proto.Message.Parser.ParseFrom(bytes);
            return Convert(proto_message);
        }

        public byte[] Serialize(Message message) {
            Proto.Message proto_message = Convert(message);
            return proto_message.ToByteArray();
        }
    }
}