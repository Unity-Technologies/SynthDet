using System;

using UnityEngine;

using Unity.AI.Simulation;

namespace Unity.AI.ESimSDK
{       
    public class CaptureStream : IDisposable
    {
        public const int   kDefaultBufferSize = 8192;
        public const float kDefaultMaxSecondsElapsed = 5;

        public Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor { get; set; }

        int _index;
        byte[] _buffer;
        object _sync = new object();

        int _bufferSize;
        float _maxElapsedSeconds;
        float _elapsedSeconds;

        public CaptureStream(int bufferSize = kDefaultBufferSize, float maxElapsedSeconds = kDefaultMaxSecondsElapsed, Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = null)
        {
            _index = 0;
            _bufferSize = bufferSize;
            _buffer = new byte[_bufferSize];
            _maxElapsedSeconds = maxElapsedSeconds;
            this.functor = functor;
            _elapsedSeconds = 0;
            DXManager.Instance.Tick += this.Tick;
        }

        public void Dispose()
        {
            DXManager.Instance.Tick -= this.Tick;
        }

        public void Flush()
        {
            byte[] buffer;
            lock(_sync)
            {
                buffer = _buffer;
                _buffer = new byte[_bufferSize];
                _index = 0;
                _elapsedSeconds = 0;
            }

            Consume(buffer);
        }

        void Consume(Array data)
        {
            if (functor != null)
            {
                var req = DXManager.Instance.CreateRequest<AsyncRequest<object>>();
                req.data = data;
                req.Start(functor);
            }
        }

        public void Append(byte[] data)
        {
            int length;
            int remain;

            lock(_sync)
            {
                Debug.Assert(_buffer != null);
                Debug.Assert(_index >= 0 && _index <= _buffer.Length);

                length = _buffer.Length;
                remain = _buffer.Length - _index;
            }

            if (data.Length > length)
            {
                Consume(data);
            }
            else 
            {
                if (data.Length > remain)
                    Flush();

                lock(_sync)
                {
                    Array.Copy(data, 0, _buffer, _index, data.Length);
                    _index += data.Length;
                }
            }
        }

        public void Tick(float dt)
        {
            if (_index > 0)
            {
                _elapsedSeconds += dt;
                if (_elapsedSeconds >= _maxElapsedSeconds)
                {
                    Flush();
                }
            }
        }
    }
}
