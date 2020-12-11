using System;
using System.IO;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public class StreamSerializer : IDisposable, IMessageSerializer
    {
        public Stream Stream { get; set; }

        private BinaryReader _reader;
        private BinaryWriter _writer;

        public StreamSerializer(Stream stream)
        {
            this.Stream = stream;

            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public int ReadInt()
        {
            return _reader.ReadInt32();
        }

        public void Write(int i)
        {
            _writer.Write(i);
        }

        public uint ReadUInt32()
        {
            return _reader.ReadUInt32();
        }

        public void Write(UInt32 val)
        {
            _writer.Write(val);
        }

        public void Write(bool val)
        {
            _writer.Write(val);
        }

        public long ReadLong()
        {
            return _reader.ReadInt64();
        }

        public void Write(long val)
        {
            _writer.Write(val);
        }

        public float ReadFloat()
        {
            return _reader.ReadSingle();
        }

        public bool ReadBool()
        {
            return _reader.ReadBoolean();
        }

        public void Write(float f)
        {
            _writer.Write(f);
        }

        public string ReadString()
        {
            return _reader.ReadString();
        }
        public void Write(string s)
        {
            _writer.Write(s);
        }

        public void Write(Vector3 v)
        {
            _writer.Write(v.x);
            _writer.Write(v.y);
            _writer.Write(v.z);
        }

        public Vector3 ReadVec3()
        {
            Vector3 result;

            result.x = ReadFloat();
            result.y = ReadFloat();
            result.z = ReadFloat();

            return result;
        }

        public void ReadTransform(Transform t)
        {
            t.localPosition = ReadVec3();
            t.localEulerAngles = ReadVec3();
            t.localScale = ReadVec3();
        }

        public void Write(Transform t)
        {
            Write(t.localPosition);
            Write(t.localEulerAngles);
            Write(t.localScale);
        }

        public TransformProxy ReadTransformProxy()
        {
            var p = new TransformProxy()
            {
                LocalPosition = ReadVec3(),
                LocalEulerAngles = ReadVec3(),
                LocalScale = ReadVec3(),
            };
            return p;
        }

        public void Write(TransformProxy proxy)
        {
            Write(proxy.LocalPosition);
            Write(proxy.LocalEulerAngles);
            Write(proxy.LocalScale);
        }


        public MessageType ReadMessageType()
        {
            return (MessageType)ReadUInt32();
        }

        public void Write(MessageType messageType)
        {
            Write((UInt32)messageType);
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _reader?.Dispose();
                    _writer?.Dispose();
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~StreamSerializer()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

    }
}
