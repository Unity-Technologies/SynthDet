using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Simulation
{
    using ConcurrentTypePool = ConcurrentDictionary<Type, ConcurrentBag<object>>;

    /// <summary>
    /// The primary manager class for the SDK.
    /// Responsible for tracking data produced, uploading it, and waiting for it to complete.
    /// </summary>
    [Obsolete("Obsolete msg -> Manager (UnityUpgradable)", true)]
    public sealed class DXManager {}

    /// <summary>
    /// DataCapture path constants.
    /// </summary>
    public struct DataCapturePaths
    {
        /// <summary>
        /// Specifies log files location.
        /// </summary>
        public readonly static string Logs = "Logs";

        /// <summary>
        /// Specifies screen capture files location.
        /// </summary>
        public readonly static string ScreenCapture = "ScreenCapture";

        /// <summary>
        /// Specifies file chunks location.
        /// </summary>
        public readonly static string Chunks = "Chunks";
    }

    /// <summary>
    /// The primary manager class for the SDK.
    /// Responsible for tracking data produced, uploading it, and waiting for it to complete.
    /// </summary>
    public sealed class Manager
    {
        internal const string kVersionString       = "v0.0.10-preview.6";
        internal const string kProfilerLogFileName = "profilerLog.raw";
        internal const string kPlayerLogFileName   = "Player.Log";
        internal const string kHeartbeatFileName   = "heartbeat.txt";

        string[] _uploadsBlackList = new string[]
        {
            kProfilerLogFileName,
            kHeartbeatFileName
        };

        private static readonly Manager _instance = new Manager();

        private static int kMaxTimeBeforeShutdown = 600;

        private float _shutdownTimer = 0;

        /// <summary>
        /// Accessor to enable/disable the profiler.
        /// </summary>
        public bool ProfilerEnabled { get; set; }

        /// <summary>
        /// Returns the path to the profiler log.
        /// </summary>
        public string ProfilerPath
        {
            get
            {
                return Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName);
            }
        }

        ConcurrentDictionary<Type, ConcurrentBag<AsyncRequest>> _requestPool = new ConcurrentDictionary<Type, ConcurrentBag<AsyncRequest>>();

        List<IDataProduced> _dataConsumers = new List<IDataProduced>();

        /// <summary>
        /// Register a consumer for the data being generated.
        /// </summary>
        /// <param name="consumer">IDataProduced consumer to be added to the list of consumers.</param>
        public void RegisterDataConsumer(IDataProduced consumer)
        {
            if (!_dataConsumers.Contains(consumer))
            {
                if (consumer != null && consumer.Initialize())
                {
                    Log.V($"Registered consumer {consumer.GetType().Name}.");
                    _dataConsumers.Add(consumer);
                }
                else
                {
                    Log.E($"Failed to register consumer {consumer.GetType().Name}. Initialize failed.");
                }
            }
        }

        /// <summary>
        /// Remove the consumer from the list of consumers
        /// </summary>
        /// <param name="consumer">IDataProduced consumer to be removed from the list.</param>
        public void UnregisterDataConsumer(IDataProduced consumer)
        {
            if (_dataConsumers.Contains(consumer))
                _dataConsumers.Remove(consumer);
        }

        /// <summary>
        /// Returns AsyncRequests pool count.
        /// </summary>
        public int requestPoolCount
        {
            get
            {
                var count = 0;
                foreach (var kv in _requestPool)
                    count += kv.Value.Count;
                return count;
            }
        }

        /// <summary>
        /// Delegate declaration for per frame ticks.
        /// </summary>
        public delegate void TickDelegate(float dt);

        /// <summary>
        /// Delegate for receiving per frame ticks.
        /// </summary>
        public TickDelegate Tick;

        /// <summary>
        /// Delegate declaration for notifications.
        /// </summary>
        public delegate void NotificationDelegate();

        /// <summary>
        /// Delegate which is called when the SDK starts.
        /// </summary>
        public NotificationDelegate StartNotification;

        /// <summary>
        /// Delegate which is called when the SDK is shutting down.
        /// </summary>
        public NotificationDelegate ShutdownNotification;

        static Forward _forward;

        static bool _shutdownRequested = false;
        static bool _finalUploadsDone = false;

        /// <summary>
        /// Returns a boolean indicating if all uploads to the cloud storage are done.
        /// </summary>
        public bool FinalUploadsDone { get { return _finalUploadsDone; } }

        double _simulationElapsedTime = 0;
        
        /// <summary>
        /// Returns Simulation time elapsed in seconds.
        /// </summary>
        public double SimulationElapsedTime
        {
            get { return _simulationElapsedTime; }
        }

        double _simulationElapsedTimeUnscaled = 0;
        
        /// <summary>
        /// Returns unscaled simulation time in seconds.
        /// </summary>
        public double SimulationElapsedTimeUnscaled
        {
            get { return _simulationElapsedTimeUnscaled; }
        }

        Stopwatch _wallElapsedTime = new Stopwatch();
        
        /// <summary>
        /// Returns Wall time elapsed in seconds.
        /// </summary>
        public double WallElapsedTime
        {
            get { return _wallElapsedTime.Elapsed.TotalSeconds; }
        }

        bool ConsumptionStillInProgress()
        {
            foreach (var consumer in _dataConsumers)
                if (consumer.ConsumptionStillInProgress())
                    return true;
            return false;
        }

        private bool readyToQuit
        {
            get
            {
                _shutdownTimer += Time.deltaTime;
                _shutdownRequested = true;
                return (_finalUploadsDone && !ConsumptionStillInProgress()) || _shutdownTimer >= kMaxTimeBeforeShutdown;
            }
        }

        static Manager()
        {}

        Manager()
        {
            _simulationElapsedTime = 0;
            _simulationElapsedTimeUnscaled = 0;
            _wallElapsedTime.Restart();

            Log.level = Log.Level.All;
            _shutdownRequested = false;
            Application.wantsToQuit += () =>
            {
                return readyToQuit;
            };

#if (PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX) && !ENABLE_IL2CPP
            InstallSigTermHandler();
            InstallSigAbortHandler();
#endif

            Directory.CreateDirectory(Configuration.Instance.GetStoragePath());
        }

        /// <summary>
        /// Singleton accessor.
        /// </summary>
        public static Manager Instance
        {
            get
            {
                return _instance;
            }
        }

        class Forward : MonoBehaviour
        {
            public Manager client { get; set; }
            void Awake()
            {
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(gameObject);
            }
            void Start()
            {
                client.Start();
            }
            void Update()
            {
                client.Update(Time.deltaTime);
            }

            public static IEnumerator QueueEndOfFrameItem(Action<object> callback, object functor)
            {
                yield return new WaitForEndOfFrame();
                callback(functor);
            }
        }

        [RuntimeInitializeOnLoadMethod]
        static void OnRuntimeMethodLoad()
        {
            var go = new GameObject("Manager");
            _forward = go.AddComponent<Forward>();
            _forward.client = Manager.Instance;
            
            if (Options.uploadFilesFromPreviousRun && Configuration.Instance.IsSimulationRunningInCloud())
            {
                Instance.UploadTracesFromPreviousRun(Configuration.Instance.GetStorageBasePath());
            }
        }

        /// <summary>
        /// Queues an action/callback to be executed at the end of the frame.
        /// </summary>
        /// <param name="callback">Callback action that needs to be invoked at the end of the frame.</param>
        /// <param name="functor">Functor that needs to be passed to the callback as an argument.</param>
        public void QueueEndOfFrameItem(Action<object> callback, object functor)
        {
            _forward.StartCoroutine(Forward.QueueEndOfFrameItem(callback, functor));
        }

        /// <summary>
        /// Inform the manager that file is produced and is ready for upload.
        /// </summary>
        /// <param name="filePath">Full path to the file on the local file system</param>
        /// <param name="synchronous">boolean indicating if the upload is to be done synchronously.</param>
        public void ConsumerFileProduced(string filePath, bool synchronous = false)
        {
            if (_finalUploadsDone)
            {
                Log.I("Shutdown in progress, ignoring file produced " + filePath);
                return;
            }

            foreach (var consumer in _dataConsumers)
                consumer.Consume(filePath, synchronous || _shutdownRequested);
        }

        internal void Start()
        {
            Log.I($"Simulation SDK {kVersionString} Started.");

            StartNotification?.Invoke();

            var chunk_size_bytes = Configuration.Instance.SimulationConfig.chunk_size_bytes;
            var chunk_timeout_ms = Configuration.Instance.SimulationConfig.chunk_timeout_ms;
            if (chunk_size_bytes > 0 && chunk_timeout_ms > 0)
            {
                ChunkedUnityLog.CaptureToFile
                (
                    Path.Combine(GetDirectoryFor(DataCapturePaths.Chunks), kPlayerLogFileName),
                    true,
                    chunk_size_bytes,
                    (float)chunk_timeout_ms / 1000.0f
                );
            }
        }

        /// <summary>
        /// Begins shutting down the SDK.
        /// Shutdown will last until all uploads or any other consumption has completed.
        /// </summary>
        public void Shutdown()
        {
            _shutdownRequested = true;
            Update(0);
        }

        internal void Update(float dt)
        {
            _simulationElapsedTime += dt;
            _simulationElapsedTimeUnscaled += Time.unscaledDeltaTime;

            Tick?.Invoke(dt);

            if (_shutdownRequested)
            {
                if (!_finalUploadsDone)
                {
                    ShutdownNotification?.Invoke();

                    if (ProfilerEnabled)
                    {
                        Log.V("Disabling the Profiler to flush it down to the file system");
                        Profiler.enabled = false;
                        Profiler.enableBinaryLog = false;
                    }

                    try
                    {
                        var profilerLog = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName);
                        if (File.Exists(profilerLog))
                        {
                            Log.V("Profiler file length: " + new FileInfo(profilerLog).Length);
                            ConsumerFileProduced(profilerLog, true);
                        }

                        if (!string.IsNullOrEmpty(Application.consoleLogPath))
                        {
                            var internalLogPath = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kPlayerLogFileName);

                            if (File.Exists(internalLogPath))
                                Log.V($"Player.Log is already present at {internalLogPath} was this execution accidentally run twice?");
                            else
                                File.Copy(Application.consoleLogPath, internalLogPath);

                            Log.V("Manager upload player log: " + Application.consoleLogPath);
                            ConsumerFileProduced(internalLogPath, true);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.V($"Exception ocurred uploading profiler/player logs: {e.ToString()}");
                    }

                    // Note: It's super important that this flag is set after the two calls to ConsumerFileProduced.
                    // If set before, they will be rejected, and if not set after, then the next frame, they will be queued again.
                    // This has the effect of preventing a shutdown indefinitely. Anything that interrupts the control flow like
                    // await can cause the setting of this flag to be deferred, allowing the next tick to queue another file.
                    _finalUploadsDone = true;
                    Log.V("Final uploads completed.");
                }

                if (readyToQuit)
                {
                    Log.V("Application quit");
                    Application.Quit();
                }
            }
        }

        bool IsFileBlacklisted(string file)
        {
            return Array.Find(_uploadsBlackList, item => item.Equals(Path.GetFileName(file))) != null;
        }

        /// <summary>
        /// Upload the data from the previous run.
        /// </summary>
        /// <param name="path">Full path to the directory containing data from the previous run.</param>
        public void UploadTracesFromPreviousRun(string path)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    if (!IsFileBlacklisted(f))
                        ConsumerFileProduced(f);
                }
            }
        }

        /// <summary>
        /// Create an instance of an AsyncRequest.
        /// </summary>
        /// <typeparam name="T">AsyncRequest type.</typeparam>
        /// <returns>AsyncRequest of type T.</returns>
        public T CreateRequest<T>() where T : AsyncRequest
        {
            ConcurrentBag<AsyncRequest> bag;
            _requestPool.TryGetValue(typeof(T), out bag);

            if (bag == null)
            {
                bag = new ConcurrentBag<AsyncRequest>();
                _requestPool[typeof(T)] = bag;
            }

            AsyncRequest request;
            if (bag.TryTake(out request))
            {
                request.Reset();
                return request as T;
            }

            return (T)Activator.CreateInstance(typeof(T));
        }

        /// <summary>
        /// Recycle the async request and put it back in the pool.
        /// </summary>
        /// <param name="request">AsyncRequest to be recycled.</param>
        /// <typeparam name="T">AsyncRequest type T.</typeparam>
        public void RecycleRequest<T>(T request) where T : AsyncRequest
        {
            _requestPool[typeof(T)].Add(request);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="userPath"></param>
        /// <returns></returns>
        public string GetDirectoryFor(string type = "", string userPath = "")
        {
            string basePath;

            if (string.IsNullOrEmpty(userPath))
            {
                basePath = Path.Combine(Configuration.Instance.GetStoragePath(), type);
            }
            else
            {
                if (Path.HasExtension(userPath))
                    basePath = Path.Combine(Path.GetDirectoryName(userPath), Configuration.Instance.GetAttemptId());
                else
                    basePath = Path.Combine(userPath, Configuration.Instance.GetAttemptId(), type.ToString().ToLower());
            }

            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            return basePath;
        }

#if (PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX) && !ENABLE_IL2CPP
        delegate void SignalHandlerDelegate();

        [DllImport ("__Internal")]
        static extern IntPtr signal(int signum, SignalHandlerDelegate handler);

        [DllImport ("__Internal")]
        static extern IntPtr signal(int signum, int foo);

        [DllImport ("__Internal")]
        static extern void abort();

        static readonly string _consoleLogPath = Application.consoleLogPath;

        SignalHandlerDelegate InstallSigTermHandler()
        {
            IntPtr previous = signal(15/*SIGTERM*/, () =>
            {
                _shutdownRequested = true;
            });
            return previous != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<SignalHandlerDelegate>(previous) : null;
        }

        static bool _abortUploadLog = false;
        SignalHandlerDelegate InstallSigAbortHandler()
        {
            if (!Configuration.Instance.IsSimulationRunningInCloud())
                return null;
            IntPtr previous = signal(6/*SIGABRT*/, () =>
            {
                if (!_abortUploadLog)
                {
                    _abortUploadLog = true;

                    if (!string.IsNullOrEmpty(_consoleLogPath))
                    {
                        foreach (var consumer in _dataConsumers)
                        {
                            if (File.Exists(_consoleLogPath))
                            {
                                var internalLogPath = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs),
                                    kPlayerLogFileName);
                                File.Copy(_consoleLogPath, internalLogPath);
                                consumer.Consume(internalLogPath, true);
                            }

                            if (ProfilerEnabled)
                            {
                                Profiler.enabled = false;
                                Profiler.enableBinaryLog = false;
                                ProfilerEnabled = false;
                            }

                            var profilerLog = Path.Combine(GetDirectoryFor(DataCapturePaths.Logs), kProfilerLogFileName);
                            if (File.Exists(profilerLog))
                                consumer.Consume(profilerLog, true);
                        }
                    }
                }
                else
                {
                    signal(6/*SIGABRT*/, 0);
                    abort();
                }
            });
            return previous != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<SignalHandlerDelegate>(previous) : null;
        }
#endif//PLATFORM_STANDALONE_OSX || PLATFORM_STANDALONE_LINUX && !ENABLE_IL2CPP
    }
}
