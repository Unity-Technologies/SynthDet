using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

using UnityEngine;

namespace Unity.Simulation
{
    /// <summary>
    /// Base class for data consumers to inherit from.
    /// </summary>
    public abstract class BaseDataConsumer<T>
    {
        // Public Members

        /// <summary>
        /// Implementation for Upload must be implemented in derived class.
        /// </summary>
        public virtual bool Upload(string localPath, string objectPath)
        {
            throw new NotSupportedException($"{_ServiceName()}.Upload needs implementation.");
        }

        /// <summary>
        /// Implementation for UploadAsync must be implemented in derived class.
        /// </summary>
        public virtual Task<T> UploadAsync(Stream source, string objectPath)
        {
            throw new NotSupportedException($"{_ServiceName()}.UploadAsync needs implementation.");
        }

        /// <summary>
        /// Implementation for LocalPathToObjectPath must be implemented in derived class.
        /// </summary>
        public virtual string LocalPathToObjectPath(string localPath)
        {
            throw new NotSupportedException($"{_ServiceName()}.LocalPathToObjectPath needs implementation.");
        }

        internal void Tick(float dt)
        {
            lock(_uploadQueue)
            {
                while (_uploadsInFlight.Count < kMaxSimultaneousUploads && _uploadQueue.Count > 0)
                {
                    var path = _uploadQueue.Dequeue();
                    if (!_uploadsInFlight.ContainsKey(path))
                    {
                        FileStream file = null;
                        try
                        {
                            file = File.OpenRead(path);
                        }
                        catch(Exception e)
                        {
                            Log.E($"{_ServiceName()} Failed to open the file {path} for upload. Exception: {e.Message}", kUseConsoleLog);
                            continue;
                        }

                        var task = UploadAsync(file, LocalPathToObjectPath(path));
                        if (task == null)
                        {
                            Log.E($"{_ServiceName()} failed to upload file {path} asynchronously.", kUseConsoleLog);
                            continue;
                        }
                        var uploadable = new Uploadable(path, task);
                        uploadable.source = file;
                        _uploadsInFlight.Add(path, uploadable);
                    }
                }
            }

            foreach (var kv in _uploadsInFlight)
            {
                var path       = kv.Key;
                var uploadable = kv.Value;
                Debug.Assert(typeof(Uploadable).IsValueType == false);

                switch (uploadable.task.Status)
                {
                    case TaskStatus.Canceled:
                        uploadable.Cleanup();
                        _keysToRemove.Add(path);
                        Log.V($"{_ServiceName()} upload canceled for {path}", kUseConsoleLog);
                        break;

                    case TaskStatus.Faulted:
                        if (Manager.Instance.FinalUploadsDone)
                        {
                            uploadable.Cleanup();
                            _keysToRemove.Add(path);
                            Log.I($"{_ServiceName()} Shutdown in progress, ignoring retry for {path}", kUseConsoleLog);
                            break;
                        }
                        if (++uploadable.attempts > kMaxUploadRetryAttempts)
                        {
                            uploadable.Cleanup();
                            _keysToRemove.Add(path);
                            Log.V($"{_ServiceName()} Exceeded upload attempts for {path}.", kUseConsoleLog);
                        }
                        else
                        {
                            try
                            {
                                uploadable.source = File.OpenRead(path);
                            }
                            catch(Exception e)
                            {
                                Log.E($"{_ServiceName()} Failed to open the file {path} for upload. Exception: {e.Message}.", kUseConsoleLog);
                                break;
                            }

                            uploadable.task = UploadAsync(uploadable.source, LocalPathToObjectPath(path));
                            if (uploadable.task == null)
                            {
                                Log.E($"{_ServiceName()} failed to upload {path} asynchronously.", kUseConsoleLog);
                                uploadable.Cleanup();
                                _keysToRemove.Add(path);
                                break;
                            }
                            Log.V($"{_ServiceName()} upload faulted, retrying attempt {uploadable.attempts} for {path}");
                        }
                        break;

                    case TaskStatus.RanToCompletion:
                        uploadable.Cleanup();
                        if (Options.removeLocalFilesAfterUpload && Configuration.Instance.IsSimulationRunningInCloud())
                        {
                            Log.V($"{_ServiceName()} Removing local file {path}", kUseConsoleLog);
                            File.Delete(path);
                        }
                        _keysToRemove.Add(path);
                        break;
                }
            }

            foreach (var k in _keysToRemove)
                _uploadsInFlight.Remove(k);
            _keysToRemove.Clear();
        }

        /// <summary>
        /// Consumes the object data.
        /// </summary>
        /// <param name="data">data to be consumed. Generally a string representing a path.</param>
        /// <param name="synchronous">If true, consumption will complete before returning.</param>
        public void Consume(object data, bool synchronous = false)
        {
            Debug.Assert(data.GetType() == typeof(string));

            var path = data as string;

            if (synchronous)
            {
                if (!Upload(path, LocalPathToObjectPath(path)))
                {
                    Log.E($"{_ServiceName()} failed to upload {path} synchronously.", kUseConsoleLog);
                }
            }
            else
            {
                lock(_uploadQueue)
                {
                    if (!_uploadQueue.Contains(path))
                    {
                        _uploadQueue.Enqueue(path);
                        Log.V($"{_ServiceName()} file {path} queued", kUseConsoleLog);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if there is any consumption in progress.
        /// </summary>
        public bool ConsumptionStillInProgress()
        {
            return _uploadQueue.Count != 0 || _uploadsInFlight.Count != 0;
        }

        // Non-Public Members

        protected const int  kMaxUploadRetryAttempts = 5;
        protected const int  kMaxSimultaneousUploads = 50;
        protected const bool kUseConsoleLog          = true;

        protected class Uploadable
        {
            public int      attempts;
            public string   path;
            public Task<T>  task;
            public DateTime startTime;
            public Stream   source;

            public Uploadable(string path, Task<T> task)
            {
                this.attempts  = 0;
                this.path      = path;
                this.task      = task;
                this.startTime = DateTime.UtcNow;
                this.source    = null;
            }

            public void Cleanup()
            {
                source?.Close();
                source = null;
                Log.V($"Completed upload in {(DateTime.UtcNow - startTime).Seconds} seconds.", kUseConsoleLog);
            }
        }

        protected Queue<string>                  _uploadQueue     = new Queue<string>();
        protected Dictionary<string, Uploadable> _uploadsInFlight = new Dictionary<string, Uploadable>();
        protected List<string>                   _keysToRemove    = new List<string>();

        protected BaseDataConsumer()
        {
            Manager.Instance.Tick += this.Tick;
        }

        protected string _ServiceName()
        {
            return this.GetType().Name;
        }
    }

#if UNITY_EDITOR
    // public class FakeDataConsumer : BaseDataConsumer<bool>, IDataProduced
    // {
    //     [RuntimeInitializeOnLoadMethod]
    //     static void RegisterSelfAsConsumer()
    //     {
    //         Manager.Instance.StartNotification += () =>
    //         {
    //             Manager.Instance.RegisterDataConsumer(new FakeDataConsumer());
    //         };
    //     }
    //
    //     public override bool Upload(string localPath, string objectPath)
    //     {
    //         Log.V($"Fake synchronous upload {objectPath}");
    //         return true;
    //     }
    //
    //     public override Task<bool> UploadAsync(Stream source, string objectPath)
    //     {
    //         return Task<bool>.Run(async () =>
    //         {
    //             Log.V($"Fake async upload {objectPath}");
    //             return true;
    //         });
    //     }
    //
    //     public override string LocalPathToObjectPath(string localPath)
    //     {
    //         return Path.Combine(Path.GetFileName(Path.GetDirectoryName(localPath)), Path.GetFileName(localPath));
    //     }
    //
    //     public bool Initialize()
    //     {
    //         return true;
    //     }
    // }
#endif//UNITY_EDITOR
}
