using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Unity.Simulation.DistributedRendering.Render
{
    public class StreamQueue : ConcurrentQueue<byte>
    {
        private int _offset = 0;

        public void Enqueue(byte[] bytes)
        {

            /*
             *TODO: There has GOT to be a faster way to do this.
             */
            foreach(var b in bytes)
            {
                Enqueue(b);
            }

        }

        public bool CanRead(long length)
        {
            return Count >= length;
        }

        public byte[] Read(long count)
        {
            Debug.Assert(count < Count, "Queue underflow");

            var result = new byte[count];
            byte val;
            for(var i=0; i < count; ++i)
            {
                TryDequeue(out val);
                result[i] = val;
            }

            return result;
        }

        public long ReadLong()
        {
            var bytes = new byte[sizeof(long)];
            CopyTo(bytes, 0);

            return BitConverter.ToInt64(bytes, _offset);
        }

    }
}
