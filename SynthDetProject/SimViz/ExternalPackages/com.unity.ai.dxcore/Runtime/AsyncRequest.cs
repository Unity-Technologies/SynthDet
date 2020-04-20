using System;
using System.Collections.Concurrent;
using System.Threading;

using UnityEngine;

namespace Unity.AI.Simulation
{
    public abstract class AsyncRequest
    {
        [Flags]
        public enum Result
        {
            None      = 0,
            Completed = (1<<0),
            Error     = (1<<1) | Completed // Error implies completed
        }

        public enum ExecutionContext
        {
            None,
            ThreadPool,
            EndOfFrame
        }

        [Flags]
        protected enum State
        {
            None    = 0,
            Started = (1<<0),
            Error   = (1<<1)
        }

        protected State _state;

        protected virtual bool AnyResultHasError()
        {
            throw new NotSupportedException("AsyncRequest.AnyResultHasError called");
        }

        protected virtual bool AllResultsAreCompleted()
        {
            throw new NotSupportedException("AsyncRequest.AllResultsAreCompleted called");
        }

        public virtual void Reset()
        {
            throw new NotSupportedException("AsyncRequest.Reset called");
        }

        protected bool started
        {
            set { _state = value ? _state | State.Started : _state & ~State.Started; }
            get { return (_state & State.Started) == State.Started; }
        }

        public bool error
        {
            set { if (value) _state |= State.Error; }
            get { return (_state & State.Error) == State.Error || AnyResultHasError(); }
        }

        public bool completed
        {
            get { return started && AllResultsAreCompleted(); }
        }
    }

    public class AsyncRequest<T> : AsyncRequest, IDisposable
    {
        ConcurrentBag<Result> _results;

        public Result[] results { get { return _results.ToArray(); } }
        
        ConcurrentQueue<Func<AsyncRequest<T>, Result>> _functors;

        int _requestsInFlight;
        bool _disposed = false;

        public static AsyncRequest<T> operator +(AsyncRequest<T> a, Func<AsyncRequest<T>, Result> b)
        {
            a._functors.Enqueue(b);
            return a;
        }

        T _data;
        public ref T data { get { return ref _data; } }

        WaitCallback _waitCallback;
        
        void QueueWaitCallback(object functor)
        {
            Func<AsyncRequest<T>, Result> f = functor as Func<AsyncRequest<T>, Result>;
            Result result = Result.None;
            result = f(this);
            Debug.Assert(result != Result.None);
            _results.Add(result);
            Interlocked.Decrement(ref _requestsInFlight);
        }

        public AsyncRequest()
        {
            Reset();
        }

        ~AsyncRequest()
        {
            Dispose();
        }

        public void Start(Func<AsyncRequest<T>, Result> functor = null, ExecutionContext executionContext = ExecutionContext.ThreadPool)
        {
            Debug.Assert(functor != null);
            if (functor != null)
                _functors.Enqueue(functor);
            while (!_functors.IsEmpty)
            {
                Func<AsyncRequest<T>, Result> f;
                if (_functors.TryDequeue(out f))
                {
                    Interlocked.Increment(ref _requestsInFlight);

                    switch (executionContext)
                    {
                        case ExecutionContext.EndOfFrame:
                            DXManager.Instance.QueueEndOfFrameItem(QueueWaitCallback, f);
                            break;

                        case ExecutionContext.ThreadPool:
                        default:
                            ThreadPool.QueueUserWorkItem(_waitCallback, f);
                            break;
                    }
                }
            }
            this.started = true;
        }

        protected override bool AnyResultHasError()
        {
            if (_results == null)
                return false;
            foreach (var r in _results)
                if ((r & AsyncRequest.Result.Error) == AsyncRequest.Result.Error)
                    return true;
            return false;
        }

        protected override bool AllResultsAreCompleted()
        {
            // All results are completed if...
            // 1. The request was started.
            // 2. There are no callbacks in flight.
            // 3. All results are marked completed.
            if (!this.started)
                return false;
            if (Interlocked.Add(ref _requestsInFlight, 0) != 0)
                return false;
            Debug.Assert(_functors.IsEmpty);
            foreach (var r in _results)
                if ((r & AsyncRequest.Result.Completed) != AsyncRequest.Result.Completed)
                    return false;
            return true;
        }

        public override void Reset()
        {
            _state = State.None;
            _disposed = false;
            _waitCallback = new WaitCallback(QueueWaitCallback);
            _functors = new ConcurrentQueue<Func<AsyncRequest<T>, Result>>();
            _results = new ConcurrentBag<Result>();
            _data = default(T);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            DXManager.Instance.RecycleRequest(this);
            GC.ReRegisterForFinalize(this);
        }

        // You can use this method to complete a request without needing a lambda function.
        // Passing null will likely cause work to be skipped, but passing this will do it and complete.
        public static Result DontCare(AsyncRequest<T> r)
        {
            return Result.Completed;
        }
    }
}
