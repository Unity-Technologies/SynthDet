using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sprawl {

    public class Tensor {

        public enum TensorType {
            INT8 = 0,
            INT16 = 1,
            INT32 = 2,
            INT64 = 3,
            UINT8 = 4,
            UINT16 = 5,
            UINT32 = 6,
            UINT64 = 7,
            FLOAT = 8,
            DOUBLE = 9,
            BOOL = 10
        }

        public int ElementCount { get; set; } = 0;
        private int BufferSize { get; set; } = 0;

        public TensorType Type { get; set; } = TensorType.INT8;
        public int[] Dims { get; set; } = new int[0];

        public byte[] Int8 { get; private set; } = null;
        public short[] Int16 { get; private set; } = null;
        public int[] Int32 { get; private set; } = null;
        public long[] Int64 { get; private set; } = null;
        public byte[] UInt8 { get; private set; } = null;
        public ushort[] UInt16 { get; private set; } = null;
        public uint[] UInt32 { get; private set; } = null;
        public ulong[] UInt64 { get; private set; } = null;
        public bool[] Bool { get; private set; } = null;
        public float[] Float { get; private set; } = null;
        public double[] Double { get; private set; } = null;

        public Tensor() { }

        public Tensor(TensorType type, int[] dims, byte[] data = null) {
            Set(type, dims, data);
        }

        public Tensor(int[] dims, float[] data) {
            Set(TensorType.FLOAT, dims, data);
        }

        public Tensor(float[] data) {
            Set(TensorType.FLOAT, new int[] { data.Length }, data);
        }

        public Tensor(int[] dims, List<float> data) {
            Set(TensorType.FLOAT, dims, null);
            data.CopyTo(Float);
        }

        public Tensor(List<float> data) {
            Set(TensorType.FLOAT, new int[] { data.Count }, null);
            data.CopyTo(Float);
        }

        private string GetIndent(int level) {
            string indent = "";
            for (int i = 0; i < level; ++i) {
                indent += "  ";
            }
            return indent;
        }

        public string DebugString(int level = 0) {
            string indent = GetIndent(level);
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("{0}TensorType type {1}\n", indent, Type);
            builder.AppendFormat("{0}int[] dims = [{1}]\n", indent, string.Join(", ", Dims));
            return builder.ToString();
        }

        public byte[] DataToBytes() {
            byte[] bytes = new byte[BufferSize];
            switch (Type) {
            case TensorType.BOOL:
                Buffer.BlockCopy(Bool, 0, bytes, 0, BufferSize);
                break;
            case TensorType.INT8:
            case TensorType.UINT8:
                Buffer.BlockCopy(Int8, 0, bytes, 0, BufferSize);
                break;
            case TensorType.INT16:
                Buffer.BlockCopy(Int16, 0, bytes, 0, BufferSize);
                break;
            case TensorType.INT32:
                Buffer.BlockCopy(Int32, 0, bytes, 0, BufferSize);
                break;
            case TensorType.INT64:
                Buffer.BlockCopy(Int64, 0, bytes, 0, BufferSize);
                break;
            case TensorType.UINT16:
                Buffer.BlockCopy(UInt16, 0, bytes, 0, BufferSize);
                break;
            case TensorType.UINT32:
                Buffer.BlockCopy(UInt32, 0, bytes, 0, BufferSize);
                break;
            case TensorType.UINT64:
                Buffer.BlockCopy(UInt64, 0, bytes, 0, BufferSize);
                break;
            case TensorType.FLOAT:
                Buffer.BlockCopy(Float, 0, bytes, 0, BufferSize);
                break;
            case TensorType.DOUBLE:
                Buffer.BlockCopy(Double, 0, bytes, 0, BufferSize);
                break;
            }
            return bytes;
        }

        public void Set(TensorType type, int[] dims, Array data) {
            Type = type;
            Dims = dims;
            if (dims.Length == 0) {
                ElementCount = 0;
            } else {
                ElementCount = 1;
                foreach (int dim in dims) {
                    ElementCount *= dim;
                }
            }

            switch (type) {
            case TensorType.BOOL:
                Bool = new bool[ElementCount];
                BufferSize = ElementCount;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Bool, 0, BufferSize);
                }
                break;
            case TensorType.INT8:
            case TensorType.UINT8:
                Int8 = new byte[ElementCount];
                BufferSize = ElementCount;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Int8, 0, BufferSize);
                }
                break;
            case TensorType.INT16:
                Int16 = new short[ElementCount];
                BufferSize = ElementCount * 2;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Int16, 0, BufferSize);
                }
                break;
            case TensorType.INT32:
                Int32 = new int[ElementCount];
                BufferSize = ElementCount * 4;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Int32, 0, BufferSize);
                }
                break;
            case TensorType.INT64:
                Int64 = new long[ElementCount];
                BufferSize = ElementCount * 8;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Int64, 0, BufferSize);
                }
                break;
            case TensorType.UINT16:
                UInt16 = new ushort[ElementCount];
                BufferSize = ElementCount * 2;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, UInt16, 0, BufferSize);
                }
                break;
            case TensorType.UINT32:
                UInt32 = new uint[ElementCount];
                BufferSize = ElementCount * 4;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, UInt32, 0, BufferSize);
                }
                break;
            case TensorType.UINT64:
                UInt64 = new ulong[ElementCount];
                BufferSize = ElementCount * 8;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, UInt64, 0, BufferSize);
                }
                break;
            case TensorType.FLOAT:
                Float = new float[ElementCount];
                BufferSize = ElementCount * 4;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Float, 0, BufferSize);
                }
                break;
            case TensorType.DOUBLE:
                Double = new double[ElementCount];
                BufferSize = ElementCount * 8;
                if (data != null) {
                    Buffer.BlockCopy(data, 0, Double, 0, BufferSize);
                }
                break;
            }
        }
    }
}