using System;
using System.Collections.Concurrent;
using System.Threading;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Base class for representing an async request. A bit like Task, but doesn't
    /// execute on the main thread like Task in Unity.
    /// </summary>
    public abstract class AsyncRequest
    {
        /// <summary>
        /// An enum describing the result status.
        /// </summary>
        [Flags]
        public enum Result
        {
            None      = 0,
            Completed = (1<<0),
            Error     = (1<<1) | Completed // Error implies completed
        }

        /// <summary>
        /// An enum describing execution context for functors that needs to be invoked
        /// ThreadPool : Invoke the callback in the threadpool.
        /// EndOfFrame : Invoke the callback at the end of the frame.
        /// Immediate : Invoke the callback immediately.
        /// </summary>
        public enum ExecutionContext
        {
            None,
            ThreadPool,
            EndOfFrame,
            Immediate
        }

        /// <summary>
        /// FLags for the _state field which holds whether or not the request has started and or has an error.
        /// </summary>
        [Flags]
        protected enum State
        {
            None    = 0,
            Started = (1<<0),
            Error   = (1<<1)
        }

        /// <summary>
        /// Property which holds whether or not the request has started and or has an error.
        /// </summary>
        protected State _state;

        /// <summary>
        /// Virtual method implemented by derived classes. Called to determine if any result had an error.
        /// </summary>
        protected virtual bool AnyResultHasError()
        {
            throw new NotSupportedException("AsyncRequest.AnyResultHasError called");
        }

        /// <summary>
        /// Virtual method implemented by derived classes. Called to determine if all results are present and completed.
        /// </summary>
        protected virtual bool AllResultsAreCompleted()
        {
            throw new NotSupportedException("AsyncRequest.AllResultsAreCompleted called");
        }

        /// <summary>
        /// Resets the request. Called when returning to the object pool.
        /// </summary>
        public virtual void Reset()
        {
            throw new NotSupportedException("AsyncRequest.Reset called");
        }


        /// <summary>
        /// Property to determine if the request has started.
        /// </summary>
        protected bool started
        {
            set { _state = value ? _state | State.Started : _state & ~State.Started; }
            get { return (_state & State.Started) == State.Started; }
        }

        /// <summary>
        /// Returns true if the request has error.
        /// </summary>
        public bool error
        {
            set { if (value) _state |= State.Error; }
            get { return (_state & State.Error) == State.Error || AnyResultHasError(); }
        }

        /// <summary>
        /// Returns true if the request is completed.
        /// </summary>
        public bool completed
        {
            get { return started && AllResultsAreCompleted(); }
        }
    }

    /// <summary>
    /// Concrete AsyncRequest for specified type T.
    /// </summary>
    public class AsyncRequest<T> : AsyncRequest, IDisposable
    {
        ConcurrentBag<Result> _results;

        /// <summary>
        /// Array of asynchronous results.
        /// </summary>
        public Result[] results { get { return _results.ToArray(); } }

        ConcurrentQueue<Func<AsyncRequest<T>, Result>> _functors;

        int _requestsInFlight;
        bool _disposed = false;

        /// <summary>
        /// Operator overload for adding functors to the AsyncRequest queue.'
        /// </summary>
        /// <param name="a">Current AsyncRequest</param>
        /// <param name="b">Functor to be added</param>
        /// <returns>AsyncRequest with updated functors queue.</returns>
        public static AsyncRequest<T> operator +(AsyncRequest<T> a, Func<AsyncRequest<T>, Result> b)
        {
            a._functors.Enqueue(b);
            return a;
        }

        T _data;

        /// <summary>
        /// Returns a reference to the payload for this request.
        /// </summary>
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

        /// <summary>
        /// Default constructor for AsyncRequest.
        /// </summary>
        public AsyncRequest()
        {
            Reset();
        }

        ~AsyncRequest()
        {
            Dispose();
        }

        /// <summary>
        /// Queues a callback that needs to be executed in the given execution context.
        /// </summary>
        /// <param name="functor">A callback that needs to be invoked</param>
        /// <param name="executionContext">Execution context in which the functor needs to be invoked. viz. Threadpool, EnoOfFrame or Immediate</param>
        public void Start(Func<AsyncRequest<T>, Result> functor = null, ExecutionContext executionContext = ExecutionContext.ThreadPool)
        {
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
                            Manager.Instance.QueueEndOfFrameItem(QueueWaitCallback, f);
                            break;

                        case ExecutionContext.Immediate:
                            QueueWaitCallback(f);
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

        /// <summary>
        /// Returns true if any of the results had an error.
        /// </summary>
        protected override bool AnyResultHasError()
        {
            if (_results == null)
                return false;
            foreach (var r in _results)
                if ((r & AsyncRequest.Result.Error) == AsyncRequest.Result.Error)
                    return true;
            return false;
        }

        /// <summary>
        /// All results are completed if...
        /// 1. The request was started.
        /// 2. There are no callbacks in flight.
        /// 3. All results are marked completed.
        /// </summary>
        protected override bool AllResultsAreCompleted()
        {
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

        /// <summary>
        /// Resets the request. Called when adding back to the object pool.
        /// </summary>
        public override void Reset()
        {
            _state = State.None;
            _disposed = false;
            _waitCallback = new WaitCallback(QueueWaitCallback);
            _functors = new ConcurrentQueue<Func<AsyncRequest<T>, Result>>();
            _results = new ConcurrentBag<Result>();
            _data = default(T);
        }

        /// <summary>
        /// Disposes the request. This will add the request back to the object pool.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Manager.Instance.RecycleRequest(this);
            GC.ReRegisterForFinalize(this);
        }

        /// <summary>
        // You can use this method to complete a request without needing a lambda function.
        // Passing null will likely cause work to be skipped, but passing this will do it and complete.
        /// </summary>
        public static Result DontCare(AsyncRequest<T> r)
        {
            return Result.Completed;
        }
    }
}
